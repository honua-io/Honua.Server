using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Deployment.E2ETests.Infrastructure;
using Npgsql;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for OGC API Features endpoints.
/// </summary>
[Trait("Category", "Integration")]
public class OgcApiFeaturesTests : IClassFixture<DeploymentTestFactory>
{
    private readonly DeploymentTestFactory _factory;

    public OgcApiFeaturesTests(DeploymentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OgcLandingPage_ShouldReturnCatalogMetadata()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("ogc-test", "OGC API Test Catalog", "Testing OGC API Features")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("features-svc", "Feature Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        payload!.RootElement.GetProperty("catalog").GetProperty("id").GetString().Should().Be("ogc-test");
        payload.RootElement.GetProperty("catalog").GetProperty("title").GetString().Should().Be("OGC API Test Catalog");
    }

    [Fact]
    public async Task OgcConformance_ShouldReturnConformanceClasses()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/conformance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var conformsTo = payload!.RootElement.GetProperty("conformsTo").EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        conformsTo.Should().Contain(c => c!.Contains("ogcapi-features"));
    }

    [Fact]
    public async Task Collections_WithService_ShouldListCollections()
    {
        // Arrange
        await SetupTestTableAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("collections-test", "Collections Test", "Test collections endpoint")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("test-svc", "Test Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/test-svc/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        payload!.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        collections.EnumerateArray().Should().NotBeEmpty();
    }

    [Fact]
    public async Task CollectionItems_ShouldReturnFeatureCollection()
    {
        // Arrange
        await SetupTestTableAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("items-test", "Items Test", "Test items endpoint")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("geo-svc", "Geo Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Get first collection
        var collectionsResponse = await client.GetAsync("/ogc/geo-svc/collections");
        collectionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var collectionsPayload = await collectionsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var firstCollection = collectionsPayload!.RootElement
            .GetProperty("collections")
            .EnumerateArray()
            .FirstOrDefault();

        if (firstCollection.ValueKind != JsonValueKind.Undefined)
        {
            var collectionId = firstCollection.GetProperty("id").GetString();

            // Act - Get items from collection
            var itemsResponse = await client.GetAsync($"/ogc/geo-svc/collections/{collectionId}/items");

            // Assert
            itemsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var itemsPayload = await itemsResponse.Content.ReadFromJsonAsync<JsonDocument>();

            itemsPayload!.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
            itemsPayload.RootElement.TryGetProperty("features", out _).Should().BeTrue();
        }
    }

    [Fact]
    public async Task OgcApiFeatures_ContentNegotiation_ShouldSupportHTMLAndJSON()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Request JSON (default)
        var jsonResponse = await client.GetAsync("/ogc");
        jsonResponse.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        // Act - Request HTML
        var htmlRequest = new HttpRequestMessage(HttpMethod.Get, "/ogc");
        htmlRequest.Headers.Accept.ParseAdd("text/html");
        var htmlResponse = await client.SendAsync(htmlRequest);

        // Assert
        htmlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        htmlResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        var htmlContent = await htmlResponse.Content.ReadAsStringAsync();
        htmlContent.Should().ContainEquivalentOf("<html");
    }

    [Fact]
    public async Task OgcApiFeatures_Pagination_ShouldSupportLimitParameter()
    {
        // Arrange
        await SetupTestTableAsync();

        var metadata = new TestMetadataBuilder()
            .WithCatalog("pagination-test", "Pagination Test", "Test pagination")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("page-svc", "Page Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Get a collection
        var collectionsResponse = await client.GetAsync("/ogc/page-svc/collections");
        var collectionsPayload = await collectionsResponse.Content.ReadFromJsonAsync<JsonDocument>();
        var firstCollection = collectionsPayload!.RootElement
            .GetProperty("collections")
            .EnumerateArray()
            .FirstOrDefault();

        if (firstCollection.ValueKind != JsonValueKind.Undefined)
        {
            var collectionId = firstCollection.GetProperty("id").GetString();

            // Act - Request with limit
            var response = await client.GetAsync($"/ogc/page-svc/collections/{collectionId}/items?limit=5");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

            var features = payload!.RootElement.GetProperty("features").EnumerateArray().ToList();
            features.Should().HaveCountLessThanOrEqualTo(5);
        }
    }

    [Fact]
    public async Task OgcApiFeatures_CRSSupport_ShouldListSupportedCRS()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("crs-test", "CRS Test", "Test CRS support")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("crs-svc", "CRS Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/crs-svc/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var collections = payload!.RootElement.GetProperty("collections").EnumerateArray();
        foreach (var collection in collections)
        {
            if (collection.TryGetProperty("crs", out var crs))
            {
                var crsList = crs.EnumerateArray().Select(c => c.GetString()).ToList();
                crsList.Should().Contain(c => c!.Contains("4326"));
            }
        }
    }

    private async Task SetupTestTableAsync()
    {
        await using var connection = new NpgsqlConnection(_factory.PostgresConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE EXTENSION IF NOT EXISTS postgis;
            CREATE TABLE IF NOT EXISTS test_points (
                id SERIAL PRIMARY KEY,
                name TEXT,
                geom GEOMETRY(Point, 4326)
            );
            INSERT INTO test_points (name, geom)
            VALUES ('Point 1', ST_SetSRID(ST_MakePoint(-122.4, 37.8), 4326))
            ON CONFLICT DO NOTHING;
        ";
        await cmd.ExecuteNonQueryAsync();
    }
}
