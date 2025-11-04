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

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ProviderInterfaceTests
{
    [Theory]
    [InlineData(typeof(BigQueryDataStoreProvider), "bigquery")]
    [InlineData(typeof(CosmosDbDataStoreProvider), "cosmosdb")]
    [InlineData(typeof(MongoDbDataStoreProvider), "mongodb")]
    [InlineData(typeof(ElasticsearchDataStoreProvider), "elasticsearch")]
    [InlineData(typeof(RedshiftDataStoreProvider), "redshift")]
    [InlineData(typeof(SnowflakeDataStoreProvider), "snowflake")]
    [InlineData(typeof(OracleDataStoreProvider), "oracle")]
    public void AllProviders_ShouldImplementIDataStoreProvider(Type providerType, string expectedProviderKey)
    {
        // Arrange & Act
        var provider = Activator.CreateInstance(providerType) as IDataStoreProvider;

        // Assert
        provider.Should().NotBeNull($"{providerType.Name} should implement IDataStoreProvider");
        provider!.Provider.Should().Be(expectedProviderKey, $"{providerType.Name} should have correct provider key");
        provider.Capabilities.Should().NotBeNull($"{providerType.Name} should have capabilities");
    }

    [Fact]
    public void BigQueryProvider_ShouldHaveCorrectProviderKey()
    {
        // Arrange
        var provider = new BigQueryDataStoreProvider();

        // Act & Assert
        provider.Provider.Should().Be("bigquery");
        provider.Provider.Should().Be(BigQueryDataStoreProvider.ProviderKey);
    }

    [Fact]
    public void CosmosDbProvider_ShouldHaveCorrectProviderKey()
    {
        // Arrange
        var provider = new CosmosDbDataStoreProvider();

        // Act & Assert
        provider.Provider.Should().Be("cosmosdb");
        provider.Provider.Should().Be(CosmosDbDataStoreProvider.ProviderKey);
    }

    [Fact]
    public void MongoDbProvider_ShouldHaveCorrectProviderKey()
    {
        // Arrange
        var provider = new MongoDbDataStoreProvider();

        // Act & Assert
        provider.Provider.Should().Be("mongodb");
        provider.Provider.Should().Be(MongoDbDataStoreProvider.ProviderKey);
    }

    [Fact]
    public void ElasticsearchProvider_ShouldHaveCorrectProviderKey()
    {
        // Arrange
        var provider = new ElasticsearchDataStoreProvider();

        // Act & Assert
        provider.Provider.Should().Be("elasticsearch");
        provider.Provider.Should().Be(ElasticsearchDataStoreProvider.ProviderKey);
    }

    [Fact]
    public void SnowflakeProvider_ShouldHaveCorrectProviderKey()
    {
        // Arrange
        var provider = new SnowflakeDataStoreProvider();

        // Act & Assert
        provider.Provider.Should().Be("snowflake");
        provider.Provider.Should().Be(SnowflakeDataStoreProvider.ProviderKey);
    }

}
