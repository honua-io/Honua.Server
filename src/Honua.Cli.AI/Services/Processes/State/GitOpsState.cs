// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for GitOps Configuration Process workflow.
/// Tracks Git-driven configuration changes with validation and rollback.
/// </summary>
public class GitOpsState
{
    public string ProcessId { get; set; } = string.Empty;
    public string GitOpsId { get; set; } = string.Empty;
    public string RepoUrl { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string ConfigPath { get; set; } = string.Empty;
    public string? LastCommitHash { get; set; }
    public List<string> ChangedFiles { get; set; } = new();
    public string? PreviousConfigVersion { get; set; }
    public Dictionary<string, string> DeployedConfig { get; set; } = new();
    public bool ConfigValid { get; set; }
    public bool ConfigSynced { get; set; }
    public bool DriftDetected { get; set; }
    public bool AutoSync { get; set; }
    public bool RollbackPerformed { get; set; }
    public DateTime StartTime { get; set; }
    public string Status { get; set; } = "Pending";
    public bool ValidationPassed { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool UserApproved { get; set; }
}
