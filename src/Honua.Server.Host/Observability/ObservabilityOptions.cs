// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Host.Observability;

/// <summary>
/// Configuration options for application observability including logging, metrics, and distributed tracing.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Gets or sets the logging configuration options.
    /// </summary>
    public LoggingOptions Logging { get; init; } = new();

    /// <summary>
    /// Gets or sets the metrics collection and export configuration.
    /// </summary>
    public MetricsOptions Metrics { get; init; } = new();

    /// <summary>
    /// Gets or sets the distributed tracing configuration.
    /// </summary>
    public TracingOptions Tracing { get; init; } = new();

    /// <summary>
    /// Configuration options for application logging.
    /// </summary>
    public sealed class LoggingOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use JSON-formatted console logging.
        /// When true, logs are written in structured JSON format. When false or null, uses plain text format.
        /// </summary>
        public bool? JsonConsole { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether to include logging scopes in log output.
        /// Scopes provide additional context for log messages. Null uses framework defaults.
        /// </summary>
        public bool? IncludeScopes { get; init; }
    }

    /// <summary>
    /// Configuration options for metrics collection and export.
    /// </summary>
    public sealed class MetricsOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether metrics collection is enabled.
        /// When false, no metrics will be collected or exposed.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// Gets or sets the HTTP endpoint path where metrics are exposed.
        /// Default is "/metrics".
        /// </summary>
        public string Endpoint { get; init; } = "/metrics";

        /// <summary>
        /// Gets or sets a value indicating whether to use Prometheus format for metrics export.
        /// When true, metrics are exposed in Prometheus text format. Default is true.
        /// </summary>
        public bool UsePrometheus { get; init; } = true;
    }

    public sealed class TracingOptions
    {
        /// <summary>
        /// Exporter type: "none", "console", or "otlp"
        /// </summary>
        public string Exporter { get; init; } = "none";

        /// <summary>
        /// OTLP endpoint URL (e.g., "http://localhost:4317" for Jaeger)
        /// </summary>
        public string? OtlpEndpoint { get; init; }
    }
}
