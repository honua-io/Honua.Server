// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Deployment;

/// <summary>
/// Interface for storing and managing deployment state
/// Inspired by ArgoCD's application state management
/// </summary>
public interface IDeploymentStateStore
{
    /// <summary>
    /// Create a new deployment
    /// </summary>
    Task<Deployment> CreateDeploymentAsync(
        string environment,
        string commit,
        string branch = "main",
        string initiatedBy = "system",
        bool autoRollback = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a deployment by ID
    /// </summary>
    Task<Deployment?> GetDeploymentAsync(
        string deploymentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transition deployment to new state
    /// </summary>
    Task TransitionAsync(
        string deploymentId,
        DeploymentState newState,
        string? message = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update deployment health status
    /// </summary>
    Task UpdateHealthAsync(
        string deploymentId,
        DeploymentHealth health,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update sync status
    /// </summary>
    Task UpdateSyncStatusAsync(
        string deploymentId,
        SyncStatus syncStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set deployment plan
    /// </summary>
    Task SetPlanAsync(
        string deploymentId,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add validation result
    /// </summary>
    Task AddValidationResultAsync(
        string deploymentId,
        ValidationResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set backup ID
    /// </summary>
    Task SetBackupIdAsync(
        string deploymentId,
        string backupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark deployment as failed with error
    /// </summary>
    Task FailDeploymentAsync(
        string deploymentId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current deployment for environment
    /// </summary>
    Task<Deployment?> GetCurrentDeploymentAsync(
        string environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get last successful deployment for environment
    /// </summary>
    Task<Deployment?> GetLastSuccessfulDeploymentAsync(
        string environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get environment state (ArgoCD Application-inspired)
    /// </summary>
    Task<EnvironmentState> GetEnvironmentStateAsync(
        string environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get deployment history for environment
    /// </summary>
    Task<List<DeploymentSummary>> GetDeploymentHistoryAsync(
        string environment,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all environments
    /// </summary>
    Task<List<string>> ListEnvironmentsAsync(
        CancellationToken cancellationToken = default);
}
