using System.Net;
using System.Text.Json;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Honua.Server.Host.Tests.Validation;

[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class ValidationMiddlewareTests
{
    [Fact]
    public async Task ValidationException_ReturnsProblemDetails()
    {
        using var host = await CreateTestHost(app =>
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/test-validation")
                {
                    var errors = new Dictionary<string, string[]>
                    {
                        ["field1"] = ["Field1 is required."],
                        ["field2"] = ["Field2 must be between 1 and 100."]
                    };
                    throw new ValidationException(errors);
                }
                await next(context);
            });
        });

        var client = host.GetTestClient();
        var response = await client.GetAsync("/test-validation");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ValidationProblemDetails>(content, JsonSerializerOptionsRegistry.DevTooling);

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Equal("One or more validation errors occurred.", problem.Title);
        Assert.Contains("field1", problem.Errors.Keys);
        Assert.Contains("field2", problem.Errors.Keys);
    }

    [Fact]
    public async Task ArgumentException_WithValidationMessage_ReturnsProblemDetails()
    {
        using var host = await CreateTestHost(app =>
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/test-argument")
                {
                    throw new ArgumentException("ServiceId is invalid", "serviceId");
                }
                await next(context);
            });
        });

        var client = host.GetTestClient();
        var response = await client.GetAsync("/test-argument");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, JsonSerializerOptionsRegistry.DevTooling);

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("ServiceId is invalid", problem.Detail);
    }

    [Fact]
    public async Task InvalidOperationException_WithValidationMessage_ReturnsProblemDetails()
    {
        using var host = await CreateTestHost(app =>
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/test-invalid-op")
                {
                    throw new InvalidOperationException("MinZoom cannot exceed MaxZoom.");
                }
                await next(context);
            });
        });

        var client = host.GetTestClient();
        var response = await client.GetAsync("/test-invalid-op");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<ProblemDetails>(content, JsonSerializerOptionsRegistry.DevTooling);

        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("MinZoom cannot exceed MaxZoom", problem.Detail);
    }

    [Fact]
    public async Task NonValidationException_PassesThrough()
    {
        using var host = await CreateTestHost(app =>
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/test-other")
                {
                    throw new InvalidOperationException("Something went wrong");
                }
                await next(context);
            });
        });

        var client = host.GetTestClient();

        // Should throw since it's not caught by validation middleware
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await client.GetAsync("/test-other");
        });
    }

    private static async Task<IHost> CreateTestHost(Action<IApplicationBuilder> configure)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddLogging();
                });
                webHost.Configure(app =>
                {
                    app.UseMiddleware<ValidationMiddleware>();
                    configure(app);
                    app.Run(context =>
                    {
                        context.Response.StatusCode = 200;
                        return context.Response.WriteAsync("OK");
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }
}
