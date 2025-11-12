using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Host;
using Honua.Server.Host.Hosting;
using Testcontainers.PostgreSql;
using Honua.Server.Host.OData;
using Honua.Server.Host.Middleware;
using Honua.Server.Host.OpenApi.Filters;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class ODataEndpointSqliteTests : IClassFixture<ODataSqliteFixture>
{
    private readonly ODataSqliteFixture _fixture;

    public ODataEndpointSqliteTests(ODataSqliteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ServiceDocument_ShouldExposeEntitySet()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var (setName, typeName) = await ODataTestHelpers.DiscoverEntitySetAsync(client);

        setName.Should().Be("roads_roads_primary");
        typeName.Should().Contain("roads_roads_primary");
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportCrud()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var (entitySet, _) = await ODataTestHelpers.DiscoverEntitySetAsync(client);

        var initial = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}");
        var initialValues = initial.GetProperty("value").EnumerateArray().ToList();
        initialValues.Should().HaveCountGreaterThan(2);
        initialValues.Select(v => v.GetProperty("road_id").GetInt32()).Should().Contain(new[] { 1001, 1002, 1003 });
        var originalFirstId = initialValues.First().GetProperty("road_id").GetInt32();
        var existingIds = initialValues.Select(v => v.GetProperty("road_id").GetInt32()).ToHashSet();
        var newId = existingIds.Max() + 1;
        var createPayload = new
        {
            road_id = newId,
            name = "Client Insert",
            status = "open",
            observed_at = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            geom = "{\"type\":\"LineString\",\"coordinates\":[[-122.30,45.70],[-122.20,45.80]]}"
        };

        var create = await client.PostAsJsonAsync($"/odata/{entitySet}", createPayload);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await ODataTestHelpers.GetSingleAsync(client, entitySet, newId);
        created.GetProperty("name").GetString().Should().Be("Client Insert");
        created.TryGetProperty("geom_wkt", out var geomWkt).Should().BeTrue();
        geomWkt.GetString().Should().Contain("LINESTRING");

        var patchPayload = new { status = "planned" };
        var patch = await ODataTestHelpers.SendPatchAsync(client, $"/odata/{entitySet}({newId})", patchPayload);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await ODataTestHelpers.GetSingleAsync(client, entitySet, newId);
        updated.GetProperty("status").GetString().Should().Be("planned");
        updated.TryGetProperty("geom_wkt", out var updatedWkt).Should().BeTrue();
        updatedWkt.GetString().Should().Contain("LINESTRING");

        var delete = await client.DeleteAsync($"/odata/{entitySet}({newId})");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var missing = await client.GetAsync($"/odata/{entitySet}({newId})");
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ODataEndpoints_ShouldHonorQueryOptions()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var (entitySet, _) = await ODataTestHelpers.DiscoverEntitySetAsync(client);

        var topResponse = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$top=1");
        var firstTopId = topResponse.GetProperty("value").EnumerateArray().Select(v => v.GetProperty("road_id").GetInt32()).Single();

        var orderResponse = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$orderby=status desc&$top=1");
        orderResponse.GetProperty("value").EnumerateArray().First().GetProperty("status").GetString().Should().Be("planned");

        var selectResponse = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$select=road_id,name");
        var selected = selectResponse.GetProperty("value").EnumerateArray().First();
        selected.TryGetProperty("status", out _).Should().BeFalse();

        var countResponse = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$count=true&$top=0");
        countResponse.GetProperty("@odata.count").GetInt32().Should().BeGreaterThanOrEqualTo(3);

        var skipResponse = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$skip=1");
        var skipFirst = skipResponse.GetProperty("value").EnumerateArray().First().GetProperty("road_id").GetInt32();
        skipFirst.Should().NotBe(firstTopId);

        var filtered = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$filter=status eq 'open'");
        filtered.GetProperty("value").EnumerateArray().Should().OnlyContain(item => item.GetProperty("status").GetString() == "open");
    }
    [Fact]
    public async Task ODataEndpoints_ShouldRejectGeometryFilters_OnSqlite()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var (entitySet, _) = await ODataTestHelpers.DiscoverEntitySetAsync(client);
        var filter = Uri.EscapeDataString("geo.intersects(geom, geometry'SRID=4326;LINESTRING(-122.5 45.5,-122.4 45.6)')");
        var response = await client.GetAsync($"/odata/{entitySet}?$filter={filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

}

public sealed class ODataEndpointPostgresTests : IClassFixture<ODataPostgresFixture>
{
    private readonly ODataPostgresFixture _fixture;

    public ODataEndpointPostgresTests(ODataPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ODataEndpoints_ShouldSupportCrud()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var (entitySet, _) = await ODataTestHelpers.DiscoverEntitySetAsync(client);

        var list = await ODataTestHelpers.GetValueAsync(client, $"/v1/odata/{entitySet}");
        list.GetProperty("value").EnumerateArray().Count().Should().BeGreaterThanOrEqualTo(2);

        var existingIds = list.GetProperty("value").EnumerateArray().Select(v => v.GetProperty("road_id").GetInt32()).ToHashSet();
        var newId = existingIds.Max() + 1;
        var createPayload = new
        {
            road_id = newId,
            name = "River Road",
            status = "open",
            observed_at = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            geom = "{\"type\":\"LineString\",\"coordinates\":[[-70.05,5.05],[-69.95,5.15]]}"
        };

        var create = await client.PostAsJsonAsync($"/odata/{entitySet}", createPayload);
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var updatedPayload = new { status = "planned" };
        var patch = await ODataTestHelpers.SendPatchAsync(client, $"/v1/odata/{entitySet}({newId})", updatedPayload);
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var updated = await ODataTestHelpers.GetSingleAsync(client, entitySet, newId);
        updated.GetProperty("status").GetString().Should().Be("planned");
        updated.TryGetProperty("geom_wkt", out var updatedWkt).Should().BeTrue();
        updatedWkt.GetString().Should().Contain("LINESTRING");

        var delete = await client.DeleteAsync($"/odata/{entitySet}({newId})");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
    [Fact]
    public async Task ODataEndpoints_ShouldFilterByGeometry()
    {
        var client = _fixture.CreateAuthenticatedClient();
        var (entitySet, _) = await ODataTestHelpers.DiscoverEntitySetAsync(client);
        var filter = Uri.EscapeDataString("geo.intersects(geom, geometry'SRID=4326;LINESTRING(-122.51 45.49,-122.39 45.61)')");
        var response = await ODataTestHelpers.GetValueAsync(client, $"/odata/{entitySet}?$filter={filter}");

        var results = response.GetProperty("value").EnumerateArray().ToList();
        results.Should().ContainSingle();
        results[0].GetProperty("road_id").GetInt32().Should().Be(1001);
    }

}

public static class ODataTestHelpers
{
    public static async Task<(string EntitySetName, string EntityTypeName)> DiscoverEntitySetAsync(HttpClient client)
    {
        // Note: OData endpoints are at /odata (unversioned) due to ASP.NET Core OData v8 routing limitations
        // with route prefixes containing forward slashes (e.g., "v1/odata")
        var response = await client.GetAsync("/odata/$metadata");
        var metadataXml = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Request to /odata/$metadata failed with {(int)response.StatusCode}: {response.ReasonPhrase}. Body: {metadataXml}");
        }
        var doc = XDocument.Parse(metadataXml);
        XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";

        var entitySet = doc.Descendants(edm + "EntitySet").First();
        var entitySetName = entitySet.Attribute("Name")?.Value ?? throw new InvalidOperationException("Entity set name missing.");
        var entityTypeName = entitySet.Attribute("EntityType")?.Value ?? throw new InvalidOperationException("Entity type name missing.");
        return (entitySetName, entityTypeName);
    }

    public static Task<HttpResponseMessage> SendPatchAsync(HttpClient client, string uri, object payload)
    {
        var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Prefer", "return=representation");
        return client.SendAsync(request);
    }

    public static async Task<JsonElement> GetValueAsync(HttpClient client, string uri)
    {
        var response = await client.GetAsync(uri);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Request to {uri} failed with {(int)response.StatusCode}: {response.ReasonPhrase}. Body: {body}");
        }

        var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return payload.RootElement;
    }

    public static async Task<JsonElement> GetSingleAsync(HttpClient client, string entitySet, int id)
    {
        var payload = await GetValueAsync(client, $"/odata/{entitySet}({id})");
        return payload;
    }
}

public sealed class ODataSqliteFixture : WebApplicationFactory<Program>, IDisposable
{
    private const string AdminUsername = "admin";
    private const string AdminPassword = "TestAdmin123!";

    private readonly string _databasePath;
    private readonly string _metadataPath;
    private readonly string _authStorePath;
    private readonly bool _skip;
    private readonly string? _skipReason;
    private bool _bootstrapped;

    public string DatabasePath => _databasePath;

    public ODataSqliteFixture()
    {
        try
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"honua-odata-sqlite-{Guid.NewGuid():N}.db");
            SeedDatabase(_databasePath);
            _metadataPath = WriteMetadata(_databasePath);
            _authStorePath = Path.Combine(Path.GetTempPath(), $"honua-odata-sqlite-auth-{Guid.NewGuid():N}.db");
        }
        catch (Exception ex)
        {
            _skip = true;
            _skipReason = ex.Message;
            _databasePath = string.Empty;
            _metadataPath = string.Empty;
            _authStorePath = string.Empty;
        }
    }

    public bool ShouldSkip => _skip;

    public string? SkipReason => _skipReason;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        if (_skip)
        {
            base.ConfigureWebHost(builder);
            return;
        }

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = _metadataPath,
                ["honua:services:odata:enabled"] = "true",
                ["honua:services:odata:allowWrites"] = "true",
                ["honua:authentication:mode"] = "Local",
                ["honua:authentication:enforce"] = "true",
                ["honua:authentication:quickStart:enabled"] = "false",
                ["honua:authentication:local:storePath"] = _authStorePath,
                ["honua:authentication:bootstrap:adminUsername"] = AdminUsername,
                ["honua:authentication:bootstrap:adminPassword"] = AdminPassword,
                ["RateLimiting:Enabled"] = "false",
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Don't replace OData-specific services (IMetadataProvider, IMetadataRegistry, ODataModelCache)
            // Let them use the real implementations with test configuration from ConfigureAppConfiguration

            services.RemoveAll<IOutputCacheInvalidationService>();
            services.RemoveAll<IOutputCacheStore>();

            services.AddSingleton<IOutputCacheStore, NoOpOutputCacheStore>();
            services.AddSingleton<IOutputCacheInvalidationService, NoOpOutputCacheInvalidationService>();

            services.PostConfigure<SwaggerGenOptions>(options =>
            {
                options.DocumentFilterDescriptors.RemoveAll(descriptor => descriptor.Type == typeof(VersionInfoDocumentFilter));
            });
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = base.CreateClient();
        if (!_skip)
        {
            EnsureBootstrapAsync().GetAwaiter().GetResult();
            EnsureCsrfTokenAsync(client).GetAwaiter().GetResult();
            AuthenticateClientAsync(client).GetAwaiter().GetResult();
        }

        return client;
    }

    public new void Dispose()
    {
        base.Dispose();
        if (_skip)
        {
            return;
        }

        TryDeleteFile(_databasePath);
        TryDeleteFile(_metadataPath);
        TryDeleteFile(_authStorePath);
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private MetadataSnapshot CreateMetadataSnapshot(string connectionString)
    {
        var catalog = new CatalogDefinition { Id = "honua-sample", Title = "Honua Sample Catalog" };
        var folders = new[] { new FolderDefinition { Id = "transportation" } };
        var dataSources = new[]
        {
            new DataSourceDefinition { Id = "sqlite-primary", Provider = "sqlite", ConnectionString = connectionString }
        };
        var services = new[]
        {
            new ServiceDefinition
            {
                Id = "roads",
                Title = "Road Centerlines",
                FolderId = "transportation",
                ServiceType = "feature",
                DataSourceId = "sqlite-primary",
                Enabled = true,
                Ogc = new OgcServiceDefinition { CollectionsEnabled = true, ItemLimit = 1000 }
            }
        };
        var layers = new[]
        {
            new LayerDefinition
            {
                Id = "roads-primary",
                ServiceId = "roads",
                Title = "Primary Roads",
                GeometryType = "LineString",
                IdField = "road_id",
                DisplayField = "name",
                GeometryField = "geom",
                Fields = new[]
                {
                    new FieldDefinition { Name = "road_id", DataType = "int", Nullable = false },
                    new FieldDefinition { Name = "name", DataType = "string" },
                    new FieldDefinition { Name = "status", DataType = "string" },
                    new FieldDefinition { Name = "observed_at", DataType = "datetime" },
                    new FieldDefinition { Name = "geom", DataType = "geometry" }
                },
                Storage = new LayerStorageDefinition
                {
                    Table = "roads_primary",
                    GeometryColumn = "geom",
                    PrimaryKey = "road_id",
                    Srid = 4326
                }
            }
        };

        var server = new ServerDefinition
        {
            AllowedHosts = new[] { "*" }
        };

        return new MetadataSnapshot(catalog, folders, dataSources, services, layers, server: server);
    }

    private sealed class InMemoryMetadataProvider : IMetadataProvider
    {
        private readonly MetadataSnapshot _snapshot;

        public InMemoryMetadataProvider(MetadataSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);

        public bool SupportsChangeNotifications => false;
#pragma warning disable CS0067
        public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
#pragma warning restore CS0067
    }

    private static void SeedDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
CREATE TABLE roads_primary (
  road_id INTEGER PRIMARY KEY,
  name TEXT,
  status TEXT,
  observed_at TEXT,
  geom TEXT
);
""";
            command.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
INSERT INTO roads_primary (road_id, name, status, observed_at, geom)
VALUES (@id, @name, @status, @observed, @geom);
""";

            void AddRow(int id, string name, string status, string wkt)
            {
                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("@id", id);
                insert.Parameters.AddWithValue("@name", name);
                insert.Parameters.AddWithValue("@status", status);
                insert.Parameters.AddWithValue("@observed", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                insert.Parameters.AddWithValue("@geom", wkt);
                insert.ExecuteNonQuery();
            }

            AddRow(1001, "Meridian Trail", "open", "LINESTRING(-122.5 45.5, -122.4 45.6)");
            AddRow(1002, "Equatorial Drive", "planned", "LINESTRING(-122.6 45.4, -122.5 45.5)");
            AddRow(1003, "Dateline Connector", "open", "LINESTRING(-122.7 45.3, -122.6 45.4)");
        }
    }

    private static string WriteMetadata(string databasePath)
    {
        var connectionString = $"Data Source={databasePath}";
        var metadata = new
        {
            catalog = new { id = "honua-sample", title = "Honua Sample Catalog" },
            folders = new[] { new { id = "transportation" } },
            dataSources = new[]
            {
                new { id = "sqlite-primary", provider = "sqlite", connectionString }
            },
            services = new[]
            {
                new
                {
                    id = "roads",
                    title = "Road Centerlines",
                    folderId = "transportation",
                    serviceType = "feature",
                    dataSourceId = "sqlite-primary",
                    enabled = true,
                    ogc = new { collectionsEnabled = true, itemLimit = 1000 }
                }
            },
            layers = new[]
            {
                new
                {
                    id = "roads-primary",
                    serviceId = "roads",
                    title = "Primary Roads",
                    geometryType = "LineString",
                    idField = "road_id",
                    displayField = "name",
                    geometryField = "geom",
                    itemType = "feature",
                    fields = new[]
                    {
                        new { name = "road_id", dataType = "int", nullable = false },
                        new { name = "name", dataType = "string", nullable = true },
                        new { name = "status", dataType = "string", nullable = true },
                        new { name = "observed_at", dataType = "datetime", nullable = true },
                        new { name = "geom", dataType = "geometry", nullable = true }
                    },
                    storage = new
                    {
                        table = "roads_primary",
                        geometryColumn = "geom",
                        primaryKey = "road_id",
                        srid = 4326
                    }
                }
            },
            server = new
            {
                allowedHosts = new[] { "*" }
            }
        };

        // Use standard JSON options instead of source-generated resolver for anonymous objects
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(metadata, options);
        var path = Path.Combine(Path.GetTempPath(), $"honua-odata-metadata-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private async Task EnsureBootstrapAsync()
    {
        if (_bootstrapped)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IAuthBootstrapService>();
        var result = await bootstrap.BootstrapAsync().ConfigureAwait(false);
        if (result.Status == AuthBootstrapStatus.Failed)
        {
            throw new InvalidOperationException($"Bootstrap failed: {result.Message}");
        }

        _bootstrapped = true;
    }

    private static async Task EnsureCsrfTokenAsync(HttpClient client)
    {
        var baseAddress = client.BaseAddress ?? new Uri("https://localhost");
        var origin = baseAddress.GetLeftPart(UriPartial.Authority);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/security/csrf-token");
        request.Headers.Referrer = baseAddress;
        request.Headers.TryAddWithoutValidation("Origin", origin);

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Failed to obtain CSRF token: {(int)response.StatusCode} {response.StatusCode}; Body: {errorBody}");
        }

        using var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var csrfResponse = await JsonSerializer.DeserializeAsync<JsonElement>(content).ConfigureAwait(false);
        if (csrfResponse.TryGetProperty("headerName", out var headerName) &&
            csrfResponse.TryGetProperty("token", out var tokenValue))
        {
            var header = headerName.GetString();
            var token = tokenValue.GetString();
            if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Remove(header);
                client.DefaultRequestHeaders.Add(header, token);
            }
        }
    }

    private static async Task AuthenticateClientAsync(HttpClient client)
    {
        if (client.DefaultRequestHeaders.Authorization is not null)
        {
            return;
        }

        var response = await client.PostAsJsonAsync("/v1/api/auth/local/login", new { username = AdminUsername, password = AdminPassword }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        var token = payload.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed class NoOpOutputCacheInvalidationService : IOutputCacheInvalidationService
    {
        public Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpOutputCacheStore : IOutputCacheStore
    {
        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask EvictByTagAsync(string[] tags, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan duration, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Array.Empty<byte>());

        public ValueTask<IReadOnlyList<string>> GetTagsAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public ValueTask EvictAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

public sealed class ODataPostgresFixture : WebApplicationFactory<Program>
{
    private const string AdminUsername = "admin";
    private const string AdminPassword = "TestAdmin123!";

    private PostgreSqlContainer? _container;
    private string? _metadataPath;
    private string? _metadataJson;
    private string? _authStorePath;
    private bool _skip;
    private string? _skipReason;
    private bool _bootstrapped;

    public ODataPostgresFixture()
    {
        try
        {
            _container = new PostgreSqlBuilder()
                .WithImage("postgis/postgis:16-3.4")
                .WithDatabase("honua_odata")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithCleanUp(true)
                .Build();

            _container.StartAsync().GetAwaiter().GetResult();
            PrepareDatabaseAsync().GetAwaiter().GetResult();
            _metadataPath = WriteMetadata(_container.GetConnectionString());
            _authStorePath = Path.Combine(Path.GetTempPath(), $"honua-odata-postgres-auth-{Guid.NewGuid():N}.db");
        }
        catch (Exception ex)
        {
            _skip = true;
            _skipReason = $"Postgres container unavailable: {ex.Message}";
        }
    }

    public bool ShouldSkip => _skip;

    public string? SkipReason => _skipReason;

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        if (_skip)
        {
            base.ConfigureWebHost(builder);
            return;
        }

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = _metadataPath,
                ["honua:services:odata:enabled"] = "true",
                ["honua:services:odata:allowWrites"] = "true",
                ["honua:authentication:mode"] = "Local",
                ["honua:authentication:enforce"] = "true",
                ["honua:authentication:quickStart:enabled"] = "false",
                ["honua:authentication:local:storePath"] = _authStorePath,
                ["honua:authentication:bootstrap:adminUsername"] = AdminUsername,
                ["honua:authentication:bootstrap:adminPassword"] = AdminPassword,
                ["RateLimiting:Enabled"] = "false",
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Don't replace OData-specific services (IMetadataProvider, IMetadataRegistry, ODataModelCache)
            // Let them use the real implementations with test configuration from ConfigureAppConfiguration

            services.RemoveAll<IOutputCacheInvalidationService>();
            services.RemoveAll<IOutputCacheStore>();

            services.AddSingleton<IOutputCacheStore, PostgresNoOpOutputCacheStore>();
            services.AddSingleton<IOutputCacheInvalidationService, PostgresNoOpOutputCacheInvalidationService>();

            services.PostConfigure<SwaggerGenOptions>(options =>
            {
                options.DocumentFilterDescriptors.RemoveAll(descriptor => descriptor.Type == typeof(VersionInfoDocumentFilter));
            });
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = base.CreateClient();
        if (!_skip)
        {
            EnsureBootstrapAsync().GetAwaiter().GetResult();
            EnsureCsrfTokenAsync(client).GetAwaiter().GetResult();
            AuthenticateClientAsync(client).GetAwaiter().GetResult();
        }

        return client;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_metadataPath is not null && File.Exists(_metadataPath))
        {
            File.Delete(_metadataPath);
        }

        if (_authStorePath is not null && File.Exists(_authStorePath))
        {
            File.Delete(_authStorePath);
        }

        if (!_skip && _container is not null)
        {
            await _container.DisposeAsync();
        }

        await base.DisposeAsync();
    }

    private async Task PrepareDatabaseAsync()
    {
        if (_container is null)
        {
            throw new InvalidOperationException("Postgres container is not available.");
        }

        await using var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE EXTENSION IF NOT EXISTS postgis;";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DROP TABLE IF EXISTS public.roads_primary;";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
CREATE TABLE public.roads_primary (
    road_id integer PRIMARY KEY,
    name text,
    status text,
    observed_at timestamptz,
    geom geometry(LineString, 3857)
);
""";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
INSERT INTO public.roads_primary (road_id, name, status, observed_at, geom)
VALUES
    (1001, 'Meridian Trail', 'open', '2022-01-01T00:00:00Z', ST_Transform(ST_GeomFromText('LINESTRING(-122.51 45.49, -122.39 45.61)', 4326), 3857)),
    (1002, 'Equatorial Drive', 'planned', '2022-01-02T00:00:00Z', ST_Transform(ST_GeomFromText('LINESTRING(-70.0 5.0, -69.5 5.5)', 4326), 3857));
""";
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
    }

    private string WriteMetadata(string connectionString)
    {
        var metadata = new
        {
            catalog = new { id = "honua-pg", title = "Honua Postgres Catalog" },
            folders = new[] { new { id = "transportation" } },
            dataSources = new[]
            {
                new { id = "postgis-primary", provider = "postgis", connectionString }
            },
            services = new[]
            {
                new
                {
                    id = "roads",
                    title = "Road Centerlines",
                    folderId = "transportation",
                    serviceType = "feature",
                    dataSourceId = "postgis-primary",
                    enabled = true,
                    ogc = new { collectionsEnabled = true, itemLimit = 1000 }
                }
            },
            layers = new[]
            {
                new
                {
                    id = "roads-primary",
                    serviceId = "roads",
                    title = "Primary Roads",
                    geometryType = "LineString",
                    idField = "road_id",
                    displayField = "name",
                    geometryField = "geom",
                    itemType = "feature",
                    storage = new
                    {
                        table = "roads_primary",
                        geometryColumn = "geom",
                        primaryKey = "road_id",
                        srid = 3857
                    }
                }
            },
            server = new
            {
                allowedHosts = new[] { "*" }
            }
        };

        // Use standard JSON options instead of source-generated resolver for anonymous objects
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(metadata, options);
        var path = Path.Combine(Path.GetTempPath(), $"honua-odata-postgres-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        _metadataJson = json;
        return path;
    }

    private sealed class StaticMetadataProvider : IMetadataProvider
    {
        private readonly MetadataSnapshot _snapshot;

        public StaticMetadataProvider(string json)
        {
            // Note: Cannot use JsonMetadataProvider.Parse here because it uses source-generated JSON serializers
            // which don't support deserializing JSON created from anonymous objects.
            // Since this class appears to be unused test infrastructure, commenting out for now.
            _snapshot = null!; // JsonMetadataProvider.Parse(json);
            // var layer = _snapshot.Services
            //     .SelectMany(s => s.Layers)
            //     .FirstOrDefault(l => string.Equals(l.Id, "roads-primary", StringComparison.OrdinalIgnoreCase));
            // var field = layer?.Fields.FirstOrDefault(f => string.Equals(f.Name, "road_id", StringComparison.OrdinalIgnoreCase));
        }

        public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot);
        }

        public bool SupportsChangeNotifications => false;
#pragma warning disable CS0067
        public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
#pragma warning restore CS0067
    }

    private async Task EnsureBootstrapAsync()
    {
        if (_bootstrapped)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IAuthBootstrapService>();
        var result = await bootstrap.BootstrapAsync().ConfigureAwait(false);
        if (result.Status == AuthBootstrapStatus.Failed)
        {
            throw new InvalidOperationException($"Bootstrap failed: {result.Message}");
        }

        _bootstrapped = true;
    }

    private static async Task AuthenticateClientAsync(HttpClient client)
    {
        if (client.DefaultRequestHeaders.Authorization is not null)
        {
            return;
        }

        var response = await client.PostAsJsonAsync("/v1/api/auth/local/login", new { username = AdminUsername, password = AdminPassword }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        var token = payload.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static async Task EnsureCsrfTokenAsync(HttpClient client)
    {
        var baseAddress = client.BaseAddress ?? new Uri("https://localhost");
        var origin = baseAddress.GetLeftPart(UriPartial.Authority);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/security/csrf-token");
        request.Headers.Referrer = baseAddress;
        request.Headers.TryAddWithoutValidation("Origin", origin);

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Failed to obtain CSRF token: {(int)response.StatusCode} {response.StatusCode}; Body: {errorBody}");
        }

        using var content = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var csrfResponse = await JsonSerializer.DeserializeAsync<JsonElement>(content).ConfigureAwait(false);
        if (csrfResponse.TryGetProperty("headerName", out var headerName) &&
            csrfResponse.TryGetProperty("token", out var tokenValue))
        {
            var header = headerName.GetString();
            var token = tokenValue.GetString();
            if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Remove(header);
                client.DefaultRequestHeaders.Add(header, token);
            }
        }
    }

    private sealed class PostgresNoOpOutputCacheInvalidationService : IOutputCacheInvalidationService
    {
        public Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PostgresNoOpOutputCacheStore : IOutputCacheStore
    {
        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask EvictByTagAsync(string[] tags, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan duration, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Array.Empty<byte>());

        public ValueTask<IReadOnlyList<string>> GetTagsAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public ValueTask EvictAsync(string key, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
