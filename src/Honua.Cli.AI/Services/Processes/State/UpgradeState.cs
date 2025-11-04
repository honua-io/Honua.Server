// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for Upgrade Process workflow.
/// Tracks blue-green deployment state for zero-downtime upgrades.
/// </summary>
public class UpgradeState
{
    public string UpgradeId { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string TargetVersion { get; set; } = string.Empty;
    public string? BackupLocation { get; set; }
    public string GreenEnvironment { get; set; } = string.Empty; // Current production
    public string BlueEnvironment { get; set; } = string.Empty; // New version
    public DateTime StartTime { get; set; }
    public int TrafficPercentageOnBlue { get; set; }
    public string Status { get; set; } = "Pending";
    public bool MigrationsCompleted { get; set; }
    public bool ValidationPassed { get; set; }
    public bool CanRollback { get; set; } = true;
    public string? CloudProjectId { get; set; } // GCP/AWS/Azure project/account identifier
    public Dictionary<string, string> InfrastructureOutputs { get; set; } = new(); // Preserved from deployment
}
