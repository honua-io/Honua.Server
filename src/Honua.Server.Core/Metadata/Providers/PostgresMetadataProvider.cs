// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using Npgsql;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata.Providers;

/// <summary>
/// Metadata provider backed by PostgreSQL with NOTIFY/LISTEN for real-time cluster synchronization.
/// Uses JSONB for optimized JSON storage and native PostgreSQL pub/sub for instant notifications.
///
/// Features:
/// - PostgreSQL ACID transactions
/// - NOTIFY/LISTEN for real-time pub/sub (latency under 1 second)
/// - JSONB column for optimized JSON queries and indexing
/// - Versioning and rollback support
/// - Audit trail
/// - Auto-schema creation
///
/// Requirements:
/// - PostgreSQL 10+ (for JSONB and improved pub/sub)
/// - PostGIS extension (optional, for spatial metadata queries)
/// </summary>
public sealed class PostgresMetadataProvider : IMutableMetadataProvider, IReloadableMetadataProvider, IAsyncDisposable, IDisposable
{
    private const string MetadataSnapshotsTableName = "metadata_snapshots";
    private const string MetadataChangeLogTableName = "metadata_change_log";
    private const string NotifyFunctionName = "notify_metadata_change";
    private const string MetadataChangeTriggerName = "metadata_change_trigger";
    private const string DefaultNotificationChannel = "honua_metadata_changes";

    private readonly string _connectionString;
    private readonly ILogger<PostgresMetadataProvider> _logger;
    private readonly PostgresMetadataOptions _options;
    private readonly string _instanceId;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private readonly string _quotedSchemaName;
    private readonly string _schemaPrefix;
    private readonly string _metadataSnapshotsTable;
    private readonly string _metadataChangeLogTable;
    private readonly string _notifyFunctionIdentifier;
    private readonly string _metadataChangeTriggerIdentifier;
    private readonly string _notificationChannelIdentifier;
    private readonly string _notificationChannelLiteral;
    private Task? _initializationTask;
    private CancellationTokenSource? _notificationLoopCts;
    private Task? _notificationLoopTask;
    private NpgsqlConnection? _notificationConnection;
    private int _notificationListenerStarted;
    private bool _disposed;
    private long _lastChangeVersion;

    // Use centralized JSON helper for consistent serialization
    private static readonly JsonSerializerOptions SerializerOptions = JsonHelper.CreateOptions(
        writeIndented: false,
        camelCase: true,
        caseInsensitive: true,
        ignoreNullValues: true,
        maxDepth: 64
    );

    public bool SupportsChangeNotifications => _options.EnableNotifications;
    public bool SupportsVersioning => true;
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;

    public PostgresMetadataProvider(
        string connectionString,
        PostgresMetadataOptions options,
        ILogger<PostgresMetadataProvider> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _instanceId = Environment.MachineName ?? Guid.NewGuid().ToString();
        if (_options.SchemaName.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("SchemaName must be provided for Postgres metadata", nameof(options));
        }

        var schemaName = _options.SchemaName;
        var notificationChannel = _options.NotificationChannel.IsNullOrWhiteSpace()
            ? DefaultNotificationChannel
            : _options.NotificationChannel;

        if (_options.EnableNotifications && notificationChannel.IsNullOrWhiteSpace())
        {
            throw new ArgumentException(
                "NotificationChannel must be provided when notifications are enabled for Postgres metadata",
                nameof(options));
        }

        _options.NotificationChannel = notificationChannel;

        _quotedSchemaName = QuoteIdentifier(schemaName);
        _schemaPrefix = _quotedSchemaName + ".";
        _metadataSnapshotsTable = _schemaPrefix + QuoteIdentifier(MetadataSnapshotsTableName);
        _metadataChangeLogTable = _schemaPrefix + QuoteIdentifier(MetadataChangeLogTableName);
        _notifyFunctionIdentifier = _schemaPrefix + QuoteIdentifier(NotifyFunctionName);
        _metadataChangeTriggerIdentifier = QuoteIdentifier(MetadataChangeTriggerName);
        _notificationChannelIdentifier = QuoteIdentifier(notificationChannel);
        _notificationChannelLiteral = QuoteLiteral(notificationChannel);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        var initializationTask = Volatile.Read(ref _initializationTask);
        if (initializationTask is not null)
        {
            await initializationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            initializationTask = Volatile.Read(ref _initializationTask);
            if (initializationTask is not null)
            {
                await initializationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            initializationTask = InitializeCoreAsync(cancellationToken);
            Volatile.Write(ref _initializationTask, initializationTask);
        }
        finally
        {
            _initializationLock.Release();
        }

        try
        {
            await initializationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Volatile.Write(ref _initializationTask, null);
            throw;
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            if (_options.EnableNotifications)
            {
                EnsureNotificationListenerStarted();
            }

            _logger.LogInformation(
                "PostgresMetadataProvider initialized for instance {InstanceId} (notifications: {Notifications})",
                _instanceId, _options.EnableNotifications);
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailure(ex, "PostgresMetadataProvider initialization");
            throw;
        }
    }

    /// <summary>
    /// Creates the database schema if it doesn't exist.
    /// </summary>
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(new CommandDefinition(
            $"CREATE SCHEMA IF NOT EXISTS {_quotedSchemaName};",
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var createSnapshotsSql = $@"
            CREATE TABLE IF NOT EXISTS {_metadataSnapshotsTable} (
                id BIGSERIAL PRIMARY KEY,
                change_version BIGSERIAL NOT NULL,
                version_id VARCHAR(50) UNIQUE NOT NULL,
                snapshot_jsonb JSONB NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                created_by VARCHAR(100),
                is_active BOOLEAN NOT NULL DEFAULT false,
                label TEXT,
                size_bytes INTEGER,
                checksum VARCHAR(100)
            );

            CREATE INDEX IF NOT EXISTS idx_metadata_snapshots_active
                ON {_metadataSnapshotsTable}(is_active) WHERE is_active = true;

            CREATE INDEX IF NOT EXISTS idx_metadata_snapshots_version_id
                ON {_metadataSnapshotsTable}(version_id);

            CREATE INDEX IF NOT EXISTS idx_metadata_snapshots_created_at
                ON {_metadataSnapshotsTable}(created_at DESC);

            CREATE INDEX IF NOT EXISTS idx_metadata_snapshots_jsonb
                ON {_metadataSnapshotsTable} USING GIN (snapshot_jsonb);
        ";

        await connection.ExecuteAsync(new CommandDefinition(
            createSnapshotsSql,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var createChangeLogSql = $@"
            CREATE TABLE IF NOT EXISTS {_metadataChangeLogTable} (
                id BIGSERIAL PRIMARY KEY,
                version_id VARCHAR(50) NOT NULL,
                change_type VARCHAR(50) NOT NULL,
                changed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                changed_by VARCHAR(100),
                instance_id VARCHAR(100),
                size_bytes INTEGER,
                description TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_metadata_change_log_changed_at
                ON {_metadataChangeLogTable}(changed_at DESC);
        ";

        await connection.ExecuteAsync(new CommandDefinition(
            createChangeLogSql,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var createNotifySql = $@"
            CREATE OR REPLACE FUNCTION {_notifyFunctionIdentifier}()
            RETURNS TRIGGER AS $$
            BEGIN
                PERFORM pg_notify(
                    {_notificationChannelLiteral},
                    json_build_object(
                        'version_id', NEW.version_id,
                        'change_version', NEW.change_version,
                        'instance_id', NEW.created_by,
                        'timestamp', EXTRACT(EPOCH FROM NEW.created_at)
                    )::text
                );
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            DROP TRIGGER IF EXISTS {_metadataChangeTriggerIdentifier} ON {_metadataSnapshotsTable};

            CREATE TRIGGER {_metadataChangeTriggerIdentifier}
            AFTER INSERT OR UPDATE ON {_metadataSnapshotsTable}
            FOR EACH ROW
            WHEN (NEW.is_active = true)
            EXECUTE FUNCTION {_notifyFunctionIdentifier}();
        ";

        await connection.ExecuteAsync(new CommandDefinition(
            createNotifySql,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        _logger.LogDebug("PostgreSQL schema '{Schema}' ensured", _options.SchemaName);
    }

    private void EnsureNotificationListenerStarted()
    {
        if (Interlocked.CompareExchange(ref _notificationListenerStarted, 1, 0) != 0)
        {
            return;
        }

        _notificationLoopCts = new CancellationTokenSource();
        _notificationLoopTask = Task.Run(() => RunNotificationListenerAsync(_notificationLoopCts.Token));
    }

    private async Task RunNotificationListenerAsync(CancellationToken cancellationToken)
    {
        var retryDelay = TimeSpan.FromSeconds(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                connection.Notification += OnNotificationReceived;
                Volatile.Write(ref _notificationConnection, connection);

                await using (var command = new NpgsqlCommand($"LISTEN {_notificationChannelIdentifier}", connection))
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation(
                    "PostgreSQL LISTEN started on channel '{Channel}' for instance {InstanceId}",
                    _options.NotificationChannel, _instanceId);

                retryDelay = TimeSpan.FromSeconds(1);

                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await connection.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    connection.Notification -= OnNotificationReceived;
                    Volatile.Write(ref _notificationConnection, null);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PostgreSQL notification listener disconnected. Retrying in {Delay}.", retryDelay);

                try
                {
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var nextSeconds = Math.Min(retryDelay.TotalSeconds * 2, 30);
                retryDelay = TimeSpan.FromSeconds(nextSeconds);
            }
        }
    }

    /// <summary>
    /// Loads the current active metadata snapshot from PostgreSQL.
    /// </summary>
    public async Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var command = new CommandDefinition($@"
                SELECT snapshot_jsonb::text, change_version
                FROM {_metadataSnapshotsTable}
                WHERE is_active = true
                ORDER BY change_version DESC
                LIMIT 1
            ", cancellationToken: cancellationToken);

            var result = await connection
                .QueryFirstOrDefaultAsync<(string SnapshotJsonb, long ChangeVersion)>(command)
                .ConfigureAwait(false);

            if (result == default)
            {
                throw new InvalidOperationException(
                    "No active metadata snapshot found in PostgreSQL. " +
                    "Initialize metadata using SaveAsync() or migrate from another provider.");
            }

            _lastChangeVersion = result.ChangeVersion;

            var snapshot = JsonSerializer.Deserialize<MetadataSnapshot>(result.SnapshotJsonb, SerializerOptions);
            if (snapshot is null)
            {
                throw new InvalidOperationException("Failed to deserialize metadata snapshot from PostgreSQL");
            }

            _logger.LogDebug(
                "Loaded metadata snapshot from PostgreSQL (change version: {ChangeVersion}, size: {Size} bytes)",
                result.ChangeVersion, result.SnapshotJsonb.Length);

            return snapshot;
        }
        catch (NpgsqlException ex)
        {
            return ExceptionHandler.ExecuteWithMapping<MetadataSnapshot>(
                () => throw ex,
                e => new InvalidOperationException("PostgreSQL connection failed. Cannot load metadata.", e),
                _logger,
                "PostgreSQL metadata load");
        }
    }

    /// <summary>
    /// Saves a complete metadata snapshot to PostgreSQL with versioning and audit trail.
    /// Triggers NOTIFY to all listening instances for immediate synchronization.
    /// </summary>
    public async Task SaveAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            var versionId = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow;
            var checksum = ComputeChecksum(json);

            // Use PostgreSQL transaction for atomic update
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // 1. Deactivate previous active snapshot
                await connection.ExecuteAsync(new CommandDefinition(
                    $"UPDATE {_metadataSnapshotsTable} SET is_active = false WHERE is_active = true",
                    transaction: transaction,
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

                // 2. Insert new active snapshot
                var insertSnapshotCommand = new CommandDefinition($@"
                    INSERT INTO {_metadataSnapshotsTable}
                        (version_id, snapshot_jsonb, created_at, created_by, is_active, label, size_bytes, checksum)
                    VALUES
                        (@VersionId, @SnapshotJsonb::jsonb, @CreatedAt, @CreatedBy, true, @Label, @SizeBytes, @Checksum)
                ",
                    new
                    {
                        VersionId = versionId,
                        SnapshotJsonb = json,
                        CreatedAt = timestamp,
                        CreatedBy = _instanceId,
                        Label = $"Auto-save from {_instanceId}",
                        SizeBytes = json.Length,
                        Checksum = checksum
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                await connection.ExecuteAsync(insertSnapshotCommand).ConfigureAwait(false);

                // 3. Log change
                var insertLogCommand = new CommandDefinition($@"
                    INSERT INTO {_metadataChangeLogTable}
                        (version_id, change_type, changed_at, changed_by, instance_id, size_bytes, description)
                    VALUES
                        (@VersionId, @ChangeType, @ChangedAt, @ChangedBy, @InstanceId, @SizeBytes, @Description)
                ",
                    new
                    {
                        VersionId = versionId,
                        ChangeType = "full_snapshot",
                        ChangedAt = timestamp,
                        ChangedBy = _instanceId,
                        InstanceId = _instanceId,
                        SizeBytes = json.Length,
                        Description = $"Auto-save from {_instanceId}"
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken);

                await connection.ExecuteAsync(insertLogCommand).ConfigureAwait(false);

                // 4. Clean up old versions if max versions exceeded
                if (_options.MaxVersions > 0)
                {
                    var cleanupCommand = new CommandDefinition($@"
                        DELETE FROM {_metadataSnapshotsTable}
                        WHERE id NOT IN (
                            SELECT id FROM {_metadataSnapshotsTable}
                            ORDER BY created_at DESC
                            LIMIT @MaxVersions
                        )
                    ", new { _options.MaxVersions }, transaction: transaction, cancellationToken: cancellationToken);

                    await connection.ExecuteAsync(cleanupCommand).ConfigureAwait(false);
                }

                // 5. Commit transaction (triggers NOTIFY via trigger)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation(
                    "Saved metadata snapshot to PostgreSQL (version: {VersionId}, size: {Size} bytes)",
                    versionId, json.Length);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (NpgsqlException ex)
        {
            ExceptionHandler.ExecuteWithMapping(
                () => throw ex,
                e => new InvalidOperationException("PostgreSQL connection failed. Cannot save metadata.", e),
                _logger,
                "PostgreSQL metadata save");
        }
    }

    /// <summary>
    /// Updates a single layer definition. For full metadata updates, use SaveAsync().
    /// </summary>
    public async Task UpdateLayerAsync(LayerDefinition layer, CancellationToken cancellationToken = default)
    {
        if (layer is null)
        {
            throw new ArgumentNullException(nameof(layer));
        }

        // Load current snapshot, update layer, save
        var snapshot = await LoadAsync(cancellationToken);
        var layers = snapshot.Layers.ToList();
        var index = layers.FindIndex(l => string.Equals(l.Id, layer.Id, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            layers[index] = layer;
        }
        else
        {
            layers.Add(layer);
        }

        var updatedSnapshot = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services,
            layers,
            snapshot.RasterDatasets,
            snapshot.Styles,
            snapshot.LayerGroups,
            snapshot.Server);

        await SaveAsync(updatedSnapshot, cancellationToken);
    }

    /// <summary>
    /// Creates a named version of current metadata state for rollback.
    /// </summary>
    public async Task<MetadataVersion> CreateVersionAsync(string? label = null, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken).ConfigureAwait(false);

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var versionId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        var checksum = ComputeChecksum(json);

        var insertCommand = new CommandDefinition($@"
            INSERT INTO {_metadataSnapshotsTable}
                (version_id, snapshot_jsonb, created_at, created_by, is_active, label, size_bytes, checksum)
            VALUES
                (@VersionId, @SnapshotJsonb::jsonb, @CreatedAt, @CreatedBy, false, @Label, @SizeBytes, @Checksum)
        ",
            new
            {
                VersionId = versionId,
                SnapshotJsonb = json,
                CreatedAt = timestamp,
                CreatedBy = _instanceId,
                Label = label ?? $"Manual snapshot from {_instanceId}",
                SizeBytes = json.Length,
                Checksum = checksum
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(insertCommand).ConfigureAwait(false);

        _logger.LogInformation("Created metadata version {VersionId} with label '{Label}'", versionId, label);

        return new MetadataVersion(versionId, timestamp, label, json.Length, checksum);
    }

    /// <summary>
    /// Restores metadata from a previously created version.
    /// </summary>
    public async Task RestoreVersionAsync(string versionId, CancellationToken cancellationToken = default)
    {
        if (versionId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Version ID must be provided", nameof(versionId));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition($@"
            SELECT snapshot_jsonb::text
            FROM {_metadataSnapshotsTable}
            WHERE version_id = @VersionId
        ", new { VersionId = versionId }, cancellationToken: cancellationToken);

        var json = await connection.QueryFirstOrDefaultAsync<string>(command).ConfigureAwait(false);

        if (json.IsNullOrEmpty())
        {
            throw new InvalidOperationException($"Version {versionId} not found in PostgreSQL");
        }

        var snapshot = JsonSerializer.Deserialize<MetadataSnapshot>(json, SerializerOptions);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Failed to deserialize version {versionId}");
        }

        _logger.LogWarning("Restoring metadata from version {VersionId}", versionId);
        await SaveAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lists all available metadata versions, newest first.
    /// </summary>
    public async Task<IReadOnlyList<MetadataVersion>> ListVersionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition($@"
            SELECT
                version_id AS {nameof(MetadataVersion.Id)},
                created_at AS {nameof(MetadataVersion.CreatedAt)},
                label AS {nameof(MetadataVersion.Label)},
                size_bytes AS {nameof(MetadataVersion.SizeBytes)},
                checksum AS {nameof(MetadataVersion.Checksum)}
            FROM {_metadataSnapshotsTable}
            ORDER BY created_at DESC
        ", cancellationToken: cancellationToken);

        var versions = await connection.QueryAsync<MetadataVersion>(command).ConfigureAwait(false);

        return versions.ToList();
    }

    /// <summary>
    /// Reloads metadata from PostgreSQL (typically called after receiving NOTIFY).
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reloading metadata from PostgreSQL");
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        MetadataChanged?.Invoke(this, new MetadataChangedEventArgs("postgres-reload"));
    }

    /// <summary>
    /// Handles NOTIFY notifications from PostgreSQL trigger.
    /// </summary>
    private void OnNotificationReceived(object? sender, NpgsqlNotificationEventArgs e)
    {
        if (_disposed) return;

        try
        {
            var notification = JsonSerializer.Deserialize<Dictionary<string, object>>(e.Payload);
            if (notification is null) return;

            var instanceId = notification.TryGetValue("instance_id", out var instanceObj)
                ? instanceObj.ToString()
                : "unknown";

            // Don't reload if we were the source of the change
            if (instanceId == _instanceId)
            {
                _logger.LogDebug("Ignoring self-generated NOTIFY notification");
                return;
            }

            _logger.LogInformation(
                "Received PostgreSQL NOTIFY from instance {InstanceId}, triggering reload",
                instanceId);

            // Trigger reload asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogOperationFailure(ex, "Reload metadata after NOTIFY");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailure(ex, "Process NOTIFY notification", e.Payload);
        }
    }

    private static string ComputeChecksum(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _notificationLoopCts?.Cancel();

        if (_notificationLoopTask is not null)
        {
            try
            {
                await _notificationLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal during shutdown.
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "PostgreSQL notification listener stopped with error during disposal");
            }
        }

        _notificationLoopCts?.Dispose();
        _notificationConnection?.Dispose();
        _initializationLock.Dispose();
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (identifier.IsNullOrEmpty())
        {
            throw new ArgumentException("Identifier must be provided", nameof(identifier));
        }

        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }

    private static string QuoteLiteral(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return "'" + value.Replace("'", "''") + "'";
    }
}

/// <summary>
/// Configuration options for PostgresMetadataProvider.
/// </summary>
public sealed class PostgresMetadataOptions
{
    /// <summary>
    /// PostgreSQL schema name for metadata tables. Default: "honua".
    /// </summary>
    public string SchemaName { get; set; } = "honua";

    /// <summary>
    /// PostgreSQL NOTIFY/LISTEN channel name. Default: "honua_metadata_changes".
    /// </summary>
    public string NotificationChannel { get; set; } = "honua_metadata_changes";

    /// <summary>
    /// Whether to enable NOTIFY/LISTEN for change notifications. Default: true.
    /// Set to false to disable real-time sync (not recommended for production).
    /// </summary>
    public bool EnableNotifications { get; set; } = true;

    /// <summary>
    /// Maximum number of versions to retain. Older versions are auto-deleted. Default: 100.
    /// Set to -1 for unlimited (not recommended).
    /// </summary>
    public int MaxVersions { get; set; } = 100;

    /// <summary>
    /// Whether to enable detailed logging of NOTIFY messages. Default: false.
    /// </summary>
    public bool VerboseLogging { get; set; }
}
