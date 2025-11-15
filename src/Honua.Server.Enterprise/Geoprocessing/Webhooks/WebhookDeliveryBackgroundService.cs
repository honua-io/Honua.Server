// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace Honua.Server.Enterprise.Geoprocessing.Webhooks;

/// <summary>
/// Background service that processes webhook delivery queue with retry logic.
/// Delivers geoprocessing job completion notifications to configured webhook endpoints.
/// </summary>
public sealed class WebhookDeliveryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookDeliveryBackgroundService> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePolicy;

    // Configuration
    private const int PollIntervalSeconds = 5;
    private const int MaxConcurrentDeliveries = 10;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private int _activeDeliveries = 0;

    public WebhookDeliveryBackgroundService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ILogger<WebhookDeliveryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentDeliveries, MaxConcurrentDeliveries);

        // Create resilience policy for webhook HTTP requests
        // Use 5 retries with exponential backoff (matches database max_attempts default)
        _resiliencePolicy = ResiliencePolicies.CreateHttpRetryPolicy(
            maxRetries: 3, // 3 Polly retries per delivery attempt
            initialDelay: TimeSpan.FromSeconds(1),
            timeout: TimeSpan.FromSeconds(30),
            logger: loggerFactory.CreateLogger("Resilience.WebhookDelivery"));

        _logger.LogInformation(
            "WebhookDeliveryBackgroundService initialized: MaxConcurrent={MaxConcurrent}, PollInterval={PollInterval}s",
            MaxConcurrentDeliveries, PollIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookDeliveryBackgroundService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var deliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

                // Get next webhook from queue
                var delivery = await deliveryService.DequeueNextAsync(stoppingToken);

                if (delivery != null)
                {
                    _logger.LogInformation(
                        "Found pending webhook delivery {DeliveryId} for job {JobId} (attempt {Attempt}/{MaxAttempts})",
                        delivery.Id, delivery.JobId, delivery.AttemptCount, delivery.MaxAttempts);

                    // Wait for available slot
                    await _concurrencySemaphore.WaitAsync(stoppingToken);

                    Interlocked.Increment(ref _activeDeliveries);

                    // Process delivery in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessDeliveryAsync(delivery, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Unhandled exception processing webhook delivery {DeliveryId}", delivery.Id);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeDeliveries);
                            _concurrencySemaphore.Release();
                        }
                    }, stoppingToken);
                }
                else
                {
                    // No pending deliveries, wait before polling again
                    _logger.LogDebug("No pending webhook deliveries, waiting {Seconds}s", PollIntervalSeconds);
                    await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("WebhookDeliveryBackgroundService stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WebhookDeliveryBackgroundService main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("WebhookDeliveryBackgroundService stopped");
    }

    /// <summary>
    /// Processes a single webhook delivery
    /// </summary>
    private async Task ProcessDeliveryAsync(WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var deliveryService = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

        try
        {
            _logger.LogInformation(
                "Delivering webhook {DeliveryId} to {WebhookUrl} for job {JobId}",
                delivery.Id, delivery.WebhookUrl, delivery.JobId);

            // Execute HTTP POST with resilience policy
            var result = await _resiliencePolicy.ExecuteAsync(async ct =>
            {
                using var httpClient = _httpClientFactory.CreateClient("WebhookDelivery");

                // Add custom headers if provided
                if (delivery.Headers != null)
                {
                    foreach (var header in delivery.Headers)
                    {
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                // Set standard headers
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Honua-Webhook-Delivery/1.0");
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Honua-Job-Id", delivery.JobId);
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Honua-Delivery-Id", delivery.Id.ToString());
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Honua-Attempt", delivery.AttemptCount.ToString());

                var response = await httpClient.PostAsJsonAsync(delivery.WebhookUrl, delivery.Payload, ct);

                return response;
            }, cancellationToken);

            // Read response body
            var responseBody = await result.Content.ReadAsStringAsync(cancellationToken);

            if (result.IsSuccessStatusCode)
            {
                // Success - record delivery
                await deliveryService.RecordSuccessAsync(
                    delivery.Id,
                    (int)result.StatusCode,
                    responseBody,
                    cancellationToken);

                _logger.LogInformation(
                    "Successfully delivered webhook {DeliveryId} for job {JobId} (HTTP {Status})",
                    delivery.Id, delivery.JobId, (int)result.StatusCode);
            }
            else
            {
                // HTTP error response - record failure
                var errorMessage = $"HTTP {(int)result.StatusCode}: {result.ReasonPhrase}";

                await deliveryService.RecordFailureAsync(
                    delivery.Id,
                    (int)result.StatusCode,
                    errorMessage,
                    cancellationToken);

                _logger.LogWarning(
                    "Webhook delivery {DeliveryId} failed with HTTP {Status} (attempt {Attempt}/{MaxAttempts})",
                    delivery.Id, (int)result.StatusCode, delivery.AttemptCount, delivery.MaxAttempts);
            }
        }
        catch (HttpRequestException ex)
        {
            // Network/connection error
            var errorMessage = $"HTTP request failed: {ex.Message}";

            await deliveryService.RecordFailureAsync(
                delivery.Id,
                null,
                errorMessage,
                cancellationToken);

            _logger.LogError(ex,
                "Webhook delivery {DeliveryId} failed with network error (attempt {Attempt}/{MaxAttempts})",
                delivery.Id, delivery.AttemptCount, delivery.MaxAttempts);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout
            var errorMessage = "Request timed out";

            await deliveryService.RecordFailureAsync(
                delivery.Id,
                null,
                errorMessage,
                cancellationToken);

            _logger.LogWarning(
                "Webhook delivery {DeliveryId} timed out (attempt {Attempt}/{MaxAttempts})",
                delivery.Id, delivery.AttemptCount, delivery.MaxAttempts);
        }
        catch (Exception ex)
        {
            // Unexpected error
            var errorMessage = $"Unexpected error: {ex.Message}";

            await deliveryService.RecordFailureAsync(
                delivery.Id,
                null,
                errorMessage,
                cancellationToken);

            _logger.LogError(ex,
                "Webhook delivery {DeliveryId} failed with unexpected error",
                delivery.Id);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("WebhookDeliveryBackgroundService stopping gracefully");

        if (_activeDeliveries > 0)
        {
            _logger.LogInformation(
                "Waiting for {Count} active webhook deliveries to complete (timeout: 30s)",
                _activeDeliveries);

            var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);

            while (_activeDeliveries > 0 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(1000, cancellationToken);
            }

            if (_activeDeliveries > 0)
            {
                _logger.LogWarning(
                    "{Count} webhook deliveries still in progress after timeout, forcing shutdown",
                    _activeDeliveries);
            }
            else
            {
                _logger.LogInformation("All active webhook deliveries completed, shutting down");
            }
        }

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _concurrencySemaphore?.Dispose();
        base.Dispose();
    }
}
