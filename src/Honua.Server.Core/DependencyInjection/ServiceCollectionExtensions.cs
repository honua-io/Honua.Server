// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Caching.Resilience;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Auth;
using Honua.Server.Core.Data.DuckDB;
using Honua.Server.Core.Data.MySql;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Data.SqlServer;
using Honua.Server.Core.Data.Validation;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Elevation;
using Honua.Server.Core.Export;
using Honua.Server.Core.Geoservices.GeometryService;
using Honua.Server.Core.Import;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Metadata.Snapshots;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Honua.Server.Core.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Hedging;
using Polly.Timeout;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds distributed caching support for multi-instance deployments.
    /// Uses Redis if connection string is configured, otherwise falls back to in-memory cache for development.
    /// </summary>
    public static IServiceCollection AddHonuaCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add distributed cache
        var redisConnection = configuration.GetConnectionString("Redis");

        if (redisConnection.HasValue())
        {
            // Use Redis for distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "Honua_";
            });

            // Register IConnectionMultiplexer for advanced cache invalidation
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
            {
                return StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection);
            });

            // Register distributed cache invalidation service (Redis only)
            services.AddSingleton<IDistributedCacheInvalidationService, RedisCacheInvalidationService>();
        }
        else
        {
            // Fallback to in-memory cache for development
            services.AddDistributedMemoryCache();
        }

        // Configure memory cache with size limits to prevent OOM
        // Load cache size limit options from configuration
        services.Configure<CacheSizeLimitOptions>(configuration.GetSection(CacheSizeLimitOptions.SectionName));
        services.Configure<InMemoryStoreOptions>(configuration.GetSection(InMemoryStoreOptions.SectionName));

        // Configure cache invalidation options
        services.Configure<CacheInvalidationOptions>(configuration.GetSection("CacheInvalidation"));

        // Register cache invalidation retry policy
        services.AddSingleton<CacheInvalidationRetryPolicy>();

        // Configure query result cache options
        services.Configure<QueryResultCacheOptions>(configuration.GetSection("QueryResultCache"));

        // Register query result cache service
        services.AddSingleton<IQueryResultCacheService, QueryResultCacheService>();

        // Add memory cache with bounded size to prevent memory exhaustion
        services.AddMemoryCache(options =>
        {
            var cacheConfig = configuration.GetSection(CacheSizeLimitOptions.SectionName).Get<CacheSizeLimitOptions>()
                              ?? new CacheSizeLimitOptions();

            // Validate configuration
            cacheConfig.Validate();

            // Set size limit (entries must specify size via CacheItemOptions.Size)
            // Default: 10,000 entries to prevent unbounded growth
            options.SizeLimit = cacheConfig.MaxTotalEntries;

            // Configure expiration scan frequency
            // Default: 1 minute for responsive eviction
            options.ExpirationScanFrequency = cacheConfig.ExpirationScanFrequency;

            // Enable automatic compaction on memory pressure
            options.CompactionPercentage = cacheConfig.CompactionPercentage;
        });

        // Register cache metrics collector for monitoring
        services.AddSingleton<CacheMetricsCollector>();

        return services;
    }

    public static IServiceCollection AddHonuaCore(this IServiceCollection services, IConfiguration configuration, string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(basePath);

        var honuaSection = configuration.GetSection("honua");
        if (!honuaSection.Exists())
        {
            throw new InvalidDataException("Configuration missing 'honua' section.");
        }

        // Register configuration sections for validation
        services.Configure<HonuaAuthenticationOptions>(configuration.GetSection(HonuaAuthenticationOptions.SectionName));
        services.Configure<OpenRosaOptions>(configuration.GetSection("OpenRosa"));
        services.Configure<ConnectionStringOptions>(configuration.GetSection(ConnectionStringOptions.SectionName));
        services.Configure<GeometryValidation.GeometryComplexityOptions>(
            configuration.GetSection(GeometryValidation.GeometryComplexityOptions.SectionName));

        // Register validators
        services.AddSingleton<IValidateOptions<HonuaAuthenticationOptions>, HonuaAuthenticationOptionsValidator>();

        // Register geometry complexity validator for DOS protection
        services.AddSingleton<GeometryValidation.GeometryComplexityValidator>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GeometryValidation.GeometryComplexityOptions>>().Value;
            return new GeometryValidation.GeometryComplexityValidator(options);
        });

        // Configuration hot reload support
        services.AddSingleton<ConfigurationChangeNotificationService>();

        // Add configuration validation - this will validate on startup
        services.AddConfigurationValidation();

        // Add distributed and memory caching support
        services.AddHonuaCaching(configuration);

        // Register time provider for testable time-dependent code
        services.AddSingleton<Time.ITimeProvider, Time.SystemTimeProvider>();

        // Note: Compression codecs for Zarr and other raster formats are now registered
        // in Honua.Server.Core.Raster project via AddHonuaCompressionCodecs extension method

        services.AddSingleton<IMetadataSchemaValidator>(sp =>
        {
            var cache = sp.GetRequiredService<IMemoryCache>();
            return MetadataSchemaValidator.CreateDefault(cache);
        });

        // Configuration V2 metadata system is now mandatory
        // Legacy metadata provider registration has been removed
        // Metadata provider should be registered via Configuration V2 system

        services.AddSingleton<IDataStoreProviderFactory, DataStoreProviderFactory>();
        services.AddSingleton<IFeatureContextResolver, FeatureContextResolver>();
        services.AddKeyedSingleton<IDataStoreProvider>(SqliteDataStoreProvider.ProviderKey, (_, _) => new SqliteDataStoreProvider());
        services.AddKeyedSingleton<IDataStoreProvider>(DuckDBDataStoreProvider.ProviderKey, (_, _) => new DuckDBDataStoreProvider());
        services.AddKeyedSingleton<IDataStoreProvider>(PostgresDataStoreProvider.ProviderKey, (_, _) => new PostgresDataStoreProvider());
        services.AddKeyedSingleton<IDataStoreProvider>(SqlServerDataStoreProvider.ProviderKey, (_, _) => new SqlServerDataStoreProvider());
        services.AddKeyedSingleton<IDataStoreProvider>(MySqlDataStoreProvider.ProviderKey, (_, _) => new MySqlDataStoreProvider());

        // IFeatureRepository is stateless and safe as Singleton
        // It resolves feature context per operation and doesn't hold request-specific state
        services.AddSingleton<IFeatureRepository, FeatureRepository>();

        // Schema validation and discovery - provider-specific implementations
        services.AddSingleton<PostgresSchemaDiscoveryService>();
        services.AddSingleton<MySqlSchemaDiscoveryService>();
        services.AddSingleton<SqlServerSchemaDiscoveryService>();
        services.AddSingleton<SqliteSchemaDiscoveryService>();

        services.AddSingleton<PostgresSchemaValidator>();
        services.AddSingleton<MySqlSchemaValidator>();
        services.AddSingleton<SqlServerSchemaValidator>();
        services.AddSingleton<SqliteSchemaValidator>();

        services.AddSingleton<ISchemaValidatorFactory, SchemaValidatorFactory>();
        services.AddSingleton<ISchemaDiscoveryServiceFactory, SchemaDiscoveryServiceFactory>();

        services.AddSingleton<IGeoPackageExporter, GeoPackageExporter>();
        services.AddSingleton<IFlatGeobufExporter, FlatGeobufExporter>();
        // Note: IGeoArrowExporter is now registered in Honua.Server.Core.Raster
        services.AddSingleton<IPmTilesExporter, PmTilesExporter>();
        services.AddSingleton<ICsvExporter, CsvExporter>();
        services.AddSingleton<IShapefileExporter, ShapefileExporter>();

        // Register elevation services for 3D visualization support
        services.AddElevationServices();

        services.AddSingleton<ICatalogProjectionService, CatalogProjectionService>();
        services.AddSingleton<RasterStacCatalogBuilder>();
        services.AddSingleton<VectorStacCatalogBuilder>();
        services.AddSingleton<IRasterStacCatalogSynchronizer, RasterStacCatalogSynchronizer>();
        services.AddSingleton<IVectorStacCatalogSynchronizer, VectorStacCatalogSynchronizer>();

        // Register STAC catalog store with configuration from appsettings
        services.Configure<StacCatalogOptions>(configuration.GetSection(StacCatalogOptions.SectionName));
        services.AddSingleton<IStacCatalogStore>(sp =>
        {
            var stacOptions = sp.GetRequiredService<IOptions<StacCatalogOptions>>().Value;
            return new StacCatalogStoreFactory().Create(stacOptions);
        });

        services.AddSingleton<IAuthRepository>(sp =>
        {
            var authOptions = sp.GetRequiredService<IOptionsMonitor<HonuaAuthenticationOptions>>();
            var connectionStrings = sp.GetRequiredService<IOptions<ConnectionStringOptions>>().Value;
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var authMetrics = sp.GetService<AuthMetrics>();
            var dataAccess = sp.GetService<IOptions<DataAccessOptions>>();

            var localOptions = authOptions.CurrentValue.Local;
            var provider = (localOptions.Provider ?? "sqlite").Trim().ToLowerInvariant();

            return provider switch
            {
                "sqlite" => new SqliteAuthRepository(basePath, authOptions, loggerFactory.CreateLogger<SqliteAuthRepository>(), authMetrics, dataAccess),
                "postgres" or "postgresql" => new PostgresAuthRepository(
                    authOptions,
                    loggerFactory.CreateLogger<PostgresAuthRepository>(),
                    authMetrics,
                    ResolveConnectionString(localOptions, connectionStrings, "Postgres"),
                    localOptions.Schema),
                "mysql" => new MySqlAuthRepository(
                    authOptions,
                    loggerFactory.CreateLogger<MySqlAuthRepository>(),
                    authMetrics,
                    ResolveConnectionString(localOptions, connectionStrings, "MySql")),
                "sqlserver" => new SqlServerAuthRepository(
                    authOptions,
                    loggerFactory.CreateLogger<SqlServerAuthRepository>(),
                    authMetrics,
                    ResolveConnectionString(localOptions, connectionStrings, "SqlServer"),
                    localOptions.Schema),
                _ => throw new InvalidOperationException($"Unsupported local authentication provider '{localOptions.Provider}'.")
            };
        });
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IPasswordComplexityValidator>(sp => new PasswordComplexityValidator(
            minimumLength: 12,
            requireUppercase: true,
            requireLowercase: true,
            requireDigit: true,
            requireSpecialCharacter: true));
        services.AddSingleton<IAuthBootstrapService, AuthBootstrapService>();
        services.AddSingleton<ILocalSigningKeyProvider, LocalSigningKeyProvider>();
        services.AddSingleton<ILocalTokenService, LocalTokenService>();
        services.AddSingleton<ILocalAuthenticationService, LocalAuthenticationService>();
        services.AddSingleton<ISecurityAuditLogger, SecurityAuditLogger>();

        // Token revocation services
        services.AddHonuaTokenRevocation(configuration);

        // Register metadata cache options
        services.Configure<MetadataCacheOptions>(configuration.GetSection("Honua:MetadataCache"));

        // Register metadata cache metrics
        services.AddSingleton<MetadataCacheMetrics>();

        // METADATA PROVIDER REGISTRATION
        // Configuration V2 with HclMetadataProvider is now the only supported metadata source
        services.AddSingleton<IMetadataProvider>(sp =>
        {
            // Configuration V2 is now mandatory
            var v2ConfigPath = configuration.GetValue<string>("Honua:ConfigurationV2:Path");
            if (string.IsNullOrWhiteSpace(v2ConfigPath))
            {
                throw new InvalidOperationException(
                    "Configuration V2 path is required. Legacy metadata providers (JSON/YAML) have been removed. " +
                    "Please configure 'Honua:ConfigurationV2:Path' in appsettings.json to point to your .hcl configuration file.");
            }

            var v2Config = Configuration.V2.HonuaConfigLoader.Load(v2ConfigPath);
            return new HclMetadataProvider(v2Config);
        });

        services.AddSingleton<IMetadataRegistry>(sp =>
        {
            var provider = sp.GetRequiredService<IMetadataProvider>();
            var innerRegistry = new MetadataRegistry(provider);

            // Wrap with caching layer if distributed cache is available
            // IMPORTANT: IDistributedCache lifetime assumption
            // We assume IDistributedCache is registered as Singleton (standard for Redis/SQL distributed cache)
            // If using a non-standard distributed cache implementation, verify it's registered as Singleton
            // to avoid captive dependency issues when injected into this Singleton registry
            var distributedCache = sp.GetService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
            if (distributedCache is not null)
            {
                var cacheOptionsMonitor = sp.GetRequiredService<IOptionsMonitor<MetadataCacheOptions>>();
                var invalidationOptions = sp.GetRequiredService<IOptionsMonitor<CacheInvalidationOptions>>();
                var logger = sp.GetRequiredService<ILogger<CachedMetadataRegistry>>();
                var retryPolicy = sp.GetRequiredService<CacheInvalidationRetryPolicy>();
                var metrics = sp.GetRequiredService<MetadataCacheMetrics>();
                return new CachedMetadataRegistry(innerRegistry, distributedCache, cacheOptionsMonitor, invalidationOptions, logger, retryPolicy, metrics);
            }

            return innerRegistry;
        });

        // Note: Raster services (IRasterDatasetRegistry, IRasterRenderer, IMapFishPrintService, etc.)
        // are now registered in Honua.Server.Core.Raster project via AddHonuaRasterServices extension method
        services.AddSingleton<IGeometrySerializer, EsriGeometrySerializer>();
        services.AddSingleton<IGeometryOperationExecutor, GeometryOperationExecutor>();

        // Metadata snapshot store removed - legacy feature that depended on file-based metadata configuration
        // Security configuration validator removed - legacy feature
        services.AddSingleton<Observability.IApiMetrics, Observability.ApiMetrics>();
        services.AddSingleton<Observability.ICircuitBreakerMetrics, Observability.CircuitBreakerMetrics>();

        // Configure circuit breaker options and service
        services.Configure<Resilience.CircuitBreakerOptions>(configuration.GetSection(Resilience.CircuitBreakerOptions.SectionName));
        services.AddSingleton<Resilience.ICircuitBreakerService, Resilience.CircuitBreakerService>();

        // Configure bulkhead resilience options and services
        services.Configure<Resilience.BulkheadOptions>(configuration.GetSection(Resilience.BulkheadOptions.SectionName));
        services.AddSingleton<Resilience.BulkheadPolicyProvider>();
        services.AddSingleton<Resilience.TenantResourceLimiter>();
        services.AddSingleton<Resilience.MemoryCircuitBreaker>();

        // Security validation hosted service removed - legacy feature
        // MetadataInitializationHostedService and ServiceApiValidationHostedService removed - handled by Configuration V2
        services.AddHostedService<AuthInitializationHostedService>();

        services.AddSingleton<IDataIngestionQueueStore>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var logger = sp.GetRequiredService<ILogger<FileDataIngestionQueueStore>>();
            var queueDirectory = Path.Combine(env.ContentRootPath, "var", "ingestion-queue");
            return new FileDataIngestionQueueStore(queueDirectory, logger);
        });

        // Register feature schema validator for data ingestion validation
        services.AddSingleton<Import.Validation.IFeatureSchemaValidator, Import.Validation.FeatureSchemaValidator>();

        // Configure data ingestion options with transaction support
        services.AddOptions<Configuration.DataIngestionOptions>()
            .BindConfiguration("Honua:DataIngestion")
            .ValidateOnStart();

        // Note: DataIngestionService is now registered in Honua.Server.Core.Raster

        // GeoservicesRest migration services removed - legacy feature that depended on file-based metadata configuration
        // Configure GeoservicesRest client with hedging for latency-sensitive operations
        services.AddHttpClient<Migration.GeoservicesRest.IGeoservicesRestServiceClient, Migration.GeoservicesRest.GeoservicesRestServiceClient>()
            .AddResilienceHandler("geoservices-hedging", (builder, context) =>
            {
                var metrics = context.ServiceProvider.GetService<Observability.ICircuitBreakerMetrics>();

                // Apply hedging to reduce latency when querying external ArcGIS services
                var hedgingPipeline = CreateHttpHedgingPipeline(context.ServiceProvider, metrics);
                builder.AddPipeline(hedgingPipeline);
            });

        services.AddSingleton<IFeatureEditAuthorizationService, FeatureEditAuthorizationService>();
        services.AddSingleton<IFeatureEditConstraintValidator, FeatureEditConstraintValidator>();

        // IFeatureEditOrchestrator is stateless and safe as Singleton
        // It orchestrates feature editing operations but doesn't hold request-specific state
        // All dependencies are either Singleton services or resolved per-operation (metadata snapshot)
        services.AddSingleton<IFeatureEditOrchestrator, FeatureEditOrchestrator>();

        // Register IFeatureAttachmentRepository with environment-based factory
        services.AddSingleton<IFeatureAttachmentRepository>(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var redis = sp.GetService<StackExchange.Redis.IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILogger<Attachments.RedisFeatureAttachmentRepository>>();

            // Use Redis in production/staging if available
            if (!env.IsDevelopment() && redis != null && redis.IsConnected)
            {
                logger.LogInformation("Using Redis-backed feature attachment repository");
                return new Attachments.RedisFeatureAttachmentRepository(redis, logger);
            }

            // Otherwise, use in-memory repository
            if (!env.IsDevelopment())
            {
                logger.LogWarning(
                    "Redis is not available. Using in-memory feature attachment repository. " +
                    "This is not suitable for production or multi-instance deployments.");
            }
            return new Attachments.InMemoryFeatureAttachmentRepository();
        });

        services.AddSingleton<IAttachmentStoreProvider>(sp => new FileSystemAttachmentStoreProvider(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetService<IMeterFactory>()));

        // Note: Cloud attachment providers (S3, Azure Blob, GCS) are now registered
        // in Honua.Server.Core.Cloud project via AddCloudAttachmentStoreProviders extension method

        // Register attachment configuration options
        services.Configure<AttachmentConfigurationOptions>(configuration.GetSection(AttachmentConfigurationOptions.SectionName));

        // Conditionally register database attachment provider if any profile uses it
        var attachmentConfig = configuration.GetSection(AttachmentConfigurationOptions.SectionName).Get<AttachmentConfigurationOptions>();
        if (attachmentConfig?.Profiles?.Values.Any(profile => profile != null && string.Equals(profile.Provider, AttachmentStoreProviderKeys.Database, StringComparison.OrdinalIgnoreCase)) == true)
        {
            services.AddSingleton<IAttachmentStoreProvider>(sp => new DatabaseAttachmentStoreProvider(sp.GetRequiredService<ILoggerFactory>()));
        }
        services.AddSingleton<IAttachmentStoreSelector, AttachmentStoreSelector>();
        services.AddSingleton<IFeatureAttachmentOrchestrator, FeatureAttachmentOrchestrator>();

        // OpenRosa/ODK services
        services.AddSingleton<OpenRosa.IXFormGenerator, OpenRosa.XFormGenerator>();
        services.AddSingleton<OpenRosa.ISubmissionProcessor, OpenRosa.SubmissionProcessor>();
        services.AddSingleton<OpenRosa.ISubmissionRepository>(sp =>
        {
            var connectionString = Path.Combine(basePath, "data", "openrosa-submissions.db");
            return new OpenRosa.SqliteSubmissionRepository($"Data Source={connectionString}");
        });

        // Register AEC services for graph database, 3D geometry, and IFC import
        services.AddScoped<Services.IGraphDatabaseService, Services.GraphDatabaseService>();
        services.AddScoped<Services.Geometry3D.IGeometry3DService, Services.Geometry3D.Geometry3DService>();
        services.AddScoped<Services.Geometry3D.IMeshConverter, Services.Geometry3D.MeshConverter>();
        services.AddScoped<Services.IIfcImportService, Services.IfcImportService>();

        return services;
    }

    /// <summary>
    /// Adds JWT token revocation services using Redis-backed storage.
    /// Enables token blacklisting for security purposes (logout, account compromise, etc.).
    /// </summary>
    public static IServiceCollection AddHonuaTokenRevocation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure token revocation options
        services.Configure<TokenRevocationOptions>(
            configuration.GetSection(TokenRevocationOptions.SectionName));

        // Register revocation service
        services.AddSingleton<ITokenRevocationService, RedisTokenRevocationService>();

        // Add health check for token revocation service
        // Note: Use Degraded status instead of Unhealthy because the service can still function
        // without Redis (it will just use in-memory storage as fallback)
        // Only include in "ready" check, not "live" check, as it depends on external Redis
        // Use 2 second timeout to avoid slow health checks when Redis is unavailable
        services.AddHealthChecks()
            .AddCheck<RedisTokenRevocationService>(
                "token_revocation",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "ready" },
                timeout: TimeSpan.FromSeconds(2));

        return services;
    }

    /// <summary>
    /// Adds GitOps and deployment services for managing deployments and approvals.
    /// Configures file-based state storage and approval workflow.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <param name="basePath">Base path for data storage</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddHonuaGitOpsServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string basePath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrEmpty(basePath);

        // Configure deployment state storage directory
        var stateDirectory = configuration.GetValue<string>("Honua:GitOps:StateDirectory");
        if (stateDirectory.IsNullOrWhiteSpace())
        {
            stateDirectory = Path.Combine(basePath, "data", "deployments");
        }
        else if (!Path.IsPathFullyQualified(stateDirectory))
        {
            stateDirectory = Path.GetFullPath(Path.Combine(basePath, stateDirectory));
        }

        // Configure approval storage directory
        var approvalDirectory = configuration.GetValue<string>("Honua:GitOps:ApprovalDirectory");
        if (approvalDirectory.IsNullOrWhiteSpace())
        {
            approvalDirectory = Path.Combine(basePath, "data", "approvals");
        }
        else if (!Path.IsPathFullyQualified(approvalDirectory))
        {
            approvalDirectory = Path.GetFullPath(Path.Combine(basePath, approvalDirectory));
        }

        // Register deployment state store
        services.AddSingleton<Deployment.IDeploymentStateStore>(sp =>
        {
            return new Deployment.FileStateStore(stateDirectory);
        });

        // Register approval service
        services.AddSingleton<Deployment.IApprovalService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<Deployment.FileApprovalService>>();

            // Load environment policies from configuration if available
            var policiesSection = configuration.GetSection("Honua:GitOps:DeploymentPolicies");
            var environmentPolicies = new Dictionary<string, Deployment.DeploymentPolicy>();

            if (policiesSection.Exists())
            {
                foreach (var child in policiesSection.GetChildren())
                {
                    var policy = new Deployment.DeploymentPolicy();
                    child.Bind(policy);
                    environmentPolicies[child.Key.ToLowerInvariant()] = policy;
                }
            }

            return new Deployment.FileApprovalService(
                approvalDirectory,
                logger,
                environmentPolicies.Count > 0 ? environmentPolicies : null);
        });

        return services;
    }

    /// <summary>
    /// Registers a CRS transformation provider.
    /// If no provider is specified, defaults to <see cref="ProjNETCrsTransformProvider"/>.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="provider">Custom CRS transformation provider (optional)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCrsTransformProvider(
        this IServiceCollection services,
        ICrsTransformProvider? provider = null)
    {
        var crsProvider = provider ?? new ProjNETCrsTransformProvider();
        CrsTransform.Provider = crsProvider;
        services.AddSingleton(crsProvider);
        return services;
    }

    private static ResiliencePipeline<HttpResponseMessage> CreateHttpHedgingPipeline(
        IServiceProvider serviceProvider,
        Observability.ICircuitBreakerMetrics? metrics)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("Resilience.Hedging");

        const int maxHedgedAttempts = 2;
        const int hedgingDelayMilliseconds = 50;
        var timeout = TimeSpan.FromSeconds(5);

        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddHedging(new HedgingStrategyOptions<HttpResponseMessage>
            {
                MaxHedgedAttempts = maxHedgedAttempts,
                Delay = TimeSpan.FromMilliseconds(hedgingDelayMilliseconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                    .Handle<TimeoutRejectedException>()
                    .Handle<SocketException>()
                    .HandleResult(response =>
                        response.StatusCode >= HttpStatusCode.InternalServerError ||
                        response.StatusCode == HttpStatusCode.RequestTimeout ||
                        response.StatusCode == HttpStatusCode.TooManyRequests),
                OnHedging = args =>
                {
                    var attemptNumber = args.AttemptNumber;

                    logger.LogWarning(
                        "Hedging HTTP request (attempt {AttemptNumber}).",
                        attemptNumber);

                    metrics?.RecordHedgingAttempt(attemptNumber, "N/A", "None");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = timeout,
                OnTimeout = args =>
                {
                    logger.LogWarning(
                        "Hedging operation timed out after {Timeout}s (all attempts failed or exceeded timeout)",
                        args.Timeout.TotalSeconds);

                    metrics?.RecordHedgingTimeout(args.Timeout.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    private static string ResolveConnectionString(HonuaAuthenticationOptions.LocalOptions localOptions, ConnectionStringOptions connectionStrings, string providerName)
    {
        if (localOptions.ConnectionString.HasValue())
        {
            return localOptions.ConnectionString;
        }

        if (localOptions.ConnectionStringName.HasValue())
        {
            var named = GetConnectionStringByName(localOptions.ConnectionStringName, connectionStrings);
            if (named.HasValue())
            {
                return named;
            }
        }

        var fallback = providerName switch
        {
            "Postgres" => connectionStrings.Postgres ?? connectionStrings.DefaultConnection,
            "MySql" => connectionStrings.MySql ?? connectionStrings.DefaultConnection,
            "SqlServer" => connectionStrings.SqlServer ?? connectionStrings.DefaultConnection,
            _ => connectionStrings.DefaultConnection
        };

        if (fallback.HasValue())
        {
            return fallback;
        }

        throw new InvalidOperationException($"Connection string not configured for local authentication provider '{localOptions.Provider}'.");
    }

    private static string? GetConnectionStringByName(string name, ConnectionStringOptions options)
    {
        if (name.IsNullOrWhiteSpace())
        {
            return null;
        }

        return name switch
        {
            nameof(ConnectionStringOptions.DefaultConnection) or "DefaultConnection" => options.DefaultConnection,
            nameof(ConnectionStringOptions.Postgres) or "Postgres" or "PostgreSQL" => options.Postgres,
            nameof(ConnectionStringOptions.MySql) or "MySql" or "MySQL" => options.MySql,
            nameof(ConnectionStringOptions.SqlServer) or "SqlServer" or "SQLServer" => options.SqlServer,
            _ => null
        };
    }
}
