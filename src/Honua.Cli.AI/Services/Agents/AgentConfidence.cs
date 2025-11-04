// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Confidence scoring for agent selection based on task match and historical performance.
/// Similar to PatternConfidence but for multi-agent coordination.
/// </summary>
public sealed class AgentConfidence
{
    public string AgentName { get; init; } = string.Empty;
    public double TaskMatchScore { get; init; }  // How well agent matches task type (0-1)
    public double HistoricalSuccessRate { get; init; }  // Past performance (0-1)
    public int CompletedTasks { get; init; }
    public double Overall { get; init; }  // Composite score
    public string Level { get; init; } = string.Empty;  // High/Medium/Low
    public string Explanation { get; init; } = string.Empty;

    /// <summary>
    /// Calculate agent confidence score for a given task.
    /// Formula: (taskMatch * 0.5) + (successRate * 0.4) + (min(completedTasks/20, 1.0) * 0.1)
    /// </summary>
    public static AgentConfidence Calculate(
        string agentName,
        double taskMatchScore,
        double historicalSuccessRate,
        int completedTasks)
    {
        ArgumentNullException.ThrowIfNull(agentName);

        // Evidence score: saturates at 20 completed tasks
        var evidenceScore = Math.Min(completedTasks / 20.0, 1.0);

        // Composite score: task match is most important (50%), then success rate (40%), then evidence (10%)
        var overall = (taskMatchScore * 0.5) + (historicalSuccessRate * 0.4) + (evidenceScore * 0.1);

        var level = overall switch
        {
            >= 0.8 => "High",
            >= 0.6 => "Medium",
            _ => "Low"
        };

        var explanation = GenerateExplanation(agentName, taskMatchScore, historicalSuccessRate, completedTasks, overall, level);

        return new AgentConfidence
        {
            AgentName = agentName,
            TaskMatchScore = taskMatchScore,
            HistoricalSuccessRate = historicalSuccessRate,
            CompletedTasks = completedTasks,
            Overall = overall,
            Level = level,
            Explanation = explanation
        };
    }

    private static string GenerateExplanation(
        string agentName,
        double taskMatch,
        double successRate,
        int completedTasks,
        double overall,
        string level)
    {
        if (level == "High")
        {
            return $"High confidence in {agentName}. {taskMatch:P0} task match, {successRate:P0} success rate over {completedTasks} tasks. This agent is well-suited for this request.";
        }

        if (level == "Medium")
        {
            var reason = taskMatch < 0.7 ? "task match could be better" :
                        successRate < 0.7 ? "historical success rate is moderate" :
                        "limited historical evidence";
            return $"Medium confidence in {agentName}. Overall {overall:P0} score - {reason}. Agent can handle this but may not be optimal.";
        }

        var issue = taskMatch < 0.5 ? "poor task match" :
                   successRate < 0.5 ? "low success rate" :
                   "insufficient historical data";
        return $"Low confidence in {agentName}. {issue} ({overall:P0} overall). Consider alternative agent or manual review.";
    }

    public override string ToString() => Explanation;
}

/// <summary>
/// Performance statistics for an agent over a time period.
/// </summary>
public sealed class AgentPerformanceStats
{
    public string AgentName { get; init; } = string.Empty;
    public int TotalInteractions { get; init; }
    public int SuccessfulInteractions { get; init; }
    public int FailedInteractions { get; init; }
    public double AverageConfidence { get; init; }
    public double SuccessRate => TotalInteractions > 0 ? (double)SuccessfulInteractions / TotalInteractions : 0;
    public TimeSpan AverageExecutionTime { get; init; }

    public string Summary =>
        $"{AgentName}: {SuccessRate:P0} success ({SuccessfulInteractions}/{TotalInteractions}), avg confidence {AverageConfidence:P0}";
}
