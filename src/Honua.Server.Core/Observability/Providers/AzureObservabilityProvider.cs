// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Honua.Server.Core.Observability.Providers;

/// <summary>
/// Azure Monitor / Application Insights observability provider.
/// Exports metrics and traces to Azure Application Insights using OpenTelemetry exporters.
/// </summary>
/// <remarks>
/// <para><strong>Configuration Requirements:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Connection String:</strong> Set <c>observability:azure:connectionString</c> or
/// <c>ApplicationInsights:ConnectionString</c> in configuration.
/// Format: <c>InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/</c>
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Environment Variable:</strong> Can also be set via <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Features:</strong></para>
/// <list type="bullet">
/// <item><description>Metrics export to Azure Monitor with automatic aggregation</description></item>
/// <item><description>Distributed tracing with Azure Application Insights</description></item>
/// <item><description>Automatic correlation of metrics and traces using operation IDs</description></item>
/// <item><description>Integration with Azure Application Map for service topology visualization</description></item>
/// </list>
///
/// <para><strong>Logging:</strong></para>
/// <para>
/// Azure logging is handled separately via Serilog.Sinks.ApplicationInsights.
/// Configure in Serilog configuration section with the same connection string.
/// </para>
///
/// <para><strong>Package Requirements:</strong></para>
/// <list type="bullet">
/// <item><description><c>Azure.Monitor.OpenTelemetry.Exporter</c> - Version 1.3.0 or higher</description></item>
/// </list>
/// </remarks>
public sealed class AzureObservabilityProvider : ICloudObservabilityProvider
{
    /// <inheritdoc />
    public string ProviderName => "azure";

    /// <inheritdoc />
    public void ConfigureMetrics(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);
        if (connectionString.IsNullOrEmpty())
        {
            return;
        }

        services.AddOpenTelemetry().WithMetrics(metricBuilder =>
        {
            metricBuilder.AddAzureMonitorMetricExporter(options =>
            {
                options.ConnectionString = connectionString;
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureTracing(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);
        if (connectionString.IsNullOrEmpty())
        {
            return;
        }

        services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            tracingBuilder.AddAzureMonitorTraceExporter(options =>
            {
                options.ConnectionString = connectionString;
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration)
    {
        // Azure logging is handled via Serilog.Sinks.ApplicationInsights
        // No additional configuration needed here
    }

    /// <inheritdoc />
    public bool IsEnabled(IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);
        return !connectionString.IsNullOrEmpty();
    }

    /// <summary>
    /// Gets the Azure Application Insights connection string from configuration.
    /// Checks multiple configuration sources in order:
    /// 1. observability:azure:connectionString
    /// 2. ApplicationInsights:ConnectionString
    /// 3. Environment variable APPLICATIONINSIGHTS_CONNECTION_STRING
    /// </summary>
    private static string? GetConnectionString(IConfiguration configuration)
    {
        return configuration.GetValue<string>("observability:azure:connectionString")
            ?? configuration.GetValue<string>("ApplicationInsights:ConnectionString")
            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
    }
}
