// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Configuration options for WFS (Web Feature Service) protocol implementation.
/// Controls pagination, query limits, caching, and DoS protection parameters.
/// </summary>
public sealed class WfsOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "honua:wfs";

    /// <summary>
    /// Gets or sets the cache duration for GetCapabilities responses in seconds.
    /// Default is 3600 seconds (1 hour).
    /// </summary>
    [Range(0, 86400)]
    public int CapabilitiesCacheDuration { get; set; } = 3600;

    /// <summary>
    /// Gets or sets the cache duration for DescribeFeatureType responses in seconds.
    /// Default is 86400 seconds (24 hours).
    /// This applies to both HTTP caching (Cache-Control headers) and server-side schema caching.
    /// </summary>
    [Range(0, 86400)]
    public int DescribeFeatureTypeCacheDuration { get; set; } = 86400;

    /// <summary>
    /// Gets or sets whether HTTP caching is enabled for WFS operations.
    /// Default is true.
    /// </summary>
    public bool CachingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether server-side schema caching is enabled for WFS DescribeFeatureType.
    /// When enabled, parsed XML Schema documents are cached in memory to avoid repeated
    /// database queries for field metadata. Default is true.
    /// </summary>
    /// <remarks>
    /// Schema caching significantly improves performance for repeated GetFeature operations
    /// that reference schema information. Schemas are invalidated automatically on TTL expiration
    /// or manually when collections are modified or deleted.
    /// </remarks>
    public bool EnableSchemaCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of schemas to cache in memory.
    /// Default is 1000 schemas. Set to 0 for unlimited (not recommended).
    /// </summary>
    /// <remarks>
    /// Each cached schema is approximately 1-10 KB depending on field count.
    /// A limit of 1000 schemas consumes approximately 1-10 MB of memory.
    /// </remarks>
    [Range(0, 10_000)]
    public int MaxCachedSchemas { get; set; } = 1_000;

    /// <summary>
    /// Default number of features to return when the count parameter is not specified.
    /// This applies to WFS GetFeature and GetPropertyValue requests.
    /// </summary>
    [Range(1, 10_000)]
    public int DefaultCount { get; set; } = 100;

    /// <summary>
    /// Maximum number of features that can be requested in a single GetFeature operation.
    /// This is a critical security parameter to prevent DoS attacks through excessive data requests.
    /// Individual requests exceeding this limit will be rejected with an OGC exception.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxFeatures { get; set; } = 10_000;

    /// <summary>
    /// Enable filter complexity checking to prevent DoS attacks through complex queries.
    /// When enabled, filters exceeding MaxFilterComplexity will be rejected.
    /// </summary>
    public bool EnableComplexityCheck { get; set; } = true;

    /// <summary>
    /// Maximum allowed complexity score for CQL and XML filters.
    /// Complexity is calculated based on number of predicates, spatial operations, and nesting depth.
    /// Filters exceeding this score will be rejected to prevent expensive queries.
    /// </summary>
    [Range(1, 10_000)]
    public int MaxFilterComplexity { get; set; } = 1_000;

    /// <summary>
    /// Maximum number of features allowed in a single WFS Transaction request.
    /// This prevents memory exhaustion from large batch inserts/updates/deletes.
    /// Transactions exceeding this limit will be rejected with an OGC exception.
    /// </summary>
    [Range(1, 100_000)]
    public int MaxTransactionFeatures { get; set; } = 5_000;

    /// <summary>
    /// Batch size for processing large WFS Transaction operations.
    /// Features will be processed in batches of this size to prevent memory exhaustion.
    /// Smaller values reduce memory usage but increase processing overhead.
    /// </summary>
    [Range(10, 10_000)]
    public int TransactionBatchSize { get; set; } = 500;

    /// <summary>
    /// Timeout in seconds for WFS Transaction operations.
    /// Large transactions may take significant time to process.
    /// Default is 300 seconds (5 minutes).
    /// </summary>
    [Range(10, 3600)]
    public int TransactionTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Enable streaming XML parsing for WFS Transaction requests.
    /// When enabled, large transaction payloads are parsed incrementally using XmlReader
    /// instead of loading the entire document into memory as XDocument.
    /// Recommended for production environments handling large transactions.
    /// </summary>
    public bool EnableStreamingTransactionParser { get; set; } = true;
}
