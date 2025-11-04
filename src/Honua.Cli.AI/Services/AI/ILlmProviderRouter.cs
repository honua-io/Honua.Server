// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Routes LLM requests to different providers based on task characteristics.
/// Enables multi-provider strategies for better results and redundancy.
/// </summary>
public interface ILlmProviderRouter
{
    /// <summary>
    /// Routes a request to the most appropriate provider based on task type.
    /// </summary>
    Task<LlmResponse> RouteRequestAsync(
        LlmRequest request,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets second opinion from a different provider for critical decisions.
    /// </summary>
    Task<SecondOpinionResult> GetSecondOpinionAsync(
        LlmRequest request,
        LlmResponse firstOpinion,
        string firstProvider,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs request on multiple providers in parallel and synthesizes results.
    /// </summary>
    Task<ConsensusResult> GetConsensusAsync(
        LlmRequest request,
        string[] providers,
        LlmTaskContext taskContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context about the LLM task for routing decisions.
/// </summary>
public sealed class LlmTaskContext
{
    public string TaskType { get; init; } = string.Empty;
    public string Criticality { get; init; } = "medium"; // low, medium, high, critical
    public bool RequiresSecondOpinion { get; init; }
    public bool RequiresConsensus { get; init; }
    public int MaxLatencyMs { get; init; } = 30000;
    public decimal MaxCostUsd { get; init; } = 0.10m;
}

/// <summary>
/// Result of getting second opinion from another provider.
/// </summary>
public sealed class SecondOpinionResult
{
    public LlmResponse FirstOpinion { get; init; } = null!;
    public LlmResponse SecondOpinion { get; init; } = null!;
    public string FirstProvider { get; init; } = string.Empty;
    public string SecondProvider { get; init; } = string.Empty;
    public bool Agrees { get; init; }
    public string? Disagreement { get; init; }
    public LlmResponse RecommendedResponse { get; init; } = null!;
    public string Reasoning { get; init; } = string.Empty;
}

/// <summary>
/// Result of getting consensus from multiple providers.
/// </summary>
public sealed class ConsensusResult
{
    public LlmResponse[] Responses { get; init; } = Array.Empty<LlmResponse>();
    public string[] Providers { get; init; } = Array.Empty<string>();
    public LlmResponse SynthesizedResponse { get; init; } = null!;
    public double AgreementScore { get; init; } // 0.0 to 1.0
    public string ConsensusMethod { get; init; } = string.Empty;
}
