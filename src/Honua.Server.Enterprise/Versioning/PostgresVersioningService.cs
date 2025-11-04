// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Versioning;

/// <summary>
/// PostgreSQL implementation of versioning service using temporal tables
/// </summary>
public class PostgresVersioningService<T> : IVersioningService<T> where T : VersionedEntityBase, new()
{
    private readonly string _connectionString;
    private readonly string _tableName;
    private readonly string _quotedTableName;
    private readonly IMergeEngine<T> _mergeEngine;
    private readonly ILogger<PostgresVersioningService<T>> _logger;

    public PostgresVersioningService(
        string connectionString,
        string tableName,
        IMergeEngine<T>? mergeEngine = null,
        ILogger<PostgresVersioningService<T>>? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
        _mergeEngine = mergeEngine ?? new DefaultMergeEngine<T>();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate and quote table name to prevent SQL injection
        SqlIdentifierValidator.ValidateIdentifier(tableName, nameof(tableName));
        _quotedTableName = SqlIdentifierValidator.ValidateAndQuotePostgres(tableName);

        _logger.LogInformation("PostgresVersioningService initialized for table {TableName}", _quotedTableName);
    }

    public async Task<T> CreateAsync(T entity, string createdBy, string? commitMessage = null, CancellationToken cancellationToken = default)
    {
        entity.Version = 1;
        entity.VersionCreatedAt = DateTimeOffset.UtcNow;
        entity.VersionCreatedBy = createdBy;
        entity.CommitMessage = commitMessage ?? "Initial version";
        entity.Branch = "main";
        entity.UpdateContentHash();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = BuildInsertSql();
        await connection.ExecuteAsync(new CommandDefinition(sql, entity, cancellationToken: cancellationToken));

        return entity;
    }

    public async Task<T> UpdateAsync(T entity, string updatedBy, string? commitMessage = null, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get current version
        var currentVersion = await GetCurrentVersionNumberAsync(entity.Id, connection, cancellationToken);
        if (currentVersion == null)
        {
            throw new InvalidOperationException($"Entity {entity.Id} does not exist");
        }

        // Create new version
        entity.Version = currentVersion.Value + 1;
        entity.ParentVersion = currentVersion.Value;
        entity.VersionCreatedAt = DateTimeOffset.UtcNow;
        entity.VersionCreatedBy = updatedBy;
        entity.CommitMessage = commitMessage ?? $"Update to version {entity.Version}";
        entity.UpdateContentHash();

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Mark current version as superseded
            await connection.ExecuteAsync(new CommandDefinition(
                $"UPDATE {_quotedTableName} SET version_valid_to = @Now WHERE id = @Id AND version = @Version",
                new { Now = DateTimeOffset.UtcNow, entity.Id, Version = currentVersion.Value },
                transaction,
                cancellationToken: cancellationToken));

            // Insert new version
            var sql = BuildInsertSql();
            await connection.ExecuteAsync(new CommandDefinition(sql, entity, transaction, cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return entity;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> DeleteAsync(Guid id, string deletedBy, string? reason = null, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(id, cancellationToken);
        if (current == null)
        {
            throw new InvalidOperationException($"Entity {id} does not exist");
        }

        current.IsDeleted = true;
        return await UpdateAsync(current, deletedBy, reason ?? "Deleted", cancellationToken);
    }

    public async Task<T?> GetCurrentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            SELECT * FROM {_quotedTableName}
            WHERE id = @Id
              AND version_valid_to IS NULL
              AND is_deleted = FALSE
            ORDER BY version DESC
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<T?> GetVersionAsync(Guid id, long version, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $"SELECT * FROM {_quotedTableName} WHERE id = @Id AND version = @Version";
        return await connection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, new { Id = id, Version = version }, cancellationToken: cancellationToken));
    }

    public async Task<T?> GetAtTimestampAsync(Guid id, DateTimeOffset timestamp, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            SELECT * FROM {_quotedTableName}
            WHERE id = @Id
              AND version_valid_from <= @Timestamp
              AND (version_valid_to IS NULL OR version_valid_to > @Timestamp)
            ORDER BY version DESC
            LIMIT 1";

        return await connection.QueryFirstOrDefaultAsync<T>(new CommandDefinition(sql, new { Id = id, Timestamp = timestamp }, cancellationToken: cancellationToken));
    }

    public async Task<VersionHistory<T>> GetHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            SELECT * FROM {_quotedTableName}
            WHERE id = @Id
            ORDER BY version ASC";

        var versions = await connection.QueryAsync<T>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        var versionList = versions.ToList();

        var history = new VersionHistory<T>
        {
            EntityId = id,
            Versions = versionList.Select(v => new VersionNode<T>
            {
                Entity = v,
                ParentVersion = v.ParentVersion,
                Branch = v.Branch
            }).ToList()
        };

        if (versionList.Any())
        {
            history.FirstVersionAt = versionList.First().VersionCreatedAt;
            history.LastVersionAt = versionList.Last().VersionCreatedAt;
        }

        // Build parent-child relationships
        foreach (var node in history.Versions)
        {
            if (node.ParentVersion.HasValue)
            {
                var parent = history.Versions.FirstOrDefault(v => v.Entity.Version == node.ParentVersion.Value);
                if (parent != null)
                {
                    parent.ChildVersions.Add(node.Entity.Version);
                }
            }
        }

        return history;
    }

    public async Task<ChangeSet> GetChangesAsync(Guid id, long fromVersion, long toVersion, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT * FROM version_changes
            WHERE entity_id = @Id
              AND from_version = @FromVersion
              AND to_version = @ToVersion
            ORDER BY field_name";

        var changes = await connection.QueryAsync<FieldChange>(new CommandDefinition(sql, new { Id = id, FromVersion = fromVersion, ToVersion = toVersion }, cancellationToken: cancellationToken));

        var fromEntity = await GetVersionAsync(id, fromVersion, cancellationToken);
        var toEntity = await GetVersionAsync(id, toVersion, cancellationToken);

        return new ChangeSet
        {
            EntityId = id,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            FromTimestamp = fromEntity?.VersionCreatedAt ?? DateTimeOffset.MinValue,
            ToTimestamp = toEntity?.VersionCreatedAt ?? DateTimeOffset.UtcNow,
            ChangedBy = toEntity?.VersionCreatedBy,
            CommitMessage = toEntity?.CommitMessage,
            FieldChanges = changes.ToList()
        };
    }

    public async Task<ChangeSet> CompareWithCurrentAsync(Guid id, long compareVersion, CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentAsync(id, cancellationToken);
        if (current == null)
        {
            throw new InvalidOperationException($"Entity {id} does not exist");
        }

        return await GetChangesAsync(id, compareVersion, current.Version, cancellationToken);
    }

    public async Task<List<MergeConflict>> DetectConflictsAsync(Guid id, long baseVersion, long currentVersion, long incomingVersion, CancellationToken cancellationToken = default)
    {
        var baseEntity = await GetVersionAsync(id, baseVersion, cancellationToken);
        var currentEntity = await GetVersionAsync(id, currentVersion, cancellationToken);
        var incomingEntity = await GetVersionAsync(id, incomingVersion, cancellationToken);

        if (baseEntity == null || currentEntity == null || incomingEntity == null)
        {
            throw new InvalidOperationException("One or more versions not found");
        }

        return _mergeEngine.DetectConflicts(baseEntity, currentEntity, incomingEntity);
    }

    public async Task<MergeResult<T>> MergeAsync(MergeRequest request, CancellationToken cancellationToken = default)
    {
        var baseEntity = await GetVersionAsync(request.EntityId, request.BaseVersion, cancellationToken);
        var currentEntity = await GetVersionAsync(request.EntityId, request.CurrentVersion, cancellationToken);
        var incomingEntity = await GetVersionAsync(request.EntityId, request.IncomingVersion, cancellationToken);

        if (baseEntity == null || currentEntity == null || incomingEntity == null)
        {
            return new MergeResult<T>
            {
                Success = false,
                ErrorMessage = "One or more versions not found"
            };
        }

        var result = _mergeEngine.Merge(baseEntity, currentEntity, incomingEntity, request.Strategy, request.FieldResolutions);

        if (result.Success && !result.HasConflicts)
        {
            // Create new merged version
            result.MergedEntity!.Version = Math.Max(request.CurrentVersion, request.IncomingVersion) + 1;
            result.MergedEntity.ParentVersion = request.CurrentVersion;
            result.MergedEntity.VersionCreatedBy = request.MergedBy;
            result.MergedEntity.CommitMessage = request.CommitMessage ?? $"Merged version {request.IncomingVersion} into {request.CurrentVersion}";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                // Insert merged version
                var sql = BuildInsertSql();
                await connection.ExecuteAsync(new CommandDefinition(sql, result.MergedEntity, transaction, cancellationToken: cancellationToken));

                // Log merge operation
                await LogMergeOperationAsync(connection, transaction, request, result, cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        return result;
    }

    public async Task<RollbackResult<T>> RollbackAsync(RollbackRequest request, CancellationToken cancellationToken = default)
    {
        var targetVersion = await GetVersionAsync(request.EntityId, request.ToVersion, cancellationToken);
        if (targetVersion == null)
        {
            return new RollbackResult<T>
            {
                Success = false,
                ErrorMessage = $"Version {request.ToVersion} not found"
            };
        }

        // Create a copy of the target version as a new version
        var current = await GetCurrentAsync(request.EntityId, cancellationToken);
        if (current == null)
        {
            return new RollbackResult<T>
            {
                Success = false,
                ErrorMessage = "Current version not found"
            };
        }

        var restoredEntity = CloneEntity(targetVersion);
        restoredEntity.Version = current.Version + 1;
        restoredEntity.ParentVersion = current.Version;
        restoredEntity.VersionCreatedBy = request.RolledBackBy;
        restoredEntity.CommitMessage = $"Rolled back to version {request.ToVersion}: {request.Reason}";
        restoredEntity.VersionCreatedAt = DateTimeOffset.UtcNow;
        restoredEntity.Branch = request.CreateBranch ? $"rollback-{request.ToVersion}" : current.Branch;

        var result = await UpdateAsync(restoredEntity, request.RolledBackBy, restoredEntity.CommitMessage, cancellationToken);

        return new RollbackResult<T>
        {
            Success = true,
            RestoredEntity = result,
            NewVersion = result.Version
        };
    }

    public async Task<T> CreateBranchAsync(Guid id, long fromVersion, string branchName, string createdBy, CancellationToken cancellationToken = default)
    {
        var sourceVersion = await GetVersionAsync(id, fromVersion, cancellationToken);
        if (sourceVersion == null)
        {
            throw new InvalidOperationException($"Version {fromVersion} not found");
        }

        var branchedEntity = CloneEntity(sourceVersion);
        branchedEntity.Version = fromVersion + 1;
        branchedEntity.ParentVersion = fromVersion;
        branchedEntity.Branch = branchName;
        branchedEntity.VersionCreatedBy = createdBy;
        branchedEntity.CommitMessage = $"Created branch '{branchName}' from version {fromVersion}";
        branchedEntity.VersionCreatedAt = DateTimeOffset.UtcNow;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = BuildInsertSql();
        await connection.ExecuteAsync(new CommandDefinition(sql, branchedEntity, cancellationToken: cancellationToken));

        return branchedEntity;
    }

    public async Task<List<string>> GetBranchesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            SELECT DISTINCT branch
            FROM {_quotedTableName}
            WHERE id = @Id
            ORDER BY branch";

        var branches = await connection.QueryAsync<string>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return branches.Where(b => !string.IsNullOrEmpty(b)).ToList();
    }

    public async Task<VersionTree<T>> GetVersionTreeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var history = await GetHistoryAsync(id, cancellationToken);

        var tree = new VersionTree<T>
        {
            EntityId = id,
            Nodes = history.Versions,
            Branches = history.Versions
                .Where(v => v.Branch != null)
                .GroupBy(v => v.Branch!)
                .ToDictionary(g => g.Key, g => g.ToList())
        };

        return tree;
    }

    // Helper methods

    private async Task<long?> GetCurrentVersionNumberAsync(Guid id, NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var sql = $@"
            SELECT MAX(version)
            FROM {_quotedTableName}
            WHERE id = @Id
              AND is_deleted = FALSE";

        return await connection.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    private string BuildInsertSql()
    {
        // This would be generated based on the entity type
        // For now, returning a placeholder - in real implementation, this would use reflection
        // or source generators to build the appropriate INSERT statement
        return $"INSERT INTO {_quotedTableName} (/* columns */) VALUES (/* values */)";
    }

    private T CloneEntity(T source)
    {
        // Deep clone using JSON serialization
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json)!;
    }

    private async Task LogMergeOperationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, MergeRequest request, MergeResult<T> result, CancellationToken cancellationToken)
    {
        var sql = @"
            INSERT INTO merge_operations (
                entity_type, entity_id, base_version, current_version, incoming_version,
                result_version, merge_strategy, auto_merged_fields, conflicted_fields,
                manually_resolved_fields, status, merged_by, commit_message
            ) VALUES (
                @EntityType, @EntityId, @BaseVersion, @CurrentVersion, @IncomingVersion,
                @ResultVersion, @MergeStrategy, @AutoMergedFields, @ConflictedFields,
                @ManuallyResolvedFields, @Status, @MergedBy, @CommitMessage
            )";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            EntityType = typeof(T).Name,
            request.EntityId,
            request.BaseVersion,
            request.CurrentVersion,
            request.IncomingVersion,
            ResultVersion = result.MergedEntity?.Version,
            MergeStrategy = request.Strategy.ToString(),
            AutoMergedFields = result.AutoMergedChanges.Count,
            ConflictedFields = result.Conflicts.Count,
            ManuallyResolvedFields = result.Conflicts.Count(c => c.IsResolved),
            Status = result.Success ? (result.HasConflicts ? "conflicts_remaining" : "success") : "failed",
            request.MergedBy,
            request.CommitMessage
        }, transaction, cancellationToken: cancellationToken));
    }
}
