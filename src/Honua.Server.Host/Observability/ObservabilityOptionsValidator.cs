// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Validates observability configuration options.
/// </summary>
public sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
    {
        var failures = new List<string>();

        // Validate metrics configuration
        if (options.Metrics?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(options.Metrics.Endpoint))
            {
                failures.Add("Observability Metrics endpoint is required when metrics are enabled. Set 'observability:metrics:endpoint'. Example: '/metrics'");
            }
            else if (!options.Metrics.Endpoint.StartsWith("/"))
            {
                failures.Add($"Observability Metrics endpoint '{options.Metrics.Endpoint}' must start with '/'. Set 'observability:metrics:endpoint'. Example: '/metrics'");
            }
        }

        // Validate tracing configuration
        if (options.Tracing != null)
        {
            var validExporters = new[] { "none", "console", "otlp" };
            if (!Array.Exists(validExporters, e => e.Equals(options.Tracing.Exporter, StringComparison.OrdinalIgnoreCase)))
            {
                failures.Add($"Observability Tracing exporter '{options.Tracing.Exporter}' is invalid. Valid values: {string.Join(", ", validExporters)}. Set 'observability:tracing:exporter'.");
            }

            // Validate OTLP endpoint if OTLP exporter is selected
            if (options.Tracing.Exporter.Equals("otlp", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(options.Tracing.OtlpEndpoint))
                {
                    failures.Add("Observability Tracing OTLP endpoint is required when exporter is 'otlp'. Set 'observability:tracing:otlpEndpoint'. Example: 'http://localhost:4317'");
                }
                else if (!Uri.TryCreate(options.Tracing.OtlpEndpoint, UriKind.Absolute, out var uri))
                {
                    failures.Add($"Observability Tracing OTLP endpoint '{options.Tracing.OtlpEndpoint}' is not a valid URL. Set 'observability:tracing:otlpEndpoint'. Example: 'http://localhost:4317'");
                }
                else if (uri.Scheme != "http" && uri.Scheme != "https")
                {
                    failures.Add($"Observability Tracing OTLP endpoint '{options.Tracing.OtlpEndpoint}' must use http or https scheme. Set 'observability:tracing:otlpEndpoint'. Example: 'http://localhost:4317'");
                }
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
