// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

/// <summary>
/// Docker container-based test fixture for OData endpoint testing.
/// Starts a real Honua Server instance in a container to avoid WebApplicationFactory limitations.
/// </summary>
public sealed class ODataContainerFixture : IAsyncLifetime
{
    private IContainer? _container;
    private readonly string _testDataPath;

    public string BaseUrl { get; private set; } = string.Empty;
    public HttpClient Client { get; private set; } = null!;

    public ODataContainerFixture()
    {
        // Create test data directory for metadata JSON files
        _testDataPath = Path.Combine(Path.GetTempPath(), $"honua-odata-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataPath);
    }

    public async Task InitializeAsync()
    {
        // Get solution directory (assumes tests are in tests/Honua.Server.Core.Tests.OgcProtocols)
        var solutionDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".."));

        // Build Honua server image from Dockerfile
        var image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(solutionDir)
            .WithDockerfile("Dockerfile")
            .WithName($"honua-server:odata-test-{Guid.NewGuid():N}")
            .WithCleanUp(true)
            .Build();

        await image.CreateAsync();

        // Create test metadata JSON (will be implemented in next step)
        var metadataPath = CreateTestMetadata();

        // Start container with test configuration
        _container = new ContainerBuilder()
            .WithImage(image)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
            .WithEnvironment("HONUA__METADATA__PATH", "/app/testdata/metadata.json")
            .WithEnvironment("ConnectionStrings__HonuaDb", "Data Source=/app/testdata/test.db")
            .WithBindMount(_testDataPath, "/app/testdata")
            .WithPortBinding(8080, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/healthz/live")
                    .ForPort(8080)))
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync();

        // Get mapped port and create client
        var port = _container.GetMappedPublicPort(8080);
        BaseUrl = $"http://localhost:{port}";
        Client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (_container != null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        // Clean up test data directory
        if (Directory.Exists(_testDataPath))
        {
            try
            {
                Directory.Delete(_testDataPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTestMetadata()
    {
        var metadataPath = Path.Combine(_testDataPath, "metadata.json");

        // Generate valid metadata JSON that matches JsonMetadataProvider schema
        var metadata = $$"""
        {
          "catalog": {
            "id": "honua-odata-test",
            "title": "Honua OData Test Catalog",
            "description": "Test catalog for OData endpoint tests",
            "version": "1.0.0"
          },
          "folders": [
            {
              "id": "transportation",
              "title": "Transportation"
            }
          ],
          "dataSources": [
            {
              "id": "sqlite-primary",
              "provider": "sqlite",
              "connectionString": "Data Source=file:honua-odata-test?mode=memory&cache=shared"
            }
          ],
          "services": [
            {
              "id": "roads",
              "title": "Road Centerlines",
              "folderId": "transportation",
              "serviceType": "feature",
              "dataSourceId": "sqlite-primary",
              "enabled": true,
              "description": "Road centerline reference layer",
              "ogc": {
                "collectionsEnabled": true,
                "itemLimit": 1000
              }
            }
          ],
          "layers": [
            {
              "id": "roads-primary",
              "serviceId": "roads",
              "title": "Primary Roads",
              "geometryType": "LineString",
              "idField": "road_id",
              "displayField": "name",
              "geometryField": "geom",
              "itemType": "feature",
              "storage": {
                "table": "roads_primary",
                "geometryColumn": "geom",
                "primaryKey": "road_id",
                "srid": 4326
              },
              "fields": [
                {
                  "name": "road_id",
                  "dataType": "int",
                  "nullable": false
                },
                {
                  "name": "name",
                  "dataType": "string",
                  "nullable": true
                },
                {
                  "name": "status",
                  "dataType": "string",
                  "nullable": true
                },
                {
                  "name": "length_km",
                  "dataType": "double",
                  "nullable": true
                }
              ]
            }
          ],
          "styles": [],
          "layerGroups": [],
          "server": {
            "allowedHosts": ["*"]
          }
        }
        """;

        File.WriteAllText(metadataPath, metadata);

        return metadataPath;
    }
}

/// <summary>
/// xUnit collection definition for OData container tests.
/// All tests in this collection will share the same container instance.
/// </summary>
[CollectionDefinition("ODataContainer")]
public class ODataContainerCollection : ICollectionFixture<ODataContainerFixture>
{
    // This class is never instantiated.
    // It exists only to define the collection and associate it with the fixture.
}
