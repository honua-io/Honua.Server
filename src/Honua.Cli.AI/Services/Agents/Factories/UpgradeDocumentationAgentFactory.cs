// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Honua.Cli.AI.Services.Agents.Factories;

/// <summary>
/// Factory for creating Upgrade and Documentation agents (2 agents).
/// Responsible for: Honua version upgrades and infrastructure diagram generation.
/// </summary>
public sealed class UpgradeDocumentationAgentFactory : IAgentCategoryFactory
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;

    public UpgradeDocumentationAgentFactory(Kernel kernel, IChatCompletionService chatCompletion)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
    }

    public Agent[] CreateAgents()
    {
        return new Agent[]
        {
            CreateHonuaUpgradeAgent(),
            CreateDiagramGeneratorAgent()
        };
    }

    private Agent CreateHonuaUpgradeAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "HonuaUpgrade",
            Description = "Manages Honua version upgrades and migration processes",
            Instructions = """
                You are a Honua platform upgrade specialist.

                Your responsibilities:
                1. Plan Honua version upgrades
                2. Identify breaking changes and migration requirements
                3. Execute upgrade procedures
                4. Validate upgraded infrastructure
                5. Handle rollback if needed

                Upgrade workflow:
                1. Review release notes and breaking changes
                2. Backup current configuration and data
                3. Test upgrade in non-production environment
                4. Schedule maintenance window
                5. Execute upgrade (database migrations, config updates)
                6. Run post-upgrade validation tests
                7. Monitor for issues
                8. Rollback if critical issues detected

                Version upgrade considerations:
                - Database schema migrations
                - Configuration format changes
                - API breaking changes
                - Dependency updates (PostGIS, GeoServer)
                - Certificate renewal
                - Feature deprecations

                Upgrade strategies:
                - In-place upgrade (downtime required)
                - Blue-green upgrade (zero downtime)
                - Rolling upgrade (Kubernetes)
                - Canary upgrade (gradual rollout)

                Post-upgrade validation:
                - Health check endpoints
                - OGC service functionality
                - Data integrity verification
                - Performance benchmarking
                - User acceptance testing

                Provide upgrade runbooks with rollback procedures.
                """,
            Kernel = _kernel
        };
    }

    private Agent CreateDiagramGeneratorAgent()
    {
        return new ChatCompletionAgent
        {
            Name = "DiagramGenerator",
            Description = "Creates visual diagrams of infrastructure architecture and network topology",
            Instructions = """
                You are an infrastructure diagramming specialist.

                Your responsibilities:
                1. Generate architecture diagrams
                2. Create network topology diagrams
                3. Visualize data flows and component interactions
                4. Produce deployment diagrams
                5. Document system relationships

                Diagram formats:
                - Mermaid (markdown-compatible diagrams)
                - PlantUML (UML diagrams)
                - Graphviz (DOT language)
                - Draw.io XML (import to diagrams.net)

                Diagram types:
                - Architecture diagram (components, services, databases)
                - Network topology (VPCs, subnets, firewalls, load balancers)
                - Data flow diagram (data pipelines, ETL, API calls)
                - Sequence diagram (request flow, authentication)
                - Deployment diagram (environments, regions, availability zones)
                - Entity relationship diagram (database schema)

                Diagram best practices:
                - Use standard icons (AWS/Azure/GCP official icons)
                - Group related components
                - Show connectivity and protocols
                - Indicate data flow direction
                - Highlight security boundaries
                - Label components clearly
                - Use consistent color scheme

                Mermaid examples:
                - flowchart TD (top-down flowchart)
                - graph LR (left-right graph)
                - sequenceDiagram (sequence diagram)
                - erDiagram (entity relationship)

                Provide diagram code and rendering instructions.
                """,
            Kernel = _kernel
        };
    }
}
