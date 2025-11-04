// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for license tracking and quota management.
/// </summary>
public class LicenseMetrics
{
    private readonly ObservableGauge<int> _activeLicenses;
    private readonly ObservableGauge<double> _licenseQuotaUsage;
    private readonly Counter<long> _licenseRevocations;
    private readonly Counter<long> _licenseValidations;
    private readonly Counter<long> _quotaExceeded;

    private readonly Dictionary<string, int> _activeLicensesByTier = new();
    private readonly Dictionary<(string CustomerId, string QuotaType), double> _quotaUsageByCustomer = new();
    private readonly object _lock = new();

    public LicenseMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.License");

        _activeLicenses = meter.CreateObservableGauge(
            "active_licenses_total",
            observeValues: ObserveActiveLicenses,
            description: "Current number of active licenses by tier");

        _licenseQuotaUsage = meter.CreateObservableGauge(
            "license_quota_usage_percent",
            observeValues: ObserveLicenseQuotaUsage,
            description: "License quota usage percentage by customer and quota type");

        _licenseRevocations = meter.CreateCounter<long>(
            "license_revocations_total",
            description: "Total number of license revocations");

        _licenseValidations = meter.CreateCounter<long>(
            "license_validations_total",
            description: "Total number of license validations");

        _quotaExceeded = meter.CreateCounter<long>(
            "quota_exceeded_total",
            description: "Total number of quota exceeded events");
    }

    /// <summary>
    /// Updates active license count for a tier.
    /// </summary>
    public void UpdateActiveLicenses(string tier, int count)
    {
        lock (_lock)
        {
            _activeLicensesByTier[tier] = count;
        }
    }

    /// <summary>
    /// Updates quota usage for a customer.
    /// </summary>
    public void UpdateQuotaUsage(string customerId, string quotaType, double usagePercent)
    {
        lock (_lock)
        {
            _quotaUsageByCustomer[(customerId, quotaType)] = usagePercent;
        }
    }

    /// <summary>
    /// Records a license revocation.
    /// </summary>
    public void RecordRevocation(string reason, string tier)
    {
        _licenseRevocations.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records a license validation attempt.
    /// </summary>
    public void RecordValidation(bool success, string tier)
    {
        _licenseValidations.Add(1,
            new KeyValuePair<string, object?>("success", success.ToString()),
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records a quota exceeded event.
    /// </summary>
    public void RecordQuotaExceeded(string customerId, string quotaType)
    {
        _quotaExceeded.Add(1,
            new KeyValuePair<string, object?>("customer_id", customerId),
            new KeyValuePair<string, object?>("quota_type", quotaType));
    }

    private IEnumerable<Measurement<int>> ObserveActiveLicenses()
    {
        lock (_lock)
        {
            foreach (var kvp in _activeLicensesByTier)
            {
                yield return new Measurement<int>(
                    kvp.Value,
                    new KeyValuePair<string, object?>("tier", kvp.Key));
            }
        }
    }

    private IEnumerable<Measurement<double>> ObserveLicenseQuotaUsage()
    {
        lock (_lock)
        {
            foreach (var kvp in _quotaUsageByCustomer)
            {
                yield return new Measurement<double>(
                    kvp.Value,
                    new KeyValuePair<string, object?>("customer_id", kvp.Key.CustomerId),
                    new KeyValuePair<string, object?>("quota_type", kvp.Key.QuotaType));
            }
        }
    }
}
