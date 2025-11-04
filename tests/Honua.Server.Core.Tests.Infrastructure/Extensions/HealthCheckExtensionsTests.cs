using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Extensions;

/// <summary>
/// Tests for HealthCheckExtensions ensuring proper health check registration.
/// </summary>
[Trait("Category", "Unit")]
public class HealthCheckExtensionsTests
{
    [Fact]
    public void AddHonuaHealthChecks_ShouldRegisterAllHealthChecks()
    {
        // Arrange
        var services = CreateServices(out var metadataPath, new Dictionary<string, string?>
        {
            { "honua:dataProviders:0:id", "test-provider" },
            { "honua:dataProviders:0:type", "PostgreSQL" },
            { "honua:dataProviders:0:connectionString", "Server=localhost;Database=test" }
        });

        // Act
        services.AddHonuaHealthChecks();

        // Assert
        try
        {
            var provider = services.BuildServiceProvider();
            var healthCheckService = provider.GetService<HealthCheckService>();
            healthCheckService.Should().NotBeNull();
        }
        finally
        {
            TryDelete(metadataPath);
        }
    }

    [Fact]
    public void AddHonuaHealthChecks_ShouldRegisterHealthCheckContributors()
    {
        // Arrange
        var services = CreateServices(out var metadataPath);

        // Act
        services.AddHonuaHealthChecks();

        // Assert
        try
        {
            var provider = services.BuildServiceProvider();
            var contributors = provider.GetServices<IDataSourceHealthContributor>();
            contributors.Should().NotBeNull();
            contributors.Should().NotBeEmpty();
        }
        finally
        {
            TryDelete(metadataPath);
        }
    }

    [Fact]
    public async Task AddHonuaHealthChecks_SelfCheck_ShouldBeHealthy()
    {
        // Arrange
        var services = CreateServices(out var metadataPath);
        services.AddHonuaHealthChecks();

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Act
        try
        {
            var result = await healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("live"));

            // Assert
            result.Status.Should().Be(HealthStatus.Healthy);
        }
        finally
        {
            TryDelete(metadataPath);
        }
    }

    private static ServiceCollection CreateServices(out string metadataPath, IDictionary<string, string?>? additionalConfig = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        metadataPath = Path.Combine(Path.GetTempPath(), $"honua-health-{Guid.NewGuid():N}.json");
        var metadataJson = "{\"server\":{\"allowedHosts\":[\"localhost\"]},\"catalog\":{\"id\":\"health\"},\"folders\":[],\"dataSources\":[],\"services\":[],\"layers\":[],\"rasterDatasets\":[],\"styles\":[]}";
        File.WriteAllText(metadataPath, metadataJson);

        var configValues = new Dictionary<string, string?>
        {
            { "honua:metadata:provider", "json" },
            { "honua:metadata:path", metadataPath },
            { "honua:authentication:mode", "Local" },
            { "honua:authentication:enforce", "false" },
            { "ConnectionStrings:Redis", "localhost" }
        };

        if (additionalConfig != null)
        {
            foreach (var kvp in additionalConfig)
            {
                configValues[kvp.Key] = kvp.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddHonuaCoreServices(configuration, AppContext.BaseDirectory);

        return services;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }
}
