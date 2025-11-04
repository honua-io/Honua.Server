using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

/// <summary>
/// Tests that validate test database structure and data integrity.
/// These tests prevent false positives by ensuring test data matches metadata claims.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class DatabaseIntrospectionTests
{
    private readonly ITestOutputHelper _output;

    public DatabaseIntrospectionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string FindSolutionRoot()
    {
        var directory = Directory.GetCurrentDirectory();
        while (directory != null)
        {
            if (Directory.GetFiles(directory, "*.sln").Length > 0)
            {
                return directory;
            }
            directory = Directory.GetParent(directory)?.FullName;
        }
        throw new InvalidOperationException("Could not find solution root");
    }

    [Fact]
    public void OgcSampleDatabase_GeneratesIntrospectionReport()
    {
        // Arrange - Find the database relative to the solution root
        var solutionRoot = FindSolutionRoot();
        var dbPath = Path.Combine(solutionRoot, "samples", "ogc", "ogc-sample.db");
        var connectionString = $"Data Source={dbPath}";

        if (!File.Exists(dbPath))
        {
            _output.WriteLine($"Database not found at: {dbPath}");
            _output.WriteLine($"Current directory: {Directory.GetCurrentDirectory()}");
            _output.WriteLine("Skipping introspection test.");
            return;
        }

        // Act
        using var inspector = new DatabaseIntrospectionUtility(connectionString);
        var report = inspector.GenerateValidationReport();

        // Assert
        report.Should().NotBeNullOrEmpty();
        _output.WriteLine(report);

        // Save report to file for inspection
        var reportPath = Path.Combine("TestResults", "database-introspection-report.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, report);
        _output.WriteLine($"\nReport saved to: {reportPath}");
    }

    [Fact]
    public void RoadsPrimaryTable_HasCorrectSchema()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var dbPath = Path.Combine(solutionRoot, "samples", "ogc", "ogc-sample.db");
        var connectionString = $"Data Source={dbPath}";

        if (!File.Exists(dbPath))
        {
            _output.WriteLine($"Database not found, skipping test");
            return;
        }

        // Act
        using var inspector = new DatabaseIntrospectionUtility(connectionString);
        var tables = inspector.GetTables();

        // Assert - should have at least roads_primary
        tables.Should().Contain("roads_primary", "test database should have roads_primary table");

        if (tables.Contains("roads_primary"))
        {
            var schema = inspector.GetTableSchema("roads_primary");

            // Log schema details
            _output.WriteLine($"Table: roads_primary");
            _output.WriteLine($"Row Count: {schema.RowCount}");
            _output.WriteLine($"Geometry Column: {schema.GeometryColumn}");
            _output.WriteLine($"Geometry Type: {schema.GeometryType}");
            _output.WriteLine($"SRID: {schema.Srid}");

            // Validations
            schema.RowCount.Should().BeGreaterThan(0, "table should not be empty");

            if (!string.IsNullOrEmpty(schema.GeometryColumn))
            {
                var geomAnalysis = inspector.AnalyzeGeometryColumn("roads_primary", schema.GeometryColumn);
                _output.WriteLine("Actual geometry types found:");
                foreach (var (geomType, count) in geomAnalysis.GeometryTypes)
                {
                    _output.WriteLine($"  {geomType}: {count}");
                }

                // Calculate actual bbox
                var bbox = inspector.CalculateActualBbox("roads_primary", schema.GeometryColumn);
                _output.WriteLine($"Actual bbox: [{bbox.MinX:F6}, {bbox.MinY:F6}, {bbox.MaxX:F6}, {bbox.MaxY:F6}]");

                // Validate SRID
                new[] { 4326, 3857, 0, -1 }.Should().Contain(schema.Srid, "SRID should be a valid CRS");
            }

            // Check for primary key
            var pkColumns = schema.Columns.FindAll(c => c.IsPrimaryKey);
            pkColumns.Should().NotBeEmpty("table should have a primary key for OGC API compliance");
            _output.WriteLine($"Primary key: {string.Join(", ", pkColumns.ConvertAll(c => c.Name))}");
        }
    }

    [Fact]
    public void MetadataJson_GeometryTypesMatchActualData()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var dbPath = Path.Combine(solutionRoot, "samples", "ogc", "ogc-sample.db");
        var metadataPath = Path.Combine(solutionRoot, "samples", "ogc", "metadata.json");
        var connectionString = $"Data Source={dbPath}";

        if (!File.Exists(dbPath) || !File.Exists(metadataPath))
        {
            _output.WriteLine("Database or metadata not found, skipping test");
            return;
        }

        // Act
        using var inspector = new DatabaseIntrospectionUtility(connectionString);
        var metadataJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metadataPath));

        // Get layers from metadata
        if (!metadataJson.RootElement.TryGetProperty("layers", out var layersArray))
        {
            _output.WriteLine("No layers found in metadata");
            return;
        }

        var mismatches = new System.Collections.Generic.List<string>();

        foreach (var layer in layersArray.EnumerateArray())
        {
            var layerId = layer.GetProperty("id").GetString();
            var declaredGeomType = layer.GetProperty("geometryType").GetString();
            var table = layer.GetProperty("storage").GetProperty("table").GetString();
            var geomColumn = layer.GetProperty("storage").GetProperty("geometryColumn").GetString();

            _output.WriteLine($"\nValidating layer: {layerId}");
            _output.WriteLine($"  Table: {table}");
            _output.WriteLine($"  Declared geometry type: {declaredGeomType}");

            // For TEXT-based geometry storage (Honua's approach), use the geometry column from metadata
            // rather than relying on SpatiaLite's geometry_columns table
            var geomAnalysis = inspector.AnalyzeGeometryColumn(table!, geomColumn!);

            _output.WriteLine($"  Actual geometry types:");
            foreach (var (geomType, count) in geomAnalysis.GeometryTypes)
            {
                _output.WriteLine($"    - {geomType}: {count}");

                // Check if declared type matches actual
                var normalizedDeclared = declaredGeomType?.ToUpperInvariant();
                var normalizedActual = geomType.ToUpperInvariant();

                // Handle variants (POINT vs POINT, LINESTRING vs MULTILINESTRING, etc.)
                if (!normalizedActual.Contains(normalizedDeclared ?? ""))
                {
                    var mismatch = $"{layerId}: Declared '{declaredGeomType}' but found '{geomType}' ({count} features)";
                    mismatches.Add(mismatch);
                    _output.WriteLine($"    ⚠️  MISMATCH: {mismatch}");
                }
            }
        }

        // Assert
        mismatches.Should().BeEmpty("metadata geometry types should match actual data");
    }

    [Fact]
    public void MetadataJson_BboxMatchesActualExtent()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var dbPath = Path.Combine(solutionRoot, "samples", "ogc", "ogc-sample.db");
        var metadataPath = Path.Combine(solutionRoot, "samples", "ogc", "metadata.json");
        var connectionString = $"Data Source={dbPath}";

        if (!File.Exists(dbPath) || !File.Exists(metadataPath))
        {
            _output.WriteLine("Database or metadata not found, skipping test");
            return;
        }

        // Act
        using var inspector = new DatabaseIntrospectionUtility(connectionString);
        var metadataJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(metadataPath));

        if (!metadataJson.RootElement.TryGetProperty("layers", out var layersArray))
        {
            return;
        }

        var tolerance = 0.1; // Allow small differences
        var mismatches = new System.Collections.Generic.List<string>();

        foreach (var layer in layersArray.EnumerateArray())
        {
            var layerId = layer.GetProperty("id").GetString();
            var table = layer.GetProperty("storage").GetProperty("table").GetString();
            var geomColumn = layer.GetProperty("storage").GetProperty("geometryColumn").GetString();

            if (!layer.TryGetProperty("extent", out var extent) || !extent.TryGetProperty("bbox", out var bboxArray))
            {
                continue;
            }

            var declaredBbox = bboxArray[0].EnumerateArray().Select(e => e.GetDouble()).ToArray();
            _output.WriteLine($"\nValidating bbox for layer: {layerId}");
            _output.WriteLine($"  Declared: [{string.Join(", ", declaredBbox.Select(v => v.ToString("F6")))}]");

            var actualBbox = inspector.CalculateActualBbox(table!, geomColumn!);
            _output.WriteLine($"  Actual:   [{actualBbox.MinX:F6}, {actualBbox.MinY:F6}, {actualBbox.MaxX:F6}, {actualBbox.MaxY:F6}]");

            // Check if bboxes match within tolerance
            if (Math.Abs(declaredBbox[0] - actualBbox.MinX) > tolerance ||
                Math.Abs(declaredBbox[1] - actualBbox.MinY) > tolerance ||
                Math.Abs(declaredBbox[2] - actualBbox.MaxX) > tolerance ||
                Math.Abs(declaredBbox[3] - actualBbox.MaxY) > tolerance)
            {
                var mismatch = $"{layerId}: Declared bbox doesn't match actual data (tolerance: {tolerance})";
                mismatches.Add(mismatch);
                _output.WriteLine($"  ⚠️  MISMATCH: {mismatch}");
            }
        }

        // Assert
        mismatches.Should().BeEmpty($"metadata bboxes should match actual data within {tolerance} degrees");
    }
}
