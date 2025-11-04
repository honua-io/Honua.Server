using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class CartoToolkitTests : IClassFixture<CartoToolkitFixture>, IAsyncLifetime
{
    private static readonly SemaphoreSlim NpmRestoreLock = new(1, 1);
    private static bool _modulesInstalled;

    private readonly CartoToolkitFixture _fixture;

    public CartoToolkitTests(CartoToolkitFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CartoToolkit_ShouldExecuteQueries()
    {
        if (!ShouldRunToolkit())
        {
            return;
        }

        using var client = _fixture.CreateClient();
        var baseUrl = client.BaseAddress?.ToString().TrimEnd('/') ?? throw new InvalidOperationException("Client base address was not configured.");

        var solutionRoot = TestEnvironment.SolutionRoot;
        var scriptPath = Path.Combine(solutionRoot, "tests", "carto-toolkit", "carto-toolkit.smoke.mjs");
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException("Carto toolkit smoke script was not found.", scriptPath);
        }

        var processInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"{scriptPath} {baseUrl} roads.roads-primary",
            WorkingDirectory = Path.Combine(solutionRoot, "tests", "carto-toolkit"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo) ?? throw new InvalidOperationException("Failed to start node process.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Carto toolkit script failed with exit code {process.ExitCode}: {stderr}");
        }

        using var document = JsonDocument.Parse(stdout);
        var root = document.RootElement;

        root.GetProperty("rowCount").GetInt32().Should().BeGreaterThan(0);
        root.GetProperty("totalRows").GetInt32().Should().BeGreaterThan(0);

        var keys = root.GetProperty("firstRowKeys").EnumerateArray();
        keys.Should().Contain(element => string.Equals(element.GetString(), "geom", StringComparison.OrdinalIgnoreCase));
    }

    public async Task InitializeAsync()
    {
        if (!ShouldRunToolkit())
        {
            return;
        }

        await _fixture.PrepareMetadataAsync();
        await EnsureNodeModulesAsync();
    }

    public Task DisposeAsync() => ShouldRunToolkit() ? _fixture.ResetMetadataAsync() : Task.CompletedTask;

    private static async Task EnsureNodeModulesAsync()
    {
        if (_modulesInstalled)
        {
            return;
        }

        await NpmRestoreLock.WaitAsync();
        try
        {
            if (_modulesInstalled)
            {
                return;
            }

            var solutionRoot = TestEnvironment.SolutionRoot;
            var workingDirectory = Path.Combine(solutionRoot, "tests", "carto-toolkit");
            var processInfo = new ProcessStartInfo
            {
                FileName = "npm",
                Arguments = "install",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo) ?? throw new InvalidOperationException("Failed to start npm install.");
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"npm install failed with exit code {process.ExitCode}: {error}");
            }

            _modulesInstalled = true;
        }
        finally
        {
            NpmRestoreLock.Release();
        }
    }
    private static bool ShouldRunToolkit() => string.Equals(
        Environment.GetEnvironmentVariable("HONUA_RUN_CARTO_TOOLKIT"),
        "1",
        StringComparison.OrdinalIgnoreCase);
}

public sealed class CartoToolkitFixture : IAsyncLifetime, IDisposable
{
    private readonly HonuaWebApplicationFactory _factory;
    private string? _originalMetadata;
    private string? _databasePath;
    private bool _metadataPrepared;

    public CartoToolkitFixture()
    {
        _factory = new HonuaWebApplicationFactory().WithHonuaWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["honua:authentication:mode"] = "QuickStart",
                    ["honua:authentication:quickStart:enabled"] = "true",
                    ["honua:authentication:enforce"] = "false"
                });
            });
        });
    }

    public IServiceProvider Services => _factory.Services;

    public HttpClient CreateClient() => _factory.CreateClient();

    public async Task PrepareMetadataAsync()
    {
        if (_metadataPrepared)
        {
            return;
        }

        _originalMetadata = File.ReadAllText(_factory.MetadataPath);
        _databasePath = Path.Combine(Path.GetTempPath(), $"honua-carto-toolkit-{Guid.NewGuid():N}.db");

        var sampleDb = Path.Combine(TestEnvironment.SolutionRoot, "samples", "ogc", "ogc-sample.db");
        File.Copy(sampleDb, _databasePath, overwrite: true);

        var metadataNode = JsonNode.Parse(_originalMetadata!) ?? throw new InvalidOperationException("Failed to parse metadata.");
        var dataSources = metadataNode["dataSources"]?.AsArray() ?? throw new InvalidOperationException("Metadata missing dataSources array.");
        if (dataSources.Count == 0)
        {
            throw new InvalidOperationException("Metadata does not contain any data sources.");
        }

        dataSources[0]!["connectionString"] = $"Data Source={_databasePath}";
        var updatedMetadata = metadataNode.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
        File.WriteAllText(_factory.MetadataPath, updatedMetadata);

        using var scope = Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
        await registry.ReloadAsync();

        _metadataPrepared = true;
    }

    public async Task ResetMetadataAsync()
    {
        if (!_metadataPrepared)
        {
            return;
        }

        if (_originalMetadata is not null)
        {
            File.WriteAllText(_factory.MetadataPath, _originalMetadata);
            using var scope = Services.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<IMetadataRegistry>();
            await registry.ReloadAsync();
        }

        if (_databasePath is not null)
        {
            try
            {
                File.Delete(_databasePath);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }

        _metadataPrepared = false;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ResetMetadataAsync();
        _factory.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
    }
}
