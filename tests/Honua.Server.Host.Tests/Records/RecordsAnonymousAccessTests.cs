using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Host.Tests.Records;

[Trait("Category", "Integration")]
[Trait("Feature", "Records")]
public sealed class RecordsAnonymousAccessTests : IClassFixture<RecordsWebApplicationFactory>
{
    private readonly RecordsWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public RecordsAnonymousAccessTests(RecordsWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task GetRecordsLanding_AllowsAnonymousAccess()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/records");
        var payload = await response.Content.ReadAsStringAsync();

        _output.WriteLine(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Response payload: {payload}");
    }

    [Fact]
    public async Task GetRecordsConformance_AllowsAnonymousAccess()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.GetAsync("/records/conformance");
        var payload = await response.Content.ReadAsStringAsync();

        _output.WriteLine(payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Response payload: {payload}");
    }
}

public sealed class RecordsWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _metadataPath;

    public RecordsWebApplicationFactory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"honua-records-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        _metadataPath = Path.Combine(root, "metadata.json");
        File.WriteAllText(_metadataPath, MetadataJson);
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
                ["ConnectionStrings:Redis"] = ""
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMetadataProvider>();
            services.AddSingleton<IMetadataProvider>(_ => new JsonMetadataProvider(_metadataPath, watchForChanges: false));

            services.PostConfigure<Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions>(options =>
            {
                options.DocumentFilterDescriptors.RemoveAll(descriptor =>
                    descriptor.Type == typeof(Honua.Server.Host.OpenApi.Filters.VersionInfoDocumentFilter));
            });
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

    private const string MetadataJson = """
{
  "server": {
    "allowedHosts": [ "localhost" ]
  },
  "catalog": {
    "id": "honua-test",
    "title": "Honua Records Test Catalog",
    "description": "Metadata for records anonymous access tests",
    "version": "1.0.0"
  },
  "folders": [
    {
      "id": "test-folder",
      "title": "Records Folder"
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
      "id": "records-service",
      "title": "Records Service",
      "folderId": "test-folder",
      "serviceType": "feature",
      "dataSourceId": "in-memory",
      "enabled": true,
      "description": "Service used for records anonymous access verification",
      "ogc": {
        "collectionsEnabled": true,
        "itemLimit": 1000
      }
    }
  ],
  "layers": [
    {
      "id": "records-layer",
      "serviceId": "records-service",
      "title": "Records Layer",
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
