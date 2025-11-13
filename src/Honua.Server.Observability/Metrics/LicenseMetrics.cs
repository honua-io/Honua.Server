// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for license tracking and quota management.
/// </summary>
public class LicenseMetrics
{
    private readonly ObservableGauge<int> activeLicenses;
    private readonly ObservableGauge<double> licenseQuotaUsage;
    private readonly Counter<long> licenseRevocations;
    private readonly Counter<long> licenseValidations;
    private readonly Counter<long> quotaExceeded;

    private readonly Dictionary<string, int> activeLicensesByTier = new();
    private readonly Dictionary<(string CustomerId, string QuotaType), double> quotaUsageByCustomer = new();
    private readonly object lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    public LicenseMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.License");

        this.activeLicenses = meter.CreateObservableGauge(
            "active_licenses_total",
            observeValues: this.ObserveActiveLicenses,
            description: "Current number of active licenses by tier");

        this.licenseQuotaUsage = meter.CreateObservableGauge(
            "license_quota_usage_percent",
            observeValues: this.ObserveLicenseQuotaUsage,
            description: "License quota usage percentage by customer and quota type");

        this.licenseRevocations = meter.CreateCounter<long>(
            "license_revocations_total",
            description: "Total number of license revocations");

        this.licenseValidations = meter.CreateCounter<long>(
            "license_validations_total",
            description: "Total number of license validations");

        this.quotaExceeded = meter.CreateCounter<long>(
            "quota_exceeded_total",
            description: "Total number of quota exceeded events");
    }

    /// <summary>
    /// Updates active license count for a tier.
    /// </summary>
    /// <param name="tier">The license tier.</param>
    /// <param name="count">The license count.</param>
    public void UpdateActiveLicenses(string tier, int count)
    {
        lock (this.lockObject)
        {
            this.activeLicensesByTier[tier] = count;
        }
    }

    /// <summary>
    /// Updates quota usage for a customer.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="quotaType">The quota type.</param>
    /// <param name="usagePercent">The usage percentage.</param>
    public void UpdateQuotaUsage(string customerId, string quotaType, double usagePercent)
    {
        lock (this.lockObject)
        {
            this.quotaUsageByCustomer[(customerId, quotaType)] = usagePercent;
        }
    }

    /// <summary>
    /// Records a license revocation.
    /// </summary>
    /// <param name="reason">The revocation reason.</param>
    /// <param name="tier">The license tier.</param>
    public void RecordRevocation(string reason, string tier)
    {
        this.licenseRevocations.Add(1,
            new KeyValuePair<string, object?>("reason", reason),
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records a license validation attempt.
    /// </summary>
    /// <param name="success">Whether validation succeeded.</param>
    /// <param name="tier">The license tier.</param>
    public void RecordValidation(bool success, string tier)
    {
        this.licenseValidations.Add(1,
            new KeyValuePair<string, object?>("success", success.ToString()),
            new KeyValuePair<string, object?>("tier", tier));
    }

    /// <summary>
    /// Records a quota exceeded event.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="quotaType">The quota type.</param>
    public void RecordQuotaExceeded(string customerId, string quotaType)
    {
        this.quotaExceeded.Add(1,
            new KeyValuePair<string, object?>("customer_id", customerId),
            new KeyValuePair<string, object?>("quota_type", quotaType));
    }

    private IEnumerable<Measurement<int>> ObserveActiveLicenses()
    {
        lock (this.lockObject)
        {
            foreach (var kvp in this.activeLicensesByTier)
            {
                yield return new Measurement<int>(
                    kvp.Value,
                    new KeyValuePair<string, object?>("tier", kvp.Key));
            }
        }
    }

    private IEnumerable<Measurement<double>> ObserveLicenseQuotaUsage()
    {
        lock (this.lockObject)
        {
            foreach (var kvp in this.quotaUsageByCustomer)
            {
                yield return new Measurement<double>(
                    kvp.Value,
                    new KeyValuePair<string, object?>("customer_id", kvp.Key.CustomerId),
                    new KeyValuePair<string, object?>("quota_type", kvp.Key.QuotaType));
            }
        }
    }
}
