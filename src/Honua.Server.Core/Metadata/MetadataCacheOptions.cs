// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Configuration options for metadata caching with Redis.
/// </summary>
public sealed class MetadataCacheOptions
{
    /// <summary>
    /// Cache key prefix for namespacing. Default: "honua:metadata:"
    /// </summary>
    public string KeyPrefix { get; set; } = "honua:metadata:";

    /// <summary>
    /// Time-to-live for cached metadata snapshots. Default: 5 minutes.
    /// Set to null or TimeSpan.Zero to disable expiration.
    /// </summary>
    public TimeSpan? Ttl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Enable cache warming on startup. Default: true.
    /// </summary>
    public bool WarmCacheOnStartup { get; set; } = true;

    /// <summary>
    /// Cache schema version for backward compatibility.
    /// Increment when making breaking changes to serialized format.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Enable fallback to disk when Redis is unavailable. Default: true.
    /// </summary>
    public bool FallbackToDiskOnFailure { get; set; } = true;

    /// <summary>
    /// Enable cache metrics collection. Default: true.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Timeout for Redis operations. Default: 5 seconds.
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Enable compression for cached metadata. Default: true.
    /// Uses GZip compression to reduce memory footprint.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets the versioned cache key for metadata snapshots.
    /// Format: {KeyPrefix}snapshot:v{SchemaVersion}
    /// </summary>
    public string GetSnapshotCacheKey() =>
        CacheKeyGenerator.GenerateMetadataKey("default", SchemaVersion, KeyPrefix);
}
