using Xunit;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Honua.Cli.AI.Tests.Services.VectorSearch;

[Trait("Category", "Unit")]
public sealed class CachedDeploymentPatternKnowledgeStoreTests
{
    [Fact]
    public async Task SearchPatternsAsync_FirstCall_HitsDatabaseAndCaches()
    {
        // Arrange
        var mockInner = new Mock<IDeploymentPatternKnowledgeStore>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var results = new List<PatternSearchResult>
        {
            new() { Id = "pattern-1", PatternName = "Test Pattern", Score = 0.9 }
        };

        mockInner
            .Setup(x => x.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var cachedStore = new CachedDeploymentPatternKnowledgeStore(
            mockInner.Object,
            cache,
            NullLogger<CachedDeploymentPatternKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws"
        };

        // Act
        var result1 = await cachedStore.SearchPatternsAsync(requirements);

        // Assert
        result1.Should().BeEquivalentTo(results);
        mockInner.Verify(
            x => x.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchPatternsAsync_SecondCall_ReturnsCachedResults()
    {
        // Arrange
        var mockInner = new Mock<IDeploymentPatternKnowledgeStore>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var results = new List<PatternSearchResult>
        {
            new() { Id = "pattern-1", PatternName = "Cached Pattern", Score = 0.85 }
        };

        mockInner
            .Setup(x => x.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var cachedStore = new CachedDeploymentPatternKnowledgeStore(
            mockInner.Object,
            cache,
            NullLogger<CachedDeploymentPatternKnowledgeStore>.Instance);

        var requirements = new DeploymentRequirements
        {
            DataVolumeGb = 500,
            ConcurrentUsers = 1000,
            CloudProvider = "aws"
        };

        // Act
        var result1 = await cachedStore.SearchPatternsAsync(requirements);
        var result2 = await cachedStore.SearchPatternsAsync(requirements);

        // Assert
        result1.Should().BeEquivalentTo(results);
        result2.Should().BeEquivalentTo(results);

        // Inner store should only be called once (second call uses cache)
        mockInner.Verify(
            x => x.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchPatternsAsync_DifferentRequirements_HitsDatabaseSeparately()
    {
        // Arrange
        var mockInner = new Mock<IDeploymentPatternKnowledgeStore>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        mockInner
            .Setup(x => x.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PatternSearchResult>());

        var cachedStore = new CachedDeploymentPatternKnowledgeStore(
            mockInner.Object,
            cache,
            NullLogger<CachedDeploymentPatternKnowledgeStore>.Instance);

        var requirements1 = new DeploymentRequirements { DataVolumeGb = 500, ConcurrentUsers = 1000 };
        var requirements2 = new DeploymentRequirements { DataVolumeGb = 1000, ConcurrentUsers = 2000 };

        // Act
        await cachedStore.SearchPatternsAsync(requirements1);
        await cachedStore.SearchPatternsAsync(requirements2);

        // Assert: Different requirements = different cache entries
        mockInner.Verify(
            x => x.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task IndexApprovedPatternAsync_DelegatesToInner()
    {
        // Arrange
        var mockInner = new Mock<IDeploymentPatternKnowledgeStore>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        mockInner
            .Setup(x => x.IndexApprovedPatternAsync(It.IsAny<DeploymentPattern>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cachedStore = new CachedDeploymentPatternKnowledgeStore(
            mockInner.Object,
            cache,
            NullLogger<CachedDeploymentPatternKnowledgeStore>.Instance);

        var pattern = new DeploymentPattern
        {
            Id = "new-pattern",
            Name = "New Pattern",
            CloudProvider = "azure"
        };

        // Act
        await cachedStore.IndexApprovedPatternAsync(pattern);

        // Assert
        mockInner.Verify(
            x => x.IndexApprovedPatternAsync(pattern, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
