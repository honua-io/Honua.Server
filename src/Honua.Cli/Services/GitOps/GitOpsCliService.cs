// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Deployment;
using Honua.Server.Core.Performance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.GitOps;

/// <summary>
/// Shared service for GitOps CLI commands
/// Provides access to deployment state and approval services
/// </summary>
public class GitOpsCliService
{
    private readonly string _stateDirectory;
    private readonly string _approvalDirectory;
    private readonly ILogger<GitOpsCliService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initialize GitOps CLI service
    /// </summary>
    /// <param name="stateDirectory">Directory containing deployment state files (default: ./data/gitops-state/)</param>
    /// <param name="approvalDirectory">Directory containing approval records (default: ./data/gitops-approvals/)</param>
    /// <param name="logger">Optional logger</param>
    public GitOpsCliService(
        string? stateDirectory = null,
        string? approvalDirectory = null,
        ILogger<GitOpsCliService>? logger = null)
    {
        _stateDirectory = stateDirectory ?? Path.Combine(".", "data", "gitops-state");
        _approvalDirectory = approvalDirectory ?? Path.Combine(".", "data", "gitops-approvals");
        _logger = logger ?? NullLogger<GitOpsCliService>.Instance;

        _jsonOptions = new JsonSerializerOptions(JsonSerializerOptionsRegistry.WebIndented)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        // Ensure directories exist
        Directory.CreateDirectory(_stateDirectory);
        Directory.CreateDirectory(_approvalDirectory);
    }

    /// <summary>
    /// Get environment state
    /// </summary>
    public async Task<EnvironmentState?> GetEnvironmentStateAsync(
        string environment,
        CancellationToken cancellationToken = default)
    {
        var filePath = Path.Combine(_stateDirectory, $"{environment}.json");

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<EnvironmentState>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading environment state for '{Environment}'", environment);
            throw new InvalidOperationException($"Failed to read environment state for '{environment}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get deployment by ID
    /// </summary>
    public async Task<Deployment?> GetDeploymentAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        // Extract environment from deployment ID (format: env-timestamp)
        var parts = deploymentId.Split('-');
        if (parts.Length < 2)
        {
            return null;
        }

        var environment = parts[0];
        var state = await GetEnvironmentStateAsync(environment, cancellationToken);

        if (state == null)
        {
            return null;
        }

        if (state.CurrentDeployment?.Id == deploymentId)
        {
            return state.CurrentDeployment;
        }

        if (state.LastSuccessfulDeployment?.Id == deploymentId)
        {
            return state.LastSuccessfulDeployment;
        }

        return null;
    }

    /// <summary>
    /// Get approval status for deployment
    /// </summary>
    public async Task<ApprovalStatus?> GetApprovalStatusAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetApprovalFilePath(deploymentId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<ApprovalStatus>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading approval status for '{DeploymentId}'", deploymentId);
            throw new InvalidOperationException($"Failed to read approval status for '{deploymentId}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Approve deployment
    /// </summary>
    public async Task ApproveDeploymentAsync(
        string deploymentId,
        string approver,
        CancellationToken cancellationToken = default)
    {
        var approvalStatus = await GetApprovalStatusAsync(deploymentId, cancellationToken);
        if (approvalStatus == null)
        {
            throw new InvalidOperationException($"Approval record not found for deployment '{deploymentId}'");
        }

        if (approvalStatus.State != ApprovalState.Pending)
        {
            throw new InvalidOperationException(
                $"Deployment '{deploymentId}' has already been {approvalStatus.State.ToString().ToLowerInvariant()}");
        }

        if (approvalStatus.IsExpired)
        {
            throw new InvalidOperationException(
                $"Approval for deployment '{deploymentId}' has expired (expired at {approvalStatus.ExpiresAt:yyyy-MM-dd HH:mm:ss})");
        }

        approvalStatus.State = ApprovalState.Approved;
        approvalStatus.RespondedAt = DateTime.UtcNow;
        approvalStatus.Responder = approver;

        await SaveApprovalStatusAsync(approvalStatus, cancellationToken);

        _logger.LogInformation("Deployment '{DeploymentId}' approved by '{Approver}'", deploymentId, approver);
    }

    /// <summary>
    /// Reject deployment
    /// </summary>
    public async Task RejectDeploymentAsync(
        string deploymentId,
        string rejecter,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var approvalStatus = await GetApprovalStatusAsync(deploymentId, cancellationToken);
        if (approvalStatus == null)
        {
            throw new InvalidOperationException($"Approval record not found for deployment '{deploymentId}'");
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

        _logger.LogWarning("Deployment '{DeploymentId}' rejected by '{Rejecter}': {Reason}", deploymentId, rejecter, reason);
    }

    /// <summary>
    /// List all environments
    /// </summary>
    public Task<List<string>> ListEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_stateDirectory))
        {
            return Task.FromResult(new List<string>());
        }

        var files = Directory.GetFiles(_stateDirectory, "*.json");
        var environments = files
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(name => !name.IsNullOrEmpty())
            .ToList();

        return Task.FromResult(environments!);
    }

    /// <summary>
    /// Get deployment history for environment
    /// </summary>
    public async Task<List<DeploymentSummary>> GetDeploymentHistoryAsync(
        string environment,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var state = await GetEnvironmentStateAsync(environment, cancellationToken);
        if (state == null)
        {
            return new List<DeploymentSummary>();
        }

        return state.History.Take(limit).ToList();
    }

    /// <summary>
    /// Format duration as human-readable string
    /// </summary>
    public static string FormatDuration(TimeSpan? duration)
    {
        if (!duration.HasValue)
        {
            return "N/A";
        }

        var d = duration.Value;

        if (d.TotalSeconds < 60)
        {
            return $"{(int)d.TotalSeconds}s";
        }
        else if (d.TotalMinutes < 60)
        {
            return $"{(int)d.TotalMinutes}m {d.Seconds}s";
        }
        else if (d.TotalHours < 24)
        {
            return $"{(int)d.TotalHours}h {d.Minutes}m";
        }
        else
        {
            return $"{(int)d.TotalDays}d {d.Hours}h";
        }
    }

    /// <summary>
    /// Format timestamp as human-readable relative time
    /// </summary>
    public static string FormatRelativeTime(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var diff = now - timestamp;

        if (diff.TotalSeconds < 60)
        {
            return "just now";
        }
        else if (diff.TotalMinutes < 60)
        {
            var minutes = (int)diff.TotalMinutes;
            return $"{minutes} minute{(minutes == 1 ? "" : "s")} ago";
        }
        else if (diff.TotalHours < 24)
        {
            var hours = (int)diff.TotalHours;
            return $"{hours} hour{(hours == 1 ? "" : "s")} ago";
        }
        else if (diff.TotalDays < 7)
        {
            var days = (int)diff.TotalDays;
            return $"{days} day{(days == 1 ? "" : "s")} ago";
        }
        else
        {
            return timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
    }

    /// <summary>
    /// Get short commit SHA (first 8 characters)
    /// </summary>
    public static string GetShortCommit(string commit)
    {
        if (commit.IsNullOrEmpty())
        {
            return "N/A";
        }

        return commit.Length > 8 ? commit.Substring(0, 8) : commit;
    }

    // Private helper methods

    private async Task SaveApprovalStatusAsync(
        ApprovalStatus approvalStatus,
        CancellationToken cancellationToken)
    {
        var filePath = GetApprovalFilePath(approvalStatus.DeploymentId);

        try
        {
            var json = JsonSerializer.Serialize(approvalStatus, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving approval status for '{DeploymentId}'", approvalStatus.DeploymentId);
            throw new InvalidOperationException($"Failed to save approval status for '{approvalStatus.DeploymentId}': {ex.Message}", ex);
        }
    }

    private string GetApprovalFilePath(string deploymentId)
    {
        // Sanitize deployment ID for use as filename
        var safeFileName = string.Concat(deploymentId.Where(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_'));

        return Path.Combine(_approvalDirectory, $"{safeFileName}.json");
    }
}

/// <summary>
/// Approval status record
/// </summary>
public class ApprovalStatus
{
    public string DeploymentId { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public ApprovalState State { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Responder { get; set; }
    public string? Reason { get; set; }
    public DeploymentPlan? Plan { get; set; }

    [JsonIgnore]
    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
}
