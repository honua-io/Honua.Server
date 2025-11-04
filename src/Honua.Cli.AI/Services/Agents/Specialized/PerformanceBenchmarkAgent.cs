// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.AI;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent for performance benchmarking, load testing, and capacity planning.
/// Helps users understand their deployment performance characteristics and optimize for scale.
/// </summary>
public sealed class PerformanceBenchmarkAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;

    public PerformanceBenchmarkAgent(Kernel kernel, ILlmProvider? llmProvider = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
    }

    /// <summary>
    /// Generates performance benchmarking plan and recommendations.
    /// </summary>
    public async Task<AgentStepResult> GenerateBenchmarkPlanAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Detect deployment type from workspace
            string deploymentType = DetectDeploymentType(context.WorkspacePath);
            string endpoint = DetectEndpoint(context.WorkspacePath, deploymentType);

            // Generate benchmark plan
            var plan = await GenerateBenchmarkingPlanAsync(request, deploymentType, endpoint, context, cancellationToken);

            // Save plan to workspace
            var planPath = System.IO.Path.Combine(context.WorkspacePath, "benchmark-plan.md");
            await System.IO.File.WriteAllTextAsync(planPath, plan, cancellationToken);

            return new AgentStepResult
            {
                AgentName = "PerformanceBenchmark",
                Action = "GenerateBenchmarkPlan",
                Success = true,
                Message = plan,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "PerformanceBenchmark",
                Action = "GenerateBenchmarkPlan",
                Success = false,
                Message = $"Error generating benchmark plan: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    [KernelFunction, Description("Generate comprehensive performance benchmarking plan")]
    private async Task<string> GenerateBenchmarkingPlanAsync(
        string request,
        string deploymentType,
        string endpoint,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (_llmProvider == null)
        {
            return GenerateDefaultBenchmarkPlan(request, deploymentType, endpoint);
        }

        var prompt = $@"Generate a comprehensive performance benchmarking and load testing plan for a GIS/geospatial tile server deployment.

**Deployment Context:**
- Type: {deploymentType}
- Endpoint: {endpoint}
- User Request: {request}

**Create a detailed plan that includes:**

1. **Testing Strategy**:
   - Baseline performance tests (single user, low load)
   - Load tests (expected production load)
   - Stress tests (find breaking point)
   - Spike tests (sudden traffic increases)
   - Soak tests (sustained load over time)

2. **Tools and Commands**:
   - Apache Bench (ab) commands for simple HTTP benchmarking
   - wrk/wrk2 commands for high-performance HTTP benchmarking
   - Locust scripts for distributed load testing
   - Artillery or k6 configurations if appropriate

3. **Key Metrics to Monitor**:
   - Latency (p50, p95, p99 percentiles)
   - Throughput (requests per second)
   - Error rates and failure patterns
   - Resource utilization (CPU, memory, network)
   - Database query performance
   - Cache hit rates

4. **Performance Targets** (for geospatial services):
   - Latency targets: p95 < 100ms, p99 < 200ms
   - Throughput targets: 1000+ req/sec per instance
   - Error rate: < 0.1%
   - Cache hit rate: > 90% for tiles

5. **Analysis Guidance**:
   - How to interpret results
   - Warning signs (exponential latency increase, high error rates, etc.)
   - Good performance indicators

6. **Optimization Recommendations**:
   - Caching strategies
   - Horizontal vs vertical scaling
   - CDN integration
   - Database optimization

7. **Auto-scaling Configuration** (if applicable for {deploymentType}):
   - Recommended HPA settings for Kubernetes
   - Auto-scaling rules for cloud platforms
   - Scale triggers based on metrics

Provide practical, executable commands and scripts that can be run immediately.
Format the response as markdown with clear sections and code blocks.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            Temperature = 0.3,
            MaxTokens = 3000
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success || response.Content.IsNullOrWhiteSpace())
        {
            return GenerateDefaultBenchmarkPlan(request, deploymentType, endpoint);
        }

        return response.Content;
    }

    private string GenerateDefaultBenchmarkPlan(string request, string deploymentType, string endpoint)
    {
        var requestLower = request.ToLowerInvariant();
        var plan = new StringBuilder();

        plan.AppendLine("# Performance Benchmarking Plan");
        plan.AppendLine();
        plan.AppendLine($"## Detected Configuration");
        plan.AppendLine($"- Deployment Type: **{deploymentType}**");
        plan.AppendLine($"- Endpoint: `{endpoint}`");
        plan.AppendLine();

        // Determine testing approach based on request
        bool needsBaseline = requestLower.Contains("baseline") || requestLower.Contains("initial");
        bool needsLoad = requestLower.Contains("load") || requestLower.Contains("production") || !needsBaseline;
        bool needsStress = requestLower.Contains("stress") || requestLower.Contains("limit") || requestLower.Contains("capacity");
        bool needsSpike = requestLower.Contains("spike") || requestLower.Contains("burst");
        bool needsSoak = requestLower.Contains("soak") || requestLower.Contains("sustained") || requestLower.Contains("endurance");

        plan.AppendLine("## Recommended Testing Strategy");
        plan.AppendLine();

        if (needsBaseline || (!needsLoad && !needsStress && !needsSpike && !needsSoak))
        {
            plan.AppendLine("### 1. Baseline Performance Test");
            plan.AppendLine("Establish normal performance with minimal load:");
            plan.AppendLine();
            plan.AppendLine("```bash");
            plan.AppendLine($"# Apache Bench: 100 requests, 1 concurrent");
            plan.AppendLine($"ab -n 100 -c 1 {endpoint}/health");
            plan.AppendLine();
            plan.AppendLine($"# wrk: 10 seconds, 1 thread, 1 connection");
            plan.AppendLine($"wrk -t1 -c1 -d10s {endpoint}/health");
            plan.AppendLine("```");
            plan.AppendLine();
        }

        if (needsLoad)
        {
            plan.AppendLine("### 2. Load Test (Expected Production Load)");
            plan.AppendLine("Simulate expected production traffic:");
            plan.AppendLine();
            plan.AppendLine("```bash");
            plan.AppendLine($"# Apache Bench: 10,000 requests, 50 concurrent users");
            plan.AppendLine($"ab -n 10000 -c 50 -k {endpoint}/");
            plan.AppendLine();
            plan.AppendLine($"# wrk: 60 seconds, 4 threads, 50 connections");
            plan.AppendLine($"wrk -t4 -c50 -d60s --latency {endpoint}/");
            plan.AppendLine("```");
            plan.AppendLine();
        }

        if (needsStress)
        {
            plan.AppendLine("### 3. Stress Test (Find Breaking Point)");
            plan.AppendLine("Gradually increase load:");
            plan.AppendLine();
            plan.AppendLine("```bash");
            plan.AppendLine("for c in 10 50 100 200 500 1000; do");
            plan.AppendLine($"  echo \"Testing with $c connections...\"");
            plan.AppendLine($"  wrk -t8 -c$c -d30s --latency {endpoint}/ > stress_${{c}}.log");
            plan.AppendLine("  sleep 10");
            plan.AppendLine("done");
            plan.AppendLine("```");
            plan.AppendLine();
        }

        plan.AppendLine("## Key Metrics to Monitor");
        plan.AppendLine();
        plan.AppendLine("### Application Metrics:");
        plan.AppendLine("- **Latency:** p50, p95, p99 response times");
        plan.AppendLine("- **Throughput:** Requests per second");
        plan.AppendLine("- **Error Rate:** Failed requests percentage");
        plan.AppendLine();
        plan.AppendLine("### System Metrics:");
        plan.AppendLine("- **CPU Usage:** Should stay below 80%");
        plan.AppendLine("- **Memory Usage:** Watch for leaks");
        plan.AppendLine("- **Network I/O:** Bandwidth utilization");
        plan.AppendLine();

        plan.AppendLine("## Performance Targets");
        plan.AppendLine();
        plan.AppendLine("For geospatial tile services:");
        plan.AppendLine("- **Latency:** p95 < 100ms, p99 < 200ms");
        plan.AppendLine("- **Throughput:** 1000+ req/sec per instance");
        plan.AppendLine("- **Error Rate:** < 0.1%");
        plan.AppendLine("- **Cache Hit Rate:** > 90% for tiles");
        plan.AppendLine();

        plan.AppendLine("## Next Steps");
        plan.AppendLine();
        plan.AppendLine("1. Start with baseline test");
        plan.AppendLine("2. Run load test with expected traffic");
        plan.AppendLine("3. Analyze results and identify bottlenecks");
        plan.AppendLine("4. Apply optimizations");
        plan.AppendLine("5. Re-test to validate improvements");
        plan.AppendLine();

        return plan.ToString();
    }

    private string DetectDeploymentType(string workspacePath)
    {
        // Check for deployment artifacts
        if (System.IO.Directory.Exists(System.IO.Path.Combine(workspacePath, "terraform-aws")) ||
            System.IO.File.Exists(System.IO.Path.Combine(workspacePath, "terraform-aws", "main.tf")))
        {
            var tfPath = System.IO.Path.Combine(workspacePath, "terraform-aws", "main.tf");
            if (System.IO.File.Exists(tfPath))
            {
                var content = System.IO.File.ReadAllText(tfPath);
                if (content.Contains("aws_lambda_function")) return "AWS Lambda";
                if (content.Contains("aws_ecs")) return "AWS ECS";
            }
            return "AWS";
        }

        if (System.IO.Directory.Exists(System.IO.Path.Combine(workspacePath, "terraform-azure")) ||
            System.IO.File.Exists(System.IO.Path.Combine(workspacePath, "terraform-azure", "main.tf")))
        {
            var tfPath = System.IO.Path.Combine(workspacePath, "terraform-azure", "main.tf");
            if (System.IO.File.Exists(tfPath))
            {
                var content = System.IO.File.ReadAllText(tfPath);
                if (content.Contains("azurerm_function_app")) return "Azure Functions";
                if (content.Contains("azurerm_container_app")) return "Azure Container Apps";
            }
            return "Azure";
        }

        if (System.IO.Directory.Exists(System.IO.Path.Combine(workspacePath, "terraform-gcp")) ||
            System.IO.File.Exists(System.IO.Path.Combine(workspacePath, "terraform-gcp", "main.tf")))
        {
            var tfPath = System.IO.Path.Combine(workspacePath, "terraform-gcp", "main.tf");
            if (System.IO.File.Exists(tfPath))
            {
                var content = System.IO.File.ReadAllText(tfPath);
                if (content.Contains("google_cloudfunctions2_function")) return "GCP Cloud Functions";
                if (content.Contains("google_cloud_run")) return "GCP Cloud Run";
            }
            return "GCP";
        }

        if (System.IO.File.Exists(System.IO.Path.Combine(workspacePath, "docker-compose.yml")))
            return "Docker Compose";

        var yamlFiles = System.IO.Directory.Exists(workspacePath)
            ? System.IO.Directory.GetFiles(workspacePath, "*.yaml")
            : Array.Empty<string>();

        foreach (var file in yamlFiles)
        {
            if (System.IO.File.ReadAllText(file).Contains("kind: Deployment"))
                return "Kubernetes";
        }

        return "Unknown";
    }

    private string DetectEndpoint(string workspacePath, string deploymentType)
    {
        return deploymentType switch
        {
            "Docker Compose" => "http://localhost:5000",
            "Kubernetes" => "http://localhost:30000",
            "AWS Lambda" or "AWS ECS" => "https://your-api-gateway.execute-api.us-west-2.amazonaws.com",
            "Azure Container Apps" or "Azure Functions" => "https://honua-prod.azurecontainerapps.io",
            "GCP Cloud Run" or "GCP Cloud Functions" => "https://honua-prod-abc123.run.app",
            _ => "http://localhost:5000"
        };
    }
}
