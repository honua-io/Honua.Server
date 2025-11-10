using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Host;
using Honua.Server.Host.Security;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class AdminMetadataEndpointTests : IClassFixture<ReloadableMetadataFactory>
{
    private readonly ReloadableMetadataFactory _factory;

    public AdminMetadataEndpointTests(ReloadableMetadataFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Diff_ShouldReportAddedService()
    {
        var client = await CreateAuthenticatedClientAsync();
        var baselineResponse = await client.PostAsync("/v1/admin/metadata/apply", JsonContent(MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"))));
        baselineResponse.EnsureSuccessStatusCode();

        var diffJson = MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"), ("new-roads", "new-layer"));
        var response = await client.PostAsync("/v1/admin/metadata/diff", JsonContent(diffJson));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("status").GetString().Should().Be("ok");
        payload.TryGetProperty("warnings", out var warningsNode).Should().BeTrue();
        warningsNode.ValueKind.Should().Be(JsonValueKind.Array);
        payload.GetProperty("diff").GetProperty("services").GetProperty("added").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("new-roads");
    }

    [Fact]
    public async Task Diff_ShouldReturnUnprocessableEntityWhenSchemaFails()
    {
        var client = await CreateAuthenticatedClientAsync();
        var invalidJson = MetadataTestFile.CreateWithoutRequiredLayerField();

        var response = await client.PostAsync("/v1/admin/metadata/diff", JsonContent(invalidJson));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Apply_ShouldPersistMetadataAndReload()
    {
        var client = await CreateAuthenticatedClientAsync();

        var baselineResponse = await client.PostAsync("/v1/admin/metadata/apply", JsonContent(MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"))));
        baselineResponse.EnsureSuccessStatusCode();

        var applyJson = MetadataTestFile.Create("catalog-v2", ("roads", "roads-primary"));
        var response = await client.PostAsync("/v1/admin/metadata/apply", JsonContent(applyJson));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("status").GetString().Should().Be("applied");
        payload.TryGetProperty("warnings", out var warningsNode).Should().BeTrue();
        warningsNode.ValueKind.Should().Be(JsonValueKind.Array);

        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        await registry.EnsureInitializedAsync();
        var snapshot = await registry.GetSnapshotAsync();
        snapshot.Catalog.Id.Should().Be("catalog-v2");

        File.ReadAllText(_factory.MetadataPath).Should().Contain("catalog-v2");
    }

    [Fact]
    public async Task Apply_ShouldReturnUnprocessableEntityWhenMetadataInvalid()
    {
        var client = await CreateAuthenticatedClientAsync();
        var original = File.ReadAllText(_factory.MetadataPath);

        var response = await client.PostAsync("/v1/admin/metadata/apply", JsonContent("{ invalid json }"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();

        File.ReadAllText(_factory.MetadataPath).Should().Be(original);
    }

    [Fact]
    public async Task Reload_ShouldRefreshSnapshotWhenMetadataValid()
    {
        var client = await CreateAuthenticatedClientAsync();

        MetadataTestFile.Write(_factory.MetadataPath, "catalog-v1", ("roads", "roads-primary"));
        await client.PostAsync("/v1/admin/metadata/reload", content: null);

        using (var scope = _factory.Services.CreateScope())
        {
            var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
            await registry.EnsureInitializedAsync();
            var snapshot = await registry.GetSnapshotAsync();
            snapshot.Catalog.Id.Should().Be("catalog-v1");
        }

        MetadataTestFile.Write(_factory.MetadataPath, "catalog-v2", ("roads", "roads-primary"));
        var response = await client.PostAsync("/v1/admin/metadata/reload", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("status").GetString().Should().Be("reloaded");

        using var verificationScope = _factory.Services.CreateScope();
        var updatedRegistry = verificationScope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        await updatedRegistry.EnsureInitializedAsync();
        var updatedSnapshot = await updatedRegistry.GetSnapshotAsync();
        updatedSnapshot.Catalog.Id.Should().Be("catalog-v2");
    }

    [Fact]
    public async Task Reload_ShouldReturnUnprocessableEntityWhenMetadataInvalid()
    {
        var client = await CreateAuthenticatedClientAsync();

        MetadataTestFile.Write(_factory.MetadataPath, "catalog-v1", ("roads", "roads-primary"));
        await client.PostAsync("/v1/admin/metadata/reload", content: null);

        await File.WriteAllTextAsync(_factory.MetadataPath, "{ invalid json }");

        var failure = await client.PostAsync("/v1/admin/metadata/reload", content: null);
        if (failure.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            var content = await failure.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"Reload failure response: {content}");
        }

        failure.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var errorPayload = await failure.Content.ReadFromJsonAsync<JsonElement>();
        errorPayload.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();

        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        await registry.EnsureInitializedAsync();
        var registrySnapshot = await registry.GetSnapshotAsync();
        registrySnapshot.Catalog.Id.Should().Be("catalog-v1");
    }

    [Fact]
    public async Task Snapshots_CreateAndList_ShouldReturnCreatedSnapshot()
    {
        var client = await CreateAuthenticatedClientAsync();

        await client.PostAsync("/v1/admin/metadata/apply", JsonContent(MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"))));

        var createResponse = await client.PostAsJsonAsync("/v1/admin/metadata/snapshots", new { label = "release-v1", notes = "baseline" });
        if (createResponse.StatusCode != HttpStatusCode.Created)
        {
            var content = await createResponse.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"Snapshot create failure: {content}");
        }
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var label = created.GetProperty("snapshot").GetProperty("label").GetString();
        label.Should().NotBeNullOrWhiteSpace();

        var listResponse = await client.GetAsync("/v1/admin/metadata/snapshots");
        listResponse.EnsureSuccessStatusCode();
        var listPayload = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        listPayload.GetProperty("snapshots")
            .EnumerateArray()
            .Select(element => element.GetProperty("label").GetString())
            .Should().Contain(label);
    }

    [Fact]
    public async Task Snapshots_Restore_ShouldRevertMetadata()
    {
        var client = await CreateAuthenticatedClientAsync();

        await client.PostAsync("/v1/admin/metadata/apply", JsonContent(MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"))));
        var snapshotResponse = await client.PostAsJsonAsync("/v1/admin/metadata/snapshots", new { label = "baseline" });
        if (snapshotResponse.StatusCode != HttpStatusCode.Created)
        {
            var content = await snapshotResponse.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"Snapshot create failure: {content}");
        }
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var snapshotPayload = await snapshotResponse.Content.ReadFromJsonAsync<JsonElement>();
        var label = snapshotPayload.GetProperty("snapshot").GetProperty("label").GetString();

        await client.PostAsync("/v1/admin/metadata/apply", JsonContent(MetadataTestFile.Create("catalog-v2", ("roads", "roads-primary"))));
        var restoreResponse = await client.PostAsync($"/v1/admin/metadata/snapshots/{label}/restore", content: null);
        restoreResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        await registry.EnsureInitializedAsync();
        var snapshot = await registry.GetSnapshotAsync();
        snapshot.Catalog.Id.Should().Be("catalog-v1");
    }

    [Fact]
    public async Task Snapshots_Get_ShouldReturnMetadataContent()
    {
        var client = await CreateAuthenticatedClientAsync();

        await client.PostAsync("/v1/admin/metadata/apply", JsonContent(MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"))));
        var snapshotResponse = await client.PostAsJsonAsync("/v1/admin/metadata/snapshots", new { label = "content" });
        if (snapshotResponse.StatusCode != HttpStatusCode.Created)
        {
            var content = await snapshotResponse.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"Snapshot create failure: {content}");
        }
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var snapshotPayload = await snapshotResponse.Content.ReadFromJsonAsync<JsonElement>();
        var label = snapshotPayload.GetProperty("snapshot").GetProperty("label").GetString();

        var getResponse = await client.GetAsync($"/v1/admin/metadata/snapshots/{label}");
        getResponse.EnsureSuccessStatusCode();
        var payload = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("metadata").GetString().Should().Contain("catalog-v1");
    }

    [Fact]
    public async Task Validate_ShouldReturnOkForValidPayload()
    {
        var client = await CreateAuthenticatedClientAsync();
        var payload = MetadataTestFile.Create("catalog-v1", ("roads", "roads-primary"));

        var response = await client.PostAsync("/v1/admin/metadata/validate", JsonContent(payload));
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            var authHeader = string.Join(", ", response.Headers.WwwAuthenticate.Select(h => h.Parameter is null ? h.Scheme : $"{h.Scheme} {h.Parameter}"));
            throw new InvalidOperationException($"Validate endpoint failed: {(int)response.StatusCode} {response.ReasonPhrase} - {body} (WWW-Authenticate: {authHeader})");
        }

        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        responseBody.GetProperty("status").GetString().Should().Be("valid");
        responseBody.GetProperty("warnings").ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Validate_ShouldReturnUnprocessableForInvalidPayload()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/v1/admin/metadata/validate", JsonContent("{ invalid json }"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
    private Task<HttpClient> CreateAuthenticatedClientAsync() => _factory.CreateAuthenticatedClientAsync();

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");
}

public sealed class ReloadableMetadataFactory : WebApplicationFactory<Program>, IDisposable
{
    private const string AdminUsername = "admin";
    internal const string AdminPassword = "ChangeMe123!";

    private readonly string _authStorePath;
    private readonly string? _originalBuildVersion;
    private readonly string? _originalBuildDate;
    private bool _bootstrapped;

    public ReloadableMetadataFactory()
    {
        MetadataPath = Path.Combine(Path.GetTempPath(), $"honua-metadata-{Guid.NewGuid():N}.json");
        MetadataTestFile.Write(MetadataPath, "initial", ("svc", "layer"));
        _authStorePath = Path.Combine(Path.GetTempPath(), $"honua-auth-{Guid.NewGuid():N}.db");
        _originalBuildVersion = Environment.GetEnvironmentVariable("BUILD_VERSION");
        _originalBuildDate = Environment.GetEnvironmentVariable("BUILD_DATE");
        Environment.SetEnvironmentVariable("BUILD_VERSION", _originalBuildVersion ?? "test-build");
        Environment.SetEnvironmentVariable("BUILD_DATE", _originalBuildDate ?? "2025-01-01T00:00:00Z");
    }

    public string MetadataPath { get; }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.Sources.Clear();
            var configuration = new Dictionary<string, string?>
            {
                ["honua:metadata:provider"] = "json",
                ["honua:metadata:path"] = MetadataPath,
                ["honua:authentication:mode"] = "Local",
                ["honua:authentication:enforce"] = "false",
                ["honua:authentication:quickStart:enabled"] = "false",
                ["honua:authentication:local:storePath"] = _authStorePath,
                ["honua:authentication:bootstrap:adminUsername"] = AdminUsername,
                ["honua:authentication:bootstrap:adminPassword"] = AdminPassword,
                ["ConnectionStrings:Redis"] = "localhost:6379,abortConnect=false"
            };
            config.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHonuaConfigurationService>();
            services.RemoveAll<IMetadataProvider>();
            services.RemoveAll<IMetadataRegistry>();
            services.RemoveAll<IOutputCacheInvalidationService>();
            services.RemoveAll<RequestDelegateFactoryOptions>();
            services.RemoveAll<IOptions<RequestDelegateFactoryOptions>>();
            services.AddSingleton<IOutputCacheStore, NoopOutputCacheStore>();
            services.AddOutputCache();
            services.Configure<RouteHandlerOptions>(options => { options.ThrowOnBadRequest = false; });
            services.AddSingleton<IOptions<RequestDelegateFactoryOptions>>(
                Options.Create(new RequestDelegateFactoryOptions
                {
                    ThrowOnBadRequest = false,
                    DisableInferBodyFromParameters = false
                }));

            services.AddSingleton<IHonuaConfigurationService>(_ => new HonuaConfigurationService(new HonuaConfiguration
            {
                Metadata = new MetadataConfiguration
                {
                    Provider = "json",
                    Path = MetadataPath
                }
            }));

            services.AddSingleton<IMetadataProvider>(_ => new JsonMetadataProvider(MetadataPath));
            services.AddSingleton<IMetadataRegistry>(sp => new MetadataRegistry(sp.GetRequiredService<IMetadataProvider>()));
            services.AddSingleton<IOutputCacheInvalidationService, NoopOutputCacheInvalidationService>();
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        await EnsureBootstrapAsync();
        await EnsureCsrfTokenAsync(client);
        return client;
    }

    public new void Dispose()
    {
        base.Dispose();
        if (File.Exists(MetadataPath))
        {
            File.Delete(MetadataPath);
        }

        if (File.Exists(_authStorePath))
        {
            File.Delete(_authStorePath);
        }

        Environment.SetEnvironmentVariable("BUILD_VERSION", _originalBuildVersion);
        Environment.SetEnvironmentVariable("BUILD_DATE", _originalBuildDate);
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

    private sealed class NoopOutputCacheInvalidationService : IOutputCacheInvalidationService
    {
        public Task InvalidateStacCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateStacCollectionCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateStacItemsCacheAsync(string collectionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateCatalogCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAllCacheAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopOutputCacheStore : IOutputCacheStore
    {
        public ValueTask EvictByTagAsync(string tag, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<byte[]> GetAsync(string key, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Array.Empty<byte>());

        public ValueTask SetAsync(string key, byte[] value, string[]? tags, TimeSpan validFor, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
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

        var csrfResponse = await response.Content.ReadFromJsonAsync<CsrfTokenResponse>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Failed to obtain CSRF token for authentication.");

        client.DefaultRequestHeaders.Remove(csrfResponse.HeaderName);
        client.DefaultRequestHeaders.Add(csrfResponse.HeaderName, csrfResponse.Token);
    }
}

internal static class MetadataTestFile
{
    public static string Create(string catalogId, params (string ServiceId, string LayerId)[] services)
    {
        var serviceEntries = services.Select(s => new { id = s.ServiceId, folderId = "folder", serviceType = "feature", dataSourceId = "ds" }).ToArray();
        var layerEntries = services.Select(s => new { id = s.LayerId, serviceId = s.ServiceId, geometryType = "Point", idField = "id", geometryField = "geom" }).ToArray();

        var model = new
        {
            server = new
            {
                allowedHosts = new[] { "localhost" }
            },
            catalog = new { id = catalogId },
            folders = new[] { new { id = "folder" } },
            dataSources = new[] { new { id = "ds", provider = "sqlite", connectionString = "Data Source=:memory:" } },
            services = serviceEntries,
            layers = layerEntries
        };

        return JsonSerializer.Serialize(model, JsonSerializerOptionsRegistry.WebIndented);
    }

    public static string CreateWithoutRequiredLayerField()
    {
        var model = new
        {
            server = new
            {
                allowedHosts = new[] { "localhost" }
            },
            catalog = new { id = "invalid" },
            folders = new[] { new { id = "folder" } },
            dataSources = new[] { new { id = "ds", provider = "sqlite", connectionString = "Data Source=:memory:" } },
            services = new[] { new { id = "svc", folderId = "folder", serviceType = "feature", dataSourceId = "ds" } },
            layers = new[] { new { id = "layer", serviceId = "svc", geometryType = "Point", idField = "id" } }
        };

        return JsonSerializer.Serialize(model, JsonSerializerOptionsRegistry.WebIndented);
    }

    public static void Write(string path, string catalogId, params (string ServiceId, string LayerId)[] services)
    {
        File.WriteAllText(path, Create(catalogId, services));
    }
}
