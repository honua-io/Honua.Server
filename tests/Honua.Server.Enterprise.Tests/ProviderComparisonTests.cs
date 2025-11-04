using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Enterprise.Data.BigQuery;
using Honua.Server.Enterprise.Data.CosmosDb;
using Honua.Server.Enterprise.Data.Elasticsearch;
using Honua.Server.Enterprise.Data.MongoDB;
using Honua.Server.Enterprise.Data.Redshift;
using Honua.Server.Enterprise.Data.Snowflake;
using Honua.Server.Enterprise.Data.Oracle;

namespace Honua.Server.Enterprise.Tests;

/// <summary>
/// Comparative tests across all enterprise providers to ensure consistency
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ProviderComparisonTests
{
    private static readonly (Type Type, string Key, IDataStoreCapabilities Caps)[] AllProviders =
    [
        (typeof(BigQueryDataStoreProvider), "bigquery", BigQueryDataStoreCapabilities.Instance),
        (typeof(CosmosDbDataStoreProvider), "cosmosdb", CosmosDbDataStoreCapabilities.Instance),
        (typeof(MongoDbDataStoreProvider), "mongodb", MongoDbDataStoreCapabilities.Instance),
        (typeof(ElasticsearchDataStoreProvider), "elasticsearch", ElasticsearchDataStoreCapabilities.Instance),
        (typeof(RedshiftDataStoreProvider), "redshift", RedshiftDataStoreCapabilities.Instance),
        (typeof(SnowflakeDataStoreProvider), "snowflake", SnowflakeDataStoreCapabilities.Instance),
        (typeof(OracleDataStoreProvider), "oracle", OracleDataStoreCapabilities.Instance),
    ];

    [Fact]
    public void AllProviders_ShouldHaveUniqueProviderKeys()
    {
        // Arrange & Act
        var keys = AllProviders.Select(p => p.Key).ToList();

        // Assert
        keys.Should().OnlyHaveUniqueItems("each provider must have a unique identifier");
    }

    [Fact]
    public void AllProviders_ShouldHaveNonNullCapabilities()
    {
        // Assert
        foreach (var (type, key, caps) in AllProviders)
        {
            caps.Should().NotBeNull($"{key} provider should have capabilities");
        }
    }

    [Fact]
    public void AllProviders_ShouldHaveValidMaxQueryParameters()
    {
        // Assert
        foreach (var (type, key, caps) in AllProviders)
        {
            caps.MaxQueryParameters.Should().BeGreaterThan(0,
                $"{key} provider should have positive MaxQueryParameters");
            caps.MaxQueryParameters.Should().BeLessThanOrEqualTo(100000,
                $"{key} provider MaxQueryParameters seems unreasonably high");
        }
    }

    [Theory]
    [InlineData("bigquery", false, false)] // Analytics DB
    [InlineData("cosmosdb", true, true)]   // Transactional NoSQL
    [InlineData("mongodb", true, true)]     // Transactional NoSQL
    [InlineData("elasticsearch", false, true)] // Search engine with geo indexes
    [InlineData("redshift", true, false)]   // Data warehouse
    [InlineData("snowflake", true, false)]  // Data warehouse
    [InlineData("oracle", true, true)]      // Enterprise RDBMS
    public void Provider_ShouldHaveExpectedTransactionSupport(string providerKey, bool supportsTransactions, bool supportsSpatialIndexes)
    {
        // Arrange
        var provider = AllProviders.Single(p => p.Key == providerKey);

        // Assert
        provider.Caps.SupportsTransactions.Should().Be(supportsTransactions,
            $"{providerKey} transaction support should match its architecture");
        provider.Caps.SupportsSpatialIndexes.Should().Be(supportsSpatialIndexes,
            $"{providerKey} spatial index support should match its architecture");
    }

    [Fact]
    public void AllProviders_ShouldSupportNativeGeometry()
    {
        // Assert - All enterprise providers should support spatial data
        foreach (var (type, key, caps) in AllProviders)
        {
            caps.SupportsNativeGeometry.Should().BeTrue(
                $"{key} is an enterprise spatial provider and must support native geometry");
        }
    }

    [Fact]
    public void AllProviders_ShouldSupportServerSideGeometryOperations()
    {
        // Assert - All enterprise providers should have server-side spatial functions
        foreach (var (type, key, caps) in AllProviders)
        {
            caps.SupportsServerSideGeometryOperations.Should().BeTrue(
                $"{key} should support server-side geometry operations for performance");
        }
    }

    [Fact]
    public void CloudProviders_ShouldNotSupportMVT()
    {
        // Cloud/managed databases typically don't have MVT generation
        var cloudProviders = new[] { "bigquery", "cosmosdb", "mongodb", "elasticsearch", "redshift", "snowflake", "oracle" };

        foreach (var key in cloudProviders)
        {
            var provider = AllProviders.Single(p => p.Key == key);
            provider.Caps.SupportsNativeMvt.Should().BeFalse(
                $"{key} should not have native MVT support (cloud/managed service)");
        }
    }

    [Fact]
    public void AnalyticsProviders_ShouldHaveHighParameterLimits()
    {
        // Analytics-oriented databases typically support more parameters
        var analyticsProviders = new[] { "bigquery", "snowflake", "redshift" };

        foreach (var key in analyticsProviders)
        {
            var provider = AllProviders.Single(p => p.Key == key);
            provider.Caps.MaxQueryParameters.Should().BeGreaterThanOrEqualTo(1000,
                $"{key} is analytics-oriented and should support many parameters");
        }
    }

    [Theory]
    [InlineData("bigquery", 1000)]
    [InlineData("cosmosdb", 2000)]
    [InlineData("mongodb", 10000)]
    [InlineData("snowflake", 16384)]
    [InlineData("oracle", 32767)]
    [InlineData("redshift", 32767)]
    [InlineData("elasticsearch", 10000)]
    public void Provider_ShouldHaveCorrectMaxQueryParameters(string providerKey, int expectedMax)
    {
        // Arrange
        var provider = AllProviders.Single(p => p.Key == providerKey);

        // Assert
        provider.Caps.MaxQueryParameters.Should().Be(expectedMax,
            $"{providerKey} should have documented parameter limit");
    }

    [Fact]
    public void WGS84OnlyProviders_ShouldNotSupportCrsTransformations()
    {
        // Providers that only support WGS84 cannot transform CRS
        var wgs84Only = new[] { "bigquery", "cosmosdb", "mongodb", "elasticsearch", "snowflake" };

        foreach (var key in wgs84Only)
        {
            var provider = AllProviders.Single(p => p.Key == key);
            provider.Caps.SupportsCrsTransformations.Should().BeFalse(
                $"{key} only supports WGS84 and cannot transform CRS");
        }
    }

    [Fact]
    public void PostGISCompatibleProviders_ShouldSupportCrsTransformations()
    {
        // PostGIS-compatible providers should support CRS transformations
        var postGISCompatible = new[] { "oracle", "redshift" };

        foreach (var key in postGISCompatible)
        {
            var provider = AllProviders.Single(p => p.Key == key);
            provider.Caps.SupportsCrsTransformations.Should().BeTrue(
                $"{key} is PostGIS/Oracle Spatial compatible and should support CRS transforms");
        }
    }
}
