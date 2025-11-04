using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;

namespace Honua.Examples.Alerting;

/// <summary>
/// Examples of sending alerts directly from application code.
/// </summary>
public class DirectAlertExamples
{
    private readonly IAlertClient _alertClient;
    private readonly ILogger<DirectAlertExamples> _logger;

    public DirectAlertExamples(IAlertClient alertClient, ILogger<DirectAlertExamples> logger)
    {
        _alertClient = alertClient;
        _logger = logger;
    }

    /// <summary>
    /// Example 1: Alert on critical business logic failure.
    /// </summary>
    public async Task ProcessPaymentExample(decimal amount, string orderId)
    {
        try
        {
            // Payment processing logic
            await ProcessPayment(amount, orderId);
        }
        catch (PaymentDeclinedException ex)
        {
            // Send critical alert immediately
            await _alertClient.SendCriticalAlertAsync(
                name: "PaymentDeclined",
                description: $"Payment of ${amount} declined for order {orderId}: {ex.DeclineReason}",
                labels: new Dictionary<string, string>
                {
                    ["order_id"] = orderId,
                    ["amount"] = amount.ToString("F2"),
                    ["decline_reason"] = ex.DeclineReason,
                    ["payment_method"] = ex.PaymentMethod
                });

            throw;
        }
    }

    /// <summary>
    /// Example 2: Alert on data integrity issues.
    /// </summary>
    public async Task ValidateDataIntegrityExample()
    {
        var inconsistencies = await CheckDataIntegrity();

        if (inconsistencies.Count > 0)
        {
            await _alertClient.SendAlertAsync(
                name: "DataIntegrityViolation",
                severity: "high",
                description: $"Found {inconsistencies.Count} data integrity violations",
                labels: new Dictionary<string, string>
                {
                    ["violation_count"] = inconsistencies.Count.ToString(),
                    ["check_type"] = "referential_integrity"
                });
        }
    }

    /// <summary>
    /// Example 3: Alert on authentication failures.
    /// </summary>
    public async Task HandleFailedLoginExample(string username, string ipAddress, int failureCount)
    {
        if (failureCount >= 5)
        {
            await _alertClient.SendAlertAsync(
                name: "BruteForceAttempt",
                severity: "critical",
                description: $"User {username} has {failureCount} failed login attempts from {ipAddress}",
                labels: new Dictionary<string, string>
                {
                    ["username"] = username,
                    ["ip_address"] = ipAddress,
                    ["failure_count"] = failureCount.ToString(),
                    ["threat_level"] = "high"
                });
        }
    }

    /// <summary>
    /// Example 4: Alert on resource exhaustion.
    /// </summary>
    public async Task CheckResourceLimitsExample()
    {
        var storageUsage = await GetStorageUsage();

        if (storageUsage.PercentUsed > 90)
        {
            await _alertClient.SendCriticalAlertAsync(
                name: "StorageNearCapacity",
                description: $"Storage at {storageUsage.PercentUsed}% capacity ({storageUsage.UsedGB}GB / {storageUsage.TotalGB}GB)",
                labels: new Dictionary<string, string>
                {
                    ["percent_used"] = storageUsage.PercentUsed.ToString("F1"),
                    ["used_gb"] = storageUsage.UsedGB.ToString(),
                    ["total_gb"] = storageUsage.TotalGB.ToString(),
                    ["storage_type"] = "raster_cache"
                });
        }
    }

    /// <summary>
    /// Example 5: Alert on external service failures.
    /// </summary>
    public async Task CallExternalApiExample()
    {
        try
        {
            await CallExternalService();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            await _alertClient.SendAlertAsync(
                name: "ExternalServiceUnavailable",
                severity: "medium",
                description: $"External geocoding service is unavailable: {ex.Message}",
                labels: new Dictionary<string, string>
                {
                    ["service_name"] = "geocoding-api",
                    ["error_type"] = "service_unavailable",
                    ["status_code"] = "503"
                });

            // Fall back to cached data
            await UseCachedGeocodingData();
        }
    }

    /// <summary>
    /// Example 6: Alert on SLA violations.
    /// </summary>
    public async Task MonitorSlaExample(string operationName, TimeSpan duration, TimeSpan slaThreshold)
    {
        if (duration > slaThreshold)
        {
            var violationPercent = (duration.TotalMilliseconds / slaThreshold.TotalMilliseconds - 1) * 100;

            await _alertClient.SendAlertAsync(
                name: "SlaViolation",
                severity: "medium",
                description: $"{operationName} took {duration.TotalMilliseconds}ms (SLA: {slaThreshold.TotalMilliseconds}ms, {violationPercent:F0}% over)",
                labels: new Dictionary<string, string>
                {
                    ["operation"] = operationName,
                    ["duration_ms"] = duration.TotalMilliseconds.ToString("F0"),
                    ["sla_ms"] = slaThreshold.TotalMilliseconds.ToString("F0"),
                    ["violation_percent"] = violationPercent.ToString("F0")
                });
        }
    }

    // Mock methods for examples
    private Task ProcessPayment(decimal amount, string orderId) => Task.CompletedTask;
    private Task<List<string>> CheckDataIntegrity() => Task.FromResult(new List<string>());
    private Task<StorageInfo> GetStorageUsage() => Task.FromResult(new StorageInfo());
    private Task CallExternalService() => Task.CompletedTask;
    private Task UseCachedGeocodingData() => Task.CompletedTask;

    private class StorageInfo
    {
        public int PercentUsed { get; set; }
        public long UsedGB { get; set; }
        public long TotalGB { get; set; }
    }
}

public class PaymentDeclinedException : Exception
{
    public string DeclineReason { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
}
