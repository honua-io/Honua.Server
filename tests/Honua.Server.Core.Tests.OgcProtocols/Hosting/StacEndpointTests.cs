using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Stac;
using Honua.Server.Host.Stac;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Sdk;
using JsonArray = System.Text.Json.Nodes.JsonArray;
using JsonObject = System.Text.Json.Nodes.JsonObject;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Trait("Category", "Integration")]
public sealed class StacEnvironmentFixture : IDisposable
{
    private readonly string? _previous;
    private readonly bool _previousForce;

    public StacEnvironmentFixture()
    {
        _previous = Environment.GetEnvironmentVariable("HONUA_ENABLE_STAC_FIXTURE");
        _previousForce = HonuaWebApplicationFactory.ForceStac;
        Environment.SetEnvironmentVariable("HONUA_ENABLE_STAC_FIXTURE", "1");
        HonuaWebApplicationFactory.ForceStac = true;
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HONUA_ENABLE_STAC_FIXTURE", _previous);
        HonuaWebApplicationFactory.ForceStac = _previousForce;
    }
}

[CollectionDefinition("stac-endpoints", DisableParallelization = true)]
public sealed class StacEndpointCollection : ICollectionFixture<StacEnvironmentFixture>
{
}

[Collection("stac-endpoints")]
public sealed class StacEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private const string ProjectionExtension = "https://stac-extensions.github.io/projection/v1.0.0/schema.json";
    private const string CollectionId = "roads-imagery";
    private readonly HonuaWebApplicationFactory _factory;

    public StacEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCollectionItems_ReturnsNotFound_ForUnknownCollection()
    {
        await EnsureCatalogAsync(_factory);
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/stac/collections/missing-collection/items");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCollectionItems_NegativeLimit_DefaultsToTenAndEmitsNextToken()
    {
        await EnsureCatalogAsync(_factory);
        await SeedCollectionItemsAsync(_factory, desiredCount: 12);

        using var client = _factory.CreateAuthenticatedClient();
        var totalCount = await CountCollectionItemsAsync(_factory);

        var firstResponse = await client.GetAsync($"/stac/collections/{CollectionId}/items?limit=-5");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = JsonNode.Parse(await firstResponse.Content.ReadAsStringAsync())!.AsObject();
        var firstFeatures = firstPayload["features"]!.AsArray();
        firstFeatures.Count.Should().Be(10);
        firstPayload["context"]!.AsObject()["returned"]!.GetValue<int>().Should().Be(10);

        var nextHref = ExtractLink(firstPayload["links"]!.AsArray(), "next");
        nextHref.Should().Contain("limit=10", "pagination should preserve the default page size");
        var secondResponse = await client.GetAsync(ResolveHref(client, nextHref));

        var secondPayload = JsonNode.Parse(await secondResponse.Content.ReadAsStringAsync())!.AsObject();
        var secondFeatures = secondPayload["features"]!.AsArray();
        secondFeatures.Count.Should().BeLessThanOrEqualTo(10);
        (firstFeatures.Count + secondFeatures.Count).Should().BeGreaterThanOrEqualTo(totalCount);
        var secondContext = secondPayload["context"]?.AsObject() ?? throw new InvalidOperationException("STAC response missing context block.");
        secondContext["returned"]!.GetValue<int>().Should().Be(secondFeatures.Count);
    }

    [Fact]
    public async Task Search_ReturnsNotFound_WhenCollectionFilterDoesNotMatch()
    {
        await EnsureCatalogAsync(_factory);
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/stac/search?collections=does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_PaginatesWithTokenAndReportsMatches()
    {
        await EnsureCatalogAsync(_factory);
        await SeedCollectionItemsAsync(_factory, desiredCount: 15);

        using var client = _factory.CreateAuthenticatedClient();
        var totalCount = await CountCollectionItemsAsync(_factory);

        var firstResponse = await client.GetAsync($"/stac/search?collections={CollectionId}&limit=6");
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var firstPayload = JsonNode.Parse(await firstResponse.Content.ReadAsStringAsync())!.AsObject();
        var firstFeatures = firstPayload["features"]!.AsArray();
        firstFeatures.Count.Should().Be(6);
        firstPayload["context"]!.AsObject()["matched"]!.GetValue<int>().Should().BeGreaterThanOrEqualTo(6);

        var nextHref = ExtractLink(firstPayload["links"]!.AsArray(), "next");
        nextHref.Should().Contain("limit=6", "pagination should retain explicit search limits");
        var secondResponse = await client.GetAsync(ResolveHref(client, nextHref));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondPayload = JsonNode.Parse(await secondResponse.Content.ReadAsStringAsync())!.AsObject();
        var secondFeatures = secondPayload["features"]!.AsArray();
        var remaining = totalCount - firstFeatures.Count;
        var expectedSecondPage = Math.Min(remaining, 6);
        expectedSecondPage.Should().BeGreaterThan(0);
        secondFeatures.Count.Should().Be(expectedSecondPage);
        var secondContext = secondPayload["context"]?.AsObject() ?? throw new InvalidOperationException("STAC response missing context block.");
        secondContext["returned"]!.GetValue<int>().Should().Be(expectedSecondPage);
    }

    [Fact]
    public async Task GetRoot_ReturnsCatalogMetadata()
    {
        await EnsureCatalogAsync(_factory);
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetFromJsonAsync<StacRootResponse>("/stac");

        response.Should().NotBeNull();
        response!.Type.Should().Be("Catalog");
        response.Links.Should().Contain(link => link.Rel == "data");
    }

    [Fact]
    public async Task GetCollections_ReturnsSeededCollection()
    {
        await EnsureCatalogAsync(_factory);
        using var client = _factory.CreateAuthenticatedClient();

        var httpResponse = await client.GetAsync("/stac/collections");
        var body = await httpResponse.Content.ReadAsStringAsync();
        httpResponse.IsSuccessStatusCode.Should().BeTrue($"STAC collections request failed: {body}");
        var response = JsonSerializer.Deserialize<StacCollectionsResponse>(body, JsonSerializerOptionsRegistry.DevTooling);

        response.Should().NotBeNull("Response body: {0}", body);
        response!.Collections.Should().Contain(collection => collection.Id == CollectionId);
        response.Collections.Should().Contain(c => c.StacExtensions.Contains(ProjectionExtension));
    }

    [Fact]
    public async Task GetItems_ReturnsGeoJsonFeatureCollection()
    {
        await EnsureCatalogAsync(_factory);
        using var client = _factory.CreateAuthenticatedClient();

        var responseMessage = await client.GetAsync($"/stac/collections/{CollectionId}/items");
        responseMessage.EnsureSuccessStatusCode();

        var payloadNode = JsonNode.Parse(await responseMessage.Content.ReadAsStringAsync());
        payloadNode.Should().NotBeNull();
        var payload = payloadNode!.AsObject();
        var features = payload["features"]!.AsArray();
        payload["type"]!.GetValue<string>().Should().Be("FeatureCollection");
        features.Should().NotBeEmpty();
        var firstFeature = features.FirstOrDefault();
        firstFeature.Should().NotBeNull();

        if (firstFeature is not JsonObject firstFeatureObject)
        {
            throw new XunitException("Expected first feature to be a JSON object.");
        }

        var extensionNode = firstFeatureObject["stac_extensions"] ?? throw new XunitException("Expected stac_extensions property.");
        var extensionArray = Assert.IsType<JsonArray>(extensionNode);
#pragma warning disable CS8602 // extensionArray is guaranteed non-null by pattern checks above
        extensionArray.Should().ContainSingle().Which.GetValue<string>().Should().Be(ProjectionExtension);
#pragma warning restore CS8602

        var assetsNode = firstFeatureObject["assets"] ?? throw new XunitException("Expected assets property.");
        var assetsObject = Assert.IsType<JsonObject>(assetsNode);
#pragma warning disable CS8602 // assetsObject is guaranteed non-null by Assert above
        assetsObject.ContainsKey("thumbnail").Should().BeTrue();
#pragma warning restore CS8602
    }

    [Fact]
    public async Task Search_ReturnsFilteredFeatures()
    {
        await EnsureCatalogAsync(_factory);
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetFromJsonAsync<StacItemCollectionResponse>($"/stac/search?collections={CollectionId}");
        response.Should().NotBeNull();
        response!.Features.Should().NotBeEmpty();
        response.Features[0].StacExtensions.Should().Contain(ProjectionExtension);
        response.Context.Should().NotBeNull();
        response.Context!["matched"]!.GetValue<int>().Should().BeGreaterThan(0);
    }

    private static async Task EnsureCatalogAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStacCatalogStore>();
        await store.EnsureInitializedAsync();
        var collections = await store.ListCollectionsAsync();
        if (collections.Any(c => string.Equals(c.Id, CollectionId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var synchronizer = scope.ServiceProvider.GetRequiredService<IRasterStacCatalogSynchronizer>();
        await synchronizer.SynchronizeAllAsync();

        collections = await store.ListCollectionsAsync();
        if (collections.Any(c => string.Equals(c.Id, CollectionId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (factory is HonuaWebApplicationFactory honuaFactory)
        {
            await SeedCollectionItemsAsync(honuaFactory, desiredCount: 3);
        }
    }

    private static async Task SeedCollectionItemsAsync(HonuaWebApplicationFactory factory, int desiredCount)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStacCatalogStore>();

        await store.EnsureInitializedAsync();

        var collection = await store.GetCollectionAsync(CollectionId) ?? await CreateFallbackCollectionAsync(store);

        var allItems = (await store.ListItemsAsync(CollectionId, 1000, cancellationToken: default)).ToList();

        foreach (var extra in allItems.Where(i => i.Id.StartsWith($"{CollectionId}-extra-", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            await store.DeleteItemAsync(CollectionId, extra.Id, cancellationToken: default);
            allItems.Remove(extra);
        }

        if (allItems.Count == 0)
        {
            await CreateFallbackItemAsync(store, collection);
            allItems = (await store.ListItemsAsync(CollectionId, 1000, cancellationToken: default)).ToList();
        }

        if (allItems.Count == 0)
        {
            throw new InvalidOperationException("Unable to seed baseline STAC items for pagination tests.");
        }

        var baseline = await NormalizeBaselineItemAsync(store, allItems[0]);
        allItems[0] = baseline;

        var nextIndex = 1;
        while (allItems.Count < desiredCount)
        {
            var timestamp = DateTimeOffset.UtcNow.AddMinutes(nextIndex);
            var clone = baseline with
            {
                Id = $"{CollectionId}-extra-{nextIndex:D2}",
                Datetime = timestamp,
                StartDatetime = timestamp.AddMinutes(-5),
                EndDatetime = timestamp,
                CreatedAtUtc = timestamp,
                UpdatedAtUtc = timestamp
            };

            await store.UpsertItemAsync(clone, cancellationToken: default);
            allItems.Add(clone);
            nextIndex++;
        }
    }

    private static async Task<int> CountCollectionItemsAsync(HonuaWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IStacCatalogStore>();
        var items = await store.ListItemsAsync(CollectionId, 1000, cancellationToken: default);
        return items.Count;
    }

    private static async Task<StacCollectionRecord> CreateFallbackCollectionAsync(IStacCatalogStore store)
    {
        var now = DateTimeOffset.UtcNow;
        var collection = new StacCollectionRecord
        {
            Id = CollectionId,
            Title = "Seed Collection",
            Description = "Synthetic imagery collection for tests",
            License = "proprietary",
            Keywords = new[] { "roads", "imagery" },
            Extent = new StacExtent
            {
                Spatial = new List<double[]> { new[] { -10d, -10d, 10d, 10d } },
                Temporal = new List<StacTemporalInterval>
                {
                    new() { Start = now.AddDays(-1), End = now }
                }
            },
            Properties = new JsonObject
            {
                ["honua:serviceId"] = "roads",
                ["honua:layerId"] = "roads-primary",
                ["thumbnail"] = "https://example.test/thumbs/roads.png"
            },
            ServiceId = "roads",
            LayerId = "roads-primary",
            Extensions = new[] { ProjectionExtension },
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await store.UpsertCollectionAsync(collection);
        return collection;
    }

    private static async Task CreateFallbackItemAsync(IStacCatalogStore store, StacCollectionRecord collection)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var assets = new Dictionary<string, StacAsset>(StringComparer.OrdinalIgnoreCase)
        {
            ["cog"] = new StacAsset
            {
                Href = "https://example.test/roads-imagery.tif",
                Type = "image/tiff; application=geotiff; profile=cloud-optimized",
                Roles = new[] { "data" },
                Properties = new JsonObject
                {
                    ["honua:sourceType"] = "cog",
                    ["honua:defaultStyleId"] = "natural-color",
                    ["honua:styleIds"] = new JsonArray("natural-color", "infrared")
                }
            },
            ["thumbnail"] = new StacAsset
            {
                Href = "https://example.test/thumbs/roads.png",
                Type = "image/png",
                Roles = new[] { "thumbnail" }
            }
        };

        var properties = new JsonObject
        {
            ["title"] = "Seed Item",
            ["honua:serviceId"] = collection.ServiceId ?? "roads",
            ["honua:layerId"] = collection.LayerId ?? "roads-primary",
            ["proj:epsg"] = 4326
        };

        var item = new StacItemRecord
        {
            Id = $"{CollectionId}-seed",
            CollectionId = CollectionId,
            Title = "Seed Item",
            Description = "Seed raster item",
            Properties = properties,
            Assets = assets,
            Bbox = new[] { -10d, -10d, 10d, 10d },
            Geometry = null,
            Datetime = timestamp,
            StartDatetime = timestamp.AddMinutes(-5),
            EndDatetime = timestamp,
            RasterDatasetId = CollectionId,
            Extensions = new[] { ProjectionExtension },
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };

        await store.UpsertItemAsync(item);
    }

    private static async Task<StacItemRecord> NormalizeBaselineItemAsync(IStacCatalogStore store, StacItemRecord baseline)
    {
        var properties = baseline.Properties is null
            ? new JsonObject()
            : JsonNode.Parse(baseline.Properties.ToJsonString())!.AsObject();

        properties["honua:serviceId"] ??= "roads";
        properties["honua:layerId"] ??= "roads-primary";
        properties["proj:epsg"] ??= 4326;

        var normalized = baseline with
        {
            Properties = properties,
            Extensions = baseline.Extensions.Count == 0 ? new[] { ProjectionExtension } : baseline.Extensions,
            Assets = EnsureThumbnailAsset(baseline.Assets)
        };

        await store.UpsertItemAsync(normalized);
        return normalized;
    }

    private static IReadOnlyDictionary<string, StacAsset> EnsureThumbnailAsset(IReadOnlyDictionary<string, StacAsset> assets)
    {
        if (assets.ContainsKey("thumbnail"))
        {
            return assets;
        }

        var dictionary = new Dictionary<string, StacAsset>(assets, StringComparer.OrdinalIgnoreCase)
        {
            ["thumbnail"] = new StacAsset
            {
                Href = "https://example.test/thumbs/roads.png",
                Type = "image/png",
                Roles = new[] { "thumbnail" }
            }
        };

        return dictionary;
    }

    private static string ExtractLink(JsonArray links, string rel)
    {
        foreach (var linkNode in links)
        {
            if (linkNode is null)
            {
                continue;
            }

            var relValue = linkNode["rel"]?.GetValue<string>();
            if (!string.Equals(relValue, rel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var href = linkNode["href"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(href))
            {
                return href;
            }
        }

        throw new XunitException($"Link with rel '{rel}' was not found in STAC response links array.");
    }

    private static string ResolveHref(HttpClient client, string href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            throw new XunitException("STAC link href was empty.");
        }

        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        var baseAddress = client.BaseAddress ?? throw new InvalidOperationException("HttpClient is missing BaseAddress for relative STAC link resolution.");

        if (href.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            var fileUri = new Uri(href);
            var decodedPath = Uri.UnescapeDataString(fileUri.AbsolutePath);
            var relative = string.IsNullOrEmpty(fileUri.Query)
                ? decodedPath
                : string.Concat(decodedPath, fileUri.Query);
            return new Uri(baseAddress, relative).ToString();
        }

        return new Uri(baseAddress, href).ToString();
    }
}
