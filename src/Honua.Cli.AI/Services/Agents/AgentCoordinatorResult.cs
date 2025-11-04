// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Result of agent coordinator processing.
/// </summary>
public sealed class AgentCoordinatorResult
{
    /// <summary>
    /// Whether the request was successfully processed.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// User-facing response message.
    /// </summary>
    public string Response { get; init; } = string.Empty;

    /// <summary>
    /// Which specialized agents were involved (hidden from user by default).
    /// </summary>
    public List<string> AgentsInvolved { get; init; } = new();

    /// <summary>
    /// Detailed execution results (for debug/verbose modes).
    /// </summary>
    public List<AgentStepResult> Steps { get; init; } = new();

    /// <summary>
    /// Any warnings or non-critical issues encountered.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Error message if Success is false.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Suggested next steps for the user.
    /// </summary>
    public List<string> NextSteps { get; init; } = new();

    /// <summary>
    /// Process ID for long-running workflows (optional).
    /// </summary>
    public string? ProcessId { get; init; }
}

/// <summary>
/// Result of an individual agent step.
/// </summary>
public sealed class AgentStepResult
{
    /// <summary>
    /// The agent that executed this step.
    /// </summary>
    public string AgentName { get; init; } = string.Empty;

    /// <summary>
    /// What action was taken.
    /// </summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Whether the step succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Result message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// How long the step took.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// History of agent interactions.
/// </summary>
public sealed class AgentInteractionHistory
{
    /// <summary>
    /// Session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// All interactions in this session.
    /// </summary>
    public List<AgentInteraction> Interactions { get; init; } = new();
}

/// <summary>
/// A single interaction between user and agents.
/// </summary>
public sealed class AgentInteraction
{
    /// <summary>
    /// When the interaction occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// User's request.
    /// </summary>
    public string UserRequest { get; init; } = string.Empty;

    /// <summary>
    /// Agents that were coordinated.
    /// </summary>
    public List<string> AgentsUsed { get; init; } = new();

    /// <summary>
    /// Whether the interaction succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Response provided to user.
    /// </summary>
    public string Response { get; init; } = string.Empty;
}
