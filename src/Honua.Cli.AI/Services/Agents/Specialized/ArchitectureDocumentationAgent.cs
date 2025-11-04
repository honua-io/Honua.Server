// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Generates comprehensive architecture documentation for deployment plans:
/// - Architecture Decision Records (ADRs)
/// - Deployment topology explanations
/// - Requirements-to-architecture traceability
/// - Cloud resource inventory
/// - Network topology descriptions
/// - Security and compliance documentation
/// </summary>
public sealed class ArchitectureDocumentationAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<ArchitectureDocumentationAgent> _logger;

    public ArchitectureDocumentationAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<ArchitectureDocumentationAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates comprehensive architecture documentation from deployment plan.
    /// </summary>
    public async Task<ArchitectureDocumentation> GenerateAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generating architecture documentation for {CloudProvider}", request.CloudProvider);

            var doc = new ArchitectureDocumentation
            {
                Title = $"{request.CloudProvider} Deployment Architecture",
                GeneratedAt = DateTime.UtcNow,
                CloudProvider = request.CloudProvider
            };

            // Generate all documentation sections in parallel
            var tasks = new[]
            {
                GenerateExecutiveSummaryAsync(request, cancellationToken),
                GenerateArchitectureOverviewAsync(request, cancellationToken),
                GenerateRequirementsTraceabilityAsync(request, cancellationToken),
                GenerateTopologyDescriptionAsync(request, cancellationToken),
                GenerateResourceInventoryAsync(request, cancellationToken),
                GenerateSecurityConsiderationsAsync(request, cancellationToken),
                GenerateOperationalNotesAsync(request, cancellationToken)
            };

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            doc.ExecutiveSummary = results[0];
            doc.ArchitectureOverview = results[1];
            doc.RequirementsTraceability = results[2];
            doc.TopologyDescription = results[3];
            doc.ResourceInventory = results[4];
            doc.SecurityConsiderations = results[5];
            doc.OperationalNotes = results[6];

            return doc;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate architecture documentation");
            throw;
        }
    }

    /// <summary>
    /// Generates markdown document from architecture documentation.
    /// </summary>
    public string RenderMarkdown(ArchitectureDocumentation doc)
    {
        var md = new StringBuilder();

        md.AppendLine($"# {doc.Title}");
        md.AppendLine();
        md.AppendLine($"**Generated:** {doc.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
        md.AppendLine($"**Cloud Provider:** {doc.CloudProvider}");
        md.AppendLine();
        md.AppendLine("---");
        md.AppendLine();

        md.AppendLine("## Executive Summary");
        md.AppendLine();
        md.AppendLine(doc.ExecutiveSummary);
        md.AppendLine();

        md.AppendLine("## Architecture Overview");
        md.AppendLine();
        md.AppendLine(doc.ArchitectureOverview);
        md.AppendLine();

        md.AppendLine("## Requirements Traceability");
        md.AppendLine();
        md.AppendLine(doc.RequirementsTraceability);
        md.AppendLine();

        md.AppendLine("## Topology Description");
        md.AppendLine();
        md.AppendLine(doc.TopologyDescription);
        md.AppendLine();

        md.AppendLine("## Resource Inventory");
        md.AppendLine();
        md.AppendLine(doc.ResourceInventory);
        md.AppendLine();

        md.AppendLine("## Security Considerations");
        md.AppendLine();
        md.AppendLine(doc.SecurityConsiderations);
        md.AppendLine();

        md.AppendLine("## Operational Notes");
        md.AppendLine();
        md.AppendLine(doc.OperationalNotes);
        md.AppendLine();

        md.AppendLine("---");
        md.AppendLine();
        md.AppendLine("**References:**");
        md.AppendLine();
        md.AppendLine("- **ASCII Diagram:** See architecture diagram above for visual representation");
        md.AppendLine("- **Terraform Graph:** Run `terraform graph | dot -Tsvg > infrastructure.svg` for detailed dependency graph");
        md.AppendLine("- **IAM Policies:** See generated Terraform files for complete permission definitions");
        md.AppendLine("- **Metadata Configuration:** See metadata.json for Honua service configuration");

        return md.ToString();
    }

    private async Task<string> GenerateExecutiveSummaryAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate an executive summary for this cloud deployment:

Deployment Description: {request.DeploymentSummary}
Cloud Provider: {request.CloudProvider}
Plan Steps: {string.Join(", ", request.PlanSteps.Take(5))}

Write a 2-3 paragraph executive summary that covers:
1. Purpose and scope of the deployment
2. Key architectural decisions
3. Expected outcomes and benefits

Write in clear, professional language suitable for technical leadership.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 500,
            Temperature = 0.4
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating executive summary";
        }

        return response.Content.Trim();
    }

    private async Task<string> GenerateArchitectureOverviewAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate an architecture overview for this deployment:

Cloud Provider: {request.CloudProvider}
Deployment: {request.DeploymentSummary}
Steps: {string.Join("; ", request.PlanSteps)}

Describe the architecture in technical detail:

### Compute Layer
- Container orchestration platform (ECS, AKS, Cloud Run, Kubernetes, Docker)
- Scaling strategy (auto-scaling, replicas, resource limits)
- Service mesh or load balancing approach

### Data Layer
- Database type and configuration (PostGIS, managed RDS, etc)
- Storage services (S3, Azure Blob, GCS for raster data)
- Data persistence and backup strategy

### Network Layer
- VPC/VNet configuration
- Subnet design (public/private)
- Internet gateway, NAT gateway, load balancers
- Security groups and firewall rules

### Observability Layer
- Logging (CloudWatch, Azure Monitor, Cloud Logging)
- Metrics and monitoring
- Tracing and alerting

Use markdown formatting with clear headers.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1200,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating architecture overview";
        }

        return response.Content.Trim();
    }

    private async Task<string> GenerateRequirementsTraceabilityAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate a requirements-to-architecture traceability matrix:

User Requirements: {request.UserRequirements ?? request.DeploymentSummary}
Architecture Decisions: {string.Join("; ", request.PlanSteps)}

For each key user requirement, explain:
1. What was the requirement?
2. Which architectural component addresses it?
3. Why was this approach chosen?
4. What alternatives were considered?

Format as a markdown table:

| Requirement | Architectural Decision | Rationale | Alternatives Considered |
|-------------|----------------------|-----------|------------------------|
| ... | ... | ... | ... |

Or as a bulleted list if table format doesn't work well.

Focus on traceability: show how user needs drove specific technical decisions.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1000,
            Temperature = 0.4
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating documentation section";
        }

        return response.Content.Trim();
    }

    private async Task<string> GenerateTopologyDescriptionAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate a detailed network topology description:

Cloud Provider: {request.CloudProvider}
Deployment: {request.DeploymentSummary}

Describe the network topology in detail:

### VPC/Virtual Network Design
- CIDR blocks and IP addressing
- Subnet segmentation strategy
- Availability zones / regions

### Traffic Flow
1. Inbound traffic: Internet → Load Balancer → Compute
2. Internal traffic: Compute → Database, Compute → Storage
3. Outbound traffic: Compute → External APIs, monitoring

### Security Boundaries
- Public subnet resources (load balancers, bastion hosts)
- Private subnet resources (compute, databases)
- Egress-only resources (NAT gateway paths)
- Security groups and network ACLs

### DNS and Service Discovery
- Domain configuration
- Internal DNS resolution
- Service mesh or service discovery mechanism

Reference the ASCII network diagram for visual representation.
Use clear markdown formatting.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1000,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating documentation section";
        }

        return response.Content.Trim();
    }

    private async Task<string> GenerateResourceInventoryAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate a cloud resource inventory from these plan steps:

Cloud Provider: {request.CloudProvider}
Steps: {string.Join("; ", request.PlanSteps)}

Create a complete inventory of cloud resources being provisioned:

### Compute Resources
- Container services, clusters, task definitions
- Instance types, vCPU, memory configurations
- Auto-scaling policies

### Database Resources
- Database engines and versions
- Instance classes
- Storage volumes and IOPS
- Backup retention policies

### Storage Resources
- Object storage buckets
- Volume types and sizes
- Lifecycle policies

### Networking Resources
- VPCs/VNets, subnets, route tables
- Load balancers, target groups
- Internet/NAT gateways
- Security groups

### IAM and Security Resources
- Service accounts, roles, policies
- Secrets and credentials management
- TLS certificates

Format as markdown with clear sections. Include estimated costs if mentioned in the plan.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1200,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating documentation section";
        }

        return response.Content.Trim();
    }

    private async Task<string> GenerateSecurityConsiderationsAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate security and compliance documentation:

Cloud Provider: {request.CloudProvider}
Deployment: {request.DeploymentSummary}

Document security controls and considerations:

### Authentication and Authorization
- IAM roles and permissions (principle of least privilege)
- Service account configurations
- API key management

### Network Security
- Security group rules (ingress/egress)
- Network segmentation (public/private subnets)
- Firewall configurations
- DDoS protection

### Data Security
- Encryption at rest (database, storage)
- Encryption in transit (TLS/SSL)
- Database credentials management (secrets manager)
- Backup and recovery procedures

### Compliance Considerations
- Logging and audit trails
- Data sovereignty (region selection)
- GDPR/compliance requirements if applicable

### Security Best Practices
- Patch management strategy
- Container image scanning
- Vulnerability management
- Monitoring and alerting for security events

Reference the generated IAM policies for detailed permission documentation.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1000,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating documentation section";
        }

        return response.Content.Trim();
    }

    private async Task<string> GenerateOperationalNotesAsync(
        ArchitectureDocumentationRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate operational runbook notes:

Deployment Steps: {string.Join("; ", request.PlanSteps)}
Cloud Provider: {request.CloudProvider}

Document operational procedures:

### Deployment Process
- Deployment order and dependencies
- Expected deployment time
- Rollback procedures

### Post-Deployment Verification
- Health check endpoints to verify
- Smoke tests to run
- Monitoring dashboards to check

### Day-2 Operations
- Scaling procedures (when and how to scale)
- Log locations and analysis
- Common troubleshooting scenarios
- Backup and disaster recovery

### Maintenance Windows
- Database maintenance procedures
- Certificate rotation
- Security patching strategy

### Monitoring and Alerts
- Key metrics to watch
- Alert thresholds
- On-call runbook steps

### Cost Optimization
- Right-sizing recommendations
- Reserved instance opportunities
- Cost monitoring and budgets

Keep it practical and actionable for operations teams.";

        var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1000,
            Temperature = 0.4
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return "Error generating documentation section";
        }

        return response.Content.Trim();
    }
}

// Supporting types

public sealed class ArchitectureDocumentationRequest
{
    public string DeploymentSummary { get; init; } = string.Empty;
    public string CloudProvider { get; init; } = string.Empty;
    public List<string> PlanSteps { get; init; } = new();
    public string? UserRequirements { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

public sealed class ArchitectureDocumentation
{
    public string Title { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string CloudProvider { get; set; } = string.Empty;
    public string ExecutiveSummary { get; set; } = string.Empty;
    public string ArchitectureOverview { get; set; } = string.Empty;
    public string RequirementsTraceability { get; set; } = string.Empty;
    public string TopologyDescription { get; set; } = string.Empty;
    public string ResourceInventory { get; set; } = string.Empty;
    public string SecurityConsiderations { get; set; } = string.Empty;
    public string OperationalNotes { get; set; } = string.Empty;
}
