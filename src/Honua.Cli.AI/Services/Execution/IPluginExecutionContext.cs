// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Execution;

/// <summary>
/// Provides context for execution operations including workspace path, approval mode, and audit trail
/// </summary>
public interface IPluginExecutionContext
{
    /// <summary>
    /// Workspace directory where operations will be executed
    /// </summary>
    string WorkspacePath { get; }

    /// <summary>
    /// Whether to require user approval before executing actions
    /// </summary>
    bool RequireApproval { get; }

    /// <summary>
    /// Whether to perform dry-run (simulation) instead of actual execution
    /// </summary>
    bool DryRun { get; }

    /// <summary>
    /// Audit trail of executed actions
    /// </summary>
    List<PluginExecutionAuditEntry> AuditTrail { get; }

    /// <summary>
    /// Record an action in the audit trail
    /// </summary>
    void RecordAction(string plugin, string action, string details, bool success, string? error = null);

    /// <summary>
    /// Request user approval for an action
    /// </summary>
    Task<bool> RequestApprovalAsync(string action, string details, string[] resources);

    /// <summary>
    /// Get the session ID for rollback purposes
    /// </summary>
    string SessionId { get; }
}

/// <summary>
/// Represents an entry in the execution audit trail
/// </summary>
public record PluginExecutionAuditEntry(
    DateTime Timestamp,
    string Plugin,
    string Action,
    string Details,
    bool Success,
    string? Error = null
);
