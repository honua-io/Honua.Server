using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Cloud.Marketplace;

/// <summary>
/// Factory for creating marketplace metering services based on the cloud provider
/// </summary>
public class MarketplaceMeteringServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketplaceMeteringServiceFactory> _logger;
    private readonly MarketplaceOptions _options;

    public MarketplaceMeteringServiceFactory(
        IServiceProvider serviceProvider,
        ILogger<MarketplaceMeteringServiceFactory> logger,
        IOptions<MarketplaceOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Create the appropriate metering service based on configuration
    /// </summary>
    public IMarketplaceMeteringService CreateMeteringService()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Marketplace metering is disabled");
            return new NoOpMarketplaceMeteringService();
        }

        // Detect cloud provider
        var cloudProvider = DetectCloudProvider();

        _logger.LogInformation("Detected cloud provider: {CloudProvider}", cloudProvider);

        return cloudProvider switch
        {
            CloudProvider.AWS => _serviceProvider.GetRequiredService<AwsMarketplaceMeteringService>(),
            CloudProvider.Azure => _serviceProvider.GetRequiredService<AzureMarketplaceMeteringService>(),
            CloudProvider.GCP => _serviceProvider.GetRequiredService<GcpMarketplaceMeteringService>(),
            CloudProvider.Unknown => new NoOpMarketplaceMeteringService(),
            _ => throw new NotSupportedException($"Cloud provider {cloudProvider} is not supported")
        };
    }

    /// <summary>
    /// Detect the current cloud provider based on environment
    /// </summary>
    private CloudProvider DetectCloudProvider()
    {
        // Check explicit configuration first
        if (!string.IsNullOrEmpty(_options.AwsProductCode))
        {
            return CloudProvider.AWS;
        }

        if (!string.IsNullOrEmpty(_options.AzureOfferId))
        {
            return CloudProvider.Azure;
        }

        if (!string.IsNullOrEmpty(_options.GcpProductId))
        {
            return CloudProvider.GCP;
        }

        // Detect from environment
        // AWS: Check for ECS_CONTAINER_METADATA_URI or AWS_EXECUTION_ENV
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_EXECUTION_ENV")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_REGION")))
        {
            return CloudProvider.AWS;
        }

        // Azure: Check for WEBSITE_INSTANCE_ID or AZURE_CLIENT_ID
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_CLIENT_ID")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_RESOURCE_ID")))
        {
            return CloudProvider.Azure;
        }

        // GCP: Check for GOOGLE_CLOUD_PROJECT or K_SERVICE
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE")) ||
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCP_PROJECT")))
        {
            return CloudProvider.GCP;
        }

        _logger.LogWarning("Could not detect cloud provider from environment");
        return CloudProvider.Unknown;
    }
}

/// <summary>
/// Cloud provider enumeration
/// </summary>
public enum CloudProvider
{
    Unknown,
    AWS,
    Azure,
    GCP
}

/// <summary>
/// No-op implementation for when marketplace metering is disabled
/// </summary>
public class NoOpMarketplaceMeteringService : IMarketplaceMeteringService
{
    public Task<bool> ReportUsageAsync(
        string dimension,
        int quantity,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<bool> ReportUsageWithAllocationsAsync(
        string dimension,
        Dictionary<string, int> allocations,
        DateTime timestamp,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<int> BatchReportUsageAsync(
        IEnumerable<UsageRecord> records,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    public bool IsRegistered => false;
}

/// <summary>
/// Extension methods for registering marketplace metering services
/// </summary>
public static class MarketplaceMeteringServiceExtensions
{
    /// <summary>
    /// Add marketplace metering services to the service collection
    /// </summary>
    public static IServiceCollection AddMarketplaceMetering(
        this IServiceCollection services,
        Action<MarketplaceOptions>? configure = null)
    {
        // Register options
        var optionsBuilder = services.AddOptions<MarketplaceOptions>()
            .BindConfiguration(MarketplaceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        // Register HTTP clients
        services.AddHttpClient("AzureMarketplace");
        services.AddHttpClient("GcpMarketplace");

        // Register all metering service implementations
        services.AddSingleton<AwsMarketplaceMeteringService>();
        services.AddSingleton<AzureMarketplaceMeteringService>();
        services.AddSingleton<GcpMarketplaceMeteringService>();
        services.AddSingleton<MarketplaceMeteringServiceFactory>();

        // Register the factory-based service
        services.AddSingleton<IMarketplaceMeteringService>(sp =>
        {
            var factory = sp.GetRequiredService<MarketplaceMeteringServiceFactory>();
            return factory.CreateMeteringService();
        });

        // Register as hosted services for lifecycle management
        services.AddHostedService(sp =>
        {
            var meteringService = sp.GetRequiredService<IMarketplaceMeteringService>();
            return meteringService as IHostedService ?? new NoOpHostedService();
        });

        return services;
    }

    private class NoOpHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
