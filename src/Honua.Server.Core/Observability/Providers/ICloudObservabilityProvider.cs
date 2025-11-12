// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Observability.Providers;

/// <summary>
/// Provider abstraction for cloud-specific observability integrations.
/// Supports Azure Monitor, AWS CloudWatch/X-Ray, and GCP Cloud Monitoring/Trace.
/// This pattern allows Honua Server to integrate with any cloud provider's observability stack
/// while maintaining a consistent configuration interface.
/// </summary>
/// <remarks>
/// The provider pattern decouples cloud-specific implementations from the core observability setup.
/// All providers use OpenTelemetry as the underlying instrumentation layer, ensuring consistency
/// across metrics, traces, and logs regardless of the cloud platform.
/// </remarks>
public interface ICloudObservabilityProvider
{
    /// <summary>
    /// Provider name (e.g., "azure", "aws", "gcp", "none").
    /// Used for logging and configuration selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Configure metrics export to the cloud provider.
    /// This method is called during application startup to add provider-specific metric exporters
    /// to the OpenTelemetry metrics pipeline.
    /// </summary>
    /// <param name="services">The service collection for dependency injection.</param>
    /// <param name="configuration">Application configuration containing provider-specific settings.</param>
    /// <remarks>
    /// Implementations should:
    /// - Add OpenTelemetry metric exporters for their cloud platform
    /// - Configure export intervals, batching, and other platform-specific options
    /// - Handle missing or invalid configuration gracefully
    /// - Log configuration errors or warnings
    /// </remarks>
    void ConfigureMetrics(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Configure distributed tracing to the cloud provider.
    /// This method is called during application startup to add provider-specific trace exporters
    /// to the OpenTelemetry tracing pipeline.
    /// </summary>
    /// <param name="services">The service collection for dependency injection.</param>
    /// <param name="configuration">Application configuration containing provider-specific settings.</param>
    /// <remarks>
    /// Implementations should:
    /// - Add OpenTelemetry trace exporters for their cloud platform
    /// - Configure sampling, batching, and other platform-specific options
    /// - Support distributed trace context propagation (W3C Trace Context standard)
    /// - Handle missing or invalid configuration gracefully
    /// - Log configuration errors or warnings
    /// </remarks>
    void ConfigureTracing(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Configure logging to the cloud provider (optional, most use Serilog sinks).
    /// This method is called during application startup to add provider-specific logging configuration.
    /// </summary>
    /// <param name="loggingBuilder">The logging builder for configuring logging providers.</param>
    /// <param name="configuration">Application configuration containing provider-specific settings.</param>
    /// <remarks>
    /// Most cloud providers handle logging through Serilog sinks rather than OpenTelemetry.
    /// This method is provided for completeness but typically returns without action.
    /// Cloud logging sinks are configured separately in Serilog configuration:
    /// - Azure: Serilog.Sinks.ApplicationInsights
    /// - AWS: Serilog.Sinks.AwsCloudWatch
    /// - GCP: Google.Cloud.Logging.V2
    /// </remarks>
    void ConfigureLogging(ILoggingBuilder loggingBuilder, IConfiguration configuration);

    /// <summary>
    /// Whether this provider is configured and enabled.
    /// Used to determine if the provider should be activated during startup.
    /// </summary>
    /// <param name="configuration">Application configuration to check for provider-specific settings.</param>
    /// <returns>True if the provider has valid configuration and should be enabled; otherwise, false.</returns>
    /// <remarks>
    /// Implementations should check for:
    /// - Required configuration values (connection strings, endpoints, credentials)
    /// - Feature flags or explicit enable/disable settings
    /// - Valid credential paths or environment variables
    ///
    /// This method should not throw exceptions. If configuration is invalid,
    /// it should return false and optionally log a warning.
    /// </remarks>
    bool IsEnabled(IConfiguration configuration);
}
