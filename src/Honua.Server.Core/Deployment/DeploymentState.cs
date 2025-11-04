// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Deployment;

/// <summary>
/// Deployment state machine states
/// Inspired by ArgoCD's application states
/// </summary>
public enum DeploymentState
{
    /// <summary>
    /// Deployment has been created but not started
    /// </summary>
    Pending,

    /// <summary>
    /// Running pre-deployment validation
    /// </summary>
    Validating,

    /// <summary>
    /// Generating deployment plan and calculating diff
    /// </summary>
    Planning,

    /// <summary>
    /// Waiting for human approval (production deployments)
    /// </summary>
    AwaitingApproval,

    /// <summary>
    /// Creating backups before deployment
    /// </summary>
    BackingUp,

    /// <summary>
    /// Actively applying changes
    /// </summary>
    Applying,

    /// <summary>
    /// Running post-deployment validation
    /// </summary>
    PostValidating,

    /// <summary>
    /// Deployment completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Deployment failed
    /// </summary>
    Failed,

    /// <summary>
    /// Actively rolling back changes
    /// </summary>
    RollingBack,

    /// <summary>
    /// Rollback completed
    /// </summary>
    RolledBack
}

/// <summary>
/// Health status of a deployment (ArgoCD-inspired)
/// </summary>
public enum DeploymentHealth
{
    /// <summary>
    /// Health status is unknown or being determined
    /// </summary>
    Unknown,

    /// <summary>
    /// Deployment is healthy and operating normally
    /// </summary>
    Healthy,

    /// <summary>
    /// Deployment is progressing towards healthy state
    /// </summary>
    Progressing,

    /// <summary>
    /// Deployment is degraded but functional
    /// </summary>
    Degraded,

    /// <summary>
    /// Deployment has failed health checks
    /// </summary>
    Unhealthy
}

/// <summary>
/// Sync status between Git and deployed state (ArgoCD-inspired)
/// </summary>
public enum SyncStatus
{
    /// <summary>
    /// Sync status is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// Deployed state matches Git
    /// </summary>
    Synced,

    /// <summary>
    /// Deployed state does not match Git
    /// </summary>
    OutOfSync,

    /// <summary>
    /// Sync is in progress
    /// </summary>
    Syncing
}
