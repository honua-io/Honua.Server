// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Amazon.Batch;
using Amazon.CloudWatchLogs;
using Amazon.Runtime;
using Amazon.S3;
using Honua.Server.Enterprise.Geoprocessing.Executors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Extension methods for registering geoprocessing services.
/// </summary>
public static class GeoprocessingServiceCollectionExtensions
{
    /// <summary>
    /// Adds geoprocessing services including the worker service and alert bridge.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGeoprocessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register control plane (job queue management)
        services.AddSingleton<IControlPlane, PostgresControlPlane>();

        // Register metrics service
        services.AddSingleton<IGeoprocessingMetrics, GeoprocessingMetrics>();

        // Register alert bridge service
        var alertReceiverBaseUrl = configuration["AlertReceiver:BaseUrl"] ?? "http://localhost:5555";
        services.AddSingleton<IGeoprocessingToAlertBridgeService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GeoprocessingToAlertBridgeService>>();
            return new GeoprocessingToAlertBridgeService(httpClientFactory, logger, alertReceiverBaseUrl);
        });

        // Configure HTTP client for alert receiver
        services.AddHttpClient("AlertReceiver", client =>
        {
            client.BaseAddress = new Uri(alertReceiverBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register worker service as a hosted background service
        services.AddHostedService<GeoprocessingWorkerService>();

        return services;
    }

    /// <summary>
    /// Adds geoprocessing services with custom alert receiver URL.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="alertReceiverBaseUrl">The base URL for the alert receiver service.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGeoprocessing(
        this IServiceCollection services,
        string alertReceiverBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(alertReceiverBaseUrl);

        // Register control plane (job queue management)
        services.AddSingleton<IControlPlane, PostgresControlPlane>();

        // Register metrics service
        services.AddSingleton<IGeoprocessingMetrics, GeoprocessingMetrics>();

        // Register alert bridge service
        services.AddSingleton<IGeoprocessingToAlertBridgeService>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GeoprocessingToAlertBridgeService>>();
            return new GeoprocessingToAlertBridgeService(httpClientFactory, logger, alertReceiverBaseUrl);
        });

        // Configure HTTP client for alert receiver
        services.AddHttpClient("AlertReceiver", client =>
        {
            client.BaseAddress = new Uri(alertReceiverBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register worker service as a hosted background service
        services.AddHostedService<GeoprocessingWorkerService>();

        return services;
    }

    /// <summary>
    /// Adds geoprocessing services without alerting (for testing or minimal deployments).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGeoprocessingWithoutAlerting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register control plane (job queue management)
        services.AddSingleton<IControlPlane, PostgresControlPlane>();

        // Register metrics service (still enabled for observability)
        services.AddSingleton<IGeoprocessingMetrics, GeoprocessingMetrics>();

        // Register worker service as a hosted background service (without alert bridge)
        services.AddHostedService<GeoprocessingWorkerService>();

        return services;
    }

    /// <summary>
    /// Adds AWS Batch executor for large-scale geoprocessing operations.
    /// Registers AWS SDK clients and configures the executor with options from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAwsBatchExecutor(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind configuration options
        services.Configure<AwsBatchExecutorOptions>(
            configuration.GetSection("Geoprocessing:AwsBatch"));

        // Register AWS SDK clients
        // These use the default AWS SDK credential chain:
        // 1. Environment variables (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
        // 2. IAM role (EC2/ECS instance profile) - RECOMMENDED for production
        // 3. Shared credentials file (~/.aws/credentials)

        services.AddSingleton<IAmazonBatch>(sp =>
        {
            var region = configuration["Geoprocessing:AwsBatch:Region"] ?? configuration["AWS:Region"];
            var config = new Amazon.Batch.AmazonBatchConfig();

            if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }

            return new AmazonBatchClient(config);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var region = configuration["Geoprocessing:AwsBatch:Region"] ?? configuration["AWS:Region"];
            var config = new Amazon.S3.AmazonS3Config();

            if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }

            return new AmazonS3Client(config);
        });

        services.AddSingleton<IAmazonCloudWatchLogs>(sp =>
        {
            var region = configuration["Geoprocessing:AwsBatch:Region"] ?? configuration["AWS:Region"];
            var config = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsConfig();

            if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }

            return new AmazonCloudWatchLogsClient(config);
        });

        // Register the AWS Batch executor
        services.AddSingleton<AwsBatchExecutor>();

        // Also register as ICloudBatchExecutor for dependency injection
        services.AddSingleton<ICloudBatchExecutor>(sp => sp.GetRequiredService<AwsBatchExecutor>());

        return services;
    }

    /// <summary>
    /// Adds AWS Batch executor with custom AWS credentials.
    /// Use this for development/testing with explicit credentials.
    /// In production, prefer using IAM roles via AddAwsBatchExecutor(services, configuration).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="credentials">AWS credentials.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAwsBatchExecutor(
        this IServiceCollection services,
        IConfiguration configuration,
        AWSCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(credentials);

        // Bind configuration options
        services.Configure<AwsBatchExecutorOptions>(
            configuration.GetSection("Geoprocessing:AwsBatch"));

        // Register AWS SDK clients with explicit credentials
        var region = configuration["Geoprocessing:AwsBatch:Region"] ?? configuration["AWS:Region"];

        services.AddSingleton<IAmazonBatch>(sp =>
        {
            var config = new Amazon.Batch.AmazonBatchConfig();
            if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }
            return new AmazonBatchClient(credentials, config);
        });

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var config = new Amazon.S3.AmazonS3Config();
            if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }
            return new AmazonS3Client(credentials, config);
        });

        services.AddSingleton<IAmazonCloudWatchLogs>(sp =>
        {
            var config = new Amazon.CloudWatchLogs.AmazonCloudWatchLogsConfig();
            if (!string.IsNullOrWhiteSpace(region))
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
            }
            return new AmazonCloudWatchLogsClient(credentials, config);
        });

        // Register the AWS Batch executor
        services.AddSingleton<AwsBatchExecutor>();

        // Also register as ICloudBatchExecutor for dependency injection
        services.AddSingleton<ICloudBatchExecutor>(sp => sp.GetRequiredService<AwsBatchExecutor>());

        return services;
    }
}
