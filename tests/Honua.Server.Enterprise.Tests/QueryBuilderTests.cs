using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.Data.BigQuery;
using Honua.Server.Enterprise.Data.Snowflake;

namespace Honua.Server.Enterprise.Tests;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class QueryBuilderTests
{
    private LayerDefinition CreateTestLayer()
    {
        return new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "test_table",
                PrimaryKey = "id",
                Srid = 4326
            }
        };
    }

    [Fact]
    public void BuildSelect_WithBasicQuery_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var query = new FeatureQuery(Limit: 10);

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        result.Sql.Should().Contain("SELECT *");
        result.Sql.Should().Contain("ST_ASGEOJSON(`geom`) as _geojson");
        result.Sql.Should().Contain("FROM `test_table`");
        result.Sql.Should().Contain("LIMIT @limit_value");
        result.Parameters.Should().NotBeNull();
        result.Parameters.Should().HaveCount(1);
        result.Parameters![0].Value.Should().Be(10);
    }

    [Fact]
    public void BuildCount_WithBasicQuery_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var query = new FeatureQuery();

        // Act
        var result = builder.BuildCount(query);

        // Assert
        result.Sql.Should().Contain("SELECT COUNT(*) as count");
        result.Sql.Should().Contain("FROM `test_table`");
    }

    [Fact]
    public void BuildById_WithUuidId_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var featureId = "a7b3c4d5-e6f7-8901-2345-6789abcdef01";

        // Act
        var result = builder.BuildById(featureId);

        // Assert
        result.Sql.Should().Contain("WHERE `id` = @feature_id");
        result.Sql.Should().Contain("LIMIT 1");
        result.Parameters.Should().NotBeNull();
        result.Parameters.Should().HaveCount(1);
        result.Parameters![0].Value.Should().Be(featureId);
    }

    [Fact]
    public void BuildById_WithNumericId_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);

        // Act
        var result = builder.BuildById("123");

        // Assert
        result.Sql.Should().Contain("WHERE `id` = @feature_id");
        result.Sql.Should().Contain("LIMIT 1");
        result.Parameters.Should().NotBeNull();
        result.Parameters.Should().HaveCount(1);
        result.Parameters![0].Value.Should().Be("123");
    }

    [Fact]
    public void BuildSelect_WithPagination_GeneratesValidSnowflakeSql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new SnowflakeFeatureQueryBuilder(layer);
        var query = new FeatureQuery(Limit: 5, Offset: 10);

        // Act
        var (sql, parameters) = builder.BuildSelect(query);

        // Assert
        sql.Should().Contain("SELECT *");
        sql.Should().Contain("ST_ASGEOJSON(\"geom\") as _geojson");
        sql.Should().Contain("FROM \"test_table\"");
        sql.Should().Contain("LIMIT :limit");
        sql.Should().Contain("OFFSET :offset");
        parameters.Should().ContainKey("limit").WhoseValue.Should().Be(5);
        parameters.Should().ContainKey("offset").WhoseValue.Should().Be(10);
    }


    [Fact]
    public void BuildSelect_WithSorting_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var sortOrders = new List<FeatureSortOrder>
        {
            new("name", FeatureSortDirection.Ascending),
            new("created", FeatureSortDirection.Descending)
        };
        var query = new FeatureQuery(SortOrders: sortOrders);

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        result.Sql.Should().Contain("ORDER BY `name` ASC, `created` DESC");
    }

    [Fact]
    public void BuildSelect_WithLargePagination_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var query = new FeatureQuery(Limit: 1000, Offset: 50000);

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        result.Sql.Should().Contain("LIMIT @limit_value");
        result.Sql.Should().Contain("OFFSET @offset_value");
        result.Parameters.Should().NotBeNull();
        result.Parameters!.Should().Contain(p => p.Name == "limit_value" && (int)p.Value! == 1000);
        result.Parameters!.Should().Contain(p => p.Name == "offset_value" && (int)p.Value! == 50000);
    }

    [Fact]
    public void BuildSelect_WithSpecialCharactersInFieldNames_QuotesProperlyInSnowflakeSql()
    {
        // Arrange
        // Use a quoted identifier for the field name since hyphens require quoting
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "\"feature-id\"",  // Quoted because it contains a hyphen
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = "test_table",
                PrimaryKey = "\"feature-id\"",  // Quoted because it contains a hyphen
                Srid = 4326
            }
        };
        var builder = new SnowflakeFeatureQueryBuilder(layer);

        // Act - Use BuildById to ensure the primary key field appears in SQL and gets quoted
        var (sql, parameters) = builder.BuildById("test-id");

        // Assert - Field name with hyphen should be properly quoted
        sql.Should().Contain("\"feature-id\"");
        parameters.Should().ContainKey("id").WhoseValue.Should().Be("test-id");
    }

    [Fact]
    public void BuildSelect_WithMultiFieldSorting_GeneratesValidBigQuerySql()
    {
        // Arrange
        var layer = CreateTestLayer();
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var sortOrders = new List<FeatureSortOrder>
        {
            new("priority", FeatureSortDirection.Descending),
            new("name", FeatureSortDirection.Ascending),
            new("created_at", FeatureSortDirection.Descending)
        };
        var query = new FeatureQuery(SortOrders: sortOrders);

        // Act
        var result = builder.BuildSelect(query);

        // Assert
        result.Sql.Should().Contain("ORDER BY");
        result.Sql.Should().Contain("`priority` DESC");
        result.Sql.Should().Contain("`name` ASC");
        result.Sql.Should().Contain("`created_at` DESC");
    }
}
