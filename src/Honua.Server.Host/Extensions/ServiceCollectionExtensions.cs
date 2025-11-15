// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Configuration;
using Honua.Server.Core.DependencyInjection;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Host.Carto;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Filters;
using Honua.Server.Host.GeoservicesREST.Services;
using Honua.Server.Host.Hosting;
using Honua.Server.Host.Middleware;
#if ENABLE_ODATA
using Honua.Server.Host.OData;
#endif
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Stac;
using Honua.Server.Host.Wfs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for registering application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core Honua services including data providers, metadata, and caching.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="contentRootPath">The content root path for the application.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaCoreServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.AddHonuaCore(configuration, contentRootPath);

        // Register exception handlers (.NET 8+ IExceptionHandler interface)
        // Handlers execute in order; first one to return true wins
        services.AddExceptionHandler<ExceptionHandlers.GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Register security headers options with validation
        services.AddOptions<SecurityHeadersOptions>()
            .Bind(configuration.GetSection(SecurityHeadersOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register OGC cache header service with validation
        services.AddOptions<CacheHeaderOptions>()
            .Bind(configuration.GetSection("CacheHeaders"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<OgcCacheHeaderService>();
        services.AddSingleton<OgcApiDefinitionCache>();

        // Register OGC service layer components (extracted from OgcSharedHandlers)
        services.AddSingleton<Ogc.Services.OgcCrsService>();
        services.AddSingleton<Ogc.Services.OgcLinkBuilder>();
        services.AddSingleton<Ogc.Services.OgcParameterParser>();

        // Register additional OGC service layer components (Phase 1-4 refactoring)
        services.AddSingleton<Ogc.Services.IOgcCollectionResolver, Ogc.Services.OgcCollectionResolver>();
        services.AddSingleton<Ogc.Services.IOgcFeaturesGeoJsonHandler, Ogc.Services.OgcFeaturesGeoJsonHandler>();
        services.AddSingleton<Ogc.Services.IOgcFeaturesQueryHandler, Ogc.Services.OgcFeaturesQueryHandler>();
        services.AddSingleton<Ogc.Services.IOgcFeaturesEditingHandler, Ogc.Services.OgcFeaturesEditingHandler>();
        services.AddSingleton<Ogc.Services.IOgcFeaturesAttachmentHandler, Ogc.Services.OgcFeaturesAttachmentHandler>();
        services.AddSingleton<Ogc.Services.IOgcTilesHandler, Ogc.Services.OgcTilesHandler>();
        services.AddSingleton<Ogc.Services.IOgcFeaturesRenderingHandler, Ogc.Services.OgcFeaturesRenderingHandler>();

        // Register Phase 1.4 refactored services (breaking up OgcSharedHandlers)
        services.AddSingleton<Ogc.Services.OgcResponseBuilder>();
        services.AddSingleton<Ogc.Services.IOgcConformanceService, Ogc.Services.ConformanceService>();
        services.AddSingleton<Ogc.Services.IOgcLandingPageService, Ogc.Services.LandingPageService>();
        services.AddSingleton<Ogc.Services.IOgcCollectionService, Ogc.Services.CollectionService>();

        // Register filter parsing cache (performance optimization)
        services.AddOptions<Ogc.Services.FilterParsingCacheOptions>()
            .Bind(configuration.GetSection("FilterParsingCache"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<Ogc.Services.FilterParsingCacheMetrics>();
        services.AddSingleton<Ogc.Services.FilterParsingCacheService>();

        // Register WMS options with validation for memory management and limits
        services.AddOptions<WmsOptions>()
            .Bind(configuration.GetSection(WmsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register OGC API options with validation
        services.AddOptions<OgcApiOptions>()
            .Bind(configuration.GetSection(OgcApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register capabilities caching services (performance optimization)
        services.AddHonuaCapabilitiesCache(configuration);

        // Register OGC collections list caching services (performance optimization)
        services.AddHonuaOgcCollectionsCache();

        return services;
    }

    /// <summary>
    /// Configures performance optimization features including response compression and caching.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaPerformanceOptimizations(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHonuaResponseCompression();
        services.AddHonuaHostCaching(configuration);

        return services;
    }

    /// <summary>
    /// Configures request size limits for file uploads and large requests.
    /// Sets appropriate limits for different endpoint types to prevent DoS attacks:
    /// - Default: 100MB for general API requests
    /// - Large uploads (tiles/rasters): 50MB max
    /// - JSON APIs: 10MB max
    /// Use [RequestSizeLimit] attribute on specific endpoints that need different limits.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The web application builder for method chaining.</returns>
    public static WebApplicationBuilder ConfigureRequestLimits(this WebApplicationBuilder builder)
    {
        // Default max request body size: 100MB
        // This prevents arbitrarily large requests from causing memory exhaustion
        const long defaultMaxBodySize = 100L * 1024 * 1024; // 100MB

        builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = builder.Configuration.GetValue("RequestLimits:MaxBodySize", defaultMaxBodySize);
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            // Set global max request body size to prevent DoS attacks
            options.Limits.MaxRequestBodySize = builder.Configuration.GetValue("RequestLimits:MaxBodySize", defaultMaxBodySize);

            // Additional security limits
            options.Limits.MaxRequestHeadersTotalSize = 32 * 1024; // 32KB
            options.Limits.MaxRequestLineSize = 8 * 1024; // 8KB
            options.Limits.MaxRequestBufferSize = 1 * 1024 * 1024; // 1MB
        });

        return builder;
    }

    /// <summary>
    /// Adds CORS services with metadata-based policy provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaCors(this IServiceCollection services)
    {
        services.AddSingleton<ICorsPolicyProvider, MetadataCorsPolicyProvider>();
        services.AddSingleton<IConfigureOptions<HostFilteringOptions>, MetadataHostFilteringOptionsConfigurator>();
        services.AddSingleton<IOptionsChangeTokenSource<HostFilteringOptions>>(sp =>
            (MetadataHostFilteringOptionsConfigurator)sp.GetRequiredService<IConfigureOptions<HostFilteringOptions>>());
        services.AddCors();

        return services;
    }

    /// <summary>
    /// Adds WFS lock manager for feature locking support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaWfsServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register WFS options with validation
        services.AddOptions<WfsOptions>()
            .Bind(configuration.GetSection(WfsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register metrics
        services.AddSingleton<Wfs.IWfsLockManagerMetrics, Wfs.WfsLockManagerMetrics>();

        // Register IWfsLockManager with environment-based factory
        services.AddSingleton<IWfsLockManager>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var redis = sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<Wfs.RedisWfsLockManager>>();
            var metrics = sp.GetRequiredService<Wfs.IWfsLockManagerMetrics>();

            // Use Redis in production/staging if available
            if (!env.IsDevelopment() && redis != null && redis.IsConnected)
            {
                logger.LogInformation("Using Redis-backed WFS lock manager");
                return new Wfs.RedisWfsLockManager(redis, logger, metrics);
            }

            // Otherwise, use in-memory lock manager
            var memoryLogger = sp.GetRequiredService<ILogger<Wfs.InMemoryWfsLockManager>>();
            if (!env.IsDevelopment())
            {
                memoryLogger.LogWarning(
                    "Redis is not available. Using in-memory WFS lock manager. " +
                    "This is not suitable for production or multi-instance deployments.");
            }
            return new Wfs.InMemoryWfsLockManager();
        });

        // Register WFS schema cache
        services.AddSingleton<IWfsSchemaCache, WfsSchemaCache>();

        return services;
    }

    /// <summary>
    /// Adds capabilities caching services for OGC GetCapabilities responses.
    /// Includes WFS, WMS, WCS, WMTS, and CSW capabilities caching with automatic invalidation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaCapabilitiesCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register capabilities cache options with validation
        services.AddOptions<Services.CapabilitiesCacheOptions>()
            .Bind(configuration.GetSection(Services.CapabilitiesCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register capabilities cache service
        services.AddSingleton<Services.ICapabilitiesCache, Services.CapabilitiesCache>();

        // Register background service for automatic cache invalidation on metadata changes
        services.AddHostedService<Services.CapabilitiesCacheInvalidationService>();

        return services;
    }

    /// <summary>
    /// Adds OGC API collections list caching services for improved performance.
    /// Caches collections list responses (JSON and HTML) with automatic invalidation on metadata changes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// Implements the caching strategy from PERFORMANCE_OPTIMIZATION_OPPORTUNITIES.md:
    /// - Cache key format: ogc:collections:{service_id}:{format}:{accept_language}
    /// - Default TTL: 10 minutes (configurable via OgcApi:CollectionsCacheDurationSeconds)
    /// - Maximum entries: 500 (configurable via OgcApi:MaxCachedCollections)
    /// - Automatic invalidation on service/layer metadata updates
    /// </remarks>
    public static IServiceCollection AddHonuaOgcCollectionsCache(this IServiceCollection services)
    {
        // Register OGC collections cache service
        services.AddSingleton<IOgcCollectionsCache, OgcCollectionsCache>();

        // Register background service for automatic cache invalidation on metadata changes
        services.AddHostedService<OgcCollectionsCacheInvalidationService>();

        return services;
    }

    /// <summary>
    /// Adds raster tile cache metrics and services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaRasterServices(this IServiceCollection services)
    {
        // Register raster dataset registry
        services.AddSingleton<Honua.Server.Core.Raster.IRasterDatasetRegistry, Honua.Server.Core.Raster.RasterDatasetRegistry>();

        services.AddSingleton<IRasterTileCacheMetrics, RasterTileCacheMetrics>();

        // Register raster tile cache provider factory and provider
        services.AddSingleton<Honua.Server.Core.Raster.Caching.IRasterTileCacheProviderFactory, Honua.Server.Core.Raster.Caching.RasterTileCacheProviderFactory>();
        services.AddSingleton<Honua.Server.Core.Raster.Caching.IRasterTileCacheProvider>(sp =>
        {
            var factory = sp.GetRequiredService<Honua.Server.Core.Raster.Caching.IRasterTileCacheProviderFactory>();
            var rasterCacheOptions = sp.GetService<IOptions<RasterCacheOptions>>()?.Value;
            // Default configuration if not configured
            var rasterTileCacheConfig = rasterCacheOptions ?? new RasterCacheOptions
            {
                CogCacheEnabled = true,
                CogCacheProvider = "filesystem"
            };
            return factory.Create(rasterTileCacheConfig);
        });

        // Register raster source providers (required by IRasterSourceProviderRegistry)
        // Only register providers without external dependencies for now
        services.AddSingleton<Honua.Server.Core.Raster.Sources.IRasterSourceProvider, Honua.Server.Core.Raster.Sources.FileSystemRasterSourceProvider>();
        services.AddSingleton<Honua.Server.Core.Raster.Sources.IRasterSourceProvider, Honua.Server.Core.Raster.Sources.HttpRasterSourceProvider>();
        // Cloud providers (S3, Azure, GCS) require their respective cloud SDK services to be registered first
        // TODO: Register cloud raster source providers when cloud storage is configured

        // Register raster source provider registry (required by IRasterRenderer)
        services.AddSingleton<Honua.Server.Core.Raster.Sources.IRasterSourceProviderRegistry, Honua.Server.Core.Raster.Sources.RasterSourceProviderRegistry>();

        // Register raster metadata cache (required by IRasterRenderer)
        services.AddSingleton<Honua.Server.Core.Raster.RasterMetadataCache>();

        // Register raster renderer (required by WMS/WCS)
        services.AddSingleton<Honua.Server.Core.Raster.Rendering.IRasterRenderer, Honua.Server.Core.Raster.Rendering.SkiaSharpRasterRenderer>();

        // Register data ingestion service (BackgroundService)
        services.AddSingleton<Honua.Server.Core.Raster.Import.DataIngestionService>();
        services.AddSingleton<Honua.Server.Core.Raster.Import.IDataIngestionService>(sp =>
            sp.GetRequiredService<Honua.Server.Core.Raster.Import.DataIngestionService>());
        services.AddHostedService(sp =>
            sp.GetRequiredService<Honua.Server.Core.Raster.Import.DataIngestionService>());

        // Register export services
        services.AddSingleton<Honua.Server.Core.Raster.Export.IGeoArrowExporter, Honua.Server.Core.Raster.Export.GeoArrowExporter>();
        services.AddSingleton<Honua.Server.Core.Raster.Export.IGeoParquetExporter, Honua.Server.Core.Raster.Export.GeoParquetExporter>();

        // TODO: RasterTilePreseedService has unregistered dependencies (IRasterTileCacheProvider)
        // Commenting out until Core.Raster services are properly registered
        // services.AddSingleton<RasterTilePreseedService>();
        // services.AddSingleton<IRasterTilePreseedService>(sp => sp.GetRequiredService<RasterTilePreseedService>());
        // services.AddHostedService(sp => sp.GetRequiredService<RasterTilePreseedService>());

        return services;
    }

    /// <summary>
    /// Adds vector tile preseed services with resource exhaustion protection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaVectorTilePreseedServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure limits from appsettings with validation
        services.AddOptions<VectorTiles.VectorTilePreseedLimits>()
            .Bind(configuration.GetSection("honua:vectorTilePreseed"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register the service as both singleton and hosted service
        services.AddSingleton<VectorTiles.VectorTilePreseedService>();
        services.AddSingleton<VectorTiles.IVectorTilePreseedService>(sp =>
            sp.GetRequiredService<VectorTiles.VectorTilePreseedService>());
        services.AddHostedService(sp =>
            sp.GetRequiredService<VectorTiles.VectorTilePreseedService>());

        return services;
    }

    /// <summary>
    /// Adds STAC catalog synchronization background service and validation services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaStacServices(this IServiceCollection services)
    {
        services.AddHostedService<StacCatalogSynchronizationHostedService>();
        services.AddSingleton<IStacValidationService, StacValidationService>();
        services.AddSingleton<StacMetrics>();

        // Register STAC service layer (refactored from controller)
        services.AddScoped<Stac.Services.StacControllerHelper>();
        services.AddScoped<Stac.Services.StacParsingService>();
        services.AddScoped<Stac.Services.StacReadService>();
        services.AddScoped<Stac.Services.StacCollectionService>();
        services.AddScoped<Stac.Services.StacItemService>();

        return services;
    }

    /// <summary>
    /// Adds Carto SQL query processing services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaCartoServices(this IServiceCollection services)
    {
        services.AddSingleton<CartoDatasetResolver>();
        services.AddSingleton<CartoSqlQueryParser>();
        services.AddSingleton<CartoSqlQueryExecutor>();

        return services;
    }

    /// <summary>
    /// Adds MVC controllers with views and Razor Pages support.
    /// Configures JSON deserialization with security limits to prevent DoS attacks.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The MVC builder for further configuration.</returns>
    public static IMvcBuilder AddHonuaMvcServices(this IServiceCollection services)
    {
        // Register API versioning before MVC
        services.AddHonuaApiVersioning();

        // Register Geoservices REST API services (refactored from god class)
        services.AddGeoservicesRestServices();

        var mvcBuilder = services.AddControllersWithViews(options =>
        {
            // Add security filters in the correct order (innermost to outermost):
            // 1. SecureExceptionFilter - catches all exceptions (outermost in execution)
            options.Filters.Add<SecureExceptionFilter>();

            // 2. SecureInputValidationFilter - validates input before action execution
            options.Filters.Add<SecureInputValidationFilter>();

            // 3. SecureOutputSanitizationFilter - sanitizes output after action execution
            options.Filters.Add<SecureOutputSanitizationFilter>();
        })
        .AddJsonOptions(options =>
        {
            // Apply JSON security limits to prevent DoS attacks
            // - MaxDepth prevents stack overflow from deeply nested JSON
            // - Protects all controller endpoints receiving JSON
            Honua.Server.Host.Configuration.JsonSecurityOptions.ApplySecurityLimits(options.JsonSerializerOptions);
        });

        services.AddRazorPages();

        return mvcBuilder;
    }

#if ENABLE_ODATA
    /// <summary>
    /// Configures OData services (AOT-compatible implementation using Minimal APIs).
    /// OData endpoints are registered via MapODataEndpoints in EndpointExtensions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="mvcBuilder">The MVC builder.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaODataServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IMvcBuilder mvcBuilder)
    {
        // OData v4 is now implemented using AOT-compatible Minimal APIs
        // Endpoints are registered in MapODataEndpoints() - no Microsoft.AspNetCore.OData dependencies

        // Register hosted service for catalog warm-up
        services.AddHostedService<CatalogProjectionWarmupHostedService>();

        return services;
    }
#else
    /// <summary>
    /// Placeholder when OData is disabled. Registers catalog warm-up service only.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="mvcBuilder">The MVC builder.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaODataServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IMvcBuilder mvcBuilder)
    {
        // OData is disabled - only register catalog warm-up
        services.AddHostedService<CatalogProjectionWarmupHostedService>();
        return services;
    }
#endif

    /// <summary>
    /// Adds runtime security validation services for production deployments.
    /// Also configures trusted proxy validation for forwarded headers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaSecurityValidation(this IServiceCollection services)
    {
        services.AddSingleton<IRuntimeSecurityConfigurationValidator, RuntimeSecurityConfigurationValidator>();
        services.AddHostedService<RuntimeSecurityValidationHostedService>();
        services.AddHostedService<ProductionSecurityValidationHostedService>();

        // SECURITY FIX: Register TrustedProxyValidator as a singleton
        // This is used to validate forwarded headers (X-Forwarded-For, X-Forwarded-Proto, etc.)
        services.AddSingleton<TrustedProxyValidator>();

        return services;
    }

    /// <summary>
    /// Adds database schema validation services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaSchemaValidation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<SchemaValidationOptions>()
            .Bind(configuration.GetSection("SchemaValidation"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHostedService<SchemaValidationHostedService>();

        return services;
    }

    /// <summary>
    /// Registers Geoservices REST API service layer (refactored from god class).
    /// Note: GeometryComplexityValidator is a static utility class used for DoS protection
    /// in geometry operations (validates max vertices, coordinates, and nesting depth).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddGeoservicesRestServices(this IServiceCollection services)
    {
        // Register all Geoservices service implementations
        services.AddScoped<IGeoservicesMetadataService, GeoservicesMetadataService>();
        services.AddSingleton<IGeoservicesAuditLogger, GeoservicesAuditLogger>();
        services.AddScoped<IGeoservicesQueryService, GeoservicesQueryService>();
        services.AddScoped<IGeoservicesEditingService, GeoservicesEditingService>();

        // Register streaming writers for efficient export formats
        services.AddScoped<StreamingGeoJsonWriter>();
        services.AddScoped<StreamingKmlWriter>();

        // Register export and attachment services (refactored from controller)
        services.AddScoped<IGeoservicesExportService, GeoservicesExportService>();
        services.AddScoped<IGeoservicesAttachmentService, GeoservicesAttachmentService>();

        return services;
    }

    /// <summary>
    /// Configures localization services with support for multiple languages.
    /// Supports: English (en-US), French (fr-FR), Spanish (es-ES), Italian (it-IT),
    /// German (de-DE), Portuguese (pt-BR), and Japanese (ja-JP).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaLocalization(this IServiceCollection services)
    {
        // Add localization services with Resources path
        services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });

        // Configure supported cultures
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[]
            {
                new System.Globalization.CultureInfo("en-US"), // English (United States) - Default
                new System.Globalization.CultureInfo("fr-FR"), // French (France)
                new System.Globalization.CultureInfo("es-ES"), // Spanish (Spain)
                new System.Globalization.CultureInfo("it-IT"), // Italian (Italy)
                new System.Globalization.CultureInfo("de-DE"), // German (Germany)
                new System.Globalization.CultureInfo("pt-BR"), // Portuguese (Brazil)
                new System.Globalization.CultureInfo("ja-JP")  // Japanese (Japan)
            };

            options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en-US");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            // Configure culture providers (order matters - first match wins):
            // 1. Query string (?culture=fr-FR)
            // 2. Cookie (.AspNetCore.Culture)
            // 3. Accept-Language header
            options.RequestCultureProviders = new Microsoft.AspNetCore.Localization.IRequestCultureProvider[]
            {
                new Microsoft.AspNetCore.Localization.QueryStringRequestCultureProvider(),
                new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider(),
                new Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider()
            };
        });

        return services;
    }

    /// <summary>
    /// Adds comprehensive health checks for database, cache, and storage.
    /// Provides /health, /health/ready, and /health/live endpoints for Kubernetes probes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure health check options with validation
        services.AddOptions<HealthChecks.HealthCheckOptions>()
            .Bind(configuration.GetSection(HealthChecks.HealthCheckOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var healthCheckOptions = configuration
            .GetSection(HealthChecks.HealthCheckOptions.SectionName)
            .Get<HealthChecks.HealthCheckOptions>() ?? new HealthChecks.HealthCheckOptions();

        // Build health checks with conditional registration based on configuration
        var healthChecksBuilder = services.AddHealthChecks();

        // Add database health check (tagged as "ready" for readiness probe)
        if (healthCheckOptions.EnableDatabaseCheck)
        {
            healthChecksBuilder.AddCheck<HealthChecks.DatabaseHealthCheck>(
                "database",
                tags: new[] { "ready", "database" },
                timeout: healthCheckOptions.Timeout);
        }

        // Add cache health check (tagged as "ready" for readiness probe)
        if (healthCheckOptions.EnableCacheCheck)
        {
            healthChecksBuilder.AddCheck<HealthChecks.CacheHealthCheck>(
                "cache",
                tags: new[] { "ready", "cache" },
                timeout: healthCheckOptions.Timeout);
        }

        // Add storage health check (tagged as "ready" for readiness probe)
        if (healthCheckOptions.EnableStorageCheck)
        {
            healthChecksBuilder.AddCheck<HealthChecks.StorageHealthCheck>(
                "storage",
                tags: new[] { "ready", "storage" },
                timeout: healthCheckOptions.Timeout);
        }

        // Add circuit breaker health check (tagged as "live" - service is degraded but running)
        healthChecksBuilder.AddCheck<HealthChecks.CircuitBreakerHealthCheck>(
            "circuit_breaker",
            tags: new[] { "live", "resilience" },
            timeout: healthCheckOptions.Timeout);

        // Add Health Checks UI if enabled (typically for development/staging)
        if (healthCheckOptions.EnableUI)
        {
            services.AddHealthChecksUI(setup =>
            {
                setup.SetEvaluationTimeInSeconds((int)healthCheckOptions.Period.TotalSeconds);
                setup.MaximumHistoryEntriesPerEndpoint(50);
            }).AddInMemoryStorage();
        }

        return services;
    }
}
