// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents;

public sealed class AgentCapabilityRegistry
{
    private readonly Dictionary<string, TaskTypeCapability> _taskTypes;
    private readonly Dictionary<string, AgentCapability> _agents;
    private readonly ILogger<AgentCapabilityRegistry> _logger;

    public AgentCapabilityRegistry(IOptions<AgentCapabilityOptions>? options, ILogger<AgentCapabilityRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var value = options?.Value ?? new AgentCapabilityOptions();

        _taskTypes = new Dictionary<string, TaskTypeCapability>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in (value.TaskTypes?.Count > 0 ? value.TaskTypes : AgentCapabilityDefaults.CreateDefaultTaskTypes()))
        {
            if (task.Name.IsNullOrWhiteSpace())
            {
                continue;
            }

            _taskTypes[task.Name] = NormalizeTaskType(task);
        }

        _agents = new Dictionary<string, AgentCapability>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in (value.Agents?.Count > 0 ? value.Agents : AgentCapabilityDefaults.CreateDefaultAgents()))
        {
            if (agent.AgentName.IsNullOrWhiteSpace())
            {
                continue;
            }

            _agents[agent.AgentName] = NormalizeAgent(agent);
        }
    }

    public string[] GetSuggestedAgents(string taskType)
    {
        if (taskType.IsNullOrWhiteSpace())
        {
            return new[] { "DefaultAgent" };
        }

        // Find agents that specialize in this task type
        var suggestedAgents = _agents
            .Where(a => a.Value.Specializations?.Contains(taskType, StringComparer.OrdinalIgnoreCase) == true)
            .OrderBy(a => a.Value.Priority)
            .Select(a => a.Key)
            .ToArray();

        if (suggestedAgents.Length == 0)
        {
            // Fallback to general agents
            suggestedAgents = _agents
                .Where(a => a.Value.Specializations?.Contains("general", StringComparer.OrdinalIgnoreCase) == true)
                .OrderBy(a => a.Value.Priority)
                .Select(a => a.Key)
                .ToArray();
        }

        return suggestedAgents.Length > 0 ? suggestedAgents : new[] { "DefaultAgent" };
    }

    public string ClassifyTaskType(string userRequest)
    {
        if (userRequest.IsNullOrWhiteSpace())
        {
            return GetDefaultTaskType();
        }

        var lowerRequest = userRequest.ToLowerInvariant();

        var ranked = _taskTypes.Values
            .Select(task => new
            {
                task.Name,
                Score = task.Keywords.Sum(keyword => lowerRequest.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ranked.Count > 0 && ranked[0].Score > 0)
        {
            return ranked[0].Name;
        }

        _logger.LogDebug("No keyword match detected for request '{Request}'. Falling back to default task type.", userRequest);
        return GetDefaultTaskType();
    }

    public double CalculateTaskMatchScore(string agentName, string taskType)
    {
        if (!_agents.TryGetValue(agentName, out var capability))
        {
            return 0.5;
        }

        if (capability.Specializations.Contains(taskType, StringComparer.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (capability.Specializations.Any(s => taskType.Contains(s, StringComparison.OrdinalIgnoreCase) || s.Contains(taskType, StringComparison.OrdinalIgnoreCase)))
        {
            return 0.7;
        }

        return 0.3;
    }

    public IEnumerable<string> GetRegisteredAgents() => _agents.Keys;

    private string GetDefaultTaskType()
    {
        if (_taskTypes.ContainsKey("configuration"))
        {
            return "configuration";
        }

        return _taskTypes.Keys.FirstOrDefault() ?? "configuration";
    }

    private static TaskTypeCapability NormalizeTaskType(TaskTypeCapability task)
    {
        task.Keywords = task.Keywords?
            .Where(keyword => keyword.HasValue())
            .Select(keyword => keyword.ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        return task;
    }

    private static AgentCapability NormalizeAgent(AgentCapability agent)
    {
        agent.Specializations = agent.Specializations?
            .Where(spec => spec.HasValue())
            .Select(spec => spec.ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        return agent;
    }
}
