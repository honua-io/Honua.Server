// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Telemetry;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Smart LLM provider router that selects providers based on task characteristics.
/// Implements multi-provider strategies for second opinions and consensus.
/// </summary>
public sealed class SmartLlmProviderRouter : ILlmProviderRouter
{
    private readonly ILlmProviderFactory _factory;
    private readonly ILogger<SmartLlmProviderRouter> _logger;
    private readonly PostgreSqlTelemetryService? _telemetryService;

    // Provider characteristics for routing decisions (baseline - reinforcement learning will override)
    private static readonly Dictionary<string, ProviderProfile> ProviderProfiles = new()
    {
        ["anthropic"] = new ProviderProfile
        {
            Name = "Anthropic Claude",
            StrengthCategories = new[] { "reasoning", "analysis", "code-generation", "security-review" },
            AvgLatencyMs = 2500,
            CostPer1kTokens = 0.015m,
            MaxTokens = 200000,
            BestFor = "Complex reasoning, long context, detailed analysis"
        },
        ["openai"] = new ProviderProfile
        {
            Name = "OpenAI GPT",
            StrengthCategories = new[] { "creative", "summarization", "json-generation", "general-purpose" },
            AvgLatencyMs = 1500,
            CostPer1kTokens = 0.01m,
            MaxTokens = 128000,
            BestFor = "Fast responses, creative tasks, structured output"
        }
    };

    public SmartLlmProviderRouter(
        ILlmProviderFactory factory,
        ILogger<SmartLlmProviderRouter> logger,
        PostgreSqlTelemetryService? telemetryService = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryService = telemetryService;
    }

    /// <summary>
    /// Routes request to best provider based on task context.
    /// Uses reinforcement learning from historical performance data.
    /// </summary>
    public async Task<LlmResponse> RouteRequestAsync(
        LlmRequest request,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var selectedProvider = await SelectProviderForTaskWithLearningAsync(taskContext, cancellationToken);
        _logger.LogDebug("Routing {TaskType} to {Provider}", taskContext.TaskType, selectedProvider);

        var provider = _factory.GetProvider(selectedProvider);
        var fallbackUsed = false;
        LlmResponse response;

        try
        {
            response = await provider!.CompleteAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider {Provider} failed, attempting fallback", selectedProvider);
            fallbackUsed = true;

            // Try fallback provider
            var fallbackProvider = SelectSecondaryProvider(selectedProvider, taskContext);
            provider = _factory.GetProvider(fallbackProvider);
            response = await provider!.CompleteAsync(request, cancellationToken);
            selectedProvider = fallbackProvider;
        }

        sw.Stop();

        // Record routing decision for reinforcement learning
        if (_telemetryService != null)
        {
            await _telemetryService.RecordLlmRoutingDecisionAsync(
                taskContext.TaskType,
                selectedProvider,
                fallbackUsed,
                response.Success,
                (int)sw.ElapsedMilliseconds,
                tokenCount: null,
                costUsd: null,
                context: new Dictionary<string, object>
                {
                    ["criticality"] = taskContext.Criticality,
                    ["maxLatencyMs"] = taskContext.MaxLatencyMs,
                    ["maxCostUsd"] = taskContext.MaxCostUsd
                });
        }

        return response;
    }

    /// <summary>
    /// Gets second opinion from different provider for critical decisions.
    /// </summary>
    public async Task<SecondOpinionResult> GetSecondOpinionAsync(
        LlmRequest request,
        LlmResponse firstOpinion,
        string firstProvider,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default)
    {
        // Select different provider for second opinion
        var secondProvider = SelectSecondaryProvider(firstProvider, taskContext);
        _logger.LogInformation("Getting second opinion from {Provider} (first was {First})",
            secondProvider, firstProvider);

        var provider = _factory.GetProvider(secondProvider);
        var secondOpinion = await provider!.CompleteAsync(request, cancellationToken);

        // Analyze agreement
        var agreementAnalysis = await AnalyzeAgreementAsync(
            firstOpinion,
            secondOpinion,
            firstProvider,
            secondProvider,
            taskContext,
            cancellationToken);

        return agreementAnalysis;
    }

    /// <summary>
    /// Gets consensus from multiple providers running in parallel.
    /// </summary>
    public async Task<ConsensusResult> GetConsensusAsync(
        LlmRequest request,
        string[] providers,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting consensus from {Count} providers: {Providers}",
            providers.Length, string.Join(", ", providers));

        var sw = Stopwatch.StartNew();

        // Run all providers in parallel
        var tasks = providers.Select(async providerName =>
        {
            try
            {
                var provider = _factory.GetProvider(providerName);
                var response = await provider!.CompleteAsync(request, cancellationToken);
                return (providerName, response, success: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed during consensus", providerName);
                return (providerName, new LlmResponse
                {
                    Content = string.Empty,
                    Model = "error",
                    Success = false,
                    ErrorMessage = ex.Message
                }, success: false);
            }
        });

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        sw.Stop();

        var successfulResults = results.Where(r => r.success && r.Item2.Success).ToArray();

        if (successfulResults.Length == 0)
        {
            _logger.LogError("All providers failed during consensus");
            throw new InvalidOperationException("All providers failed to generate consensus");
        }

        _logger.LogInformation("Consensus received from {Success}/{Total} providers in {Ms}ms",
            successfulResults.Length, providers.Length, sw.ElapsedMilliseconds);

        // Synthesize consensus
        var synthesized = await SynthesizeConsensusAsync(
            successfulResults.Select(r => r.Item2).ToArray(),
            successfulResults.Select(r => r.providerName).ToArray(),
            taskContext,
            cancellationToken);

        return new ConsensusResult
        {
            Responses = successfulResults.Select(r => r.Item2).ToArray(),
            Providers = successfulResults.Select(r => r.providerName).ToArray(),
            SynthesizedResponse = synthesized.response,
            AgreementScore = synthesized.agreementScore,
            ConsensusMethod = synthesized.method
        };
    }

    /// <summary>
    /// Selects best provider for a task type using reinforcement learning.
    /// Falls back to heuristic-based routing if no learning data available.
    /// </summary>
    private async Task<string> SelectProviderForTaskWithLearningAsync(
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default)
    {
        // If telemetry service available, use reinforcement learning
        if (_telemetryService != null)
        {
            try
            {
                // Get provider statistics for this task type over last 7 days
                var stats = await _telemetryService.GetProviderStatsAsync(
                    taskContext.TaskType,
                    TimeSpan.FromDays(7));

                if (stats.Any())
                {
                    // Calculate weighted score for each provider and find the best one
                    // Score = (success_rate * 0.5) + (speed_score * 0.3) + (cost_score * 0.2)
                    var bestProvider = stats
                        .Select(kvp =>
                        {
                            var provider = kvp.Key;
                            var providerStats = kvp.Value;

                            var successRate = providerStats.SuccessRate;
                            var speedScore = 1.0 - Math.Min(providerStats.AvgLatencyMs / 10000.0, 1.0); // Normalize latency
                            var costScore = 1.0; // Could be calculated from cost data

                            var weightedScore = (successRate * 0.5) + (speedScore * 0.3) + (costScore * 0.2);

                            return new { Provider = provider, Score = weightedScore, Stats = providerStats };
                        })
                        .MaxBy(p => p.Score);

                    if (bestProvider != null)
                    {
                        _logger.LogInformation(
                            "Reinforcement learning selected {Provider} for {TaskType} (score: {Score:F2}, success rate: {SuccessRate:P0}, avg latency: {Latency}ms)",
                            bestProvider.Provider,
                            taskContext.TaskType,
                            bestProvider.Score,
                            bestProvider.Stats.SuccessRate,
                            bestProvider.Stats.AvgLatencyMs);

                        return bestProvider.Provider;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to use reinforcement learning for routing, falling back to heuristics");
            }
        }

        // Fall back to heuristic-based routing
        return SelectProviderForTaskHeuristic(taskContext);
    }

    /// <summary>
    /// Heuristic-based provider selection (fallback when no learning data).
    /// </summary>
    private string SelectProviderForTaskHeuristic(LlmTaskContext taskContext)
    {
        var availableProviders = _factory.GetAvailableProviders()
            .Where(p => p != "mock")
            .ToArray();

        // If only one provider available, use it
        if (availableProviders.Length == 1)
        {
            return availableProviders[0];
        }

        // If multiple providers available, use smart routing
        if (availableProviders.Length == 0)
        {
            throw new InvalidOperationException("No LLM providers configured with API keys");
        }

        // Prefer Anthropic if available
        var preferAnthropic = availableProviders.Contains("anthropic");
        var preferOpenAI = availableProviders.Contains("openai");

        // Critical tasks: Use Anthropic for superior reasoning
        if (taskContext.Criticality == "critical" && preferAnthropic)
        {
            return "anthropic";
        }

        // Task-specific routing
        var taskType = taskContext.TaskType.ToLowerInvariant();

        if ((taskType.Contains("security") || taskType.Contains("review") || taskType.Contains("analysis")) && preferAnthropic)
        {
            return "anthropic"; // Better at detailed analysis
        }

        if ((taskType.Contains("summarize") || taskType.Contains("creative")) && preferOpenAI)
        {
            return "openai"; // Better at creative tasks
        }

        // Latency-sensitive: OpenAI is faster
        if (taskContext.MaxLatencyMs < 2000 && preferOpenAI)
        {
            return "openai";
        }

        // Cost-sensitive: OpenAI is cheaper
        if (taskContext.MaxCostUsd < 0.01m && preferOpenAI)
        {
            return "openai";
        }

        // Default to Anthropic if available, otherwise first available
        return preferAnthropic ? "anthropic" : availableProviders[0];
    }

    /// <summary>
    /// Selects secondary provider different from primary.
    /// </summary>
    private string SelectSecondaryProvider(string primaryProvider, LlmTaskContext taskContext)
    {
        var availableProviders = _factory.GetAvailableProviders()
            .Where(p => p != "mock" && !p.Equals(primaryProvider, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (availableProviders.Length == 0)
        {
            throw new InvalidOperationException($"No alternative provider available for second opinion (primary: {primaryProvider})");
        }

        // Prefer opposite provider for diversity
        var normalized = primaryProvider.ToLowerInvariant();
        if (normalized == "anthropic" && availableProviders.Contains("openai"))
        {
            return "openai";
        }
        if (normalized == "openai" && availableProviders.Contains("anthropic"))
        {
            return "anthropic";
        }

        // Return first available alternative
        return availableProviders[0];
    }

    /// <summary>
    /// Analyzes agreement between two LLM responses.
    /// </summary>
    private Task<SecondOpinionResult> AnalyzeAgreementAsync(
        LlmResponse first,
        LlmResponse second,
        string firstProvider,
        string secondProvider,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken)
    {
        // Simple heuristic: check if responses are similar
        var firstContent = first.Content.Trim().ToLowerInvariant();
        var secondContent = second.Content.Trim().ToLowerInvariant();

        // Extract key decisions if JSON
        var firstDecision = TryExtractDecision(first.Content);
        var secondDecision = TryExtractDecision(second.Content);

        bool agrees;
        string? disagreement = null;
        LlmResponse recommended;
        string reasoning;

        if (firstDecision != null && secondDecision != null)
        {
            // Both are structured decisions
            agrees = firstDecision.Equals(secondDecision, StringComparison.OrdinalIgnoreCase);

            if (agrees)
            {
                recommended = first; // Both agree, use first
                reasoning = $"Both {firstProvider} and {secondProvider} agree on the decision: {firstDecision}";
            }
            else
            {
                disagreement = $"{firstProvider} suggests: {firstDecision}\n{secondProvider} suggests: {secondDecision}";
                // Prefer Anthropic for critical decisions
                recommended = firstProvider == "anthropic" ? first : second;
                reasoning = $"Providers disagree. Using {(firstProvider == "anthropic" ? firstProvider : secondProvider)} recommendation for critical decision.";
            }
        }
        else
        {
            // Text-based similarity
            var similarity = CalculateSimpleSimilarity(firstContent, secondContent);
            agrees = similarity > 0.7;

            if (agrees)
            {
                recommended = first;
                reasoning = $"Responses are {similarity:P0} similar. Providers generally agree.";
            }
            else
            {
                disagreement = "Providers gave substantially different responses";
                recommended = firstProvider == "anthropic" ? first : second;
                reasoning = $"Responses differ significantly. Using {(firstProvider == "anthropic" ? firstProvider : secondProvider)} recommendation.";
            }
        }

        return Task.FromResult(new SecondOpinionResult
        {
            FirstOpinion = first,
            SecondOpinion = second,
            FirstProvider = firstProvider,
            SecondProvider = secondProvider,
            Agrees = agrees,
            Disagreement = disagreement,
            RecommendedResponse = recommended,
            Reasoning = reasoning
        });
    }

    /// <summary>
    /// Synthesizes consensus from multiple provider responses.
    /// </summary>
    private Task<(LlmResponse response, double agreementScore, string method)> SynthesizeConsensusAsync(
        LlmResponse[] responses,
        string[] providers,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken)
    {
        if (responses.Length == 1)
        {
            return Task.FromResult((responses[0], 1.0, "single-provider"));
        }

        // Try to extract structured decisions
        var decisions = responses.Select(r => TryExtractDecision(r.Content)).ToArray();

        if (decisions.All(d => d != null))
        {
            // All providers returned structured decisions
            var distinctDecisions = decisions.Distinct().ToArray();

            if (distinctDecisions.Length == 1)
            {
                // Perfect consensus
                _logger.LogInformation("Perfect consensus: all providers agree");
                return Task.FromResult((responses[0], 1.0, "unanimous"));
            }

            // Majority vote
            var grouped = decisions
                .GroupBy(d => d)
                .OrderByDescending(g => g.Count())
                .First();

            var majorityDecision = grouped.Key;
            var majorityIndex = Array.IndexOf(decisions, majorityDecision);
            var agreementScore = (double)grouped.Count() / responses.Length;

            _logger.LogInformation("Majority consensus: {Score:P0} agree on {Decision}",
                agreementScore, majorityDecision);

            return Task.FromResult((responses[majorityIndex], agreementScore, "majority-vote"));
        }

        // Fall back to longest/most detailed response
        var longestResponse = responses.OrderByDescending(r => r.Content.Length).First();
        _logger.LogInformation("Using longest response for consensus ({Length} chars)", longestResponse.Content.Length);

        return Task.FromResult((longestResponse, 0.5, "longest-response"));
    }

    /// <summary>
    /// Tries to extract a decision/recommendation from response content.
    /// </summary>
    private string? TryExtractDecision(string content)
    {
        try
        {
            // Try to parse as JSON and extract key decision fields
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Look for common decision fields
            if (root.TryGetProperty("recommendation", out var rec))
            {
                return rec.GetString();
            }
            if (root.TryGetProperty("decision", out var dec))
            {
                return dec.GetString();
            }
            if (root.TryGetProperty("approach", out var app))
            {
                return app.GetString();
            }
            if (root.TryGetProperty("cloudProvider", out var cloud))
            {
                return cloud.GetString();
            }

            return null;
        }
        catch
        {
            // Not JSON or parsing failed
            return null;
        }
    }

    /// <summary>
    /// Calculates simple similarity between two texts (0.0 to 1.0).
    /// </summary>
    private double CalculateSimpleSimilarity(string text1, string text2)
    {
        var words1 = text1.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var words2 = text2.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (words1.Count == 0 || words2.Count == 0)
        {
            return 0;
        }

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return (double)intersection / union;
    }
}

internal sealed class ProviderProfile
{
    public string Name { get; init; } = string.Empty;
    public string[] StrengthCategories { get; init; } = Array.Empty<string>();
    public int AvgLatencyMs { get; init; }
    public decimal CostPer1kTokens { get; init; }
    public int MaxTokens { get; init; }
    public string BestFor { get; init; } = string.Empty;
}
