using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Deployment.E2ETests.Infrastructure;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for the complete deployment workflow from metadata ingestion through API availability.
/// </summary>
[Trait("Category", "E2E")]
public class DeploymentWorkflowTests : IClassFixture<DeploymentTestFactory>
{
    private readonly DeploymentTestFactory _factory;

    public DeploymentWorkflowTests(DeploymentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CompleteDeploymentWorkflow_QuickStartMode_ShouldSucceed()
    {
        // Arrange - Write valid metadata
        var metadata = new TestMetadataBuilder()
            .WithCatalog("quickstart-catalog", "QuickStart Test Catalog", "QuickStart deployment test")
            .AddPostgresDataSource("test-db", _factory.PostgresConnectionString)
            .AddFeatureService("test-service", "Test Feature Service", "test-db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        // Act - Create client (triggers application startup)
        var client = _factory.CreateClient();

        // Assert - Verify startup health
        var startupResponse = await client.GetAsync("/healthz/startup");
        startupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var startupHealth = await startupResponse.Content.ReadFromJsonAsync<JsonDocument>();
        startupHealth!.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        // Assert - Verify liveness health
        var liveResponse = await client.GetAsync("/healthz/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var liveHealth = await liveResponse.Content.ReadFromJsonAsync<JsonDocument>();
        liveHealth!.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        // Assert - Verify readiness health
        var readyResponse = await client.GetAsync("/healthz/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var readyHealth = await readyResponse.Content.ReadFromJsonAsync<JsonDocument>();
        readyHealth!.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

        // Assert - Verify OGC landing page
        var ogcResponse = await client.GetAsync("/ogc");
        ogcResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ogcPayload = await ogcResponse.Content.ReadFromJsonAsync<JsonDocument>();
        ogcPayload!.RootElement.GetProperty("catalog").GetProperty("id").GetString()
            .Should().Be("quickstart-catalog");

        // Assert - Verify services are available
        var servicesArray = ogcPayload.RootElement.GetProperty("services").EnumerateArray().ToList();
        servicesArray.Should().ContainSingle(s => s.GetProperty("id").GetString() == "test-service");
    }

    [Fact]
    public async Task CompleteDeploymentWorkflow_LocalAuthMode_ShouldRequireAuthentication()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("auth-catalog", "Auth Test Catalog", "Authentication deployment test")
            .AddPostgresDataSource("test-db", _factory.PostgresConnectionString)
            .AddFeatureService("secure-service", "Secure Feature Service", "test-db")
            .Build();

        _factory.UseLocalAuth();
        _factory.WriteMetadata(metadata);

        // Act - Try to access OGC without authentication
        var unauthClient = _factory.CreateClient();
        var unauthResponse = await unauthClient.GetAsync("/ogc");

        // Assert - Should require authentication
        unauthResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Act - Authenticate and access
        var authClient = await _factory.CreateAuthenticatedClientAsync();
        var authResponse = await authClient.GetAsync("/ogc");

        // Assert - Should succeed with authentication
        authResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await authResponse.Content.ReadFromJsonAsync<JsonDocument>();
        payload!.RootElement.GetProperty("catalog").GetProperty("id").GetString()
            .Should().Be("auth-catalog");
    }

    [Fact]
    public async Task MetadataIngestion_ValidJSON_ShouldLoadSuccessfully()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("ingestion-test", "Ingestion Test Catalog", "Metadata ingestion test")
            .AddPostgresDataSource("primary-db", _factory.PostgresConnectionString)
            .AddFolder("geo-services", "Geospatial Services", 10)
            .AddFeatureService("roads", "Road Network", "primary-db", "geo-services")
            .AddFeatureService("parcels", "Land Parcels", "primary-db", "geo-services")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        // Act
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var catalog = payload!.RootElement.GetProperty("catalog");
        catalog.GetProperty("id").GetString().Should().Be("ingestion-test");
        catalog.GetProperty("title").GetString().Should().Be("Ingestion Test Catalog");

        var services = payload.RootElement.GetProperty("services").EnumerateArray().ToList();
        services.Should().HaveCount(2);
        services.Should().Contain(s => s.GetProperty("id").GetString() == "roads");
        services.Should().Contain(s => s.GetProperty("id").GetString() == "parcels");
    }

    [Fact]
    public async Task ApplicationStartup_WithValidMetadata_ShouldInitializeAllComponents()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("startup-test", "Startup Test Catalog", "Startup component test")
            .AddPostgresDataSource("db1", _factory.PostgresConnectionString)
            .AddFeatureService("service1", "Service 1", "db1")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        // Act
        var client = _factory.CreateClient();

        // Assert - Check all health check components
        var readyResponse = await client.GetAsync("/healthz/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await readyResponse.Content.ReadFromJsonAsync<JsonDocument>();

        var entries = health!.RootElement.GetProperty("entries");

        // Verify metadata health check
        entries.GetProperty("metadata").GetProperty("status").GetString()
            .Should().Be("Healthy", "metadata should be loaded successfully");

        // Verify data sources health check
        entries.GetProperty("dataSources").GetProperty("status").GetString()
            .Should().Be("Healthy", "data sources should be accessible");

        // Verify schema health check (may be degraded if no schema validation configured)
        entries.TryGetProperty("schema", out _).Should().BeTrue("schema health check should be registered");
    }

    [Fact]
    public async Task DatabaseMigration_PostgresDataSource_ShouldApplySuccessfully()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("migration-test", "Migration Test Catalog", "Database migration test")
            .AddPostgresDataSource("migrated-db", _factory.PostgresConnectionString)
            .AddFeatureService("test-service", "Test Service", "migrated-db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        // Act - Create client triggers startup and migrations
        var client = _factory.CreateClient();

        // Assert - Verify STAC tables exist (migrations applied)
        var stacResponse = await client.GetAsync("/stac");
        stacResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var stacPayload = await stacResponse.Content.ReadFromJsonAsync<JsonDocument>();
        stacPayload!.RootElement.GetProperty("type").GetString().Should().Be("Catalog");
        stacPayload.RootElement.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthChecks_AllEndpoints_ShouldBeAccessible()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act & Assert - Startup health
        var startupResponse = await client.GetAsync("/healthz/startup");
        startupResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act & Assert - Liveness health
        var liveResponse = await client.GetAsync("/healthz/live");
        liveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act & Assert - Readiness health
        var readyResponse = await client.GetAsync("/healthz/ready");
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify health check response format
        var readyHealth = await readyResponse.Content.ReadFromJsonAsync<JsonDocument>();
        readyHealth!.RootElement.GetProperty("status").GetString().Should().BeOneOf("Healthy", "Degraded");
        readyHealth.RootElement.TryGetProperty("entries", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RedisCache_WhenConfigured_ShouldBeHealthy()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var health = await response.Content.ReadFromJsonAsync<JsonDocument>();

        var entries = health!.RootElement.GetProperty("entries");
        if (entries.TryGetProperty("redisStores", out var redisHealth))
        {
            // Redis is configured, should be healthy
            redisHealth.GetProperty("status").GetString().Should().Be("Healthy");
        }
    }
}
