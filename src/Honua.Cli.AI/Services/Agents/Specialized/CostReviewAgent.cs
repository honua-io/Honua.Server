// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Cost review agent that analyzes infrastructure configurations for cost optimization opportunities.
/// Implements the Review & Critique pattern for cost validation.
/// </summary>
public sealed class CostReviewAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;

    public CostReviewAgent(Kernel kernel, ILlmProvider llmProvider)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
    }

    /// <summary>
    /// Reviews infrastructure code for cost optimization opportunities.
    /// </summary>
    public async Task<CostReviewResult> ReviewAsync(
        string artifactType,
        string content,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Quick heuristic checks
        var heuristicIssues = PerformHeuristicChecks(artifactType, content);

        // LLM-powered analysis
        var llmIssues = await PerformLlmReviewAsync(artifactType, content, context, cancellationToken);

        var allIssues = heuristicIssues.Concat(llmIssues).ToList();

        // Calculate estimated cost impact
        var estimatedMonthlySavings = allIssues.Sum(i => i.EstimatedMonthlySavingsUsd);
        var highImpactCount = allIssues.Count(i => i.Impact == "high");

        var approved = highImpactCount == 0 && estimatedMonthlySavings < 500;

        return new CostReviewResult
        {
            Approved = approved,
            TotalIssues = allIssues.Count,
            HighImpactIssues = highImpactCount,
            EstimatedMonthlySavings = estimatedMonthlySavings,
            Issues = allIssues,
            Recommendation = approved
                ? $"Cost review passed. Estimated monthly cost: reasonable. {allIssues.Count} minor optimizations available."
                : $"Cost review flagged {highImpactCount} high-impact issues. Potential savings: ${estimatedMonthlySavings:F0}/month. Review before deployment.",
            ReviewedAt = DateTime.UtcNow
        };
    }

    private List<CostIssue> PerformHeuristicChecks(string artifactType, string content)
    {
        var issues = new List<CostIssue>();

        if (artifactType.Contains("terraform", StringComparison.OrdinalIgnoreCase))
        {
            // Check for oversized RDS instances
            var rdsPatterns = new[]
            {
                @"instance_class\s*=\s*[""']db\.r[56]\.(8x|12x|16x|24x)large[""']",
                @"instance_class\s*=\s*[""']db\.m[56]\.(8x|12x|16x|24x)large[""']"
            };

            foreach (var pattern in rdsPatterns)
            {
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                {
                    issues.Add(new CostIssue
                    {
                        Impact = "high",
                        Category = "Oversized Database",
                        Description = "RDS instance type appears oversized for typical workloads",
                        Recommendation = "Consider right-sizing RDS instance. Start with smaller instance (db.t3.large or db.m5.xlarge) and scale based on metrics",
                        EstimatedMonthlySavingsUsd = 1200,
                        Location = "RDS instance_class configuration"
                    });
                    break;
                }
            }

            // Check for missing auto-scaling
            if (content.Contains("aws_ecs_service", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("aws_eks_cluster", StringComparison.OrdinalIgnoreCase))
            {
                if (!content.Contains("aws_appautoscaling", StringComparison.OrdinalIgnoreCase) &&
                    !content.Contains("autoscaling", StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new CostIssue
                    {
                        Impact = "medium",
                        Category = "Missing Auto-scaling",
                        Description = "Container services configured without auto-scaling",
                        Recommendation = "Implement auto-scaling to scale down during low traffic periods",
                        EstimatedMonthlySavingsUsd = 400,
                        Location = "ECS service or EKS node group configuration"
                    });
                }
            }

            // Check for expensive availability zones (multi-AZ without need)
            var azMatches = Regex.Matches(content, @"availability_zone\s*=", RegexOptions.IgnoreCase);
            if (azMatches.Count >= 3 && !content.Contains("multi_az", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CostIssue
                {
                    Impact = "medium",
                    Category = "Multi-AZ Without HA Config",
                    Description = "Resources spread across multiple AZs without high-availability configuration",
                    Recommendation = "For dev/test environments, consider single-AZ deployment. For production, ensure multi-AZ is intentional for HA",
                    EstimatedMonthlySavingsUsd = 300,
                    Location = "Resource availability_zone settings"
                });
            }

            // Check for missing S3 lifecycle policies
            if (content.Contains("aws_s3_bucket", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("lifecycle_rule", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("lifecycle_configuration", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CostIssue
                {
                    Impact = "medium",
                    Category = "Missing S3 Lifecycle",
                    Description = "S3 buckets without lifecycle policies for data tiering",
                    Recommendation = "Add lifecycle rules to transition old data to S3 Glacier or Intelligent-Tiering",
                    EstimatedMonthlySavingsUsd = 200,
                    Location = "S3 bucket configuration"
                });
            }

            // Check for expensive load balancers
            if (content.Contains("aws_lb\"", StringComparison.OrdinalIgnoreCase) &&
                !content.Contains("load_balancer_type", StringComparison.OrdinalIgnoreCase))
            {
                // Default is ALB which is more expensive than NLB for simple use cases
                issues.Add(new CostIssue
                {
                    Impact = "low",
                    Category = "Load Balancer Type",
                    Description = "Using Application Load Balancer (default) which may be more expensive than needed",
                    Recommendation = "If you only need TCP/UDP routing, Network Load Balancer is cheaper. For HTTP routing with WAF, ALB is appropriate",
                    EstimatedMonthlySavingsUsd = 50,
                    Location = "Load balancer configuration"
                });
            }

            // Check for NAT Gateway (expensive)
            if (content.Contains("aws_nat_gateway", StringComparison.OrdinalIgnoreCase))
            {
                var natCount = Regex.Matches(content, @"resource\s+[""']aws_nat_gateway[""']", RegexOptions.IgnoreCase).Count;
                if (natCount > 1)
                {
                    issues.Add(new CostIssue
                    {
                        Impact = "high",
                        Category = "Multiple NAT Gateways",
                        Description = $"{natCount} NAT Gateways configured (expensive: ~$32/month each + data transfer)",
                        Recommendation = "For dev/test, use single NAT Gateway or NAT Instance. For production multi-AZ, this is expected",
                        EstimatedMonthlySavingsUsd = natCount > 2 ? (natCount - 1) * 200 : 150,
                        Location = "NAT Gateway resources"
                    });
                }
            }

            // Check for provisioned IOPS (expensive)
            if (Regex.IsMatch(content, @"storage_type\s*=\s*[""']io[12][""']", RegexOptions.IgnoreCase))
            {
                issues.Add(new CostIssue
                {
                    Impact = "medium",
                    Category = "Provisioned IOPS",
                    Description = "Using provisioned IOPS storage (io1/io2) which is significantly more expensive",
                    Recommendation = "Use gp3 storage for most workloads (3000 IOPS baseline, scalable, cheaper). Reserve io1/io2 for extreme performance needs",
                    EstimatedMonthlySavingsUsd = 500,
                    Location = "EBS volume or RDS storage_type"
                });
            }
        }

        // Kubernetes-specific checks
        if (artifactType.Contains("kubernetes", StringComparison.OrdinalIgnoreCase))
        {
            // Check for missing resource requests (leads to over-provisioning)
            if (!content.Contains("requests:", StringComparison.Ordinal) &&
                content.Contains("kind: Deployment", StringComparison.Ordinal))
            {
                issues.Add(new CostIssue
                {
                    Impact = "medium",
                    Category = "Missing Resource Requests",
                    Description = "Pods without resource requests lead to inefficient cluster utilization",
                    Recommendation = "Set CPU and memory requests for accurate scheduling and cluster autoscaling",
                    EstimatedMonthlySavingsUsd = 350,
                    Location = "Container resources section"
                });
            }

            // Check for missing HPA (Horizontal Pod Autoscaler)
            if (content.Contains("kind: Deployment", StringComparison.Ordinal) &&
                !content.Contains("kind: HorizontalPodAutoscaler", StringComparison.Ordinal) &&
                !content.Contains("autoscaling", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CostIssue
                {
                    Impact = "medium",
                    Category = "No Horizontal Pod Autoscaling",
                    Description = "Deployments without HPA run fixed replica counts regardless of load",
                    Recommendation = "Add HorizontalPodAutoscaler to scale down during low-traffic periods",
                    EstimatedMonthlySavingsUsd = 400,
                    Location = "Deployment manifest"
                });
            }

            // Check for expensive storage classes
            if (content.Contains("storageClassName: premium", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("storageClassName: managed-premium", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new CostIssue
                {
                    Impact = "medium",
                    Category = "Premium Storage",
                    Description = "Using premium/SSD storage which is 3-5x more expensive than standard",
                    Recommendation = "Use standard storage for non-performance-critical data. Reserve premium for databases",
                    EstimatedMonthlySavingsUsd = 250,
                    Location = "PersistentVolumeClaim storageClassName"
                });
            }
        }

        return issues;
    }

    private async Task<List<CostIssue>> PerformLlmReviewAsync(
        string artifactType,
        string content,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"You are a FinOps expert specializing in cloud cost optimization.

Review infrastructure-as-code for cost optimization opportunities:

1. **Right-sizing**: Oversized instances, excessive storage, unused capacity
2. **Auto-scaling**: Fixed capacity vs dynamic scaling
3. **Reserved Instances**: On-demand usage where RIs/Savings Plans would save money
4. **Storage Tiering**: Missing lifecycle policies, expensive storage tiers
5. **Data Transfer**: Cross-region/AZ traffic, NAT Gateway costs
6. **Unused Resources**: Idle instances, unattached volumes, orphaned snapshots
7. **Serverless Opportunities**: VMs that could be replaced with Lambda/Functions
8. **Spot Instances**: Batch workloads not using spot/preemptible instances

Impact levels:
- **high**: >$500/month savings (oversized instances, expensive resources)
- **medium**: $100-500/month (missing auto-scaling, lifecycle policies)
- **low**: <$100/month (minor optimizations)

Return findings as JSON:
[
  {
    ""impact"": ""high"" | ""medium"" | ""low"",
    ""category"": ""string"",
    ""description"": ""string"",
    ""recommendation"": ""string"",
    ""estimatedMonthlySavingsUsd"": number,
    ""location"": ""string""
  }
]

If no cost issues, return: []";

        var userPrompt = $@"Review this {artifactType} for cost optimization:

```
{content}
```

Context:
- Environment: {(context.DryRun ? "dev/test" : "production")}
- Workspace: {context.WorkspacePath}

Return JSON array of cost issues.";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.2,
            MaxTokens = 1500
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return new List<CostIssue>();
        }

        try
        {
            var json = ExtractJson(response.Content);
            var issues = System.Text.Json.JsonSerializer.Deserialize<List<CostIssue>>(
                json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return issues ?? new List<CostIssue>();
        }
        catch
        {
            return new List<CostIssue>();
        }
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

public sealed class CostReviewResult
{
    public bool Approved { get; init; }
    public int TotalIssues { get; init; }
    public int HighImpactIssues { get; init; }
    public decimal EstimatedMonthlySavings { get; init; }
    public List<CostIssue> Issues { get; init; } = new();
    public string Recommendation { get; init; } = string.Empty;
    public DateTime ReviewedAt { get; init; }
}

public sealed class CostIssue
{
    public string Impact { get; init; } = "medium";
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public decimal EstimatedMonthlySavingsUsd { get; init; }
    public string Location { get; init; } = string.Empty;
}
