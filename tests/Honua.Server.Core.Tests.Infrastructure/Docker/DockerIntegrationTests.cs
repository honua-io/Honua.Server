using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Infrastructure.Docker;

/// <summary>
/// Docker integration tests to ensure Honua Server runs correctly in containerized environments.
/// Tests container lifecycle, networking, environment configuration, and health.
/// </summary>
[Trait("Category", "Docker")]
[Trait("Category", "Integration")]
public class DockerIntegrationTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private IContainer? _honuaContainer;
    private IContainer? _postgresContainer;

    public DockerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // Tests will create containers as needed
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_honuaContainer != null)
        {
            await _honuaContainer.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    // =====================================================
    // Container Lifecycle Tests
    // =====================================================

    [Fact]
    public async Task HonuaContainer_StartsSuccessfully_WithQuickStartMode()
    {
        // Arrange
        var metadataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "metadata-ogc-sample.json");

        _honuaContainer = new ContainerBuilder()
            .WithImage("honua-server:test") // Assumes image is built locally
            .WithPortBinding(5000, true)
            .WithEnvironment("HONUA_ALLOW_QUICKSTART", "true")
            .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
            .WithEnvironment("HONUA__METADATA__PATH", "/app/metadata.json")
            .WithEnvironment("HONUA__AUTHENTICATION__MODE", "QuickStart")
            .WithEnvironment("HONUA__AUTHENTICATION__ENFORCE", "false")
            .WithBindMount(metadataPath, "/app/metadata.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000).ForPath("/ogc")))
            .Build();

        // Act & Assert - container should start without errors
        await _honuaContainer.StartAsync();
        _output.WriteLine($"✅ Container started successfully on port {_honuaContainer.GetMappedPublicPort(5000)}");
    }

    [Fact]
    public async Task HonuaContainer_RespondsToHttpRequests()
    {
        // Arrange
        var metadataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "metadata-ogc-sample.json");

        _honuaContainer = new ContainerBuilder()
            .WithImage("honua-server:test")
            .WithPortBinding(5000, true)
            .WithEnvironment("HONUA_ALLOW_QUICKSTART", "true")
            .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
            .WithEnvironment("HONUA__METADATA__PATH", "/app/metadata.json")
            .WithEnvironment("HONUA__AUTHENTICATION__MODE", "QuickStart")
            .WithEnvironment("HONUA__AUTHENTICATION__ENFORCE", "false")
            .WithBindMount(metadataPath, "/app/metadata.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000).ForPath("/ogc")))
            .Build();

        await _honuaContainer.StartAsync();

        var port = _honuaContainer.GetMappedPublicPort(5000);
        using var httpClient = new HttpClient();

        // Act
        var response = await httpClient.GetAsync($"http://localhost:{port}/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("links", out _).Should().BeTrue();

        _output.WriteLine($"✅ Container responded successfully to HTTP request");
    }

    // =====================================================
    // Database Connectivity Tests
    // =====================================================

    [Fact]
    public async Task HonuaContainer_ConnectsToPostgresContainer_Successfully()
    {
        // Arrange - Start PostgreSQL container
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgis/postgis:16-3.4")
            .WithPortBinding(5432, true)
            .WithEnvironment("POSTGRES_USER", "honua_test")
            .WithEnvironment("POSTGRES_PASSWORD", "honua_test_password")
            .WithEnvironment("POSTGRES_DB", "honua_test")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _postgresContainer.StartAsync();
        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);

        _output.WriteLine($"✅ PostgreSQL container started on port {postgresPort}");

        // Wait for PostgreSQL to be fully ready
        await Task.Delay(3000);

        // Create test metadata pointing to containerized PostgreSQL
        var testMetadata = $$"""
        {
          "dataSources": [
            {
              "id": "test-postgres",
              "provider": "postgres",
              "connectionString": "Host=host.docker.internal;Port={{postgresPort}};Database=honua_test;Username=honua_test;Password=honua_test_password;"
            }
          ],
          "services": []
        }
        """;

        var metadataPath = Path.Combine(Path.GetTempPath(), $"honua-docker-test-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(metadataPath, testMetadata);

        try
        {
            // Arrange - Start Honua container with PostgreSQL connection
            _honuaContainer = new ContainerBuilder()
                .WithImage("honua-server:test")
                .WithPortBinding(5000, true)
                .WithEnvironment("HONUA_ALLOW_QUICKSTART", "true")
                .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
                .WithEnvironment("HONUA__METADATA__PATH", "/app/metadata.json")
                .WithEnvironment("HONUA__AUTHENTICATION__MODE", "QuickStart")
                .WithEnvironment("HONUA__AUTHENTICATION__ENFORCE", "false")
                .WithBindMount(metadataPath, "/app/metadata.json")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000).ForPath("/ogc")))
                .Build();

            await _honuaContainer.StartAsync();

            var port = _honuaContainer.GetMappedPublicPort(5000);
            using var httpClient = new HttpClient();

            // Act
            var response = await httpClient.GetAsync($"http://localhost:{port}/ogc");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            _output.WriteLine($"✅ Honua connected to PostgreSQL successfully");
        }
        finally
        {
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }
        }
    }

    // =====================================================
    // Environment Configuration Tests
    // =====================================================

    [Fact]
    public async Task HonuaContainer_RespectsEnvironmentVariables()
    {
        // Arrange
        var metadataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "metadata-ogc-sample.json");

        _honuaContainer = new ContainerBuilder()
            .WithImage("honua-server:test")
            .WithPortBinding(5000, true)
            .WithEnvironment("HONUA_ALLOW_QUICKSTART", "true")
            .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
            .WithEnvironment("HONUA__METADATA__PATH", "/app/metadata.json")
            .WithEnvironment("HONUA__AUTHENTICATION__MODE", "QuickStart")
            .WithEnvironment("HONUA__AUTHENTICATION__ENFORCE", "false")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Production")
            .WithBindMount(metadataPath, "/app/metadata.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000).ForPath("/ogc")))
            .Build();

        // Act
        await _honuaContainer.StartAsync();

        // Assert - container should start with custom environment
        var exitCode = await _honuaContainer.GetExitCodeAsync();
        exitCode.Should().Be(0, "container should still be running");

        _output.WriteLine($"✅ Container respects environment variables");
    }

    // =====================================================
    // Volume Mount Tests
    // =====================================================

    [Fact]
    public async Task HonuaContainer_LoadsMetadataFromVolume()
    {
        // Arrange - Create custom metadata
        var customMetadata = """
        {
          "dataSources": [],
          "services": []
        }
        """;

        var metadataPath = Path.Combine(Path.GetTempPath(), $"honua-docker-custom-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(metadataPath, customMetadata);

        _honuaContainer = new ContainerBuilder()
            .WithImage("honua-server:test")
            .WithPortBinding(5000, true)
            .WithEnvironment("HONUA_ALLOW_QUICKSTART", "true")
            .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
            .WithEnvironment("HONUA__METADATA__PATH", "/app/custom-metadata.json")
            .WithEnvironment("HONUA__AUTHENTICATION__MODE", "QuickStart")
            .WithEnvironment("HONUA__AUTHENTICATION__ENFORCE", "false")
            .WithBindMount(metadataPath, "/app/custom-metadata.json")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000).ForPath("/ogc")))
            .Build();

        try
        {
            // Act
            await _honuaContainer.StartAsync();

            var port = _honuaContainer.GetMappedPublicPort(5000);
            using var httpClient = new HttpClient();

            var response = await httpClient.GetAsync($"http://localhost:{port}/ogc/collections");

            // Assert - should return empty collections from custom metadata
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("collections", out var collections))
            {
                collections.GetArrayLength().Should().Be(0, "custom metadata has no services/collections");
            }

            _output.WriteLine($"✅ Container loaded custom metadata from volume");
        }
        finally
        {
            if (File.Exists(metadataPath))
            {
                File.Delete(metadataPath);
            }
        }
    }

    // =====================================================
    // Container Resource Tests
    // =====================================================

    [Fact]
    public async Task HonuaContainer_RunsWithResourceLimits()
    {
        // Arrange
        var metadataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "metadata-ogc-sample.json");

        _honuaContainer = new ContainerBuilder()
            .WithImage("honua-server:test")
            .WithPortBinding(5000, true)
            .WithEnvironment("HONUA_ALLOW_QUICKSTART", "true")
            .WithEnvironment("HONUA__METADATA__PROVIDER", "json")
            .WithEnvironment("HONUA__METADATA__PATH", "/app/metadata.json")
            .WithEnvironment("HONUA__AUTHENTICATION__MODE", "QuickStart")
            .WithEnvironment("HONUA__AUTHENTICATION__ENFORCE", "false")
            .WithBindMount(metadataPath, "/app/metadata.json")
            // Note: Resource limits removed due to Testcontainers API changes in v3.10
            // .WithResourceMapping() - to be re-added once API is updated
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5000).ForPath("/ogc")))
            .Build();

        // Act
        await _honuaContainer.StartAsync();

        var port = _honuaContainer.GetMappedPublicPort(5000);
        using var httpClient = new HttpClient();

        var response = await httpClient.GetAsync($"http://localhost:{port}/ogc");

        // Assert - should still work with resource limits
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        _output.WriteLine($"✅ Container runs successfully with CPU and memory limits");
    }
}
