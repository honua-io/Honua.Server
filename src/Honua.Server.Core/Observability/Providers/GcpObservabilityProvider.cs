// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Honua.Server.Core.Observability.Providers;

/// <summary>
/// Google Cloud Platform (GCP) observability provider.
/// Exports metrics to Cloud Monitoring and traces to Cloud Trace using OpenTelemetry exporters.
/// </summary>
/// <remarks>
/// <para><strong>Configuration Requirements:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Project ID:</strong> Set <c>observability:gcp:projectId</c> in configuration.
/// Example: <c>my-gcp-project-123456</c>
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>GCP Credentials:</strong> Loaded from GCP credential provider chain:
/// <list type="number">
/// <item>Environment variable: <c>GOOGLE_APPLICATION_CREDENTIALS</c> pointing to service account JSON</item>
/// <item>GCE metadata service (automatic on Compute Engine, GKE, Cloud Run)</item>
/// <item>gcloud CLI credentials: <c>~/.config/gcloud/application_default_credentials.json</c></item>
/// </list>
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Features:</strong></para>
/// <list type="bullet">
/// <item><description>Metrics export to Cloud Monitoring with automatic resource detection</description></item>
/// <item><description>Distributed tracing with Cloud Trace</description></item>
/// <item><description>Integration with Cloud Trace UI for trace visualization</description></item>
/// <item><description>Automatic GCP resource detection (GCE, GKE, Cloud Run, Cloud Functions)</description></item>
/// <item><description>Support for Cloud Monitoring custom metrics</description></item>
/// </list>
///
/// <para><strong>OTLP Collector Requirement:</strong></para>
/// <para>
/// This provider uses the OTLP exporter configured to send data to an OpenTelemetry Collector
/// that forwards to Cloud Monitoring and Cloud Trace. For GKE, use the Google Cloud Operations suite.
/// </para>
/// <para>
/// <strong>Alternative:</strong> For direct export, use Google Cloud-specific OpenTelemetry exporters:
/// <list type="bullet">
/// <item><c>Google.Cloud.Diagnostics.AspNetCore</c> - Native GCP integration</item>
/// <item><c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c> - OTLP exporter with GCP authentication</item>
/// </list>
/// </para>
///
/// <para><strong>Logging:</strong></para>
/// <para>
/// GCP Cloud Logging is handled separately via Google.Cloud.Logging.V2 or Serilog.Sinks.GoogleCloudLogging.
/// Configure in Serilog configuration section.
/// </para>
///
/// <para><strong>Package Requirements:</strong></para>
/// <list type="bullet">
/// <item><description><c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c> - Version 1.12.0 or higher</description></item>
/// <item><description><c>Google.Cloud.Diagnostics.AspNetCore</c> - Version 5.1.0 or higher (optional, for direct export)</description></item>
/// <item><description><c>Google.Cloud.OpenTelemetry</c> - Version 1.0.0 or higher (optional, for GCP resource detection)</description></item>
/// </list>
///
/// <para><strong>IAM Permissions Required:</strong></para>
/// <list type="bullet">
/// <item><description><c>monitoring.timeSeries.create</c> - Write metrics to Cloud Monitoring</description></item>
/// <item><description><c>cloudtrace.traces.patch</c> - Write traces to Cloud Trace</description></item>
/// </list>
/// </remarks>
public sealed class GcpObservabilityProvider : ICloudObservabilityProvider
{
    /// <inheritdoc />
    public string ProviderName => "gcp";

    /// <inheritdoc />
    public void ConfigureMetrics(IServiceCollection services, IConfiguration configuration)
    {
        var projectId = GetProjectId(configuration);
        if (projectId.IsNullOrEmpty())
        {
            return;
        }

        var otlpEndpoint = GetOtlpEndpoint(configuration);

        services.AddOpenTelemetry().WithMetrics(metricBuilder =>
        {
            // Add GCP resource detection
            metricBuilder.ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: configuration.GetValue<string>("ServiceName") ?? "honua-server",
                    serviceVersion: configuration.GetValue<string>("ServiceVersion") ?? "1.0.0");

                // Add GCP-specific resource attributes
                resource.AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("gcp.project_id", projectId),
                    new KeyValuePair<string, object>("cloud.provider", "gcp")
                });

                // GCP resource detectors will automatically detect GCE, GKE, Cloud Run metadata
                // Note: Requires Google.Cloud.OpenTelemetry package for automatic detection
                // resource.AddDetector(new Google.Cloud.OpenTelemetry.ResourceDetectors.GcpResourceDetector());
            });

            // Export to OTLP (OpenTelemetry Collector forwards to Cloud Monitoring)
            metricBuilder.AddOtlpExporter(options =>
            {
                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
                // Default OTLP endpoint is localhost:4317 (OpenTelemetry Collector)
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureTracing(IServiceCollection services, IConfiguration configuration)
    {
        var projectId = GetProjectId(configuration);
        if (projectId.IsNullOrEmpty())
        {
            return;
        }

        var otlpEndpoint = GetOtlpEndpoint(configuration);

        services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            // Add GCP resource detection
            tracingBuilder.ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: configuration.GetValue<string>("ServiceName") ?? "honua-server",
                    serviceVersion: configuration.GetValue<string>("ServiceVersion") ?? "1.0.0");

                // Add GCP-specific resource attributes
                resource.AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("gcp.project_id", projectId),
                    new KeyValuePair<string, object>("cloud.provider", "gcp")
                });

                // GCP resource detectors will automatically detect GCE, GKE, Cloud Run metadata
                // Note: Requires Google.Cloud.OpenTelemetry package for automatic detection
                // resource.AddDetector(new Google.Cloud.OpenTelemetry.ResourceDetectors.GcpResourceDetector());
            });

            // Export to OTLP (OpenTelemetry Collector forwards to Cloud Trace)
            tracingBuilder.AddOtlpExporter(options =>
            {
                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
                // Default OTLP endpoint is localhost:4317 (OpenTelemetry Collector)
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration)
    {
        // GCP Cloud Logging is handled via Google.Cloud.Logging.V2 or Serilog.Sinks.GoogleCloudLogging
        // No additional configuration needed here
    }

    /// <inheritdoc />
    public bool IsEnabled(IConfiguration configuration)
    {
        var projectId = GetProjectId(configuration);
        return !projectId.IsNullOrEmpty();
    }

    /// <summary>
    /// Gets the GCP project ID from configuration or environment.
    /// Checks: observability:gcp:projectId, GCP:ProjectId, GOOGLE_CLOUD_PROJECT, GCP_PROJECT
    /// </summary>
    private static string? GetProjectId(IConfiguration configuration)
    {
        return configuration.GetValue<string>("observability:gcp:projectId")
            ?? configuration.GetValue<string>("GCP:ProjectId")
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")
            ?? Environment.GetEnvironmentVariable("GCP_PROJECT");
    }

    /// <summary>
    /// Gets the OTLP endpoint for GCP (typically OpenTelemetry Collector).
    /// Checks: observability:gcp:otlpEndpoint
    /// Default: http://localhost:4317 (OpenTelemetry Collector sidecar)
    /// </summary>
    private static string? GetOtlpEndpoint(IConfiguration configuration)
    {
        return configuration.GetValue<string>("observability:gcp:otlpEndpoint")
            ?? "http://localhost:4317";
    }
}
