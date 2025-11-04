// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Serialization;

using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Decomposes complex tasks into hierarchical sub-tasks for parallel or sequential execution.
/// Implements the Hierarchical Task Decomposition pattern.
/// </summary>
public sealed class HierarchicalTaskDecomposer
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<HierarchicalTaskDecomposer> _logger;

    public HierarchicalTaskDecomposer(
        ILlmProvider llmProvider,
        ILogger<HierarchicalTaskDecomposer> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes a request to determine if hierarchical decomposition is needed.
    /// </summary>
    public Task<DecompositionDecision> ShouldDecomposeAsync(
        string request,
        IntentAnalysisResult intent,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Heuristic-based quick decisions
        var requiresMultipleAgents = intent.RequiresMultipleAgents;
        var agentCount = intent.RequiredAgents.Count;

        // Multi-cloud deployments always benefit from hierarchical decomposition
        var cloudProviders = new[] { "aws", "azure", "gcp", "google cloud" };
        var mentionedClouds = cloudProviders.Count(c => request.Contains(c, StringComparison.OrdinalIgnoreCase));

        if (mentionedClouds >= 2)
        {
            _logger.LogInformation("Multi-cloud deployment detected. Hierarchical decomposition recommended.");
            return Task.FromResult(new DecompositionDecision
            {
                ShouldDecompose = true,
                Reason = "Multi-cloud deployment requiring parallel cloud-specific agents",
                DecompositionStrategy = DecompositionStrategy.ParallelByCloudProvider,
                EstimatedComplexity = "high"
            });
        }

        // Complex deployments with many agents
        if (agentCount >= 4)
        {
            _logger.LogInformation("{AgentCount} agents required. Considering hierarchical decomposition", agentCount);
            return Task.FromResult(new DecompositionDecision
            {
                ShouldDecompose = true,
                Reason = $"Complex task requiring {agentCount} specialized agents",
                DecompositionStrategy = DecompositionStrategy.SequentialWithSubtasks,
                EstimatedComplexity = "high"
            });
        }

        // Check for keywords indicating complex workflows
        var complexityKeywords = new[]
        {
            "multi-region", "multi-cloud", "blue-green", "canary",
            "disaster recovery", "high availability", "production-grade",
            "microservices", "full stack", "end-to-end"
        };

        var complexityScore = complexityKeywords.Count(k => request.Contains(k, StringComparison.OrdinalIgnoreCase));

        if (complexityScore >= 2)
        {
            _logger.LogInformation("High complexity keywords detected (score: {ComplexityScore}). Decomposition recommended", complexityScore);
            return Task.FromResult(new DecompositionDecision
            {
                ShouldDecompose = true,
                Reason = "High-complexity deployment with multiple architectural concerns",
                DecompositionStrategy = DecompositionStrategy.SequentialWithSubtasks,
                EstimatedComplexity = "high"
            });
        }

        // For simpler tasks, no decomposition needed
        return Task.FromResult(new DecompositionDecision
        {
            ShouldDecompose = false,
            Reason = "Task complexity does not warrant hierarchical decomposition",
            DecompositionStrategy = DecompositionStrategy.None,
            EstimatedComplexity = agentCount > 2 ? "medium" : "low"
        });
    }

    /// <summary>
    /// Decomposes a task into hierarchical sub-tasks.
    /// </summary>
    public async Task<TaskDecomposition> DecomposeAsync(
        string request,
        IntentAnalysisResult intent,
        DecompositionStrategy strategy,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"You are an expert at decomposing complex infrastructure deployment tasks into hierarchical sub-tasks.

Your goal is to break down a complex task into:
1. **Parent tasks**: High-level phases (e.g., ""Architecture Design"", ""Infrastructure Provisioning"", ""Security Configuration"")
2. **Child tasks**: Specific actions within each phase (e.g., ""Generate Terraform for AWS"", ""Configure IAM roles"")

Principles:
- Identify **parallel opportunities**: Tasks that can run simultaneously (e.g., generating configs for different clouds)
- Identify **dependencies**: Tasks that must run sequentially (e.g., infrastructure must exist before configuration)
- Keep hierarchy shallow (max 2 levels: parent → children)
- Each child task should map to a specific agent action

Return JSON:
{
  ""phases"": [
    {
      ""name"": ""string"",
      ""description"": ""string"",
      ""parallelizable"": true/false,
      ""tasks"": [
        {
          ""name"": ""string"",
          ""agent"": ""string (agent name)"",
          ""action"": ""string (what the agent should do)"",
          ""dependencies"": [""parent phase or task names""],
          ""estimatedDuration"": ""string (e.g., '5 minutes')""
        }
      ]
    }
  ]
}";

        var userPrompt = $@"Decompose this task hierarchically:

**Request**: {request}

**Detected Intent**: {intent.PrimaryIntent}
**Required Agents**: {string.Join(", ", intent.RequiredAgents)}
**Strategy**: {strategy}

**Context**:
- Workspace: {context.WorkspacePath}
- Mode: {(context.DryRun ? "planning" : "execution")}

Break this into parent phases and child tasks. Identify what can run in parallel.";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.3,
            MaxTokens = 1500
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogWarning("LLM decomposition failed. Falling back to heuristic decomposition.");
            return FallbackDecomposition(intent, strategy);
        }

        try
        {
            var json = ExtractJson(response.Content);
            var decomposition = JsonSerializer.Deserialize<TaskDecomposition>(
                json,
                CliJsonOptions.DevTooling);

            if (decomposition?.Phases == null || decomposition.Phases.Count == 0)
            {
                _logger.LogWarning("Empty decomposition returned. Using fallback.");
                return FallbackDecomposition(intent, strategy);
            }

            _logger.LogInformation($"Task decomposed into {decomposition.Phases.Count} phases with {decomposition.Phases.Sum(p => p.Tasks.Count)} total tasks.");
            return decomposition;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse decomposition JSON. Using fallback.");
            return FallbackDecomposition(intent, strategy);
        }
    }

    /// <summary>
    /// Fallback decomposition using heuristics when LLM fails.
    /// </summary>
    private TaskDecomposition FallbackDecomposition(IntentAnalysisResult intent, DecompositionStrategy strategy)
    {
        var phases = new List<DecompositionPhase>();

        if (strategy == DecompositionStrategy.ParallelByCloudProvider)
        {
            // Parallel execution by cloud provider
            var cloudProviders = new[] { "AWS", "Azure", "GCP" };
            foreach (var cloud in cloudProviders)
            {
                phases.Add(new DecompositionPhase
                {
                    Name = $"{cloud} Deployment",
                    Description = $"Deploy infrastructure to {cloud}",
                    Parallelizable = true,
                    Tasks = new List<DecompositionTask>
                    {
                        new()
                        {
                            Name = $"Generate {cloud} Configuration",
                            Agent = "DeploymentConfiguration",
                            Action = $"Generate Terraform for {cloud}",
                            Dependencies = new List<string>(),
                            EstimatedDuration = "5 minutes"
                        },
                        new()
                        {
                            Name = $"Deploy to {cloud}",
                            Agent = "DeploymentExecution",
                            Action = $"Execute {cloud} deployment",
                            Dependencies = new List<string> { $"Generate {cloud} Configuration" },
                            EstimatedDuration = "10 minutes"
                        }
                    }
                });
            }
        }
        else
        {
            // Sequential with sub-tasks
            phases.Add(new DecompositionPhase
            {
                Name = "Planning",
                Description = "Analyze requirements and plan architecture",
                Parallelizable = false,
                Tasks = intent.RequiredAgents
                    .Take(1)
                    .Select(agent => new DecompositionTask
                    {
                        Name = $"{agent} Analysis",
                        Agent = agent,
                        Action = $"Analyze and plan using {agent}",
                        Dependencies = new List<string>(),
                        EstimatedDuration = "3 minutes"
                    })
                    .ToList()
            });

            phases.Add(new DecompositionPhase
            {
                Name = "Implementation",
                Description = "Execute configuration and deployment",
                Parallelizable = true,
                Tasks = intent.RequiredAgents
                    .Skip(1)
                    .Select(agent => new DecompositionTask
                    {
                        Name = $"{agent} Execution",
                        Agent = agent,
                        Action = $"Execute using {agent}",
                        Dependencies = new List<string> { "Planning" },
                        EstimatedDuration = "5 minutes"
                    })
                    .ToList()
            });
        }

        return new TaskDecomposition { Phases = phases };
    }

    private string ExtractJson(string text)
    {
        if (text.Contains("```json"))
        {
            var start = text.IndexOf("```json") + 7;
            var end = text.IndexOf("```", start);
            if (end > start) return text.Substring(start, end - start).Trim();
        }
        else if (text.Contains("```"))
        {
            var start = text.IndexOf("```") + 3;
            var end = text.IndexOf("```", start);
            if (end > start) return text.Substring(start, end - start).Trim();
        }
        return text.Trim();
    }
}

/// <summary>
/// Decision about whether to decompose a task hierarchically.
/// </summary>
public sealed class DecompositionDecision
{
    public bool ShouldDecompose { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DecompositionStrategy DecompositionStrategy { get; init; }
    public string EstimatedComplexity { get; init; } = "low";
}

/// <summary>
/// Strategy for decomposing a task.
/// </summary>
public enum DecompositionStrategy
{
    None,
    ParallelByCloudProvider,
    SequentialWithSubtasks,
    MixedParallelSequential
}

/// <summary>
/// Hierarchical task decomposition result.
/// </summary>
public sealed class TaskDecomposition
{
    public List<DecompositionPhase> Phases { get; init; } = new();
}

/// <summary>
/// A phase in the task decomposition (parent level).
/// </summary>
public sealed class DecompositionPhase
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Parallelizable { get; init; }
    public List<DecompositionTask> Tasks { get; init; } = new();
}

/// <summary>
/// A specific task within a phase (child level).
/// </summary>
public sealed class DecompositionTask
{
    public string Name { get; init; } = string.Empty;
    public string Agent { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public List<string> Dependencies { get; init; } = new();
    public string EstimatedDuration { get; init; } = string.Empty;
}
