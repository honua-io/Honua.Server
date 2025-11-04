using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class ObservabilityMetricsTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public ObservabilityMetricsTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MetricsEndpoint_Disabled_ShouldReturnNotFound()
    {
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/metrics");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MetricsEndpoint_Enabled_ShouldExposePrometheus()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["observability:metrics:enabled"] = "true",
                    ["observability:metrics:endpoint"] = "/metrics-test",
                    ["observability:metrics:usePrometheus"] = "true"
                });
            });
        });

        await BootstrapAsync(factory);
        var client = await CreateAuthenticatedClientAsync(factory);
        var warmup = await client.GetAsync("/healthz/live");
        warmup.EnsureSuccessStatusCode();

        var response = await client.GetAsync("/metrics-test");
        response.EnsureSuccessStatusCode();

        response.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("http_server_request_duration_seconds")
            .And.Contain("dotnet_process_cpu_count");
    }

    private static async Task BootstrapAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var bootstrap = scope.ServiceProvider.GetRequiredService<IAuthBootstrapService>();
        var result = await bootstrap.BootstrapAsync().ConfigureAwait(false);
        if (result.Status == AuthBootstrapStatus.Failed)
        {
            throw new InvalidOperationException($"Bootstrap failed: {result.Message}");
        }
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/local/login", new { username = HonuaWebApplicationFactory.DefaultAdminUsername, password = HonuaWebApplicationFactory.DefaultAdminPassword }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
        var token = payload.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
