// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Cost;

/// <summary>
/// Tracks deployment costs and compares actual vs. estimated costs.
/// </summary>
public sealed class CostTrackingService
{
    private readonly string _costTrackingFile;

    public CostTrackingService(string workspacePath)
    {
        _costTrackingFile = Path.Combine(workspacePath, ".honua", "cost-tracking.json");
    }

    public async Task<CostTrackingData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_costTrackingFile))
        {
            return new CostTrackingData();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_costTrackingFile, cancellationToken);
            return JsonSerializer.Deserialize<CostTrackingData>(json) ?? new CostTrackingData();
        }
        catch
        {
            return new CostTrackingData();
        }
    }

    public async Task SaveAsync(CostTrackingData data, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_costTrackingFile);
        if (!directory.IsNullOrEmpty() && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = CliJsonOptions.Indented;

        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(_costTrackingFile, json, cancellationToken);
    }

    public async Task RecordEstimateAsync(
        string deploymentId,
        string architecture,
        decimal estimatedMonthlyCost,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken);

        data.Deployments[deploymentId] = new DeploymentCostRecord
        {
            DeploymentId = deploymentId,
            Architecture = architecture,
            EstimatedMonthlyCost = estimatedMonthlyCost,
            EstimatedAt = DateTime.UtcNow,
            ActualCosts = new List<ActualCostEntry>()
        };

        await SaveAsync(data, cancellationToken);
    }

    public async Task RecordActualCostAsync(
        string deploymentId,
        decimal actualCost,
        DateTime forMonth,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken);

        if (!data.Deployments.ContainsKey(deploymentId))
        {
            throw new InvalidOperationException($"Deployment {deploymentId} not found");
        }

        var deployment = data.Deployments[deploymentId];
        deployment.ActualCosts.Add(new ActualCostEntry
        {
            Month = forMonth,
            Amount = actualCost,
            RecordedAt = DateTime.UtcNow
        });

        await SaveAsync(data, cancellationToken);
    }

    public async Task<CostComparison> GetCostComparisonAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken);

        if (!data.Deployments.ContainsKey(deploymentId))
        {
            return new CostComparison
            {
                HasData = false,
                Message = $"No cost data found for deployment {deploymentId}"
            };
        }

        var deployment = data.Deployments[deploymentId];

        if (deployment.ActualCosts.Count == 0)
        {
            return new CostComparison
            {
                HasData = true,
                EstimatedMonthlyCost = deployment.EstimatedMonthlyCost,
                Message = "No actual costs recorded yet. Check back after your first billing cycle."
            };
        }

        var avgActualCost = deployment.ActualCosts.Average(c => c.Amount);
        var variance = avgActualCost - deployment.EstimatedMonthlyCost;
        var variancePercent = (variance / deployment.EstimatedMonthlyCost) * 100;

        var monthlyBreakdown = deployment.ActualCosts
            .OrderByDescending(c => c.Month)
            .Take(6)
            .Select(c => new MonthlyCostEntry
            {
                Month = c.Month.ToString("MMM yyyy"),
                Actual = c.Amount,
                Estimated = deployment.EstimatedMonthlyCost,
                Variance = c.Amount - deployment.EstimatedMonthlyCost
            })
            .ToList();

        var status = variancePercent switch
        {
            < -10 => "🎉 Under budget!",
            < 10 => "✅ On track",
            < 25 => "⚠️ Slightly over budget",
            _ => "🚨 Significantly over budget"
        };

        return new CostComparison
        {
            HasData = true,
            EstimatedMonthlyCost = deployment.EstimatedMonthlyCost,
            AverageActualCost = avgActualCost,
            Variance = variance,
            VariancePercent = variancePercent,
            Status = status,
            MonthlyBreakdown = monthlyBreakdown,
            Message = GenerateCostAnalysisMessage(deployment, avgActualCost, variancePercent)
        };
    }

    public async Task<string> GenerateCostReportAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken);

        if (data.Deployments.Count == 0)
        {
            return "No deployment cost data available yet.";
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        report.AppendLine("║                    Cost Tracking Report                       ║");
        report.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
        report.AppendLine();

        var totalEstimated = 0m;
        var totalActual = 0m;

        foreach (var (id, deployment) in data.Deployments)
        {
            report.AppendLine($"Deployment: {deployment.Architecture}");
            report.AppendLine($"ID: {id}");
            report.AppendLine($"Estimated: ${deployment.EstimatedMonthlyCost:N2}/month");

            if (deployment.ActualCosts.Any())
            {
                var avgActual = deployment.ActualCosts.Average(c => c.Amount);
                var variance = ((avgActual - deployment.EstimatedMonthlyCost) / deployment.EstimatedMonthlyCost) * 100;

                report.AppendLine($"Actual (avg): ${avgActual:N2}/month");
                report.AppendLine($"Variance: {variance:+0.0;-0.0}%");

                totalActual += avgActual;
            }
            else
            {
                report.AppendLine("Actual: (no data yet)");
            }

            totalEstimated += deployment.EstimatedMonthlyCost;
            report.AppendLine();
        }

        report.AppendLine("───────────────────────────────────────────────────────────────");
        report.AppendLine($"Total Estimated: ${totalEstimated:N2}/month");

        if (totalActual > 0)
        {
            report.AppendLine($"Total Actual: ${totalActual:N2}/month");
            var totalVariance = ((totalActual - totalEstimated) / totalEstimated) * 100;
            report.AppendLine($"Total Variance: {totalVariance:+0.0;-0.0}%");
        }

        return report.ToString();
    }

    private string GenerateCostAnalysisMessage(
        DeploymentCostRecord deployment,
        decimal avgActualCost,
        decimal variancePercent)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"## Cost Analysis - {deployment.Architecture}");
        sb.AppendLine();
        sb.AppendLine($"**Estimated**: ${deployment.EstimatedMonthlyCost:N2}/month");
        sb.AppendLine($"**Actual (average)**: ${avgActualCost:N2}/month");
        sb.AppendLine($"**Variance**: {variancePercent:+0.0;-0.0}%");
        sb.AppendLine();

        if (variancePercent < -10)
        {
            sb.AppendLine("🎉 Great news! You're spending less than estimated.");
            sb.AppendLine();
            sb.AppendLine("**Possible reasons:**");
            sb.AppendLine("- Lower than expected traffic");
            sb.AppendLine("- Effective caching reducing compute costs");
            sb.AppendLine("- Conservative estimates");
        }
        else if (variancePercent > 25)
        {
            sb.AppendLine("🚨 Your costs are significantly higher than estimated.");
            sb.AppendLine();
            sb.AppendLine("**Recommended actions:**");
            sb.AppendLine("1. Review usage patterns - are you seeing more traffic than expected?");
            sb.AppendLine("2. Check for resource waste (idle instances, unused storage)");
            sb.AppendLine("3. Consider optimization strategies:");
            sb.AppendLine("   - Enable auto-scaling to reduce idle resources");
            sb.AppendLine("   - Add caching layers to reduce database queries");
            sb.AppendLine("   - Use CDN more aggressively");
            sb.AppendLine("   - Consider reserved instances for predictable workloads");
            sb.AppendLine();
            sb.AppendLine("Would you like me to analyze your infrastructure for cost optimization opportunities?");
        }
        else
        {
            sb.AppendLine("✅ Your costs are tracking close to estimates.");
            sb.AppendLine();
            sb.AppendLine("This is normal variance and usually reflects:");
            sb.AppendLine("- Natural traffic fluctuations");
            sb.AppendLine("- Seasonal usage patterns");
            sb.AppendLine("- Minor architectural differences");
        }

        return sb.ToString();
    }
}

// Supporting types

public class CostTrackingData
{
    public Dictionary<string, DeploymentCostRecord> Deployments { get; set; } = new();
}

public class DeploymentCostRecord
{
    public string DeploymentId { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public decimal EstimatedMonthlyCost { get; set; }
    public DateTime EstimatedAt { get; set; }
    public List<ActualCostEntry> ActualCosts { get; set; } = new();
}

public class ActualCostEntry
{
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class CostComparison
{
    public bool HasData { get; set; }
    public decimal EstimatedMonthlyCost { get; set; }
    public decimal AverageActualCost { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercent { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<MonthlyCostEntry> MonthlyBreakdown { get; set; } = new();
}

public class MonthlyCostEntry
{
    public string Month { get; set; } = string.Empty;
    public decimal Actual { get; set; }
    public decimal Estimated { get; set; }
    public decimal Variance { get; set; }
}
