using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Postgres;

[Collection("DatabaseTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "Data")]
[Trait("Database", "Postgres")]
[Trait("Speed", "Fast")]
public sealed class PostgresDataStoreGeometryTests
{
    [Fact]
    public void BuildValueExpression_ShouldUseGeoJsonFunction_WhenValueIsGeoJson()
    {
        var layer = CreateLayerDefinition();
        var attributes = new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["geom"] = "{\"type\":\"Point\",\"coordinates\":[-122.5,45.5]}"
        };

        var (column, srid) = NormalizeGeometryColumn(layer, attributes);
        var expression = BuildValueExpression(column, srid);

        expression.Should().Contain("ST_GeomFromGeoJSON");
    }

    [Fact]
    public void BuildValueExpression_ShouldUseWktFunction_WhenValueIsText()
    {
        var layer = CreateLayerDefinition();
        var attributes = new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["geom"] = "POINT(-122.5 45.5)"
        };

        var (column, srid) = NormalizeGeometryColumn(layer, attributes);
        var expression = BuildValueExpression(column, srid);

        expression.Should().Contain("ST_GeomFromText");
    }

    private static LayerDefinition CreateLayerDefinition()
    {
        return new LayerDefinition
        {
            Id = "roads",
            ServiceId = "svc",
            Title = "Roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int", Nullable = false },
                new FieldDefinition { Name = "geom", DataType = "geometry", Nullable = true }
            },
            Storage = new LayerStorageDefinition
            {
                Table = "roads",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                Srid = 3857
            }
        };
    }

    private static (object Column, int Srid) NormalizeGeometryColumn(LayerDefinition layer, IDictionary<string, object?> attributes)
    {
        // Get the PostgresRecordMapper type from the assembly
        var mapperType = typeof(PostgresDataStoreProvider).Assembly
            .GetType("Honua.Server.Core.Data.Postgres.PostgresRecordMapper")
            ?? throw new InvalidOperationException("PostgresRecordMapper not found");

        var normalizeMethod = mapperType.GetMethod(
            "NormalizeRecord",
            BindingFlags.Public | BindingFlags.Static) ?? throw new InvalidOperationException("NormalizeRecord not found");

        var normalized = normalizeMethod.Invoke(null, new object?[] { layer, attributes, true })
            ?? throw new InvalidOperationException("NormalizeRecord returned null");

        var columnsProperty = normalized.GetType().GetProperty("Columns") ?? throw new InvalidOperationException("Columns property missing");
        var sridProperty = normalized.GetType().GetProperty("Srid") ?? throw new InvalidOperationException("Srid property missing");

        var columns = ((System.Collections.IEnumerable)columnsProperty.GetValue(normalized)!)
            .Cast<object>();
        var column = columns.Single(c => (bool)(c.GetType().GetProperty("IsGeometry")?.GetValue(c) ?? false));
        var srid = (int)(sridProperty.GetValue(normalized) ?? 0);
        return (column, srid);
    }

    private static string BuildValueExpression(object column, int srid)
    {
        // Get the PostgresRecordMapper type from the assembly
        var mapperType = typeof(PostgresDataStoreProvider).Assembly
            .GetType("Honua.Server.Core.Data.Postgres.PostgresRecordMapper")
            ?? throw new InvalidOperationException("PostgresRecordMapper not found");

        var method = mapperType.GetMethod(
            "BuildValueExpression",
            BindingFlags.Public | BindingFlags.Static) ?? throw new InvalidOperationException("BuildValueExpression not found");

        return (string)method.Invoke(null, new[] { column, (object)srid })!;
    }
}
