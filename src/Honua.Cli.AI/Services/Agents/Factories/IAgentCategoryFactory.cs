// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.SemanticKernel.Agents;

namespace Honua.Cli.AI.Services.Agents.Factories;

/// <summary>
/// Base interface for category-specific agent factories.
/// Each factory is responsible for creating agents within a specific domain.
/// </summary>
public interface IAgentCategoryFactory
{
    /// <summary>
    /// Creates all agents for this category.
    /// </summary>
    Agent[] CreateAgents();
}
