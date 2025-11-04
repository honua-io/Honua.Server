// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Deployment;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Notifications;

/// <summary>
/// Composite notification service that delegates to multiple notification services (Slack, Email, etc.)
/// Continues sending notifications to other services even if one fails.
/// Implements resilient error handling - notification failures never block deployments.
/// </summary>
public class CompositeNotificationService : INotificationService
{
    private readonly IEnumerable<INotificationService> _services;
    private readonly ILogger<CompositeNotificationService> _logger;

    /// <summary>
    /// Initialize CompositeNotificationService with multiple notification services
    /// </summary>
    public CompositeNotificationService(
        IEnumerable<INotificationService> services,
        ILogger<CompositeNotificationService> logger)
    {
        Guard.NotNull(services);
        Guard.NotNull(logger);

        // Filter out the composite service itself to prevent recursion
        _services = services.Where(s => s is not CompositeNotificationService).ToList();
        _logger = logger;

        _logger.LogInformation(
            "Initialized CompositeNotificationService with {ServiceCount} notification service(s)",
            _services.Count());
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentCreatedAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "deployment created",
            deployment.Id,
            async (service, ct) => await service.NotifyDeploymentCreatedAsync(deployment, plan, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyApprovalRequiredAsync(
        Deployment.Deployment deployment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "approval required",
            deployment.Id,
            async (service, ct) => await service.NotifyApprovalRequiredAsync(deployment, plan, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyApprovedAsync(
        Deployment.Deployment deployment,
        string approver,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "deployment approved",
            deployment.Id,
            async (service, ct) => await service.NotifyApprovedAsync(deployment, approver, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyRejectedAsync(
        Deployment.Deployment deployment,
        string rejecter,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "deployment rejected",
            deployment.Id,
            async (service, ct) => await service.NotifyRejectedAsync(deployment, rejecter, reason, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentStartedAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "deployment started",
            deployment.Id,
            async (service, ct) => await service.NotifyDeploymentStartedAsync(deployment, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentCompletedAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "deployment completed",
            deployment.Id,
            async (service, ct) => await service.NotifyDeploymentCompletedAsync(deployment, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyDeploymentFailedAsync(
        Deployment.Deployment deployment,
        string error,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "deployment failed",
            deployment.Id,
            async (service, ct) => await service.NotifyDeploymentFailedAsync(deployment, error, ct),
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyRollbackAsync(
        Deployment.Deployment deployment,
        CancellationToken cancellationToken = default)
    {
        await NotifyAllServicesAsync(
            "rollback",
            deployment.Id,
            async (service, ct) => await service.NotifyRollbackAsync(deployment, ct),
            cancellationToken);
    }

    // Private helper methods

    private async Task NotifyAllServicesAsync(
        string notificationType,
        string deploymentId,
        Func<INotificationService, CancellationToken, Task> notifyAction,
        CancellationToken cancellationToken)
    {
        if (!_services.Any())
        {
            _logger.LogDebug(
                "No notification services configured, skipping {NotificationType} notification for deployment {DeploymentId}",
                notificationType,
                deploymentId);
            return;
        }

        var tasks = _services.Select(async service =>
        {
            try
            {
                await notifyAction(service, cancellationToken);

                _logger.LogDebug(
                    "Successfully sent {NotificationType} notification via {ServiceType} for deployment {DeploymentId}",
                    notificationType,
                    service.GetType().Name,
                    deploymentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to send {NotificationType} notification via {ServiceType} for deployment {DeploymentId}. " +
                    "Will continue with other notification services.",
                    notificationType,
                    service.GetType().Name,
                    deploymentId);
            }
        });

        // Wait for all notification tasks to complete (don't fail if some do)
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
