// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Discovery;

/// <summary>
/// Service for discovering database tables and automatically creating metadata.
/// Inspired by pg_tileserv's zero-configuration approach.
/// </summary>
public interface ITableDiscoveryService
{
    /// <summary>
    /// Discovers all spatial tables in the database that match the configured criteria.
    /// </summary>
    /// <param name="dataSourceId">The data source to discover tables from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of discovered tables with metadata.</returns>
    Task<IEnumerable<DiscoveredTable>> DiscoverTablesAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers a specific table by name.
    /// </summary>
    /// <param name="dataSourceId">The data source to discover from.</param>
    /// <param name="tableName">The table name (schema.table or just table).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discovered table metadata if found.</returns>
    Task<DiscoveredTable?> DiscoverTableAsync(
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata for a discovered spatial table.
/// </summary>
public sealed class DiscoveredTable
{
    /// <summary>
    /// Database schema name (e.g., "public").
    /// </summary>
    public required string Schema { get; init; }

    /// <summary>
    /// Table name.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Full qualified name (schema.table).
    /// </summary>
    public string QualifiedName => $"{Schema}.{TableName}";

    /// <summary>
    /// Geometry column name.
    /// </summary>
    public required string GeometryColumn { get; init; }

    /// <summary>
    /// SRID of the geometry column.
    /// </summary>
    public required int SRID { get; init; }

    /// <summary>
    /// Geometry type (Point, LineString, Polygon, etc.).
    /// </summary>
    public required string GeometryType { get; init; }

    /// <summary>
    /// Primary key column name.
    /// </summary>
    public required string PrimaryKeyColumn { get; init; }

    /// <summary>
    /// All columns in the table (excluding geometry).
    /// </summary>
    public required Dictionary<string, ColumnInfo> Columns { get; init; }

    /// <summary>
    /// Whether the table has a spatial index on the geometry column.
    /// </summary>
    public bool HasSpatialIndex { get; init; }

    /// <summary>
    /// Estimated row count (from statistics).
    /// </summary>
    public long EstimatedRowCount { get; init; }

    /// <summary>
    /// Spatial extent of the data (bounding box).
    /// </summary>
    public Envelope? Extent { get; init; }

    /// <summary>
    /// Table description/comment if available.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Information about a table column.
/// </summary>
public sealed class ColumnInfo
{
    /// <summary>
    /// Column name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Honua data type (string, int32, etc.).
    /// </summary>
    public required string DataType { get; init; }

    /// <summary>
    /// Database storage type.
    /// </summary>
    public required string StorageType { get; init; }

    /// <summary>
    /// Whether the column allows nulls.
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Whether this is the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>
    /// Friendly display name (humanized from column name).
    /// </summary>
    public string? Alias { get; init; }
}

/// <summary>
/// Spatial extent (bounding box).
/// </summary>
public sealed class Envelope
{
    public required double MinX { get; init; }
    public required double MinY { get; init; }
    public required double MaxX { get; init; }
    public required double MaxY { get; init; }

    public double[] ToArray() => new[] { MinX, MinY, MaxX, MaxY };
}
