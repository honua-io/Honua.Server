// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Locking;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// Stores and retrieves kerchunk references with caching and distributed locking.
/// Implements cache-aside pattern with lazy generation.
/// </summary>
/// <remarks>
/// <para><b>Distributed Locking:</b></para>
/// <list type="bullet">
/// <item>Uses Redis-based distributed locks when Redis is configured (recommended for production clusters)</item>
/// <item>Falls back to in-memory locks when Redis is unavailable (single-instance deployments)</item>
/// <item>Prevents cache stampede where multiple instances regenerate the same metadata concurrently</item>
/// <item>Configurable via RasterCacheConfiguration.EnableDistributedLocking</item>
/// </list>
///
/// <para><b>Lock Behavior:</b></para>
/// <list type="bullet">
/// <item>Lock key format: "kerchunk:lock:{zarrUrl}" for global coordination</item>
/// <item>Automatic lock expiry prevents deadlocks if a process crashes</item>
/// <item>Double-check pattern: verifies cache after acquiring lock (another instance may have generated it)</item>
/// </list>
///
/// <para><b>Configuration:</b></para>
/// <list type="bullet">
/// <item>Set EnableDistributedLocking=true with Redis connection string for multi-instance deployments</item>
/// <item>Set EnableDistributedLocking=false for single-instance deployments (uses in-memory locks)</item>
/// <item>Adjust DistributedLockTimeout and DistributedLockExpiry based on dataset size</item>
/// </list>
/// </remarks>
public sealed class KerchunkReferenceStore : IKerchunkReferenceStore
{
    private readonly IKerchunkGenerator _generator;
    private readonly IKerchunkCacheProvider _cacheProvider;
    private readonly IDistributedLock _distributedLock;
    private readonly ILogger<KerchunkReferenceStore> _logger;
    private readonly RasterCacheConfiguration _config;

    // Fallback in-memory locks for backward compatibility when distributed locking is disabled
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fallbackLocks = new();

    private readonly TimeSpan _generationTimeout;

    public KerchunkReferenceStore(
        IKerchunkGenerator generator,
        IKerchunkCacheProvider cacheProvider,
        IDistributedLock distributedLock,
        IOptions<HonuaConfiguration> options,
        ILogger<KerchunkReferenceStore> logger)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
        _distributedLock = distributedLock ?? throw new ArgumentNullException(nameof(distributedLock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var honuaConfig = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _config = honuaConfig.RasterCache;
        _generationTimeout = _config.DistributedLockTimeout;

        if (_config.EnableDistributedLocking)
        {
            _logger.LogInformation(
                "Kerchunk reference store using distributed locking (LockTimeout={LockTimeout}, LockExpiry={LockExpiry})",
                _config.DistributedLockTimeout,
                _config.DistributedLockExpiry);
        }
        else
        {
            _logger.LogWarning(
                "Kerchunk reference store using in-memory locking only. " +
                "Multi-instance deployments may experience cache stampede. " +
                "Enable distributed locking in configuration for production clusters.");
        }
    }

    public async Task<KerchunkReferences> GetOrGenerateAsync(
        string sourceUri,
        KerchunkGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(sourceUri, options);

        // Fast path: Check cache first
        var cached = await _cacheProvider.GetAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogDebug("Kerchunk references retrieved from cache for {SourceUri}", sourceUri);
            return cached;
        }

        // Slow path: Generate with distributed locking to prevent duplicate work across instances
        if (_config.EnableDistributedLocking)
        {
            return await GenerateWithDistributedLockAsync(sourceUri, cacheKey, options, cancellationToken);
        }
        else
        {
            return await GenerateWithInMemoryLockAsync(sourceUri, cacheKey, options, cancellationToken);
        }
    }

    private async Task<KerchunkReferences> GenerateWithDistributedLockAsync(
        string sourceUri,
        string cacheKey,
        KerchunkGenerationOptions options,
        CancellationToken cancellationToken)
    {
        var lockKey = $"kerchunk:lock:{cacheKey}";

        _logger.LogDebug(
            "Attempting to acquire distributed lock for kerchunk generation: SourceUri={SourceUri}, LockKey={LockKey}",
            sourceUri, lockKey);

        await using var lockHandle = await _distributedLock.TryAcquireAsync(
            lockKey,
            _config.DistributedLockTimeout,
            _config.DistributedLockExpiry,
            cancellationToken);

        if (lockHandle == null)
        {
            _logger.LogWarning(
                "Timeout waiting for distributed lock for kerchunk generation: SourceUri={SourceUri}, Timeout={Timeout}. " +
                "This may indicate slow generation or contention across multiple instances.",
                sourceUri,
                _config.DistributedLockTimeout);

            throw new TimeoutException(
                $"Timeout waiting for distributed lock for kerchunk generation: {sourceUri}. " +
                $"Generation may be taking longer than {_config.DistributedLockTimeout.TotalMinutes} minutes.");
        }

        _logger.LogDebug(
            "Distributed lock acquired for kerchunk generation: SourceUri={SourceUri}, AcquiredAt={AcquiredAt}",
            sourceUri, lockHandle.AcquiredAt);

        // Double-check cache after acquiring lock (another instance might have generated it)
        var cached = await _cacheProvider.GetAsync(cacheKey, cancellationToken);
        if (cached != null)
        {
            _logger.LogInformation(
                "Kerchunk references found in cache after acquiring distributed lock (generated by another instance): SourceUri={SourceUri}",
                sourceUri);
            return cached;
        }

        // Generate references
        _logger.LogInformation(
            "Generating kerchunk references with distributed lock: SourceUri={SourceUri}",
            sourceUri);

        var refs = await _generator.GenerateAsync(sourceUri, options, cancellationToken);

        // Cache the result
        await _cacheProvider.SetAsync(cacheKey, refs, ttl: null, cancellationToken);

        _logger.LogInformation(
            "Kerchunk references generated and cached successfully: SourceUri={SourceUri}",
            sourceUri);

        return refs;
    }

    private async Task<KerchunkReferences> GenerateWithInMemoryLockAsync(
        string sourceUri,
        string cacheKey,
        KerchunkGenerationOptions options,
        CancellationToken cancellationToken)
    {
        var lockObj = _fallbackLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        try
        {
            // Wait for lock with timeout
            var acquired = await lockObj.WaitAsync(_generationTimeout, cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning(
                    "Timeout waiting for in-memory kerchunk generation lock for {SourceUri}",
                    sourceUri);

                throw new TimeoutException(
                    $"Timeout waiting for in-memory kerchunk generation lock for {sourceUri}. " +
                    $"Generation may be taking longer than {_generationTimeout.TotalMinutes} minutes.");
            }

            try
            {
                // Double-check cache after acquiring lock (another thread might have generated it)
                var cached = await _cacheProvider.GetAsync(cacheKey, cancellationToken);
                if (cached != null)
                {
                    _logger.LogDebug("Kerchunk references found in cache after acquiring in-memory lock for {SourceUri}", sourceUri);
                    return cached;
                }

                // Generate references
                _logger.LogInformation("Generating kerchunk references for {SourceUri} (cache miss)", sourceUri);

                var refs = await _generator.GenerateAsync(sourceUri, options, cancellationToken);

                // Cache the result
                await _cacheProvider.SetAsync(cacheKey, refs, ttl: null, cancellationToken);

                return refs;
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or generate kerchunk references for {SourceUri}", sourceUri);
            throw;
        }
    }

    public async Task<KerchunkReferences> GenerateAsync(
        string sourceUri,
        KerchunkGenerationOptions options,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(sourceUri, options);

        if (!force)
        {
            // Check cache first
            var cached = await _cacheProvider.GetAsync(cacheKey, cancellationToken);
            if (cached != null)
            {
                _logger.LogInformation("Kerchunk references already exist for {SourceUri}, use force=true to regenerate", sourceUri);
                return cached;
            }
        }

        // Generate with distributed or in-memory locking
        if (_config.EnableDistributedLocking)
        {
            return await ForceGenerateWithDistributedLockAsync(sourceUri, cacheKey, options, force, cancellationToken);
        }
        else
        {
            return await ForceGenerateWithInMemoryLockAsync(sourceUri, cacheKey, options, force, cancellationToken);
        }
    }

    private async Task<KerchunkReferences> ForceGenerateWithDistributedLockAsync(
        string sourceUri,
        string cacheKey,
        KerchunkGenerationOptions options,
        bool force,
        CancellationToken cancellationToken)
    {
        var lockKey = $"kerchunk:lock:{cacheKey}";

        await using var lockHandle = await _distributedLock.TryAcquireAsync(
            lockKey,
            _config.DistributedLockTimeout,
            _config.DistributedLockExpiry,
            cancellationToken);

        if (lockHandle == null)
        {
            throw new TimeoutException(
                $"Timeout waiting for distributed lock for kerchunk generation: {sourceUri}");
        }

        if (force)
        {
            _logger.LogInformation("Force regenerating kerchunk references with distributed lock: SourceUri={SourceUri}", sourceUri);
        }
        else
        {
            _logger.LogInformation("Generating kerchunk references with distributed lock: SourceUri={SourceUri}", sourceUri);
        }

        var refs = await _generator.GenerateAsync(sourceUri, options, cancellationToken);

        // Cache the result
        await _cacheProvider.SetAsync(cacheKey, refs, ttl: null, cancellationToken);

        _logger.LogInformation("Successfully generated and cached kerchunk references: SourceUri={SourceUri}", sourceUri);

        return refs;
    }

    private async Task<KerchunkReferences> ForceGenerateWithInMemoryLockAsync(
        string sourceUri,
        string cacheKey,
        KerchunkGenerationOptions options,
        bool force,
        CancellationToken cancellationToken)
    {
        var lockObj = _fallbackLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        try
        {
            var acquired = await lockObj.WaitAsync(_generationTimeout, cancellationToken);
            if (!acquired)
            {
                throw new TimeoutException(
                    $"Timeout waiting for in-memory kerchunk generation lock for {sourceUri}");
            }

            try
            {
                if (force)
                {
                    _logger.LogInformation("Force regenerating kerchunk references: SourceUri={SourceUri}", sourceUri);
                }
                else
                {
                    _logger.LogInformation("Generating kerchunk references: SourceUri={SourceUri}", sourceUri);
                }

                var refs = await _generator.GenerateAsync(sourceUri, options, cancellationToken);

                // Cache the result
                await _cacheProvider.SetAsync(cacheKey, refs, ttl: null, cancellationToken);

                _logger.LogInformation("Successfully generated and cached kerchunk references: SourceUri={SourceUri}", sourceUri);

                return refs;
            }
            finally
            {
                lockObj.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate kerchunk references for {SourceUri}", sourceUri);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string sourceUri, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(sourceUri, new KerchunkGenerationOptions());
        return await _cacheProvider.ExistsAsync(cacheKey, cancellationToken);
    }

    public async Task DeleteAsync(string sourceUri, CancellationToken cancellationToken = default)
    {
        var cacheKey = GenerateCacheKey(sourceUri, new KerchunkGenerationOptions());

        try
        {
            await _cacheProvider.DeleteAsync(cacheKey, cancellationToken);
            _logger.LogInformation("Deleted kerchunk references for {SourceUri}", sourceUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete kerchunk references for {SourceUri}", sourceUri);
            throw;
        }
    }

    /// <summary>
    /// Generates a deterministic cache key from source URI and options.
    /// Uses SHA256 hash to ensure consistent key length and avoid filesystem/S3 key issues.
    /// </summary>
    private static string GenerateCacheKey(string sourceUri, KerchunkGenerationOptions options)
    {
        return CacheKeyGenerator.GenerateKerchunkKey(
            sourceUri,
            options.Variables,
            options.IncludeCoordinates,
            options.ConsolidateMetadata);
    }
}
