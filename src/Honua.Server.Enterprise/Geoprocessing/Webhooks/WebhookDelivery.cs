// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Geoprocessing.Webhooks;

/// <summary>
/// Represents a webhook delivery request with retry tracking
/// </summary>
public class WebhookDelivery
{
    /// <summary>Unique delivery identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Associated job ID</summary>
    public required string JobId { get; init; }

    /// <summary>Webhook URL to deliver to</summary>
    public required string WebhookUrl { get; init; }

    /// <summary>Payload to send (will be serialized to JSON)</summary>
    public required Dictionary<string, object> Payload { get; init; }

    /// <summary>Custom headers to include in request</summary>
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>Current delivery status</summary>
    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;

    /// <summary>When delivery was created</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When to attempt next retry</summary>
    public DateTimeOffset NextRetryAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When last delivery attempt was made</summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>When delivery completed (success or abandoned)</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Number of delivery attempts made</summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>Maximum delivery attempts allowed</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>HTTP status code from last attempt</summary>
    public int? LastResponseStatus { get; set; }

    /// <summary>Response body from last attempt (truncated)</summary>
    public string? LastResponseBody { get; set; }

    /// <summary>Error message from last attempt</summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>Tenant ID for tracking</summary>
    public required string TenantId { get; init; }

    /// <summary>Process ID for tracking</summary>
    public required string ProcessId { get; init; }

    /// <summary>
    /// Checks if delivery should be retried
    /// </summary>
    public bool ShouldRetry => Status == WebhookDeliveryStatus.Pending && AttemptCount < MaxAttempts;

    /// <summary>
    /// Checks if delivery is in a terminal state
    /// </summary>
    public bool IsTerminal => Status is WebhookDeliveryStatus.Delivered or WebhookDeliveryStatus.Abandoned;
}

/// <summary>
/// Webhook delivery status
/// </summary>
public enum WebhookDeliveryStatus
{
    /// <summary>Waiting to be delivered</summary>
    Pending,

    /// <summary>Currently being delivered</summary>
    Processing,

    /// <summary>Successfully delivered</summary>
    Delivered,

    /// <summary>Temporarily failed (will retry)</summary>
    Failed,

    /// <summary>Permanently failed (max retries exceeded)</summary>
    Abandoned
}

/// <summary>
/// Webhook payload for job completion
/// </summary>
public class JobCompletionWebhookPayload
{
    /// <summary>Event type</summary>
    public string Event { get; init; } = "job.completed";

    /// <summary>Job ID</summary>
    public required string JobId { get; init; }

    /// <summary>Process ID</summary>
    public required string ProcessId { get; init; }

    /// <summary>Tenant ID</summary>
    public required string TenantId { get; init; }

    /// <summary>Job status</summary>
    public required string Status { get; init; }

    /// <summary>Whether job succeeded</summary>
    public bool Success { get; init; }

    /// <summary>When job was created</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>When job completed</summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Duration in milliseconds</summary>
    public long? DurationMs { get; init; }

    /// <summary>Output URL (if available)</summary>
    public string? OutputUrl { get; init; }

    /// <summary>Error message (if failed)</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Features processed count</summary>
    public long? FeaturesProcessed { get; init; }

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>Webhook delivery timestamp</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
