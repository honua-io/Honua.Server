using System.Text.Json;

namespace Honua.Server.Deployment.E2ETests.Infrastructure;

/// <summary>
/// Helper for building test metadata JSON.
/// </summary>
public class TestMetadataBuilder
{
    private string _catalogId = "test-catalog";
    private string _catalogTitle = "Test Catalog";
    private string _catalogDescription = "Test catalog for E2E tests";
    private readonly List<object> _dataSources = new();
    private readonly List<object> _services = new();
    private readonly List<object> _folders = new();

    public TestMetadataBuilder WithCatalog(string id, string title, string description)
    {
        _catalogId = id;
        _catalogTitle = title;
        _catalogDescription = description;
        return this;
    }

    public TestMetadataBuilder AddPostgresDataSource(string id, string connectionString)
    {
        _dataSources.Add(new
        {
            id,
            provider = "postgres",
            connectionString
        });
        return this;
    }

    public TestMetadataBuilder AddSqliteDataSource(string id, string connectionString)
    {
        _dataSources.Add(new
        {
            id,
            provider = "sqlite",
            connectionString
        });
        return this;
    }

    public TestMetadataBuilder AddFolder(string id, string title, int order = 10)
    {
        _folders.Add(new
        {
            id,
            title,
            order
        });
        return this;
    }

    public TestMetadataBuilder AddFeatureService(
        string id,
        string title,
        string dataSourceId,
        string folderId = "default",
        bool enabled = true)
    {
        _services.Add(new
        {
            id,
            title,
            folderId,
            serviceType = "feature",
            dataSourceId,
            enabled,
            description = $"{title} - OGC API Features service",
            keywords = new[] { "features", "ogc" },
            ogc = new
            {
                collectionsEnabled = true,
                itemLimit = 1000,
                defaultCrs = "EPSG:4326",
                additionalCrs = new[] { "EPSG:3857" }
            }
        });
        return this;
    }

    public TestMetadataBuilder AddRasterService(
        string id,
        string title,
        string dataSourceId,
        string folderId = "default",
        bool enabled = true)
    {
        _services.Add(new
        {
            id,
            title,
            folderId,
            serviceType = "raster",
            dataSourceId,
            enabled,
            description = $"{title} - Raster tile service",
            keywords = new[] { "raster", "tiles" }
        });
        return this;
    }

    public string Build()
    {
        var metadata = new
        {
            catalog = new
            {
                id = _catalogId,
                title = _catalogTitle,
                description = _catalogDescription,
                version = "2025.10",
                keywords = new[] { "gis", "ogc", "test" }
            },
            folders = _folders.Count > 0 ? _folders : new List<object>
            {
                new { id = "default", title = "Default Services", order = 10 }
            },
            dataSources = _dataSources,
            services = _services
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(metadata, options);
    }

    public static string CreateMinimalMetadata()
    {
        return new TestMetadataBuilder()
            .WithCatalog("minimal-catalog", "Minimal Test Catalog", "Minimal catalog for testing")
            .Build();
    }

    public static string CreateInvalidMetadata()
    {
        return "{ invalid json }";
    }

    public static string CreateMetadataWithMissingDataSource()
    {
        return new TestMetadataBuilder()
            .WithCatalog("test-catalog", "Test Catalog", "Test catalog")
            .AddFeatureService("test-service", "Test Service", "non-existent-datasource")
            .Build();
    }
}
