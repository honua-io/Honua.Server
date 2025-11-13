// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for container registry operations.
/// </summary>
public class RegistryMetrics
{
    private readonly Counter<long> registryProvisioning;
    private readonly Histogram<double> provisioningDuration;
    private readonly Counter<long> registryAccess;
    private readonly Counter<long> credentialRevocations;
    private readonly Counter<long> registryErrors;
    private readonly ObservableGauge<int> activeRegistries;

    private readonly Dictionary<string, int> activeRegistriesByProvider = new();
    private readonly object lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryMetrics"/> class.
    /// </summary>
    /// <param name="meterFactory">The meter factory.</param>
    public RegistryMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Registry");

        this.registryProvisioning = meter.CreateCounter<long>(
            "registry_provisioning_total",
            description: "Total number of registry provisioning operations");

        this.provisioningDuration = meter.CreateHistogram<double>(
            "registry_provisioning_duration_seconds",
            unit: "s",
            description: "Registry provisioning duration in seconds");

        this.registryAccess = meter.CreateCounter<long>(
            "registry_access_total",
            description: "Total number of registry access operations");

        this.credentialRevocations = meter.CreateCounter<long>(
            "credential_revocations_total",
            description: "Total number of credential revocations");

        this.registryErrors = meter.CreateCounter<long>(
            "registry_errors_total",
            description: "Total number of registry errors");

        this.activeRegistries = meter.CreateObservableGauge(
            "active_registries_total",
            observeValues: this.ObserveActiveRegistries,
            description: "Current number of active registries by provider");
    }

    /// <summary>
    /// Records a registry provisioning operation.
    /// </summary>
    /// <param name="provider">The registry provider.</param>
    /// <param name="success">Whether provisioning succeeded.</param>
    /// <param name="duration">The provisioning duration.</param>
    public void RecordProvisioning(string provider, bool success, TimeSpan duration)
    {
        this.registryProvisioning.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));

        this.provisioningDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("success", success.ToString()));
    }

    /// <summary>
    /// Records a registry access operation.
    /// </summary>
    /// <param name="provider">The registry provider.</param>
    /// <param name="operation">The operation type.</param>
    public void RecordAccess(string provider, string operation)
    {
        this.registryAccess.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    /// Records a credential revocation.
    /// </summary>
    /// <param name="provider">The registry provider.</param>
    /// <param name="reason">The revocation reason.</param>
    public void RecordCredentialRevocation(string provider, string reason)
    {
        this.credentialRevocations.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("reason", reason));
    }

    /// <summary>
    /// Records a registry error.
    /// </summary>
    /// <param name="provider">The registry provider.</param>
    /// <param name="errorType">The error type.</param>
    public void RecordError(string provider, string errorType)
    {
        this.registryErrors.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Updates the count of active registries for a provider.
    /// </summary>
    /// <param name="provider">The registry provider.</param>
    /// <param name="count">The registry count.</param>
    public void UpdateActiveRegistries(string provider, int count)
    {
        lock (this.lockObject)
        {
            this.activeRegistriesByProvider[provider] = count;
        }
    }

    private IEnumerable<Measurement<int>> ObserveActiveRegistries()
    {
        lock (this.lockObject)
        {
            foreach (var kvp in this.activeRegistriesByProvider)
            {
                yield return new Measurement<int>(
                    kvp.Value,
                    new KeyValuePair<string, object?>("provider", kvp.Key));
            }
        }
    }
}
