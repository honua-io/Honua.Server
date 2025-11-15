// <copyright file="AlertDeliveryFailure.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.Json;

namespace Honua.Server.AlertReceiver.Data;

/// <summary>
/// Status of a failed alert delivery in the dead letter queue.
/// </summary>
public enum AlertDeliveryFailureStatus
{
    /// <summary>
    /// Waiting for retry attempt.
    /// </summary>
    Pending,

    /// <summary>
    /// Currently being retried.
    /// </summary>
    Retrying,

    /// <summary>
    /// Successfully delivered after retry.
    /// </summary>
    Resolved,

    /// <summary>
    /// Abandoned after max retry attempts.
    /// </summary>
    Abandoned,
}

/// <summary>
/// Represents a failed alert delivery stored in the dead letter queue.
/// Used to track and retry alerts that could not be delivered to their target channels.
/// </summary>
public sealed class AlertDeliveryFailure
{
    /// <summary>
    /// Gets or sets unique identifier for the failed delivery.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the alert fingerprint for correlation.
    /// </summary>
    public required string AlertFingerprint { get; set; }

    /// <summary>
    /// Gets or sets the full alert payload as JSON.
    /// </summary>
    public required string AlertPayloadJson { get; set; }

    /// <summary>
    /// Gets or sets the target channel that failed (e.g., "Slack", "PagerDuty").
    /// </summary>
    public required string TargetChannel { get; set; }

    /// <summary>
    /// Gets or sets the error message from the failed delivery.
    /// </summary>
    public required string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets additional error details as JSON (stack trace, HTTP response, etc.).
    /// </summary>
    public string? ErrorDetailsJson { get; set; }

    /// <summary>
    /// Gets or sets the number of retry attempts made.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets when the delivery first failed.
    /// </summary>
    public DateTimeOffset FailedAt { get; set; }

    /// <summary>
    /// Gets or sets when the last retry attempt was made.
    /// </summary>
    public DateTimeOffset? LastRetryAt { get; set; }

    /// <summary>
    /// Gets or sets the scheduled time for the next retry attempt.
    /// </summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    /// <summary>
    /// Gets or sets when the delivery was successfully resolved.
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// Gets or sets the current status of the failed delivery.
    /// </summary>
    public AlertDeliveryFailureStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the alert severity for prioritization.
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Calculates the next retry time using exponential backoff.
    /// </summary>
    /// <param name="baseDelayMinutes">Base delay in minutes (default: 5).</param>
    /// <returns>The calculated next retry time.</returns>
    public DateTimeOffset CalculateNextRetryTime(int baseDelayMinutes = 5)
    {
        // Exponential backoff: 5m, 10m, 20m, 40m, 80m
        var delayMinutes = baseDelayMinutes * Math.Pow(2, this.RetryCount);
        var maxDelayMinutes = 120; // Cap at 2 hours
        var actualDelay = Math.Min(delayMinutes, maxDelayMinutes);

        return DateTimeOffset.UtcNow.AddMinutes(actualDelay);
    }

    /// <summary>
    /// Creates error details JSON from an exception.
    /// </summary>
    /// <param name="exception">The exception to serialize.</param>
    /// <returns>JSON string containing error details.</returns>
    public static string CreateErrorDetailsJson(Exception exception)
    {
        var errorDetails = new
        {
            ExceptionType = exception.GetType().FullName,
            Message = exception.Message,
            StackTrace = exception.StackTrace,
            InnerException = exception.InnerException != null
                ? new
                {
                    Type = exception.InnerException.GetType().FullName,
                    Message = exception.InnerException.Message,
                }
                : null,
        };

        return JsonSerializer.Serialize(errorDetails);
    }
}
