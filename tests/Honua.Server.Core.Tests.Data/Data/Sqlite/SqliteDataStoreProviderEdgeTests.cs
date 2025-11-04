using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Sqlite;

[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
public sealed class SqliteDataStoreProviderEdgeTests
{
    [Fact]
    public void NormalizeRecord_ShouldHandleSpecialCharactersInColumnNames()
    {
        var layer = new LayerDefinition
        {
            Id = "roads",
            ServiceId = "svc",
            Title = "Roads",
            GeometryType = "Point",
            IdField = "road_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int", Nullable = false },
                new FieldDefinition { Name = "geom", DataType = "geometry", Nullable = true },
                new FieldDefinition { Name = "speed-limit", DataType = "double" },
                new FieldDefinition { Name = "NA\"ME", DataType = "string" },
            },
            Storage = new LayerStorageDefinition
            {
                Table = "roads",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                Srid = 3857
            }
        };

        var attributes = new Dictionary<string, object?>
        {
            ["road_id"] = 1,
            ["speed-limit"] = 45,
            ["NA\"ME"] = "Main St"
        };

        var act = () => InvokeNormalizeRecord(layer, attributes, includeKey: false);
        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage("*speed-limit*");
    }

    [Fact]
    public void NormalizeValue_ShouldReturnWktForGeoJson()
    {
        var geometryJson = "{\"type\":\"Point\",\"coordinates\":[-122.5,45.5]}";
        var method = typeof(SqliteDataStoreProvider).GetMethod(
            "NormalizeGeometryValue",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) ?? throw new InvalidOperationException("Method not found");

        var wkt = (string?)method.Invoke(null, new object?[] { geometryJson });
        wkt.Should().StartWith("POINT", because: "WKT output should be POINT geometry");
    }

    private static object InvokeNormalizeRecord(LayerDefinition layer, IDictionary<string, object?> attributes, bool includeKey)
    {
        var method = typeof(SqliteDataStoreProvider).GetMethod(
            "NormalizeRecord",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static) ?? throw new InvalidOperationException("NormalizeRecord not found");

        return method.Invoke(null, new object?[] { layer, attributes, includeKey })!;
    }
}
