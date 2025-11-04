using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Commands;
using Honua.Cli.Services.Configuration;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Tests.Support;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class ConfigCommandsTests
{
    [Fact]
    public async Task ConfigInit_ShouldPersistConfiguration()
    {
        using var root = new TemporaryDirectory();
        var environment = new TestEnvironment(root.Path);
        var console = new TestConsole();
        var store = new HonuaCliConfigStore(environment);

        var command = new ConfigInitCommand(console, environment, store);
        var settings = new ConfigInitCommand.Settings
        {
            Host = "http://localhost:7000",
            Token = "abc",
            Overwrite = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        var config = await store.LoadAsync();
        config.Host.Should().Be("http://localhost:7000");
        config.Token.Should().Be("abc");
    }

    [Fact]
    public async Task StatusCommand_ShouldReportHealthAndAuth()
    {
        using var root = new TemporaryDirectory();
        var environment = new TestEnvironment(root.Path);
        var store = new HonuaCliConfigStore(environment);
        await store.SaveAsync(new HonuaCliConfig("http://localhost:5000", "token"));

        var handler = new StubHttpMessageHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        var factory = new StubHttpClientFactory(client);
        var console = new TestConsole();
        var resolver = new ControlPlaneConnectionResolver(store);

        var command = new StatusCommand(console, factory, resolver, store);
        var exitCode = await command.ExecuteAsync(null!, new StatusCommand.Settings());

        exitCode.Should().Be(0);
        console.Output.Should().Contain("/healthz/ready");
        console.Output.Should().Contain("ok");
        console.Output.Should().Contain("snapshots");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request.RequestUri is null)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
            }

            return request.RequestUri.AbsolutePath switch
            {
                "/healthz/ready" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
                "/admin/metadata/snapshots" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"snapshots\":[{\"label\":\"demo\"}]}", Encoding.UTF8, "application/json")
                }),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))
            };
        }
    }
}
