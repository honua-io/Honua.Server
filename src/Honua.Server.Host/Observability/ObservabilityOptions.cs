// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Host.Observability;

public sealed class ObservabilityOptions
{
    public LoggingOptions Logging { get; init; } = new();

    public MetricsOptions Metrics { get; init; } = new();

    public TracingOptions Tracing { get; init; } = new();

    public sealed class LoggingOptions
    {
        public bool? JsonConsole { get; init; }

        public bool? IncludeScopes { get; init; }
    }

    public sealed class MetricsOptions
    {
        public bool Enabled { get; init; }

        public string Endpoint { get; init; } = "/metrics";

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
