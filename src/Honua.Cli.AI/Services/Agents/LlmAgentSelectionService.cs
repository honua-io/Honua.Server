// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Server.Core.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Agents;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// LLM-based agent selection service that intelligently chooses relevant agents
/// for each user request, reducing token usage and improving response times.
/// </summary>
public sealed class LlmAgentSelectionService : IAgentSelectionService
{
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LlmAgentSelectionService> _logger;
    private readonly AgentSelectionOptions _options;
    private readonly ActivitySource _activitySource;

    public LlmAgentSelectionService(
        ILlmProviderFactory llmProviderFactory,
        IMemoryCache cache,
        ILogger<LlmAgentSelectionService> logger,
        IOptions<AgentSelectionOptions> options)
    {
        _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _activitySource = new ActivitySource("Honua.AgentSelection");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Agent>> SelectRelevantAgentsAsync(
        string userRequest,
        IReadOnlyList<Agent> availableAgents,
        int maxAgents,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("SelectRelevantAgents");
        activity?.SetTag("agent.total_available", availableAgents.Count);
        activity?.SetTag("agent.max_requested", maxAgents);
        activity?.SetTag("agent.intelligent_selection_enabled", _options.EnableIntelligentSelection);

        // If intelligent selection is disabled, return all agents
        if (!_options.EnableIntelligentSelection)
        {
            _logger.LogInformation(
                "Intelligent agent selection is disabled. Using all {AgentCount} agents.",
                availableAgents.Count);
            activity?.SetTag("agent.selected_count", availableAgents.Count);
            return availableAgents;
        }

        // Check cache if enabled
        var cacheKey = GenerateCacheKey(userRequest, maxAgents);
        if (_options.EnableSelectionCaching && _cache.TryGetValue<List<string>>(cacheKey, out var cachedAgentNames))
        {
            _logger.LogDebug("Agent selection cache hit for request hash: {CacheKey}", cacheKey);
            activity?.SetTag("agent.cache_hit", true);

            var cachedAgents = availableAgents
                .Where(a => a.Name != null && cachedAgentNames!.Contains(a.Name))
                .ToList();

            if (cachedAgents.Count > 0)
            {
                activity?.SetTag("agent.selected_count", cachedAgents.Count);
                return cachedAgents;
            }

            _logger.LogWarning("Cached agents not found in available agents. Proceeding with LLM selection.");
        }
        else
        {
            activity?.SetTag("agent.cache_hit", false);
        }

        try
        {
            // Use LLM to select relevant agents
            var selectedAgents = await SelectAgentsUsingLlmAsync(
                userRequest,
                availableAgents,
                maxAgents,
                cancellationToken);

            // Cache the selection
            if (_options.EnableSelectionCaching && selectedAgents.Count > 0)
            {
                var agentNames = selectedAgents.Select(a => a.Name).ToList();
                var cacheOptions = new CacheOptionsBuilder()
                    .WithAbsoluteExpiration(_options.CacheDuration)
                    .BuildMemory();
                _cache.Set(cacheKey, agentNames, cacheOptions);
                _logger.LogDebug(
                    "Cached agent selection for {Duration}. Selected {Count} agents.",
                    _options.CacheDuration,
                    agentNames.Count);
            }

            activity?.SetTag("agent.selected_count", selectedAgents.Count);
            activity?.SetTag("agent.selection_method", "llm");

            return selectedAgents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select agents using LLM. Falling back to all agents.");
            activity?.SetTag("agent.selection_failed", true);
            activity?.SetTag("agent.fallback", true);
            activity?.SetTag("agent.selected_count", availableAgents.Count);

            // Fallback to all agents on error
            return availableAgents;
        }
    }

    /// <inheritdoc/>
    public async Task<AgentSelectionExplanation> ExplainSelectionAsync(
        string userRequest,
        IReadOnlyList<Agent> selectedAgents,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("ExplainSelection");

        try
        {
            var llmProvider = _llmProviderFactory.CreatePrimary();

            var prompt = BuildExplanationPrompt(userRequest, selectedAgents);

            var request = new LlmRequest
            {
                SystemPrompt = "You are an expert at explaining agent selection decisions. Provide clear, concise reasoning.",
                UserPrompt = prompt,
                MaxTokens = 500,
                Temperature = 0.3
            };

            var response = await llmProvider.CompleteAsync(request, cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning("Failed to generate explanation: {Error}", response.ErrorMessage);
                return new AgentSelectionExplanation(
                    "Explanation generation failed.",
                    new Dictionary<string, string>());
            }

            // Parse the explanation (basic implementation - can be improved with structured output)
            var scores = selectedAgents
                .Where(a => a.Name != null)
                .ToDictionary(
                    a => a.Name!,
                    a => $"Selected for handling {a.Description ?? "unknown task"}");

            return new AgentSelectionExplanation(response.Content, scores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating selection explanation");
            return new AgentSelectionExplanation(
                $"Error: {ex.Message}",
                new Dictionary<string, string>());
        }
    }

    private async Task<IReadOnlyList<Agent>> SelectAgentsUsingLlmAsync(
        string userRequest,
        IReadOnlyList<Agent> availableAgents,
        int maxAgents,
        CancellationToken cancellationToken)
    {
        var llmProvider = _llmProviderFactory.CreatePrimary();

        var prompt = BuildSelectionPrompt(userRequest, availableAgents, maxAgents);

        var request = new LlmRequest
        {
            SystemPrompt = "You are an intelligent agent selection system. Your task is to select the most relevant agents for a given user request.",
            UserPrompt = prompt,
            MaxTokens = _options.SelectionMaxTokens,
            Temperature = _options.SelectionTemperature
        };

        _logger.LogDebug("Sending agent selection request to LLM. Available agents: {Count}", availableAgents.Count);

        var response = await llmProvider.CompleteAsync(request, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("LLM agent selection failed: {Error}", response.ErrorMessage);
            throw new InvalidOperationException($"LLM selection failed: {response.ErrorMessage}");
        }

        var candidates = ParseAgentSelections(response.Content, availableAgents.Count);

        var effectiveThreshold = _options.MinimumRelevanceScore;
        if (effectiveThreshold >= 0.7 && effectiveThreshold < 0.9)
        {
            effectiveThreshold = Math.Max(effectiveThreshold, 0.8);
        }

        var filteredCandidates = candidates
            .Where(c => c.Index >= 0 && c.Index < availableAgents.Count)
            .Where(c => !c.Confidence.HasValue || c.Confidence.Value >= effectiveThreshold)
            .OrderByDescending(c => c.Confidence ?? 1.0)
            .ThenBy(c => c.Index)
            .GroupBy(c => c.Index)
            .Select(g => g.First())
            .OrderByDescending(c => c.Confidence ?? 1.0)
            .ThenBy(c => c.Index)
            .ToList();

        if (filteredCandidates.Count == 0)
        {
            _logger.LogWarning(
                "No valid agents selected by LLM. Falling back to first {MaxAgents} agents.",
                maxAgents);
            return availableAgents.Take(Math.Min(maxAgents, _options.MaxAgentsPerRequest)).ToList();
        }

        _logger.LogInformation(
            "LLM selected {SelectedCount} agents from {TotalCount} available: {Indices}",
            filteredCandidates.Count,
            availableAgents.Count,
            string.Join(", ", filteredCandidates.Select(c => c.Index)));

        return filteredCandidates
            .Select(c => availableAgents[c.Index])
            .Take(Math.Min(maxAgents, _options.MaxAgentsPerRequest))
            .ToList();
    }

    private string BuildSelectionPrompt(
        string userRequest,
        IReadOnlyList<Agent> availableAgents,
        int maxAgents)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an agent selection system. Given a user request and a list of available agents with their capabilities, select the most relevant agents.");
        sb.AppendLine();
        sb.AppendLine($"User Request: \"{userRequest}\"");
        sb.AppendLine();
        sb.AppendLine("Available Agents:");

        for (int i = 0; i < availableAgents.Count; i++)
        {
            var agent = availableAgents[i];
            sb.AppendLine($"{i}. {agent.Name} - {agent.Description}");
        }

        sb.AppendLine();
        sb.AppendLine($"Instructions:");
        sb.AppendLine($"1. Analyze the user request to understand what they need help with");
        sb.AppendLine($"2. Identify which agents have capabilities relevant to this request");
        sb.AppendLine($"3. Select the {maxAgents} most relevant agents");
        sb.AppendLine($"4. Return ONLY a JSON array of agent indices (0-based), ranked by relevance");
        sb.AppendLine($"5. Select agents that complement each other (e.g., deployment + security + cost review)");
        sb.AppendLine();
        sb.AppendLine($"Example response format:");
        sb.AppendLine($"[0, 5, 12, 3, 8]");
        sb.AppendLine();
        sb.AppendLine($"Return ONLY the JSON array, no other text:");

        return sb.ToString();
    }

    private string BuildExplanationPrompt(string userRequest, IReadOnlyList<Agent> selectedAgents)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"User Request: \"{userRequest}\"");
        sb.AppendLine();
        sb.AppendLine("Selected Agents:");

        foreach (var agent in selectedAgents)
        {
            sb.AppendLine($"- {agent.Name}: {agent.Description}");
        }

        sb.AppendLine();
        sb.AppendLine("Explain in 2-3 sentences why these agents were selected for this request:");

        return sb.ToString();
    }

    private List<SelectionCandidate> ParseAgentSelections(string llmResponse, int maxIndex)
    {
        try
        {
            var payload = ExtractJsonPayload(llmResponse);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return new List<SelectionCandidate>();
            }

            using var document = JsonDocument.Parse(payload);
            return ParseSelectionDocument(document.RootElement, maxIndex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing agent indices from LLM response");
        }

        // Return empty list on parse failure (caller will handle fallback)
        return new List<SelectionCandidate>();
    }

    private static string ExtractJsonPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return string.Empty;
        }

        var trimmed = response.Trim();

        if (trimmed.Contains("```", StringComparison.Ordinal))
        {
            var firstFence = trimmed.IndexOf("```", StringComparison.Ordinal);
            var secondFence = trimmed.IndexOf("```", firstFence + 3, StringComparison.Ordinal);
            if (secondFence > firstFence)
            {
                var fencedContent = trimmed.Substring(firstFence + 3, secondFence - firstFence - 3);
                var newlineIndex = fencedContent.IndexOf('\n');
                if (newlineIndex >= 0 && fencedContent[..newlineIndex].Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    fencedContent = fencedContent[(newlineIndex + 1)..];
                }
                trimmed = fencedContent.Trim();
            }
        }

        return trimmed;
    }

    private List<SelectionCandidate> ParseSelectionDocument(JsonElement root, int maxIndex)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return ParseIndexArray(root);
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("selections", out var selectionsElement) && selectionsElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<SelectionCandidate>();
                foreach (var selection in selectionsElement.EnumerateArray())
                {
                    if (selection.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var index))
                    {
                        double? confidence = null;
                        if (selection.TryGetProperty("confidence", out var confidenceElement))
                        {
                            confidence = confidenceElement.ValueKind switch
                            {
                                JsonValueKind.Number => confidenceElement.GetDouble(),
                                JsonValueKind.String when double.TryParse(confidenceElement.GetString(), out var parsed) => parsed,
                                _ => (double?)null
                            };
                        }

                        string? reason = null;
                        if (selection.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String)
                        {
                            reason = reasonElement.GetString();
                        }

                        list.Add(new SelectionCandidate(index, confidence, reason));
                    }
                }

                if (list.Count > 0)
                {
                    return list;
                }
            }

            if (root.TryGetProperty("indices", out var indicesElement) && indicesElement.ValueKind == JsonValueKind.Array)
            {
                return ParseIndexArray(indicesElement);
            }
        }

        if (root.ValueKind == JsonValueKind.String)
        {
            var value = root.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    using var nested = JsonDocument.Parse(value);
                    return ParseSelectionDocument(nested.RootElement, maxIndex);
                }
                catch
                {
                    // ignore and fall through
                }
            }
        }

        _logger.LogWarning("Unrecognized selection payload format: {Payload}", root.ToString());
        return new List<SelectionCandidate>();
    }

    private List<SelectionCandidate> ParseIndexArray(JsonElement arrayElement)
    {
        var list = new List<SelectionCandidate>();
        foreach (var element in arrayElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var idx))
            {
                list.Add(new SelectionCandidate(idx, null, null));
            }
            else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var idx2))
            {
                double? confidence = null;
                if (element.TryGetProperty("confidence", out var confidenceElement))
                {
                    confidence = confidenceElement.ValueKind switch
                    {
                        JsonValueKind.Number => confidenceElement.GetDouble(),
                        JsonValueKind.String when double.TryParse(confidenceElement.GetString(), out var parsed) => parsed,
                        _ => (double?)null
                    };
                }

                string? reason = null;
                if (element.TryGetProperty("reason", out var reasonElement) && reasonElement.ValueKind == JsonValueKind.String)
                {
                    reason = reasonElement.GetString();
                }

                list.Add(new SelectionCandidate(idx2, confidence, reason));
            }
        }

        return list;
    }

    private string GenerateCacheKey(string userRequest, int maxAgents)
    {
        // Create a simple hash-based cache key
        var input = $"{userRequest.ToLowerInvariant().Trim()}_{maxAgents}";
        var hash = input.GetHashCode();
        return $"agent_selection_{hash}";
    }

    private sealed record SelectionCandidate(int Index, double? Confidence, string? Reason);
}
