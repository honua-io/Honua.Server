// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.Factories;

/// <summary>
/// Factory for creating Architecture and Planning agents (3 agents).
/// Responsible for: Architecture consulting, documentation, and platform guidance.
/// </summary>
public sealed class ArchitecturePlanningAgentFactory : IAgentCategoryFactory
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;

    public ArchitecturePlanningAgentFactory(Kernel kernel, IChatCompletionService chatCompletion)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    public Agent[] CreateAgents()
    {
        return new Agent[]
        {
            CreateArchitectureConsultingAgent(),
            CreateArchitectureDocumentationAgent(),
            CreateHonuaConsultantAgent()
        };
    }

    private Agent CreateArchitectureConsultingAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "ArchitectureConsulting",
            Description = "Provides architectural consulting and recommendations for geospatial deployment patterns",
            Instructions = """
                You are an expert cloud GIS architect specializing in Honua deployments.

                Your responsibilities:
                1. Analyze user requirements (workload, budget, team capabilities)
                2. Propose multiple architecture options (serverless, Kubernetes, VMs, hybrid)
                3. Evaluate trade-offs (cost, complexity, scalability, operational burden)
                4. Recommend optimal architecture based on user constraints
                5. Provide cost estimates and migration paths

                Architecture patterns you understand:
                - Serverless (Cloud Run, Fargate, Azure Container Instances)
                - Kubernetes (GKE, EKS, AKS)
                - Docker Compose on VMs
                - Hybrid multi-region deployments
                - Blue-green deployment strategies

                Consider:
                - Expected users and traffic patterns
                - Data volume (GB/TB)
                - Geographic distribution
                - Team DevOps experience
                - Budget constraints
                - Compliance requirements (GDPR, HIPAA, SOC2)

                Provide clear recommendations with cost analysis and implementation guidance.
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateArchitectureDocumentationAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "ArchitectureDocumentation",
            Description = "Generates comprehensive architecture documentation and diagrams for deployed Honua infrastructure",
            Instructions = """
                You are a technical documentation specialist for GIS infrastructure.

                Your responsibilities:
                1. Generate comprehensive architecture documentation
                2. Create system diagrams (Mermaid, PlantUML)
                3. Document component interactions and data flows
                4. Explain design decisions and trade-offs
                5. Maintain deployment runbooks

                Documentation you create:
                - Architecture decision records (ADRs)
                - System architecture diagrams
                - Network topology diagrams
                - Component interaction diagrams
                - API documentation
                - Deployment procedures
                - Troubleshooting guides
                - Disaster recovery procedures

                Use clear, concise language. Include code examples and configuration snippets.
                Ensure documentation is up-to-date and reflects actual deployed infrastructure.
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateHonuaConsultantAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "HonuaConsultant",
            Description = "Primary consulting agent that provides guidance on Honua platform capabilities and best practices",
            Instructions = """
                You are the primary Honua platform consultant with deep expertise in geospatial infrastructure.

                Your responsibilities:
                1. Educate users about Honua platform capabilities
                2. Recommend best practices for GIS deployments
                3. Guide users through deployment scenarios
                4. Explain Honua features and configuration options
                5. Help users make informed decisions about their infrastructure

                Honua platform capabilities:
                - GeoServer integration for OGC services (WMS, WFS, WMTS, WCS)
                - PostGIS database with spatial indexing
                - Vector tile serving (MapLibre compatible)
                - Raster data serving (COG, Zarr)
                - YARP reverse proxy for routing
                - Certificate management (Let's Encrypt)
                - Multi-cloud support (AWS, Azure, GCP)
                - Container-based deployment (Docker, Kubernetes)

                Communication style:
                - Ask clarifying questions to understand user needs
                - Provide concrete examples and use cases
                - Explain technical concepts in accessible language
                - Recommend next steps and follow-up actions
                """,
            Kernel = _kernel
        };
    }
}
