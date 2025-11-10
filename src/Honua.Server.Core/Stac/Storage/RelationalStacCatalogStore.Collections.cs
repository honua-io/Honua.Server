// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac.Storage;

/// <summary>
/// Partial class containing STAC collection operations (Get, List, Upsert, Delete).
/// </summary>
internal abstract partial class RelationalStacCatalogStore
{
    /// <summary>
    /// Inserts or updates a STAC collection with optional optimistic concurrency control.
    /// </summary>
    /// <param name="collection">The collection record to upsert.</param>
    /// <param name="expectedETag">Optional ETag for optimistic concurrency control. If provided, the update will fail if the current ETag doesn't match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DBConcurrencyException">Thrown when the expectedETag is provided but doesn't match the current ETag in the database.</exception>
    public async Task UpsertCollectionAsync(StacCollectionRecord collection, string? expectedETag = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(collection);
        Guard.NotNullOrWhiteSpace(collection.Id);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Generate new ETag for this update
        var newETag = Guid.NewGuid().ToString("N");
        var parameters = BuildCollectionParameters(collection, newETag, expectedETag);
        var updated = await ExecuteNonQueryAsync(connection, transaction, UpdateCollectionSql, parameters, cancellationToken).ConfigureAwait(false);

        if (updated == 0 && expectedETag != null)
        {
            // Update failed and we have an expected ETag - this is a concurrency conflict
            throw new DBConcurrencyException(
                $"Collection '{collection.Id}' was modified by another user. Expected ETag: {expectedETag}");
        }

        if (updated == 0)
        {
            // No rows updated and no expected ETag - this is a new insert
            await ExecuteNonQueryAsync(connection, transaction, InsertCollectionSql, parameters, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StacCollectionRecord?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = SelectCollectionByIdSql;
        AddParameter(command, "@id", collectionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadCollection(reader);
        }

        return null;
    }

    /// <summary>
    /// Fetches multiple collections by their IDs in a single database query to avoid N+1 query problems.
    /// </summary>
    /// <param name="collectionIds">The collection IDs to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of collections that were found. Collections that don't exist are not included in the result.</returns>
    /// <remarks>
    /// This method uses a parameterized IN clause to fetch all requested collections in a single query,
    /// significantly improving performance when multiple collections need to be fetched.
    /// The returned list maintains the order of input IDs where possible, but missing collections are omitted.
    /// </remarks>
    public async Task<IReadOnlyList<StacCollectionRecord>> GetCollectionsAsync(IReadOnlyList<string> collectionIds, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNull(collectionIds);

        // Handle empty input early
        if (collectionIds.Count == 0)
        {
            return Array.Empty<StacCollectionRecord>();
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        return await OperationInstrumentation.Create<IReadOnlyList<StacCollectionRecord>>("GetCollectionsBatch")
            .WithActivitySource(StacMetrics.ActivitySource)
            .WithLogger(Logger)
            .WithTag("provider", ProviderName)
            .WithTag("collection_count", collectionIds.Count)
            .ExecuteAsync(async activity =>
            {
                await using var connection = CreateConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                await using var command = connection.CreateCommand();

                // Build parameterized IN clause for batch fetching
                var parameterNames = new List<string>();
                for (var i = 0; i < collectionIds.Count; i++)
                {
                    var paramName = $"@id{i}";
                    parameterNames.Add(paramName);
                    AddParameter(command, paramName, collectionIds[i]);
                }

                command.CommandText = $@"select id, title, description, license, version, keywords_json, extent_json, properties_json, links_json, extensions_json, conforms_to, data_source_id, service_id, layer_id, etag, created_at, updated_at
from stac_collections
where id IN ({string.Join(", ", parameterNames)})
order by id";

                var results = new List<StacCollectionRecord>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    results.Add(ReadCollection(reader));
                }

                activity?.SetTag("found_count", results.Count);
                activity?.SetTag("missing_count", collectionIds.Count - results.Count);

                Logger?.LogDebug(
                    "Batch fetched {FoundCount} collections out of {RequestedCount} requested (provider: {Provider})",
                    results.Count,
                    collectionIds.Count,
                    ProviderName);

                // Record metrics
                StacMetrics.CollectionBatchFetchCount.Add(1,
                    new KeyValuePair<string, object?>("provider", ProviderName));
                StacMetrics.CollectionBatchFetchSize.Record(collectionIds.Count,
                    new KeyValuePair<string, object?>("provider", ProviderName));

                return results;
            });
    }

    public async Task<IReadOnlyList<StacCollectionRecord>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<StacCollectionRecord>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = SelectCollectionsSql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadCollection(reader));
        }

        return results;
    }

    public async Task<StacCollectionListResult> ListCollectionsAsync(int limit, string? token = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        // Normalize limit to be between 1 and 1000
        var normalizedLimit = Math.Max(1, Math.Min(1000, limit));

        // Fetch N+1 items to detect if there are more results
        var fetchLimit = normalizedLimit + 1;

        var collections = new List<StacCollectionRecord>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        // Build query with pagination
        var sql = @"select id, title, description, license, version, keywords_json, extent_json, properties_json, links_json, extensions_json, conforms_to, data_source_id, service_id, layer_id, etag, created_at, updated_at
from stac_collections";

        // Add token-based pagination filter
        if (!token.IsNullOrEmpty())
        {
            sql += " where id > @token";
        }

        sql += $" order by id {BuildLimitClause(fetchLimit)}";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "@limit", fetchLimit);
        if (!token.IsNullOrEmpty())
        {
            AddParameter(command, "@token", token);
        }

        // Execute query and read results
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            collections.Add(ReadCollection(reader));
        }

        // Determine next token
        string? nextToken = null;
        if (collections.Count > normalizedLimit)
        {
            nextToken = collections[^1].Id; // Last item ID becomes next token
            collections.RemoveAt(collections.Count - 1); // Remove the N+1 item
        }

        // Get total count (executed in parallel with the main query would be ideal, but we'll do it sequentially for simplicity)
        var totalCount = await GetTotalCollectionCountAsync(connection, cancellationToken).ConfigureAwait(false);

        return new StacCollectionListResult
        {
            Collections = collections,
            TotalCount = totalCount,
            NextToken = nextToken
        };
    }

    private static async Task<int> GetTotalCollectionCountAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from stac_collections";
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

    public async Task<bool> DeleteCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = DeleteCollectionSql;
        AddParameter(command, "@id", collectionId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }
}
