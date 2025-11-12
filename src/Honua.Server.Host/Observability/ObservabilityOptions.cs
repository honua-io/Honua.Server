// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Host.Observability;

/// <summary>
/// Configuration options for application observability including logging, metrics, and distributed tracing.
/// </summary>
public sealed class ObservabilityOptions
{
    /// <summary>
    /// Gets or sets the cloud provider for observability (azure, aws, gcp, or none for self-hosted).
    /// Default is "none" which uses Prometheus/Jaeger/self-hosted solutions.
    /// </summary>
    public string? CloudProvider { get; init; }

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
    /// Gets or sets the Azure-specific observability configuration.
    /// </summary>
    public AzureObservabilityOptions Azure { get; init; } = new();

    /// <summary>
    /// Gets or sets the AWS-specific observability configuration.
    /// </summary>
    public AwsObservabilityOptions Aws { get; init; } = new();

    /// <summary>
    /// Gets or sets the GCP-specific observability configuration.
    /// </summary>
    public GcpObservabilityOptions Gcp { get; init; } = new();

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

        /// <summary>
        /// Gets or sets a value indicating whether to export metrics to Azure Monitor/Application Insights.
        /// When true, metrics are sent to Azure Application Insights. Requires ApplicationInsights:ConnectionString configuration.
        /// </summary>
        public bool UseAzureMonitor { get; init; }
    }

    public sealed class TracingOptions
    {
        /// <summary>
        /// Exporter type: "none", "console", "otlp", or "azuremonitor"
        /// </summary>
        public string Exporter { get; init; } = "none";

        /// <summary>
        /// OTLP endpoint URL (e.g., "http://localhost:4317" for Jaeger)
        /// </summary>
        public string? OtlpEndpoint { get; init; }

        /// <summary>
        /// Azure Application Insights connection string for distributed tracing.
        /// Format: InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/
        /// </summary>
        public string? AppInsightsConnectionString { get; init; }

        /// <summary>
        /// Sampling ratio for traces (0.0 to 1.0). Default is 1.0 (100% of traces).
        /// Use lower values (e.g., 0.1 for 10%) in high-traffic production environments.
        /// </summary>
        public double SamplingRatio { get; init; } = 1.0;
    }

    /// <summary>
    /// Azure-specific observability configuration for Application Insights / Azure Monitor.
    /// </summary>
    public sealed class AzureObservabilityOptions
    {
        /// <summary>
        /// Azure Application Insights connection string.
        /// Format: InstrumentationKey=xxx;IngestionEndpoint=https://xxx.in.applicationinsights.azure.com/
        /// Can also be set via APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
        /// </summary>
        public string? ConnectionString { get; init; }
    }

    /// <summary>
    /// AWS-specific observability configuration for CloudWatch and X-Ray.
    /// </summary>
    public sealed class AwsObservabilityOptions
    {
        /// <summary>
        /// AWS region (e.g., "us-east-1", "eu-west-1").
        /// Can also be set via AWS_REGION or AWS_DEFAULT_REGION environment variables.
        /// </summary>
        public string? Region { get; init; }

        /// <summary>
        /// OTLP endpoint for AWS Distro for OpenTelemetry (ADOT) Collector.
        /// Default: http://localhost:4317 (ADOT Collector sidecar/DaemonSet).
        /// </summary>
        public string? OtlpEndpoint { get; init; }

        /// <summary>
        /// AWS credentials are loaded from the standard AWS credential provider chain:
        /// 1. Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
        /// 2. Shared credentials file (~/.aws/credentials)
        /// 3. IAM role (EC2, ECS, EKS, Lambda)
        /// No explicit configuration needed here.
        /// </summary>
        /// <remarks>
        /// This is a documentation property only. Actual credentials are loaded automatically
        /// by the AWS SDK from environment variables, credential files, or IAM roles.
        /// </remarks>
        public string? _CredentialsNote => "Loaded from AWS credential provider chain (env vars, ~/.aws/credentials, IAM role)";
    }

    /// <summary>
    /// GCP-specific observability configuration for Cloud Monitoring and Cloud Trace.
    /// </summary>
    public sealed class GcpObservabilityOptions
    {
        /// <summary>
        /// GCP project ID (e.g., "my-project-123456").
        /// Can also be set via GOOGLE_CLOUD_PROJECT or GCP_PROJECT environment variables.
        /// </summary>
        public string? ProjectId { get; init; }

        /// <summary>
        /// OTLP endpoint for OpenTelemetry Collector that forwards to GCP.
        /// Default: http://localhost:4317 (OpenTelemetry Collector sidecar/DaemonSet).
        /// </summary>
        public string? OtlpEndpoint { get; init; }

        /// <summary>
        /// GCP credentials are loaded from the standard GCP credential provider chain:
        /// 1. GOOGLE_APPLICATION_CREDENTIALS environment variable (path to service account JSON)
        /// 2. GCE metadata service (automatic on Compute Engine, GKE, Cloud Run)
        /// 3. gcloud CLI credentials (~/.config/gcloud/application_default_credentials.json)
        /// No explicit configuration needed here.
        /// </summary>
        /// <remarks>
        /// This is a documentation property only. Actual credentials are loaded automatically
        /// by the Google Cloud SDK from environment variables, metadata service, or gcloud CLI.
        /// </remarks>
        public string? _CredentialsNote => "Loaded from GOOGLE_APPLICATION_CREDENTIALS, GCE metadata, or gcloud CLI";
    }
}
