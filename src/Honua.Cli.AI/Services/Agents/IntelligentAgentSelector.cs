// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Intelligently selects the best agent for a request based on:
/// 1. Task type matching (semantic similarity)
/// 2. Historical performance (success rate)
/// 3. Confidence scoring
/// </summary>
public class IntelligentAgentSelector
{
    private readonly ILlmProvider? _llmProvider;
    private readonly IPatternUsageTelemetry? _telemetry;
    private readonly ILogger<IntelligentAgentSelector> _logger;
    private readonly AgentCapabilityRegistry _capabilities;

    // Constructor for tests with LLM provider
    public IntelligentAgentSelector(
        ILlmProvider llmProvider,
        AgentCapabilityRegistry capabilities,
        ILogger<IntelligentAgentSelector> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // Original constructor for backward compatibility
    public IntelligentAgentSelector(
        ILogger<IntelligentAgentSelector> logger,
        AgentCapabilityRegistry capabilities,
        IPatternUsageTelemetry? telemetry = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _telemetry = telemetry;
    }

    /// <summary>
    /// Select the best agent for a request with confidence scoring.
    /// </summary>
    public virtual async Task<(string AgentName, AgentConfidence Confidence)> SelectBestAgentAsync(
        string userRequest,
        string[] availableAgents,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Selecting best agent for request: {Request}", userRequest);

        var taskType = _capabilities.ClassifyTaskType(userRequest);
        _logger.LogDebug("Classified task type: {TaskType}", taskType);

        var agentScores = new List<(string AgentName, AgentConfidence Confidence)>();

        foreach (var agentName in availableAgents)
        {
            var confidence = await ScoreAgentAsync(agentName, taskType, cancellationToken);
            agentScores.Add((agentName, confidence));

            _logger.LogDebug(
                "Agent {AgentName}: {Level} confidence ({Overall:P0}) - TaskMatch: {TaskMatch:P0}, Success: {Success:P0}",
                agentName,
                confidence.Level,
                confidence.Overall,
                confidence.TaskMatchScore,
                confidence.HistoricalSuccessRate);
        }

        // Prefer High confidence agents
        var highConfidence = agentScores.Where(x => string.Equals(x.Confidence.Level, "High", StringComparison.OrdinalIgnoreCase)).ToList();
        if (highConfidence.Any())
        {
            var best = highConfidence.MaxBy(x => x.Confidence.Overall)!;
            _logger.LogInformation(
                "Selected {AgentName} with High confidence ({Confidence:P0})",
                best.AgentName,
                best.Confidence.Overall);
            return best;
        }

        // Fallback to highest overall score
        var fallback = agentScores.MaxBy(x => x.Confidence.Overall)!;
        _logger.LogInformation(
            "Selected {AgentName} with {Level} confidence ({Confidence:P0})",
            fallback.AgentName,
            fallback.Confidence.Level,
            fallback.Confidence.Overall);

        return fallback;
    }

    private async Task<AgentConfidence> ScoreAgentAsync(
        string agentName,
        string taskType,
        CancellationToken cancellationToken)
    {
        var taskMatchScore = _capabilities.CalculateTaskMatchScore(agentName, taskType);

        double historicalSuccessRate = 0.7; // Default neutral success rate
        int completedTasks = 0;

        if (_telemetry != null)
        {
            try
            {
                var histTaskMatch = await _telemetry.GetAgentTaskMatchScoreAsync(
                    agentName, taskType, cancellationToken);

                taskMatchScore = (taskMatchScore * 0.4) + (histTaskMatch * 0.6);

                var stats = await _telemetry.GetAgentPerformanceAsync(
                    agentName, TimeSpan.FromDays(90), cancellationToken);

                historicalSuccessRate = stats.SuccessRate;
                completedTasks = stats.TotalInteractions;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve agent performance data for {AgentName}", agentName);
            }
        }

        return AgentConfidence.Calculate(
            agentName,
            taskMatchScore,
            historicalSuccessRate,
            completedTasks);
    }

    /// <summary>
    /// Select an agent based on intent (for tests)
    /// </summary>
    public async Task<AgentSelection> SelectAgentAsync(
        AgentIntent intent,
        Dictionary<string, string> context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_llmProvider != null)
            {
                // Use LLM for intelligent selection
                var prompt = $@"Select the best agent for this task:
Task Type: {intent.TaskType}
Confidence: {intent.Confidence}
Required Capabilities: {string.Join(", ", intent.RequiredCapabilities ?? Array.Empty<string>())}
Context: {JsonSerializer.Serialize(context)}

Available agents and their capabilities:
{string.Join("\n", GetAvailableAgents().Select(a => $"- {a.Name}: {a.Description}"))}

Respond with JSON: {{ ""selectedAgent"": ""AgentName"", ""confidence"": 0.0-1.0, ""reasoning"": ""explanation"" }}";

                var request = new LlmRequest
                {
                    UserPrompt = prompt,
                    SystemPrompt = "You are an intelligent agent selector. Always respond with valid JSON.",
                    Temperature = 0.7
                };
                var response = await _llmProvider.CompleteAsync(request, cancellationToken);
                var json = JsonSerializer.Deserialize<JsonElement>(response.Content);

                return new AgentSelection
                {
                    AgentName = json.GetProperty("selectedAgent").GetString() ?? "DefaultAgent",
                    Confidence = json.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.75,
                    Reasoning = json.TryGetProperty("reasoning", out var reason) ? reason.GetString() : null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to select agent using LLM, falling back to registry");
        }

        // Fallback to registry-based selection
        var taskType = intent.TaskType ?? _capabilities.ClassifyTaskType(string.Join(" ", context.Values));
        var agents = _capabilities.GetSuggestedAgents(taskType);

        return new AgentSelection
        {
            AgentName = agents.FirstOrDefault() ?? "DefaultAgent",
            Confidence = intent.Confidence * 0.8, // Reduce confidence for fallback
            Reasoning = "Selected based on task type matching"
        };
    }

    /// <summary>
    /// Select multiple agents for complex tasks
    /// </summary>
    public async Task<List<AgentSelection>> SelectMultipleAgentsAsync(
        AgentIntent intent,
        Dictionary<string, string> context,
        int maxAgents = 3,
        CancellationToken cancellationToken = default)
    {
        var selections = new List<AgentSelection>();

        try
        {
            if (_llmProvider != null)
            {
                var prompt = $@"Select up to {maxAgents} agents for this complex task:
Task Type: {intent.TaskType}
Required Capabilities: {string.Join(", ", intent.RequiredCapabilities ?? Array.Empty<string>())}

Respond with JSON: {{ ""selectedAgents"": [{{ ""name"": ""AgentName"", ""confidence"": 0.0-1.0 }}] }}";

                var request = new LlmRequest
                {
                    UserPrompt = prompt,
                    SystemPrompt = "You are an intelligent agent selector. Always respond with valid JSON.",
                    Temperature = 0.7
                };
                var response = await _llmProvider.CompleteAsync(request, cancellationToken);
                var json = JsonSerializer.Deserialize<JsonElement>(response.Content);

                if (json.TryGetProperty("selectedAgents", out var agents))
                {
                    foreach (var agent in agents.EnumerateArray())
                    {
                        var selection = new AgentSelection
                        {
                            AgentName = agent.GetProperty("name").GetString() ?? "DefaultAgent",
                            Confidence = agent.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.75
                        };

                        // Filter by minimum confidence threshold
                        if (selection.Confidence >= 0.7)
                        {
                            selections.Add(selection);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to select multiple agents using LLM");
        }

        // Ensure we have at least one agent
        if (!selections.Any())
        {
            selections.Add(new AgentSelection
            {
                AgentName = "DefaultAgent",
                Confidence = 0.6,
                Reasoning = "Fallback selection"
            });
        }

        return selections.OrderByDescending(s => s.Confidence).Take(maxAgents).ToList();
    }

    /// <summary>
    /// Get list of available agents
    /// </summary>
    public List<AgentInfo> GetAvailableAgents()
    {
        return new List<AgentInfo>
        {
            new AgentInfo
            {
                Name = "DefaultAgent",
                Description = "General purpose agent for basic tasks",
                Capabilities = new[] { "general", "basic" }
            },
            new AgentInfo
            {
                Name = "DataAgent",
                Description = "Handles data processing and analysis",
                Capabilities = new[] { "data", "analysis", "processing" }
            },
            new AgentInfo
            {
                Name = "DeploymentAgent",
                Description = "Manages deployment and infrastructure tasks",
                Capabilities = new[] { "deployment", "infrastructure", "kubernetes", "docker" }
            },
            new AgentInfo
            {
                Name = "ValidationAgent",
                Description = "Validates plans and configurations",
                Capabilities = new[] { "validation", "review", "compliance" }
            },
            new AgentInfo
            {
                Name = "MonitoringAgent",
                Description = "Sets up monitoring and alerting",
                Capabilities = new[] { "monitoring", "metrics", "alerts" }
            }
        };
    }
}
