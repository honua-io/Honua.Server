using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Server.Core.Performance;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Utility to introspect SQLite/SpatiaLite databases and validate test data integrity.
/// </summary>
public sealed class DatabaseIntrospectionUtility : IDisposable
{
    private readonly SqliteConnection _connection;

    public DatabaseIntrospectionUtility(string connectionString)
    {
        _connection = new SqliteConnection(connectionString);
        _connection.Open();

        // Load SpatiaLite extension
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT load_extension('mod_spatialite');";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // SpatiaLite may not be available in all environments
            Console.WriteLine("Warning: SpatiaLite extension not loaded");
        }
    }

    /// <summary>
    /// Get all tables in the database.
    /// </summary>
    public List<string> GetTables()
    {
        var tables = new List<string>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE 'spatial_%' AND name NOT LIKE 'geometry_%' AND name NOT LIKE 'idx_%';";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>
    /// Get detailed schema information for a table.
    /// </summary>
    public TableSchema GetTableSchema(string tableName)
    {
        var schema = new TableSchema { TableName = tableName };

        // Get column info
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"PRAGMA table_info({tableName});";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                schema.Columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(1),
                    Type = reader.GetString(2),
                    NotNull = reader.GetInt32(3) == 1,
                    DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsPrimaryKey = reader.GetInt32(5) == 1
                });
            }
        }

        // Check for spatial column via geometry_columns
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                SELECT f_geometry_column, geometry_type, coord_dimension, srid, spatial_index_enabled
                FROM geometry_columns
                WHERE f_table_name = @tableName;";
            cmd.Parameters.AddWithValue("@tableName", tableName);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                schema.GeometryColumn = reader.GetString(0);
                schema.GeometryType = reader.IsDBNull(1) ? null : reader.GetString(1);
                schema.CoordDimension = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                schema.Srid = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                schema.HasSpatialIndex = reader.IsDBNull(4) ? false : reader.GetInt32(4) == 1;
            }
        }
        catch
        {
            // geometry_columns may not exist
        }

        // Get row count
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            schema.RowCount = Convert.ToInt32(cmd.ExecuteScalar());
        }

        return schema;
    }

    /// <summary>
    /// Analyze actual geometry types in the data (not just the schema declaration).
    /// Supports both SpatiaLite geometries and TEXT-based GeoJSON/WKT storage (Honua's approach).
    /// </summary>
    public GeometryAnalysis AnalyzeGeometryColumn(string tableName, string geometryColumn)
    {
        var analysis = new GeometryAnalysis
        {
            TableName = tableName,
            GeometryColumn = geometryColumn
        };

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT {geometryColumn} FROM {tableName} WHERE {geometryColumn} IS NOT NULL";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var geomText = reader.GetString(0);
                var geometry = TryParseGeometry(geomText);
                if (geometry != null)
                {
                    var geomType = geometry.GeometryType.ToUpperInvariant();
                    analysis.GeometryTypes[geomType] = analysis.GeometryTypes.GetValueOrDefault(geomType) + 1;
                }
            }
            catch
            {
                // Skip unparseable geometries
            }
        }

        return analysis;
    }

    /// <summary>
    /// Calculate actual bbox from geometry data.
    /// Supports both SpatiaLite geometries and TEXT-based GeoJSON/WKT storage (Honua's approach).
    /// </summary>
    public BboxInfo CalculateActualBbox(string tableName, string geometryColumn)
    {
        var bbox = new BboxInfo
        {
            MinX = double.MaxValue,
            MinY = double.MaxValue,
            MaxX = double.MinValue,
            MaxY = double.MinValue
        };

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT {geometryColumn} FROM {tableName} WHERE {geometryColumn} IS NOT NULL";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var geomText = reader.GetString(0);
                var geometry = TryParseGeometry(geomText);
                if (geometry != null)
                {
                    var envelope = geometry.EnvelopeInternal;
                    bbox.MinX = Math.Min(bbox.MinX, envelope.MinX);
                    bbox.MinY = Math.Min(bbox.MinY, envelope.MinY);
                    bbox.MaxX = Math.Max(bbox.MaxX, envelope.MaxX);
                    bbox.MaxY = Math.Max(bbox.MaxY, envelope.MaxY);
                }
            }
            catch
            {
                // Skip unparseable geometries
            }
        }

        // Return empty bbox if no valid geometries found
        if (bbox.MinX == double.MaxValue)
        {
            return new BboxInfo();
        }

        return bbox;
    }

    /// <summary>
    /// Calculate temporal extent if temporal column exists.
    /// </summary>
    public TemporalInfo CalculateTemporalExtent(string tableName, string temporalColumn)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            SELECT
                MIN({temporalColumn}) as min_time,
                MAX({temporalColumn}) as max_time
            FROM {tableName}
            WHERE {temporalColumn} IS NOT NULL;";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new TemporalInfo
            {
                MinTime = reader.IsDBNull(0) ? null : reader.GetString(0),
                MaxTime = reader.IsDBNull(1) ? null : reader.GetString(1)
            };
        }

        return new TemporalInfo();
    }

    /// <summary>
    /// Get sample data from table for inspection.
    /// </summary>
    public List<Dictionary<string, object?>> GetSampleData(string tableName, int limit = 5)
    {
        var samples = new List<Dictionary<string, object?>>();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM {tableName} LIMIT {limit};";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var fieldName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);

                // Convert blob geometry to WKT for readability
                if (value is byte[] blob && fieldName.ToLower().Contains("geom"))
                {
                    try
                    {
                        var wkbReader = new WKBReader();
                        var geom = wkbReader.Read(blob);
                        value = $"{geom.GeometryType} (WKT: {geom.AsText().Substring(0, Math.Min(50, geom.AsText().Length))}...)";
                    }
                    catch
                    {
                        value = $"<binary geometry, {blob.Length} bytes>";
                    }
                }

                row[fieldName] = value;
            }
            samples.Add(row);
        }

        return samples;
    }

    /// <summary>
    /// Generate a comprehensive validation report.
    /// </summary>
    public string GenerateValidationReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine("=== DATABASE INTROSPECTION REPORT ===");
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();

        var tables = GetTables();
        report.AppendLine($"Found {tables.Count} table(s):");

        foreach (var table in tables)
        {
            report.AppendLine();
            report.AppendLine($"TABLE: {table}");
            report.AppendLine(new string('-', 60));

            var schema = GetTableSchema(table);
            report.AppendLine($"Row Count: {schema.RowCount}");

            if (!string.IsNullOrEmpty(schema.GeometryColumn))
            {
                report.AppendLine($"Geometry Column: {schema.GeometryColumn}");
                report.AppendLine($"Declared Geometry Type: {schema.GeometryType ?? "unknown"}");
                report.AppendLine($"SRID: {schema.Srid}");
                report.AppendLine($"Spatial Index: {(schema.HasSpatialIndex ? "YES" : "NO")}");

                // Analyze actual geometry types
                var geomAnalysis = AnalyzeGeometryColumn(table, schema.GeometryColumn);
                report.AppendLine("Actual Geometry Types:");
                foreach (var (geomType, count) in geomAnalysis.GeometryTypes)
                {
                    report.AppendLine($"  - {geomType}: {count} features");
                }

                // Calculate actual bbox
                var bbox = CalculateActualBbox(table, schema.GeometryColumn);
                report.AppendLine($"Actual Bbox: [{bbox.MinX:F6}, {bbox.MinY:F6}, {bbox.MaxX:F6}, {bbox.MaxY:F6}]");
            }

            // Primary key
            var pkColumns = schema.Columns.Where(c => c.IsPrimaryKey).ToList();
            if (pkColumns.Any())
            {
                report.AppendLine($"Primary Key: {string.Join(", ", pkColumns.Select(c => c.Name))}");
            }
            else
            {
                report.AppendLine("⚠️  WARNING: No primary key defined!");
            }

            // Sample data
            report.AppendLine("\nSample Data (first 3 rows):");
            var samples = GetSampleData(table, 3);
            foreach (var sample in samples)
            {
                report.AppendLine($"  {JsonSerializer.Serialize(sample, SampleSerializationOptions)}");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Try to parse geometry from TEXT (GeoJSON or WKT).
    /// </summary>
    private static NetTopologySuite.Geometries.Geometry? TryParseGeometry(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // Try GeoJSON first (Honua's primary format)
        if (text.TrimStart().StartsWith("{"))
        {
            try
            {
                var geoJsonReader = new GeoJsonReader();
                return geoJsonReader.Read<NetTopologySuite.Geometries.Geometry>(text);
            }
            catch
            {
                // Not valid GeoJSON, try WKT
            }
        }

        // Try WKT
        try
        {
            var wktReader = new WKTReader();
            return wktReader.Read(text);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    private static readonly JsonSerializerOptions SampleSerializationOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public sealed class TableSchema
{
    public string TableName { get; set; } = "";
    public List<ColumnInfo> Columns { get; set; } = new();
    public int RowCount { get; set; }
    public string? GeometryColumn { get; set; }
    public string? GeometryType { get; set; }
    public int CoordDimension { get; set; }
    public int Srid { get; set; }
    public bool HasSpatialIndex { get; set; }
}

public sealed class ColumnInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool NotNull { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsPrimaryKey { get; set; }
}

public sealed class GeometryAnalysis
{
    public string TableName { get; set; } = "";
    public string GeometryColumn { get; set; } = "";
    public Dictionary<string, int> GeometryTypes { get; set; } = new();
}

public sealed class BboxInfo
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    public double[] ToArray() => new[] { MinX, MinY, MaxX, MaxY };
}

public sealed class TemporalInfo
{
    public string? MinTime { get; set; }
    public string? MaxTime { get; set; }
}
