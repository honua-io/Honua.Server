// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Contrib.Extensions.AWSXRay.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Honua.Server.Core.Observability.Providers;

/// <summary>
/// AWS CloudWatch and X-Ray observability provider.
/// Exports metrics to CloudWatch and traces to X-Ray using OpenTelemetry exporters.
/// </summary>
/// <remarks>
/// <para><strong>Configuration Requirements:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>AWS Region:</strong> Set <c>observability:aws:region</c> in configuration.
/// Example: <c>us-east-1</c>, <c>eu-west-1</c>
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>AWS Credentials:</strong> Loaded from AWS credential provider chain:
/// <list type="number">
/// <item>Environment variables: <c>AWS_ACCESS_KEY_ID</c>, <c>AWS_SECRET_ACCESS_KEY</c></item>
/// <item>Shared credentials file: <c>~/.aws/credentials</c></item>
/// <item>IAM role for EC2, ECS, or EKS</item>
/// <item>IAM role for AWS Lambda</item>
/// </list>
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Features:</strong></para>
/// <list type="bullet">
/// <item><description>Metrics export to CloudWatch with automatic namespace organization</description></item>
/// <item><description>Distributed tracing with AWS X-Ray</description></item>
/// <item><description>Integration with AWS Service Map for service topology visualization</description></item>
/// <item><description>Support for X-Ray trace segments and subsegments</description></item>
/// <item><description>Automatic AWS resource tagging (EC2, ECS, EKS)</description></item>
/// </list>
///
/// <para><strong>OTLP Collector Requirement:</strong></para>
/// <para>
/// This provider uses the OTLP exporter configured to send data to an AWS Distro for OpenTelemetry (ADOT) Collector,
/// which then forwards to CloudWatch and X-Ray. Deploy the ADOT Collector as a sidecar or DaemonSet.
/// </para>
/// <para>
/// <strong>Alternative:</strong> For direct export without a collector, use the AWS-specific OpenTelemetry exporters:
/// <list type="bullet">
/// <item><c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c> configured for AWS endpoints</item>
/// <item><c>OpenTelemetry.Contrib.Extensions.AWSXRay</c> for X-Ray ID generation</item>
/// </list>
/// </para>
///
/// <para><strong>Logging:</strong></para>
/// <para>
/// AWS CloudWatch Logs is handled separately via Serilog.Sinks.AwsCloudWatch.
/// Configure in Serilog configuration section.
/// </para>
///
/// <para><strong>Package Requirements:</strong></para>
/// <list type="bullet">
/// <item><description><c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c> - Version 1.12.0 or higher</description></item>
/// <item><description><c>OpenTelemetry.Contrib.Extensions.AWSXRay</c> - Version 1.4.0 or higher (optional, for X-Ray trace IDs)</description></item>
/// <item><description><c>AWSSDK.CloudWatch</c> - Version 3.7.0 or higher (optional, for direct CloudWatch export)</description></item>
/// </list>
/// </remarks>
public sealed class AwsObservabilityProvider : ICloudObservabilityProvider
{
    /// <inheritdoc />
    public string ProviderName => "aws";

    /// <inheritdoc />
    public void ConfigureMetrics(IServiceCollection services, IConfiguration configuration)
    {
        var region = GetAwsRegion(configuration);
        if (region.IsNullOrEmpty())
        {
            return;
        }

        var otlpEndpoint = GetOtlpEndpoint(configuration);

        services.AddOpenTelemetry().WithMetrics(metricBuilder =>
        {
            // Add AWS resource detection
            metricBuilder.ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: configuration.GetValue<string>("ServiceName") ?? "honua-server",
                    serviceVersion: configuration.GetValue<string>("ServiceVersion") ?? "1.0.0");

                // AWS resource detectors will automatically detect EC2, ECS, EKS metadata
                // Note: Requires OpenTelemetry.ResourceDetectors.AWS package
                // resource.AddDetector(new OpenTelemetry.ResourceDetectors.AWS.AWSResourceDetector());
            });

            // Export to OTLP (ADOT Collector forwards to CloudWatch)
            metricBuilder.AddOtlpExporter(options =>
            {
                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
                // Default OTLP endpoint is localhost:4317 (ADOT Collector)
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureTracing(IServiceCollection services, IConfiguration configuration)
    {
        var region = GetAwsRegion(configuration);
        if (region.IsNullOrEmpty())
        {
            return;
        }

        var otlpEndpoint = GetOtlpEndpoint(configuration);

        services.AddOpenTelemetry().WithTracing(tracingBuilder =>
        {
            // Add AWS resource detection
            tracingBuilder.ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: configuration.GetValue<string>("ServiceName") ?? "honua-server",
                    serviceVersion: configuration.GetValue<string>("ServiceVersion") ?? "1.0.0");

                // AWS resource detectors will automatically detect EC2, ECS, EKS metadata
                // Note: Requires OpenTelemetry.ResourceDetectors.AWS package
                // resource.AddDetector(new OpenTelemetry.ResourceDetectors.AWS.AWSResourceDetector());
            });

            // Add X-Ray ID generator for compatibility with AWS X-Ray
            tracingBuilder.AddXRayTraceId();

            // Export to OTLP (ADOT Collector forwards to X-Ray)
            tracingBuilder.AddOtlpExporter(options =>
            {
                if (!otlpEndpoint.IsNullOrEmpty())
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                }
                // Default OTLP endpoint is localhost:4317 (ADOT Collector)
            });
        });
    }

    /// <inheritdoc />
    public void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration)
    {
        // AWS CloudWatch Logs is handled via Serilog.Sinks.AwsCloudWatch
        // No additional configuration needed here
    }

    /// <inheritdoc />
    public bool IsEnabled(IConfiguration configuration)
    {
        var region = GetAwsRegion(configuration);
        return !region.IsNullOrEmpty();
    }

    /// <summary>
    /// Gets the AWS region from configuration or environment.
    /// Checks: observability:aws:region, AWS:Region, AWS_REGION, AWS_DEFAULT_REGION
    /// </summary>
    private static string? GetAwsRegion(IConfiguration configuration)
    {
        return configuration.GetValue<string>("observability:aws:region")
            ?? configuration.GetValue<string>("AWS:Region")
            ?? Environment.GetEnvironmentVariable("AWS_REGION")
            ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
    }

    /// <summary>
    /// Gets the OTLP endpoint for AWS (typically ADOT Collector).
    /// Checks: observability:aws:otlpEndpoint
    /// Default: http://localhost:4317 (ADOT Collector sidecar)
    /// </summary>
    private static string? GetOtlpEndpoint(IConfiguration configuration)
    {
        return configuration.GetValue<string>("observability:aws:otlpEndpoint")
            ?? "http://localhost:4317";
    }
}
