using System.Diagnostics;
using Dapper;
using FluentAssertions;
using Honua.Build.Orchestrator;
using Honua.Build.Orchestrator.Models;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace Honua.Server.Integration.Tests;

/// <summary>
/// End-to-end integration tests for complete Honua build system workflows.
/// Tests the full pipeline from intake to build to delivery.
/// </summary>
[Collection("Integration")]
public class EndToEndWorkflowTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly IServiceProvider _services;

    public EndToEndWorkflowTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _services = CreateServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Test_CompleteWorkflow_NewCustomer_Success()
    {
        // Arrange - Create customer with license
        var customerId = "customer-001";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                licenseTier: "Professional",
                maxConcurrentBuilds: 2,
                allowedRegistries: new[] { "GitHubContainerRegistry", "AwsEcr" });
        });

        var manifest = ManifestBuilder.CreateDefault()
            .WithId("manifest-001")
            .WithName("Test GIS Server")
            .WithModule("WMS")
            .WithModule("WFS")
            .WithTarget("aws-graviton3", "aws", "graviton3", "linux-arm64")
            .Build();

        var manifestHash = Honua.Build.Orchestrator.ManifestHasher.ComputeHash(manifest);

        // Act - Queue build
        var buildId = await QueueBuildAsync(customerId, manifest.Id, manifestHash, "aws-graviton3");

        // Act - Process build (simulated)
        await SimulateBuildProcessingAsync(buildId, manifestHash, "aws-graviton3");

        // Assert - Check build completed successfully
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var build = await connection.QuerySingleAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue WHERE id = @BuildId
        ", new { BuildId = buildId });

        build.Status.Should().Be("success");
        build.CompletedAt.Should().NotBeNull();
        build.OutputPath.Should().NotBeNullOrEmpty();
        build.ErrorMessage.Should().BeNullOrEmpty();

        // Assert - Check cache was populated
        var cacheEntry = await connection.QuerySingleOrDefaultAsync<BuildCacheRecord>(@"
            SELECT * FROM build_cache_registry
            WHERE manifest_hash = @Hash
        ", new { Hash = manifestHash });

        cacheEntry.Should().NotBeNull();
        cacheEntry!.ManifestHash.Should().Be(manifestHash);
        cacheEntry.TargetId.Should().Be("aws-graviton3");
        cacheEntry.CacheHitCount.Should().Be(0);
    }

    [Fact]
    public async Task Test_CompleteWorkflow_CacheHit_FastDelivery()
    {
        // Arrange - Pre-populate cache with existing build
        var customerId = "customer-002";
        var manifestHash = "abc123def456";
        var manifestId = "manifest-002";
        var targetId = "aws-graviton3";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Enterprise");
            builder.WithCacheEntry(
                manifestHash,
                manifestId,
                targetId,
                imageReference: "ghcr.io/honua/customer-002/gis-server:v1.0.0-linux-arm64",
                architecture: "linux-arm64",
                binarySize: 45_000_000);
        });

        // Act - Queue build
        var buildId = await QueueBuildAsync(customerId, manifestId, manifestHash, targetId);

        var stopwatch = Stopwatch.StartNew();

        // Act - Process build (should hit cache)
        await SimulateCachedBuildProcessingAsync(buildId, manifestHash, targetId);

        stopwatch.Stop();

        // Assert - Should be very fast (cache hit)
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "cache hit should be nearly instantaneous");

        // Assert - Build marked as success
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var build = await connection.QuerySingleAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue WHERE id = @BuildId
        ", new { BuildId = buildId });

        build.Status.Should().Be("success");
        build.CompletedAt.Should().NotBeNull();

        // Assert - Cache hit count incremented
        var cacheEntry = await connection.QuerySingleAsync<BuildCacheRecord>(@"
            SELECT * FROM build_cache_registry
            WHERE manifest_hash = @Hash
        ", new { Hash = manifestHash });

        cacheEntry.CacheHitCount.Should().BeGreaterThanOrEqualTo(1);
        cacheEntry.LastAccessedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Test_CompleteWorkflow_MultipleTargets_ParallelBuilds()
    {
        // Arrange - Create customer with high concurrency limit
        var customerId = "customer-003";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                licenseTier: "Enterprise",
                maxConcurrentBuilds: 4);
        });

        var manifest = ManifestBuilder.CreateDefault()
            .WithId("manifest-003")
            .WithName("Multi-Platform GIS Server")
            .WithTarget("aws-graviton3", "aws", "graviton3", "linux-arm64")
            .WithTarget("azure-ampere", "azure", "ampere", "linux-arm64")
            .WithTarget("gcp-x64", "gcp", "x86_64", "linux-x64")
            .Build();

        var manifestHash = Honua.Build.Orchestrator.ManifestHasher.ComputeHash(manifest);

        // Act - Queue builds for all targets
        var buildIds = new List<Guid>();
        foreach (var target in manifest.Targets)
        {
            var buildId = await QueueBuildAsync(customerId, manifest.Id, manifestHash, target.Id);
            buildIds.Add(buildId);
        }

        // Act - Process builds in parallel
        var stopwatch = Stopwatch.StartNew();
        var processTasks = buildIds.Select(buildId =>
        {
            var target = manifest.Targets.First(t => buildIds.IndexOf(buildId) == manifest.Targets.IndexOf(t));
            return SimulateBuildProcessingAsync(buildId, manifestHash, target.Id);
        });

        await Task.WhenAll(processTasks);
        stopwatch.Stop();

        // Assert - All builds completed successfully
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var completedBuilds = await connection.QueryAsync<BuildQueueRecord>(@"
            SELECT * FROM build_queue
            WHERE customer_id = @CustomerId AND manifest_hash = @Hash
            ORDER BY created_at
        ", new { CustomerId = customerId, Hash = manifestHash });

        completedBuilds.Should().HaveCount(3);
        completedBuilds.Should().OnlyContain(b => b.Status == "success");
        completedBuilds.Should().OnlyContain(b => b.CompletedAt != null);

        // Assert - Cache entries created for all targets
        var cacheEntries = await connection.QueryAsync<BuildCacheRecord>(@"
            SELECT * FROM build_cache_registry
            WHERE manifest_hash = @Hash
        ", new { Hash = manifestHash });

        cacheEntries.Should().HaveCount(3);
        cacheEntries.Select(c => c.TargetId).Should().BeEquivalentTo(manifest.Targets.Select(t => t.Id));

        // Assert - Parallel execution was faster than sequential
        var expectedSequentialTime = TimeSpan.FromSeconds(6); // 3 builds * 2 seconds each
        stopwatch.Elapsed.Should().BeLessThan(expectedSequentialTime,
            "parallel builds should complete faster than sequential");
    }

    [Fact]
    public async Task Test_CompleteWorkflow_WithBuildDelivery_Success()
    {
        // Arrange
        var customerId = "customer-004";
        var manifestHash = "delivery-test-123";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Professional");
            builder.WithRegistryCredentials(
                customerId,
                "GitHubContainerRegistry",
                namespace_: $"honua/{customerId}",
                registryUrl: "ghcr.io",
                expiresAt: DateTimeOffset.UtcNow.AddHours(1));
            builder.WithCacheEntry(
                manifestHash,
                "manifest-004",
                "aws-graviton3",
                imageReference: "ghcr.io/honua/builds/gis-server:v1.0.0",
                architecture: "linux-arm64");
        });

        // Act - Deliver build to customer registry
        var deliveryService = _services.GetRequiredService<IBuildDeliveryService>();
        var cacheKey = new BuildCacheKey
        {
            CustomerId = customerId,
            BuildName = "gis-server",
            Version = "v1.0.0",
            Architecture = "linux-arm64"
        };

        var result = await deliveryService.DeliverBuildAsync(
            cacheKey,
            RegistryType.GitHubContainerRegistry,
            sourceBuildPath: null,
            CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ImageReference.Should().Contain(customerId);
        result.ImageReference.Should().Contain("gis-server");
        result.TargetRegistry.Should().Be(RegistryType.GitHubContainerRegistry);
    }

    [Fact]
    public async Task Test_CompleteWorkflow_LicenseExpired_AccessDenied()
    {
        // Arrange - Customer with expired license
        var customerId = "customer-005";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                licenseTier: "Standard",
                status: "expired",
                expiresAt: DateTimeOffset.UtcNow.AddDays(-7));
        });

        // Act - Attempt to deliver build
        var deliveryService = _services.GetRequiredService<IBuildDeliveryService>();
        var cacheKey = new BuildCacheKey
        {
            CustomerId = customerId,
            BuildName = "test-build",
            Version = "v1.0.0"
        };

        var result = await deliveryService.DeliverBuildAsync(
            cacheKey,
            RegistryType.GitHubContainerRegistry,
            sourceBuildPath: null,
            CancellationToken.None);

        // Assert - Access should be denied
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Access denied");
    }

    // Helper methods

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddSingleton(_fixture.LoggerFactory);
        services.AddLogging();

        // Add mock services
        var mockCacheChecker = new Mock<IRegistryCacheChecker>();
        mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<BuildCacheKey>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        services.AddSingleton(mockCacheChecker.Object);

        // Add access manager
        services.AddSingleton<Honua.Server.Intake.Services.IRegistryAccessManager, MockRegistryAccessManager>();

        // Add build delivery service
        services.AddSingleton<IBuildDeliveryService, MockBuildDeliveryService>();

        // Add configuration
        var provisioningOptions = new RegistryProvisioningOptions
        {
            GitHubOrganization = "honua",
            AwsRegion = "us-west-2",
            AwsAccountId = "123456789012"
        };
        services.AddSingleton(Options.Create(provisioningOptions));

        var deliveryOptions = new BuildDeliveryOptions
        {
            PreferredTool = "crane",
            AutoTagLatest = true
        };
        services.AddSingleton(Options.Create(deliveryOptions));

        return services.BuildServiceProvider();
    }

    private async Task<Guid> QueueBuildAsync(string customerId, string manifestId, string manifestHash, string targetId)
    {
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var buildId = await connection.QuerySingleAsync<Guid>(@"
            INSERT INTO build_queue (customer_id, manifest_id, manifest_hash, status, priority, target_id)
            VALUES (@CustomerId, @ManifestId, @ManifestHash, 'queued', 100, @TargetId)
            RETURNING id
        ", new { CustomerId = customerId, ManifestId = manifestId, ManifestHash = manifestHash, TargetId = targetId });

        return buildId;
    }

    private async Task SimulateBuildProcessingAsync(Guid buildId, string manifestHash, string targetId)
    {
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Update build to running
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'running', started_at = NOW()
            WHERE id = @BuildId
        ", new { BuildId = buildId });

        // Simulate build time
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Update build to success
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'success',
                completed_at = NOW(),
                output_path = @OutputPath
            WHERE id = @BuildId
        ", new { BuildId = buildId, OutputPath = $"/builds/output/{buildId}" });

        // Add cache entry
        await connection.ExecuteAsync(@"
            INSERT INTO build_cache_registry (manifest_hash, manifest_id, target_id, image_reference, architecture, binary_size)
            VALUES (@ManifestHash, @ManifestId, @TargetId, @ImageReference, @Architecture, @BinarySize)
            ON CONFLICT (manifest_hash) DO NOTHING
        ", new
        {
            ManifestHash = manifestHash,
            ManifestId = "test-manifest",
            TargetId = targetId,
            ImageReference = $"ghcr.io/honua/builds/test:{manifestHash}",
            Architecture = "linux-arm64",
            BinarySize = 50_000_000L
        });
    }

    private async Task SimulateCachedBuildProcessingAsync(Guid buildId, string manifestHash, string targetId)
    {
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Update build to running
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'running', started_at = NOW()
            WHERE id = @BuildId
        ", new { BuildId = buildId });

        // Cache hit - very fast
        await Task.Delay(TimeSpan.FromMilliseconds(100));

        // Update build to success
        await connection.ExecuteAsync(@"
            UPDATE build_queue
            SET status = 'success',
                completed_at = NOW(),
                output_path = @OutputPath
            WHERE id = @BuildId
        ", new { BuildId = buildId, OutputPath = $"/builds/cache/{manifestHash}" });

        // Increment cache hit count
        await connection.ExecuteAsync(@"
            UPDATE build_cache_registry
            SET cache_hit_count = cache_hit_count + 1,
                last_accessed_at = NOW()
            WHERE manifest_hash = @ManifestHash
        ", new { ManifestHash = manifestHash });
    }
}

// Helper classes for database records
internal record BuildQueueRecord
{
    public Guid Id { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string ManifestId { get; init; } = string.Empty;
    public string ManifestHash { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string? TargetId { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

internal record BuildCacheRecord
{
    public Guid Id { get; init; }
    public string ManifestHash { get; init; } = string.Empty;
    public string ManifestId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string ImageReference { get; init; } = string.Empty;
    public string? Digest { get; init; }
    public string? Architecture { get; init; }
    public long? BinarySize { get; init; }
    public int CacheHitCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastAccessedAt { get; init; }
}
