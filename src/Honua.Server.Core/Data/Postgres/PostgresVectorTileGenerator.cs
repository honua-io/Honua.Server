// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.VectorTiles;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Handles generation of Mapbox Vector Tiles (MVT) from PostgreSQL/PostGIS data.
/// </summary>
internal sealed class PostgresVectorTileGenerator
{
    private readonly PostgresConnectionManager _connectionManager;

    public PostgresVectorTileGenerator(PostgresConnectionManager connectionManager)
    {
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
    }

    public async Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        await using var connection = await _connectionManager.CreateConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await connection.OpenAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        var tableName = PostgresRecordMapper.QuoteIdentifier(layer.Storage?.Table ?? layer.Id);
        var geometryColumn = PostgresRecordMapper.QuoteIdentifier(layer.GeometryField ?? "geom");
        var storageSrid = layer.Storage?.Srid ?? CrsHelper.Wgs84;

        // Use VectorTileOptions from layer metadata or service defaults
        var options = service.VectorTileOptions ?? VectorTileOptions.Default;
        var processor = new VectorTileProcessor(options);

        // Calculate tile bounds in Web Mercator (EPSG:3857)
        const double earthRadius = 6378137.0;
        const double originShift = Math.PI * earthRadius;
        var tileSize = 2.0 * originShift / Math.Pow(2, zoom);
        var minX = -originShift + (x * tileSize);
        var maxY = originShift - (y * tileSize);
        var maxX = minX + tileSize;
        var minY = maxY - tileSize;

        // BUG FIX #21: Use DateTimeOffset to preserve timezone information
        // Build temporal WHERE clause if datetime filter is provided
        string? temporalWhereClause = null;
        DateTimeOffset? datetimeValue = null;
        if (!string.IsNullOrWhiteSpace(datetime) && layer.Temporal.Enabled)
        {
            temporalWhereClause = BuildTemporalWhereClause(layer.Temporal);
            if (DateTimeOffset.TryParse(datetime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
            {
                // Normalize to UTC to ensure correct instant comparison in SQL
                datetimeValue = parsed.ToUniversalTime();
            }
            else
            {
                // Invalid datetime format - skip temporal filter
                temporalWhereClause = null;
            }
        }

        var attributeColumns = layer.Fields?
            .Select(field => field.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           !string.Equals(name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var projectedColumns = attributeColumns is { Length: > 0 } ? attributeColumns : null;

        // BUG FIX: Use antimeridian-aware query for geographic CRS when tile may cross dateline
        // For Web Mercator (3857), tiles never cross the antimeridian due to projection limits
        // For geographic CRS (4326), check if tile crosses antimeridian (minX > maxX)
        var usesGeographicCrs = storageSrid == 4326;
        var crossesAntimeridian = usesGeographicCrs && minX > maxX;

        string sql;
        if (crossesAntimeridian)
        {
            // Use antimeridian-aware query that splits into western and eastern hemispheres
            sql = processor.BuildPostgisMvtQueryWithAntimeridianHandling(
                tableName,
                geometryColumn,
                storageSrid,
                zoom,
                layer.Id ?? "default",
                minX,
                minY,
                maxX,
                maxY,
                temporalWhereClause,
                projectedColumns);
        }
        else
        {
            // Standard query - no antimeridian crossing
            sql = processor.ShouldCluster(zoom)
                ? processor.BuildClusteringQuery(tableName, geometryColumn, storageSrid, zoom, layer.Id ?? "default", temporalWhereClause, projectedColumns)
                : processor.BuildPostgisMvtQuery(tableName, geometryColumn, storageSrid, zoom, layer.Id ?? "default", temporalWhereClause, projectedColumns);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(minX);
        command.Parameters.AddWithValue(minY);
        command.Parameters.AddWithValue(maxX);
        command.Parameters.AddWithValue(maxY);
        command.Parameters.AddWithValue(layer.Id ?? "default");
        if (datetimeValue.HasValue)
        {
            command.Parameters.AddWithValue("@datetime", datetimeValue.Value);
        }

        var result = await _connectionManager.RetryPipeline.ExecuteAsync(async ct =>
            await command.ExecuteScalarAsync(ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        if (result is byte[] mvtBytes)
        {
            return mvtBytes;
        }

        return Array.Empty<byte>();
    }

    private static string BuildTemporalWhereClause(LayerTemporalDefinition temporal)
    {
        // Handle single timestamp field or time range
        if (!string.IsNullOrWhiteSpace(temporal.StartField))
        {
            var startField = PostgresRecordMapper.QuoteIdentifier(temporal.StartField);

            if (!string.IsNullOrWhiteSpace(temporal.EndField))
            {
                // Range query: datetime falls between start and end
                var endField = PostgresRecordMapper.QuoteIdentifier(temporal.EndField);
                return $"({startField} <= @datetime::timestamp AND {endField} >= @datetime::timestamp)";
            }
            else
            {
                // Single field exact or range match
                return $"{startField} = @datetime::timestamp";
            }
        }

        return string.Empty;
    }
}
