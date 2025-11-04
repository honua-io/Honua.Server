// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Represents the analyzed intent from a user request
/// </summary>
public class AgentIntent
{
    public string TaskType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string[]? RequiredCapabilities { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a selected agent for task execution
/// </summary>
public class AgentSelection
{
    public string AgentName { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? Reasoning { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents available agent information
/// </summary>
public class AgentInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Capabilities { get; set; } = Array.Empty<string>();
    public double Priority { get; set; } = 1.0;
}

/// <summary>
/// Agent performance statistics
/// </summary>
public class AgentStats
{
    public string AgentName { get; set; } = string.Empty;
    public int TotalInteractions { get; set; }
    public double SuccessRate { get; set; }
    public double AverageExecutionTimeMs { get; set; }
}