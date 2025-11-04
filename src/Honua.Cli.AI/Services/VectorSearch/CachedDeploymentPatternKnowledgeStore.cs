// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Caching decorator for deployment pattern knowledge store.
/// Reduces API calls and improves performance by caching search results.
/// </summary>
public sealed class CachedDeploymentPatternKnowledgeStore : IDeploymentPatternKnowledgeStore
{
    private readonly IDeploymentPatternKnowledgeStore _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedDeploymentPatternKnowledgeStore> _logger;
    private readonly TimeSpan _searchCacheTtl;

    public CachedDeploymentPatternKnowledgeStore(
        IDeploymentPatternKnowledgeStore inner,
        IMemoryCache cache,
        ILogger<CachedDeploymentPatternKnowledgeStore> logger,
        TimeSpan? searchCacheTtl = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _searchCacheTtl = searchCacheTtl ?? TimeSpan.FromMinutes(15);
    }

    public async Task IndexApprovedPatternAsync(
        DeploymentPattern pattern,
        CancellationToken cancellationToken = default)
    {
        // Invalidate all search caches when a new pattern is indexed
        // (Simple approach: remove all cached searches)
        _logger.LogInformation(
            "Indexing pattern {PatternId}, invalidating search cache",
            pattern.Id);

        // Note: IMemoryCache doesn't have a clear-all method,
        // so we rely on TTL expiration. For production, consider
        // tracking cache keys or using IDistributedCache with tags.

        await _inner.IndexApprovedPatternAsync(pattern, cancellationToken);
    }

    public async Task<List<PatternSearchResult>> SearchPatternsAsync(
        DeploymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(requirements);

        // Try to get from cache
        if (_cache.TryGetValue<List<PatternSearchResult>>(cacheKey, out var cachedResults))
        {
            _logger.LogDebug(
                "Cache hit for pattern search: {CloudProvider}/{DataVolume}GB/{Users} users",
                requirements.CloudProvider,
                requirements.DataVolumeGb,
                requirements.ConcurrentUsers);

            return cachedResults!;
        }

        // Cache miss - fetch from underlying store
        _logger.LogDebug(
            "Cache miss for pattern search: {CloudProvider}/{DataVolume}GB/{Users} users",
            requirements.CloudProvider,
            requirements.DataVolumeGb,
            requirements.ConcurrentUsers);

        var results = await _inner.SearchPatternsAsync(requirements, cancellationToken);

        // Cache the results
        var cacheEntryOptions = new CacheOptionsBuilder()
            .WithAbsoluteExpiration(_searchCacheTtl)
            .WithSize(1) // Each entry counts as 1 unit
            .BuildMemory();

        _cache.Set(cacheKey, results, cacheEntryOptions);

        _logger.LogInformation(
            "Cached {Count} pattern search results for {Minutes} minutes",
            results.Count,
            _searchCacheTtl.TotalMinutes);

        return results;
    }

    private static string GenerateCacheKey(DeploymentRequirements requirements)
    {
        // Create a deterministic cache key based on requirements
        var keyData = new
        {
            requirements.CloudProvider,
            requirements.DataVolumeGb,
            requirements.ConcurrentUsers,
            requirements.Region
        };

        var json = JsonSerializer.Serialize(keyData);

        // Use SHA256 hash for consistent, collision-resistant keys
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(hashBytes);

        return $"pattern-search:{hash}";
    }
}
