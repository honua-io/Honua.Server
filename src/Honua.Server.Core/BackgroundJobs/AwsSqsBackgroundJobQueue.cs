// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// AWS SQS implementation of background job queue.
/// Suitable for Tier 3 deployments requiring high throughput and scalability.
/// </summary>
/// <remarks>
/// Implementation approach:
///
/// 1. Enqueue: SendMessage to SQS queue
/// 2. Receive: ReceiveMessage with long polling (WaitTimeSeconds = 20)
/// 3. Complete: DeleteMessage
/// 4. Abandon: ChangeMessageVisibility (makes message visible again after delay)
///
/// Advantages:
/// - Event-driven (no polling overhead)
/// - Highly scalable (millions of messages/second)
/// - Native retry and dead-letter queue support
/// - Automatic message expiry and cleanup
/// - FIFO queues for exactly-once delivery
///
/// Disadvantages:
/// - External dependency on AWS
/// - Additional cost for SQS usage
/// - Network latency to AWS region
///
/// Suitable for:
/// - Tier 3 deployments
/// - High job throughput (> 100 jobs/second)
/// - Cloud-native architectures
/// - Multi-region deployments
/// </remarks>
public sealed class AwsSqsBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly IAmazonSQS _sqs;
    private readonly ILogger<AwsSqsBackgroundJobQueue> _logger;
    private readonly AwsSqsOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AwsSqsBackgroundJobQueue(
        IAmazonSQS sqs,
        ILogger<AwsSqsBackgroundJobQueue> logger,
        IOptions<BackgroundJobsOptions> options)
    {
        _sqs = sqs ?? throw new ArgumentNullException(nameof(sqs));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value?.AwsSqs ?? throw new ArgumentNullException(nameof(options));

        _options.Validate();
    }

    /// <inheritdoc />
    public Task<string> EnqueueAsync<TJob>(TJob job, CancellationToken cancellationToken = default)
        where TJob : class
    {
        return EnqueueAsync(job, new EnqueueOptions(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> EnqueueAsync<TJob>(
        TJob job,
        EnqueueOptions options,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        if (job == null)
            throw new ArgumentNullException(nameof(job));

        try
        {
            var jobType = typeof(TJob).Name;
            var payload = JsonSerializer.Serialize(job, JsonOptions);

            var request = new SendMessageRequest
            {
                QueueUrl = _options.QueueUrl,
                MessageBody = payload,
                DelaySeconds = (int)(options.DelaySeconds?.TotalSeconds ?? 0),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    ["JobType"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = jobType
                    },
                    ["Priority"] = new MessageAttributeValue
                    {
                        DataType = "Number",
                        StringValue = options.Priority.ToString()
                    },
                    ["EnqueuedAt"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = DateTimeOffset.UtcNow.ToString("O")
                    }
                }
            };

            // Add custom attributes
            if (options.Attributes != null)
            {
                foreach (var (key, value) in options.Attributes)
                {
                    request.MessageAttributes[$"Custom_{key}"] = new MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = value
                    };
                }
            }

            // FIFO queue support
            if (_options.UseFifoQueue)
            {
                if (string.IsNullOrWhiteSpace(options.MessageGroupId))
                    throw new InvalidOperationException("MessageGroupId is required for FIFO queues");

                request.MessageGroupId = options.MessageGroupId;

                // Use deduplication ID if provided, otherwise SQS uses content-based deduplication
                if (!string.IsNullOrWhiteSpace(options.DeduplicationId))
                {
                    request.MessageDeduplicationId = options.DeduplicationId;
                }
            }

            var response = await _sqs.SendMessageAsync(request, cancellationToken);

            _logger.LogDebug(
                "Enqueued job {JobType} to SQS with message ID {MessageId}, priority {Priority}",
                jobType,
                response.MessageId,
                options.Priority);

            return response.MessageId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing job of type {JobType} to SQS", typeof(TJob).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<QueueMessage<TJob>>> ReceiveAsync<TJob>(
        int maxMessages = 1,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        if (maxMessages < 1 || maxMessages > 10)
            throw new ArgumentOutOfRangeException(nameof(maxMessages), "Must be between 1 and 10");

        try
        {
            var jobType = typeof(TJob).Name;

            var request = new ReceiveMessageRequest
            {
                QueueUrl = _options.QueueUrl,
                MaxNumberOfMessages = Math.Min(maxMessages, _options.MaxNumberOfMessages),
                WaitTimeSeconds = _options.WaitTimeSeconds, // Long polling
                MessageAttributeNames = new List<string> { "All" },
                AttributeNames = new List<string> { "ApproximateReceiveCount", "SentTimestamp" }
            };

            var response = await _sqs.ReceiveMessageAsync(request, cancellationToken);

            var messages = new List<QueueMessage<TJob>>();

            foreach (var sqsMessage in response.Messages)
            {
                // Verify job type matches
                if (sqsMessage.MessageAttributes.TryGetValue("JobType", out var jobTypeAttr))
                {
                    if (jobTypeAttr.StringValue != jobType)
                    {
                        _logger.LogWarning(
                            "Skipping message {MessageId} with unexpected job type {ActualType} (expected {ExpectedType})",
                            sqsMessage.MessageId,
                            jobTypeAttr.StringValue,
                            jobType);
                        continue;
                    }
                }

                try
                {
                    var body = JsonSerializer.Deserialize<TJob>(sqsMessage.Body, JsonOptions);
                    if (body == null)
                    {
                        _logger.LogError(
                            "Failed to deserialize message {MessageId} body",
                            sqsMessage.MessageId);
                        continue;
                    }

                    // Parse delivery count
                    var deliveryCount = 1;
                    if (sqsMessage.Attributes.TryGetValue("ApproximateReceiveCount", out var receiveCountStr))
                    {
                        int.TryParse(receiveCountStr, out deliveryCount);
                    }

                    // Parse enqueued timestamp
                    var enqueuedAt = DateTimeOffset.UtcNow;
                    if (sqsMessage.MessageAttributes.TryGetValue("EnqueuedAt", out var enqueuedAtAttr))
                    {
                        DateTimeOffset.TryParse(enqueuedAtAttr.StringValue, out enqueuedAt);
                    }
                    else if (sqsMessage.Attributes.TryGetValue("SentTimestamp", out var sentTimestampStr))
                    {
                        // Fallback to SentTimestamp (Unix epoch milliseconds)
                        if (long.TryParse(sentTimestampStr, out var sentTimestamp))
                        {
                            enqueuedAt = DateTimeOffset.FromUnixTimeMilliseconds(sentTimestamp);
                        }
                    }

                    // Extract custom attributes
                    var attributes = sqsMessage.MessageAttributes
                        .Where(kv => kv.Key.StartsWith("Custom_"))
                        .ToDictionary(
                            kv => kv.Key.Substring("Custom_".Length),
                            kv => kv.Value.StringValue);

                    messages.Add(new QueueMessage<TJob>
                    {
                        MessageId = sqsMessage.MessageId,
                        Body = body,
                        ReceiptHandle = sqsMessage.ReceiptHandle,
                        DeliveryCount = deliveryCount,
                        EnqueuedAt = enqueuedAt,
                        Attributes = attributes.Count > 0 ? attributes : null
                    });
                }
                catch (JsonException ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to deserialize message {MessageId}",
                        sqsMessage.MessageId);
                }
            }

            _logger.LogDebug(
                "Received {Count} messages of type {JobType} from SQS",
                messages.Count,
                jobType);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages of type {JobType} from SQS", typeof(TJob).Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

        try
        {
            // Note: messageId here is actually the ReceiptHandle for SQS
            // The QueueMessage.MessageId is for logging, ReceiptHandle is for deletion
            var request = new DeleteMessageRequest
            {
                QueueUrl = _options.QueueUrl,
                ReceiptHandle = messageId
            };

            await _sqs.DeleteMessageAsync(request, cancellationToken);

            _logger.LogDebug("Completed and deleted message from SQS");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing message in SQS");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AbandonAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message ID cannot be null or empty", nameof(messageId));

        try
        {
            // Change visibility timeout to 0 to make message immediately available for retry
            // Or set a small delay for exponential backoff
            var request = new ChangeMessageVisibilityRequest
            {
                QueueUrl = _options.QueueUrl,
                ReceiptHandle = messageId,
                VisibilityTimeout = 5 // Make visible again after 5 seconds
            };

            await _sqs.ChangeMessageVisibilityAsync(request, cancellationToken);

            _logger.LogDebug("Abandoned message in SQS (will be retried after 5s)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error abandoning message in SQS");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> GetQueueDepthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new GetQueueAttributesRequest
            {
                QueueUrl = _options.QueueUrl,
                AttributeNames = new List<string>
                {
                    "ApproximateNumberOfMessages",
                    "ApproximateNumberOfMessagesNotVisible"
                }
            };

            var response = await _sqs.GetQueueAttributesAsync(request, cancellationToken);

            var visible = 0L;
            var notVisible = 0L;

            if (response.Attributes.TryGetValue("ApproximateNumberOfMessages", out var visibleStr))
            {
                long.TryParse(visibleStr, out visible);
            }

            if (response.Attributes.TryGetValue("ApproximateNumberOfMessagesNotVisible", out var notVisibleStr))
            {
                long.TryParse(notVisibleStr, out notVisible);
            }

            var total = visible + notVisible;

            _logger.LogDebug(
                "SQS queue depth: {Visible} visible, {NotVisible} not visible, {Total} total",
                visible,
                notVisible,
                total);

            return total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting queue depth from SQS");
            throw;
        }
    }
}
