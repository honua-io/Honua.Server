// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Observability.HealthChecks;
using Honua.Server.Observability.Metrics;
using Honua.Server.Observability.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Honua.Server.Observability;

/// <summary>
/// Extension methods for configuring Honua observability services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds comprehensive observability services including metrics, logging, tracing, and health checks.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="serviceName">Service name for telemetry</param>
    /// <param name="serviceVersion">Service version for telemetry</param>
    /// <param name="connectionString">Database connection string for health checks</param>
    /// <param name="configureTracing">Optional action to configure tracing</param>
    /// <param name="configuration">Optional configuration for advanced features</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHonuaObservability(
        this IServiceCollection services,
        string serviceName = "Honua.Server",
        string serviceVersion = "1.0.0",
        string? connectionString = null,
        Action<TracerProviderBuilder>? configureTracing = null,
        IConfiguration? configuration = null)
    {
        // Add OpenTelemetry with comprehensive instrumentation
        services.AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureResourceAttributes(resource, serviceName, serviceVersion, configuration))
            .WithMetrics(builder =>
            {
                builder
                    // Add custom meters
                    .AddMeter("Honua.BuildQueue")
                    .AddMeter("Honua.Cache")
                    .AddMeter("Honua.License")
                    .AddMeter("Honua.Registry")
                    .AddMeter("Honua.Intake")
                    .AddMeter("Honua.Http")

                    // Add runtime instrumentation
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddProcessInstrumentation()

                    // Add Prometheus exporter
                    .AddPrometheusExporter(options =>
                    {
                        options.ScrapeEndpointPath = "/metrics";
                        options.ScrapeResponseCacheDurationMilliseconds = 0;
                    });

                // Configure histogram buckets following OpenTelemetry best practices
                builder.AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000 }
                });

                builder.AddView("http.client.request.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new[] { 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000 }
                });
            })
            .WithTracing(builder =>
            {
                // Configure comprehensive distributed tracing
                builder
                    // ASP.NET Core instrumentation - trace incoming HTTP requests
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Don't trace health check endpoints (too noisy)
                            var path = httpContext.Request.Path.Value ?? string.Empty;
                            return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) &&
                                   !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase);
                        };
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.header.user-agent", request.Headers.UserAgent.ToString());
                            activity.SetTag("http.request.header.accept", request.Headers.Accept.ToString());
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.content_length", response.ContentLength);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("exception.escaped", true);
                        };
                    })

                    // HTTP Client instrumentation - trace outgoing HTTP requests
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.FilterHttpRequestMessage = request =>
                        {
                            // Don't trace internal health checks or metrics
                            var uri = request.RequestUri?.ToString() ?? string.Empty;
                            return !uri.Contains("/health") && !uri.Contains("/metrics");
                        };
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            activity.SetTag("http.client.method", request.Method.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            activity.SetTag("http.client.status_code", (int)response.StatusCode);
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            activity.SetTag("exception.http_client", true);
                        };
                    })

                    // SQL Client instrumentation - trace database operations
                    .AddSqlClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.SetDbStatementForText = true;
                        // Note: Enrich callback signature changed in newer versions
                        // We'll rely on automatic instrumentation for now
                    })

                    // StackExchange.Redis instrumentation - trace cache operations
                    .AddRedisInstrumentation(options =>
                    {
                        options.SetVerboseDatabaseStatements = false; // Don't log commands (security)
                        options.Enrich = (activity, command) =>
                        {
                            activity.SetTag("db.system", "redis");
                        };
                    })

                    // Add all Honua activity sources
                    .AddSource("Honua.Server.OgcProtocols")
                    .AddSource("Honua.Server.OData")
                    .AddSource("Honua.Server.Stac")
                    .AddSource("Honua.Server.Database")
                    .AddSource("Honua.Server.RasterTiles")
                    .AddSource("Honua.Server.Metadata")
                    .AddSource("Honua.Server.Authentication")
                    .AddSource("Honua.Server.Export")
                    .AddSource("Honua.Server.Import")
                    .AddSource("Honua.Server.Notifications")

                    // Configure sampling - default to always on for development
                    .SetSampler(new AlwaysOnSampler());

                // Allow custom tracing configuration
                configureTracing?.Invoke(builder);
            });

        // Register metric services
        services.AddSingleton<BuildQueueMetrics>();
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<LicenseMetrics>();
        services.AddSingleton<RegistryMetrics>();
        services.AddSingleton<IntakeMetrics>();

        // Add health checks
        if (!string.IsNullOrEmpty(connectionString))
        {
            services.AddHealthChecks()
                .AddCheck("database",
                    new DatabaseHealthCheck(connectionString),
                    HealthStatus.Unhealthy,
                    tags: new[] { "database", "postgres", })
                .AddCheck("license",
                    new LicenseHealthCheck(connectionString),
                    HealthStatus.Degraded,
                    tags: new[] { "license", })
                .AddCheck("queue",
                    new QueueHealthCheck(connectionString),
                    HealthStatus.Degraded,
                    tags: new[] { "queue", "build", })
                .AddCheck("registry",
                    new RegistryHealthCheck(connectionString),
                    HealthStatus.Degraded,
                    tags: new[] { "registry", "docker", });
        }
        else
        {
            services.AddHealthChecks();
        }

        return services;
    }

    /// <summary>
    /// Configures Serilog structured logging for Honua services.
    /// </summary>
    /// <param name="loggingBuilder">The logging builder.</param>
    /// <param name="serviceName">The service name.</param>
    /// <param name="minimumLevel">The minimum log level.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddHonuaSerilog(
        this ILoggingBuilder loggingBuilder,
        string serviceName = "Honua.Server",
        LogEventLevel minimumLevel = LogEventLevel.Information)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .WriteTo.Console(new CompactJsonFormatter())
            .WriteTo.File(
                new CompactJsonFormatter(),
                path: $"logs/{serviceName}-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();

        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog(dispose: true);

        return loggingBuilder;
    }

    /// <summary>
    /// Adds the correlation ID and metrics middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseHonuaMetrics(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<MetricsMiddleware>();
        return app;
    }

    /// <summary>
    /// Maps health check endpoints with RFC-compliant response format.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseHonuaHealthChecks(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            AllowCachingResponses = false
        });

        app.UseHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false, // Liveness - always healthy if app is running
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            AllowCachingResponses = false
        });

        app.UseHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("database") || check.Tags.Contains("queue"),
            ResponseWriter = HealthCheckResponseWriter.WriteResponse,
            AllowCachingResponses = false
        });

        return app;
    }

    /// <summary>
    /// Maps Prometheus metrics endpoint.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder app)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        return app;
    }

    /// <summary>
    /// Adds OpenTelemetry logging with OTLP exporter support.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="serviceName">Service name for telemetry.</param>
    /// <param name="serviceVersion">Service version for telemetry.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddOpenTelemetryLogging(
        this ILoggingBuilder builder,
        IConfiguration configuration,
        string serviceName = "Honua.Server",
        string serviceVersion = "1.0.0")
    {
        builder.AddOpenTelemetry(options =>
        {
            // Configure resource attributes
            options.SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                    .AddAttributes(GetResourceAttributes(serviceName, serviceVersion, configuration)));

            // Include formatted message
            options.IncludeFormattedMessage = true;

            // Include scopes for better context
            options.IncludeScopes = true;

            // Parse state values for structured logging
            options.ParseStateValues = true;

            // Configure OTLP exporter if enabled
            var otlpEndpoint = configuration?["observability:logging:otlpEndpoint"];
            var exporterType = configuration?["observability:logging:exporter"]?.ToLowerInvariant();

            switch (exporterType)
            {
                case "otlp":
                    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    {
                        options.AddOtlpExporter(otlpOptions =>
                        {
                            otlpOptions.Endpoint = new Uri(otlpEndpoint);

                            var headers = configuration?["observability:logging:otlpHeaders"];
                            if (!string.IsNullOrWhiteSpace(headers))
                            {
                                otlpOptions.Headers = headers;
                            }
                        });
                    }
                    break;

                case "console":
                    options.AddConsoleExporter();
                    break;
            }
        });

        return builder;
    }

    #region Private Helper Methods

    private static ResourceBuilder ConfigureResourceAttributes(
        ResourceBuilder builder,
        string serviceName,
        string serviceVersion,
        IConfiguration? configuration)
    {
        builder.AddService(serviceName: serviceName, serviceVersion: serviceVersion);
        builder.AddAttributes(GetResourceAttributes(serviceName, serviceVersion, configuration));
        return builder;
    }

    private static IEnumerable<KeyValuePair<string, object>> GetResourceAttributes(
        string serviceName,
        string serviceVersion,
        IConfiguration? configuration)
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            // Required semantic conventions
            new("service.namespace", "Honua"),
            new("service.instance.id", GetServiceInstanceId()),

            // Deployment environment
            new("deployment.environment", GetEnvironment(configuration)),

            // Host semantic conventions
            new("host.name", Environment.MachineName),
            new("host.id", GetHostId()),

            // Process semantic conventions
            new("process.pid", Environment.ProcessId),
            new("process.runtime.name", ".NET"),
            new("process.runtime.version", Environment.Version.ToString()),
            new("process.runtime.description", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription),

            // OS semantic conventions
            new("os.type", GetOSType()),
            new("os.description", System.Runtime.InteropServices.RuntimeInformation.OSDescription),

            // Telemetry SDK
            new("telemetry.sdk.name", "opentelemetry"),
            new("telemetry.sdk.language", "dotnet"),
        };

        // Add cloud-specific attributes
        AddCloudAttributes(attributes, configuration);

        // Add Kubernetes attributes if available
        AddKubernetesAttributes(attributes);

        // Add container attributes if available
        AddContainerAttributes(attributes);

        return attributes;
    }

    private static string GetServiceInstanceId()
    {
        return Environment.GetEnvironmentVariable("SERVICE_INSTANCE_ID")
               ?? Environment.GetEnvironmentVariable("HOSTNAME")
               ?? Environment.GetEnvironmentVariable("COMPUTERNAME")
               ?? $"{Environment.MachineName}-{Environment.ProcessId}";
    }

    private static string GetEnvironment(IConfiguration? configuration)
    {
        return configuration?["ASPNETCORE_ENVIRONMENT"]
               ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
               ?? "Production";
    }

    private static string GetHostId()
    {
        return Environment.GetEnvironmentVariable("HOST_ID") ?? Environment.MachineName;
    }

    private static string GetOSType()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            return "windows";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            return "linux";
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            return "darwin";
        return "unknown";
    }

    private static void AddCloudAttributes(List<KeyValuePair<string, object>> attributes, IConfiguration? configuration)
    {
        var cloudProvider = configuration?["observability:cloudProvider"]?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cloudProvider) || cloudProvider == "none")
            return;

        attributes.Add(new("cloud.provider", cloudProvider));

        switch (cloudProvider)
        {
            case "aws":
                AddAwsAttributes(attributes);
                break;
            case "azure":
                AddAzureAttributes(attributes);
                break;
            case "gcp":
                AddGcpAttributes(attributes);
                break;
        }
    }

    private static void AddAwsAttributes(List<KeyValuePair<string, object>> attributes)
    {
        var region = Environment.GetEnvironmentVariable("AWS_REGION") ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
        if (!string.IsNullOrWhiteSpace(region))
            attributes.Add(new("cloud.region", region));

        var accountId = Environment.GetEnvironmentVariable("AWS_ACCOUNT_ID");
        if (!string.IsNullOrWhiteSpace(accountId))
            attributes.Add(new("cloud.account.id", accountId));

        var ecsMetadata = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");
        if (!string.IsNullOrWhiteSpace(ecsMetadata))
        {
            attributes.Add(new("cloud.platform", "aws_ecs"));
            var taskArn = Environment.GetEnvironmentVariable("ECS_TASK_ARN");
            if (!string.IsNullOrWhiteSpace(taskArn))
                attributes.Add(new("cloud.resource_id", taskArn));
        }
    }

    private static void AddAzureAttributes(List<KeyValuePair<string, object>> attributes)
    {
        var region = Environment.GetEnvironmentVariable("AZURE_REGION") ?? Environment.GetEnvironmentVariable("REGION_NAME");
        if (!string.IsNullOrWhiteSpace(region))
            attributes.Add(new("cloud.region", region));

        var websiteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
        if (!string.IsNullOrWhiteSpace(websiteName))
        {
            attributes.Add(new("cloud.platform", "azure_app_service"));
            var instanceId = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
            if (!string.IsNullOrWhiteSpace(instanceId))
                attributes.Add(new("service.instance.id", instanceId));
        }
    }

    private static void AddGcpAttributes(List<KeyValuePair<string, object>> attributes)
    {
        var region = Environment.GetEnvironmentVariable("GCP_REGION") ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_REGION");
        if (!string.IsNullOrWhiteSpace(region))
            attributes.Add(new("cloud.region", region));

        var projectId = Environment.GetEnvironmentVariable("GCP_PROJECT") ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
        if (!string.IsNullOrWhiteSpace(projectId))
            attributes.Add(new("cloud.account.id", projectId));

        var serviceName = Environment.GetEnvironmentVariable("K_SERVICE");
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            attributes.Add(new("cloud.platform", "gcp_cloud_run"));
            var revision = Environment.GetEnvironmentVariable("K_REVISION");
            if (!string.IsNullOrWhiteSpace(revision))
                attributes.Add(new("service.instance.id", revision));
        }
    }

    private static void AddKubernetesAttributes(List<KeyValuePair<string, object>> attributes)
    {
        var podName = Environment.GetEnvironmentVariable("KUBERNETES_POD_NAME");
        var namespace_ = Environment.GetEnvironmentVariable("KUBERNETES_NAMESPACE");

        if (!string.IsNullOrWhiteSpace(namespace_))
            attributes.Add(new("k8s.namespace.name", namespace_));

        if (!string.IsNullOrWhiteSpace(podName))
            attributes.Add(new("k8s.pod.name", podName));

        var clusterName = Environment.GetEnvironmentVariable("KUBERNETES_CLUSTER_NAME");
        if (!string.IsNullOrWhiteSpace(clusterName))
            attributes.Add(new("k8s.cluster.name", clusterName));
    }

    private static void AddContainerAttributes(List<KeyValuePair<string, object>> attributes)
    {
        if (File.Exists("/.dockerenv"))
        {
            var containerId = Environment.GetEnvironmentVariable("HOSTNAME");
            if (!string.IsNullOrWhiteSpace(containerId))
                attributes.Add(new("container.id", containerId));
        }

        var containerName = Environment.GetEnvironmentVariable("CONTAINER_NAME");
        if (!string.IsNullOrWhiteSpace(containerName))
            attributes.Add(new("container.name", containerName));
    }

    #endregion
}
