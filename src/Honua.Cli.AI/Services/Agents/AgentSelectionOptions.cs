// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Configuration options for intelligent agent selection.
/// Controls how the LLM-based agent selector chooses relevant agents for each request.
/// </summary>
public class AgentSelectionOptions
{
    /// <summary>
    /// Maximum number of agents to select per request.
    /// Default: 5 agents (down from 28 agents).
    /// </summary>
    public int MaxAgentsPerRequest { get; set; } = 5;

    /// <summary>
    /// Whether to enable intelligent LLM-based agent selection.
    /// If false, all agents will be used (fallback behavior).
    /// Default: true.
    /// </summary>
    public bool EnableIntelligentSelection { get; set; } = true;

    /// <summary>
    /// Whether to enable caching of agent selection results.
    /// Caches selections for similar requests to reduce LLM API calls.
    /// Default: true.
    /// </summary>
    public bool EnableSelectionCaching { get; set; } = true;

    /// <summary>
    /// How long to cache agent selection results.
    /// Default: 15 minutes.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Minimum relevance score (0.0 to 1.0) for an agent to be selected.
    /// Agents with scores below this threshold are excluded.
    /// Default: 0.3.
    /// </summary>
    public double MinimumRelevanceScore { get; set; } = 0.3;

    /// <summary>
    /// Temperature for the LLM selection model (0.0 = deterministic, 1.0 = creative).
    /// Lower values produce more consistent selections.
    /// Default: 0.1 (very deterministic).
    /// </summary>
    public double SelectionTemperature { get; set; } = 0.1;

    /// <summary>
    /// Maximum tokens for the selection LLM request.
    /// Default: 1000 tokens (enough for agent list and selection).
    /// </summary>
    public int SelectionMaxTokens { get; set; } = 1000;
}
