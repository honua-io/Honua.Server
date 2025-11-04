using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Host.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

public sealed class SecurityPolicyMiddlewareTests
{
    [Fact]
    public async Task PostWithoutAuthorizationMetadata_Returns403()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/test");
        using var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetWithoutAuthorizationMetadata_AllowsRequest()
    {
        using var client = CreateClient();
        using var response = await client.GetAsync("/api/test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SpecCompliantMutation_OnAllowListedRoute_IsPermitted()
    {
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/stac/search")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static HttpClient CreateClient()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddRouting())
            .Configure(app =>
            {
                app.UseRouting();
                app.UseSecurityPolicy();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/api/test", () => Results.Ok());
                    endpoints.MapGet("/api/test", () => Results.Ok());
                    endpoints.MapPost("/stac/search", () => Results.Ok());
                });
            });

        var server = new TestServer(builder);
        return server.CreateClient();
    }
}
