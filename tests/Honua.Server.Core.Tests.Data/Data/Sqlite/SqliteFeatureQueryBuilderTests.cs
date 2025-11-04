using System;
using System.Collections.Generic;
using System.Globalization;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Sqlite;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data.Sqlite;

[Collection("DatabaseTests")]
[Trait("Category", "Integration")]
public class SqliteFeatureQueryBuilderTests
{
    [Fact]
    public void BuildSelect_ShouldComposeBasicQuery()
    {
        var (service, layer) = CreateMetadata();
        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var query = new FeatureQuery(Limit: 25, Offset: 0);

        var definition = builder.BuildSelect(query);

        definition.Sql.Should().Be("select t.* from \"roads_primary\" t order by t.\"road_id\" asc limit @limit offset @offset");
        definition.Parameters.Should().ContainKey("limit").WhoseValue.Should().Be(25);
        definition.Parameters.Should().ContainKey("offset").WhoseValue.Should().Be(0);
    }

    [Fact]
    public void BuildSelect_ShouldApplyFilters()
    {
        var (service, layer) = CreateMetadata();
        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var bbox = new BoundingBox(-123, 45, -122, 46);
        var interval = new TemporalInterval(DateTimeOffset.Parse("2020-01-01T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2020-12-31T00:00:00Z", CultureInfo.InvariantCulture));
        var query = new FeatureQuery(Bbox: bbox, Temporal: interval, Limit: 50, Offset: 10);

        var definition = builder.BuildSelect(query);

        definition.Sql.Should().Contain("json_extract(t.\"geom\", '$.coordinates[0]') >= @bbox_minx");
        definition.Sql.Should().Contain("json_extract(t.\"geom\", '$.coordinates[1]') <= @bbox_maxy");
        definition.Sql.Should().Contain("t.\"observed_at\" >= @datetime_start");
        definition.Sql.Should().Contain("order by t.\"road_id\" asc");
        definition.Parameters.Should().Contain(new KeyValuePair<string, object?>("bbox_minx", -123d));
        definition.Parameters.Should().Contain(new KeyValuePair<string, object?>("bbox_maxy", 46d));
        definition.Parameters.Should().ContainKey("datetime_start");
        definition.Parameters.Should().ContainKey("datetime_end");
    }

    [Fact]
    public void BuildSelect_ShouldSelectRequestedProperties()
    {
        var (service, layer) = CreateMetadata();
        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var query = new FeatureQuery(PropertyNames: new[] { "name", "status" }, Limit: 5);

        var definition = builder.BuildSelect(query);

        definition.Sql.Should().StartWith("select t.\"geom\", t.\"road_id\", t.\"name\", t.\"status\" from \"roads_primary\" t");
        definition.Parameters.Should().ContainKey("limit").WhoseValue.Should().Be(5);
    }

    [Fact]
    public void BuildCount_ShouldIgnorePagination()
    {
        var (service, layer) = CreateMetadata();
        var builder = new SqliteFeatureQueryBuilder(service, layer);
        var query = new FeatureQuery(Limit: 10, Offset: 5);

        var definition = builder.BuildCount(query);

        definition.Sql.Should().Be("select count(*) from \"roads_primary\" t");
        definition.Parameters.Should().BeEmpty();
    }

    private static (ServiceDefinition Service, LayerDefinition Layer) CreateMetadata()
    {
        var service = new ServiceDefinition
        {
            Id = "roads",
            Title = "Road Centerlines",
            FolderId = "transportation",
            ServiceType = "feature",
            DataSourceId = "sqlite-primary",
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = "EPSG:4326"
            }
        };

        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = service.Id,
            Title = "Primary Roads",
            GeometryType = "Point",
            IdField = "road_id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "roads_primary",
                GeometryColumn = "geom",
                PrimaryKey = "road_id",
                TemporalColumn = "observed_at",
                Srid = 3857
            }
        };

        return (service, layer);
    }
}
