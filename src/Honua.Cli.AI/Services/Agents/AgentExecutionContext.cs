// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Context for agent execution containing workspace information and execution mode.
/// </summary>
public sealed class AgentExecutionContext
{
    /// <summary>
    /// Path to the workspace directory.
    /// </summary>
    public string WorkspacePath { get; init; } = Directory.GetCurrentDirectory();

    /// <summary>
    /// Whether to run in dry-run mode (planning only, no actual execution).
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Whether to require explicit user approval before executing operations.
    /// </summary>
    public bool RequireApproval { get; init; } = true;

    /// <summary>
    /// Session identifier for tracking related requests.
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Previous interactions in this session (for conversation context).
    /// </summary>
    public List<string> ConversationHistory { get; init; } = new();

    /// <summary>
    /// User's preferred verbosity level.
    /// </summary>
    public VerbosityLevel Verbosity { get; init; } = VerbosityLevel.Normal;
}

/// <summary>
/// Verbosity level for agent output.
/// </summary>
public enum VerbosityLevel
{
    /// <summary>
    /// Minimal output - only critical information.
    /// </summary>
    Minimal,

    /// <summary>
    /// Normal output - balanced information.
    /// </summary>
    Normal,

    /// <summary>
    /// Verbose output - detailed explanations.
    /// </summary>
    Verbose,

    /// <summary>
    /// Debug output - everything including agent reasoning.
    /// </summary>
    Debug
}
