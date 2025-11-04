using FluentAssertions;
using Honua.Server.Enterprise.Data.BigQuery;
using Honua.Server.Enterprise.Data.CosmosDb;
using Honua.Server.Enterprise.Data.MongoDB;
using Honua.Server.Enterprise.Data.Redshift;
using Honua.Server.Enterprise.Data.Snowflake;
using Honua.Server.Enterprise.Data.Oracle;

namespace Honua.Server.Enterprise.Tests;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ProviderCapabilitiesTests
{
    [Fact]
    public void BigQuery_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = BigQueryDataStoreCapabilities.Instance;

        // Assert
        capabilities.SupportsNativeGeometry.Should().BeTrue("BigQuery supports GEOGRAPHY type");
        capabilities.SupportsTransactions.Should().BeFalse("BigQuery is analytics-oriented");
        capabilities.SupportsServerSideGeometryOperations.Should().BeTrue("BigQuery GIS functions available");
        capabilities.SupportsCrsTransformations.Should().BeFalse("BigQuery GEOGRAPHY is WGS84 only");
        capabilities.MaxQueryParameters.Should().Be(1000);
    }

    [Fact]
    public void CosmosDb_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = CosmosDbDataStoreCapabilities.Instance;

        // Assert
        capabilities.SupportsNativeGeometry.Should().BeTrue("Cosmos DB supports GeoJSON");
        capabilities.SupportsTransactions.Should().BeTrue("Cosmos DB supports transactions within partition");
        capabilities.SupportsSpatialIndexes.Should().BeTrue("Cosmos DB has geospatial indexing");
        capabilities.SupportsCrsTransformations.Should().BeFalse("Cosmos DB uses WGS84 only");
    }

    [Fact]
    public void MongoDB_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = MongoDbDataStoreCapabilities.Instance;

        // Assert
        capabilities.SupportsNativeGeometry.Should().BeTrue("MongoDB supports GeoJSON");
        capabilities.SupportsTransactions.Should().BeTrue("MongoDB supports multi-document transactions");
        capabilities.SupportsSpatialIndexes.Should().BeTrue("MongoDB has 2dsphere indexes");
        capabilities.SupportsServerSideGeometryOperations.Should().BeTrue("$geoIntersects, $geoWithin available");
    }

    [Fact]
    public void Snowflake_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = SnowflakeDataStoreCapabilities.Instance;

        // Assert
        capabilities.SupportsNativeGeometry.Should().BeTrue("Snowflake supports GEOGRAPHY/GEOMETRY");
        capabilities.SupportsTransactions.Should().BeTrue("Snowflake supports transactions");
        capabilities.SupportsSpatialIndexes.Should().BeFalse("Snowflake uses automatic clustering");
        capabilities.SupportsServerSideGeometryOperations.Should().BeTrue("ST_* functions available");
        capabilities.MaxQueryParameters.Should().Be(16384);
    }


    [Fact]
    public void Oracle_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = OracleDataStoreCapabilities.Instance;

        // Assert
        capabilities.SupportsNativeGeometry.Should().BeTrue("Oracle has SDO_GEOMETRY");
        capabilities.SupportsTransactions.Should().BeTrue();
        capabilities.SupportsSpatialIndexes.Should().BeTrue("Oracle has R-tree spatial indexes");
        capabilities.SupportsServerSideGeometryOperations.Should().BeTrue("SDO_* functions available");
        capabilities.SupportsCrsTransformations.Should().BeTrue("SDO_CS.TRANSFORM available");
    }

    [Fact]
    public void Redshift_ShouldHaveCorrectCapabilities()
    {
        // Arrange & Act
        var capabilities = RedshiftDataStoreCapabilities.Instance;

        // Assert
        capabilities.SupportsNativeGeometry.Should().BeTrue("Redshift has PostGIS-compatible functions");
        capabilities.SupportsTransactions.Should().BeTrue();
        capabilities.SupportsSpatialIndexes.Should().BeFalse("Redshift uses columnar storage");
        capabilities.SupportsCrsTransformations.Should().BeTrue("ST_Transform available");
        capabilities.SupportsReturningClause.Should().BeTrue();
    }
}
