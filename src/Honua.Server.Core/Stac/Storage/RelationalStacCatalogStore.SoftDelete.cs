// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.SoftDelete;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Stac.Storage;

/// <summary>
/// Soft delete implementation for RelationalStacCatalogStore.
/// This partial class adds soft delete, restore, and hard delete methods for collections and items.
/// </summary>
internal abstract partial class RelationalStacCatalogStore
{
    // Optional dependency - if null, soft delete operations will still work but no audit trail
    protected IDeletionAuditStore? DeletionAuditStore { get; set; }

    /// <summary>
    /// Soft-deletes a STAC collection by marking it as deleted.
    /// </summary>
    public async Task<bool> SoftDeleteCollectionAsync(
        string collectionId,
        string? deletedBy,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE stac_collections
SET is_deleted = @IsDeleted,
    deleted_at = @DeletedAt,
    deleted_by = @DeletedBy
WHERE id = @Id AND (is_deleted IS NULL OR is_deleted = @NotDeleted)";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            AddParameter(command, "@Id", collectionId);
            AddParameter(command, "@IsDeleted", GetBooleanValue(true));
            AddParameter(command, "@NotDeleted", GetBooleanValue(false));
            AddParameter(command, "@DeletedAt", DateTime.UtcNow);
            AddParameter(command, "@DeletedBy", deletedBy ?? (object)DBNull.Value);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0 && DeletionAuditStore != null)
            {
                // Record audit trail
                var context = new DeletionContext { UserId = deletedBy };
                await DeletionAuditStore.RecordDeletionAsync(
                    "StacCollection",
                    collectionId,
                    "soft",
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            Logger?.LogInformation(
                "Soft-deleted STAC collection {CollectionId} by user {UserId}",
                collectionId, deletedBy ?? "<system>");

            return rowsAffected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Restores a soft-deleted STAC collection.
    /// </summary>
    public async Task<bool> RestoreCollectionAsync(
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE stac_collections
SET is_deleted = @NotDeleted,
    deleted_at = NULL,
    deleted_by = NULL
WHERE id = @Id AND is_deleted = @IsDeleted";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            AddParameter(command, "@Id", collectionId);
            AddParameter(command, "@IsDeleted", GetBooleanValue(true));
            AddParameter(command, "@NotDeleted", GetBooleanValue(false));

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0 && DeletionAuditStore != null)
            {
                // Record restoration audit trail
                var context = DeletionContext.Empty;
                await DeletionAuditStore.RecordRestorationAsync(
                    "StacCollection",
                    collectionId,
                    context,
                    cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            Logger?.LogInformation("Restored STAC collection {CollectionId}", collectionId);

            return rowsAffected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Permanently deletes a STAC collection (hard delete).
    /// </summary>
    public async Task<bool> HardDeleteCollectionAsync(
        string collectionId,
        string? deletedBy,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // First record audit trail before permanent deletion
            if (DeletionAuditStore != null)
            {
                var context = new DeletionContext { UserId = deletedBy };
                await DeletionAuditStore.RecordDeletionAsync(
                    "StacCollection",
                    collectionId,
                    "hard",
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            // Now perform hard delete
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = DeleteCollectionSql;

            AddParameter(command, "@id", collectionId);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            Logger?.LogWarning(
                "Hard-deleted STAC collection {CollectionId} by user {UserId} (PERMANENT)",
                collectionId, deletedBy ?? "<system>");

            return rowsAffected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Soft-deletes a STAC item by marking it as deleted.
    /// </summary>
    public async Task<bool> SoftDeleteItemAsync(
        string collectionId,
        string itemId,
        string? deletedBy,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE stac_items
SET is_deleted = @IsDeleted,
    deleted_at = @DeletedAt,
    deleted_by = @DeletedBy
WHERE collection_id = @CollectionId AND id = @Id AND (is_deleted IS NULL OR is_deleted = @NotDeleted)";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            AddParameter(command, "@CollectionId", collectionId);
            AddParameter(command, "@Id", itemId);
            AddParameter(command, "@IsDeleted", GetBooleanValue(true));
            AddParameter(command, "@NotDeleted", GetBooleanValue(false));
            AddParameter(command, "@DeletedAt", DateTime.UtcNow);
            AddParameter(command, "@DeletedBy", deletedBy ?? (object)DBNull.Value);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0 && DeletionAuditStore != null)
            {
                var context = new DeletionContext { UserId = deletedBy };
                await DeletionAuditStore.RecordDeletionAsync(
                    "StacItem",
                    $"{collectionId}/{itemId}",
                    "soft",
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            Logger?.LogInformation(
                "Soft-deleted STAC item {CollectionId}/{ItemId} by user {UserId}",
                collectionId, itemId, deletedBy ?? "<system>");

            return rowsAffected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Restores a soft-deleted STAC item.
    /// </summary>
    public async Task<bool> RestoreItemAsync(
        string collectionId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        const string sql = @"
UPDATE stac_items
SET is_deleted = @NotDeleted,
    deleted_at = NULL,
    deleted_by = NULL
WHERE collection_id = @CollectionId AND id = @Id AND is_deleted = @IsDeleted";

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            AddParameter(command, "@CollectionId", collectionId);
            AddParameter(command, "@Id", itemId);
            AddParameter(command, "@IsDeleted", GetBooleanValue(true));
            AddParameter(command, "@NotDeleted", GetBooleanValue(false));

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (rowsAffected > 0 && DeletionAuditStore != null)
            {
                var context = DeletionContext.Empty;
                await DeletionAuditStore.RecordRestorationAsync(
                    "StacItem",
                    $"{collectionId}/{itemId}",
                    context,
                    cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            Logger?.LogInformation("Restored STAC item {CollectionId}/{ItemId}", collectionId, itemId);

            return rowsAffected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Permanently deletes a STAC item (hard delete).
    /// </summary>
    public async Task<bool> HardDeleteItemAsync(
        string collectionId,
        string itemId,
        string? deletedBy,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // First record audit trail before permanent deletion
            if (DeletionAuditStore != null)
            {
                var context = new DeletionContext { UserId = deletedBy };
                await DeletionAuditStore.RecordDeletionAsync(
                    "StacItem",
                    $"{collectionId}/{itemId}",
                    "hard",
                    context,
                    null,
                    cancellationToken).ConfigureAwait(false);
            }

            // Now perform hard delete
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = DeleteItemSql;

            AddParameter(command, "@collectionId", collectionId);
            AddParameter(command, "@id", itemId);

            var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            Logger?.LogWarning(
                "Hard-deleted STAC item {CollectionId}/{ItemId} by user {UserId} (PERMANENT)",
                collectionId, itemId, deletedBy ?? "<system>");

            return rowsAffected > 0;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Gets the boolean value appropriate for this database provider.
    /// Override in provider-specific implementations (e.g., SQLite uses 0/1).
    /// </summary>
    protected virtual object GetBooleanValue(bool value) => value;
}
