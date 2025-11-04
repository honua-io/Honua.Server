// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Represents confidence scores for a pattern recommendation.
/// Combines multiple factors to assess recommendation quality.
/// </summary>
public sealed class PatternConfidence
{
    /// <summary>
    /// Vector similarity score from semantic search (0.0 to 1.0).
    /// </summary>
    public double VectorSimilarity { get; init; }

    /// <summary>
    /// Historical success rate of this pattern (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Number of successful deployments using this pattern.
    /// </summary>
    public int DeploymentCount { get; init; }

    /// <summary>
    /// Composite confidence score (0.0 to 1.0).
    /// Formula: (similarity * 0.4) + (successRate * 0.4) + (min(deploymentCount/50, 1.0) * 0.2)
    /// </summary>
    public double Overall { get; init; }

    /// <summary>
    /// Confidence level: "High" (&gt;= 0.8), "Medium" (&gt;= 0.6), or "Low" (&lt; 0.6).
    /// </summary>
    public string Level { get; init; } = "Unknown";

    /// <summary>
    /// Human-readable explanation of the confidence score.
    /// </summary>
    public string Explanation { get; init; } = string.Empty;

    public static PatternConfidence Calculate(PatternSearchResult result)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        // Normalize deployment count (saturates at 50 deployments = 1.0)
        var deploymentScore = Math.Min(result.DeploymentCount / 50.0, 1.0);

        // Weighted composite score
        // 40% vector similarity (how well it matches the query)
        // 40% success rate (how often it succeeds)
        // 20% deployment count (how much evidence we have)
        var overall = (result.Score * 0.4) +
                      (result.SuccessRate * 0.4) +
                      (deploymentScore * 0.2);

        var level = overall switch
        {
            >= 0.8 => "High",
            >= 0.6 => "Medium",
            _ => "Low"
        };

        var explanation = GenerateExplanation(result, overall, level);

        return new PatternConfidence
        {
            VectorSimilarity = result.Score,
            SuccessRate = result.SuccessRate,
            DeploymentCount = result.DeploymentCount,
            Overall = overall,
            Level = level,
            Explanation = explanation
        };
    }

    private static string GenerateExplanation(
        PatternSearchResult result,
        double overall,
        string level)
    {
        var parts = new System.Collections.Generic.List<string>();

        // Explain vector similarity
        if (result.Score >= 0.9)
            parts.Add("excellent semantic match");
        else if (result.Score >= 0.7)
            parts.Add("good semantic match");
        else if (result.Score >= 0.5)
            parts.Add("moderate semantic match");
        else
            parts.Add("weak semantic match");

        // Explain success rate
        if (result.SuccessRate >= 0.95)
            parts.Add($"exceptional {result.SuccessRate:P0} success rate");
        else if (result.SuccessRate >= 0.85)
            parts.Add($"strong {result.SuccessRate:P0} success rate");
        else if (result.SuccessRate >= 0.70)
            parts.Add($"solid {result.SuccessRate:P0} success rate");
        else
            parts.Add($"moderate {result.SuccessRate:P0} success rate");

        // Explain deployment count
        if (result.DeploymentCount >= 50)
            parts.Add($"extensive production evidence ({result.DeploymentCount}+ deployments)");
        else if (result.DeploymentCount >= 20)
            parts.Add($"strong production evidence ({result.DeploymentCount} deployments)");
        else if (result.DeploymentCount >= 10)
            parts.Add($"good production evidence ({result.DeploymentCount} deployments)");
        else if (result.DeploymentCount >= 5)
            parts.Add($"moderate production evidence ({result.DeploymentCount} deployments)");
        else
            parts.Add($"limited production evidence ({result.DeploymentCount} deployment{(result.DeploymentCount == 1 ? "" : "s")})");

        var explanation = $"{level} confidence: {string.Join(", ", parts)}.";

        // Add warning for low confidence
        if (level == "Low")
        {
            explanation += " Consider reviewing alternatives or requesting manual review.";
        }

        return explanation;
    }

    public override string ToString() =>
        $"{Level} confidence ({Overall:P0}): {Explanation}";
}

/// <summary>
/// Extension methods for PatternSearchResult confidence calculations.
/// </summary>
public static class PatternSearchResultExtensions
{
    /// <summary>
    /// Calculates the confidence score for this pattern recommendation.
    /// </summary>
    public static PatternConfidence GetConfidence(this PatternSearchResult result)
        => PatternConfidence.Calculate(result);
}
