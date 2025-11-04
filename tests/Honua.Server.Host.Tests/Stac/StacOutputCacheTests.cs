using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Server.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Honua.Server.Host.Tests.Stac;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "STAC")]
[Trait("Speed", "Slow")]
public sealed class StacOutputCacheTests : IClassFixture<StacWebApplicationFactory>
{
    private readonly StacWebApplicationFactory _factory;

    public StacOutputCacheTests(StacWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCollections_ShouldReturnCacheHeaders()
    {
        // Arrange
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        using var response = await client.GetAsync("/stac/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().NotBeNull();

        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        document.RootElement.GetProperty("collections")[0].GetProperty("id").GetString()
            .Should().Be("sample-collection");
    }

    [Fact]
    public async Task GetRoot_AllowsAnonymousAccess()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/stac");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetConformance_AllowsAnonymousAccess()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/stac/conformance");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

public sealed class StacWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _metadataPath;

    public StacWebApplicationFactory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"honua-stac-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        _metadataPath = Path.Combine(root, "metadata.json");
        File.WriteAllText(_metadataPath, StacTestData.MetadataJson);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["honua:metadata:path"] = _metadataPath,
                ["honua:metadata:provider"] = "json",
                ["honua:authentication:mode"] = "QuickStart",
                ["honua:authentication:quickStart:enabled"] = "true",
                ["honua:authentication:enforce"] = "false",
                ["honua:authentication:allowQuickStart"] = "true",
                ["RateLimiting:Enabled"] = "false",
                ["ConnectionStrings:Redis"] = "",
                ["AllowedHosts"] = "localhost",
                ["Performance:OutputCache:MaxSizeMB"] = "256",
                ["honua:cors:allowAnyOrigin"] = "true"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IStacCatalogStore>();
            services.AddSingleton<IStacCatalogStore, TestStacCatalogStore>();

            // Ensure metadata provider reads from our in-memory file without watchers
            services.RemoveAll<IMetadataProvider>();
            services.AddSingleton<IMetadataProvider>(_ => new JsonMetadataProvider(_metadataPath, watchForChanges: false));
        });
    }

    public new void Dispose()
    {
        base.Dispose();
        try
        {
            if (File.Exists(_metadataPath))
            {
                File.Delete(_metadataPath);
            }

            var directory = Path.GetDirectoryName(_metadataPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup; ignore IO race conditions in tests.
        }
    }
}

internal sealed class TestStacCatalogStore : IStacCatalogStore
{
    private readonly IReadOnlyList<StacCollectionRecord> _collections = new[]
    {
        new StacCollectionRecord
        {
            Id = "sample-collection",
            Title = "Sample Collection",
            Description = "Test collection for verifying STAC output cache headers",
            ETag = "\"stac-sample-etag\""
        }
    };

    public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<StacCollectionRecord?> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        var collection = _collections.FirstOrDefault(c => string.Equals(c.Id, collectionId, StringComparison.Ordinal));
        return Task.FromResult(collection);
    }

    public Task<IReadOnlyList<StacCollectionRecord>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_collections);
    }

    public Task<StacCollectionListResult> ListCollectionsAsync(int limit, string? token = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StacCollectionListResult
        {
            Collections = _collections,
            TotalCount = _collections.Count,
            NextToken = null
        });
    }

    public Task<IReadOnlyList<StacItemRecord>> ListItemsAsync(string collectionId, int limit, string? pageToken = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<StacItemRecord>>(Array.Empty<StacItemRecord>());
    }

    public Task<StacItemRecord?> GetItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<StacItemRecord?>(null);
    }

    public Task UpsertCollectionAsync(StacCollectionRecord collection, string? expectedETag = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> DeleteCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task UpsertItemAsync(StacItemRecord item, string? expectedETag = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<BulkUpsertResult> BulkUpsertItemsAsync(IReadOnlyList<StacItemRecord> items, BulkUpsertOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<bool> DeleteItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public Task<StacSearchResult> SearchAsync(StacSearchParameters parameters, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}

internal static class StacTestData
{
    public const string MetadataJson = """
{
  "server": {
    "allowedHosts": [ "localhost" ]
  },
  "catalog": {
    "id": "honua-test",
    "title": "Honua STAC Test Catalog",
    "description": "Minimal metadata for STAC output cache tests",
    "version": "1.0.0"
  },
  "folders": [
    {
      "id": "test",
      "title": "Test Folder"
    }
  ],
  "dataSources": [
    {
      "id": "in-memory",
      "provider": "sqlite",
      "connectionString": "Data Source=:memory:"
    }
  ],
  "services": [
    {
      "id": "stac-service",
      "title": "STAC Service",
      "folderId": "test",
      "serviceType": "feature",
      "dataSourceId": "in-memory",
      "enabled": true,
      "description": "Service used for STAC cache header verification",
      "ogc": {
        "collectionsEnabled": true,
        "itemLimit": 1000
      }
    }
  ],
  "layers": [
    {
      "id": "stac-layer",
      "serviceId": "stac-service",
      "title": "STAC Layer",
      "geometryType": "Polygon",
      "idField": "id",
      "displayField": "name",
      "geometryField": "geom",
      "crs": [ "EPSG:4326" ]
    }
  ]
}
""";
}
