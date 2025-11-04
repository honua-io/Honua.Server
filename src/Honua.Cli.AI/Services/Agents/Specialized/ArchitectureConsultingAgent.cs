// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for architecture consulting, design trade-offs, and cost optimization.
/// Acts as a cloud GIS consultant helping users make informed decisions about their infrastructure.
/// </summary>
public sealed class ArchitectureConsultingAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<ArchitectureConsultingAgent> _logger;

    public ArchitectureConsultingAgent(Kernel kernel, ILlmProvider? llmProvider = null, ILogger<ArchitectureConsultingAgent>? logger = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ArchitectureConsultingAgent>.Instance;
    }

    /// <summary>
    /// Analyzes requirements and provides architecture recommendations with cost trade-offs.
    /// </summary>
    public async Task<AgentStepResult> AnalyzeArchitectureAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Extract requirements from the request
            var requirements = await ExtractRequirementsAsync(request, context, cancellationToken);

            // Generate architecture options
            var options = await GenerateArchitectureOptionsAsync(requirements, context, cancellationToken);

            // Analyze cost and trade-offs for each option
            var analysis = await AnalyzeTradeOffsAsync(options, requirements, context, cancellationToken);

            // Generate recommendation
            var recommendation = await GenerateRecommendationAsync(analysis, requirements, context, cancellationToken);

            return new AgentStepResult
            {
                AgentName = "ArchitectureConsulting",
                Action = "AnalyzeArchitecture",
                Success = true,
                Message = recommendation
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "ArchitectureConsulting",
                Action = "AnalyzeArchitecture",
                Success = false,
                Message = $"Error analyzing architecture: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Extract user requirements from natural language request")]
    private async Task<UserRequirements> ExtractRequirementsAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (_llmProvider == null)
        {
            return ExtractRequirementsHeuristic(request);
        }

        var prompt = $@"Extract infrastructure requirements from this user request:

""{request}""

Analyze and extract:
1. **Workload characteristics**:
   - Expected users/requests per day
   - Data volume (GB/TB)
   - Geographic distribution
   - Traffic patterns (steady, bursty, seasonal)

2. **Technical requirements**:
   - GIS capabilities needed (vector tiles, raster, WMS, WFS, etc.)
   - Database requirements (PostGIS, replication, etc.)
   - Performance targets (response time, throughput)
   - Compliance needs (HIPAA, SOC2, etc.)

3. **Budget constraints**:
   - Monthly budget range
   - Cost priority (minimize cost vs. maximize performance)
   - Willingness to manage infrastructure

4. **Team capabilities**:
   - DevOps experience level
   - Preferred cloud provider
   - Existing infrastructure

Respond with JSON:
{{
  ""workload"": {{
    ""expectedUsers"": ""<estimate>"",
    ""dataVolume"": ""<GB/TB>"",
    ""trafficPattern"": ""steady|bursty|seasonal"",
    ""geographicScope"": ""local|regional|global""
  }},
  ""technical"": {{
    ""gisCapabilities"": [""vector-tiles"", ""raster"", ""wms"", ""wfs""],
    ""databaseNeeds"": [""postgis"", ""replication"", ""backup""],
    ""performanceTargets"": {{
      ""maxResponseTime"": ""<ms>"",
      ""targetThroughput"": ""<requests/sec>""
    }}
  }},
  ""budget"": {{
    ""monthlyBudget"": ""<USD>"",
    ""costPriority"": ""low-cost|balanced|high-performance"",
    ""managementPreference"": ""managed|self-hosted|hybrid""
  }},
  ""team"": {{
    ""devopsExperience"": ""beginner|intermediate|advanced"",
    ""cloudPreference"": ""aws|azure|gcp|any"",
    ""existingInfra"": ""<description>""
  }}
}}

Return ONLY valid JSON.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            Temperature = 0.1,
            MaxTokens = 1000
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (response.Success)
        {
            try
            {
                var json = CleanJsonResponse(response.Content);
                var extracted = JsonSerializer.Deserialize<ExtractedRequirements>(json);

                if (extracted != null)
                {
                    return ConvertToUserRequirements(extracted);
                }
            }
            catch (JsonException ex)
            {
                // Log error and fall back to heuristic if JSON parsing fails
                _logger.LogDebug("Failed to deserialize requirements: {Message}", ex.Message);
            }
        }

        return ExtractRequirementsHeuristic(request);
    }

    [KernelFunction, Description("Generate architecture options based on requirements")]
    private Task<List<ArchitectureOption>> GenerateArchitectureOptionsAsync(
        UserRequirements requirements,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var options = new List<ArchitectureOption>();

        // Option 1: Serverless/Managed (lowest ops, higher per-request cost)
        options.Add(GenerateServerlessOption(requirements));

        // Option 2: Container-based (balanced)
        options.Add(GenerateContainerOption(requirements));

        // Option 3: VM-based (highest control, lowest per-request cost)
        if (requirements.Budget?.CostPriority != "low-cost" || requirements.DataVolume > 100)
        {
            options.Add(GenerateVmOption(requirements));
        }

        // Option 4: Hybrid (for specific use cases)
        if (requirements.TrafficPattern == "bursty" || requirements.GeographicScope == "global")
        {
            options.Add(GenerateHybridOption(requirements));
        }

        return Task.FromResult(options);
    }

    [KernelFunction, Description("Analyze cost and trade-offs for architecture options")]
    private Task<ArchitectureAnalysis> AnalyzeTradeOffsAsync(
        List<ArchitectureOption> options,
        UserRequirements requirements,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var option in options)
        {
            // Calculate estimated costs
            option.EstimatedMonthlyCost = CalculateMonthlyCost(option, requirements);
            option.EstimatedYearlyCost = option.EstimatedMonthlyCost * 12;

            // Rate complexity (1-10, lower is simpler)
            option.ComplexityRating = RateComplexity(option);

            // Rate scalability (1-10, higher is better)
            option.ScalabilityRating = RateScalability(option);

            // Rate operational burden (1-10, lower is less work)
            option.OperationalBurden = RateOperationalBurden(option);

            // Rate vendor lock-in (1-10, lower is more portable)
            option.VendorLockIn = RateVendorLockIn(option);
        }

        // Rank options based on requirements
        var rankedOptions = RankOptions(options, requirements);

        return Task.FromResult(new ArchitectureAnalysis
        {
            Options = rankedOptions,
            Requirements = requirements
        });
    }

    [KernelFunction, Description("Generate final recommendation with reasoning")]
    private Task<string> GenerateRecommendationAsync(
        ArchitectureAnalysis analysis,
        UserRequirements requirements,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Architecture Recommendations\n");

        sb.AppendLine("Based on your requirements, I've analyzed several architecture options:\n");

        var topOption = analysis.Options.First();
        sb.AppendLine($"### 🏆 Recommended: {topOption.Name}\n");
        sb.AppendLine($"**Estimated Cost**: ${topOption.EstimatedMonthlyCost:N0}/month (${topOption.EstimatedYearlyCost:N0}/year)");
        sb.AppendLine($"**Complexity**: {GetComplexityLabel(topOption.ComplexityRating)} ({topOption.ComplexityRating}/10)");
        sb.AppendLine($"**Scalability**: {GetScalabilityLabel(topOption.ScalabilityRating)} ({topOption.ScalabilityRating}/10)");
        sb.AppendLine($"**Ops Burden**: {GetOpsLabel(topOption.OperationalBurden)} ({topOption.OperationalBurden}/10)\n");

        sb.AppendLine($"**Why this option?**");
        sb.AppendLine($"{topOption.Recommendation}\n");

        sb.AppendLine($"**What you'll get:**");
        foreach (var component in topOption.Components)
        {
            sb.AppendLine($"- {component}");
        }
        sb.AppendLine();

        sb.AppendLine($"**Trade-offs:**");
        foreach (var tradeoff in topOption.TradeOffs)
        {
            sb.AppendLine($"- {tradeoff}");
        }
        sb.AppendLine();

        if (analysis.Options.Count > 1)
        {
            sb.AppendLine("### Alternative Options\n");
            foreach (var option in analysis.Options.Skip(1))
            {
                sb.AppendLine($"#### {option.Name}");
                sb.AppendLine($"- **Cost**: ${option.EstimatedMonthlyCost:N0}/month");
                sb.AppendLine($"- **Complexity**: {option.ComplexityRating}/10");
                sb.AppendLine($"- **Scalability**: {option.ScalabilityRating}/10");
                sb.AppendLine($"- **When to choose**: {option.BestFor}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("### Next Steps\n");
        sb.AppendLine("Would you like me to:");
        sb.AppendLine("1. Deploy the recommended architecture");
        sb.AppendLine("2. Explore one of the alternative options in detail");
        sb.AppendLine("3. Adjust the requirements (budget, performance, etc.)");
        sb.AppendLine("4. See a detailed cost breakdown");

        return Task.FromResult(sb.ToString());
    }

    // Helper methods for generating architecture options

    private ArchitectureOption GenerateServerlessOption(UserRequirements req)
    {
        var cloudProvider = req.Team?.CloudPreference ?? "gcp";
        var components = new List<string>();
        var tradeoffs = new List<string>();

        if (cloudProvider == "gcp")
        {
            components.Add("Cloud Run for Honua server (auto-scaling, pay-per-request)");
            components.Add("Cloud SQL for PostgreSQL with PostGIS");
            components.Add("Cloud Storage for raster data");
            components.Add("Cloud CDN for tile caching");
            components.Add("Cloud Memorystore (Redis) for session cache");

            tradeoffs.Add("✅ Zero ops - fully managed, auto-scales to zero");
            tradeoffs.Add("✅ Pay only for what you use");
            tradeoffs.Add("⚠️ Cold starts (1-2s for first request)");
            tradeoffs.Add("⚠️ Higher cost per request at scale");
            tradeoffs.Add("⚠️ Some GCP lock-in");
        }
        else if (cloudProvider == "aws")
        {
            components.Add("AWS Fargate for Honua server (serverless containers)");
            components.Add("Aurora Serverless PostgreSQL with PostGIS");
            components.Add("S3 for raster data");
            components.Add("CloudFront for tile caching");
            components.Add("ElastiCache Serverless (Redis)");

            tradeoffs.Add("✅ Minimal ops overhead");
            tradeoffs.Add("✅ Scales automatically");
            tradeoffs.Add("⚠️ Aurora Serverless can be slow to scale");
            tradeoffs.Add("⚠️ More complex networking (VPC, subnets)");
        }
        else // Azure
        {
            components.Add("Azure Container Instances for Honua");
            components.Add("Azure Database for PostgreSQL - Flexible Server");
            components.Add("Azure Blob Storage for rasters");
            components.Add("Azure CDN");
            components.Add("Azure Cache for Redis");

            tradeoffs.Add("✅ Simple deployment model");
            tradeoffs.Add("✅ Good integration with Azure services");
            tradeoffs.Add("⚠️ Container Instances can be expensive at scale");
            tradeoffs.Add("⚠️ Less auto-scaling sophistication vs. competitors");
        }

        return new ArchitectureOption
        {
            Name = $"Serverless/Managed ({cloudProvider.ToUpper()})",
            Type = "serverless",
            CloudProvider = cloudProvider,
            Components = components,
            TradeOffs = tradeoffs,
            BestFor = "Small to medium workloads, variable traffic, minimal ops team",
            Recommendation = "Best for getting started quickly with minimal operational overhead. You'll pay more per request, but save significantly on DevOps time. Great choice if your team is small or if you want to validate the product before investing in infrastructure.",
            ScalabilityRating = 8,
            ComplexityRating = 2,
            OperationalBurden = 1,
            VendorLockIn = 7
        };
    }

    private ArchitectureOption GenerateContainerOption(UserRequirements req)
    {
        var cloudProvider = req.Team?.CloudPreference ?? "any";
        var useKubernetes = req.Team?.DevOpsExperience == "advanced" || req.DataVolume > 500;

        var components = new List<string>();
        var tradeoffs = new List<string>();

        if (useKubernetes)
        {
            if (cloudProvider == "gcp" || cloudProvider == "any")
            {
                components.Add("GKE (Google Kubernetes Engine) with auto-scaling");
                components.Add("Cloud SQL for PostgreSQL (or self-hosted PostGIS in-cluster)");
                components.Add("Persistent volumes for raster data");
                components.Add("Nginx Ingress with Cloud CDN");
                components.Add("Redis deployed in-cluster");

                tradeoffs.Add("✅ Maximum flexibility and control");
                tradeoffs.Add("✅ Can run anywhere (multi-cloud, on-prem)");
                tradeoffs.Add("✅ Best price/performance at scale");
                tradeoffs.Add("⚠️ Requires Kubernetes expertise");
                tradeoffs.Add("⚠️ More complex operations and monitoring");
            }
            else if (cloudProvider == "aws")
            {
                components.Add("Amazon EKS (Elastic Kubernetes Service)");
                components.Add("RDS PostgreSQL with PostGIS");
                components.Add("EBS volumes for rasters");
                components.Add("ALB Ingress with CloudFront");
                components.Add("ElastiCache Redis");
            }
            else // Azure
            {
                components.Add("Azure Kubernetes Service (AKS)");
                components.Add("Azure Database for PostgreSQL");
                components.Add("Azure Disks for rasters");
                components.Add("Application Gateway with CDN");
                components.Add("Azure Cache for Redis");
            }

            return new ArchitectureOption
            {
                Name = "Kubernetes on Managed Cluster",
                Type = "kubernetes",
                CloudProvider = cloudProvider,
                Components = components,
                TradeOffs = tradeoffs,
                BestFor = "Medium to large workloads, experienced team, need for portability",
                Recommendation = "Kubernetes gives you maximum control and the best cost efficiency at scale. However, it requires a team comfortable with K8s operations. If you have the expertise, this is the most future-proof option.",
                ScalabilityRating = 10,
                ComplexityRating = 8,
                OperationalBurden = 7,
                VendorLockIn = 3
            };
        }
        else
        {
            // Docker Compose for development/small deployments
            components.Add("Docker Compose orchestration");
            components.Add("PostGIS container");
            components.Add("Redis container");
            components.Add("Nginx container for reverse proxy");
            components.Add("Volume mounts for raster data");
            components.Add("Deployed on a single VM or small cluster");

            tradeoffs.Add("✅ Simple to understand and deploy");
            tradeoffs.Add("✅ Easy local development parity");
            tradeoffs.Add("✅ Very cost-effective for small workloads");
            tradeoffs.Add("⚠️ Limited auto-scaling");
            tradeoffs.Add("⚠️ Single point of failure without clustering");
            tradeoffs.Add("⚠️ Manual scaling and updates");

            return new ArchitectureOption
            {
                Name = "Docker Compose on VM",
                Type = "docker",
                CloudProvider = "any",
                Components = components,
                TradeOffs = tradeoffs,
                BestFor = "Development, testing, small production workloads (<1000 users)",
                Recommendation = "Perfect for getting started or for small, stable workloads. You can always migrate to Kubernetes later if needed. Very cost-effective and simple to operate.",
                ScalabilityRating = 4,
                ComplexityRating = 3,
                OperationalBurden = 5,
                VendorLockIn = 1
            };
        }
    }

    private ArchitectureOption GenerateVmOption(UserRequirements req)
    {
        return new ArchitectureOption
        {
            Name = "Traditional VM-based Architecture",
            Type = "vm",
            CloudProvider = req.Team?.CloudPreference ?? "any",
            Components = new List<string>
            {
                "Compute VMs with auto-scaling groups",
                "Managed PostgreSQL with PostGIS",
                "Object storage for rasters",
                "Load balancer with SSL termination",
                "Redis for caching",
                "Monitoring and logging"
            },
            TradeOffs = new List<string>
            {
                "✅ Predictable costs with reserved instances",
                "✅ Maximum control over configuration",
                "✅ Lowest per-request cost at high scale",
                "⚠️ More operational overhead",
                "⚠️ Slower to scale than containers",
                "⚠️ Requires more infrastructure management"
            },
            BestFor = "High-volume, steady workloads where you can commit to reserved instances",
            Recommendation = "Best economics for high-volume steady workloads. If you can commit to 1-3 year reserved instances, this offers the lowest per-request cost. However, it requires more hands-on infrastructure management.",
            ScalabilityRating = 7,
            ComplexityRating = 6,
            OperationalBurden = 8,
            VendorLockIn = 5
        };
    }

    private ArchitectureOption GenerateHybridOption(UserRequirements req)
    {
        return new ArchitectureOption
        {
            Name = "Hybrid Multi-Region Architecture",
            Type = "hybrid",
            CloudProvider = req.Team?.CloudPreference ?? "gcp",
            Components = new List<string>
            {
                "Serverless compute in multiple regions (Cloud Run/Lambda)",
                "Primary PostgreSQL database with read replicas",
                "Global CDN for tile distribution",
                "Object storage with multi-region replication",
                "Redis clusters in each region",
                "Global load balancer"
            },
            TradeOffs = new List<string>
            {
                "✅ Best global performance (low latency everywhere)",
                "✅ High availability and disaster recovery",
                "✅ Can handle traffic spikes in any region",
                "⚠️ Highest cost due to multi-region data",
                "⚠️ Complex data consistency patterns",
                "⚠️ Requires careful data replication strategy"
            },
            BestFor = "Global applications with users across continents, mission-critical workloads",
            Recommendation = "This architecture provides the best global performance and reliability, but at a premium cost. Choose this if you have users worldwide and need to guarantee low latency everywhere, or if you have strict uptime requirements.",
            ScalabilityRating = 10,
            ComplexityRating = 9,
            OperationalBurden = 8,
            VendorLockIn = 6
        };
    }

    // Cost estimation methods

    private decimal CalculateMonthlyCost(ArchitectureOption option, UserRequirements req)
    {
        // Simplified cost model - in production, this would be much more detailed
        var baseCost = option.Type switch
        {
            "serverless" => EstimateServerlessCost(req),
            "kubernetes" => EstimateKubernetesCost(req),
            "docker" => EstimateDockerCost(req),
            "vm" => EstimateVmCost(req),
            "hybrid" => EstimateHybridCost(req),
            _ => 500m
        };

        // Adjust for data volume
        var storageCost = req.DataVolume * 0.023m; // ~$0.023/GB for cloud storage

        // Adjust for database size
        var dbCost = option.Type == "serverless" ? 100m : 200m;

        // CDN costs
        var cdnCost = req.GeographicScope == "global" ? 200m : 50m;

        return baseCost + storageCost + dbCost + cdnCost;
    }

    private decimal EstimateServerlessCost(UserRequirements req)
    {
        // Rough estimates based on moderate usage
        var expectedUsers = ParseUserCount(req.Workload?.ExpectedUsers ?? "1000");
        var requestsPerMonth = expectedUsers * 30 * 10; // 10 requests per user per day

        var computeCost = (requestsPerMonth / 1000000m) * 40m; // $40 per 1M requests
        var memoryTimeCost = (requestsPerMonth / 1000000m) * 25m; // $25 per 1M GB-seconds

        return computeCost + memoryTimeCost + 50m; // +$50 base costs
    }

    private decimal EstimateKubernetesCost(UserRequirements req)
    {
        // Cluster cost + node costs
        var clusterFee = 75m; // Managed K8s cluster fee
        var nodeCosts = 300m; // 3 nodes @ $100/month each
        return clusterFee + nodeCosts;
    }

    private decimal EstimateDockerCost(UserRequirements req)
    {
        // Single VM or small VM cluster
        return req.ExpectedUsers > 5000 ? 200m : 100m;
    }

    private decimal EstimateVmCost(UserRequirements req)
    {
        // VM cluster with load balancer
        var vmCount = Math.Max(2, req.ExpectedUsers / 2500);
        return vmCount * 120m; // $120 per VM/month
    }

    private decimal EstimateHybridCost(UserRequirements req)
    {
        // Multi-region costs are ~3x single region
        var baseCost = EstimateServerlessCost(req);
        return baseCost * 3m;
    }

    // Rating methods

    private int RateComplexity(ArchitectureOption option)
    {
        return option.Type switch
        {
            "docker" => 3,
            "serverless" => 2,
            "vm" => 6,
            "kubernetes" => 8,
            "hybrid" => 9,
            _ => 5
        };
    }

    private int RateScalability(ArchitectureOption option)
    {
        return option.Type switch
        {
            "docker" => 4,
            "serverless" => 8,
            "vm" => 7,
            "kubernetes" => 10,
            "hybrid" => 10,
            _ => 6
        };
    }

    private int RateOperationalBurden(ArchitectureOption option)
    {
        return option.Type switch
        {
            "docker" => 5,
            "serverless" => 1,
            "vm" => 8,
            "kubernetes" => 7,
            "hybrid" => 8,
            _ => 5
        };
    }

    private int RateVendorLockIn(ArchitectureOption option)
    {
        return option.Type switch
        {
            "docker" => 1,
            "serverless" => 7,
            "vm" => 5,
            "kubernetes" => 3,
            "hybrid" => 6,
            _ => 5
        };
    }

    private List<ArchitectureOption> RankOptions(List<ArchitectureOption> options, UserRequirements req)
    {
        // Score each option based on requirements
        foreach (var option in options)
        {
            var score = 0m;

            // Cost priority
            if (req.Budget?.CostPriority == "low-cost")
            {
                score += (1000m - option.EstimatedMonthlyCost) / 100m;
            }
            else if (req.Budget?.CostPriority == "high-performance")
            {
                score += option.ScalabilityRating * 10;
            }
            else // balanced
            {
                score += (1000m - option.EstimatedMonthlyCost) / 200m;
                score += option.ScalabilityRating * 5;
            }

            // DevOps experience
            if (req.Team?.DevOpsExperience == "beginner")
            {
                score += (10 - option.ComplexityRating) * 5;
                score += (10 - option.OperationalBurden) * 5;
            }

            // Management preference
            if (req.Budget?.ManagementPreference == "managed")
            {
                score += (10 - option.OperationalBurden) * 8;
            }

            option.Score = score;
        }

        return options.OrderByDescending(o => o.Score).ToList();
    }

    // Utility methods

    private UserRequirements ExtractRequirementsHeuristic(string request)
    {
        var lower = request.ToLowerInvariant();

        return new UserRequirements
        {
            ExpectedUsers = lower.Contains("large") || lower.Contains("enterprise") ? 50000 :
                          lower.Contains("medium") ? 10000 : 1000,
            DataVolume = lower.Contains("large data") || lower.Contains("tb") ? 1000 :
                        lower.Contains("gb") ? 100 : 10,
            TrafficPattern = lower.Contains("bursty") || lower.Contains("spike") ? "bursty" :
                           lower.Contains("seasonal") ? "seasonal" : "steady",
            GeographicScope = lower.Contains("global") || lower.Contains("worldwide") ? "global" :
                            lower.Contains("regional") ? "regional" : "local",
            Budget = new BudgetConstraints
            {
                CostPriority = lower.Contains("cheap") || lower.Contains("low cost") ? "low-cost" :
                             lower.Contains("performance") || lower.Contains("fast") ? "high-performance" : "balanced",
                ManagementPreference = lower.Contains("managed") || lower.Contains("serverless") ? "managed" :
                                     lower.Contains("kubernetes") || lower.Contains("docker") ? "self-hosted" : "hybrid"
            },
            Team = new TeamCapabilities
            {
                DevOpsExperience = lower.Contains("kubernetes") || lower.Contains("advanced") ? "advanced" :
                                 lower.Contains("beginner") || lower.Contains("simple") ? "beginner" : "intermediate",
                CloudPreference = lower.Contains("aws") ? "aws" :
                                lower.Contains("azure") ? "azure" :
                                lower.Contains("gcp") || lower.Contains("google") ? "gcp" : "any"
            }
        };
    }

    private UserRequirements ConvertToUserRequirements(ExtractedRequirements extracted)
    {
        return new UserRequirements
        {
            ExpectedUsers = ParseUserCount(extracted.Workload?.ExpectedUsers ?? "1000"),
            DataVolume = ParseDataVolume(extracted.Workload?.DataVolume ?? "10GB"),
            TrafficPattern = extracted.Workload?.TrafficPattern ?? "steady",
            GeographicScope = extracted.Workload?.GeographicScope ?? "local",
            Budget = extracted.Budget,
            Team = extracted.Team,
            Workload = extracted.Workload,
            Technical = extracted.Technical
        };
    }

    private int ParseUserCount(string input)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, @"(\d+)[kK]?");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
        {
            return input.ToLowerInvariant().Contains("k") ? num * 1000 : num;
        }
        return 1000;
    }

    private int ParseDataVolume(string input)
    {
        var match = System.Text.RegularExpressions.Regex.Match(input, @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num))
        {
            return input.ToUpperInvariant().Contains("TB") ? num * 1000 : num;
        }
        return 10;
    }

    private string CleanJsonResponse(string response)
    {
        var cleaned = response.Trim();
        if (cleaned.StartsWith("```json"))
            cleaned = cleaned.Substring(7);
        if (cleaned.StartsWith("```"))
            cleaned = cleaned.Substring(3);
        if (cleaned.EndsWith("```"))
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        return cleaned.Trim();
    }

    private string GetComplexityLabel(int rating) => rating switch
    {
        <= 3 => "Simple",
        <= 6 => "Moderate",
        _ => "Complex"
    };

    private string GetScalabilityLabel(int rating) => rating switch
    {
        <= 4 => "Limited",
        <= 7 => "Good",
        _ => "Excellent"
    };

    private string GetOpsLabel(int rating) => rating switch
    {
        <= 3 => "Low",
        <= 6 => "Moderate",
        _ => "High"
    };
}

// Supporting types

public class UserRequirements
{
    public int ExpectedUsers { get; set; }
    public int DataVolume { get; set; }
    public string TrafficPattern { get; set; } = "steady";
    public string GeographicScope { get; set; } = "local";
    public BudgetConstraints? Budget { get; set; }
    public TeamCapabilities? Team { get; set; }
    public WorkloadCharacteristics? Workload { get; set; }
    public TechnicalRequirements? Technical { get; set; }
}

public class ExtractedRequirements
{
    public WorkloadCharacteristics? Workload { get; set; }
    public TechnicalRequirements? Technical { get; set; }
    public BudgetConstraints? Budget { get; set; }
    public TeamCapabilities? Team { get; set; }
}

public class WorkloadCharacteristics
{
    public string? ExpectedUsers { get; set; }
    public string? DataVolume { get; set; }
    public string? TrafficPattern { get; set; }
    public string? GeographicScope { get; set; }
}

public class TechnicalRequirements
{
    public List<string>? GisCapabilities { get; set; }
    public List<string>? DatabaseNeeds { get; set; }
    public PerformanceTargets? PerformanceTargets { get; set; }
}

public class PerformanceTargets
{
    public string? MaxResponseTime { get; set; }
    public string? TargetThroughput { get; set; }
}

public class BudgetConstraints
{
    public string? MonthlyBudget { get; set; }
    public string? CostPriority { get; set; }
    public string? ManagementPreference { get; set; }
}

public class TeamCapabilities
{
    public string? DevOpsExperience { get; set; }
    public string? CloudPreference { get; set; }
    public string? ExistingInfra { get; set; }
}

public class ArchitectureOption
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string CloudProvider { get; set; } = string.Empty;
    public List<string> Components { get; set; } = new();
    public List<string> TradeOffs { get; set; } = new();
    public string BestFor { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;

    public decimal EstimatedMonthlyCost { get; set; }
    public decimal EstimatedYearlyCost { get; set; }
    public int ComplexityRating { get; set; }
    public int ScalabilityRating { get; set; }
    public int OperationalBurden { get; set; }
    public int VendorLockIn { get; set; }
    public decimal Score { get; set; }
}

public class ArchitectureAnalysis
{
    public List<ArchitectureOption> Options { get; set; } = new();
    public UserRequirements Requirements { get; set; } = new();
}
