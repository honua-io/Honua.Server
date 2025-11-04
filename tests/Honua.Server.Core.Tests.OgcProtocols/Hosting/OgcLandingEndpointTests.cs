using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Stac;
using Honua.Server.Host;
using Honua.Server.Host.Middleware;
using Honua.Server.Core.Styling;
using Honua.Server.Host.OpenApi.Filters;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Threading.RateLimiting;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class OgcLandingEndpointTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public OgcLandingEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OgcLanding_ShouldReturnCatalogInformation()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/ogc");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        payload.TryGetProperty("catalog", out var catalog).Should().BeTrue();
        catalog.GetProperty("id").GetString().Should().Be("honua-sample");
        catalog.GetProperty("title").GetString().Should().Be("Honua Sample Catalog");

        payload.TryGetProperty("services", out var services).Should().BeTrue();
        services.EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetString()
            .Should().Be("roads");
    }

    [Fact]
    public async Task OgcLanding_ShouldRenderHtml_WhenRequested()
    {
        var client = _factory.CreateAuthenticatedClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/ogc");
        request.Headers.Accept.ParseAdd("text/html");

        var response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().ContainEquivalentOf("<html");
        content.Should().ContainEquivalentOf("Collections");
    }

    [Fact]
    public async Task MetadataRegistry_ShouldExposeSnapshotFromProvider()
    {
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        var snapshot = await registry.GetSnapshotAsync();

        snapshot.Catalog.Id.Should().Be("honua-sample");
        var roadLayers = snapshot.GetService("roads").Layers;
        roadLayers.Should().Contain(l => l.Id == "roads-primary");
        roadLayers.Should().Contain(l => l.Id == "roads-inspections");
    }

    [Fact]
    public async Task Collections_ShouldListLayersForService()
    {
        var client = _factory.CreateAuthenticatedClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/ogc/roads/collections");

        payload.TryGetProperty("collections", out var collections).Should().BeTrue();
        var collectionArray = collections.EnumerateArray().ToArray();
        collectionArray.Should().HaveCount(2);
        collectionArray.Should().Contain(item => item.GetProperty("id").GetString() == "roads-primary");
        collectionArray.Should().Contain(item => item.GetProperty("id").GetString() == "roads-inspections");
    }

    [Fact]
    public async Task Collections_ShouldRenderHtml_WhenRequested()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/ogc/collections?f=html");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().ContainEquivalentOf("<table");
        content.Should().ContainEquivalentOf("roads-primary");
    }

    [Fact]
    public async Task Collection_ShouldReturnLayerDetails()
    {
        var client = _factory.CreateAuthenticatedClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/ogc/roads/collections/roads-primary");

        payload.GetProperty("id").GetString().Should().Be("roads-primary");
        var links = payload.GetProperty("links")
            .EnumerateArray()
            .Select(link => link.GetProperty("href").GetString())
            .ToArray();
        links.Should().ContainSingle(href =>
            href != null && href.EndsWith("/ogc/roads/collections/roads-primary/items", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Items_ShouldReturnEmptyFeatureCollection()
    {
        var client = _factory.CreateAuthenticatedClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/ogc/roads/collections/roads-primary/items");

        payload.GetProperty("type").GetString().Should().Be("FeatureCollection");
        var features = payload.GetProperty("features").EnumerateArray().ToArray();
        features.Should().HaveCount(3);
        features.Select(f => f.GetProperty("id").GetInt32()).Should().Contain(new[] { 1001, 1002, 1003 });
    }

    [Fact]
    public async Task Items_ShouldRenderHtml_WhenRequested()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/ogc/collections/roads::roads-primary/items?f=html");

        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().ContainEquivalentOf("Number matched");
        content.Should().ContainEquivalentOf("<details");
    }

    [Fact]
    public async Task StartupHealth_ShouldReturnHealthy()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/healthz/startup");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("status").GetString().Should().Be("Healthy");
        payload.GetProperty("entries").GetProperty("metadata").GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task LivenessHealth_ShouldReturnHealthy()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/healthz/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task ReadinessHealth_ShouldReturnHealthy()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/healthz/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Status can be Degraded if optional services like Redis are unavailable
        payload.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded");
        payload.GetProperty("entries").GetProperty("dataSources").GetProperty("status").GetString().Should().Be("Healthy");
    }
    [Fact]
    public async Task Collections_ShouldReturnNotFoundForUnknownService()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/ogc/unknown/collections");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

public sealed class HonuaWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    internal const string DefaultAdminUsername = "admin";
    internal const string DefaultAdminPassword = "TestAdmin123!";

    internal static bool ForceStac { get; set; }

    private readonly string _metadataPath;
    private readonly string _authStorePath;
    private readonly string _databasePath;
    private readonly string _rootPath;
    private string? _stacCatalogPath;
    private bool _stacEnabled;
    private bool _bootstrapped;
    private Action<IWebHostBuilder>? _additionalConfigure;

    internal string MetadataPath => _metadataPath;

    public HonuaWebApplicationFactory()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"honua-ogc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_rootPath);
        _metadataPath = Path.Combine(_rootPath, "metadata.json");
        _databasePath = Path.Combine(_rootPath, "honua-test.db");
        _stacCatalogPath = Path.Combine(_rootPath, "stac-catalog.db");
        var updatedMetadata = UpdateDataSourceConnection(SampleMetadata, _databasePath);
        File.WriteAllText(_metadataPath, updatedMetadata);
        SeedSqliteDatabase(_databasePath);
        _authStorePath = Path.Combine(_rootPath, "auth.db");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            _stacEnabled = ShouldEnableStac();

            var configuration = new Dictionary<string, string?>
            {
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = _metadataPath,
                ["honua:authentication:mode"] = "QuickStart",
                ["honua:authentication:enforce"] = "false",
                ["honua:authentication:quickStart:enabled"] = "true",
                ["honua:authentication:allowQuickStart"] = "true",
                ["honua:rateLimiting:enabled"] = "false",
                ["honua:openApi:enabled"] = "false",
                ["honua:observability:metrics:enabled"] = "false",
                ["honua:security:enforcePolicies"] = "false",
                ["honua:authentication:local:storePath"] = _authStorePath,
                ["honua:authentication:bootstrap:adminUsername"] = DefaultAdminUsername,
                ["honua:authentication:bootstrap:adminPassword"] = DefaultAdminPassword,
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            };
            if (_stacEnabled && _stacCatalogPath is not null)
            {
                configuration["honua:services:stac:enabled"] = "true";
                configuration["honua:services:stac:filePath"] = _stacCatalogPath;
                configuration["honua:services:stac:connectionString"] = $"Data Source={_stacCatalogPath};Cache=Shared;Pooling=true";
            }
            else
            {
                configuration["honua:services:stac:enabled"] = "false";
            }
            config.AddInMemoryCollection(configuration);
        });

        _additionalConfigure?.Invoke(builder);

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHonuaConfigurationService>();
            services.RemoveAll<IMetadataProvider>();
            services.RemoveAll<IMetadataRegistry>();

            services.AddSingleton<IHonuaConfigurationService>(_ => new HonuaConfigurationService(new HonuaConfiguration
            {
                Metadata = new MetadataConfiguration
                {
                    Provider = "json",
                    Path = _metadataPath
                },
                Services = new ServicesConfiguration
                {
                    Stac = new StacCatalogConfiguration
                    {
                        Enabled = _stacEnabled,
                        FilePath = _stacEnabled ? _stacCatalogPath : null,
                        ConnectionString = _stacEnabled && _stacCatalogPath is not null
                            ? $"Data Source={_stacCatalogPath};Cache=Shared;Pooling=true"
                            : null
                    }
                }
            }));

            services.AddSingleton<IMetadataProvider>(_ => new JsonMetadataProvider(_metadataPath));
            services.AddSingleton<IMetadataRegistry>(sp => new MetadataRegistry(sp.GetRequiredService<IMetadataProvider>()));
            services.RemoveAll<IOutputCacheInvalidationService>();
            services.AddSingleton<IOutputCacheInvalidationService, NoOpOutputCacheInvalidationService>();
            services.RemoveAll<IStyleRepository>();
            services.AddSingleton<IStyleRepository, InMemoryStyleRepository>();
            services.RemoveAll<IOutputCacheStore>();
            services.AddSingleton<IOutputCacheStore, NoOpOutputCacheStore>();
            services.AddAuthorization(options =>
            {
                var allowAll = new AuthorizationPolicyBuilder()
                    .RequireAssertion(_ => true)
                    .Build();
                options.DefaultPolicy = allowAll;
                options.FallbackPolicy = allowAll;
            });
            services.PostConfigure<SwaggerGenOptions>(options =>
            {
                options.DocumentFilterDescriptors.RemoveAll(descriptor => descriptor.Type == typeof(VersionInfoDocumentFilter));
            });
            services.PostConfigure<RateLimiterOptions>(options =>
            {
                options.AddPolicy("OgcApiPolicy", context => RateLimitPartition.GetNoLimiter<string>("noop"));
            });
            services.RemoveAll<IAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, AllowAnonymousAuthorizationHandler>();
            services.AddSingleton<IAuthorizationPolicyProvider, AllowAnonymousPolicyProvider>();
            services.AddSingleton<IStartupFilter, AuthorizationBypassStartupFilter>();

            if (ForceStac || !_stacEnabled)
            {
                var hostedServices = services
                    .Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                                          descriptor.ImplementationType == typeof(StacCatalogSynchronizationHostedService))
                    .ToList();

                foreach (var descriptor in hostedServices)
                {
                    services.Remove(descriptor);
                }
            }
        });
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        client.BaseAddress = new Uri("https://localhost");
        EnsureCsrfTokenAsync(client).GetAwaiter().GetResult();
        return client;
    }

    public HonuaWebApplicationFactory WithHonuaWebHostBuilder(Action<IWebHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var factory = new HonuaWebApplicationFactory
        {
            _additionalConfigure = configure
        };

        return factory;
    }

    public new void Dispose()
    {
        base.Dispose();
        if (!string.IsNullOrWhiteSpace(_rootPath) && Directory.Exists(_rootPath))
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch
            {
            }
        }

        if (_stacEnabled && _stacCatalogPath is not null && File.Exists(_stacCatalogPath))
        {
            try
            {
                File.Delete(_stacCatalogPath);
            }
            catch
            {
            }
        }
    }

    private Task EnsureBootstrapAsync() => Task.CompletedTask;

    private static async Task AuthenticateClientAsync(HttpClient client)
    {
        await EnsureCsrfTokenAsync(client).ConfigureAwait(false);

        var response = await client.PostAsJsonAsync("/api/auth/local/login", new { username = DefaultAdminUsername, password = DefaultAdminPassword }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        var token = payload.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        ClearCsrfHeaders(client);
        await EnsureCsrfTokenAsync(client).ConfigureAwait(false);
    }

    private static async Task EnsureCsrfTokenAsync(HttpClient client)
    {
        var baseAddress = client.BaseAddress ?? new Uri("https://localhost");
        var origin = baseAddress.GetLeftPart(UriPartial.Authority);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/api/security/csrf-token");
        request.Headers.Referrer = baseAddress;
        request.Headers.TryAddWithoutValidation("Origin", origin);

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        if (document.RootElement.TryGetProperty("token", out var tokenElement))
        {
            var token = tokenElement.GetString();
            if (!string.IsNullOrEmpty(token))
            {
                var headerName = "X-CSRF-Token";
                if (document.RootElement.TryGetProperty("headerName", out var headerElement))
                {
                    headerName = headerElement.GetString() ?? headerName;
                }

                client.DefaultRequestHeaders.Remove(headerName);
                client.DefaultRequestHeaders.Add(headerName, token);
            }
        }
    }

    private static void ClearCsrfHeaders(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Remove("__Host-X-CSRF-Token");
        client.DefaultRequestHeaders.Remove("__RequestVerificationToken");
    }

    internal static string SampleMetadata
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Data", "metadata-ogc-sample.json");
            var json = File.ReadAllText(path);
            JsonMetadataProvider.Parse(json);
            return json;
    }
}

internal sealed class NoOpOutputCacheInvalidationService : IOutputCacheInvalidationService
{
    public Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class InMemoryStyleRepository : IStyleRepository
{
    private readonly ConcurrentDictionary<string, StyleDefinition> _styles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<StyleVersion>> _history = new(StringComparer.OrdinalIgnoreCase);

    public Task<StyleDefinition?> GetAsync(string styleId, CancellationToken cancellationToken = default)
    {
        _styles.TryGetValue(styleId, out var style);
        return Task.FromResult(style);
    }

    public Task<IReadOnlyList<StyleDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StyleDefinition> result = _styles.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<StyleDefinition> CreateAsync(StyleDefinition style, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(style);
        _styles[style.Id] = style;
        AddVersion(style.Id, style, createdBy);
        return Task.FromResult(style);
    }

    public Task<StyleDefinition> UpdateAsync(string styleId, StyleDefinition style, string? updatedBy = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(style);
        _styles[styleId] = style;
        AddVersion(styleId, style, updatedBy);
        return Task.FromResult(style);
    }

    public Task<bool> DeleteAsync(string styleId, string? deletedBy = null, CancellationToken cancellationToken = default)
    {
        var removed = _styles.TryRemove(styleId, out _);
        if (_history.TryGetValue(styleId, out var history))
        {
            history.Add(new StyleVersion
            {
                StyleId = styleId,
                Version = history.Count + 1,
                Definition = new StyleDefinition { Id = styleId, Title = "Deleted" },
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = deletedBy,
                ChangeDescription = "Deleted"
            });
        }
        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<StyleVersion>> GetVersionHistoryAsync(string styleId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StyleVersion> result = _history.TryGetValue(styleId, out var history)
            ? history.ToList()
            : Array.Empty<StyleVersion>();
        return Task.FromResult(result);
    }

    public Task<StyleDefinition?> GetVersionAsync(string styleId, int version, CancellationToken cancellationToken = default)
    {
        if (_history.TryGetValue(styleId, out var history))
        {
            var match = history.FirstOrDefault(v => v.Version == version);
            return Task.FromResult(match?.Definition);
        }

        return Task.FromResult<StyleDefinition?>(null);
    }

    public Task<bool> ExistsAsync(string styleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_styles.ContainsKey(styleId));
    }

    private void AddVersion(string styleId, StyleDefinition definition, string? user)
    {
        var history = _history.GetOrAdd(styleId, _ => new List<StyleVersion>());
        history.Add(new StyleVersion
        {
            StyleId = styleId,
            Version = history.Count + 1,
            Definition = definition,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = user,
            ChangeDescription = history.Count == 0 ? "Initial version" : "Updated"
        });
    }
}

internal sealed class NoOpOutputCacheStore : IOutputCacheStore
{
    public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    public ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken = default) => new ValueTask<byte[]>(Array.Empty<byte>());

    public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan duration, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

internal sealed class AllowAnonymousAuthorizationHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements.ToList())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

internal sealed class AllowAnonymousPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly AuthorizationOptions _options = new();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = _options.GetPolicy(policyName);
        if (policy is null)
        {
            policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
            _options.AddPolicy(policyName, policy);
        }

        return Task.FromResult<AuthorizationPolicy?>(_options.GetPolicy(policyName));
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return Task.FromResult(new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build());
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return Task.FromResult<AuthorizationPolicy?>(null);
    }
}

internal sealed class AuthorizationBypassStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseAuthentication();
            app.UseAuthorization();

            app.Use(async (context, nextMiddleware) =>
            {
                var endpoint = context.GetEndpoint();
                if (endpoint != null)
                {
                    var authorizeMetadata = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
                    if (authorizeMetadata.Count > 0)
                    {
                        var metadata = endpoint.Metadata.Where(m => m is not IAuthorizeData).ToList();
                        metadata.Add(new AllowAnonymousAttribute());
                        context.SetEndpoint(new Endpoint(endpoint.RequestDelegate, new EndpointMetadataCollection(metadata), endpoint.DisplayName));
                    }
                }

                await nextMiddleware().ConfigureAwait(false);
            });

            next(app);
        };
}

    private static string UpdateDataSourceConnection(string metadataJson, string databasePath)
    {
        var node = JsonNode.Parse(metadataJson) as JsonObject;
        if (node is null)
        {
            return metadataJson;
        }

        if (node.TryGetPropertyValue("dataSources", out var dataSourcesNode) && dataSourcesNode is JsonArray dataSources)
        {
            foreach (var source in dataSources.OfType<JsonObject>())
            {
                var id = source.TryGetPropertyValue("id", out var idNode) ? idNode?.GetValue<string>() : null;
                if (string.Equals(id, "sqlite-primary", StringComparison.OrdinalIgnoreCase))
                {
                    source["connectionString"] = $"Data Source={databasePath}";
                }
            }
        }

        var updated = node.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
        JsonMetadataProvider.Parse(updated);
        return updated;
    }

    private static void SeedSqliteDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
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

            void AddRoad(int id, string name, string status, string wkt)
            {
                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("@id", id);
                insert.Parameters.AddWithValue("@name", name);
                insert.Parameters.AddWithValue("@status", status);
                insert.Parameters.AddWithValue("@observed", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                insert.Parameters.AddWithValue("@geom", wkt);
                insert.ExecuteNonQuery();
            }

            AddRoad(1001, "Meridian Trail", "open", "LINESTRING(-122.5 45.5, -122.4 45.6)");
            AddRoad(1002, "Equatorial Drive", "planned", "LINESTRING(-122.6 45.4, -122.5 45.5)");
            AddRoad(1003, "Dateline Connector", "open", "LINESTRING(-122.7 45.3, -122.6 45.4)");
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
CREATE TABLE roads_inspections (
  inspection_id INTEGER PRIMARY KEY,
  road_id INTEGER,
  inspector TEXT,
  geom TEXT
);
""";
            command.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
INSERT INTO roads_inspections (inspection_id, road_id, inspector, geom)
VALUES (@id, @roadId, @inspector, @geom);
""";

            void AddInspection(int id, int roadId, string inspector, string wkt)
            {
                insert.Parameters.Clear();
                insert.Parameters.AddWithValue("@id", id);
                insert.Parameters.AddWithValue("@roadId", roadId);
                insert.Parameters.AddWithValue("@inspector", inspector);
                insert.Parameters.AddWithValue("@geom", wkt);
                insert.ExecuteNonQuery();
            }

            AddInspection(5001, 1001, "Rivera", "POINT(-122.48 45.55)");
            AddInspection(5002, 1002, "Hawkins", "POINT(-122.58 45.45)");
        }
    }

    private static bool ShouldEnableStac()
    {
        if (ForceStac)
        {
            return true;
        }

        var value = Environment.GetEnvironmentVariable("HONUA_ENABLE_STAC_FIXTURE");
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

}
