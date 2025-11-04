// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Deployment;

/// <summary>
/// Service for managing deployment approvals
/// Provides approval workflow for production and high-risk deployments
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Determines if a deployment requires approval based on environment and plan
    /// </summary>
    /// <param name="environment">Target environment (production, staging, development)</param>
    /// <param name="plan">Deployment plan with risk assessment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if approval is required, false otherwise</returns>
    Task<bool> RequiresApprovalAsync(
        string environment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Request approval for a deployment
    /// Creates an approval record and notifies approvers
    /// </summary>
    /// <param name="deploymentId">Deployment identifier</param>
    /// <param name="environment">Target environment</param>
    /// <param name="plan">Deployment plan</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RequestApprovalAsync(
        string deploymentId,
        string environment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve a deployment
    /// Marks the deployment as approved and allows it to proceed
    /// </summary>
    /// <param name="deploymentId">Deployment identifier</param>
    /// <param name="approver">User who approved the deployment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ApproveAsync(
        string deploymentId,
        string approver,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a deployment
    /// Marks the deployment as rejected with a reason
    /// </summary>
    /// <param name="deploymentId">Deployment identifier</param>
    /// <param name="rejecter">User who rejected the deployment</param>
    /// <param name="reason">Reason for rejection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RejectAsync(
        string deploymentId,
        string rejecter,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get approval status for a deployment
    /// Returns null if no approval record exists
    /// </summary>
    /// <param name="deploymentId">Deployment identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval status or null if not found</returns>
    Task<ApprovalStatus?> GetApprovalStatusAsync(
        string deploymentId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the approval status of a deployment
/// </summary>
public class ApprovalStatus
{
    /// <summary>
    /// Deployment identifier
    /// </summary>
    public string DeploymentId { get; set; } = string.Empty;

    /// <summary>
    /// Current approval state
    /// </summary>
    public ApprovalState State { get; set; } = ApprovalState.Pending;

    /// <summary>
    /// Environment being deployed to
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when approval was requested
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Timestamp when approval was responded to (approved or rejected)
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// User who responded to the approval request
    /// </summary>
    public string? Responder { get; set; }

    /// <summary>
    /// Reason for rejection (only set when rejected)
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Deployment plan summary
    /// </summary>
    public DeploymentPlan? Plan { get; set; }

    /// <summary>
    /// Timeout for approval (after this time, approval is considered expired)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether the approval has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt && State == ApprovalState.Pending;
}

/// <summary>
/// State of an approval request
/// </summary>
public enum ApprovalState
{
    /// <summary>
    /// Approval is pending (awaiting response)
    /// </summary>
    Pending,

    /// <summary>
    /// Deployment has been approved
    /// </summary>
    Approved,

    /// <summary>
    /// Deployment has been rejected
    /// </summary>
    Rejected
}
