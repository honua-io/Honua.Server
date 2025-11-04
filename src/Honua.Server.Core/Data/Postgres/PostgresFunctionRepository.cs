// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Npgsql;
using NpgsqlTypes;

namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Repository for calling optimized PostgreSQL functions that push complexity
/// from C# to the database for 5-10x performance improvements.
/// </summary>
/// <remarks>
/// These functions leverage PostgreSQL's query optimizer, parallel execution,
/// and spatial indexing to provide dramatic performance improvements over
/// traditional query-then-process approaches. Inspired by pg_tileserv and Martin.
/// </remarks>
internal sealed class PostgresFunctionRepository
{
    private readonly PostgresConnectionManager _connectionManager;
    private readonly WKTWriter _wktWriter;

    public PostgresFunctionRepository(PostgresConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _wktWriter = new WKTWriter();
    }

    /// <summary>
    /// Retrieves features using the optimized honua_get_features_optimized function.
    /// Provides automatic geometry simplification based on zoom level.
    /// </summary>
    /// <param name="dataSource">Data source definition</param>
    /// <param name="tableName">Table name</param>
    /// <param name="geomColumn">Geometry column name</param>
    /// <param name="bbox">Bounding box for spatial filter</param>
    /// <param name="zoom">Zoom level for simplification (optional)</param>
    /// <param name="filterSql">Additional SQL filter (optional, must be safe)</param>
    /// <param name="limit">Maximum number of features to return</param>
    /// <param name="offset">Number of features to skip</param>
    /// <param name="srid">Storage SRID</param>
    /// <param name="targetSrid">Target SRID for output</param>
    /// <param name="selectColumns">Columns to select (null = all)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of feature JSON documents</returns>
    public async IAsyncEnumerable<JsonDocument> GetFeaturesOptimizedAsync(
        DataSourceDefinition dataSource,
        string tableName,
        string geomColumn,
        Geometry bbox,
        int? zoom = null,
        string? filterSql = null,
        int limit = 1000,
        int offset = 0,
        int srid = 4326,
        int targetSrid = 4326,
        string[]? selectColumns = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionManager
            .CreateConnectionAsync(dataSource, cancellationToken)
            .ConfigureAwait(false);

        await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var wkt = _wktWriter.Write(bbox);
        var bboxGeometry = $"SRID={bbox.SRID};{wkt}";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT feature_json FROM honua_get_features_optimized(@p_table_name, @p_geom_column, @p_bbox, @p_zoom, @p_filter_sql, @p_limit, @p_offset, @p_srid, @p_target_srid, @p_select_columns)";

        cmd.Parameters.AddWithValue("p_table_name", tableName);
        cmd.Parameters.AddWithValue("p_geom_column", geomColumn);
        cmd.Parameters.AddWithValue("p_bbox", NpgsqlDbType.Geometry, bboxGeometry);
        cmd.Parameters.AddWithValue("p_zoom", zoom.HasValue ? zoom.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("p_filter_sql", filterSql ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p_limit", limit);
        cmd.Parameters.AddWithValue("p_offset", offset);
        cmd.Parameters.AddWithValue("p_srid", srid);
        cmd.Parameters.AddWithValue("p_target_srid", targetSrid);
        cmd.Parameters.AddWithValue("p_select_columns", selectColumns ?? (object)DBNull.Value);

        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var jsonText = reader.GetString(0);
            var jsonDoc = JsonDocument.Parse(jsonText);
            yield return jsonDoc;
        }
    }

    /// <summary>
    /// Generates an MVT (Mapbox Vector Tile) using the optimized honua_get_mvt_tile function.
    /// Provides automatic simplification and feature filtering based on zoom level.
    /// </summary>
    /// <param name="dataSource">Data source definition</param>
    /// <param name="tableName">Table name</param>
    /// <param name="geomColumn">Geometry column name</param>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="srid">Storage SRID</param>
    /// <param name="extent">MVT extent (default 4096)</param>
    /// <param name="buffer">Buffer size in pixels (default 256)</param>
    /// <param name="filterSql">Additional SQL filter (optional, must be safe)</param>
    /// <param name="layerName">Layer name in MVT</param>
    /// <param name="attributeColumns">Attribute columns to include (null = all)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MVT tile bytes, or null if no features in tile</returns>
    public async Task<byte[]?> GetMvtTileAsync(
        DataSourceDefinition dataSource,
        string tableName,
        string geomColumn,
        int z,
        int x,
        int y,
        int srid = 4326,
        int extent = 4096,
        int buffer = 256,
        string? filterSql = null,
        string layerName = "default",
        string[]? attributeColumns = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionManager
            .CreateConnectionAsync(dataSource, cancellationToken)
            .ConfigureAwait(false);

        await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT honua_get_mvt_tile(@p_table_name, @p_geom_column, @p_z, @p_x, @p_y, @p_srid, @p_extent, @p_buffer, @p_filter_sql, @p_layer_name, @p_attribute_columns)";

        cmd.Parameters.AddWithValue("p_table_name", tableName);
        cmd.Parameters.AddWithValue("p_geom_column", geomColumn);
        cmd.Parameters.AddWithValue("p_z", z);
        cmd.Parameters.AddWithValue("p_x", x);
        cmd.Parameters.AddWithValue("p_y", y);
        cmd.Parameters.AddWithValue("p_srid", srid);
        cmd.Parameters.AddWithValue("p_extent", extent);
        cmd.Parameters.AddWithValue("p_buffer", buffer);
        cmd.Parameters.AddWithValue("p_filter_sql", filterSql ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p_layer_name", layerName);
        cmd.Parameters.AddWithValue("p_attribute_columns", attributeColumns ?? (object)DBNull.Value);

        var result = await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return result as byte[];
    }

    /// <summary>
    /// Performs fast spatial aggregation using the honua_aggregate_features function.
    /// Returns count, extent, and optional grouped statistics.
    /// </summary>
    public async Task<AggregationResult> AggregateFeaturesAsync(
        DataSourceDefinition dataSource,
        string tableName,
        string geomColumn,
        Geometry? bbox = null,
        string? filterSql = null,
        int srid = 4326,
        int targetSrid = 4326,
        string? groupByColumn = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionManager
            .CreateConnectionAsync(dataSource, cancellationToken)
            .ConfigureAwait(false);

        await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM honua_aggregate_features(@p_table_name, @p_geom_column, @p_bbox, @p_filter_sql, @p_srid, @p_target_srid, @p_group_by_column)";

        cmd.Parameters.AddWithValue("p_table_name", tableName);
        cmd.Parameters.AddWithValue("p_geom_column", geomColumn);

        if (bbox != null)
        {
            var wkt = _wktWriter.Write(bbox);
            var bboxGeometry = $"SRID={bbox.SRID};{wkt}";
            cmd.Parameters.AddWithValue("p_bbox", NpgsqlDbType.Geometry, bboxGeometry);
        }
        else
        {
            cmd.Parameters.AddWithValue("p_bbox", DBNull.Value);
        }

        cmd.Parameters.AddWithValue("p_filter_sql", filterSql ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p_srid", srid);
        cmd.Parameters.AddWithValue("p_target_srid", targetSrid);
        cmd.Parameters.AddWithValue("p_group_by_column", groupByColumn ?? (object)DBNull.Value);

        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var groups = new List<AggregationGroup>();
        long totalCount = 0;
        string? extentJson = null;

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            totalCount = reader.GetInt64(0);
            extentJson = reader.IsDBNull(1) ? null : reader.GetString(1);

            var groupKey = reader.IsDBNull(2) ? null : reader.GetString(2);
            var groupCount = reader.GetInt64(3);
            var avgArea = reader.IsDBNull(4) ? 0 : reader.GetDecimal(4);
            var totalArea = reader.IsDBNull(5) ? 0 : reader.GetDecimal(5);

            if (groupKey != null)
            {
                groups.Add(new AggregationGroup(groupKey, groupCount, (double)avgArea, (double)totalArea));
            }
        }

        return new AggregationResult(totalCount, extentJson, groups);
    }

    /// <summary>
    /// Executes optimized spatial queries using the honua_spatial_query function.
    /// </summary>
    public async IAsyncEnumerable<SpatialQueryResult> SpatialQueryAsync(
        DataSourceDefinition dataSource,
        string tableName,
        string geomColumn,
        Geometry queryGeometry,
        string operation, // 'intersects', 'contains', 'within', 'distance'
        double? distance = null,
        int srid = 4326,
        int targetSrid = 4326,
        string? filterSql = null,
        int limit = 1000,
        int offset = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionManager
            .CreateConnectionAsync(dataSource, cancellationToken)
            .ConfigureAwait(false);

        await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var wkt = _wktWriter.Write(queryGeometry);
        var geometryParam = $"SRID={queryGeometry.SRID};{wkt}";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM honua_spatial_query(@p_table_name, @p_geom_column, @p_query_geometry, @p_operation, @p_distance, @p_srid, @p_target_srid, @p_filter_sql, @p_limit, @p_offset)";

        cmd.Parameters.AddWithValue("p_table_name", tableName);
        cmd.Parameters.AddWithValue("p_geom_column", geomColumn);
        cmd.Parameters.AddWithValue("p_query_geometry", NpgsqlDbType.Geometry, geometryParam);
        cmd.Parameters.AddWithValue("p_operation", operation);
        cmd.Parameters.AddWithValue("p_distance", distance.HasValue ? distance.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("p_srid", srid);
        cmd.Parameters.AddWithValue("p_target_srid", targetSrid);
        cmd.Parameters.AddWithValue("p_filter_sql", filterSql ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p_limit", limit);
        cmd.Parameters.AddWithValue("p_offset", offset);

        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var jsonText = reader.GetString(0);
            var jsonDoc = JsonDocument.Parse(jsonText);
            var distanceMeters = reader.IsDBNull(1) ? null : (double?)reader.GetDecimal(1);

            yield return new SpatialQueryResult(jsonDoc, distanceMeters);
        }
    }

    /// <summary>
    /// Performs fast count using the honua_fast_count function with optional estimation.
    /// </summary>
    public async Task<long> FastCountAsync(
        DataSourceDefinition dataSource,
        string tableName,
        string geomColumn,
        Geometry? bbox = null,
        string? filterSql = null,
        int srid = 4326,
        bool useEstimate = false,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionManager
            .CreateConnectionAsync(dataSource, cancellationToken)
            .ConfigureAwait(false);

        await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT honua_fast_count(@p_table_name, @p_geom_column, @p_bbox, @p_filter_sql, @p_srid, @p_use_estimate)";

        cmd.Parameters.AddWithValue("p_table_name", tableName);
        cmd.Parameters.AddWithValue("p_geom_column", geomColumn);

        if (bbox != null)
        {
            var wkt = _wktWriter.Write(bbox);
            var bboxGeometry = $"SRID={bbox.SRID};{wkt}";
            cmd.Parameters.AddWithValue("p_bbox", NpgsqlDbType.Geometry, bboxGeometry);
        }
        else
        {
            cmd.Parameters.AddWithValue("p_bbox", DBNull.Value);
        }

        cmd.Parameters.AddWithValue("p_filter_sql", filterSql ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("p_srid", srid);
        cmd.Parameters.AddWithValue("p_use_estimate", useEstimate);

        var result = await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return Convert.ToInt64(result);
    }

    /// <summary>
    /// Clusters point features using the honua_cluster_points function.
    /// </summary>
    public async IAsyncEnumerable<ClusterResult> ClusterPointsAsync(
        DataSourceDefinition dataSource,
        string tableName,
        string geomColumn,
        Geometry bbox,
        double clusterDistance, // in meters
        int srid = 4326,
        string? filterSql = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionManager
            .CreateConnectionAsync(dataSource, cancellationToken)
            .ConfigureAwait(false);

        await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var wkt = _wktWriter.Write(bbox);
        var bboxGeometry = $"SRID={bbox.SRID};{wkt}";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM honua_cluster_points(@p_table_name, @p_geom_column, @p_bbox, @p_cluster_distance, @p_srid, @p_filter_sql)";

        cmd.Parameters.AddWithValue("p_table_name", tableName);
        cmd.Parameters.AddWithValue("p_geom_column", geomColumn);
        cmd.Parameters.AddWithValue("p_bbox", NpgsqlDbType.Geometry, bboxGeometry);
        cmd.Parameters.AddWithValue("p_cluster_distance", clusterDistance);
        cmd.Parameters.AddWithValue("p_srid", srid);
        cmd.Parameters.AddWithValue("p_filter_sql", filterSql ?? (object)DBNull.Value);

        await using var reader = await _connectionManager.RetryPipeline.ExecuteAsync(
            async ct => await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var clusterId = reader.GetInt32(0);
            var pointCount = reader.GetInt64(1);
            var centroidJson = reader.GetString(2);
            var propertiesJson = reader.IsDBNull(3) ? null : reader.GetString(3);

            yield return new ClusterResult(clusterId, pointCount, centroidJson, propertiesJson);
        }
    }

    /// <summary>
    /// Checks if optimized functions are available in the database.
    /// </summary>
    public async Task<bool> AreFunctionsAvailableAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await _connectionManager
                .CreateConnectionAsync(dataSource, cancellationToken)
                .ConfigureAwait(false);

            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM pg_proc
                WHERE proname IN ('honua_get_features_optimized', 'honua_get_mvt_tile', 'honua_aggregate_features')";

            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var count = Convert.ToInt32(result);

            return count >= 3; // At least the core functions should exist
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Result from aggregation query
/// </summary>
public sealed record AggregationResult(
    long TotalCount,
    string? ExtentGeoJson,
    IReadOnlyList<AggregationGroup> Groups);

/// <summary>
/// Grouped aggregation result
/// </summary>
public sealed record AggregationGroup(
    string GroupKey,
    long Count,
    double AverageArea,
    double TotalArea);

/// <summary>
/// Result from spatial query
/// </summary>
public sealed record SpatialQueryResult(
    JsonDocument FeatureJson,
    double? DistanceMeters);

/// <summary>
/// Result from clustering query
/// </summary>
public sealed record ClusterResult(
    int ClusterId,
    long PointCount,
    string CentroidGeoJson,
    string? RepresentativePropertiesJson);
