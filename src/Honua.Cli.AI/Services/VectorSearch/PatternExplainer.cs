// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Generates human-readable explanations for why deployment patterns match requirements.
/// Uses LLM to provide context-aware reasoning about pattern recommendations.
/// </summary>
public sealed class PatternExplainer
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<PatternExplainer> _logger;

    public PatternExplainer(
        ILlmProvider llmProvider,
        ILogger<PatternExplainer> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates an explanation for why a pattern matches the given requirements.
    /// </summary>
    /// <param name="pattern">The recommended deployment pattern</param>
    /// <param name="requirements">The user's deployment requirements</param>
    /// <param name="confidence">The confidence score for this recommendation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A 2-3 sentence explanation of why this pattern is suitable</returns>
    public async Task<string> ExplainPatternAsync(
        PatternSearchResult pattern,
        DeploymentRequirements requirements,
        PatternConfidence confidence,
        CancellationToken cancellationToken = default)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (requirements == null)
            throw new ArgumentNullException(nameof(requirements));
        if (confidence == null)
            throw new ArgumentNullException(nameof(confidence));

        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(pattern, requirements, confidence);

            var llmRequest = new LlmRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                Temperature = 0.3,  // Lower temperature for consistent, factual explanations
                MaxTokens = 250
            };

            var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

            if (!response.Success)
            {
                _logger.LogWarning(
                    "Failed to generate pattern explanation: {Error}",
                    response.ErrorMessage);

                return GenerateFallbackExplanation(pattern, requirements, confidence);
            }

            _logger.LogDebug(
                "Generated explanation for pattern {PatternName} using {Tokens} tokens",
                pattern.PatternName,
                response.TotalTokens);

            return response.Content.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating pattern explanation");
            return GenerateFallbackExplanation(pattern, requirements, confidence);
        }
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a deployment consultant explaining why a specific infrastructure pattern
matches a user's requirements. Be concise, specific, and technical. Focus on the key factors
that make this pattern suitable: data handling capability, scalability for users, and any
important trade-offs.

Format your response as 2-3 clear sentences. Use specific numbers from the pattern's track record.
Be honest about trade-offs (cost, complexity, etc.) if relevant.";
    }

    private static string BuildUserPrompt(
        PatternSearchResult pattern,
        DeploymentRequirements requirements,
        PatternConfidence confidence)
    {
        return $@"Explain why this deployment pattern matches the user's needs:

**Pattern**: {pattern.PatternName}
- Cloud: {pattern.CloudProvider}
- Success Rate: {pattern.SuccessRate:P0} ({pattern.DeploymentCount} deployments)
- Configuration: {pattern.ConfigurationJson}
- Confidence: {confidence.Level} ({confidence.Overall:P0})

**User Requirements**:
- Data Volume: {requirements.DataVolumeGb}GB
- Concurrent Users: {requirements.ConcurrentUsers}
- Cloud: {requirements.CloudProvider}
- Region: {requirements.Region}

Provide a 2-3 sentence explanation covering:
1. Why this configuration handles the data volume effectively
2. How it scales for the number of concurrent users
3. One key benefit or trade-off to be aware of

Keep it technical but clear. Use specific numbers from the pattern's track record.";
    }

    private static string GenerateFallbackExplanation(
        PatternSearchResult pattern,
        DeploymentRequirements requirements,
        PatternConfidence confidence)
    {
        // Generate a simple template-based explanation if LLM fails
        var successPhrase = pattern.SuccessRate >= 0.9
            ? "has an excellent track record"
            : "has proven reliable";

        var deploymentPhrase = pattern.DeploymentCount >= 20
            ? $"validated across {pattern.DeploymentCount} production deployments"
            : $"used in {pattern.DeploymentCount} deployment{(pattern.DeploymentCount == 1 ? "" : "s")}";

        return $"This {pattern.CloudProvider} pattern {successPhrase} with a " +
               $"{pattern.SuccessRate:P0} success rate, {deploymentPhrase}. " +
               $"The configuration is designed to handle {requirements.DataVolumeGb}GB of data " +
               $"with {requirements.ConcurrentUsers} concurrent users. " +
               $"Confidence: {confidence.Level} ({confidence.Overall:P0}).";
    }

    /// <summary>
    /// Generates a brief one-sentence summary of the pattern.
    /// </summary>
    public static string GenerateSummary(PatternSearchResult pattern)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));

        return $"{pattern.PatternName} on {pattern.CloudProvider}: " +
               $"{pattern.SuccessRate:P0} success rate across " +
               $"{pattern.DeploymentCount} deployment{(pattern.DeploymentCount == 1 ? "" : "s")}";
    }
}
