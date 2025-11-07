using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Core.Deployment;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Deployment;

/// <summary>
/// Tests for deployment state machine transitions and state management
/// </summary>
[Trait("Category", "Unit")]
[Trait("Speed", "Fast")]
public class DeploymentStateMachineTests
{
    [Fact]
    public void DeploymentState_HasExpectedValues()
    {
        // Assert - Verify all expected states exist
        var states = Enum.GetValues<DeploymentState>();

        Assert.Contains(DeploymentState.Pending, states);
        Assert.Contains(DeploymentState.Validating, states);
        Assert.Contains(DeploymentState.Planning, states);
        Assert.Contains(DeploymentState.AwaitingApproval, states);
        Assert.Contains(DeploymentState.BackingUp, states);
        Assert.Contains(DeploymentState.Applying, states);
        Assert.Contains(DeploymentState.PostValidating, states);
        Assert.Contains(DeploymentState.Completed, states);
        Assert.Contains(DeploymentState.Failed, states);
        Assert.Contains(DeploymentState.RollingBack, states);
        Assert.Contains(DeploymentState.RolledBack, states);
    }

    [Theory]
    [InlineData(DeploymentState.Pending, DeploymentState.Validating, true)]
    [InlineData(DeploymentState.Validating, DeploymentState.Planning, true)]
    [InlineData(DeploymentState.Planning, DeploymentState.AwaitingApproval, true)]
    [InlineData(DeploymentState.Planning, DeploymentState.BackingUp, true)]
    [InlineData(DeploymentState.AwaitingApproval, DeploymentState.BackingUp, true)]
    [InlineData(DeploymentState.BackingUp, DeploymentState.Applying, true)]
    [InlineData(DeploymentState.Applying, DeploymentState.PostValidating, true)]
    [InlineData(DeploymentState.PostValidating, DeploymentState.Completed, true)]
    [InlineData(DeploymentState.Applying, DeploymentState.Failed, true)]
    [InlineData(DeploymentState.Failed, DeploymentState.RollingBack, true)]
    [InlineData(DeploymentState.RollingBack, DeploymentState.RolledBack, true)]
    [InlineData(DeploymentState.Completed, DeploymentState.Pending, false)]
    [InlineData(DeploymentState.RolledBack, DeploymentState.Applying, false)]
    public void StateTransition_WhenValid_ShouldBeAllowed(
        DeploymentState from,
        DeploymentState to,
        bool isValid)
    {
        // Arrange & Act
        var result = IsValidTransition(from, to);

        // Assert
        Assert.Equal(isValid, result);
    }

    [Theory]
    [InlineData(DeploymentState.Completed)]
    [InlineData(DeploymentState.Failed)]
    [InlineData(DeploymentState.RolledBack)]
    public void TerminalStates_ShouldNotAllowTransitionsOut(DeploymentState terminalState)
    {
        // Arrange
        var nonTerminalStates = new[]
        {
            DeploymentState.Pending,
            DeploymentState.Validating,
            DeploymentState.Planning,
            DeploymentState.Applying
        };

        // Act & Assert
        foreach (var targetState in nonTerminalStates)
        {
            // Terminal states should not transition to non-terminal states
            // Exception: Failed can transition to RollingBack
            if (terminalState == DeploymentState.Failed && targetState == DeploymentState.RollingBack)
            {
                continue;
            }

            var result = IsValidTransition(terminalState, targetState);
            Assert.False(result,
                $"Terminal state {terminalState} should not transition to {targetState}");
        }
    }

    [Fact]
    public void StateHistory_WhenTransitionsOccur_ShouldTrackCorrectly()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            State = DeploymentState.Pending,
            StartedAt = DateTime.UtcNow
        };

        // Initial state should be in history
        deployment.StateHistory.Add(new StateTransition
        {
            To = DeploymentState.Pending,
            Timestamp = DateTime.UtcNow,
            Message = "Deployment created"
        });

        // Act - Simulate state transitions
        TransitionDeployment(deployment, DeploymentState.Validating, "Starting validation");
        TransitionDeployment(deployment, DeploymentState.Planning, "Creating deployment plan");
        TransitionDeployment(deployment, DeploymentState.Applying, "Applying changes");
        TransitionDeployment(deployment, DeploymentState.Completed, "Deployment successful");

        // Assert
        Assert.Equal(5, deployment.StateHistory.Count);
        Assert.Equal(DeploymentState.Pending, deployment.StateHistory[0].To);
        Assert.Equal(DeploymentState.Validating, deployment.StateHistory[1].To);
        Assert.Equal(DeploymentState.Planning, deployment.StateHistory[2].To);
        Assert.Equal(DeploymentState.Applying, deployment.StateHistory[3].To);
        Assert.Equal(DeploymentState.Completed, deployment.StateHistory[4].To);

        // Verify From fields are set correctly
        Assert.Null(deployment.StateHistory[0].From);
        Assert.Equal(DeploymentState.Pending, deployment.StateHistory[1].From);
        Assert.Equal(DeploymentState.Validating, deployment.StateHistory[2].From);
        Assert.Equal(DeploymentState.Planning, deployment.StateHistory[3].From);
        Assert.Equal(DeploymentState.Applying, deployment.StateHistory[4].From);
    }

    [Fact]
    public void Duration_WhenDeploymentCompleted_ShouldCalculateCorrectly()
    {
        // Arrange
        var startTime = DateTime.UtcNow;
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            StartedAt = startTime
        };

        // Act
        var endTime = startTime.AddMinutes(5);
        deployment.CompletedAt = endTime;

        // Assert
        Assert.NotNull(deployment.Duration);
        Assert.Equal(TimeSpan.FromMinutes(5), deployment.Duration.Value);
    }

    [Fact]
    public void Duration_WhenDeploymentNotCompleted_ShouldBeNull()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            StartedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Null(deployment.Duration);
    }

    [Theory]
    [InlineData(DeploymentHealth.Unknown)]
    [InlineData(DeploymentHealth.Healthy)]
    [InlineData(DeploymentHealth.Progressing)]
    [InlineData(DeploymentHealth.Degraded)]
    [InlineData(DeploymentHealth.Unhealthy)]
    public void HealthStatus_WhenChanged_ShouldUpdateCorrectly(DeploymentHealth health)
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            Health = DeploymentHealth.Unknown
        };

        // Act
        deployment.Health = health;

        // Assert
        Assert.Equal(health, deployment.Health);
    }

    [Theory]
    [InlineData(SyncStatus.Unknown)]
    [InlineData(SyncStatus.Synced)]
    [InlineData(SyncStatus.OutOfSync)]
    [InlineData(SyncStatus.Syncing)]
    public void SyncStatus_WhenChanged_ShouldUpdateCorrectly(SyncStatus syncStatus)
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            SyncStatus = SyncStatus.Unknown
        };

        // Act
        deployment.SyncStatus = syncStatus;

        // Assert
        Assert.Equal(syncStatus, deployment.SyncStatus);
    }

    [Fact]
    public void StateTransition_WhenMessageProvided_ShouldStoreMessage()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            State = DeploymentState.Pending
        };

        var expectedMessage = "Starting validation of configuration files";

        // Act
        TransitionDeployment(deployment, DeploymentState.Validating, expectedMessage);

        // Assert
        var lastTransition = deployment.StateHistory.Last();
        Assert.Equal(expectedMessage, lastTransition.Message);
    }

    [Fact]
    public async Task StateTransition_WhenTimestampRecorded_ShouldBeInOrder()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            State = DeploymentState.Pending
        };

        deployment.StateHistory.Add(new StateTransition
        {
            To = DeploymentState.Pending,
            Timestamp = DateTime.UtcNow
        });

        // Act - Add transitions with small delays
        TransitionDeployment(deployment, DeploymentState.Validating, "Validating");
        await Task.Delay(10);
        TransitionDeployment(deployment, DeploymentState.Planning, "Planning");
        await Task.Delay(10);
        TransitionDeployment(deployment, DeploymentState.Applying, "Applying");

        // Assert - Timestamps should be in chronological order
        for (int i = 1; i < deployment.StateHistory.Count; i++)
        {
            Assert.True(
                deployment.StateHistory[i].Timestamp >= deployment.StateHistory[i - 1].Timestamp,
                "Timestamps should be in chronological order");
        }
    }

    [Fact]
    public void DeploymentPlan_WhenSet_ShouldContainResourceChanges()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123"
        };

        var plan = new DeploymentPlan
        {
            Added = new List<ResourceChange>
            {
                new ResourceChange
                {
                    Type = "layer",
                    Name = "new-layer",
                    Path = "environments/production/new-layer.yaml",
                    IsBreaking = false
                }
            },
            Modified = new List<ResourceChange>
            {
                new ResourceChange
                {
                    Type = "datasource",
                    Name = "postgres-main",
                    Path = "environments/production/datasources.json",
                    Diff = "Connection string updated",
                    IsBreaking = true
                }
            },
            Removed = new List<ResourceChange>
            {
                new ResourceChange
                {
                    Type = "layer",
                    Name = "old-layer",
                    Path = "environments/production/old-layer.yaml",
                    IsBreaking = false
                }
            },
            HasBreakingChanges = true,
            RiskLevel = RiskLevel.High
        };

        // Act
        deployment.Plan = plan;

        // Assert
        Assert.NotNull(deployment.Plan);
        Assert.Single(deployment.Plan.Added);
        Assert.Single(deployment.Plan.Modified);
        Assert.Single(deployment.Plan.Removed);
        Assert.True(deployment.Plan.HasBreakingChanges);
        Assert.Equal(RiskLevel.High, deployment.Plan.RiskLevel);
    }

    [Fact]
    public void ValidationResults_WhenAdded_ShouldTrackMultipleResults()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123"
        };

        // Act
        deployment.ValidationResults.Add(new ValidationResult
        {
            Type = "syntax",
            Success = true,
            Message = "Syntax validation passed",
            Timestamp = DateTime.UtcNow
        });

        deployment.ValidationResults.Add(new ValidationResult
        {
            Type = "policy",
            Success = false,
            Message = "Policy violation: Missing required field",
            Timestamp = DateTime.UtcNow
        });

        deployment.ValidationResults.Add(new ValidationResult
        {
            Type = "health",
            Success = true,
            Message = "Health check passed",
            Timestamp = DateTime.UtcNow
        });

        // Assert
        Assert.Equal(3, deployment.ValidationResults.Count);
        Assert.Equal(2, deployment.ValidationResults.Count(v => v.Success));
        Assert.Single(deployment.ValidationResults, v => !v.Success);
    }

    [Fact]
    public void Metadata_WhenSet_ShouldStoreKeyValuePairs()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123"
        };

        // Act
        deployment.Metadata["approver"] = "john.doe@example.com";
        deployment.Metadata["ticket"] = "JIRA-12345";
        deployment.Metadata["region"] = "us-west-2";

        // Assert
        Assert.Equal(3, deployment.Metadata.Count);
        Assert.Equal("john.doe@example.com", deployment.Metadata["approver"]);
        Assert.Equal("JIRA-12345", deployment.Metadata["ticket"]);
        Assert.Equal("us-west-2", deployment.Metadata["region"]);
    }

    [Fact]
    public void AutoRollback_WhenEnabled_ShouldBeTrue()
    {
        // Arrange & Act
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            AutoRollback = true
        };

        // Assert
        Assert.True(deployment.AutoRollback);
    }

    [Fact]
    public void AutoRollback_WhenDisabled_ShouldBeFalse()
    {
        // Arrange & Act
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            AutoRollback = false
        };

        // Assert
        Assert.False(deployment.AutoRollback);
    }

    [Fact]
    public void ErrorMessage_WhenDeploymentFails_ShouldBeSet()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            State = DeploymentState.Applying
        };

        var errorMessage = "Database migration failed: Connection timeout";

        // Act
        deployment.State = DeploymentState.Failed;
        deployment.ErrorMessage = errorMessage;

        // Assert
        Assert.Equal(DeploymentState.Failed, deployment.State);
        Assert.Equal(errorMessage, deployment.ErrorMessage);
    }

    [Fact]
    public void BackupId_WhenSet_ShouldBeStored()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123"
        };

        var backupId = "backup-20250123-120000";

        // Act
        deployment.BackupId = backupId;

        // Assert
        Assert.Equal(backupId, deployment.BackupId);
    }

    [Fact]
    public void CompleteDeploymentLifecycle_ShouldTransitionThroughAllStates()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            State = DeploymentState.Pending,
            StartedAt = DateTime.UtcNow,
            Health = DeploymentHealth.Unknown,
            SyncStatus = SyncStatus.OutOfSync
        };

        deployment.StateHistory.Add(new StateTransition
        {
            To = DeploymentState.Pending,
            Timestamp = DateTime.UtcNow,
            Message = "Deployment created"
        });

        // Act - Simulate full deployment lifecycle
        TransitionDeployment(deployment, DeploymentState.Validating, "Validating configuration");
        deployment.Health = DeploymentHealth.Progressing;

        TransitionDeployment(deployment, DeploymentState.Planning, "Creating deployment plan");
        deployment.Plan = new DeploymentPlan
        {
            Added = new List<ResourceChange> { new ResourceChange { Type = "layer", Name = "test" } }
        };

        TransitionDeployment(deployment, DeploymentState.AwaitingApproval, "Waiting for approval");

        TransitionDeployment(deployment, DeploymentState.BackingUp, "Creating backup");
        deployment.BackupId = "backup-123";

        TransitionDeployment(deployment, DeploymentState.Applying, "Applying changes");
        deployment.SyncStatus = SyncStatus.Syncing;

        TransitionDeployment(deployment, DeploymentState.PostValidating, "Running post-deployment validation");

        TransitionDeployment(deployment, DeploymentState.Completed, "Deployment successful");
        deployment.CompletedAt = DateTime.UtcNow;
        deployment.Health = DeploymentHealth.Healthy;
        deployment.SyncStatus = SyncStatus.Synced;

        // Assert
        Assert.Equal(DeploymentState.Completed, deployment.State);
        Assert.Equal(DeploymentHealth.Healthy, deployment.Health);
        Assert.Equal(SyncStatus.Synced, deployment.SyncStatus);
        Assert.NotNull(deployment.CompletedAt);
        Assert.NotNull(deployment.Duration);
        Assert.Equal(8, deployment.StateHistory.Count);
        Assert.NotNull(deployment.BackupId);
        Assert.NotNull(deployment.Plan);
    }

    [Fact]
    public void FailedDeploymentWithRollback_ShouldTransitionCorrectly()
    {
        // Arrange
        var deployment = new Server.Core.Deployment.Deployment
        {
            Id = "test-deploy",
            Environment = "production",
            Commit = "abc123",
            State = DeploymentState.Pending,
            StartedAt = DateTime.UtcNow,
            AutoRollback = true
        };

        deployment.StateHistory.Add(new StateTransition
        {
            To = DeploymentState.Pending,
            Timestamp = DateTime.UtcNow
        });

        // Act - Simulate failed deployment with rollback
        TransitionDeployment(deployment, DeploymentState.Validating, "Validating");
        TransitionDeployment(deployment, DeploymentState.Planning, "Planning");
        TransitionDeployment(deployment, DeploymentState.BackingUp, "Creating backup");
        deployment.BackupId = "backup-123";
        TransitionDeployment(deployment, DeploymentState.Applying, "Applying changes");
        TransitionDeployment(deployment, DeploymentState.Failed, "Database migration failed");
        deployment.ErrorMessage = "Database migration failed: Connection timeout";
        deployment.Health = DeploymentHealth.Unhealthy;

        TransitionDeployment(deployment, DeploymentState.RollingBack, "Rolling back changes");
        TransitionDeployment(deployment, DeploymentState.RolledBack, "Rollback completed");
        deployment.CompletedAt = DateTime.UtcNow;

        // Assert
        Assert.Equal(DeploymentState.RolledBack, deployment.State);
        Assert.Equal(DeploymentHealth.Unhealthy, deployment.Health);
        Assert.NotNull(deployment.ErrorMessage);
        Assert.NotNull(deployment.BackupId);
        Assert.NotNull(deployment.CompletedAt);
        Assert.Equal(8, deployment.StateHistory.Count);
    }

    // Helper methods

    private bool IsValidTransition(DeploymentState from, DeploymentState to)
    {
        // Define valid state transitions based on the deployment state machine
        var validTransitions = new Dictionary<DeploymentState, List<DeploymentState>>
        {
            { DeploymentState.Pending, new List<DeploymentState>
                { DeploymentState.Validating, DeploymentState.Failed } },
            { DeploymentState.Validating, new List<DeploymentState>
                { DeploymentState.Planning, DeploymentState.Failed } },
            { DeploymentState.Planning, new List<DeploymentState>
                { DeploymentState.AwaitingApproval, DeploymentState.BackingUp, DeploymentState.Failed } },
            { DeploymentState.AwaitingApproval, new List<DeploymentState>
                { DeploymentState.BackingUp, DeploymentState.Failed } },
            { DeploymentState.BackingUp, new List<DeploymentState>
                { DeploymentState.Applying, DeploymentState.Failed } },
            { DeploymentState.Applying, new List<DeploymentState>
                { DeploymentState.PostValidating, DeploymentState.Completed, DeploymentState.Failed } },
            { DeploymentState.PostValidating, new List<DeploymentState>
                { DeploymentState.Completed, DeploymentState.Failed } },
            { DeploymentState.Failed, new List<DeploymentState>
                { DeploymentState.RollingBack } },
            { DeploymentState.RollingBack, new List<DeploymentState>
                { DeploymentState.RolledBack, DeploymentState.Failed } },
            { DeploymentState.Completed, new List<DeploymentState>() }, // Terminal
            { DeploymentState.RolledBack, new List<DeploymentState>() }  // Terminal
        };

        return validTransitions.ContainsKey(from) && validTransitions[from].Contains(to);
    }

    private void TransitionDeployment(
        Server.Core.Deployment.Deployment deployment,
        DeploymentState newState,
        string? message = null)
    {
        var previousState = deployment.State;
        deployment.State = newState;
        deployment.UpdatedAt = DateTime.UtcNow;

        deployment.StateHistory.Add(new StateTransition
        {
            From = previousState,
            To = newState,
            Timestamp = DateTime.UtcNow,
            Message = message
        });
    }
}
