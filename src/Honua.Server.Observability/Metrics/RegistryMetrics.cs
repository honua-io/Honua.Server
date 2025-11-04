// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics.Metrics;

namespace Honua.Server.Observability.Metrics;

/// <summary>
/// Provides metrics instrumentation for container registry operations.
/// </summary>
public class RegistryMetrics
{
    private readonly Counter<long> _registryProvisioning;
    private readonly Histogram<double> _provisioningDuration;
    private readonly Counter<long> _registryAccess;
    private readonly Counter<long> _credentialRevocations;
    private readonly Counter<long> _registryErrors;
    private readonly ObservableGauge<int> _activeRegistries;

    private readonly Dictionary<string, int> _activeRegistriesByProvider = new();
    private readonly object _lock = new();

    public RegistryMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Honua.Registry");

        _registryProvisioning = meter.CreateCounter<long>(
            "registry_provisioning_total",
            description: "Total number of registry provisioning operations");

        _provisioningDuration = meter.CreateHistogram<double>(
            "registry_provisioning_duration_seconds",
            unit: "s",
            description: "Registry provisioning duration in seconds");

        _registryAccess = meter.CreateCounter<long>(
            "registry_access_total",
            description: "Total number of registry access operations");

        _credentialRevocations = meter.CreateCounter<long>(
            "credential_revocations_total",
            description: "Total number of credential revocations");

        _registryErrors = meter.CreateCounter<long>(
            "registry_errors_total",
            description: "Total number of registry errors");

        _activeRegistries = meter.CreateObservableGauge(
            "active_registries_total",
            observeValues: ObserveActiveRegistries,
            description: "Current number of active registries by provider");
    }

    /// <summary>
    /// Records a registry provisioning operation.
    /// </summary>
    public void RecordProvisioning(string provider, bool success, TimeSpan duration)
    {
        _registryProvisioning.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));

        _provisioningDuration.Record(duration.TotalSeconds,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("success", success.ToString()));
    }

    /// <summary>
    /// Records a registry access operation.
    /// </summary>
    public void RecordAccess(string provider, string operation)
    {
        _registryAccess.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    /// Records a credential revocation.
    /// </summary>
    public void RecordCredentialRevocation(string provider, string reason)
    {
        _credentialRevocations.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("reason", reason));
    }

    /// <summary>
    /// Records a registry error.
    /// </summary>
    public void RecordError(string provider, string errorType)
    {
        _registryErrors.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Updates the count of active registries for a provider.
    /// </summary>
    public void UpdateActiveRegistries(string provider, int count)
    {
        lock (_lock)
        {
            _activeRegistriesByProvider[provider] = count;
        }
    }

    private IEnumerable<Measurement<int>> ObserveActiveRegistries()
    {
        lock (_lock)
        {
            foreach (var kvp in _activeRegistriesByProvider)
            {
                yield return new Measurement<int>(
                    kvp.Value,
                    new KeyValuePair<string, object?>("provider", kvp.Key));
            }
        }
    }
}
