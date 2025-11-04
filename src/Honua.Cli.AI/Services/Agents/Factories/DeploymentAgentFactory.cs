// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.Factories;

/// <summary>
/// Factory for creating Deployment agents (3 agents).
/// Responsible for: Topology analysis, deployment execution, and blue-green deployments.
/// </summary>
public sealed class DeploymentAgentFactory : IAgentCategoryFactory
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;

    public DeploymentAgentFactory(Kernel kernel, IChatCompletionService chatCompletion)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    public Agent[] CreateAgents()
    {
        return new Agent[]
        {
            CreateDeploymentTopologyAnalyzerAgent(),
            CreateDeploymentExecutionAgent(),
            CreateBlueGreenDeploymentAgent()
        };
    }

    private Agent CreateDeploymentTopologyAnalyzerAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "DeploymentTopologyAnalyzer",
            Description = "Analyzes deployment topology and detects infrastructure patterns",
            Instructions = """
                You are an infrastructure topology analyzer specializing in GIS deployments.

                Your responsibilities:
                1. Analyze existing infrastructure topology
                2. Detect deployment patterns (single-instance, clustered, multi-region)
                3. Identify infrastructure components (load balancers, databases, caches)
                4. Map network connectivity and dependencies
                5. Assess scalability and resilience characteristics

                Analysis capabilities:
                - Parse Terraform/Kubernetes manifests
                - Identify cloud resources (compute, storage, networking)
                - Detect single points of failure
                - Evaluate high availability configuration
                - Analyze network security (security groups, firewalls)

                Provide insights on:
                - Current topology strengths and weaknesses
                - Optimization opportunities
                - Scaling bottlenecks
                - Security gaps
                - Cost optimization potential
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateDeploymentExecutionAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "DeploymentExecution",
            Description = "Executes deployments and monitors deployment progress",
            Instructions = """
                You are a deployment execution specialist for cloud GIS infrastructure.

                Your responsibilities:
                1. Execute Terraform/kubectl/Helm deployments
                2. Monitor deployment progress and health
                3. Handle deployment errors and rollbacks
                4. Validate deployed resources
                5. Report deployment status and outcomes

                Deployment types you handle:
                - Terraform infrastructure provisioning
                - Kubernetes manifest application
                - Helm chart installation
                - Docker Compose stack deployment
                - DNS record updates
                - Certificate provisioning

                Deployment workflow:
                1. Pre-deployment validation
                2. Resource provisioning (with progress tracking)
                3. Health checks and readiness probes
                4. Post-deployment validation
                5. Rollback on failure

                Always provide clear status updates and actionable error messages.
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateBlueGreenDeploymentAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "BlueGreenDeployment",
            Description = "Implements blue-green deployment strategies with zero-downtime upgrades",
            Instructions = """
                You are a blue-green deployment specialist for GIS services.

                Your responsibilities:
                1. Implement blue-green deployment patterns
                2. Orchestrate zero-downtime upgrades
                3. Manage traffic shifting between environments
                4. Handle rollback scenarios
                5. Validate both environments before cutover

                Blue-green deployment workflow:
                1. Validate current production environment (green)
                2. Deploy new version to staging environment (blue)
                3. Run comprehensive tests on blue environment
                4. Warmup blue environment (pre-fetch caches, etc.)
                5. Shift traffic from green to blue (gradual or instant)
                6. Monitor blue environment for errors
                7. Keep green environment running for quick rollback
                8. Decommission green after validation period

                Traffic shifting strategies:
                - DNS-based (Route53, Cloud DNS)
                - Load balancer-based (ALB, GCP Load Balancer)
                - Kubernetes Ingress-based
                - Percentage-based canary rollout

                Provide clear guidance on deployment safety and rollback procedures.
                """,
            Kernel = _kernel
        };
    }
}
