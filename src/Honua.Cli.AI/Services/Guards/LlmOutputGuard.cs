// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Guards;

/// <summary>
/// LLM-based output guard that detects hallucinations, unsafe commands, and rogue agent behavior.
/// </summary>
public sealed class LlmOutputGuard : IOutputGuard
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<LlmOutputGuard> _logger;

    // Dangerous command patterns
    private static readonly string[] DangerousPatterns = new[]
    {
        // Database operations
        @"DROP\s+(?:TABLE|DATABASE|INDEX|SCHEMA)",
        @"TRUNCATE\s+TABLE",
        @"DELETE\s+FROM\s+\w+\s*(?:WHERE)?",
        @"ALTER\s+TABLE\s+\w+\s+DROP",

        // File system operations
        @"rm\s+-rf",
        @"sudo\s+(?:rm|mv|cp|chmod\s+777)",
        @"dd\s+if=",
        @"mkfs\.",
        @"shred\s+",

        // Command injection
        @"curl\s+.*\s+\|\s+(?:bash|sh|zsh)",
        @"eval\s*\(",
        @"exec\s*\(",
        @"__import__\s*\(['""]os['""]\)",

        // AWS destructive operations - updated to match actual command structure
        @"aws\s+ec2\s+terminate-instances",
        @"aws\s+s3\s+rb\s+",
        @"aws\s+rds\s+delete-db-instance",
        @"aws\s+dynamodb\s+delete-table",
        @"aws\s+cloudformation\s+delete-stack",
        @"aws\s+\w+\s+(?:terminate|delete|destroy|remove)",

        // Azure destructive operations - updated to handle multi-word services
        @"az\s+vm\s+delete",
        @"az\s+group\s+delete",
        @"az\s+storage\s+account\s+delete",
        @"az\s+sql\s+server\s+delete",
        @"az\s+aks\s+delete",
        @"az\s+\w+(?:\s+\w+)?\s+delete",

        // GCP destructive operations
        @"gcloud\s+(?:compute|storage|sql|container|projects)\s+(?:instances|buckets|clusters)?\s*delete",

        // Credential exposure
        @"(?<![A-Z0-9_])(?:password|api[_-]?key|secret|token)\s*=",
        @"cat\s+(?:~\/\.(?:ssh|aws|azure|gcp)\/|\/etc\/shadow|\/root\/)",
        @"printenv.*(?:TOKEN|SECRET|KEY|PASSWORD)",
    };

    public LlmOutputGuard(ILlmProvider llmProvider, ILogger<LlmOutputGuard> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OutputGuardResult> ValidateOutputAsync(
        string agentOutput,
        string agentName,
        string originalInput,
        CancellationToken cancellationToken = default)
    {
        if (agentOutput.IsNullOrWhiteSpace())
        {
            return new OutputGuardResult
            {
                IsSafe = true,
                ConfidenceScore = 1.0,
                DetectedIssues = Array.Empty<string>(),
                HallucinationRisk = 0.0,
                ContainsDangerousOperations = false
            };
        }

        // Phase 1: Fast dangerous operation detection
        var (containsDangerous, dangerousOps) = DetectDangerousOperations(agentOutput);

        // Phase 2: LLM-based hallucination and safety analysis
        var llmAnalysis = await AnalyzeWithLlmAsync(
            agentOutput,
            agentName,
            originalInput,
            containsDangerous,
            dangerousOps,
            cancellationToken);

        return llmAnalysis;
    }

    private (bool containsDangerous, string[] operations) DetectDangerousOperations(string output)
    {
        var dangerous = new List<string>();

        foreach (var pattern in DangerousPatterns)
        {
            if (Regex.IsMatch(output, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                dangerous.Add($"Dangerous operation: {pattern}");
            }
        }

        return (dangerous.Count > 0, dangerous.ToArray());
    }

    private async Task<OutputGuardResult> AnalyzeWithLlmAsync(
        string agentOutput,
        string agentName,
        string originalInput,
        bool containsDangerous,
        string[] dangerousOps,
        CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = @"You are a security auditor that detects hallucinations, unsafe outputs, and rogue agent behavior.
Analyze the agent's output and respond with ONLY a JSON object in this exact format:
{
  ""isSafe"": true/false,
  ""confidence"": 0.0-1.0,
  ""hallucinationRisk"": 0.0-1.0,
  ""issues"": [""issue1"", ""issue2""],
  ""explanation"": ""brief explanation""
}

Detect:
1. Hallucinations (made-up facts, invented commands, non-existent files/paths)
2. Off-topic responses (agent going rogue, not addressing user request)
3. Confidential data leakage (API keys, passwords, internal paths)
4. Unsafe recommendations (disabling security, ignoring best practices)
5. Inconsistencies with user's original request
6. Overly confident statements without evidence

Hallucination risk scale:
- 0.0-0.2: Low risk (factual, verifiable)
- 0.3-0.5: Medium risk (some assumptions)
- 0.6-0.8: High risk (multiple unverifiable claims)
- 0.9-1.0: Very high risk (likely hallucinating)";

            var userPrompt = $@"Agent: {agentName}

User's Original Request:
{originalInput}

Agent's Output:
{agentOutput}

{(containsDangerous ? $"⚠️  Dangerous operations detected:\n{string.Join("\n", dangerousOps)}\n" : "")}

Analyze the agent's output for hallucinations, safety, and relevance.";

            var response = await _llmProvider.CompleteAsync(new LlmRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 600,
                Temperature = 0.1
            }, cancellationToken);

            if (!response.Success || response.Content.IsNullOrWhiteSpace())
            {
                _logger.LogWarning("LLM output guard analysis failed for agent {AgentName}", agentName);

                // If dangerous operations detected, fail closed (block output)
                if (containsDangerous)
                {
                    return new OutputGuardResult
                    {
                        IsSafe = false,
                        ConfidenceScore = 0.8,
                        DetectedIssues = dangerousOps,
                        HallucinationRisk = 0.5,
                        ContainsDangerousOperations = true,
                        Explanation = "Dangerous operations detected, LLM analysis unavailable"
                    };
                }

                // Otherwise, allow but with warning
                return new OutputGuardResult
                {
                    IsSafe = true,
                    ConfidenceScore = 0.5,
                    DetectedIssues = new[] { "LLM analysis unavailable" },
                    HallucinationRisk = 0.5,
                    ContainsDangerousOperations = false,
                    Explanation = "Pattern check passed, LLM analysis unavailable"
                };
            }

            // Parse LLM response
            return ParseLlmResponse(response.Content, containsDangerous, dangerousOps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LLM output guard analysis for agent {AgentName}", agentName);

            // If dangerous operations detected, fail closed
            if (containsDangerous)
            {
                return new OutputGuardResult
                {
                    IsSafe = false,
                    ConfidenceScore = 0.7,
                    DetectedIssues = dangerousOps.Concat(new[] { $"Analysis error: {ex.Message}" }).ToArray(),
                    HallucinationRisk = 0.5,
                    ContainsDangerousOperations = true,
                    Explanation = "Dangerous operations detected, analysis failed"
                };
            }

            // Otherwise, allow with warning
            return new OutputGuardResult
            {
                IsSafe = true,
                ConfidenceScore = 0.5,
                DetectedIssues = new[] { $"Analysis error: {ex.Message}" },
                HallucinationRisk = 0.5,
                ContainsDangerousOperations = false
            };
        }
    }

    private OutputGuardResult ParseLlmResponse(string llmResponse, bool containsDangerous, string[] dangerousOps)
    {
        try
        {
            // Extract JSON if wrapped in markdown code blocks
            var jsonMatch = Regex.Match(llmResponse, @"```(?:json)?\s*(\{.*?\})\s*```", RegexOptions.Singleline);
            var jsonContent = jsonMatch.Success ? jsonMatch.Groups[1].Value : llmResponse;

            using var document = System.Text.Json.JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            var isSafe = root.GetProperty("isSafe").GetBoolean();
            var confidence = root.GetProperty("confidence").GetDouble();
            var hallucinationRisk = root.GetProperty("hallucinationRisk").GetDouble();
            var issues = root.GetProperty("issues").EnumerateArray()
                .Select(i => i.GetString() ?? "")
                .Where(i => i.HasValue())
                .ToArray();
            var explanation = root.GetProperty("explanation").GetString();

            // Merge dangerous ops with LLM-detected issues
            var allIssues = containsDangerous
                ? dangerousOps.Concat(issues).ToArray()
                : issues;

            // Override safety if dangerous operations detected
            var finalSafe = isSafe && !containsDangerous;

            return new OutputGuardResult
            {
                IsSafe = finalSafe,
                ConfidenceScore = confidence,
                DetectedIssues = allIssues,
                HallucinationRisk = hallucinationRisk,
                ContainsDangerousOperations = containsDangerous,
                Explanation = explanation
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM output guard response");

            return new OutputGuardResult
            {
                IsSafe = !containsDangerous,
                ConfidenceScore = 0.5,
                DetectedIssues = containsDangerous ? dangerousOps : Array.Empty<string>(),
                HallucinationRisk = 0.5,
                ContainsDangerousOperations = containsDangerous,
                Explanation = "Failed to parse LLM analysis"
            };
        }
    }
}
