// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.VectorTiles;

/// <summary>
/// Processes vector tiles with advanced optimizations
/// </summary>
public sealed class VectorTileProcessor
{
    private readonly VectorTileOptions _options;

    public VectorTileProcessor(VectorTileOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Determines if a tile request should use overzooming
    /// </summary>
    public bool ShouldOverzoom(int requestedZoom, out int dataZoom)
    {
        if (_options.EnableOverzooming && requestedZoom > _options.MaxDataZoom)
        {
            dataZoom = _options.MaxDataZoom;
            return true;
        }

        dataZoom = requestedZoom;
        return false;
    }

    /// <summary>
    /// Calculates simplification tolerance for a given zoom level
    /// </summary>
    public double GetSimplificationTolerance(int zoom)
    {
        if (!_options.EnableSimplification)
        {
            return 0.0;
        }

        // Higher tolerance at lower zoom levels (more aggressive simplification)
        // Formula: base_tolerance * (max_zoom - current_zoom) * multiplier
        var baseTolerance = (double)_options.Extent / 4096.0;
        var zoomFactor = Math.Max(0, _options.MaxZoom - zoom);
        return baseTolerance * zoomFactor * _options.SimplificationTolerance;
    }

    /// <summary>
    /// Calculates minimum feature area threshold for a given zoom level
    /// </summary>
    public double GetMinFeatureArea(int zoom)
    {
        if (!_options.EnableFeatureReduction)
        {
            return 0.0;
        }

        // Higher threshold at lower zoom levels (more aggressive filtering)
        // Formula: min_area * (max_zoom - current_zoom)^2
        var zoomFactor = Math.Max(0, _options.MaxZoom - zoom);
        return _options.MinFeatureArea * Math.Pow(zoomFactor / 10.0, 2);
    }

    /// <summary>
    /// Determines if clustering should be enabled for a given zoom level
    /// </summary>
    public bool ShouldCluster(int zoom)
    {
        return _options.EnableClustering
            && zoom >= _options.ClusterMinZoom
            && zoom <= _options.ClusterMaxZoom;
    }

    /// <summary>
    /// Builds PostGIS MVT query with advanced options
    /// </summary>
    public string BuildPostgisMvtQuery(
        string tableName,
        string geometryColumn,
        int storageSrid,
        int requestedZoom,
        string layerName,
        string? temporalWhereClause = null,
        IEnumerable<string>? attributeColumns = null)
    {
        // Validate table name and geometry column for SQL injection protection
        // These are already quoted, so we just need to validate them
        var tableNameUnquoted = UnquoteIdentifier(tableName);
        var geometryColumnUnquoted = UnquoteIdentifier(geometryColumn);
        Security.SqlIdentifierValidator.ValidateIdentifier(tableNameUnquoted, nameof(tableName));
        Security.SqlIdentifierValidator.ValidateIdentifier(geometryColumnUnquoted, nameof(geometryColumn));

        var dataZoom = ShouldOverzoom(requestedZoom, out var actualDataZoom) ? actualDataZoom : requestedZoom;
        var simplificationTolerance = GetSimplificationTolerance(dataZoom);
        var minArea = GetMinFeatureArea(dataZoom);
        var projectedColumns = SanitizeAttributeColumns(attributeColumns, geometryColumnUnquoted);
        var selectColumnsClause = BuildSelectColumnClause(projectedColumns);

        var geometryTransform = BuildGeometryTransform(geometryColumn, storageSrid, simplificationTolerance);
        var whereClause = BuildWhereClause(geometryColumn, storageSrid, minArea, temporalWhereClause);

        return $@"
            WITH mvtgeom AS (
                SELECT
                    ST_AsMVTGeom(
                        {geometryTransform},
                        ST_MakeEnvelope($1, $2, $3, $4, 3857),
                        {_options.Extent},
                        {_options.Buffer},
                        true
                    ) AS geom{selectColumnsClause}
                FROM {tableName}
                {whereClause}
            )
            SELECT ST_AsMVT(mvtgeom.*, $5, {_options.Extent}, 'geom')
            FROM mvtgeom
            WHERE geom IS NOT NULL;
        ";
    }

    private string BuildGeometryTransform(string geometryColumn, int storageSrid, double simplificationTolerance)
    {
        var transform = $"ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 3857)";

        if (simplificationTolerance > 0)
        {
            // Apply simplification in Web Mercator projection for consistent results
            transform = $"ST_Simplify({transform}, {simplificationTolerance})";
        }

        return transform;
    }

    private string BuildWhereClause(string geometryColumn, int storageSrid, double minArea, string? temporalWhereClause = null)
    {
        var clauses = new List<string>
        {
            $"{geometryColumn} && ST_Transform(ST_MakeEnvelope($1, $2, $3, $4, 3857), {storageSrid})"
        };

        if (minArea > 0)
        {
            // Filter by area in tile coordinates
            clauses.Add($"ST_Area(ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 3857)) >= {minArea}");
        }

        if (temporalWhereClause.HasValue())
        {
            clauses.Add(temporalWhereClause);
        }

        return "WHERE " + string.Join(" AND ", clauses);
    }

    /// <summary>
    /// Builds PostGIS MVT query with antimeridian handling for geographic CRS (EPSG:4326).
    /// Splits the query into two parts if the tile crosses the antimeridian.
    /// </summary>
    public string BuildPostgisMvtQueryWithAntimeridianHandling(
        string tableName,
        string geometryColumn,
        int storageSrid,
        int requestedZoom,
        string layerName,
        double minX,
        double minY,
        double maxX,
        double maxY,
        string? temporalWhereClause = null,
        IEnumerable<string>? attributeColumns = null)
    {
        // Validate table name and geometry column for SQL injection protection
        var tableNameUnquoted = UnquoteIdentifier(tableName);
        var geometryColumnUnquoted = UnquoteIdentifier(geometryColumn);
        Security.SqlIdentifierValidator.ValidateIdentifier(tableNameUnquoted, nameof(tableName));
        Security.SqlIdentifierValidator.ValidateIdentifier(geometryColumnUnquoted, nameof(geometryColumn));

        var dataZoom = ShouldOverzoom(requestedZoom, out var actualDataZoom) ? actualDataZoom : requestedZoom;
        var simplificationTolerance = GetSimplificationTolerance(dataZoom);
        var minArea = GetMinFeatureArea(dataZoom);
        var projectedColumns = SanitizeAttributeColumns(attributeColumns, geometryColumnUnquoted);
        var selectColumnsClause = BuildSelectColumnClause(projectedColumns);

        // Check if bbox crosses antimeridian (only for geographic CRS)
        var crossesAntimeridian = storageSrid == 4326 && minX > maxX;

        if (!crossesAntimeridian)
        {
            // Standard query - no antimeridian crossing
            var geometryTransform = BuildGeometryTransformGeographic(geometryColumn, storageSrid, simplificationTolerance, false);
            var whereClause = BuildWhereClauseGeographic(geometryColumn, storageSrid, minArea, temporalWhereClause, false);

            return $@"
                WITH mvtgeom AS (
                    SELECT
                        ST_AsMVTGeom(
                            {geometryTransform},
                            ST_MakeEnvelope($1, $2, $3, $4, 4326),
                            {_options.Extent},
                            {_options.Buffer},
                            true
                        ) AS geom{selectColumnsClause}
                    FROM {tableName}
                    {whereClause}
                )
                SELECT ST_AsMVT(mvtgeom.*, $5, {_options.Extent}, 'geom')
                FROM mvtgeom
                WHERE geom IS NOT NULL;
            ";
        }

        // Antimeridian-crossing query - split into two parts and union
        // Western hemisphere: [minX, minY, 180, maxY]
        // Eastern hemisphere: [-180, minY, maxX, maxY]
        var geometryTransformShifted = BuildGeometryTransformGeographic(geometryColumn, storageSrid, simplificationTolerance, true);
        var whereClauseWest = BuildWhereClauseGeographicAntimeridian(geometryColumn, storageSrid, minArea, temporalWhereClause, true);
        var whereClauseEast = BuildWhereClauseGeographicAntimeridian(geometryColumn, storageSrid, minArea, temporalWhereClause, false);

        return $@"
            WITH mvtgeom AS (
                -- Western hemisphere part
                SELECT
                    ST_AsMVTGeom(
                        {geometryTransformShifted},
                        ST_MakeEnvelope($1, $2, 180, $4, 4326),
                        {_options.Extent},
                        {_options.Buffer},
                        true
                    ) AS geom{selectColumnsClause}
                FROM {tableName}
                {whereClauseWest}
                UNION ALL
                -- Eastern hemisphere part
                SELECT
                    ST_AsMVTGeom(
                        {geometryTransformShifted},
                        ST_MakeEnvelope(-180, $2, $3, $4, 4326),
                        {_options.Extent},
                        {_options.Buffer},
                        true
                    ) AS geom{selectColumnsClause}
                FROM {tableName}
                {whereClauseEast}
            )
            SELECT ST_AsMVT(mvtgeom.*, $5, {_options.Extent}, 'geom')
            FROM mvtgeom
            WHERE geom IS NOT NULL;
        ";
    }

    private string BuildGeometryTransformGeographic(string geometryColumn, int storageSrid, double simplificationTolerance, bool shiftLongitude)
    {
        var baseGeometry = storageSrid == 4326 ? geometryColumn : $"ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 4326)";

        // Apply ST_Shift_Longitude for antimeridian-crossing geometries
        if (shiftLongitude)
        {
            baseGeometry = $"ST_Shift_Longitude({baseGeometry})";
        }

        if (simplificationTolerance > 0)
        {
            // Apply simplification in geographic coordinates
            baseGeometry = $"ST_Simplify({baseGeometry}, {simplificationTolerance})";
        }

        return baseGeometry;
    }

    private string BuildWhereClauseGeographic(string geometryColumn, int storageSrid, double minArea, string? temporalWhereClause, bool shiftLongitude)
    {
        var envelope = storageSrid == 4326
            ? "ST_MakeEnvelope($1, $2, $3, $4, 4326)"
            : "ST_Transform(ST_MakeEnvelope($1, $2, $3, $4, 4326), " + storageSrid + ")";

        var clauses = new List<string>
        {
            $"{geometryColumn} && {envelope}"
        };

        if (minArea > 0)
        {
            // Filter by area in geographic coordinates
            var areaGeometry = storageSrid == 4326 ? geometryColumn : $"ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 4326)";
            clauses.Add($"ST_Area({areaGeometry}::geography) >= {minArea}");
        }

        if (temporalWhereClause.HasValue())
        {
            clauses.Add(temporalWhereClause);
        }

        return "WHERE " + string.Join(" AND ", clauses);
    }

    private string BuildWhereClauseGeographicAntimeridian(string geometryColumn, int storageSrid, double minArea, string? temporalWhereClause, bool isWesternHemisphere)
    {
        // For western hemisphere: longitude >= minX OR longitude <= 180
        // For eastern hemisphere: longitude >= -180 OR longitude <= maxX
        var envelope = isWesternHemisphere
            ? (storageSrid == 4326 ? "ST_MakeEnvelope($1, $2, 180, $4, 4326)" : "ST_Transform(ST_MakeEnvelope($1, $2, 180, $4, 4326), " + storageSrid + ")")
            : (storageSrid == 4326 ? "ST_MakeEnvelope(-180, $2, $3, $4, 4326)" : "ST_Transform(ST_MakeEnvelope(-180, $2, $3, $4, 4326), " + storageSrid + ")");

        var clauses = new List<string>
        {
            $"{geometryColumn} && {envelope}"
        };

        if (minArea > 0)
        {
            var areaGeometry = storageSrid == 4326 ? geometryColumn : $"ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 4326)";
            clauses.Add($"ST_Area({areaGeometry}::geography) >= {minArea}");
        }

        if (temporalWhereClause.HasValue())
        {
            clauses.Add(temporalWhereClause);
        }

        return "WHERE " + string.Join(" AND ", clauses);
    }

    /// <summary>
    /// Builds clustering query for point features
    /// </summary>
    public string BuildClusteringQuery(
        string tableName,
        string geometryColumn,
        int storageSrid,
        int zoom,
        string layerName,
        string? temporalWhereClause = null,
        IEnumerable<string>? attributeColumns = null)
    {
        if (!ShouldCluster(zoom))
        {
            return BuildPostgisMvtQuery(tableName, geometryColumn, storageSrid, zoom, layerName, temporalWhereClause, attributeColumns);
        }

        // Validate table name and geometry column for SQL injection protection
        var tableNameUnquoted = UnquoteIdentifier(tableName);
        var geometryColumnUnquoted = UnquoteIdentifier(geometryColumn);
        Security.SqlIdentifierValidator.ValidateIdentifier(tableNameUnquoted, nameof(tableName));
        Security.SqlIdentifierValidator.ValidateIdentifier(geometryColumnUnquoted, nameof(geometryColumn));

        var projectedColumns = SanitizeAttributeColumns(attributeColumns, geometryColumnUnquoted);
        var clusterAttributeProjection = BuildClusterAttributeProjection(projectedColumns);
        var clusterAggregateProjection = BuildClusterAggregateProjection(projectedColumns);
        var clusterMvtProjection = BuildClusterMvtProjection(projectedColumns);

        var whereClause = $"{geometryColumn} && ST_Transform(ST_MakeEnvelope($1, $2, $3, $4, 3857), {storageSrid})";
        if (temporalWhereClause.HasValue())
        {
            whereClause += $" AND {temporalWhereClause}";
        }

        return $@"
            WITH clusters AS (
                SELECT
                    ST_ClusterKMeans(
                        ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 3857),
                        GREATEST(1, COUNT(*) / 100)
                    ) OVER() as cluster_id,
                    ST_Transform(ST_SetSRID({geometryColumn}, {storageSrid}), 3857) as geom_3857{clusterAttributeProjection}
                FROM {tableName}
                WHERE {whereClause}
            ),
            cluster_centroids AS (
                SELECT
                    cluster_id,
                    ST_Centroid(ST_Collect(geom_3857)) as centroid,
                    COUNT(*) as point_count{clusterAggregateProjection}
                FROM clusters
                GROUP BY cluster_id
            ),
            mvtgeom AS (
                SELECT
                    ST_AsMVTGeom(
                        centroid,
                        ST_MakeEnvelope($1, $2, $3, $4, 3857),
                        {_options.Extent},
                        {_options.Buffer},
                        true
                    ) AS geom,
                    point_count,
                    cluster_id{clusterMvtProjection}
                FROM cluster_centroids
            )
            SELECT ST_AsMVT(mvtgeom.*, $5, {_options.Extent}, 'geom')
            FROM mvtgeom
            WHERE geom IS NOT NULL;
        ";
    }

    /// <summary>
    /// Removes quotes from an identifier if present.
    /// </summary>
    private static string UnquoteIdentifier(string identifier)
    {
        if (identifier.IsNullOrEmpty())
        {
            return identifier;
        }

        var trimmed = identifier.Trim();

        // PostgreSQL/SQLite: "identifier"
        if (trimmed.StartsWith("\"", StringComparison.Ordinal) && trimmed.EndsWith("\"", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1].Replace("\"\"", "\"");
        }

        // MySQL: `identifier`
        if (trimmed.StartsWith("`", StringComparison.Ordinal) && trimmed.EndsWith("`", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1].Replace("``", "`");
        }

        // SQL Server: [identifier]
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal) && trimmed.Length >= 2)
        {
            return trimmed[1..^1].Replace("]]", "]");
        }

        return trimmed;
    }

    private static IReadOnlyList<string> SanitizeAttributeColumns(IEnumerable<string>? attributeColumns, string geometryColumn)
    {
        if (attributeColumns is null)
        {
            return Array.Empty<string>();
        }

        var sanitized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in attributeColumns)
        {
            if (column.IsNullOrWhiteSpace())
            {
                continue;
            }

            if (string.Equals(column, geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(column))
            {
                continue;
            }

            Security.SqlIdentifierValidator.ValidateIdentifier(column, nameof(attributeColumns));
            sanitized.Add(column);
        }

        return sanitized;
    }

    private static string BuildSelectColumnClause(IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(columns.Count * 24);
        for (var i = 0; i < columns.Count; i++)
        {
            builder.AppendLine();
            builder.Append("                    , ").Append(QuoteIdentifier(columns[i]));
        }

        return builder.ToString();
    }

    private static string BuildClusterAttributeProjection(IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(columns.Count * 24);
        for (var i = 0; i < columns.Count; i++)
        {
            builder.AppendLine();
            builder.Append("                    , ").Append(QuoteIdentifier(columns[i]));
        }

        return builder.ToString();
    }

    private static string BuildClusterAggregateProjection(IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(columns.Count * 36);
        for (var i = 0; i < columns.Count; i++)
        {
            builder.AppendLine();
            builder.Append("                    , array_agg(")
                .Append(QuoteIdentifier(columns[i]))
                .Append(") AS ")
                .Append(QuoteIdentifier(columns[i] + "_list"));
        }

        return builder.ToString();
    }

    private static string BuildClusterMvtProjection(IReadOnlyList<string> columns)
    {
        if (columns.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(columns.Count * 24);
        for (var i = 0; i < columns.Count; i++)
        {
            builder.AppendLine();
            builder.Append("                    , ").Append(QuoteIdentifier(columns[i] + "_list"));
        }

        return builder.ToString();
    }

    private static string QuoteIdentifier(string identifier)
        => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
