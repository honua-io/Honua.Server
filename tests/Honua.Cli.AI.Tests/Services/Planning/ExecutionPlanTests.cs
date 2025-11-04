using FluentAssertions;
using Honua.Cli.AI.Secrets;
using Honua.Cli.AI.Services.Planning;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Planning;

[Trait("Category", "Unit")]
public class ExecutionPlanTests
{
    [Fact]
    public void ExecutionPlan_Creation_SetsDefaultValues()
    {
        // Arrange & Act
        var plan = new ExecutionPlan
        {
            Id = "test-plan-1",
            Title = "Test Optimization",
            Type = PlanType.Optimization,
            Steps = new List<PlanStep>(),
            CredentialsRequired = new List<CredentialRequirement>(),
            Risk = new RiskAssessment
            {
                Level = RiskLevel.Low,
                RiskFactors = new List<string>(),
                Mitigations = new List<string>(),
                AllChangesReversible = true,
                RequiresDowntime = false
            }
        };

        // Assert
        plan.Status.Should().Be(PlanStatus.Pending);
        plan.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        plan.ApprovedAt.Should().BeNull();
        plan.AppliedAt.Should().BeNull();
        plan.Environment.Should().Be("development");
    }

    [Theory]
    [InlineData(PlanType.Optimization)]
    [InlineData(PlanType.Deployment)]
    [InlineData(PlanType.Migration)]
    [InlineData(PlanType.Troubleshooting)]
    public void ExecutionPlan_SupportsAllPlanTypes(PlanType planType)
    {
        // Arrange & Act
        var plan = CreateMinimalPlan(planType);

        // Assert
        plan.Type.Should().Be(planType);
    }

    [Fact]
    public void PlanStep_WithDependencies_TracksCorrectly()
    {
        // Arrange
        var step = new PlanStep
        {
            StepNumber = 2,
            Description = "Create index",
            Type = StepType.CreateIndex,
            Operation = "CREATE INDEX idx_geom ON cities(geom)",
            DependsOn = new List<int> { 1 }
        };

        // Assert
        step.DependsOn.Should().Contain(1);
        step.Status.Should().Be(StepStatus.Pending);
        step.IsReversible.Should().BeTrue();
    }

    [Fact]
    public void PlanStep_StatusTransitions_UpdateCorrectly()
    {
        // Arrange
        var step = new PlanStep
        {
            StepNumber = 1,
            Description = "Test step",
            Type = StepType.Custom,
            Operation = "Test operation"
        };

        // Act & Assert - Initial state
        step.Status.Should().Be(StepStatus.Pending);
        step.StartedAt.Should().BeNull();
        step.CompletedAt.Should().BeNull();

        // Act & Assert - Running
        step.Status = StepStatus.Running;
        step.StartedAt = DateTime.UtcNow;
        step.StartedAt.Should().NotBeNull();

        // Act & Assert - Completed
        step.Status = StepStatus.Completed;
        step.CompletedAt = DateTime.UtcNow;
        step.CompletedAt.Should().NotBeNull();
        step.CompletedAt.Should().BeOnOrAfter(step.StartedAt.Value);
    }

    [Fact]
    public void RiskAssessment_LowRisk_HasCorrectCharacteristics()
    {
        // Arrange & Act
        var risk = new RiskAssessment
        {
            Level = RiskLevel.Low,
            RiskFactors = new List<string> { "Read-only operation" },
            Mitigations = new List<string> { "No changes to production data" },
            AllChangesReversible = true,
            RequiresDowntime = false
        };

        // Assert
        risk.Level.Should().Be(RiskLevel.Low);
        risk.AllChangesReversible.Should().BeTrue();
        risk.RequiresDowntime.Should().BeFalse();
        risk.EstimatedDowntime.Should().BeNull();
    }

    [Fact]
    public void RiskAssessment_HighRisk_RequiresDowntime()
    {
        // Arrange & Act
        var risk = new RiskAssessment
        {
            Level = RiskLevel.High,
            RiskFactors = new List<string> { "Table rebuild required", "Large table size" },
            Mitigations = new List<string> { "Backup taken", "Rollback plan ready" },
            AllChangesReversible = false,
            RequiresDowntime = true,
            EstimatedDowntime = TimeSpan.FromMinutes(30)
        };

        // Assert
        risk.Level.Should().Be(RiskLevel.High);
        risk.RequiresDowntime.Should().BeTrue();
        risk.EstimatedDowntime.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void CredentialRequirement_SpecifiesMinimalScope()
    {
        // Arrange & Act
        var credReq = new CredentialRequirement
        {
            SecretRef = "postgres-production",
            Scope = new AccessScope
            {
                Level = AccessLevel.DDL,
                AllowedOperations = new List<string> { "CREATE INDEX" },
                DeniedOperations = new List<string> { "DROP INDEX", "DROP TABLE" }
            },
            Duration = TimeSpan.FromMinutes(10),
            Purpose = "Create spatial index on cities table",
            Operations = new List<string> { "CREATE INDEX CONCURRENTLY" }
        };

        // Assert
        credReq.Scope.Level.Should().Be(AccessLevel.DDL);
        credReq.Scope.AllowedOperations.Should().Contain("CREATE INDEX");
        credReq.Scope.DeniedOperations.Should().Contain("DROP TABLE");
        credReq.Duration.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Theory]
    [InlineData(AccessLevel.ReadOnly)]
    [InlineData(AccessLevel.DDL)]
    [InlineData(AccessLevel.DML)]
    [InlineData(AccessLevel.Config)]
    [InlineData(AccessLevel.Admin)]
    public void AccessScope_SupportsAllAccessLevels(AccessLevel level)
    {
        // Arrange & Act
        var scope = new AccessScope
        {
            Level = level
        };

        // Assert
        scope.Level.Should().Be(level);
    }

    [Fact]
    public void RollbackPlan_ContainsReversalSteps()
    {
        // Arrange & Act
        var rollback = new RollbackPlan
        {
            SnapshotId = "snapshot-20250102-120000",
            Steps = new List<RollbackStep>
            {
                new RollbackStep
                {
                    Description = "Drop created index",
                    Operation = "DROP INDEX CONCURRENTLY idx_cities_geom"
                },
                new RollbackStep
                {
                    Description = "Restore configuration",
                    Operation = "RESTORE SNAPSHOT snapshot-20250102-120000"
                }
            }
        };

        // Assert
        rollback.Steps.Should().HaveCount(2);
        rollback.Steps[0].Description.Should().Contain("Drop created index");
        rollback.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ExecutionPlan_WithMultipleSteps_MaintainsOrder()
    {
        // Arrange & Act
        var plan = new ExecutionPlan
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "Test Plan",
            Type = PlanType.Optimization,
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    StepNumber = 1,
                    Description = "Analyze table",
                    Type = StepType.Custom,
                    Operation = "ANALYZE cities"
                },
                new PlanStep
                {
                    StepNumber = 2,
                    Description = "Create index",
                    Type = StepType.CreateIndex,
                    Operation = "CREATE INDEX...",
                    DependsOn = new List<int> { 1 }
                },
                new PlanStep
                {
                    StepNumber = 3,
                    Description = "Vacuum table",
                    Type = StepType.VacuumAnalyze,
                    Operation = "VACUUM ANALYZE cities",
                    DependsOn = new List<int> { 2 }
                }
            },
            CredentialsRequired = new List<CredentialRequirement>(),
            Risk = new RiskAssessment
            {
                Level = RiskLevel.Low,
                RiskFactors = new List<string>(),
                Mitigations = new List<string>(),
                AllChangesReversible = true,
                RequiresDowntime = false
            }
        };

        // Assert
        plan.Steps.Should().HaveCount(3);
        plan.Steps[0].StepNumber.Should().Be(1);
        plan.Steps[1].DependsOn.Should().Contain(1);
        plan.Steps[2].DependsOn.Should().Contain(2);
    }

    [Theory]
    [InlineData(StepType.CreateIndex)]
    [InlineData(StepType.DropIndex)]
    [InlineData(StepType.VacuumAnalyze)]
    [InlineData(StepType.CreateStatistics)]
    [InlineData(StepType.UpdateConfig)]
    public void PlanStep_SupportsAllStepTypes(StepType stepType)
    {
        // Arrange & Act
        var step = new PlanStep
        {
            StepNumber = 1,
            Description = $"Test {stepType}",
            Type = stepType,
            Operation = "Test operation"
        };

        // Assert
        step.Type.Should().Be(stepType);
    }

    private ExecutionPlan CreateMinimalPlan(PlanType type)
    {
        return new ExecutionPlan
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = "Test Plan",
            Type = type,
            Steps = new List<PlanStep>(),
            CredentialsRequired = new List<CredentialRequirement>(),
            Risk = new RiskAssessment
            {
                Level = RiskLevel.Low,
                RiskFactors = new List<string>(),
                Mitigations = new List<string>(),
                AllChangesReversible = true,
                RequiresDowntime = false
            }
        };
    }
}
