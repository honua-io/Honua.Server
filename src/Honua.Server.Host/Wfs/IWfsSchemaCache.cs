// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Provides caching for WFS DescribeFeatureType schema information.
/// </summary>
/// <remarks>
/// <para>
/// WFS DescribeFeatureType operations generate XML Schema documents describing feature types.
/// These schemas are derived from database metadata and field definitions, requiring queries
/// to resolve field types and structure. Since this metadata changes infrequently, caching
/// significantly improves performance for repeated GetFeature operations.
/// </para>
/// <para>
/// <strong>Cache Strategy:</strong>
/// <list type="bullet">
/// <item>Schemas are cached per collection (layer) with version/timestamp tracking</item>
/// <item>Cache keys include collection ID and schema version for precise invalidation</item>
/// <item>Automatic expiration via TTL to handle metadata updates</item>
/// <item>Manual invalidation for schema modifications and collection deletions</item>
/// </list>
/// </para>
/// </remarks>
public interface IWfsSchemaCache
{
    /// <summary>
    /// Attempts to retrieve a cached schema for a feature type.
    /// </summary>
    /// <param name="collectionId">The collection (layer) identifier.</param>
    /// <param name="schema">The cached schema document, or null if not found.</param>
    /// <returns>True if the schema was found in cache, otherwise false.</returns>
    bool TryGetSchema(string collectionId, out XDocument? schema);

    /// <summary>
    /// Stores a schema document in the cache.
    /// </summary>
    /// <param name="collectionId">The collection (layer) identifier.</param>
    /// <param name="schema">The schema document to cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetSchemaAsync(string collectionId, XDocument schema, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached schema for a specific collection.
    /// </summary>
    /// <param name="collectionId">The collection (layer) identifier.</param>
    /// <remarks>
    /// Called when a collection's schema is modified or the collection is deleted.
    /// </remarks>
    void InvalidateSchema(string collectionId);

    /// <summary>
    /// Invalidates all cached schemas.
    /// </summary>
    /// <remarks>
    /// Called during administrative schema updates or when metadata is reloaded.
    /// </remarks>
    void InvalidateAll();

    /// <summary>
    /// Gets cache statistics for monitoring and diagnostics.
    /// </summary>
    /// <returns>Cache statistics including hit rate and entry count.</returns>
    WfsSchemaCacheStatistics GetStatistics();
}

/// <summary>
/// Statistics for WFS schema cache monitoring.
/// </summary>
public sealed record WfsSchemaCacheStatistics
{
    /// <summary>
    /// Total number of cache hits.
    /// </summary>
    public long Hits { get; init; }

    /// <summary>
    /// Total number of cache misses.
    /// </summary>
    public long Misses { get; init; }

    /// <summary>
    /// Total number of cache evictions.
    /// </summary>
    public long Evictions { get; init; }

    /// <summary>
    /// Number of schemas currently cached.
    /// </summary>
    public int EntryCount { get; init; }

    /// <summary>
    /// Maximum number of schemas allowed in cache.
    /// </summary>
    public int MaxEntries { get; init; }

    /// <summary>
    /// Cache hit rate (hits / total requests).
    /// </summary>
    public double HitRate { get; init; }
}
