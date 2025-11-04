// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Agents;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Service for intelligently selecting relevant agents for a given user request.
/// Uses LLM-based analysis to choose only the agents that are necessary,
/// reducing token usage and improving response times.
/// </summary>
public interface IAgentSelectionService
{
    /// <summary>
    /// Selects the most relevant agents for a given user request.
    /// </summary>
    /// <param name="userRequest">The user's request text</param>
    /// <param name="availableAgents">All available agents to choose from</param>
    /// <param name="maxAgents">Maximum number of agents to select</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A subset of agents most relevant to the user request</returns>
    Task<IReadOnlyList<Agent>> SelectRelevantAgentsAsync(
        string userRequest,
        IReadOnlyList<Agent> availableAgents,
        int maxAgents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explains why specific agents were selected for a request.
    /// Useful for debugging and transparency.
    /// </summary>
    /// <param name="userRequest">The user's request text</param>
    /// <param name="selectedAgents">The agents that were selected</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An explanation of the agent selection reasoning</returns>
    Task<AgentSelectionExplanation> ExplainSelectionAsync(
        string userRequest,
        IReadOnlyList<Agent> selectedAgents,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Explains the reasoning behind agent selection.
/// </summary>
/// <param name="Reasoning">Overall reasoning for the selection</param>
/// <param name="AgentRelevanceScores">Dictionary mapping agent names to relevance explanations</param>
public record AgentSelectionExplanation(
    string Reasoning,
    Dictionary<string, string> AgentRelevanceScores);
