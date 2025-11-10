// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Processor;
using Azure.Storage.Blobs;
using Honua.Integration.Azure.Configuration;
using Honua.Integration.Azure.ErrorHandling;
using Honua.Integration.Azure.Health;
using Honua.Integration.Azure.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Integration.Azure.Services;

/// <summary>
/// Background service that consumes messages from Azure IoT Hub via Event Hub
/// and ingests them into the SensorThings API
/// </summary>
public sealed class AzureIoTHubConsumerService : BackgroundService
{
    private readonly IOptionsMonitor<AzureIoTHubOptions> _options;
    private readonly IIoTHubMessageParser _messageParser;
    private readonly ISensorThingsMapper _sensorThingsMapper;
    private readonly IDeadLetterQueueService _deadLetterQueue;
    private readonly ILogger<AzureIoTHubConsumerService> _logger;

    private EventProcessorClient? _processor;
    private readonly IoTHubConsumerHealthStatus _healthStatus = new();

    public AzureIoTHubConsumerService(
        IOptionsMonitor<AzureIoTHubOptions> options,
        IIoTHubMessageParser messageParser,
        ISensorThingsMapper sensorThingsMapper,
        IDeadLetterQueueService deadLetterQueue,
        ILogger<AzureIoTHubConsumerService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _messageParser = messageParser ?? throw new ArgumentNullException(nameof(messageParser));
        _sensorThingsMapper = sensorThingsMapper ?? throw new ArgumentNullException(nameof(sensorThingsMapper));
        _deadLetterQueue = deadLetterQueue ?? throw new ArgumentNullException(nameof(deadLetterQueue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get current health status for health checks
    /// </summary>
    public IoTHubConsumerHealthStatus GetHealthStatus() => _healthStatus;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;

        if (!opts.Enabled)
        {
            _logger.LogInformation("Azure IoT Hub consumer is disabled");
            return;
        }

        try
        {
            opts.Validate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid Azure IoT Hub configuration");
            _healthStatus.IsHealthy = false;
            _healthStatus.LastError = ex.Message;
            return;
        }

        _logger.LogInformation(
            "Starting Azure IoT Hub consumer (Consumer Group: {ConsumerGroup})",
            opts.ConsumerGroup);

        try
        {
            // Create blob container client for checkpointing
            var storageClient = new BlobContainerClient(
                opts.CheckpointStorageConnectionString,
                opts.CheckpointContainerName);

            await storageClient.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

            // Create Event Processor Client
            _processor = CreateEventProcessorClient(opts, storageClient);

            // Register event handlers
            _processor.ProcessEventAsync += ProcessEventHandler;
            _processor.ProcessErrorAsync += ProcessErrorHandler;

            // Start processing
            await _processor.StartProcessingAsync(stoppingToken);

            _logger.LogInformation("Azure IoT Hub consumer started successfully");
            _healthStatus.IsHealthy = true;
            _healthStatus.LastStartTime = DateTime.UtcNow;

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Azure IoT Hub consumer is stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Azure IoT Hub consumer");
            _healthStatus.IsHealthy = false;
            _healthStatus.LastError = ex.Message;
            throw;
        }
        finally
        {
            if (_processor != null)
            {
                try
                {
                    await _processor.StopProcessingAsync(CancellationToken.None);
                    _logger.LogInformation("Azure IoT Hub consumer stopped");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping Event Processor");
                }
            }
        }
    }

    private EventProcessorClient CreateEventProcessorClient(
        AzureIoTHubOptions opts,
        BlobContainerClient storageClient)
    {
        // Use connection string or Managed Identity
        if (!string.IsNullOrWhiteSpace(opts.EventHubConnectionString))
        {
            return new EventProcessorClient(
                storageClient,
                opts.ConsumerGroup,
                opts.EventHubConnectionString);
        }
        else
        {
            var credential = new DefaultAzureCredential();
            return new EventProcessorClient(
                storageClient,
                opts.ConsumerGroup,
                opts.EventHubNamespace!,
                opts.EventHubName!,
                credential);
        }
    }

    private async Task ProcessEventHandler(ProcessEventArgs args)
    {
        var opts = _options.CurrentValue;

        try
        {
            if (args.Data == null)
                return;

            _healthStatus.LastMessageTime = DateTime.UtcNow;
            _healthStatus.TotalMessagesReceived++;

            // Parse IoT Hub message
            var message = await _messageParser.ParseMessageAsync(args.Data, args.CancellationToken);

            _logger.LogDebug(
                "Received message from device {DeviceId} (Partition: {Partition}, Offset: {Offset})",
                message.DeviceId,
                args.Partition.PartitionId,
                args.Data.Offset);

            // Process message and create observations
            var result = await _sensorThingsMapper.ProcessMessagesAsync(
                new[] { message },
                args.CancellationToken);

            if (result.FailureCount > 0)
            {
                _logger.LogWarning(
                    "Failed to process message from device {DeviceId}: {Errors}",
                    message.DeviceId,
                    string.Join(", ", result.Errors.Select(e => e.Message)));

                // Add to dead letter queue
                if (opts.ErrorHandling.EnableDeadLetterQueue)
                {
                    var deadLetterMessage = new DeadLetterMessage
                    {
                        OriginalMessage = message,
                        Error = result.Errors[0],
                        AttemptCount = 1
                    };

                    await _deadLetterQueue.AddToDeadLetterQueueAsync(deadLetterMessage, args.CancellationToken);
                }

                _healthStatus.TotalMessagesFailed++;
            }
            else
            {
                _healthStatus.TotalMessagesProcessed++;
                _healthStatus.TotalObservationsCreated += result.ObservationsCreated;
                _healthStatus.ConsecutiveErrors = 0;
            }

            // Update checkpoint
            await args.UpdateCheckpointAsync(args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event from partition {Partition}", args.Partition.PartitionId);

            _healthStatus.ConsecutiveErrors++;
            _healthStatus.TotalMessagesFailed++;
            _healthStatus.LastError = ex.Message;

            // Mark unhealthy after too many consecutive errors
            if (_healthStatus.ConsecutiveErrors >= opts.ErrorHandling.MaxConsecutiveErrors)
            {
                _healthStatus.IsHealthy = false;
            }

            // Don't rethrow - let other partitions continue processing
        }
    }

    private Task ProcessErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Error in Event Hub partition {Partition}, Operation: {Operation}",
            args.PartitionId,
            args.Operation);

        _healthStatus.LastError = args.Exception.Message;
        _healthStatus.ConsecutiveErrors++;

        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Azure IoT Hub consumer is stopping");

        if (_processor != null)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Event Processor");
            }
        }

        await base.StopAsync(cancellationToken);
    }
}
