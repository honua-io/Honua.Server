// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.BackgroundJobs;

/// <summary>
/// Configuration for background job processing infrastructure
/// </summary>
public class BackgroundJobsOptions
{
    /// <summary>Configuration section name</summary>
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// Job processing mode: "Polling" (Tier 1-2) or "MessageQueue" (Tier 3)
    /// </summary>
    [Required]
    public BackgroundJobMode Mode { get; set; } = BackgroundJobMode.Polling;

    /// <summary>
    /// Message queue provider (required if Mode = MessageQueue)
    /// </summary>
    public MessageQueueProvider? Provider { get; set; }

    /// <summary>
    /// Maximum concurrent jobs to process
    /// </summary>
    [Range(1, 100)]
    public int MaxConcurrentJobs { get; set; } = 5;

    /// <summary>
    /// Poll interval for database polling mode (seconds)
    /// Ignored for message queue mode.
    /// </summary>
    [Range(1, 60)]
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Job execution timeout (minutes)
    /// </summary>
    [Range(1, 240)]
    public int JobTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Visibility timeout for message queues (seconds)
    /// How long a message is hidden after being received.
    /// Should be > JobTimeoutMinutes to prevent duplicate processing.
    /// </summary>
    [Range(30, 43200)] // 30 seconds to 12 hours
    public int VisibilityTimeoutSeconds { get; set; } = 1800; // 30 minutes

    /// <summary>
    /// Maximum retry attempts for transient failures
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Enable idempotency checking (requires Redis or PostgreSQL idempotency store)
    /// </summary>
    public bool EnableIdempotency { get; set; } = true;

    /// <summary>
    /// Idempotency result cache TTL (days)
    /// </summary>
    [Range(1, 30)]
    public int IdempotencyTtlDays { get; set; } = 7;

    /// <summary>
    /// AWS SQS-specific configuration
    /// </summary>
    public AwsSqsOptions? AwsSqs { get; set; }

    /// <summary>
    /// Azure Service Bus-specific configuration
    /// </summary>
    public AzureServiceBusOptions? AzureServiceBus { get; set; }

    /// <summary>
    /// RabbitMQ-specific configuration
    /// </summary>
    public RabbitMqOptions? RabbitMq { get; set; }

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public void Validate()
    {
        if (Mode == BackgroundJobMode.MessageQueue && Provider == null)
        {
            throw new InvalidOperationException(
                "Provider must be specified when Mode is MessageQueue");
        }

        if (VisibilityTimeoutSeconds < JobTimeoutMinutes * 60)
        {
            throw new InvalidOperationException(
                $"VisibilityTimeoutSeconds ({VisibilityTimeoutSeconds}) must be >= JobTimeoutMinutes * 60 ({JobTimeoutMinutes * 60})");
        }

        switch (Provider)
        {
            case MessageQueueProvider.AwsSqs:
                if (AwsSqs == null)
                    throw new InvalidOperationException("AwsSqs configuration is required when Provider is AwsSqs");
                AwsSqs.Validate();
                break;

            case MessageQueueProvider.AzureServiceBus:
                if (AzureServiceBus == null)
                    throw new InvalidOperationException("AzureServiceBus configuration is required when Provider is AzureServiceBus");
                AzureServiceBus.Validate();
                break;

            case MessageQueueProvider.RabbitMq:
                if (RabbitMq == null)
                    throw new InvalidOperationException("RabbitMq configuration is required when Provider is RabbitMq");
                RabbitMq.Validate();
                break;
        }
    }
}

/// <summary>
/// Background job processing mode
/// </summary>
public enum BackgroundJobMode
{
    /// <summary>Database polling (Tier 1-2: simple, no external dependencies)</summary>
    Polling,

    /// <summary>Message queue (Tier 3: scalable, event-driven)</summary>
    MessageQueue
}

/// <summary>
/// Message queue provider
/// </summary>
public enum MessageQueueProvider
{
    /// <summary>AWS Simple Queue Service</summary>
    AwsSqs,

    /// <summary>Azure Service Bus</summary>
    AzureServiceBus,

    /// <summary>RabbitMQ</summary>
    RabbitMq
}

/// <summary>
/// AWS SQS configuration
/// </summary>
public class AwsSqsOptions
{
    /// <summary>SQS queue URL (required)</summary>
    [Required]
    public string QueueUrl { get; set; } = string.Empty;

    /// <summary>Dead-letter queue URL (optional but recommended)</summary>
    public string? DeadLetterQueueUrl { get; set; }

    /// <summary>AWS region (defaults to SDK default region)</summary>
    public string? Region { get; set; }

    /// <summary>AWS access key ID (optional, uses IAM role if not provided)</summary>
    public string? AccessKeyId { get; set; }

    /// <summary>AWS secret access key (optional, uses IAM role if not provided)</summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>Long polling wait time (seconds, 1-20)</summary>
    [Range(0, 20)]
    public int WaitTimeSeconds { get; set; } = 20;

    /// <summary>Maximum number of messages to receive per batch (1-10)</summary>
    [Range(1, 10)]
    public int MaxNumberOfMessages { get; set; } = 10;

    /// <summary>Use FIFO queue (exactly-once delivery, ordered processing)</summary>
    public bool UseFifoQueue { get; set; } = false;

    /// <summary>Validates the configuration</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(QueueUrl))
            throw new InvalidOperationException("QueueUrl is required");

        if (UseFifoQueue && !QueueUrl.EndsWith(".fifo"))
            throw new InvalidOperationException("FIFO queue URL must end with .fifo");
    }
}

/// <summary>
/// Azure Service Bus configuration
/// </summary>
public class AzureServiceBusOptions
{
    /// <summary>Service Bus connection string (required)</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Queue name (required)</summary>
    [Required]
    public string QueueName { get; set; } = string.Empty;

    /// <summary>Maximum concurrent calls (1-100)</summary>
    [Range(1, 100)]
    public int MaxConcurrentCalls { get; set; } = 10;

    /// <summary>Validates the configuration</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ConnectionString is required");

        if (string.IsNullOrWhiteSpace(QueueName))
            throw new InvalidOperationException("QueueName is required");
    }
}

/// <summary>
/// RabbitMQ configuration
/// </summary>
public class RabbitMqOptions
{
    /// <summary>RabbitMQ host (required)</summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>RabbitMQ port (default: 5672)</summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5672;

    /// <summary>Virtual host (default: /)</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Username (required)</summary>
    [Required]
    public string Username { get; set; } = "guest";

    /// <summary>Password (required)</summary>
    [Required]
    public string Password { get; set; } = "guest";

    /// <summary>Queue name (required)</summary>
    [Required]
    public string QueueName { get; set; } = "honua-background-jobs";

    /// <summary>Use durable queues (survives broker restart)</summary>
    public bool Durable { get; set; } = true;

    /// <summary>Prefetch count (number of messages to fetch at once)</summary>
    [Range(1, 100)]
    public ushort PrefetchCount { get; set; } = 10;

    /// <summary>Validates the configuration</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("Host is required");

        if (string.IsNullOrWhiteSpace(Username))
            throw new InvalidOperationException("Username is required");

        if (string.IsNullOrWhiteSpace(Password))
            throw new InvalidOperationException("Password is required");

        if (string.IsNullOrWhiteSpace(QueueName))
            throw new InvalidOperationException("QueueName is required");
    }
}
