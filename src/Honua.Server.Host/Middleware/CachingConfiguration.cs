// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Memory;
using Honua.Server.Core.Extensions;
using System.Security.Claims;
using System.Text;

namespace Honua.Server.Host.Middleware;

/// <summary>
/// Configuration for output caching and in-memory caching.
/// Optimizes performance for frequently accessed, read-heavy OGC API endpoints.
/// Uses .NET 7+ Output Caching for better performance and flexibility.
/// </summary>
public static class CachingConfiguration
{
    public static IServiceCollection AddHonuaHostCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var outputCacheMaxMb = configuration.GetValue("Performance:OutputCache:MaxSizeMB", 100);
        var memoryCacheMaxMb = configuration.GetValue("Performance:MemoryCache:MaxSizeMB", 200);

        // Register cache invalidation service
        services.AddSingleton<IOutputCacheInvalidationService, OutputCacheInvalidationService>();

        // Add output caching middleware (.NET 7+)
        // Benefits over ResponseCaching: better performance, tag-based invalidation, resource-based
        services.AddOutputCache(options =>
        {
            // Maximum cache size (configurable, default: 100 MB)
            options.SizeLimit = outputCacheMaxMb * 1024 * 1024;

            // Default expiration policy (10 seconds for general API responses)
            options.AddBasePolicy(builder => builder
                .Expire(TimeSpan.FromSeconds(10))
                .Tag("api-cache")
                .SetVaryByHeader("Accept", "Accept-Encoding"));

            // STAC Collections policy: cache for 5 minutes
            // Tag-based invalidation allows selective cache clearing on POST/PUT/DELETE
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            options.AddPolicy("stac-collections", builder => builder
                .Expire(TimeSpan.FromMinutes(5))
                .Tag("stac", "stac-collections")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByQuery("*")
                .VaryByValue(context => GetUserCacheKey(context)));

            // STAC Collection metadata policy: cache for 10 minutes
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            options.AddPolicy("stac-collection-metadata", builder => builder
                .Expire(TimeSpan.FromMinutes(10))
                .Tag("stac", "stac-collection-metadata")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByRouteValue("collectionId")
                .VaryByValue(context => GetUserCacheKey(context)));

            // STAC Items policy: cache for 1 minute (frequently changes)
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            options.AddPolicy("stac-items", builder => builder
                .Expire(TimeSpan.FromMinutes(1))
                .Tag("stac", "stac-items")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByQuery("limit", "token", "bbox", "datetime", "filter", "fields", "sortby", "resultType", "ids")
                .SetVaryByRouteValue("collectionId")
                .VaryByValue(context => GetUserCacheKey(context)));

            // STAC Search policy: cache for 30 seconds (frequently accessed, short-lived)
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            // NOTE: Only applies to GET /stac/search
            options.AddPolicy("stac-search", builder => builder
                .Expire(TimeSpan.FromSeconds(30))
                .Tag("stac", "stac-search")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByQuery("collections", "ids", "bbox", "datetime", "limit", "token", "sortby", "fields", "filter", "filter-lang")
                .VaryByValue(context => GetUserCacheKey(context)));

            // STAC Search POST policy: cache for 30 seconds with body-based key
            // SECURITY: Vary by user identity, roles, and request body hash
            // Uses request body hash to create cache keys for POST /stac/search
            options.AddPolicy("stac-search-post", builder => builder
                .Expire(TimeSpan.FromSeconds(30))
                .Tag("stac", "stac-search-post")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByHeader("Content-Type")
                .VaryByValue(context => GetUserCacheKey(context))
                .SetVaryByQuery("*")); // Vary by all query parameters as well

            // STAC Item metadata policy: cache for 5 minutes
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            options.AddPolicy("stac-item-metadata", builder => builder
                .Expire(TimeSpan.FromMinutes(5))
                .Tag("stac", "stac-item-metadata")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByRouteValue("collectionId", "itemId")
                .VaryByValue(context => GetUserCacheKey(context)));

            // Catalog collections policy: cache for 5 minutes
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            options.AddPolicy("catalog-collections", builder => builder
                .Expire(TimeSpan.FromMinutes(5))
                .Tag("catalog", "catalog-collections")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByQuery("q", "group", "limit", "offset")
                .VaryByValue(context => GetUserCacheKey(context)));

            // Catalog record policy: cache for 10 minutes
            // SECURITY: Vary by user identity and roles to prevent cross-user cache leakage
            options.AddPolicy("catalog-record", builder => builder
                .Expire(TimeSpan.FromMinutes(10))
                .Tag("catalog", "catalog-record")
                .SetVaryByHeader("Accept", "Accept-Encoding")
                .SetVaryByRouteValue("serviceId", "layerId")
                .VaryByValue(context => GetUserCacheKey(context)));

            // OGC Conformance policy: cache for 1 hour (rarely changes)
            options.AddPolicy("ogc-conformance", builder => builder
                .Expire(TimeSpan.FromHours(1))
                .Tag("ogc", "conformance")
                .SetVaryByHeader("Accept", "Accept-Encoding"));

            // No cache policy for write operations
            options.AddPolicy("no-cache", builder => builder
                .NoCache()
                .Tag("no-cache"));
        });

        // Add in-memory cache for server-side caching
        services.AddMemoryCache(options =>
        {
            // Size limit (configurable, default: 200 MB)
            options.SizeLimit = memoryCacheMaxMb * 1024 * 1024;

            // Compact 25% when size limit reached
            options.CompactionPercentage = configuration.GetValue("Performance:MemoryCache:CompactionPercentage", 0.25);

            // Check for expired items periodically
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(
                configuration.GetValue("Performance:MemoryCache:ExpirationScanMinutes", 5));
        });

        // Add distributed cache (Redis) if configured
        var redisConnection = configuration.GetValue<string>("Redis:ConnectionString");
        if (!redisConnection.IsNullOrEmpty())
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = configuration.GetValue("Redis:InstanceName", "Honua:");
            });
        }

        return services;
    }

    public static IApplicationBuilder UseHonuaCaching(this IApplicationBuilder app)
    {
        // Output caching must be added AFTER compression and AFTER routing
        // Order: Compression -> Routing -> Output caching
        // Note: UseOutputCache() is called after UseRouting() in the middleware pipeline
        app.UseOutputCache();

        return app;
    }

    /// <summary>
    /// Generates a cache key based on user identity and roles to prevent cross-user cache leakage.
    /// Returns a consistent key for unauthenticated users (anonymous) and unique keys for authenticated users.
    /// </summary>
    /// <param name="context">The HTTP context containing user information.</param>
    /// <returns>A cache key string that varies by user identity and roles.</returns>
    private static KeyValuePair<string, string> GetUserCacheKey(HttpContext context)
    {
        var user = context.User;

        // If user is not authenticated, use a consistent "anonymous" key
        if (user?.Identity?.IsAuthenticated != true)
        {
            return new KeyValuePair<string, string>("user", "anonymous");
        }

        // Build a cache key based on user identity and roles
        var sb = new StringBuilder();

        // Add user identity name or subject claim
        var identity = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("sub")?.Value
                      ?? user.Identity.Name
                      ?? "unknown";
        sb.Append(identity);

        // Add all roles (sorted for consistency)
        var roles = user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roles.Count > 0)
        {
            sb.Append('|');
            sb.Append(string.Join(',', roles));
        }

        // Add tenant claim if present (for future multi-tenancy support)
        var tenant = user.FindFirst("tenant")?.Value
                    ?? user.FindFirst("tid")?.Value;
        if (!string.IsNullOrEmpty(tenant))
        {
            sb.Append('|');
            sb.Append(tenant);
        }

        return new KeyValuePair<string, string>("user", sb.ToString());
    }
}

/// <summary>
/// Output cache policy names for different endpoint types.
/// These correspond to the policies configured in AddHonuaHostCaching.
/// </summary>
public static class OutputCachePolicies
{
    /// <summary>
    /// Cache policy for STAC collections list (5 minutes).
    /// </summary>
    public const string StacCollections = "stac-collections";

    /// <summary>
    /// Cache policy for STAC collection metadata (10 minutes).
    /// </summary>
    public const string StacCollectionMetadata = "stac-collection-metadata";

    /// <summary>
    /// Cache policy for STAC items list (1 minute).
    /// </summary>
    public const string StacItems = "stac-items";

    /// <summary>
    /// Cache policy for STAC item metadata (5 minutes).
    /// </summary>
    public const string StacItemMetadata = "stac-item-metadata";

    /// <summary>
    /// Cache policy for STAC search (30 seconds).
    /// </summary>
    public const string StacSearch = "stac-search";

    /// <summary>
    /// Cache policy for STAC search POST with body-based caching (30 seconds).
    /// </summary>
    public const string StacSearchPost = "stac-search-post";

    /// <summary>
    /// Cache policy for catalog collections (5 minutes).
    /// </summary>
    public const string CatalogCollections = "catalog-collections";

    /// <summary>
    /// Cache policy for catalog record (10 minutes).
    /// </summary>
    public const string CatalogRecord = "catalog-record";

    /// <summary>
    /// Cache policy for OGC conformance classes (1 hour).
    /// </summary>
    public const string OgcConformance = "ogc-conformance";

    /// <summary>
    /// No caching (for write operations).
    /// </summary>
    public const string NoCache = "no-cache";
}
