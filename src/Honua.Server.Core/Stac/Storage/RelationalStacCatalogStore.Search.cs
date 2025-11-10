// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;
namespace Honua.Server.Core.Stac.Storage;

/// <summary>
/// Partial class containing STAC search operations including optimized counting, sorting, and streaming.
/// </summary>
internal abstract partial class RelationalStacCatalogStore
{
    public async Task<StacSearchResult> SearchAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(parameters);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await OperationInstrumentation.Create<StacSearchResult>("SearchItems")
            .WithActivitySource(StacMetrics.ActivitySource)
            .WithLogger(Logger)
            .WithMetrics(StacMetrics.SearchCount, StacMetrics.SearchCount, StacMetrics.SearchDuration)
            .WithTag("provider", ProviderName)
            .WithTag("collections", parameters.Collections?.Count ?? 0)
            .WithTag("limit", parameters.Limit)
            .WithTag("has_bbox", parameters.Bbox is { Length: > 0 })
            .WithTag("has_collections", parameters.Collections is { Count: > 0 })
            .ExecuteAsync(async activity =>
            {
                var limit = parameters.Limit > 0 ? parameters.Limit : 10;
                limit = Math.Min(limit, 1000);

                await using var connection = CreateConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                // For databases that don't support bbox filtering at the SQL level,
                // we need to fetch more items and filter client-side, then continue fetching if needed
                var needsClientSideBboxFiltering = parameters.Bbox is { Length: > 0 } && !SupportsBboxFiltering;
                var items = new List<StacItemRecord>();
                string? nextToken = null;
                int matched;

                if (needsClientSideBboxFiltering)
                {
                    // When client-side filtering is needed, we must count by fetching and filtering all items
                    // This is unavoidable without database-level bbox support
                    // Note: This is currently dead code since all implementations support bbox filtering,
                    // but we keep it for completeness and future database providers

                    var allItems = new List<StacItemRecord>();
                    var paramsWithoutBbox = new StacSearchParameters
                    {
                        Collections = parameters.Collections,
                        Ids = parameters.Ids,
                        Start = parameters.Start,
                        End = parameters.End,
                        Bbox = null, // Exclude bbox from SQL
                        Limit = 0, // No limit for counting
                        Token = null
                    };

                    await using (var command = connection.CreateCommand())
                    {
                        var filter = BuildSearchFilter(command, paramsWithoutBbox, includePagination: false);
                        var orderBy = BuildOrderByClause(parameters.SortBy);
                        command.CommandText = $"select collection_id, id, title, description, properties_json, assets_json, links_json, extensions_json, bbox_json, geometry_json, datetime, start_datetime, end_datetime, raster_dataset_id, etag, created_at, updated_at\nfrom stac_items {filter}\n{orderBy}";

                        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var item = ReadItem(reader);
                            if (MatchesBbox(item, parameters.Bbox))
                            {
                                allItems.Add(item);
                            }
                        }
                    }

                    matched = allItems.Count;

                    // Apply pagination token
                    if (parameters.Token.HasValue())
                    {
                        var (tokenCollection, tokenItem) = ParseContinuationToken(parameters.Token);
                        if (tokenCollection is not null && tokenItem is not null)
                        {
                            allItems = allItems.Where(item =>
                                string.CompareOrdinal(item.CollectionId, tokenCollection) > 0 ||
                                (item.CollectionId == tokenCollection && string.CompareOrdinal(item.Id, tokenItem) > 0)
                            ).ToList();
                        }
                    }

                    // Apply limit and set next token
                    if (allItems.Count > limit)
                    {
                        var last = allItems[limit - 1];
                        nextToken = $"{last.CollectionId}:{last.Id}";
                        items = allItems.Take(limit).ToList();
                    }
                    else
                    {
                        items = allItems;
                    }
                }
                else
                {
                    // Database supports all filtering - use efficient single query with optimized count
                    matched = await GetOptimizedItemCountAsync(connection, parameters, cancellationToken).ConfigureAwait(false);

                    await using (var command = connection.CreateCommand())
                    {
                        var filter = BuildSearchFilter(command, parameters, includePagination: true);
                        var orderBy = BuildOrderByClause(parameters.SortBy);
                        command.CommandText = $"select collection_id, id, title, description, properties_json, assets_json, links_json, extensions_json, bbox_json, geometry_json, datetime, start_datetime, end_datetime, raster_dataset_id, etag, created_at, updated_at\nfrom stac_items {filter}\n{orderBy}\n{BuildLimitClause(limit + 1)}";

                        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            items.Add(ReadItem(reader));
                        }
                    }

                    if (items.Count > limit)
                    {
                        var last = items[limit - 1];
                        nextToken = $"{last.CollectionId}:{last.Id}";
                        items = items.Take(limit).ToList();
                    }
                }

                activity?.SetTag("matched_count", matched);
                activity?.SetTag("returned_count", items.Count);
                activity?.SetTag("has_next_token", nextToken != null);

                return new StacSearchResult
                {
                    Items = items,
                    Matched = matched,
                    NextToken = nextToken
                };
            });
    }


    /// <summary>
    /// Gets the item count with optimization strategies including timeout handling and estimation.
    /// Returns -1 when count is unknown or skipped.
    /// </summary>
    private async Task<int> GetOptimizedItemCountAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Strategy 1: Skip count for large result sets if configured
            if (SearchOptions.SkipCountForLargeResultSets && parameters.Limit > SearchOptions.SkipCountLimitThreshold)
            {
                Logger?.LogDebug(
                    "Skipping COUNT query for STAC search with limit {Limit} (threshold: {Threshold})",
                    parameters.Limit,
                    SearchOptions.SkipCountLimitThreshold);
                return -1;
            }

            // Strategy 2: Try exact count with timeout
            try
            {
                var count = await ExecuteCountWithTimeoutAsync(connection, parameters, cancellationToken).ConfigureAwait(false);

                StacMetrics.SearchCountDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("provider", ProviderName),
                    new KeyValuePair<string, object?>("method", "exact"));

                Logger?.LogDebug(
                    "STAC search COUNT query completed in {Duration}ms, count: {Count}",
                    stopwatch.Elapsed.TotalMilliseconds,
                    count);

                // Strategy 3: If exact count exceeds threshold, use estimation instead
                if (SearchOptions.UseCountEstimation && count > SearchOptions.MaxExactCountThreshold)
                {
                    Logger?.LogInformation(
                        "STAC search COUNT {Count} exceeds threshold {Threshold}, using estimation instead",
                        count,
                        SearchOptions.MaxExactCountThreshold);

                    var estimate = await EstimateCountAsync(connection, parameters, cancellationToken).ConfigureAwait(false);

                    StacMetrics.SearchCountEstimations.Add(1,
                        new KeyValuePair<string, object?>("provider", ProviderName),
                        new KeyValuePair<string, object?>("reason", "threshold_exceeded"));

                    Logger?.LogDebug("STAC search estimated count: {Estimate}", estimate);

                    return estimate;
                }

                return count;
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                Logger?.LogWarning(
                    ex,
                    "STAC search COUNT query timed out after {Timeout}s, falling back to estimation",
                    SearchOptions.CountTimeoutSeconds);

                StacMetrics.SearchCountTimeouts.Add(1,
                    new KeyValuePair<string, object?>("provider", ProviderName));

                // Strategy 4: Fall back to estimation on timeout
                if (SearchOptions.UseCountEstimation)
                {
                    var estimate = await EstimateCountAsync(connection, parameters, cancellationToken).ConfigureAwait(false);

                    StacMetrics.SearchCountDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("provider", ProviderName),
                        new KeyValuePair<string, object?>("method", "estimated"));

                    StacMetrics.SearchCountEstimations.Add(1,
                        new KeyValuePair<string, object?>("provider", ProviderName),
                        new KeyValuePair<string, object?>("reason", "timeout"));

                    Logger?.LogInformation("STAC search using estimated count: {Estimate}", estimate);

                    return estimate;
                }

                Logger?.LogWarning("STAC search COUNT estimation is disabled, returning unknown count (-1)");

                // If estimation is disabled, return -1 to indicate unknown count
                return -1;
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Unexpected error during STAC search COUNT query, returning unknown count (-1)");
            // On any unexpected error, return -1 to indicate unknown count
            return -1;
        }
    }

    /// <summary>
    /// Executes a COUNT query with the configured timeout.
    /// </summary>
    private async Task<int> ExecuteCountWithTimeoutAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = SearchOptions.CountTimeoutSeconds;

        var filter = BuildSearchFilter(command, parameters, includePagination: false);
        command.CommandText = $"select count(*) from stac_items {filter}";

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => Convert.ToInt32(result, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Estimates the count using database-specific table statistics.
    /// Override in derived classes for database-specific optimizations.
    /// </summary>
    protected virtual async Task<int> EstimateCountAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        // Default implementation: estimate based on table statistics
        // This works for databases without filters, but derived classes should override for better accuracy

        if (ProviderName.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
            ProviderName.Contains("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            return await EstimateCountPostgresAsync(connection, parameters, cancellationToken).ConfigureAwait(false);
        }

        if (ProviderName.Contains("MySQL", StringComparison.OrdinalIgnoreCase) ||
            ProviderName.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
        {
            return await EstimateCountMySqlAsync(connection, parameters, cancellationToken).ConfigureAwait(false);
        }

        if (ProviderName.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ||
            ProviderName.Contains("MSSQL", StringComparison.OrdinalIgnoreCase))
        {
            return await EstimateCountSqlServerAsync(connection, parameters, cancellationToken).ConfigureAwait(false);
        }

        // For unknown databases, return -1 to indicate unknown count
        return -1;
    }

    /// <summary>
    /// Estimates count for PostgreSQL using pg_class statistics.
    /// </summary>
    private async Task<int> EstimateCountPostgresAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        // If searching specific collections, sum their approximate row counts
        if (parameters.Collections is { Count: > 0 })
        {
            // Use pg_class to get approximate row count for the table
            // Then estimate proportion based on collections
            command.CommandText = @"
                SELECT COALESCE(reltuples::bigint, 0)
                FROM pg_class
                WHERE relname = 'stac_items'";

            var totalEstimate = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var total = totalEstimate switch
            {
                null => 0,
                int i => i,
                long l => (int)l,
                decimal d => (int)d,
                _ => Convert.ToInt32(totalEstimate, CultureInfo.InvariantCulture)
            };

            // If filtering by specific collections, estimate a proportion
            // This is a rough estimate - better to use exact count for small result sets
            if (total > 0 && parameters.Collections.Count > 0)
            {
                // Rough estimate: divide by number of collections in the system
                // This assumes roughly equal distribution
                var collectionFactor = (double)parameters.Collections.Count / Math.Max(1, total / 10000.0);
                return Math.Max(1, (int)(total * collectionFactor));
            }

            return total;
        }
        else
        {
            // No collection filter - return total table estimate
            command.CommandText = @"
                SELECT COALESCE(reltuples::bigint, 0)
                FROM pg_class
                WHERE relname = 'stac_items'";

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result switch
            {
                null => 0,
                int i => i,
                long l => (int)l,
                decimal d => (int)d,
                _ => Convert.ToInt32(result, CultureInfo.InvariantCulture)
            };
        }
    }

    /// <summary>
    /// Estimates count for MySQL using information_schema.
    /// </summary>
    private async Task<int> EstimateCountMySqlAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT TABLE_ROWS
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
            AND TABLE_NAME = 'stac_items'";

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => Convert.ToInt32(result, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Estimates count for SQL Server using sys.partitions.
    /// </summary>
    private async Task<int> EstimateCountSqlServerAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT SUM(rows)
            FROM sys.partitions
            WHERE object_id = OBJECT_ID('stac_items')
            AND index_id IN (0, 1)";

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => Convert.ToInt32(result, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Determines if an exception is a timeout exception.
    /// </summary>
    private static bool IsTimeoutException(Exception ex)
    {
        return ex is TimeoutException ||
               ex is DbException dbEx && (
                   dbEx.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                   dbEx.Message.Contains("time out", StringComparison.OrdinalIgnoreCase)
               );
    }

    private async Task<int> ExecuteCountAsync(DbConnection connection, StacSearchParameters parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var filter = BuildSearchFilter(command, parameters, includePagination: false);
        command.CommandText = $"select count(*) from stac_items {filter}";

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => Convert.ToInt32(result, CultureInfo.InvariantCulture)
        };
    }

    /// <summary>
    /// Builds the ORDER BY clause for STAC search queries.
    /// Supports sorting by item-level fields and JSON properties with database-specific syntax.
    /// </summary>
    /// <param name="sortFields">The sort fields to apply, or null for default sorting.</param>
    /// <returns>A complete ORDER BY clause including the "ORDER BY" keywords.</returns>
    protected virtual string BuildOrderByClause(IReadOnlyList<StacSortField>? sortFields)
    {
        if (sortFields == null || sortFields.Count == 0)
        {
            // Default sort order for stable pagination
            return "ORDER BY collection_id, id";
        }

        var orderByClauses = new List<string>();
        foreach (var sortField in sortFields)
        {
            var columnExpression = MapSortFieldToColumn(sortField.Field);
            if (columnExpression == null)
            {
                // Skip invalid fields (they should have been validated earlier)
                continue;
            }

            var direction = sortField.Direction == StacSortDirection.Descending ? "DESC" : "ASC";
            orderByClauses.Add($"{columnExpression} {direction}");
        }

        if (orderByClauses.Count == 0)
        {
            // Fall back to default if no valid fields
            return "ORDER BY collection_id, id";
        }

        // Always append collection_id and id for stable pagination
        if (!sortFields.Any(f => f.Field.Equals("collection", StringComparison.OrdinalIgnoreCase)))
        {
            orderByClauses.Add("collection_id ASC");
        }
        if (!sortFields.Any(f => f.Field.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            orderByClauses.Add("id ASC");
        }

        return $"ORDER BY {string.Join(", ", orderByClauses)}";
    }

    /// <summary>
    /// Maps a STAC sort field name to a database column expression.
    /// Override in derived classes for database-specific JSON property access.
    /// </summary>
    /// <param name="fieldName">The STAC field name (e.g., "datetime", "properties.cloud_cover").</param>
    /// <returns>The SQL column expression, or null if the field is not supported.</returns>
    protected virtual string? MapSortFieldToColumn(string fieldName)
    {
        var normalizedField = fieldName.ToLowerInvariant();

        // Map top-level item fields to database columns
        return normalizedField switch
        {
            "id" => "id",
            "collection" => "collection_id",
            "datetime" => "datetime",
            "created" => "created_at",
            "updated" => "updated_at",
            _ => MapPropertyFieldToColumn(normalizedField)
        };
    }

    /// <summary>
    /// Maps a property field to a database-specific JSON column expression.
    /// Base implementation returns null; override in derived classes for JSON support.
    /// </summary>
    /// <param name="propertyName">The property name (may include "properties." prefix).</param>
    /// <returns>The SQL expression to access the JSON property, or null if not supported.</returns>
    protected virtual string? MapPropertyFieldToColumn(string propertyName)
    {
        // Base implementation doesn't support JSON property sorting
        // Derived classes (PostgreSQL, MySQL, SQL Server, SQLite) should override this
        return null;
    }

    private string BuildSearchFilter(DbCommand command, StacSearchParameters parameters, bool includePagination)
    {
        var clauses = new List<string>();

        if (parameters.Collections is { Count: > 0 })
        {
            var collectionNames = new List<string>();
            for (var index = 0; index < parameters.Collections.Count; index++)
            {
                var paramName = $"@collection{index}";
                collectionNames.Add(paramName);
                AddParameter(command, paramName, parameters.Collections[index]);
            }

            clauses.Add($"collection_id IN ({string.Join(", ", collectionNames)})");
        }

        if (parameters.Ids is { Count: > 0 })
        {
            var idNames = new List<string>();
            for (var index = 0; index < parameters.Ids.Count; index++)
            {
                var paramName = $"@id{index}";
                idNames.Add(paramName);
                AddParameter(command, paramName, parameters.Ids[index]);
            }

            clauses.Add($"id IN ({string.Join(", ", idNames)})");
        }

        var startExpr = "COALESCE(start_datetime, datetime)";
        var endExpr = "COALESCE(end_datetime, datetime)";

        if (parameters.Start.HasValue)
        {
            AddParameter(command, "@start", parameters.Start.Value.UtcDateTime);
            clauses.Add($"({endExpr} IS NULL OR {endExpr} >= @start)");
        }

        if (parameters.End.HasValue)
        {
            AddParameter(command, "@end", parameters.End.Value.UtcDateTime);
            clauses.Add($"({startExpr} IS NULL OR {startExpr} <= @end)");
        }

        if (SupportsBboxFiltering && parameters.Bbox is { Length: >= 4 })
        {
            var minXExpr = GetBboxCoordinateExpression(0);
            var minYExpr = GetBboxCoordinateExpression(1);
            var maxXExpr = GetBboxCoordinateExpression(2);
            var maxYExpr = GetBboxCoordinateExpression(3);

            if (minXExpr.HasValue() && minYExpr.HasValue() &&
                maxXExpr.HasValue() && maxYExpr.HasValue())
            {
                AddParameter(command, "@bboxMinX", parameters.Bbox[0]);
                AddParameter(command, "@bboxMinY", parameters.Bbox[1]);
                AddParameter(command, "@bboxMaxX", parameters.Bbox[2]);
                AddParameter(command, "@bboxMaxY", parameters.Bbox[3]);
                clauses.Add($"({minXExpr} <= @bboxMaxX AND {maxXExpr} >= @bboxMinX AND {minYExpr} <= @bboxMaxY AND {maxYExpr} >= @bboxMinY)");
            }
        }

        if (includePagination && parameters.Token.HasValue())
        {
            var (tokenCollection, tokenItem) = ParseContinuationToken(parameters.Token);
            if (tokenCollection is not null && tokenItem is not null)
            {
                AddParameter(command, "@tokenCollection", tokenCollection);
                AddParameter(command, "@tokenItem", tokenItem);
                clauses.Add("(collection_id > @tokenCollection OR (collection_id = @tokenCollection AND id > @tokenItem))");
            }
        }

        // Add CQL2 filter if provided
        if (parameters.Filter.HasValue())
        {
            try
            {
                var filterClause = Cql2.StacFilterIntegration.ProcessFilter(command, parameters.Filter, Cql2.StacFilterIntegration.DetectProvider(ProviderName));
                if (filterClause.HasValue())
                {
                    clauses.Add($"({filterClause})");
                }
            }
            catch (Cql2.Cql2ParseException ex)
            {
                Logger?.LogError(ex, "Failed to parse CQL2 filter: {ErrorMessage}", ex.Message);
                throw new ArgumentException($"Invalid CQL2 filter expression: {ex.Message}", nameof(parameters), ex);
            }
            catch (Cql2.Cql2BuildException ex)
            {
                Logger?.LogError(ex, "Failed to build SQL from CQL2 filter: {ErrorMessage}", ex.Message);
                throw new ArgumentException($"CQL2 filter cannot be converted to SQL: {ex.Message}", nameof(parameters), ex);
            }
        }

        if (clauses.Count == 0)
        {
            return string.Empty;
        }

        return "WHERE " + string.Join(" AND ", clauses);
    }

    /// <summary>
    /// Parses and validates a continuation token for STAC search pagination.
    /// </summary>
    /// <param name="token">The continuation token string in format "collectionId:itemId".</param>
    /// <returns>Tuple containing the collection ID and item ID, or (null, null) if invalid.</returns>
    /// <remarks>
    /// Validates token length, part lengths, and characters to prevent injection attacks.
    /// Invalid tokens are logged and return null values rather than throwing exceptions.
    /// </remarks>
    private (string? CollectionId, string? ItemId) ParseContinuationToken(string? token)
    {
        const int MaxTokenLength = 256;
        const int MaxPartLength = 128;

        if (token.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        // Validate token length
        if (token.Length > MaxTokenLength)
        {
            Logger?.LogWarning("Continuation token exceeds maximum length of {MaxLength}: {TokenLength}", MaxTokenLength, token.Length);
            return (null, null);
        }

        var parts = token.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            Logger?.LogWarning("Continuation token has invalid format (expected 'collectionId:itemId'): {Token}", token);
            return (null, null);
        }

        var collectionId = parts[0];
        var itemId = parts[1];

        // Validate part lengths
        if (collectionId.Length > MaxPartLength)
        {
            Logger?.LogWarning("Continuation token collection ID exceeds maximum length of {MaxLength}: {Length}", MaxPartLength, collectionId.Length);
            return (null, null);
        }

        if (itemId.Length > MaxPartLength)
        {
            Logger?.LogWarning("Continuation token item ID exceeds maximum length of {MaxLength}: {Length}", MaxPartLength, itemId.Length);
            return (null, null);
        }

        // Validate characters - reject control characters, semicolons, and quotes
        if (ContainsInvalidTokenCharacters(collectionId) || ContainsInvalidTokenCharacters(itemId))
        {
            Logger?.LogWarning("Continuation token contains invalid characters: {Token}", token);
            return (null, null);
        }

        return (collectionId, itemId);
    }

    /// <summary>
    /// Checks if a token part contains invalid characters that could be used for injection attacks.
    /// </summary>
    /// <param name="value">The string to validate.</param>
    /// <returns>True if the string contains invalid characters, false otherwise.</returns>
    private static bool ContainsInvalidTokenCharacters(string value)
    {
        foreach (var ch in value)
        {
            // Reject control characters, semicolons, single/double quotes
            if (char.IsControl(ch) || ch == ';' || ch == '\'' || ch == '"')
            {
                return true;
            }
        }
        return false;
    }

    private static bool MatchesBbox(StacItemRecord item, double[]? bbox)
    {
        if (bbox is null || bbox.Length < 4)
        {
            return true;
        }

        if (item.Bbox is null || item.Bbox.Length < 4)
        {
            return false;
        }

        var candidate = item.Bbox;
        return candidate[0] <= bbox[2] && candidate[2] >= bbox[0] && candidate[1] <= bbox[3] && candidate[3] >= bbox[1];
    }

    /// <summary>
    /// Searches for STAC items with streaming support to handle large result sets efficiently.
    /// Uses cursor-based pagination internally to maintain constant memory usage.
    /// </summary>
    public async IAsyncEnumerable<StacItemRecord> SearchStreamAsync(
        StacSearchParameters parameters,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Guard.NotNull(parameters);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var pageSize = SearchOptions.StreamingPageSize;
        var maxItems = SearchOptions.MaxStreamingItems;
        var itemsReturned = 0;
        string? currentToken = parameters.Token;

        Logger?.LogDebug(
            "Starting STAC streaming search: pageSize={PageSize}, maxItems={MaxItems}, collections={Collections}",
            pageSize,
            maxItems,
            parameters.Collections?.Count ?? 0);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if we've reached the maximum item limit
            if (maxItems > 0 && itemsReturned >= maxItems)
            {
                Logger?.LogInformation(
                    "STAC streaming search reached maximum item limit: {MaxItems}",
                    maxItems);
                yield break;
            }

            // Create a connection for this page
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            // Build the query for this page
            await using var command = connection.CreateCommand();

            // Create parameters for this page with the current token
            var pageParameters = new StacSearchParameters
            {
                Collections = parameters.Collections,
                Ids = parameters.Ids,
                Bbox = parameters.Bbox,
                Intersects = parameters.Intersects,
                Start = parameters.Start,
                End = parameters.End,
                Limit = pageSize,
                Token = currentToken,
                SortBy = parameters.SortBy,
                Filter = parameters.Filter,
                FilterLang = parameters.FilterLang
            };

            var filter = BuildSearchFilter(command, pageParameters, includePagination: true);
            var orderBy = BuildOrderByClause(pageParameters.SortBy);

            // Fetch one extra item to determine if there are more pages
            var fetchLimit = Math.Min(pageSize + 1, maxItems > 0 ? maxItems - itemsReturned + 1 : pageSize + 1);
            command.CommandText = $"select collection_id, id, title, description, properties_json, assets_json, links_json, extensions_json, bbox_json, geometry_json, datetime, start_datetime, end_datetime, raster_dataset_id, etag, created_at, updated_at\nfrom stac_items {filter}\n{orderBy}\n{BuildLimitClause(fetchLimit)}";

            var pageItems = new List<StacItemRecord>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    pageItems.Add(ReadItem(reader));
                }
            }

            // If no items were returned, we're done
            if (pageItems.Count == 0)
            {
                Logger?.LogDebug(
                    "STAC streaming search completed: no more items, total returned={TotalItems}",
                    itemsReturned);
                yield break;
            }

            // Determine if there are more pages
            var hasMorePages = pageItems.Count > pageSize;
            var itemsToReturn = hasMorePages ? pageSize : pageItems.Count;

            // Yield items one at a time
            for (var i = 0; i < itemsToReturn; i++)
            {
                if (maxItems > 0 && itemsReturned >= maxItems)
                {
                    yield break;
                }

                yield return pageItems[i];
                itemsReturned++;
            }

            // If there are no more pages, we're done
            if (!hasMorePages)
            {
                Logger?.LogDebug(
                    "STAC streaming search completed: end of results, total returned={TotalItems}",
                    itemsReturned);
                yield break;
            }

            // Update the continuation token for the next page
            var lastItem = pageItems[itemsToReturn - 1];
            currentToken = $"{lastItem.CollectionId}:{lastItem.Id}";

            Logger?.LogTrace(
                "STAC streaming search fetched page: items={PageItems}, totalSoFar={TotalItems}, nextToken={NextToken}",
                itemsToReturn,
                itemsReturned,
                currentToken);
        }
    }
}
