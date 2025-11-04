// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;

using Microsoft.Extensions.Logging;
using Honua.Cli.AI.Serialization;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Coordinates a swarm of architecture agents to collaboratively design infrastructure.
/// Implements the Swarm pattern for architecture decisions with debate and consensus.
/// This feeds the learning loop by tracking which recommendations users accept.
/// </summary>
public sealed class ArchitectureSwarmCoordinator
{
    private readonly ILlmProvider _llmProvider;
    private readonly IPatternUsageTelemetry? _telemetry;
    private readonly ILogger<ArchitectureSwarmCoordinator> _logger;

    public ArchitectureSwarmCoordinator(
        ILlmProvider llmProvider,
        ILogger<ArchitectureSwarmCoordinator> logger,
        IPatternUsageTelemetry? telemetry = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry;
    }

    /// <summary>
    /// Runs a swarm of architecture agents with different optimization goals.
    /// They debate and refine recommendations collaboratively.
    /// </summary>
    public async Task<SwarmConsensusResult> GenerateArchitectureOptionsAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting architecture swarm for request: {Request}", request);

        // Define swarm agents with different optimization objectives
        var swarmAgents = new[]
        {
            new SwarmAgent
            {
                Name = "CostOptimizer",
                Objective = "Minimize infrastructure cost while meeting requirements",
                Bias = "Prefers serverless, spot instances, managed services, auto-scaling"
            },
            new SwarmAgent
            {
                Name = "PerformanceOptimizer",
                Objective = "Maximize performance and minimize latency",
                Bias = "Prefers dedicated resources, CDN, read replicas, caching layers"
            },
            new SwarmAgent
            {
                Name = "SimplicityAdvocate",
                Objective = "Minimize operational complexity and maintenance burden",
                Bias = "Prefers managed services, fewer moving parts, standard patterns"
            },
            new SwarmAgent
            {
                Name = "ScalabilityArchitect",
                Objective = "Design for massive scale and global distribution",
                Bias = "Prefers Kubernetes, multi-region, horizontal scaling, event-driven"
            }
        };

        // Round 1: Each agent proposes their architecture independently
        var proposals = await GenerateInitialProposalsAsync(swarmAgents, request, context, cancellationToken);

        // Round 2: Agents critique each other's proposals
        var critiques = await GenerateCritiquesAsync(swarmAgents, proposals, request, context, cancellationToken);

        // Round 3: Synthesize consensus with tradeoff analysis
        var consensus = await SynthesizeConsensusAsync(proposals, critiques, request, context, cancellationToken);

        _logger.LogInformation("Architecture swarm completed. Generated {Count} options with consensus analysis.",
            consensus.Options.Count);

        return consensus;
    }

    /// <summary>
    /// Round 1: Each agent generates their architecture proposal.
    /// </summary>
    private async Task<List<ArchitectureProposal>> GenerateInitialProposalsAsync(
        SwarmAgent[] agents,
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Swarm Round 1: Generating {Count} independent proposals", agents.Length);

        var tasks = agents.Select(agent => GenerateProposalAsync(agent, request, context, cancellationToken));
        var proposals = await Task.WhenAll(tasks).ConfigureAwait(false);

        return proposals.Where(p => p != null).ToList()!;
    }

    private async Task<ArchitectureProposal?> GenerateProposalAsync(
        SwarmAgent agent,
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = $@"You are the {agent.Name}, a specialized architecture consultant.

**Your Objective**: {agent.Objective}
**Your Bias**: {agent.Bias}

You are part of a swarm designing infrastructure for Honua, a geospatial tile server.

Your task:
1. Analyze the request from YOUR perspective (focus on {agent.Objective.ToLowerInvariant()})
2. Propose a concrete architecture (cloud provider, compute, database, storage, networking)
3. Estimate cost/month, performance metrics, complexity score (1-10)
4. Explain why YOUR approach is best from your optimization goal

Be opinionated. Push for your objective. The swarm will debate and find balance.

Return JSON:
{{
  ""approach"": ""string (high-level strategy)"",
  ""cloudProvider"": ""aws|azure|gcp|multi-cloud"",
  ""compute"": ""string (ECS/Lambda/Kubernetes/etc)"",
  ""database"": ""string (RDS PostgreSQL/Aurora Serverless/etc)"",
  ""storage"": ""string (S3/Blob/Cloud Storage)"",
  ""caching"": ""string (CDN/Redis/none)"",
  ""estimatedCostPerMonth"": number,
  ""estimatedLatencyP95Ms"": number,
  ""complexityScore"": number (1-10, where 1=simple, 10=complex),
  ""strengths"": [""string""],
  ""tradeoffs"": [""string""],
  ""rationale"": ""string (why this optimizes for your goal)""
}}";

        var userPrompt = $@"Analyze this deployment request from the perspective of {agent.Name}:

**Request**: {request}

**Context**:
- Workspace: {context.WorkspacePath}
- Mode: {(context.DryRun ? "planning" : "execution")}

Propose YOUR optimal architecture (optimized for {agent.Objective.ToLowerInvariant()}).";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.4, // Allow some creativity for diverse proposals
            MaxTokens = 1000
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogWarning("{Agent} failed to generate proposal", agent.Name);
            return null;
        }

        try
        {
            var json = ExtractJson(response.Content);
            var proposal = JsonSerializer.Deserialize<ArchitectureProposal>(
                json,
                CliJsonOptions.DevTooling);

            if (proposal != null)
            {
                proposal.ProposedBy = agent.Name;
            }

            return proposal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Agent} proposal parsing failed", agent.Name);
            return null;
        }
    }

    /// <summary>
    /// Round 2: Each agent critiques other agents' proposals.
    /// </summary>
    private async Task<List<SwarmCritique>> GenerateCritiquesAsync(
        SwarmAgent[] agents,
        List<ArchitectureProposal> proposals,
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Swarm Round 2: Generating critiques");

        var critiques = new List<SwarmCritique>();

        foreach (var agent in agents)
        {
            var otherProposals = proposals.Where(p => p.ProposedBy != agent.Name).ToList();

            if (otherProposals.Count == 0)
            {
                continue;
            }

            var critique = await GenerateCritiqueAsync(agent, otherProposals, request, context, cancellationToken);
            if (critique != null)
            {
                critiques.Add(critique);
            }
        }

        return critiques;
    }

    private async Task<SwarmCritique?> GenerateCritiqueAsync(
        SwarmAgent agent,
        List<ArchitectureProposal> otherProposals,
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var proposalsSummary = new StringBuilder();
        foreach (var proposal in otherProposals)
        {
            proposalsSummary.AppendLine($"**{proposal.ProposedBy}**: {proposal.Approach}");
            proposalsSummary.AppendLine($"  - Provider: {proposal.CloudProvider}, Compute: {proposal.Compute}");
            proposalsSummary.AppendLine($"  - Cost: ${proposal.EstimatedCostPerMonth}/mo, Complexity: {proposal.ComplexityScore}/10");
            proposalsSummary.AppendLine($"  - Rationale: {proposal.Rationale}");
            proposalsSummary.AppendLine();
        }

        var systemPrompt = $@"You are the {agent.Name}.

Your objective: {agent.Objective}

Review the other agents' architecture proposals and provide constructive critique from YOUR perspective.

Identify:
1. **Concerns**: Issues from your optimization perspective
2. **Risks**: What could go wrong with their approach
3. **Suggestions**: How to improve while respecting their goals

Be collaborative but honest. Point out genuine issues.

Return JSON:
{{
  ""concerns"": [
    {{
      ""proposalBy"": ""string (agent name)"",
      ""issue"": ""string"",
      ""severity"": ""high|medium|low""
    }}
  ],
  ""suggestions"": [
    {{
      ""proposalBy"": ""string (agent name)"",
      ""suggestion"": ""string""
    }}
  ]
}}";

        var userPrompt = $@"Review these architecture proposals from the {agent.Name} perspective:

{proposalsSummary}

**Original Request**: {request}

Provide critique focusing on {agent.Objective.ToLowerInvariant()}.";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.3,
            MaxTokens = 800
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            return null;
        }

        try
        {
            var json = ExtractJson(response.Content);
            var critique = JsonSerializer.Deserialize<SwarmCritique>(
                json,
                CliJsonOptions.DevTooling);

            if (critique != null)
            {
                critique.CriticAgent = agent.Name;
            }

            return critique;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Round 3: Synthesize consensus and present options with tradeoff analysis.
    /// </summary>
    private async Task<SwarmConsensusResult> SynthesizeConsensusAsync(
        List<ArchitectureProposal> proposals,
        List<SwarmCritique> critiques,
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Swarm Round 3: Synthesizing consensus");

        var proposalsSummary = JsonSerializer.Serialize(proposals, CliJsonOptions.Indented);
        var critiquesSummary = JsonSerializer.Serialize(critiques, CliJsonOptions.Indented);

        var systemPrompt = @"You are a senior architect synthesizing recommendations from a swarm of specialized agents.

The swarm proposed different architectures optimized for cost, performance, simplicity, and scalability.
Each agent also critiqued the others' proposals.

Your task:
1. Identify the **top 3 viable options** (may be hybrid/refined versions of proposals)
2. For each option, provide clear **tradeoff analysis**
3. Recommend **which option for which use case** (e.g., ""Option 1 for dev/test, Option 2 for production"")
4. Identify **consensus items** (things all agents agreed on)
5. Identify **contentious items** (where agents disagreed significantly)

Return JSON:
{
  ""consensusItems"": [""string""],
  ""contentiousItems"": [
    {
      ""topic"": ""string"",
      ""perspectives"": [""string""]
    }
  ],
  ""recommendedOptions"": [
    {
      ""name"": ""string (e.g., 'Cost-Optimized', 'Performance-Optimized')"",
      ""approach"": ""string"",
      ""cloudProvider"": ""string"",
      ""compute"": ""string"",
      ""database"": ""string"",
      ""storage"": ""string"",
      ""caching"": ""string"",
      ""estimatedCostPerMonth"": number,
      ""estimatedLatencyP95Ms"": number,
      ""complexityScore"": number,
      ""strengths"": [""string""],
      ""weaknesses"": [""string""],
      ""bestFor"": ""string (use case description)""
    }
  ],
  ""recommendation"": ""string (overall guidance on which option to choose and why)""
}";

        var userPrompt = $@"Synthesize these swarm proposals and critiques into final recommendations:

**Original Request**: {request}

**Proposals**:
```json
{proposalsSummary}
```

**Critiques**:
```json
{critiquesSummary}
```

Synthesize the swarm's collective intelligence into 3 concrete, actionable options with clear tradeoffs.";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.3,
            MaxTokens = 2000
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogWarning("Consensus synthesis failed. Returning raw proposals.");
            return new SwarmConsensusResult
            {
                Options = proposals.Select(p => new ArchitectureOption
                {
                    Name = p.ProposedBy ?? "Unknown",
                    Approach = p.Approach ?? "No approach specified",
                    CloudProvider = p.CloudProvider ?? "unspecified",
                    Compute = p.Compute ?? "unspecified",
                    Database = p.Database ?? "unspecified",
                    Storage = p.Storage ?? "unspecified",
                    Caching = p.Caching,
                    EstimatedCostPerMonth = p.EstimatedCostPerMonth,
                    EstimatedLatencyP95Ms = p.EstimatedLatencyP95Ms,
                    ComplexityScore = p.ComplexityScore,
                    Strengths = p.Strengths ?? new List<string>(),
                    Weaknesses = new List<string>(),
                    BestFor = "See rationale"
                }).ToList(),
                Recommendation = "Swarm synthesis failed. Review individual proposals.",
                ConsensusItems = new List<string>(),
                ContentiousItems = new List<ContentiousItem>()
            };
        }

        try
        {
            var json = ExtractJson(response.Content);
            var consensus = JsonSerializer.Deserialize<SwarmConsensusResult>(
                json,
                CliJsonOptions.DevTooling);

            return consensus ?? new SwarmConsensusResult
            {
                Options = new List<ArchitectureOption>(),
                Recommendation = "Failed to parse consensus",
                ConsensusItems = new List<string>(),
                ContentiousItems = new List<ContentiousItem>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse consensus result");
            return new SwarmConsensusResult
            {
                Options = new List<ArchitectureOption>(),
                Recommendation = "Failed to parse consensus",
                ConsensusItems = new List<string>(),
                ContentiousItems = new List<ContentiousItem>()
            };
        }
    }

    /// <summary>
    /// Tracks which architecture option the user selected (for learning loop).
    /// </summary>
    public async Task TrackUserSelectionAsync(
        string selectedOption,
        SwarmConsensusResult consensus,
        string request,
        CancellationToken cancellationToken)
    {
        if (_telemetry == null)
        {
            return;
        }

        var selected = consensus.Options.FirstOrDefault(o =>
            o.Name.Equals(selectedOption, StringComparison.OrdinalIgnoreCase));

        if (selected == null)
        {
            _logger.LogWarning("User selected unknown option: {Option}", selectedOption);
            return;
        }

        _logger.LogInformation("User selected architecture: {Option} ({Provider}, {Compute})",
            selected.Name, selected.CloudProvider, selected.Compute);

        // Track acceptance in telemetry for learning
        try
        {
            // Build list of all options presented
            var optionsPresented = consensus.Options.Select(o => o.Name).ToList();

            // This feeds the knowledge base with user preferences
            var metadata = new Dictionary<string, object>
            {
                ["selected_option"] = selected.Name,
                ["cloud_provider"] = selected.CloudProvider,
                ["compute_type"] = selected.Compute,
                ["database"] = selected.Database,
                ["storage"] = selected.Storage,
                ["caching"] = selected.Caching ?? "none",
                ["estimated_cost"] = selected.EstimatedCostPerMonth,
                ["estimated_latency_p95_ms"] = selected.EstimatedLatencyP95Ms,
                ["complexity_score"] = selected.ComplexityScore,
                ["total_options_presented"] = consensus.Options.Count,
                ["strengths_count"] = selected.Strengths.Count,
                ["weaknesses_count"] = selected.Weaknesses.Count,
                ["request"] = request
            };

            // Actually call the telemetry service to track the swarm recommendation
            await _telemetry.TrackArchitectureSwarmAsync(
                request,
                optionsPresented,
                selected.Name,
                metadata,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Tracked architecture selection for learning: {Metadata}",
                JsonSerializer.Serialize(metadata));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track user selection");
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

public sealed class SwarmAgent
{
    public string Name { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string Bias { get; init; } = string.Empty;
}

public sealed class ArchitectureProposal
{
    public string? ProposedBy { get; set; }
    public string? Approach { get; init; }
    public string? CloudProvider { get; init; }
    public string? Compute { get; init; }
    public string? Database { get; init; }
    public string? Storage { get; init; }
    public string? Caching { get; init; }
    public decimal EstimatedCostPerMonth { get; init; }
    public int EstimatedLatencyP95Ms { get; init; }
    public int ComplexityScore { get; init; }
    public List<string>? Strengths { get; init; }
    public List<string>? Tradeoffs { get; init; }
    public string? Rationale { get; init; }
}

public sealed class SwarmCritique
{
    public string? CriticAgent { get; set; }
    public List<CritiqueConcern> Concerns { get; init; } = new();
    public List<CritiqueSuggestion> Suggestions { get; init; } = new();
}

public sealed class CritiqueConcern
{
    public string ProposalBy { get; init; } = string.Empty;
    public string Issue { get; init; } = string.Empty;
    public string Severity { get; init; } = "medium";
}

public sealed class CritiqueSuggestion
{
    public string ProposalBy { get; init; } = string.Empty;
    public string Suggestion { get; init; } = string.Empty;
}

public sealed class SwarmConsensusResult
{
    public List<string> ConsensusItems { get; init; } = new();
    public List<ContentiousItem> ContentiousItems { get; init; } = new();
    public List<ArchitectureOption> Options { get; init; } = new();
    public string Recommendation { get; init; } = string.Empty;
}

public sealed class ContentiousItem
{
    public string Topic { get; init; } = string.Empty;
    public List<string> Perspectives { get; init; } = new();
}

public sealed class ArchitectureOption
{
    public string Name { get; init; } = string.Empty;
    public string Approach { get; init; } = string.Empty;
    public string CloudProvider { get; init; } = string.Empty;
    public string Compute { get; init; } = string.Empty;
    public string Database { get; init; } = string.Empty;
    public string Storage { get; init; } = string.Empty;
    public string? Caching { get; init; }
    public decimal EstimatedCostPerMonth { get; init; }
    public int EstimatedLatencyP95Ms { get; init; }
    public int ComplexityScore { get; init; }
    public List<string> Strengths { get; init; } = new();
    public List<string> Weaknesses { get; init; } = new();
    public string BestFor { get; init; } = string.Empty;
}
