// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Stac;

/// <summary>
/// Configuration options for STAC search operations.
/// </summary>
public sealed class StacSearchOptions
{
    /// <summary>
    /// Timeout for COUNT queries in seconds. Default is 5 seconds.
    /// If the COUNT query exceeds this timeout, an estimated count will be used instead.
    /// </summary>
    public int CountTimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// Whether to use count estimation when exact count query times out.
    /// Default is true.
    /// </summary>
    public bool UseCountEstimation { get; init; } = true;

    /// <summary>
    /// Maximum number of items for which exact count will be computed.
    /// For result sets larger than this threshold, estimation will be used.
    /// Set to -1 to always compute exact count (not recommended for large datasets).
    /// Default is 100,000.
    /// </summary>
    public int MaxExactCountThreshold { get; init; } = 100_000;

    /// <summary>
    /// Whether to skip count computation entirely for large result sets.
    /// When true, searches with limits above a certain threshold will return -1 for count.
    /// This improves performance when clients don't need exact counts.
    /// Default is true.
    /// </summary>
    public bool SkipCountForLargeResultSets { get; init; } = true;

    /// <summary>
    /// The limit threshold above which counts are skipped (if SkipCountForLargeResultSets is enabled).
    /// Default is 1000.
    /// </summary>
    public int SkipCountLimitThreshold { get; init; } = 1000;

    /// <summary>
    /// Page size for streaming search operations.
    /// Controls how many items are fetched from the database in each batch.
    /// Default is 100.
    /// </summary>
    public int StreamingPageSize { get; init; } = 100;

    /// <summary>
    /// Maximum number of items to stream in a single search.
    /// Prevents unbounded streams that could impact system resources.
    /// Set to -1 for unlimited streaming. Default is 100,000.
    /// </summary>
    public int MaxStreamingItems { get; init; } = 100_000;

    /// <summary>
    /// Whether to enable streaming for large result sets automatically.
    /// When enabled, searches exceeding StreamingThreshold will use streaming.
    /// Default is true.
    /// </summary>
    public bool EnableAutoStreaming { get; init; } = true;

    /// <summary>
    /// The threshold above which automatic streaming is enabled.
    /// Only applies when EnableAutoStreaming is true.
    /// Default is 1000.
    /// </summary>
    public int StreamingThreshold { get; init; } = 1000;
}
