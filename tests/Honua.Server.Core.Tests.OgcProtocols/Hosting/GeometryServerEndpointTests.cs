using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Geoservices.GeometryService;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class GeometryServerEndpointTests : IClassFixture<HonuaWebApplicationFactory>, IDisposable
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly bool _previousForceStac;

    public GeometryServerEndpointTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _previousForceStac = HonuaWebApplicationFactory.ForceStac;
        HonuaWebApplicationFactory.ForceStac = true;
    }

    public void Dispose()
    {
        HonuaWebApplicationFactory.ForceStac = _previousForceStac;
    }

    [Fact]
    public async Task Project_ShouldReprojectPoints()
    {
        using var factory = CreateTestFactory();
        using var client = await CreateAuthenticatedClientAsync(factory);
        var payload = new
        {
            geometryType = "esriGeometryPoint",
            inSR = 4326,
            outSR = 3857,
            geometries = new
            {
                geometries = new[]
                {
                    new { x = -122.41, y = 45.52 },
                    new { x = -123.01, y = 46.11 }
                }
            }
        };

        var response = await client.PostAsJsonAsync("/v1/rest/services/Geometry/GeometryServer/project?f=json", payload);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, responseBody);
        var content = JsonNode.Parse(responseBody)!.AsObject();
        content.Should().NotBeNull();

        var geometries = content!["geometries"]!.AsArray();
        geometries.Should().HaveCount(2);
        content["spatialReference"]!.AsObject()["wkid"]!.GetValue<int>().Should().Be(3857);

        var firstProjected = geometries[0]!.AsObject();
        firstProjected["x"]!.GetValue<double>().Should().BeApproximately(-122.41, 1e-9);
        firstProjected["y"]!.GetValue<double>().Should().BeApproximately(45.52, 1e-9);

        var secondProjected = geometries[1]!.AsObject();
        secondProjected["x"]!.GetValue<double>().Should().BeApproximately(-123.01, 1e-9);
        secondProjected["y"]!.GetValue<double>().Should().BeApproximately(46.11, 1e-9);
    }

    [Fact]
    public async Task Project_ShouldRejectUnsupportedFormat()
    {
        using var factory = CreateTestFactory();
        using var client = await CreateAuthenticatedClientAsync(factory);
        var payload = new
        {
            geometryType = "esriGeometryPoint",
            inSR = 4326,
            outSR = 3857,
            geometries = new { geometries = new[] { new { x = 0, y = 0 } } }
        };

        var response = await client.PostAsJsonAsync("/v1/rest/services/Geometry/GeometryServer/project?f=geojson", payload);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, responseBody);
        var content = JsonNode.Parse(responseBody)!.AsObject();
        content!["error"]!.GetValue<string>().Should().Contain("Format 'geojson'");
    }

    [Fact]
    public async Task Project_ShouldReturnBadRequestWhenMissingGeometries()
    {
        using var factory = CreateTestFactory();
        using var client = await CreateAuthenticatedClientAsync(factory);
        var payload = new
        {
            geometryType = "esriGeometryPoint",
            inSR = 4326,
            outSR = 3857
        };

        var response = await client.PostAsJsonAsync("/v1/rest/services/Geometry/GeometryServer/project", payload);

        var responseBody = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, responseBody);
        var content = JsonNode.Parse(responseBody)!.AsObject();
        content!["error"]!.GetValue<string>().Should().Contain("geometries");
    }

    private HonuaWebApplicationFactory CreateTestFactory()
    {
        return _factory.WithHonuaWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["honua:authentication:mode"] = "Local",
                    ["honua:services:geometry:enabled"] = "true"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGeometryOperationExecutor>();
                services.AddSingleton<IGeometryOperationExecutor, PassthroughGeometryOperationExecutor>();
            });
        });
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(HonuaWebApplicationFactory factory)
    {
        await EnsureBootstrapAsync(factory);

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // Get CSRF token BEFORE login
        await EnsureCsrfTokenAsync(client);

        var response = await client.PostAsJsonAsync("/v1/api/auth/local/login", new
        {
            username = HonuaWebApplicationFactory.DefaultAdminUsername,
            password = HonuaWebApplicationFactory.DefaultAdminPassword
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonObject>();
        var token = payload!["token"]!.GetValue<string>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Refresh CSRF token AFTER authentication
        ClearCsrfHeaders(client);
        await EnsureCsrfTokenAsync(client);

        return client;
    }

    private async Task EnsureBootstrapAsync(HonuaWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IAuthBootstrapService>();
        var result = await bootstrap.BootstrapAsync();
        result.Status.Should().NotBe(AuthBootstrapStatus.Failed, result.Message ?? "Bootstrap failed");
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

    private sealed class PassthroughGeometryOperationExecutor : IGeometryOperationExecutor
    {
        public IReadOnlyList<NtsGeometry> Project(GeometryProjectOperation operation, CancellationToken cancellationToken = default)
        {
            var results = new List<NtsGeometry>(operation.Geometries.Count);
            foreach (var geometry in operation.Geometries)
            {
                var copy = geometry.Copy();
                copy.SRID = operation.OutputSpatialReference;
                results.Add(copy);
            }

            return results;
        }

        public IReadOnlyList<NtsGeometry> Buffer(GeometryBufferOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
        public IReadOnlyList<NtsGeometry> Simplify(GeometrySimplifyOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
        public NtsGeometry? Union(GeometrySetOperation operation, CancellationToken cancellationToken = default) => operation.Geometries.Count > 0 ? operation.Geometries[0] : null;
        public IReadOnlyList<NtsGeometry> Intersect(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default) => operation.Geometries1;
        public IReadOnlyList<NtsGeometry> Difference(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default) => operation.Geometries1;
        public IReadOnlyList<NtsGeometry> ConvexHull(GeometrySetOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
        public IReadOnlyList<double> Distance(GeometryDistanceOperation operation, CancellationToken cancellationToken = default) => new[] { 0.0 };
        public IReadOnlyList<double> Areas(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default) => new[] { 0.0 };
        public IReadOnlyList<double> Lengths(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default) => new[] { 0.0 };
        public IReadOnlyList<NtsGeometry> LabelPoints(GeometryLabelPointsOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
        public IReadOnlyList<NtsGeometry> Cut(GeometryCutOperation operation, CancellationToken cancellationToken = default) => operation.Target != null ? new[] { operation.Target } : Array.Empty<NtsGeometry>();
        public NtsGeometry Reshape(GeometryReshapeOperation operation, CancellationToken cancellationToken = default) => operation.Target;
        public IReadOnlyList<NtsGeometry> Offset(GeometryOffsetOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
        public IReadOnlyList<NtsGeometry> TrimExtend(GeometryTrimExtendOperation operation, CancellationToken cancellationToken = default) => operation.Polylines;
        public IReadOnlyList<NtsGeometry> Densify(GeometryDensifyOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
        public IReadOnlyList<NtsGeometry> Generalize(GeometryGeneralizeOperation operation, CancellationToken cancellationToken = default) => operation.Geometries;
    }
}
