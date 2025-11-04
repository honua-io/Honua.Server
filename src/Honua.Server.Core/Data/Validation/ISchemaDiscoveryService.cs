// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Validation;

/// <summary>
/// Service for discovering database schema and generating metadata from existing tables.
/// </summary>
public interface ISchemaDiscoveryService
{
    /// <summary>
    /// Discovers the schema of a database table and returns field definitions.
    /// </summary>
    /// <param name="dataSource">The data source to query.</param>
    /// <param name="tableName">The table name (schema.table or just table).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered table schema information.</returns>
    Task<TableSchemaInfo> DiscoverTableSchemaAsync(
        DataSourceDefinition dataSource,
        string tableName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs a layer's field definitions with the current database schema.
    /// </summary>
    /// <param name="layer">The layer to sync.</param>
    /// <param name="dataSource">The data source for the layer.</param>
    /// <param name="options">Sync options (what to add/remove/update).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated layer definition and sync results.</returns>
    Task<SchemaSyncResult> SyncLayerFieldsAsync(
        LayerDefinition layer,
        DataSourceDefinition dataSource,
        SchemaSyncOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a discovered table schema.
/// </summary>
public sealed class TableSchemaInfo
{
    /// <summary>
    /// Schema name (e.g., "public").
    /// </summary>
    public required string Schema { get; init; }

    /// <summary>
    /// Table name.
    /// </summary>
    public required string Table { get; init; }

    /// <summary>
    /// Discovered columns.
    /// </summary>
    public required List<DiscoveredColumn> Columns { get; init; }

    /// <summary>
    /// Primary key column name, if detected.
    /// </summary>
    public string? PrimaryKey { get; init; }

    /// <summary>
    /// Geometry column name, if detected.
    /// </summary>
    public string? GeometryColumn { get; init; }

    /// <summary>
    /// Geometry type (e.g., "Point", "LineString"), if detected.
    /// </summary>
    public string? GeometryType { get; init; }

    /// <summary>
    /// SRID of geometry column, if detected.
    /// </summary>
    public int? Srid { get; init; }
}

/// <summary>
/// Information about a discovered database column.
/// </summary>
public sealed class DiscoveredColumn
{
    /// <summary>
    /// Column name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Database data type (e.g., "integer", "text", "geometry").
    /// </summary>
    public required string DbType { get; init; }

    /// <summary>
    /// Whether the column allows null values.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Whether this is a primary key column.
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Suggested Honua data type for metadata.
    /// </summary>
    public string? SuggestedDataType { get; init; }

    /// <summary>
    /// Suggested storage type for metadata.
    /// </summary>
    public string? SuggestedStorageType { get; init; }
}

/// <summary>
/// Options for syncing layer metadata with database schema.
/// </summary>
public sealed class SchemaSyncOptions
{
    /// <summary>
    /// Add fields that exist in database but not in metadata (default: true).
    /// </summary>
    public bool AddMissingFields { get; set; } = true;

    /// <summary>
    /// Remove fields that exist in metadata but not in database (default: false).
    /// Enabling this can break existing queries if fields were intentionally added.
    /// </summary>
    public bool RemoveOrphanedFields { get; set; } = false;

    /// <summary>
    /// Update data types of existing fields if they don't match database (default: true).
    /// </summary>
    public bool UpdateFieldTypes { get; set; } = true;

    /// <summary>
    /// Update nullable property of fields if it doesn't match database (default: true).
    /// </summary>
    public bool UpdateNullability { get; set; } = true;

    /// <summary>
    /// Preserve field aliases, descriptions, and other custom metadata (default: true).
    /// </summary>
    public bool PreserveCustomMetadata { get; set; } = true;
}

/// <summary>
/// Result of syncing layer metadata with database schema.
/// </summary>
public sealed class SchemaSyncResult
{
    /// <summary>
    /// The updated layer definition with synced fields.
    /// </summary>
    public required LayerDefinition UpdatedLayer { get; init; }

    /// <summary>
    /// Fields that were added to metadata.
    /// </summary>
    public List<string> AddedFields { get; init; } = new();

    /// <summary>
    /// Fields that were removed from metadata.
    /// </summary>
    public List<string> RemovedFields { get; init; } = new();

    /// <summary>
    /// Fields whose types were updated.
    /// </summary>
    public List<string> UpdatedFields { get; init; } = new();

    /// <summary>
    /// Whether any changes were made.
    /// </summary>
    public bool HasChanges => AddedFields.Count > 0 || RemovedFields.Count > 0 || UpdatedFields.Count > 0;

    /// <summary>
    /// Warnings encountered during sync.
    /// </summary>
    public List<string> Warnings { get; init; } = new();
}
