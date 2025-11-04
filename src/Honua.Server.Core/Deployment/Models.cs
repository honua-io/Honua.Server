// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Deployment;

/// <summary>
/// Represents a deployment instance
/// </summary>
public class Deployment
{
    /// <summary>
    /// Unique deployment identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Target environment (dev, staging, production)
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Git commit SHA being deployed
    /// </summary>
    public string Commit { get; set; } = string.Empty;

    /// <summary>
    /// Git branch
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Current deployment state
    /// </summary>
    public DeploymentState State { get; set; } = DeploymentState.Pending;

    /// <summary>
    /// Health status (ArgoCD-inspired)
    /// </summary>
    public DeploymentHealth Health { get; set; } = DeploymentHealth.Unknown;

    /// <summary>
    /// Sync status (ArgoCD-inspired)
    /// </summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Unknown;

    /// <summary>
    /// Deployment started timestamp
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Deployment completed timestamp
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Duration of deployment
    /// </summary>
    [JsonIgnore]
    public TimeSpan? Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : null;

    /// <summary>
    /// Backup ID created before deployment
    /// </summary>
    public string? BackupId { get; set; }

    /// <summary>
    /// User or system that initiated deployment
    /// </summary>
    public string InitiatedBy { get; set; } = "system";

    /// <summary>
    /// State transition history
    /// </summary>
    public List<StateTransition> StateHistory { get; set; } = new();

    /// <summary>
    /// Deployment plan/diff
    /// </summary>
    public DeploymentPlan? Plan { get; set; }

    /// <summary>
    /// Validation results
    /// </summary>
    public List<ValidationResult> ValidationResults { get; set; } = new();

    /// <summary>
    /// Error message if deployment failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether automatic rollback is enabled
    /// </summary>
    public bool AutoRollback { get; set; } = true;

    /// <summary>
    /// Metadata/annotations
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// State transition record
/// </summary>
public class StateTransition
{
    /// <summary>
    /// Previous state
    /// </summary>
    public DeploymentState? From { get; set; }

    /// <summary>
    /// New state
    /// </summary>
    public DeploymentState To { get; set; }

    /// <summary>
    /// Transition timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Optional message
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Deployment plan (ArgoCD diff-inspired)
/// </summary>
public class DeploymentPlan
{
    /// <summary>
    /// Resources to be added
    /// </summary>
    public List<ResourceChange> Added { get; set; } = new();

    /// <summary>
    /// Resources to be modified
    /// </summary>
    public List<ResourceChange> Modified { get; set; } = new();

    /// <summary>
    /// Resources to be removed
    /// </summary>
    public List<ResourceChange> Removed { get; set; } = new();

    /// <summary>
    /// Migrations to run
    /// </summary>
    public List<Migration> Migrations { get; set; } = new();

    /// <summary>
    /// Estimated deployment duration
    /// </summary>
    public TimeSpan? EstimatedDuration { get; set; }

    /// <summary>
    /// Whether there are breaking changes
    /// </summary>
    public bool HasBreakingChanges { get; set; }

    /// <summary>
    /// Risk level assessment
    /// </summary>
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
}

/// <summary>
/// Resource change in deployment plan
/// </summary>
public class ResourceChange
{
    /// <summary>
    /// Resource type (layer, service, datasource, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Resource name/identifier
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path in Git repository
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Diff/change description
    /// </summary>
    public string? Diff { get; set; }

    /// <summary>
    /// Whether this is a breaking change
    /// </summary>
    public bool IsBreaking { get; set; }
}

/// <summary>
/// Database migration
/// </summary>
public class Migration
{
    /// <summary>
    /// Migration identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Migration description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Path to migration file
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Estimated duration
    /// </summary>
    public TimeSpan? EstimatedDuration { get; set; }
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Validation type (syntax, policy, health, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether validation passed
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Validation message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Risk level assessment
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Environment state (ArgoCD Application-inspired)
/// </summary>
public class EnvironmentState
{
    /// <summary>
    /// Environment name
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Current deployment
    /// </summary>
    public Deployment? CurrentDeployment { get; set; }

    /// <summary>
    /// Last successful deployment
    /// </summary>
    public Deployment? LastSuccessfulDeployment { get; set; }

    /// <summary>
    /// Deployment history (recent deployments)
    /// </summary>
    public List<DeploymentSummary> History { get; set; } = new();

    /// <summary>
    /// Current sync status
    /// </summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Unknown;

    /// <summary>
    /// Current health status
    /// </summary>
    public DeploymentHealth Health { get; set; } = DeploymentHealth.Unknown;

    /// <summary>
    /// Git commit currently deployed
    /// </summary>
    public string? DeployedCommit { get; set; }

    /// <summary>
    /// Latest commit in Git
    /// </summary>
    public string? LatestCommit { get; set; }

    /// <summary>
    /// Last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Deployment summary for history
/// </summary>
public class DeploymentSummary
{
    public string Id { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public DeploymentState State { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
}
