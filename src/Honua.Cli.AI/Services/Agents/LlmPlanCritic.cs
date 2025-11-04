// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents;

public sealed class LlmPlanCritic : IAgentCritic
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<LlmPlanCritic> _logger;

    public LlmPlanCritic(ILlmProvider llmProvider, ILogger<LlmPlanCritic> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<string>> EvaluateAsync(ConsultantRequestSnapshot request, AgentCoordinatorResult result, CancellationToken cancellationToken)
    {
        if (_llmProvider is MockLlmProvider)
        {
            return Array.Empty<string>();
        }

        try
        {
            var prompt = BuildPrompt(request, result);
            var response = await _llmProvider.CompleteAsync(new LlmRequest
            {
                SystemPrompt = "You are a senior SRE that audits multi-agent automation results for safety and completeness.",
                UserPrompt = prompt,
                MaxTokens = 400,
                Temperature = 0.2
            }, cancellationToken).ConfigureAwait(false);

            if (!response.Success || response.Content.IsNullOrWhiteSpace())
            {
                return Array.Empty<string>();
            }

            return ParseWarnings(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM critic evaluation failed");
            return Array.Empty<string>();
        }
    }

    private static string BuildPrompt(ConsultantRequestSnapshot request, AgentCoordinatorResult result)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Evaluate the following plan execution for safety gaps.");
        builder.AppendLine("Return a JSON array of warning strings. Return an empty array if everything looks safe.");
        builder.AppendLine();
        builder.AppendLine($"Request: {request.Prompt}");
        builder.AppendLine($"Mode: {request.Mode}");
        builder.AppendLine($"DryRun: {request.DryRun}");
        builder.AppendLine($"Agent success: {result.Success}");
        builder.AppendLine($"Coordinator response: {result.Response}");
        builder.AppendLine("Steps:");
        foreach (var step in result.Steps)
        {
            builder.AppendLine($"- Agent={step.AgentName}; Action={step.Action}; Success={step.Success}; Message={step.Message}");
        }

        builder.AppendLine("NextSteps:");
        foreach (var step in result.NextSteps)
        {
            builder.AppendLine($"- {step}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> ParseWarnings(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var warnings = new List<string>();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var value = element.GetString();
                        if (value.HasValue())
                        {
                            warnings.Add(value);
                        }
                    }
                }

                return warnings;
            }
        }
        catch (JsonException)
        {
            // fallthrough
        }

        return Array.Empty<string>();
    }
}
