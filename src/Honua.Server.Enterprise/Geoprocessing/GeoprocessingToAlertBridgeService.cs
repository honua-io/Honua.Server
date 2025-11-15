// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Service that bridges geoprocessing job events to the alert system.
/// Converts job failures, timeouts, and SLA breaches into generic alerts
/// and routes them through the alert pipeline for multi-channel notifications.
/// </summary>
public interface IGeoprocessingToAlertBridgeService
{
    /// <summary>
    /// Process a job failure event and generate alerts.
    /// </summary>
    Task ProcessJobFailureAsync(ProcessRun job, Exception error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a job timeout event and generate alerts.
    /// </summary>
    Task ProcessJobTimeoutAsync(ProcessRun job, int timeoutMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process a job SLA breach event (queued too long) and generate alerts.
    /// </summary>
    Task ProcessJobSlaBreachAsync(ProcessRun job, int queueWaitMinutes, int slaThresholdMinutes, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of geoprocessing-to-alert bridge service.
/// </summary>
public sealed class GeoprocessingToAlertBridgeService : IGeoprocessingToAlertBridgeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeoprocessingToAlertBridgeService> _logger;
    private readonly string _alertReceiverBaseUrl;

    // Configuration
    private const int HighPriorityThreshold = 7; // Priority >= 7 is high
    private const int CriticalPriorityThreshold = 9; // Priority >= 9 is critical
    private const int SlaThresholdMinutes = 5; // Default SLA: jobs should start within 5 minutes

    public GeoprocessingToAlertBridgeService(
        IHttpClientFactory httpClientFactory,
        ILogger<GeoprocessingToAlertBridgeService> logger,
        string alertReceiverBaseUrl)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alertReceiverBaseUrl = alertReceiverBaseUrl ?? throw new ArgumentNullException(nameof(alertReceiverBaseUrl));
    }

    public async Task ProcessJobFailureAsync(ProcessRun job, Exception error, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing job failure alert for job {JobId}, process {ProcessId}, retries: {RetryCount}/{MaxRetries}",
                job.JobId, job.ProcessId, job.RetryCount, job.MaxRetries);

            // Determine severity based on priority and retry count
            var severity = DetermineSeverityForFailure(job);

            var alert = new GenericAlert
            {
                Name = $"Geoprocessing Job Failed: {job.ProcessId}",
                Severity = severity,
                Status = "firing",
                Summary = $"Geoprocessing job '{job.ProcessId}' failed after {job.RetryCount} retries",
                Description = $"Job '{job.JobId}' for process '{job.ProcessId}' failed with error: {error.Message}. " +
                             $"Retries exhausted ({job.RetryCount}/{job.MaxRetries}). " +
                             $"Priority: {job.Priority}, Tenant: {job.TenantId}, User: {job.UserEmail ?? job.UserId.ToString()}.",
                Source = "geoprocessing-system",
                Service = "geoprocessing-worker",
                Fingerprint = GenerateFingerprint($"job-failure:{job.JobId}"),
                Timestamp = DateTimeOffset.UtcNow,
                Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["job_id"] = job.JobId,
                    ["process_id"] = job.ProcessId,
                    ["tenant_id"] = job.TenantId.ToString(),
                    ["user_id"] = job.UserId.ToString(),
                    ["priority"] = job.Priority.ToString(),
                    ["retry_count"] = job.RetryCount.ToString(),
                    ["max_retries"] = job.MaxRetries.ToString(),
                    ["event_type"] = "job_failure",
                    ["tier"] = job.ExecutedTier?.ToString() ?? "unknown"
                },
                Context = new Dictionary<string, object>
                {
                    ["error_message"] = error.Message,
                    ["error_type"] = error.GetType().Name,
                    ["duration_ms"] = job.DurationMs ?? 0,
                    ["queue_wait_ms"] = job.QueueWaitMs ?? 0,
                    ["created_at"] = job.CreatedAt,
                    ["started_at"] = job.StartedAt?.ToString() ?? "never_started",
                    ["features_processed"] = job.FeaturesProcessed ?? 0
                }
            };

            // Add user email if available
            if (!string.IsNullOrEmpty(job.UserEmail))
            {
                alert.Labels["user_email"] = job.UserEmail;
            }

            // Add error details if available
            if (!string.IsNullOrEmpty(job.ErrorDetails))
            {
                alert.Context["error_details"] = job.ErrorDetails;
            }

            // Add input size if available
            if (job.InputSizeMB.HasValue)
            {
                alert.Context["input_size_mb"] = job.InputSizeMB.Value;
            }

            // Add metadata
            if (job.Metadata != null)
            {
                foreach (var (key, value) in job.Metadata)
                {
                    alert.Context[$"metadata_{key}"] = value;
                }
            }

            await SendAlertAsync(alert, cancellationToken);

            _logger.LogInformation(
                "Successfully created job failure alert {Fingerprint} for job {JobId}",
                alert.Fingerprint, job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing job failure alert for job {JobId}",
                job.JobId);
            // Don't throw - we don't want alert failures to break job processing
        }
    }

    public async Task ProcessJobTimeoutAsync(ProcessRun job, int timeoutMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing job timeout alert for job {JobId}, process {ProcessId}, timeout: {TimeoutMinutes}min",
                job.JobId, job.ProcessId, timeoutMinutes);

            // Timeouts are always at least a warning, critical if high priority
            var severity = job.Priority >= HighPriorityThreshold ? "critical" : "warning";

            var alert = new GenericAlert
            {
                Name = $"Geoprocessing Job Timeout: {job.ProcessId}",
                Severity = severity,
                Status = "firing",
                Summary = $"Geoprocessing job '{job.ProcessId}' exceeded timeout of {timeoutMinutes} minutes",
                Description = $"Job '{job.JobId}' for process '{job.ProcessId}' timed out after {timeoutMinutes} minutes. " +
                             $"Priority: {job.Priority}, Tenant: {job.TenantId}, User: {job.UserEmail ?? job.UserId.ToString()}. " +
                             $"The job may be stuck or processing an unexpectedly large dataset.",
                Source = "geoprocessing-system",
                Service = "geoprocessing-worker",
                Fingerprint = GenerateFingerprint($"job-timeout:{job.JobId}"),
                Timestamp = DateTimeOffset.UtcNow,
                Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["job_id"] = job.JobId,
                    ["process_id"] = job.ProcessId,
                    ["tenant_id"] = job.TenantId.ToString(),
                    ["user_id"] = job.UserId.ToString(),
                    ["priority"] = job.Priority.ToString(),
                    ["timeout_minutes"] = timeoutMinutes.ToString(),
                    ["event_type"] = "job_timeout",
                    ["tier"] = job.ExecutedTier?.ToString() ?? "unknown"
                },
                Context = new Dictionary<string, object>
                {
                    ["timeout_threshold_minutes"] = timeoutMinutes,
                    ["duration_ms"] = job.DurationMs ?? 0,
                    ["queue_wait_ms"] = job.QueueWaitMs ?? 0,
                    ["created_at"] = job.CreatedAt,
                    ["started_at"] = job.StartedAt?.ToString() ?? "never_started",
                    ["features_processed"] = job.FeaturesProcessed ?? 0
                }
            };

            // Add user email if available
            if (!string.IsNullOrEmpty(job.UserEmail))
            {
                alert.Labels["user_email"] = job.UserEmail;
            }

            // Add input size if available
            if (job.InputSizeMB.HasValue)
            {
                alert.Context["input_size_mb"] = job.InputSizeMB.Value;
            }

            await SendAlertAsync(alert, cancellationToken);

            _logger.LogInformation(
                "Successfully created job timeout alert {Fingerprint} for job {JobId}",
                alert.Fingerprint, job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing job timeout alert for job {JobId}",
                job.JobId);
            // Don't throw - we don't want alert failures to break job processing
        }
    }

    public async Task ProcessJobSlaBreachAsync(ProcessRun job, int queueWaitMinutes, int slaThresholdMinutes, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing job SLA breach alert for job {JobId}, process {ProcessId}, queue wait: {QueueWaitMinutes}min, SLA: {SlaThresholdMinutes}min",
                job.JobId, job.ProcessId, queueWaitMinutes, slaThresholdMinutes);

            // SLA breaches are warnings by default, escalate based on priority
            var severity = DetermineSeverityForSlaBreach(job, queueWaitMinutes, slaThresholdMinutes);

            var alert = new GenericAlert
            {
                Name = $"Geoprocessing Job SLA Breach: {job.ProcessId}",
                Severity = severity,
                Status = "firing",
                Summary = $"Geoprocessing job '{job.ProcessId}' queued for {queueWaitMinutes} minutes (SLA: {slaThresholdMinutes} min)",
                Description = $"Job '{job.JobId}' for process '{job.ProcessId}' has been queued for {queueWaitMinutes} minutes, " +
                             $"exceeding the SLA threshold of {slaThresholdMinutes} minutes. " +
                             $"Priority: {job.Priority}, Tenant: {job.TenantId}, User: {job.UserEmail ?? job.UserId.ToString()}. " +
                             $"This may indicate resource contention or capacity issues.",
                Source = "geoprocessing-system",
                Service = "geoprocessing-scheduler",
                Fingerprint = GenerateFingerprint($"job-sla-breach:{job.JobId}"),
                Timestamp = DateTimeOffset.UtcNow,
                Labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["job_id"] = job.JobId,
                    ["process_id"] = job.ProcessId,
                    ["tenant_id"] = job.TenantId.ToString(),
                    ["user_id"] = job.UserId.ToString(),
                    ["priority"] = job.Priority.ToString(),
                    ["queue_wait_minutes"] = queueWaitMinutes.ToString(),
                    ["sla_threshold_minutes"] = slaThresholdMinutes.ToString(),
                    ["event_type"] = "sla_breach",
                    ["tier"] = job.ExecutedTier?.ToString() ?? "pending"
                },
                Context = new Dictionary<string, object>
                {
                    ["queue_wait_ms"] = job.QueueWaitMs ?? 0,
                    ["sla_threshold_minutes"] = slaThresholdMinutes,
                    ["sla_breach_minutes"] = queueWaitMinutes - slaThresholdMinutes,
                    ["created_at"] = job.CreatedAt,
                    ["status"] = job.Status.ToString()
                }
            };

            // Add user email if available
            if (!string.IsNullOrEmpty(job.UserEmail))
            {
                alert.Labels["user_email"] = job.UserEmail;
            }

            // Add input size if available
            if (job.InputSizeMB.HasValue)
            {
                alert.Context["input_size_mb"] = job.InputSizeMB.Value;
            }

            await SendAlertAsync(alert, cancellationToken);

            _logger.LogInformation(
                "Successfully created job SLA breach alert {Fingerprint} for job {JobId}",
                alert.Fingerprint, job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing job SLA breach alert for job {JobId}",
                job.JobId);
            // Don't throw - we don't want alert failures to break job processing
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Determines alert severity for job failures based on priority and retry count.
    /// </summary>
    private string DetermineSeverityForFailure(ProcessRun job)
    {
        // Critical: High priority jobs (>= 9) that failed
        if (job.Priority >= CriticalPriorityThreshold)
        {
            return "critical";
        }

        // Error: Medium-high priority jobs (>= 7) that failed
        if (job.Priority >= HighPriorityThreshold)
        {
            return "error";
        }

        // Warning: All other failures
        return "warning";
    }

    /// <summary>
    /// Determines alert severity for SLA breaches based on priority and breach duration.
    /// </summary>
    private string DetermineSeverityForSlaBreach(ProcessRun job, int queueWaitMinutes, int slaThresholdMinutes)
    {
        var breachFactor = (double)queueWaitMinutes / slaThresholdMinutes;

        // Critical: High priority jobs queued for 3x the SLA threshold
        if (job.Priority >= CriticalPriorityThreshold && breachFactor >= 3.0)
        {
            return "critical";
        }

        // Error: High priority jobs or severe breaches (>= 5x SLA)
        if (job.Priority >= HighPriorityThreshold || breachFactor >= 5.0)
        {
            return "error";
        }

        // Warning: All other SLA breaches
        return "warning";
    }

    /// <summary>
    /// Generates a unique fingerprint for alert deduplication.
    /// </summary>
    private string GenerateFingerprint(string data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        return $"gp-{Convert.ToHexString(hash)[..40]}"; // Take first 40 chars (20 bytes)
    }

    /// <summary>
    /// Sends an alert to the alert receiver service.
    /// </summary>
    private async Task<long?> SendAlertAsync(GenericAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("AlertReceiver");

            // Post alert to alert receiver API
            var response = await httpClient.PostAsJsonAsync("/api/alerts", alert, cancellationToken);
            response.EnsureSuccessStatusCode();

            // Try to extract alert history ID from response if available
            if (response.Content != null)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrEmpty(responseContent))
                {
                    try
                    {
                        var json = JsonDocument.Parse(responseContent);
                        if (json.RootElement.TryGetProperty("id", out var idElement))
                        {
                            return idElement.GetInt64();
                        }
                    }
                    catch
                    {
                        // Response doesn't contain ID, continue without it
                    }
                }
            }

            _logger.LogInformation("Successfully sent alert {Fingerprint} to alert receiver", alert.Fingerprint);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send alert {Fingerprint} to alert receiver", alert.Fingerprint);
            throw;
        }
    }

    #endregion
}

/// <summary>
/// Simplified GenericAlert model for sending to alert receiver.
/// Matches the structure expected by the AlertReceiver API.
/// </summary>
internal sealed class GenericAlert
{
    public string Name { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Status { get; set; } = "firing";
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = "unknown";
    public string? Service { get; set; }
    public string? Environment { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset Timestamp { get; set; }
    public string? Fingerprint { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}
