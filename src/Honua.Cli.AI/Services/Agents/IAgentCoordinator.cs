// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Coordinates multiple specialized agents to fulfill user requests.
/// Users interact with a single unified interface - the coordinator transparently
/// routes to appropriate agents and orchestrates multi-agent workflows.
/// </summary>
public interface IAgentCoordinator
{
    /// <summary>
    /// Analyzes a user request and coordinates the appropriate specialized agents to fulfill it.
    /// </summary>
    /// <param name="request">The user's natural language request</param>
    /// <param name="context">Execution context (workspace, dry-run mode, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the coordinated agent execution</returns>
    Task<AgentCoordinatorResult> ProcessRequestAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the agent coordination history for the current session.
    /// </summary>
    /// <returns>List of past agent interactions</returns>
    Task<AgentInteractionHistory> GetHistoryAsync();
}
