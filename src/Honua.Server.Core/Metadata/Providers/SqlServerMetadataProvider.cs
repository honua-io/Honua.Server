// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Core.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata.Providers;

/// <summary>
/// Metadata provider backed by SQL Server with change detection via polling or Service Broker.
/// Suitable for enterprise customers who want SQL Server-based metadata storage.
///
/// Features:
/// - SQL Server JSON columns for metadata storage
/// - Versioning and rollback support via temporal tables
/// - Audit trail
/// - Change detection via polling (Service Broker optional)
/// - Transactions and ACID guarantees
///
/// Requirements:
/// - SQL Server 2016+ (for JSON support)
/// - SQL Server 2017+ recommended (for better JSON performance)
/// </summary>
public sealed class SqlServerMetadataProvider : IMutableMetadataProvider, IReloadableMetadataProvider, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerMetadataProvider> _logger;
    private readonly SqlServerMetadataOptions _options;
    private readonly string _instanceId;
    private readonly Timer? _pollTimer;
    private long _lastChangeVersion;
    private bool _disposed;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public bool SupportsChangeNotifications => _options.EnablePolling;
    public bool SupportsVersioning => true;
    public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;

    public SqlServerMetadataProvider(
        string connectionString,
        SqlServerMetadataOptions options,
        ILogger<SqlServerMetadataProvider> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _instanceId = Environment.MachineName ?? Guid.NewGuid().ToString();

        // Initialize database schema if needed
        _ = EnsureSchemaAsync();

        // Start polling for changes if enabled
        if (_options.EnablePolling && _options.PollingIntervalSeconds > 0)
        {
            _pollTimer = new Timer(
                OnPollTimerCallback,
                null,
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds),
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds));

            _logger.LogInformation(
                "SqlServerMetadataProvider initialized with polling every {Interval}s for instance {InstanceId}",
                _options.PollingIntervalSeconds, _instanceId);
        }
        else
        {
            _logger.LogInformation(
                "SqlServerMetadataProvider initialized without polling for instance {InstanceId}",
                _instanceId);
        }
    }

    /// <summary>
    /// Loads the current active metadata snapshot from SQL Server.
    /// </summary>
    public async Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = @"
                SELECT TOP 1 SnapshotJson, ChangeVersion
                FROM honua.MetadataSnapshots
                WHERE IsActive = 1
                ORDER BY CreatedAt DESC";

            var result = await connection.QueryFirstOrDefaultAsync<(string SnapshotJson, long ChangeVersion)>(sql);

            if (result.SnapshotJson.IsNullOrEmpty())
            {
                throw new InvalidOperationException(
                    "No active metadata snapshot found in SQL Server. " +
                    "Initialize metadata using SaveAsync() or migrate from file-based provider.");
            }

            _lastChangeVersion = result.ChangeVersion;

            var snapshot = JsonSerializer.Deserialize<MetadataSnapshot>(result.SnapshotJson, SerializerOptions);
            if (snapshot is null)
            {
                throw new InvalidOperationException("Failed to deserialize metadata snapshot from SQL Server");
            }

            _logger.LogDebug("Loaded metadata snapshot from SQL Server (version: {Version})", _lastChangeVersion);
            return snapshot;
        }
        catch (SqlException ex)
        {
            return ExceptionHandler.ExecuteWithMapping<MetadataSnapshot>(
                () => throw ex,
                e => new InvalidOperationException("SQL Server connection failed. Cannot load metadata.", e),
                _logger,
                "SQL Server metadata load");
        }
    }

    /// <summary>
    /// Saves a complete metadata snapshot to SQL Server with versioning and audit trail.
    /// </summary>
    public async Task SaveAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            var versionId = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow;

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Deactivate current active snapshot
                await connection.ExecuteAsync(
                    "UPDATE honua.MetadataSnapshots SET IsActive = 0 WHERE IsActive = 1",
                    transaction: transaction);

                // 2. Insert new snapshot
                var changeVersion = await connection.QuerySingleAsync<long>(@"
                    INSERT INTO honua.MetadataSnapshots
                        (VersionId, SnapshotJson, CreatedAt, CreatedBy, IsActive, Label, SizeBytes, Checksum)
                    OUTPUT INSERTED.ChangeVersion
                    VALUES
                        (@VersionId, @Json, @CreatedAt, @CreatedBy, 1, @Label, @SizeBytes, @Checksum)",
                    new
                    {
                        VersionId = versionId,
                        Json = json,
                        CreatedAt = timestamp,
                        CreatedBy = _instanceId,
                        Label = $"Auto-save from {_instanceId}",
                        SizeBytes = json.Length,
                        Checksum = ComputeChecksum(json)
                    },
                    transaction: transaction);

                // 3. Log change
                await connection.ExecuteAsync(@"
                    INSERT INTO honua.MetadataChangeLog
                        (VersionId, Timestamp, Instance, ChangeType, SizeBytes)
                    VALUES
                        (@VersionId, @Timestamp, @Instance, @ChangeType, @SizeBytes)",
                    new
                    {
                        VersionId = versionId,
                        Timestamp = timestamp,
                        Instance = _instanceId,
                        ChangeType = "full_snapshot",
                        SizeBytes = json.Length
                    },
                    transaction: transaction);

                // 4. Clean up old versions if limit exceeded
                if (_options.MaxVersions > 0)
                {
                    await connection.ExecuteAsync(@"
                        DELETE FROM honua.MetadataSnapshots
                        WHERE VersionId IN (
                            SELECT VersionId
                            FROM honua.MetadataSnapshots
                            WHERE IsActive = 0
                            ORDER BY CreatedAt DESC
                            OFFSET @MaxVersions ROWS
                        )",
                        new { MaxVersions = _options.MaxVersions },
                        transaction: transaction);
                }

                await transaction.CommitAsync(cancellationToken);

                _lastChangeVersion = changeVersion;

                _logger.LogInformation(
                    "Saved metadata snapshot to SQL Server (version: {VersionId}, changeVersion: {ChangeVersion}, size: {Size} bytes)",
                    versionId, changeVersion, json.Length);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (SqlException ex)
        {
            ExceptionHandler.ExecuteWithMapping(
                () => throw ex,
                e => new InvalidOperationException("SQL Server connection failed. Cannot save metadata.", e),
                _logger,
                "SQL Server metadata save");
        }
    }

    /// <summary>
    /// Updates a single layer definition.
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
            snapshot.Server);

        await SaveAsync(updatedSnapshot, cancellationToken);
    }

    /// <summary>
    /// Creates a named version of current metadata state for rollback.
    /// </summary>
    public async Task<MetadataVersion> CreateVersionAsync(string? label = null, CancellationToken cancellationToken = default)
    {
        var snapshot = await LoadAsync(cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var versionId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;
        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);

        await connection.ExecuteAsync(@"
            INSERT INTO honua.MetadataSnapshots
                (VersionId, SnapshotJson, CreatedAt, CreatedBy, IsActive, Label, SizeBytes, Checksum)
            VALUES
                (@VersionId, @Json, @CreatedAt, @CreatedBy, 0, @Label, @SizeBytes, @Checksum)",
            new
            {
                VersionId = versionId,
                Json = json,
                CreatedAt = timestamp,
                CreatedBy = _instanceId,
                Label = label ?? $"Manual snapshot from {_instanceId}",
                SizeBytes = json.Length,
                Checksum = ComputeChecksum(json)
            });

        _logger.LogInformation("Created metadata version {VersionId} with label '{Label}'", versionId, label);

        return new MetadataVersion(versionId, timestamp, label, json.Length, ComputeChecksum(json));
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

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var json = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT SnapshotJson FROM honua.MetadataSnapshots WHERE VersionId = @VersionId",
            new { VersionId = versionId });

        if (json.IsNullOrEmpty())
        {
            throw new InvalidOperationException($"Version {versionId} not found in SQL Server");
        }

        var snapshot = JsonSerializer.Deserialize<MetadataSnapshot>(json, SerializerOptions);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Failed to deserialize version {versionId}");
        }

        _logger.LogWarning("Restoring metadata from version {VersionId}", versionId);
        await SaveAsync(snapshot, cancellationToken);
    }

    /// <summary>
    /// Lists all available metadata versions, newest first.
    /// </summary>
    public async Task<IReadOnlyList<MetadataVersion>> ListVersionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var versions = await connection.QueryAsync<MetadataVersion>(@"
            SELECT VersionId AS Id, CreatedAt, Label, SizeBytes, Checksum
            FROM honua.MetadataSnapshots
            ORDER BY CreatedAt DESC");

        return versions.ToList();
    }

    /// <summary>
    /// Reloads metadata from SQL Server.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Reloading metadata from SQL Server");
        await LoadAsync(cancellationToken);
        MetadataChanged?.Invoke(this, new MetadataChangedEventArgs("sql-reload"));
    }

    /// <summary>
    /// Ensures the required database schema exists.
    /// </summary>
    private async Task EnsureSchemaAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Create schema
            await connection.ExecuteAsync("IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'honua') EXEC('CREATE SCHEMA honua')");

            // Create tables
            await connection.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'honua.MetadataSnapshots'))
                BEGIN
                    CREATE TABLE honua.MetadataSnapshots (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        ChangeVersion AS CAST(Id AS BIGINT) PERSISTED,
                        VersionId NVARCHAR(50) NOT NULL UNIQUE,
                        SnapshotJson NVARCHAR(MAX) NOT NULL,
                        CreatedAt DATETIMEOFFSET NOT NULL,
                        CreatedBy NVARCHAR(255) NULL,
                        IsActive BIT NOT NULL DEFAULT 0,
                        Label NVARCHAR(500) NULL,
                        SizeBytes BIGINT NULL,
                        Checksum NVARCHAR(100) NULL,
                        INDEX IX_IsActive (IsActive, CreatedAt DESC),
                        INDEX IX_CreatedAt (CreatedAt DESC)
                    );
                END

                IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'honua.MetadataChangeLog'))
                BEGIN
                    CREATE TABLE honua.MetadataChangeLog (
                        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                        VersionId NVARCHAR(50) NOT NULL,
                        Timestamp DATETIMEOFFSET NOT NULL,
                        Instance NVARCHAR(255) NULL,
                        ChangeType NVARCHAR(50) NULL,
                        SizeBytes BIGINT NULL,
                        INDEX IX_Timestamp (Timestamp DESC)
                    );
                END");

            _logger.LogInformation("SQL Server metadata schema verified/created");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure SQL Server schema (may already exist or require permissions)");
        }
    }

    /// <summary>
    /// Polling callback to check for changes in SQL Server.
    /// </summary>
    private void OnPollTimerCallback(object? state)
    {
        if (_disposed) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                var latestVersion = await connection.QueryFirstOrDefaultAsync<long?>(
                    "SELECT MAX(ChangeVersion) FROM honua.MetadataSnapshots WHERE IsActive = 1");

                if (latestVersion.HasValue && latestVersion.Value > _lastChangeVersion)
                {
                    _logger.LogInformation(
                        "Detected metadata change in SQL Server (version {Old} -> {New}), triggering reload",
                        _lastChangeVersion, latestVersion.Value);

                    await ReloadAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SQL Server metadata polling");
            }
        });
    }

    private static string ComputeChecksum(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        _pollTimer?.Dispose();
        _logger.LogInformation("SqlServerMetadataProvider disposed");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Configuration options for SqlServerMetadataProvider.
/// </summary>
public sealed class SqlServerMetadataOptions
{
    /// <summary>
    /// Whether to enable polling for changes. Default: true.
    /// </summary>
    public bool EnablePolling { get; set; } = true;

    /// <summary>
    /// Polling interval in seconds. Default: 30 seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of versions to retain. Older versions are auto-deleted. Default: 100.
    /// Set to -1 for unlimited (not recommended).
    /// </summary>
    public int MaxVersions { get; set; } = 100;

    /// <summary>
    /// Whether to enable detailed logging. Default: false.
    /// </summary>
    public bool VerboseLogging { get; set; }
}
