// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features.Strategies;

/// <summary>
/// Adaptive search service that falls back to simpler search strategies.
/// </summary>
public sealed class AdaptiveSearchService
{
    private readonly AdaptiveFeatureService _adaptiveFeature;
    private readonly ILogger<AdaptiveSearchService> _logger;

    public AdaptiveSearchService(
        AdaptiveFeatureService adaptiveFeature,
        ILogger<AdaptiveSearchService> logger)
    {
        _adaptiveFeature = adaptiveFeature ?? throw new ArgumentNullException(nameof(adaptiveFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Performs a search using the best available strategy.
    /// </summary>
    public async Task<SearchResults> SearchAsync(
        string query,
        Func<string, CancellationToken, Task<SearchResults>> fullTextSearchFunc,
        Func<string, CancellationToken, Task<SearchResults>> basicSearchFunc,
        Func<string, CancellationToken, Task<SearchResults>> databaseScanFunc,
        CancellationToken cancellationToken = default)
    {
        var strategy = await _adaptiveFeature.GetSearchStrategyAsync(cancellationToken);

        switch (strategy)
        {
            case SearchStrategy.FullTextSearch:
                try
                {
                    _logger.LogDebug("Using full-text search for query: {Query}", query);
                    return await fullTextSearchFunc(query, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Full-text search failed for query {Query}, falling back to basic search",
                        query);

                    // Fall through to basic search
                    goto case SearchStrategy.BasicIndex;
                }

            case SearchStrategy.BasicIndex:
                try
                {
                    _logger.LogDebug("Using basic index search for query: {Query}", query);
                    return await basicSearchFunc(query, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Basic search failed for query {Query}, falling back to database scan",
                        query);

                    // Fall through to database scan
                    goto case SearchStrategy.DatabaseScan;
                }

            case SearchStrategy.DatabaseScan:
                _logger.LogWarning(
                    "Using database scan for query {Query} - this may be slow",
                    query);

                return await databaseScanFunc(query, cancellationToken);

            default:
                throw new InvalidOperationException($"Unknown search strategy: {strategy}");
        }
    }
}

/// <summary>
/// Search results with degradation information.
/// </summary>
public sealed class SearchResults
{
    public required List<SearchResult> Results { get; init; }
    public int TotalCount { get; init; }
    public required string SearchStrategy { get; init; }
    public bool IsDegraded { get; init; }
    public string? Warning { get; init; }
    public TimeSpan ExecutionTime { get; init; }
}

/// <summary>
/// Individual search result.
/// </summary>
public sealed class SearchResult
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public double Relevance { get; init; }
}
