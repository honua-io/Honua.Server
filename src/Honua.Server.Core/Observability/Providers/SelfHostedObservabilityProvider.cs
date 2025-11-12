// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Observability.Providers;

/// <summary>
/// Self-hosted / on-premises observability provider.
/// This is the default provider that relies on the base OpenTelemetry configuration
/// without any cloud-specific integrations.
/// </summary>
/// <remarks>
/// <para><strong>Supported Self-Hosted Solutions:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Metrics:</strong> Prometheus (via Prometheus exporter endpoint at <c>/metrics</c>)
/// <list type="bullet">
/// <item>Scrape endpoint: <c>http://honua-server:8080/metrics</c></item>
/// <item>Grafana dashboards for visualization</item>
/// <item>VictoriaMetrics as Prometheus alternative</item>
/// </list>
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Tracing:</strong> Jaeger, Grafana Tempo, or any OTLP-compatible backend
/// <list type="bullet">
/// <item>OTLP exporter configured via <c>observability:tracing:otlpEndpoint</c></item>
/// <item>Jaeger UI for trace visualization</item>
/// <item>Tempo for long-term trace storage</item>
/// </list>
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Logging:</strong> Grafana Loki, Elasticsearch/ELK, Seq, or file-based logging
/// <list type="bullet">
/// <item>Configured via Serilog sinks</item>
/// <item>Structured logging with JSON output</item>
/// <item>Centralized log aggregation</item>
/// </list>
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Configuration:</strong></para>
/// <para>
/// The self-hosted provider is automatically used when no cloud provider is configured
/// (<c>observability:cloudProvider = "none"</c> or not set).
/// </para>
///
/// <para><strong>Deployment Architectures:</strong></para>
/// <list type="number">
/// <item>
/// <description>
/// <strong>Docker Compose:</strong> Run Prometheus, Jaeger, and Grafana alongside Honua Server
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Kubernetes:</strong> Deploy Prometheus Operator, Jaeger Operator, and Grafana
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Single Server:</strong> All observability tools on one machine with Honua Server
/// </description>
/// </item>
/// </list>
///
/// <para><strong>No Additional Packages Required:</strong></para>
/// <para>
/// The self-hosted provider uses only the base OpenTelemetry packages already included:
/// <list type="bullet">
/// <item><c>OpenTelemetry.Exporter.Prometheus.AspNetCore</c></item>
/// <item><c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c> (for Jaeger/Tempo)</item>
/// <item><c>OpenTelemetry.Exporter.Console</c> (for debugging)</item>
/// </list>
/// </para>
///
/// <para><strong>Benefits:</strong></para>
/// <list type="bullet">
/// <item><description>No cloud vendor lock-in</description></item>
/// <item><description>Full data ownership and privacy</description></item>
/// <item><description>Lower costs for high-volume telemetry</description></item>
/// <item><description>On-premises compliance requirements</description></item>
/// <item><description>Works in air-gapped environments</description></item>
/// </list>
/// </remarks>
public sealed class SelfHostedObservabilityProvider : ICloudObservabilityProvider
{
    /// <inheritdoc />
    public string ProviderName => "none";

    /// <inheritdoc />
    public void ConfigureMetrics(IServiceCollection services, IConfiguration configuration)
    {
        // No-op: Metrics are configured in the base OpenTelemetry setup
        // Prometheus exporter is added by default in ObservabilityExtensions
    }

    /// <inheritdoc />
    public void ConfigureTracing(IServiceCollection services, IConfiguration configuration)
    {
        // No-op: Tracing is configured in the base OpenTelemetry setup
        // OTLP/console exporters are added based on observability:tracing:exporter setting
    }

    /// <inheritdoc />
    public void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration)
    {
        // No-op: Logging is configured via Serilog in Program.cs
        // Self-hosted sinks (Seq, Loki, file) are configured in Serilog configuration
    }

    /// <inheritdoc />
    public bool IsEnabled(IConfiguration configuration)
    {
        // Always enabled as the default/fallback provider
        return true;
    }
}
