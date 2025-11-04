using Xunit;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Cli.AI.Services;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Honua.Cli.AI.Tests.Services;

[Collection("AITests")]
[Trait("Category", "Integration")]
public sealed class PatternApprovalServiceTests
{
    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        var knowledgeStore = CreateMockKnowledgeStore();

        // Act
        var act = () => new PatternApprovalService(
            null!,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_WithNullKnowledgeStore_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();

        // Act
        var act = () => new PatternApprovalService(
            config,
            null!,
            NullLogger<PatternApprovalService>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("knowledgeStore");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var knowledgeStore = CreateMockKnowledgeStore();

        // Act
        var act = () => new PatternApprovalService(
            config,
            knowledgeStore,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var knowledgeStore = CreateMockKnowledgeStore();

        // Act
        var service = new PatternApprovalService(
            config,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPendingRecommendationsAsync_WithNoConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var knowledgeStore = CreateMockKnowledgeStore();

        var service = new PatternApprovalService(
            config,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetPendingRecommendationsAsync());
    }

    [Fact]
    public async Task ApprovePatternAsync_WithNoConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var knowledgeStore = CreateMockKnowledgeStore();

        var service = new PatternApprovalService(
            config,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApprovePatternAsync(Guid.NewGuid(), "Mike", "Test"));
    }

    [Fact]
    public async Task RejectPatternAsync_WithNoConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new ConfigurationBuilder().Build();
        var knowledgeStore = CreateMockKnowledgeStore();

        var service = new PatternApprovalService(
            config,
            knowledgeStore,
            NullLogger<PatternApprovalService>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RejectPatternAsync(Guid.NewGuid(), "Mike", "Not suitable"));
    }

    private static IDeploymentPatternKnowledgeStore CreateMockKnowledgeStore()
    {
        var mock = new Mock<IDeploymentPatternKnowledgeStore>();
        mock
            .Setup(store => store.IndexApprovedPatternAsync(It.IsAny<DeploymentPattern>(), It.IsAny<CancellationToken>()));

        mock
            .Setup(store => store.SearchPatternsAsync(It.IsAny<DeploymentRequirements>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PatternSearchResult>());

        return mock.Object;
    }

    private static IConfiguration CreateValidConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();
    }
}
