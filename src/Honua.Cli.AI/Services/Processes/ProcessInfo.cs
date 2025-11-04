// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Class for tracking process execution state.
/// Can be persisted to various backends (Redis, Database, In-Memory).
/// </summary>
public class ProcessInfo
{
    public string ProcessId { get; set; } = string.Empty;
    public string WorkflowType { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public string CurrentStep { get; set; } = string.Empty;
    public int CompletionPercentage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
}
