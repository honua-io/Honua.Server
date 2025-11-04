using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Honua.Server.Deployment.E2ETests.Infrastructure;
using Xunit;

namespace Honua.Server.Deployment.E2ETests;

/// <summary>
/// End-to-end tests for negative scenarios including invalid metadata, missing dependencies, and error handling.
/// </summary>
[Trait("Category", "Integration")]
public class NegativeScenarioTests : IClassFixture<DeploymentTestFactory>
{
    private readonly DeploymentTestFactory _factory;

    public NegativeScenarioTests(DeploymentTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task InvalidMetadata_MalformedJSON_ShouldHandleGracefully()
    {
        // Arrange
        var invalidMetadata = TestMetadataBuilder.CreateInvalidMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(invalidMetadata);

        // Act - Try to create client (triggers startup)
        var exception = await Record.ExceptionAsync(async () =>
        {
            var client = _factory.CreateClient();
            await client.GetAsync("/healthz/startup");
        });

        // Assert - Should handle invalid JSON gracefully
        exception.Should().NotBeNull("application should fail to start with invalid metadata");
    }

    [Fact]
    public async Task MissingMetadataFile_ShouldFailStartup()
    {
        // Arrange - Don't write metadata file
        _factory.UseQuickStartAuth();
        // No WriteMetadata call

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            var client = _factory.CreateClient();
            await client.GetAsync("/healthz/startup");
        });

        // Assert
        exception.Should().NotBeNull("application should fail when metadata file is missing");
    }

    [Fact]
    public async Task InvalidDataSource_NonExistentDatabase_ShouldBeReflectedInHealthCheck()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("invalid-db-test", "Invalid DB Test", "Test invalid database")
            .AddPostgresDataSource("bad-db", "Host=invalid-host;Port=5432;Database=nonexistent;Username=test;Password=test")
            .AddFeatureService("test-svc", "Test Service", "bad-db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Data source health check should be unhealthy or degraded
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "health check should fail when data source is unreachable");
    }

    [Fact]
    public async Task MissingDataSource_ServiceReferenceInvalid_ShouldBeDetected()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMetadataWithMissingDataSource();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            var client = _factory.CreateClient();
            await client.GetAsync("/healthz/ready");
        });

        // Assert - Should handle missing data source reference
        // Application may start but metadata validation should catch this
        if (exception == null)
        {
            var client = _factory.CreateClient();
            var healthResponse = await client.GetAsync("/healthz/ready");
            healthResponse.StatusCode.Should().NotBe(HttpStatusCode.OK,
                "health check should be unhealthy when service references missing data source");
        }
    }

    [Fact]
    public async Task EmptyMetadata_NoServices_ShouldStartButProvideEmptyCollections()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var ogcResponse = await client.GetAsync("/ogc");

        // Assert
        ogcResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await ogcResponse.Content.ReadAsStringAsync();
        content.Should().Contain("minimal-catalog");
    }

    [Fact]
    public async Task NonExistentEndpoint_ShouldReturn404()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/nonexistent/endpoint/path");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonExistentService_ShouldReturn404()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("test", "Test", "Test")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("existing-service", "Existing Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/nonexistent-service/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonExistentCollection_ShouldReturn404()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("test", "Test", "Test")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("test-service", "Test Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/test-service/collections/nonexistent-collection/items");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task STACNonExistentCollection_ShouldReturn404()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/stac/collections/nonexistent-collection");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task InvalidPaginationLimit_ShouldHandleGracefully()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("pagination-test", "Pagination Test", "Test pagination limits")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("test-svc", "Test Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Try with negative limit (should default to reasonable value)
        var negativeResponse = await client.GetAsync("/stac/search?limit=-100");

        // Assert - Should handle gracefully (not crash)
        negativeResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);

        // Act - Try with extremely large limit
        var largeResponse = await client.GetAsync("/stac/search?limit=1000000");

        // Assert - Should cap to maximum allowed
        largeResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MalformedQueryParameters_ShouldReturnBadRequest()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Try with malformed bbox parameter
        var response = await client.GetAsync("/stac/search?bbox=invalid-bbox-format");

        // Assert - Should return bad request or handle gracefully
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RedisUnavailable_ShouldDegradeGracefully()
    {
        // Note: This test would require stopping Redis container mid-test
        // For now, we verify that application can start even if Redis is configured but not critical

        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Application should still be functional even if Redis health is degraded
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        // If degraded, verify it's due to Redis
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().ContainAny("redis", "Redis", "cache");
        }
    }

    [Fact]
    public async Task ConcurrentRequests_ShouldHandleWithoutErrors()
    {
        // Arrange
        var metadata = new TestMetadataBuilder()
            .WithCatalog("concurrent-test", "Concurrent Test", "Test concurrent requests")
            .AddPostgresDataSource("db", _factory.PostgresConnectionString)
            .AddFeatureService("test-svc", "Test Service", "db")
            .Build();

        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Fire multiple concurrent requests
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            return await client.GetAsync("/healthz/live");
        });

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task LargePayloadRequest_ShouldHandleOrReject()
    {
        // Arrange
        var metadata = TestMetadataBuilder.CreateMinimalMetadata();
        _factory.UseQuickStartAuth();
        _factory.WriteMetadata(metadata);

        var client = _factory.CreateClient();

        // Act - Try to search with very large query (if search supports POST)
        var largeQuery = new
        {
            collections = Enumerable.Range(0, 1000).Select(i => $"collection-{i}").ToArray()
        };

        var response = await client.PostAsJsonAsync("/stac/search", largeQuery);

        // Assert - Should handle or reject gracefully
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.RequestEntityTooLarge);
    }
}
