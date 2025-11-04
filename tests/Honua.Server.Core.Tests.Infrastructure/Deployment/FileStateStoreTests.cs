using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Deployment;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Deployment;

[Trait("Category", "Unit")]
public class FileStateStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileStateStore _store;

    public FileStateStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _store = new FileStateStore(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task CreateDeployment_ShouldCreateNewDeployment()
    {
        // Arrange
        var environment = "production";
        var commit = "abc123";
        var initiatedBy = "test-user";

        // Act
        var deployment = await _store.CreateDeploymentAsync(environment, commit, initiatedBy: initiatedBy);

        // Assert
        Assert.NotNull(deployment);
        Assert.Equal(environment, deployment.Environment);
        Assert.Equal(commit, deployment.Commit);
        Assert.Equal(initiatedBy, deployment.InitiatedBy);
        Assert.Equal(DeploymentState.Pending, deployment.State);
        Assert.Equal(DeploymentHealth.Unknown, deployment.Health);
        Assert.Equal(SyncStatus.OutOfSync, deployment.SyncStatus);
        Assert.NotNull(deployment.StateHistory);
        Assert.Single(deployment.StateHistory);
    }

    [Fact]
    public async Task TransitionAsync_ShouldUpdateDeploymentState()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "test-user");

        // Act
        await _store.TransitionAsync(deployment.Id, DeploymentState.Validating, "Starting validation");

        // Assert
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(DeploymentState.Validating, retrieved.State);
        Assert.Equal(2, retrieved.StateHistory.Count);
        Assert.Equal("Starting validation", retrieved.StateHistory[^1].Message);
    }

    [Fact]
    public async Task TransitionAsync_ShouldSetCompletedTimestamp()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "test-user");

        // Act
        await _store.TransitionAsync(deployment.Id, DeploymentState.Completed, "Deployment successful");

        // Assert
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.CompletedAt);
        Assert.True(retrieved.CompletedAt > retrieved.StartedAt);
    }

    [Fact]
    public async Task UpdateHealthAsync_ShouldUpdateHealthStatus()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123");

        // Act
        await _store.UpdateHealthAsync(deployment.Id, DeploymentHealth.Healthy);

        // Assert
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(DeploymentHealth.Healthy, retrieved.Health);
    }

    [Fact]
    public async Task UpdateSyncStatusAsync_ShouldUpdateSyncStatus()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", "test-user");

        // Act
        await _store.UpdateSyncStatusAsync(deployment.Id, SyncStatus.Synced);

        // Assert
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(SyncStatus.Synced, retrieved.SyncStatus);
    }

    [Fact]
    public async Task SetPlanAsync_ShouldSetDeploymentPlan()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "test-user");
        var plan = new DeploymentPlan
        {
            Added = new List<ResourceChange>
            {
                new() { Path = "environments/production/new-layer.yaml", Type = "metadata", Name = "new-layer" }
            }
        };

        // Act
        await _store.SetPlanAsync(deployment.Id, plan);

        // Assert
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Plan);
        Assert.Single(retrieved.Plan.Added);
        Assert.Equal("environments/production/new-layer.yaml", retrieved.Plan.Added[0].Path);
    }

    [Fact]
    public async Task AddValidationResultAsync_ShouldAddValidationResult()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "test-user");
        var validationResult = new ValidationResult
        {
            Type = "pre-deployment",
            Success = false,
            Message = "Missing required field: apiVersion",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _store.AddValidationResultAsync(deployment.Id, validationResult);

        // Assert
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.ValidationResults);
        Assert.Single(retrieved.ValidationResults);
        Assert.False(retrieved.ValidationResults[0].Success);
        Assert.Contains("Missing required field: apiVersion", retrieved.ValidationResults[0].Message);
    }

    [Fact]
    public async Task GetDeploymentHistoryAsync_ShouldReturnDeploymentsForEnvironment()
    {
        // Arrange
        await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "user1");
        await _store.CreateDeploymentAsync("production", "def456", initiatedBy: "user2");
        await _store.CreateDeploymentAsync("development", "ghi789", initiatedBy: "user3");

        // Act
        var productionDeployments = await _store.GetDeploymentHistoryAsync("production", limit: 10);
        var developmentDeployments = await _store.GetDeploymentHistoryAsync("development", limit: 10);

        // Assert
        Assert.Equal(2, productionDeployments.Count);
        Assert.Single(developmentDeployments);
    }

    [Fact]
    public async Task GetDeploymentHistoryAsync_ShouldRespectLimit()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await _store.CreateDeploymentAsync("production", $"commit{i}", initiatedBy: "user");
        }

        // Act
        var deployments = await _store.GetDeploymentHistoryAsync("production", limit: 3);

        // Assert
        Assert.Equal(3, deployments.Count);
    }

    [Fact]
    public async Task GetEnvironmentStateAsync_ShouldReturnCurrentState()
    {
        // Arrange
        var deployment1 = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "user1");
        await _store.TransitionAsync(deployment1.Id, DeploymentState.Completed);

        var deployment2 = await _store.CreateDeploymentAsync("production", "def456", initiatedBy: "user2");
        await _store.TransitionAsync(deployment2.Id, DeploymentState.Applying);

        // Act
        var envState = await _store.GetEnvironmentStateAsync("production");

        // Assert
        Assert.NotNull(envState);
        Assert.Equal("production", envState.Environment);
        Assert.Equal("abc123", envState.DeployedCommit); // deployment1 was completed, deployment2 is still applying
        Assert.Equal(deployment2.Id, envState.CurrentDeployment?.Id);
        Assert.Equal(deployment1.Id, envState.LastSuccessfulDeployment?.Id);
    }

    [Fact]
    public async Task ThreadSafety_MultipleConcurrentOperations()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "test-user");
        var tasks = new List<Task>();

        // Act - Perform multiple concurrent state transitions
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_store.UpdateHealthAsync(deployment.Id, DeploymentHealth.Healthy));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not throw and deployment should still be retrievable
        var retrieved = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(DeploymentHealth.Healthy, retrieved.Health);
    }

    [Fact]
    public async Task GetDeploymentAsync_NonExistentDeployment_ShouldReturnNull()
    {
        // Act
        var deployment = await _store.GetDeploymentAsync("non-existent-id");

        // Assert
        Assert.Null(deployment);
    }

    [Fact]
    public async Task TransitionAsync_NonExistentDeployment_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _store.TransitionAsync("non-existent-id", DeploymentState.Completed));
    }

    [Fact]
    public async Task CompleteDeploymentLifecycle()
    {
        // Arrange
        var deployment = await _store.CreateDeploymentAsync("production", "abc123", initiatedBy: "test-user");

        // Act - Simulate complete deployment lifecycle
        await _store.TransitionAsync(deployment.Id, DeploymentState.Validating, "Validating configuration");
        await _store.TransitionAsync(deployment.Id, DeploymentState.Planning, "Creating deployment plan");

        var plan = new DeploymentPlan
        {
            Modified = new List<ResourceChange>
            {
                new() { Path = "metadata.yaml", Type = "metadata", Name = "metadata" }
            }
        };
        await _store.SetPlanAsync(deployment.Id, plan);

        await _store.TransitionAsync(deployment.Id, DeploymentState.AwaitingApproval, "Requires manual approval");
        // Simulate approval...
        await _store.TransitionAsync(deployment.Id, DeploymentState.Applying, "Applying changes");
        await _store.UpdateHealthAsync(deployment.Id, DeploymentHealth.Progressing);
        await _store.TransitionAsync(deployment.Id, DeploymentState.Completed, "Deployment successful");
        await _store.UpdateHealthAsync(deployment.Id, DeploymentHealth.Healthy);
        await _store.UpdateSyncStatusAsync(deployment.Id, SyncStatus.Synced);

        // Assert
        var final = await _store.GetDeploymentAsync(deployment.Id);
        Assert.NotNull(final);
        Assert.Equal(DeploymentState.Completed, final.State);
        Assert.Equal(DeploymentHealth.Healthy, final.Health);
        Assert.Equal(SyncStatus.Synced, final.SyncStatus);
        Assert.NotNull(final.CompletedAt);
        Assert.True(final.StateHistory.Count >= 6); // All the transitions we made
    }
}
