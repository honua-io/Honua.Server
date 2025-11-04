using System;
using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Postgres;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Postgres;

[Collection("DatabaseTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "Data")]
[Trait("Database", "Postgres")]
[Trait("Speed", "Fast")]
public sealed class PostgresFeatureQueryBuilderTests
{
    [Fact]
    public void BuildSelect_ShouldTransformGeometryLiteralToStorageSrid()
    {
        var fields = new Dictionary<string, QueryFieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["road_id"] = new QueryFieldDefinition
            {
                Name = "road_id",
                DataType = QueryDataType.Int32,
                Nullable = false,
                IsKey = true,
                IsGeometry = false
            },
            ["geom"] = new QueryFieldDefinition
            {
                Name = "geom",
                DataType = QueryDataType.Geometry,
                Nullable = true,
                IsKey = false,
                IsGeometry = true
            }
        };

        var service = new ServiceDefinition
        {
            Id = "roads",
            Title = "Roads",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "postgis",
            Ogc = new OgcServiceDefinition { DefaultCrs = "EPSG:4326" }
        };

        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = "roads",
            Title = "Roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int", Nullable = false },
                new FieldDefinition { Name = "geom", DataType = "geometry" }
            },
            Storage = new LayerStorageDefinition
            {
                Table = "roads_primary",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                Srid = 3857
            }
        };

        var geometry = new QueryGeometryValue("LINESTRING(-122.51 45.49,-122.39 45.61)", 4326);
        var filter = new QueryFilter(new QueryFunctionExpression("geo.intersects", new QueryExpression[]
        {
            new QueryFieldReference("geom"),
            new QueryConstant(geometry)
        }));
        var entityDefinition = new QueryEntityDefinition("roads-primary", "roads_primary", fields);
        var query = new FeatureQuery(Filter: filter, EntityDefinition: entityDefinition);

        var builder = new PostgresFeatureQueryBuilder(service, layer, storageSrid: 3857, targetSrid: 3857);
        var definition = builder.BuildSelect(query);

        definition.Sql.Should().Contain("ST_GeomFromText(@filter_spatial_0, 4326)");
        definition.Sql.Should().Contain("ST_Transform(ST_GeomFromText(@filter_spatial_0, 4326), 3857)");
        definition.Sql.Should().Contain("ST_Transform(ST_MakeEnvelope(@filter_spatial_1, @filter_spatial_2, @filter_spatial_3, @filter_spatial_4, 4326), 3857)");
    }
}
