// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Deployment;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Notifications;

/// <summary>
/// Service for sending deployment notifications via various channels (Slack, Email, etc.)
/// Notifications should never block or fail deployments - implementations must be resilient.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Notifies that a new deployment has been created and is starting.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="plan">The deployment plan with changes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyDeploymentCreatedAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment requires approval before proceeding.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="plan">The deployment plan with changes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyApprovalRequiredAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment has been approved.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="approver">The person who approved the deployment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyApprovedAsync(
        Deployment.Deployment deployment,
        string approver,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment has been rejected.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="rejecter">The person who rejected the deployment</param>
    /// <param name="reason">The reason for rejection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyRejectedAsync(
        Deployment.Deployment deployment,
        string rejecter,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment has started applying changes.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyDeploymentStartedAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment has completed successfully.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyDeploymentCompletedAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment has failed.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="error">The error message or exception details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyDeploymentFailedAsync(
        Deployment.Deployment deployment,
        string error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a deployment is being rolled back.
    /// </summary>
    /// <param name="deployment">The deployment instance</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the notification operation</returns>
    Task NotifyRollbackAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default);
}
