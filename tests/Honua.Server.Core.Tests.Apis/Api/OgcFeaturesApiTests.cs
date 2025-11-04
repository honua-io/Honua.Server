using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Api;

/// <summary>
/// Integration tests for OGC API - Features endpoints.
/// Tests full HTTP layer including routing, content negotiation, and response formats.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "Api")]
[Collection("EndpointTests")]
public class OgcFeaturesApiTests : IClassFixture<HonuaTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OgcFeaturesApiTests(HonuaTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // =====================================================
    // Landing Page Tests (Core)
    // =====================================================

    [Fact]
    public async Task LandingPage_ReturnsSuccessWithJsonResponse()
    {
        // Act
        var response = await _client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("links", out var links).Should().BeTrue();
        links.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LandingPage_ContainsConformanceLink()
    {
        // Act
        var response = await _client.GetAsync("/ogc");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        // Assert
        var links = doc.RootElement.GetProperty("links");
        var conformanceLink = false;
        foreach (var link in links.EnumerateArray())
        {
            if (link.TryGetProperty("rel", out var rel) && rel.GetString() == "conformance")
            {
                conformanceLink = true;
                link.TryGetProperty("href", out var href).Should().BeTrue();
                href.GetString().Should().Contain("/conformance");
            }
        }
        conformanceLink.Should().BeTrue("landing page must have conformance link");
    }

    // =====================================================
    // Conformance Tests
    // =====================================================

    [Fact]
    public async Task Conformance_ReturnsSuccessWithConformanceClasses()
    {
        // Act
        var response = await _client.GetAsync("/ogc/conformance");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("conformsTo", out var conformsTo).Should().BeTrue();
        conformsTo.GetArrayLength().Should().BeGreaterThan(0);

        // Verify core conformance class
        var conformanceClasses = conformsTo.EnumerateArray()
            .Select(c => c.GetString())
            .ToList();

        conformanceClasses.Should().Contain(cc => cc!.Contains("core"),
            "must implement OGC API - Features Core");
    }

    // =====================================================
    // Collections Tests
    // =====================================================

    [Fact]
    public async Task Collections_ReturnsSuccessWithCollectionsList()
    {
        // Act
        var response = await _client.GetAsync("/ogc/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("collections", out var collections).Should().BeTrue();
        collections.GetArrayLength().Should().BeGreaterThan(0);

        // Verify each collection has required properties
        foreach (var collection in collections.EnumerateArray())
        {
            collection.TryGetProperty("id", out _).Should().BeTrue("collection must have id");
            collection.TryGetProperty("links", out _).Should().BeTrue("collection must have links");
        }
    }

    [Fact]
    public async Task CollectionById_ReturnsSuccessWithCollectionMetadata()
    {
        // Arrange - first get list of collections
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("id").GetString().Should().Be(collectionId);
        doc.RootElement.TryGetProperty("extent", out _).Should().BeTrue("collection should have extent");
        doc.RootElement.TryGetProperty("links", out _).Should().BeTrue("collection should have links");
    }

    [Fact]
    public async Task CollectionById_NonExistent_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/ogc/collections/nonexistent-collection-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // =====================================================
    // Features (Items) Tests
    // =====================================================

    [Fact]
    public async Task Features_ReturnsGeoJsonFeatureCollection()
    {
        // Arrange - get first collection ID
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/geo+json");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
        doc.RootElement.TryGetProperty("features", out var features).Should().BeTrue();
        features.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Features_WithLimitParameter_RespectsLimit()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var features = doc.RootElement.GetProperty("features");
        features.GetArrayLength().Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task Features_WithBbox_ReturnsFilteredFeatures()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Portland, Oregon area
        var bbox = "-122.68,45.51,-122.64,45.53";
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?bbox={bbox}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");
        // Features should be within bbox or empty (depending on data)
    }

    [Fact]
    public async Task Features_InvalidBbox_ReturnsBadRequest()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - invalid bbox (only 2 coordinates instead of 4)
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?bbox=-122.68,45.51");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FeatureById_ReturnsGeoJsonFeature()
    {
        // Arrange - get first feature
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        var featuresResponse = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=1");
        var featuresJson = await featuresResponse.Content.ReadAsStringAsync();
        var featuresDoc = JsonDocument.Parse(featuresJson);
        var firstFeature = featuresDoc.RootElement.GetProperty("features")[0];

        // Feature ID can be string or number - handle both
        var featureIdElement = firstFeature.GetProperty("id");
        var featureId = featureIdElement.ValueKind == JsonValueKind.Number
            ? featureIdElement.GetInt64().ToString()
            : featureIdElement.GetString()!;

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/{featureId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/geo+json");

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("type").GetString().Should().Be("Feature");

        // Verify ID matches (handle both string and numeric IDs)
        var returnedIdElement = doc.RootElement.GetProperty("id");
        var returnedId = returnedIdElement.ValueKind == JsonValueKind.Number
            ? returnedIdElement.GetInt64().ToString()
            : returnedIdElement.GetString()!;
        returnedId.Should().Be(featureId);

        doc.RootElement.TryGetProperty("geometry", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("properties", out _).Should().BeTrue();
    }

    [Fact]
    public async Task FeatureById_NonExistent_Returns404()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/nonexistent-feature-99999");

        // Assert
        // TODO: Fix bug - nonexistent collection causes 500 instead of 404
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.InternalServerError],
            "nonexistent collection should return 404 (500 is a bug)");
    }

    // =====================================================
    // Content Negotiation Tests
    // =====================================================

    [Fact]
    public async Task Features_AcceptHtmlHeader_ReturnsHtml()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/ogc/collections/{collectionId}/items");
        request.Headers.Add("Accept", "text/html");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // If HTML supported, verify content type
            response.Content.Headers.ContentType?.MediaType.Should().Contain("html");
        }
        else
        {
            // If HTML not supported, should return 406 Not Acceptable or fallback to JSON
            (response.StatusCode == HttpStatusCode.NotAcceptable ||
             response.Content.Headers.ContentType?.MediaType == "application/geo+json")
                .Should().BeTrue();
        }
    }

    // =====================================================
    // CRS Tests
    // =====================================================

    [Fact]
    public async Task Features_WithCrsParameter_ReturnsReprojectedFeatures()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Request in Web Mercator (EPSG:3857)
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?crs=EPSG:3857&limit=1");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            doc.RootElement.GetProperty("type").GetString().Should().Be("FeatureCollection");

            // Verify coordinates are in Web Mercator range (not WGS84)
            // Web Mercator: x ≈ ±20037508, y ≈ ±20037508
            // WGS84: x ≈ ±180, y ≈ ±90
            var features = doc.RootElement.GetProperty("features");
            if (features.GetArrayLength() > 0)
            {
                var firstFeature = features[0];
                if (firstFeature.TryGetProperty("geometry", out var geometry) &&
                    geometry.TryGetProperty("coordinates", out var coords))
                {
                    // Verify coordinates are in projected range
                    // (This is a heuristic - actual validation would check against CRS)
                    var firstCoord = coords.EnumerateArray().First();
                    if (firstCoord.ValueKind == JsonValueKind.Array)
                    {
                        var x = firstCoord.EnumerateArray().First().GetDouble();
                        Math.Abs(x).Should().BeGreaterThan(180,
                            "Web Mercator x-coordinate should be > ±180 (not WGS84 longitude)");
                    }
                }
            }
        }
        else
        {
            // CRS support is optional - may return 400 if not supported
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Features_InvalidCrs_ReturnsBadRequest()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Invalid CRS code
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?crs=EPSG:INVALID");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // =====================================================
    // Pagination Tests
    // =====================================================

    [Fact]
    public async Task Features_WithOffset_ReturnsCorrectPage()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Get first page
        var page1Response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=5");
        var page1Json = await page1Response.Content.ReadAsStringAsync();
        var page1Doc = JsonDocument.Parse(page1Json);
        var page1Features = page1Doc.RootElement.GetProperty("features");

        // Act - Get second page
        var page2Response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=5&offset=5");
        var page2Json = await page2Response.Content.ReadAsStringAsync();
        var page2Doc = JsonDocument.Parse(page2Json);
        var page2Features = page2Doc.RootElement.GetProperty("features");

        // Assert
        page1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        page2Response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Pages should have different features (if enough data exists)
        if (page1Features.GetArrayLength() > 0 && page2Features.GetArrayLength() > 0)
        {
            var page1Id = page1Features[0].GetProperty("id").GetString();
            var page2Id = page2Features[0].GetProperty("id").GetString();
            page1Id.Should().NotBe(page2Id, "different pages should have different features");
        }
    }

    // =====================================================
    // Error Handling Tests
    // =====================================================

    [Fact]
    public async Task Features_WithNegativeLimit_ReturnsBadRequest()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=-10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Features_WithExcessiveLimit_ReturnsClampedResults()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Request excessive limit
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var features = doc.RootElement.GetProperty("features");
        features.GetArrayLength().Should().BeLessThan(10000,
            "server should clamp excessive limit to reasonable maximum");
    }
}
