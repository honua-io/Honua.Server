// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.Metrics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Metrics for plugin system monitoring.
/// </summary>
public interface IPluginMetrics
{
    /// <summary>
    /// Records that a plugin was loaded successfully.
    /// </summary>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <param name="loadDuration">The time taken to load the plugin.</param>
    void RecordPluginLoaded(string pluginName, TimeSpan loadDuration);

    /// <summary>
    /// Records that a plugin was unloaded.
    /// </summary>
    /// <param name="pluginName">The name of the plugin.</param>
    void RecordPluginUnloaded(string pluginName);

    /// <summary>
    /// Records a plugin error.
    /// </summary>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <param name="errorType">The type of error that occurred.</param>
    void RecordPluginError(string pluginName, string errorType);

    /// <summary>
    /// Records a plugin operation with its duration and success status.
    /// </summary>
    /// <param name="pluginName">The name of the plugin.</param>
    /// <param name="operation">The operation performed.</param>
    /// <param name="duration">The duration of the operation.</param>
    /// <param name="success">Whether the operation succeeded.</param>
    void RecordPluginOperation(string pluginName, string operation, TimeSpan duration, bool success);
}

/// <summary>
/// Implementation of plugin metrics using OpenTelemetry.
/// </summary>
public sealed class PluginMetrics : IPluginMetrics, IDisposable
{
    private readonly Meter meter;
    private readonly Counter<long> pluginsLoaded;
    private readonly Counter<long> pluginsUnloaded;
    private readonly Counter<long> pluginErrors;
    private readonly Histogram<double> pluginLoadDuration;
    private readonly Histogram<double> pluginOperationDuration;

    public PluginMetrics()
    {
        this.meter = new Meter("Honua.Server.Plugins", "1.0.0");

        this.pluginsLoaded = this.meter.CreateCounter<long>(
            "honua.plugins.loaded",
            unit: "{plugin}",
            description: "Number of plugins loaded");

        this.pluginsUnloaded = this.meter.CreateCounter<long>(
            "honua.plugins.unloaded",
            unit: "{plugin}",
            description: "Number of plugins unloaded");

        this.pluginErrors = this.meter.CreateCounter<long>(
            "honua.plugins.errors",
            unit: "{error}",
            description: "Number of plugin errors");

        this.pluginLoadDuration = this.meter.CreateHistogram<double>(
            "honua.plugins.load_duration",
            unit: "ms",
            description: "Plugin load duration");

        this.pluginOperationDuration = this.meter.CreateHistogram<double>(
            "honua.plugins.operation_duration",
            unit: "ms",
            description: "Plugin operation duration");
    }

    public void RecordPluginLoaded(string pluginName, TimeSpan loadDuration)
    {
        this.pluginsLoaded.Add(1, new KeyValuePair<string, object?>("plugin.name", NormalizePluginName(pluginName)));
        this.pluginLoadDuration.Record(
            loadDuration.TotalMilliseconds,
            new KeyValuePair<string, object?>("plugin.name", NormalizePluginName(pluginName)));
    }

    public void RecordPluginUnloaded(string pluginName)
    {
        this.pluginsUnloaded.Add(1, new KeyValuePair<string, object?>("plugin.name", NormalizePluginName(pluginName)));
    }

    public void RecordPluginError(string pluginName, string errorType)
    {
        this.pluginErrors.Add(
            1,
            new("plugin.name", NormalizePluginName(pluginName)),
            new("error.type", NormalizeErrorType(errorType)));
    }

    public void RecordPluginOperation(string pluginName, string operation, TimeSpan duration, bool success)
    {
        this.pluginOperationDuration.Record(
            duration.TotalMilliseconds,
            new("plugin.name", NormalizePluginName(pluginName)),
            new("operation", NormalizeOperation(operation)),
            new("success", success.ToString().ToLowerInvariant()));
    }

    public void Dispose()
    {
        this.meter.Dispose();
    }

    private static string NormalizePluginName(string? pluginName)
        => string.IsNullOrWhiteSpace(pluginName) ? "unknown" : pluginName;

    private static string NormalizeErrorType(string? errorType)
        => string.IsNullOrWhiteSpace(errorType) ? "unknown" : errorType.ToLowerInvariant();

    private static string NormalizeOperation(string? operation)
        => string.IsNullOrWhiteSpace(operation) ? "unknown" : operation.ToLowerInvariant();
}
