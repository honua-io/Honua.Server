// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac.Storage;

/// <summary>
/// Partial class containing STAC item operations (Get, List, Upsert, BulkUpsert, Delete).
/// </summary>
internal abstract partial class RelationalStacCatalogStore
{
    /// <summary>
    /// Inserts or updates a STAC item with optional optimistic concurrency control.
    /// </summary>
    /// <param name="item">The item record to upsert.</param>
    /// <param name="expectedETag">Optional ETag for optimistic concurrency control. If provided, the update will fail if the current ETag doesn't match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="DBConcurrencyException">Thrown when the expectedETag is provided but doesn't match the current ETag in the database.</exception>
    public async Task UpsertItemAsync(StacItemRecord item, string? expectedETag = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(item);
        Guard.NotNullOrWhiteSpace(item.Id);
        Guard.NotNullOrWhiteSpace(item.CollectionId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Generate new ETag for this update
        var newETag = Guid.NewGuid().ToString("N");
        var parameters = BuildItemParameters(item, newETag, expectedETag);
        var updated = await ExecuteNonQueryAsync(connection, transaction, UpdateItemSql, parameters, cancellationToken).ConfigureAwait(false);

        if (updated == 0 && expectedETag != null)
        {
            // Update failed and we have an expected ETag - this is a concurrency conflict
            throw new DBConcurrencyException(
                $"Item '{item.Id}' in collection '{item.CollectionId}' was modified by another user. Expected ETag: {expectedETag}");
        }

        if (updated == 0)
        {
            // No rows updated and no expected ETag - this is a new insert
            await ExecuteNonQueryAsync(connection, transaction, InsertItemSql, parameters, cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BulkUpsertResult> BulkUpsertItemsAsync(IReadOnlyList<StacItemRecord> items, BulkUpsertOptions? options = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(items);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        options ??= new BulkUpsertOptions();

        return await OperationInstrumentation.Create<BulkUpsertResult>("BulkUpsertItems")
            .WithActivitySource(StacMetrics.ActivitySource)
            .WithLogger(Logger)
            .WithMetrics(StacMetrics.BulkUpsertCount, StacMetrics.BulkUpsertCount, StacMetrics.BulkUpsertDuration)
            .WithTag("item_count", items.Count)
            .WithTag("batch_size", options.BatchSize)
            .WithTag("provider", ProviderName)
            .WithTag("atomic_transaction", options.UseAtomicTransaction)
            .ExecuteAsync(async activity =>
            {
                var failures = new List<BulkUpsertItemFailure>();
                var successCount = 0;

                // Split items into batches
                var batchSize = Math.Max(1, options.BatchSize);
                var totalBatches = (items.Count + batchSize - 1) / batchSize;

                await using var connection = CreateConnection();
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

                // CRITICAL FIX: Use single transaction for entire bulk operation
                // This ensures all-or-nothing semantics - if batch 35 of 50 fails,
                // ALL batches are rolled back, preventing partial catalog updates
                if (options.UseAtomicTransaction)
                {
                    Logger?.LogDebug(
                        "Starting atomic bulk upsert transaction for {ItemCount} items in {BatchCount} batches (provider: {Provider}, timeout: {Timeout}s)",
                        items.Count,
                        totalBatches,
                        ProviderName,
                        options.TransactionTimeoutSeconds);

                    await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);

                    try
                    {
                        // Set transaction timeout if specified
                        if (options.TransactionTimeoutSeconds > 0)
                        {
                            await using var timeoutCommand = connection.CreateCommand();
                            timeoutCommand.Transaction = transaction;
                            timeoutCommand.CommandTimeout = options.TransactionTimeoutSeconds;
                            timeoutCommand.CommandText = "SELECT 1"; // Dummy query to set timeout
                        }

                        // Process all batches within single transaction
                        for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var batchStart = batchIndex * batchSize;
                            var batchCount = Math.Min(batchSize, items.Count - batchStart);
                            var batch = items.Skip(batchStart).Take(batchCount).ToList();

                            try
                            {
                                if (options.UseBulkInsertOptimization && SupportsBulkInsert)
                                {
                                    // Use database-specific bulk insert optimization
                                    await BulkInsertItemsAsync(connection, transaction, batch, cancellationToken).ConfigureAwait(false);
                                    successCount += batch.Count;
                                }
                                else
                                {
                                    // Fall back to individual inserts
                                    foreach (var item in batch)
                                    {
                                        try
                                        {
                                            await UpsertItemInternalAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
                                            successCount++;
                                        }
                                        catch (Exception ex)
                                        {
                                            failures.Add(new BulkUpsertItemFailure
                                            {
                                                ItemId = item.Id,
                                                CollectionId = item.CollectionId,
                                                ErrorMessage = ex.Message,
                                                Exception = ex
                                            });

                                            if (!options.ContinueOnError)
                                            {
                                                throw;
                                            }
                                        }
                                    }
                                }

                                // Report progress (but don't commit yet!)
                                if (options.ReportProgress && options.ProgressCallback != null)
                                {
                                    var processedSoFar = Math.Min((batchIndex + 1) * batchSize, items.Count);
                                    options.ProgressCallback(processedSoFar, items.Count, batchIndex + 1);
                                }
                            }
                            catch (Exception ex) when (options.ContinueOnError)
                            {
                                // Add all items in the batch to failures if we couldn't process the batch
                                foreach (var item in batch)
                                {
                                    if (!failures.Any(f => f.ItemId == item.Id && f.CollectionId == item.CollectionId))
                                    {
                                        failures.Add(new BulkUpsertItemFailure
                                        {
                                            ItemId = item.Id,
                                            CollectionId = item.CollectionId,
                                            ErrorMessage = $"Batch failure: {ex.Message}",
                                            Exception = ex
                                        });
                                    }
                                }

                                Logger?.LogWarning(
                                    ex,
                                    "Batch {BatchIndex} failed but continuing (ContinueOnError=true): {ErrorMessage}",
                                    batchIndex + 1,
                                    ex.Message);
                            }
                        }

                        // COMMIT: All batches succeeded (or ContinueOnError handled failures)
                        if (!options.ContinueOnError || failures.Count == 0)
                        {
                            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                            Logger?.LogInformation(
                                "Atomic bulk upsert transaction committed: {SuccessCount} items in {BatchCount} batches (provider: {Provider})",
                                successCount,
                                totalBatches,
                                ProviderName);
                        }
                        else
                        {
                            // With ContinueOnError and failures, still commit partial success
                            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                            Logger?.LogWarning(
                                "Atomic bulk upsert transaction committed with {FailureCount} failures: {SuccessCount} items succeeded (provider: {Provider})",
                                failures.Count,
                                successCount,
                                ProviderName);
                        }
                    }
                    catch (Exception ex)
                    {
                        // ROLLBACK: Any failure rolls back entire operation
                        Logger?.LogError(
                            ex,
                            "Atomic bulk upsert transaction failed, rolling back all {BatchCount} batches ({ItemCount} items): {ErrorMessage}",
                            totalBatches,
                            items.Count,
                            ex.Message);

                        try
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception rollbackEx)
                        {
                            Logger?.LogError(
                                rollbackEx,
                                "Failed to rollback transaction during error handling: {ErrorMessage}",
                                rollbackEx.Message);
                        }

                        throw; // Re-throw original exception
                    }
                }
                else
                {
                    // Legacy mode: per-batch transactions (NOT RECOMMENDED)
                    Logger?.LogWarning(
                        "Using legacy per-batch transactions for bulk upsert (UseAtomicTransaction=false). This may cause partial catalog updates!");

                    for (var batchIndex = 0; batchIndex < totalBatches; batchIndex++)
                    {
                        var batchStart = batchIndex * batchSize;
                        var batchCount = Math.Min(batchSize, items.Count - batchStart);
                        var batch = items.Skip(batchStart).Take(batchCount).ToList();

                        try
                        {
                            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                            try
                            {
                                if (options.UseBulkInsertOptimization && SupportsBulkInsert)
                                {
                                    // Use database-specific bulk insert optimization
                                    await BulkInsertItemsAsync(connection, transaction, batch, cancellationToken).ConfigureAwait(false);
                                    successCount += batch.Count;
                                }
                                else
                                {
                                    // Fall back to individual inserts
                                    foreach (var item in batch)
                                    {
                                        try
                                        {
                                            await UpsertItemInternalAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
                                            successCount++;
                                        }
                                        catch (Exception ex)
                                        {
                                            failures.Add(new BulkUpsertItemFailure
                                            {
                                                ItemId = item.Id,
                                                CollectionId = item.CollectionId,
                                                ErrorMessage = ex.Message,
                                                Exception = ex
                                            });

                                            if (!options.ContinueOnError)
                                            {
                                                throw;
                                            }
                                        }
                                    }
                                }

                                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                                // Report progress
                                if (options.ReportProgress && options.ProgressCallback != null)
                                {
                                    var processedSoFar = Math.Min((batchIndex + 1) * batchSize, items.Count);
                                    options.ProgressCallback(processedSoFar, items.Count, batchIndex + 1);
                                }
                            }
                            catch (Exception ex) when (options.ContinueOnError)
                            {
                                // Add all items in the batch to failures if we couldn't process the batch
                                foreach (var item in batch)
                                {
                                    if (!failures.Any(f => f.ItemId == item.Id && f.CollectionId == item.CollectionId))
                                    {
                                        failures.Add(new BulkUpsertItemFailure
                                        {
                                            ItemId = item.Id,
                                            CollectionId = item.CollectionId,
                                            ErrorMessage = $"Batch failure: {ex.Message}",
                                            Exception = ex
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception) when (!options.ContinueOnError)
                        {
                            // Re-throw if we're not continuing on error
                            throw;
                        }
                    }
                }

                var result = new BulkUpsertResult
                {
                    SuccessCount = successCount,
                    FailureCount = failures.Count,
                    Failures = failures,
                    Duration = TimeSpan.Zero // Will be set by OperationInstrumentation
                };

                // Record additional metrics
                StacMetrics.BulkUpsertItemsCount.Add(successCount,
                    new KeyValuePair<string, object?>("provider", ProviderName),
                    new KeyValuePair<string, object?>("operation", "success"));
                StacMetrics.BulkUpsertFailures.Add(failures.Count,
                    new KeyValuePair<string, object?>("provider", ProviderName));
                StacMetrics.BulkUpsertThroughput.Record(result.ItemsPerSecond,
                    new KeyValuePair<string, object?>("provider", ProviderName));

                activity?.SetTag("success_count", successCount);
                activity?.SetTag("failure_count", failures.Count);
                activity?.SetTag("throughput", result.ItemsPerSecond);

                return result;
            });
    }

    private async Task UpsertItemInternalAsync(DbConnection connection, DbTransaction transaction, StacItemRecord item, CancellationToken cancellationToken)
    {
        Guard.NotNullOrWhiteSpace(item.Id);
        Guard.NotNullOrWhiteSpace(item.CollectionId);

        // Generate new ETag for this update
        var newETag = Guid.NewGuid().ToString("N");
        var parameters = BuildItemParameters(item, newETag, null);
        var updated = await ExecuteNonQueryAsync(connection, transaction, UpdateItemSql, parameters, cancellationToken).ConfigureAwait(false);

        if (updated == 0)
        {
            // No rows updated - this is a new insert
            await ExecuteNonQueryAsync(connection, transaction, InsertItemSql, parameters, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<StacItemRecord?> GetItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = SelectItemByIdSql;
        AddParameter(command, "@collectionId", collectionId);
        AddParameter(command, "@id", itemId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ReadItem(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<StacItemRecord>> ListItemsAsync(string collectionId, int limit, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var results = new List<StacItemRecord>();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        // Build SQL with database-level pagination
        var sql = SelectItemsSql;
        if (pageToken.HasValue())
        {
            sql += " AND id > @pageToken";
            AddParameter(command, "@pageToken", pageToken);
        }

        // Apply limit at database level to prevent loading all items into memory
        if (limit > 0)
        {
            sql += " " + BuildLimitClause(limit);
        }

        command.CommandText = sql;
        AddParameter(command, "@collectionId", collectionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(ReadItem(reader));
        }

        return results;
    }

    public async Task<bool> DeleteItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = DeleteItemSql;
        AddParameter(command, "@collectionId", collectionId);
        AddParameter(command, "@id", itemId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }
}
