using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host;
using Honua.Server.Host.Health;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public class HealthCheckTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public HealthCheckTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void StartupHealth_ShouldFailFast_WhenMetadataUnavailable()
    {
        var act = () => _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["honua:services:odata:enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMetadataRegistry>();
                services.AddSingleton<IMetadataRegistry, FailingMetadataRegistry>();
            });
        }).CreateClient();

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*Metadata file missing*");
    }

    [Fact]
    public async Task ReadinessHealth_ShouldReportUnhealthy_WhenPrimaryDataSourceFails()
    {
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["honua:services:odata:enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDataSourceHealthContributor>();
                services.AddSingleton<IDataSourceHealthContributor, FailingDataSourceHealthContributor>();
            });
        }).CreateClient();

        var response = await client.GetAsync("/healthz/ready");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("status").GetString().Should().Be("Unhealthy");
        payload.GetProperty("entries").GetProperty("dataSources").GetProperty("status").GetString().Should().Be("Unhealthy");
    }

    private sealed class FailingMetadataRegistry : IMetadataRegistry
    {
        public MetadataSnapshot Snapshot => throw new FileNotFoundException("Metadata file missing");

        public bool IsInitialized => false;

        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromException<MetadataSnapshot>(new FileNotFoundException("Metadata file missing"));

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.FromException(new FileNotFoundException("Metadata file missing"));

        public Task ReloadAsync(CancellationToken cancellationToken = default)
            => Task.FromException(new FileNotFoundException("Metadata file missing"));

        public IChangeToken GetChangeToken() => TestChangeTokens.Noop;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.FromException(new FileNotFoundException("Metadata file missing"));
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = null!;
            return false;
        }
    }

    private sealed class FailingDataSourceHealthContributor : IDataSourceHealthContributor
    {
        public string Provider => "sqlite";

        public Task<HealthCheckResult> CheckAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken)
            => Task.FromResult(HealthCheckResult.Unhealthy("Data source unreachable."));
    }
}
