// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Trace;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Service for managing runtime tracing configuration changes.
/// Allows dynamic adjustment of tracing exporters and sampling without restart.
/// </summary>
public sealed class RuntimeTracingConfigurationService
{
    private TracingConfiguration _current;
    private readonly object _lock = new();

    public RuntimeTracingConfigurationService(TracingConfiguration initialConfiguration)
    {
        this.current = Guard.NotNull(initialConfiguration);
    }

    /// <summary>
    /// Gets the current tracing configuration.
    /// </summary>
    public TracingConfiguration Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Updates the tracing configuration at runtime.
    /// </summary>
    /// <param name="newConfiguration">The new tracing configuration to apply.</param>
    /// <returns>True if the configuration was updated successfully.</returns>
    public bool UpdateConfiguration(TracingConfiguration newConfiguration)
    {
        Guard.NotNull(newConfiguration);

        lock (_lock)
        {
            var oldConfig = _current;
            this.current = newConfiguration;

            // If exporter type changed, we need to rebuild the TracerProvider
            // Note: OpenTelemetry doesn't support hot-swapping exporters, so this is informational
            // A full restart is still recommended for production use
            if (oldConfig.Exporter != newConfiguration.Exporter ||
                oldConfig.OtlpEndpoint != newConfiguration.OtlpEndpoint)
            {
                // Configuration changed, but actual exporter won't update until restart
                // This service tracks the desired state for the next restart
                return true;
            }

            return true;
        }
    }

    /// <summary>
    /// Updates the sampling ratio at runtime.
    /// </summary>
    public bool UpdateSamplingRatio(double samplingRatio)
    {
        if (samplingRatio < 0.0 || samplingRatio > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(samplingRatio), "Sampling ratio must be between 0.0 and 1.0");
        }

        lock (_lock)
        {
            this.current = _current with { SamplingRatio = samplingRatio };
            return true;
        }
    }

    /// <summary>
    /// Updates the OTLP endpoint at runtime.
    /// </summary>
    public bool UpdateOtlpEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));
        }

        lock (_lock)
        {
            this.current = _current with { OtlpEndpoint = endpoint };
            return true;
        }
    }

    /// <summary>
    /// Updates the exporter type at runtime.
    /// </summary>
    public bool UpdateExporter(string exporter)
    {
        var normalizedExporter = exporter?.ToLowerInvariant() ?? "none";
        if (normalizedExporter != "none" && normalizedExporter != "console" && normalizedExporter != "otlp")
        {
            throw new ArgumentException("Exporter must be 'none', 'console', or 'otlp'", nameof(exporter));
        }

        lock (_lock)
        {
            this.current = _current with { Exporter = normalizedExporter };
            return true;
        }
    }

    /// <summary>
    /// Creates a test activity to verify tracing configuration.
    /// </summary>
    public ActivityTestResult CreateTestActivity(string activityName, Dictionary<string, object?>? tags = null)
    {
        var stopwatch = Stopwatch.StartNew();

        using var activity = Activity.Current?.Source.StartActivity(activityName)
            ?? new ActivitySource("Honua.Server.Test").StartActivity(activityName);

        if (activity == null)
        {
            return new ActivityTestResult
            {
                Success = false,
                Message = "No ActivityListener is configured. Tracing may be disabled.",
                ActivityId = null,
                TraceId = null,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        // Add tags if provided
        if (tags != null)
        {
            foreach (var (key, value) in tags)
            {
                activity.SetTag(key, value);
            }
        }

        activity.SetTag("test", true);
        activity.SetTag("timestamp", DateTimeOffset.UtcNow.ToString("o"));

        stopwatch.Stop();

        return new ActivityTestResult
        {
            Success = true,
            Message = $"Test activity '{activityName}' created successfully",
            ActivityId = activity.Id,
            TraceId = activity.TraceId.ToString(),
            SpanId = activity.SpanId.ToString(),
            DurationMs = stopwatch.ElapsedMilliseconds,
            Tags = tags
        };
    }

    /// <summary>
    /// Gets statistics about the current tracing configuration.
    /// </summary>
    public TracingStatistics GetStatistics()
    {
        lock (_lock)
        {
            return new TracingStatistics
            {
                Exporter = this.current.Exporter,
                OtlpEndpoint = this.current.OtlpEndpoint,
                SamplingRatio = this.current.SamplingRatio,
                IsEnabled = this.current.Exporter != "none",
                ActivitySourcesConfigured = new[]
                {
                    "Honua.Server.OgcProtocols",
                    "Honua.Server.OData",
                    "Honua.Server.Stac",
                    "Honua.Server.Database",
                    "Honua.Server.RasterTiles",
                    "Honua.Server.Metadata",
                    "Honua.Server.Authentication",
                    "Honua.Server.Export",
                    "Honua.Server.Import"
                },
                Note = this.current.Exporter == "none"
                    ? "Tracing is disabled. Set exporter to 'console' or 'otlp' to enable."
                    : "Note: Changes to exporter type or OTLP endpoint require application restart to take effect. Sampling ratio changes apply immediately."
            };
        }
    }
}

/// <summary>
/// Tracing configuration record.
/// </summary>
public sealed record TracingConfiguration
{
    /// <summary>
    /// Exporter type: "none", "console", or "otlp"
    /// </summary>
    public string Exporter { get; init; } = "none";

    /// <summary>
    /// OTLP endpoint URL (only used when Exporter is "otlp")
    /// </summary>
    public string? OtlpEndpoint { get; init; }

    /// <summary>
    /// Sampling ratio (0.0 to 1.0). Default is 1.0 (100% sampling).
    /// </summary>
    public double SamplingRatio { get; init; } = 1.0;
}

/// <summary>
/// Result of a test activity creation.
/// </summary>
public sealed record ActivityTestResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ActivityId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public long DurationMs { get; init; }
    public Dictionary<string, object?>? Tags { get; init; }
}

/// <summary>
/// Statistics about the current tracing configuration.
/// </summary>
public sealed record TracingStatistics
{
    public string Exporter { get; init; } = "none";
    public string? OtlpEndpoint { get; init; }
    public double SamplingRatio { get; init; }
    public bool IsEnabled { get; init; }
    public string[] ActivitySourcesConfigured { get; init; } = Array.Empty<string>();
    public string Note { get; init; } = string.Empty;
}
