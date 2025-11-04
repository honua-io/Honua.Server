// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Server.Core.Notifications;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Deployment;

/// <summary>
/// File-based implementation of IApprovalService
/// Stores approval records in JSON files on filesystem
/// Thread-safe with SemaphoreSlim locking
/// </summary>
public class FileApprovalService : IApprovalService
{
    private readonly string _approvalDirectory;
    private readonly Dictionary<string, DeploymentPolicy> _environmentPolicies;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<FileApprovalService> _logger;
    private readonly INotificationService? _notificationService;
    private readonly IDeploymentStateStore? _deploymentStateStore;

    /// <summary>
    /// Initialize FileApprovalService with approval storage directory
    /// </summary>
    /// <param name="approvalDirectory">Directory to store approval records</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="environmentPolicies">Optional environment-specific policies</param>
    /// <param name="notificationService">Optional notification service for approval events</param>
    /// <param name="deploymentStateStore">Optional deployment state store for retrieving deployment details</param>
    public FileApprovalService(
        string approvalDirectory,
        ILogger<FileApprovalService> logger,
        Dictionary<string, DeploymentPolicy>? environmentPolicies = null,
        INotificationService? notificationService = null,
        IDeploymentStateStore? deploymentStateStore = null)
    {
        _approvalDirectory = approvalDirectory ?? throw new ArgumentNullException(nameof(approvalDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environmentPolicies = environmentPolicies ?? CreateDefaultPolicies();
        _notificationService = notificationService;
        _deploymentStateStore = deploymentStateStore;

        // Use WebIndented as base and add custom enum converter for approval state
        _jsonOptions = new JsonSerializerOptions(JsonSerializerOptionsRegistry.WebIndented)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        // Ensure approval directory exists
        Directory.CreateDirectory(_approvalDirectory);

        _logger.LogInformation(
            "FileApprovalService initialized with approval directory: {ApprovalDirectory}",
            _approvalDirectory);
    }

    /// <summary>
    /// Determines if approval is required based on environment and deployment plan
    /// </summary>
    public Task<bool> RequiresApprovalAsync(
        string environment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(plan);

        var policy = GetPolicyForEnvironment(environment);

        // Check if environment explicitly requires approval
        if (policy.RequiresApproval)
        {
            _logger.LogInformation(
                "Approval required for environment '{Environment}' (policy-based)",
                environment);
            return Task.FromResult(true);
        }

        // Check if risk level requires approval
        if (policy.MinimumRiskLevelForApproval.HasValue &&
            plan.RiskLevel >= policy.MinimumRiskLevelForApproval.Value)
        {
            _logger.LogInformation(
                "Approval required for environment '{Environment}' due to risk level '{RiskLevel}'",
                environment,
                plan.RiskLevel);
            return Task.FromResult(true);
        }

        // Check for breaking changes
        if (plan.HasBreakingChanges)
        {
            _logger.LogInformation(
                "Approval required for environment '{Environment}' due to breaking changes",
                environment);
            return Task.FromResult(true);
        }

        // Check for high/critical risk deployments
        if (plan.RiskLevel >= RiskLevel.High)
        {
            _logger.LogInformation(
                "Approval required for environment '{Environment}' due to high/critical risk level",
                environment);
            return Task.FromResult(true);
        }

        // Check for migrations
        if (plan.Migrations.Count > 0)
        {
            _logger.LogInformation(
                "Approval required for environment '{Environment}' due to {MigrationCount} database migration(s)",
                environment,
                plan.Migrations.Count);
            return Task.FromResult(true);
        }

        _logger.LogDebug(
            "No approval required for environment '{Environment}'",
            environment);

        return Task.FromResult(false);
    }

    /// <summary>
    /// Request approval for a deployment
    /// </summary>
    public async Task RequestApprovalAsync(
        string deploymentId,
        string environment,
        DeploymentPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deploymentId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(plan);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var policy = GetPolicyForEnvironment(environment);
            var now = DateTime.UtcNow;

            var approvalStatus = new ApprovalStatus
            {
                DeploymentId = deploymentId,
                Environment = environment,
                State = ApprovalState.Pending,
                RequestedAt = now,
                ExpiresAt = now.Add(policy.ApprovalTimeout),
                Plan = plan
            };

            await SaveApprovalStatusAsync(approvalStatus, cancellationToken);

            _logger.LogInformation(
                "Approval requested for deployment '{DeploymentId}' in environment '{Environment}' (expires at {ExpiresAt})",
                deploymentId,
                environment,
                approvalStatus.ExpiresAt);

            // Notify approval required
            if (_notificationService != null && _deploymentStateStore != null)
            {
                try
                {
                    var deployment = await _deploymentStateStore.GetDeploymentAsync(deploymentId, cancellationToken);
                    if (deployment != null)
                    {
                        await _notificationService.NotifyApprovalRequiredAsync(
                            deployment,
                            plan,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send approval required notification");
                    // Continue - approval was requested successfully
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Approve a deployment
    /// </summary>
    public async Task ApproveAsync(
        string deploymentId,
        string approver,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deploymentId);
        ArgumentException.ThrowIfNullOrEmpty(approver);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var approvalStatus = await LoadApprovalStatusAsync(deploymentId, cancellationToken);
            if (approvalStatus == null)
            {
                throw new InvalidOperationException(
                    $"Approval record not found for deployment '{deploymentId}'");
            }

            if (approvalStatus.State != ApprovalState.Pending)
            {
                throw new InvalidOperationException(
                    $"Deployment '{deploymentId}' has already been {approvalStatus.State.ToString().ToLowerInvariant()}");
            }

            if (approvalStatus.IsExpired)
            {
                throw new InvalidOperationException(
                    $"Approval for deployment '{deploymentId}' has expired (expired at {approvalStatus.ExpiresAt})");
            }

            approvalStatus.State = ApprovalState.Approved;
            approvalStatus.RespondedAt = DateTime.UtcNow;
            approvalStatus.Responder = approver;

            await SaveApprovalStatusAsync(approvalStatus, cancellationToken);

            _logger.LogInformation(
                "Deployment '{DeploymentId}' approved by '{Approver}'",
                deploymentId,
                approver);

            // Notify approved
            if (_notificationService != null && _deploymentStateStore != null)
            {
                try
                {
                    var deployment = await _deploymentStateStore.GetDeploymentAsync(deploymentId, cancellationToken);
                    if (deployment != null)
                    {
                        await _notificationService.NotifyApprovedAsync(
                            deployment,
                            approver,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send approval notification");
                    // Continue - approval was successful
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Reject a deployment
    /// </summary>
    public async Task RejectAsync(
        string deploymentId,
        string rejecter,
        string reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deploymentId);
        ArgumentException.ThrowIfNullOrEmpty(rejecter);
        ArgumentException.ThrowIfNullOrEmpty(reason);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var approvalStatus = await LoadApprovalStatusAsync(deploymentId, cancellationToken);
            if (approvalStatus == null)
            {
                throw new InvalidOperationException(
                    $"Approval record not found for deployment '{deploymentId}'");
            }

            if (approvalStatus.State != ApprovalState.Pending)
            {
                throw new InvalidOperationException(
                    $"Deployment '{deploymentId}' has already been {approvalStatus.State.ToString().ToLowerInvariant()}");
            }

            approvalStatus.State = ApprovalState.Rejected;
            approvalStatus.RespondedAt = DateTime.UtcNow;
            approvalStatus.Responder = rejecter;
            approvalStatus.Reason = reason;

            await SaveApprovalStatusAsync(approvalStatus, cancellationToken);

            _logger.LogWarning(
                "Deployment '{DeploymentId}' rejected by '{Rejecter}': {Reason}",
                deploymentId,
                rejecter,
                reason);

            // Notify rejected
            if (_notificationService != null && _deploymentStateStore != null)
            {
                try
                {
                    var deployment = await _deploymentStateStore.GetDeploymentAsync(deploymentId, cancellationToken);
                    if (deployment != null)
                    {
                        await _notificationService.NotifyRejectedAsync(
                            deployment,
                            rejecter,
                            reason,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send rejection notification");
                    // Continue - rejection was successful
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get approval status for a deployment
    /// </summary>
    public async Task<ApprovalStatus?> GetApprovalStatusAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deploymentId);

        return await LoadApprovalStatusAsync(deploymentId, cancellationToken);
    }

    // Private helper methods

    private DeploymentPolicy GetPolicyForEnvironment(string environment)
    {
        var normalizedEnv = environment.ToLowerInvariant();

        if (_environmentPolicies.TryGetValue(normalizedEnv, out var policy))
        {
            return policy;
        }

        // Return default policy if not found
        return normalizedEnv switch
        {
            "production" or "prod" => new DeploymentPolicy
            {
                RequiresApproval = true,
                ApprovalTimeout = TimeSpan.FromHours(24),
                AutoRollback = true,
                MinimumRiskLevelForApproval = RiskLevel.Medium
            },
            "staging" or "stage" => new DeploymentPolicy
            {
                RequiresApproval = false,
                ApprovalTimeout = TimeSpan.FromHours(4),
                AutoRollback = true,
                MinimumRiskLevelForApproval = RiskLevel.High
            },
            "development" or "dev" => new DeploymentPolicy
            {
                RequiresApproval = false,
                ApprovalTimeout = TimeSpan.FromHours(1),
                AutoRollback = false,
                MinimumRiskLevelForApproval = RiskLevel.Critical
            },
            _ => new DeploymentPolicy
            {
                RequiresApproval = false,
                ApprovalTimeout = TimeSpan.FromHours(4),
                AutoRollback = true
            }
        };
    }

    private async Task<ApprovalStatus?> LoadApprovalStatusAsync(
        string deploymentId,
        CancellationToken cancellationToken)
    {
        var filePath = GetApprovalFilePath(deploymentId);

        if (!File.Exists(filePath))
        {
            _logger.LogDebug(
                "Approval record not found for deployment '{DeploymentId}'",
                deploymentId);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var approvalStatus = JsonSerializer.Deserialize<ApprovalStatus>(json, _jsonOptions);

            _logger.LogDebug(
                "Loaded approval record for deployment '{DeploymentId}' with state '{State}'",
                deploymentId,
                approvalStatus?.State);

            return approvalStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading approval record for deployment '{DeploymentId}'",
                deploymentId);
            throw;
        }
    }

    private async Task SaveApprovalStatusAsync(
        ApprovalStatus approvalStatus,
        CancellationToken cancellationToken)
    {
        var filePath = GetApprovalFilePath(approvalStatus.DeploymentId);

        try
        {
            var json = JsonSerializer.Serialize(approvalStatus, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogDebug(
                "Saved approval record for deployment '{DeploymentId}' with state '{State}'",
                approvalStatus.DeploymentId,
                approvalStatus.State);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error saving approval record for deployment '{DeploymentId}'",
                approvalStatus.DeploymentId);
            throw;
        }
    }

    private string GetApprovalFilePath(string deploymentId)
    {
        // Sanitize deployment ID for use as filename
        var safeFileName = string.Concat(deploymentId.Where(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_'));

        return Path.Combine(_approvalDirectory, $"{safeFileName}.json");
    }

    private static Dictionary<string, DeploymentPolicy> CreateDefaultPolicies()
    {
        return new Dictionary<string, DeploymentPolicy>
        {
            ["production"] = new DeploymentPolicy
            {
                RequiresApproval = true,
                ApprovalTimeout = TimeSpan.FromHours(24),
                AutoRollback = true,
                MinimumRiskLevelForApproval = RiskLevel.Medium
            },
            ["staging"] = new DeploymentPolicy
            {
                RequiresApproval = false,
                ApprovalTimeout = TimeSpan.FromHours(4),
                AutoRollback = true,
                MinimumRiskLevelForApproval = RiskLevel.High
            },
            ["development"] = new DeploymentPolicy
            {
                RequiresApproval = false,
                ApprovalTimeout = TimeSpan.FromHours(1),
                AutoRollback = false,
                MinimumRiskLevelForApproval = RiskLevel.Critical
            }
        };
    }
}
