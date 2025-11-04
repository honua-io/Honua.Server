using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Deployment.E2ETests.Infrastructure;
using Npgsql;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for STAC catalog functionality.
/// </summary>
[Trait("Category", "Integration")]
public class StacCatalogTests : IClassFixture<DeploymentTestFactory>
{
    private readonly DeploymentTestFactory _factory;

    public StacCatalogTests(DeploymentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StacRoot_ShouldReturnCatalogMetadata()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.GetProperty("type").GetString().Should().Be("Catalog");
        payload.RootElement.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        payload.RootElement.TryGetProperty("links", out _).Should().BeTrue();
    }

    [Fact]
    public async Task StacCollections_ShouldReturnEmptyListInitially()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        collections.EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task StacCollection_AfterInsert_ShouldBeQueryable()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        // Insert test STAC collection
        await InsertStacCollectionAsync("test-collection", "Test Collection", "Test STAC collection");

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var collections = payload!.RootElement.GetProperty("collections").EnumerateArray().ToList();
        collections.Should().ContainSingle(c => c.GetProperty("id").GetString() == "test-collection");
    }

    [Fact]
    public async Task StacCollectionById_ShouldReturnCollectionDetails()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        await InsertStacCollectionAsync("detailed-collection", "Detailed Collection", "Collection with details");

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac/collections/detailed-collection");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.GetProperty("id").GetString().Should().Be("detailed-collection");
        payload.RootElement.GetProperty("type").GetString().Should().Be("Collection");
        payload.RootElement.GetProperty("title").GetString().Should().Be("Detailed Collection");
    }

    [Fact]
    public async Task StacCollectionItems_ShouldReturnItemsInCollection()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var collectionId = "items-collection";
        await InsertStacCollectionAsync(collectionId, "Items Collection", "Collection with items");
        await InsertStacItemAsync(collectionId, "item-1", "Item 1");

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/stac/collections/{collectionId}/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
        var features = payload.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().ContainSingle(f => f.GetProperty("id").GetString() == "item-1");
    }

    [Fact]
    public async Task StacSearch_ShouldSupportCollectionFilter()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var collectionId = "searchable-collection";
        await InsertStacCollectionAsync(collectionId, "Searchable Collection", "Collection for search");
        await InsertStacItemAsync(collectionId, "search-item-1", "Search Item 1");

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/stac/search?collections={collectionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
        var features = payload.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().ContainSingle();
        features[0].GetProperty("collection").GetString().Should().Be(collectionId);
    }

    [Fact]
    public async Task StacSearch_WithPagination_ShouldReturnLimitedResults()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var collectionId = "pagination-collection";
        await InsertStacCollectionAsync(collectionId, "Pagination Collection", "Collection for pagination");

        // Insert multiple items
        for (int i = 1; i <= 10; i++)
        {
            await InsertStacItemAsync(collectionId, $"page-item-{i}", $"Page Item {i}");
        }

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/stac/search?collections={collectionId}&limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var features = payload!.RootElement.GetProperty("features").EnumerateArray().ToList();
        features.Should().HaveCount(5);

        // Verify pagination links
        var links = payload.RootElement.GetProperty("links").EnumerateArray().ToList();
        links.Should().Contain(l => l.GetProperty("rel").GetString() == "next");
    }

    [Fact]
    public async Task StacSearch_NonExistentCollection_ShouldReturnNotFound()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac/search?collections=non-existent-collection");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StacConformance_ShouldDeclareSTACVersion()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.TryGetProperty("stac_version", out var version).Should().BeTrue();
        version.GetString().Should().NotBeNullOrEmpty();
    }

    private async Task InsertStacCollectionAsync(string id, string title, string description)
    {
        await using var connection = new NpgsqlConnection(_factory.PostgresConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO stac_collections (
                id, title, description, license, version,
                keywords_json, extent_json, links_json, extensions_json,
                created_at, updated_at
            )
            VALUES (
                @id, @title, @description, 'proprietary', '1.0.0',
                '[]', '{}', '[]', '[]',
                NOW(), NOW()
            )
            ON CONFLICT (id) DO NOTHING;
        ";
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("description", description);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertStacItemAsync(string collectionId, string itemId, string title)
    {
        await using var connection = new NpgsqlConnection(_factory.PostgresConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO stac_items (
                collection_id, id, title, properties_json, assets_json, links_json, extensions_json,
                created_at, updated_at
            )
            VALUES (
                @collection_id, @id, @title, '{}', '{}', '[]', '[]',
                NOW(), NOW()
            )
            ON CONFLICT (collection_id, id) DO NOTHING;
        ";
        cmd.Parameters.AddWithValue("collection_id", collectionId);
        cmd.Parameters.AddWithValue("id", itemId);
        cmd.Parameters.AddWithValue("title", title);
        await cmd.ExecuteNonQueryAsync();
    }
}
