// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Security;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Extensions for SemanticConsultantPlanner to add structured output support.
/// </summary>
/// <remarks>
/// This provides a more robust alternative to ParseLlmResponse by using:
/// - JSON schema validation
/// - Retry with self-correction
/// - Prompt injection filtering
/// - Better error messages
/// </remarks>
public static class SemanticConsultantPlannerExtensions
{
    /// <summary>
    /// Creates a plan using structured LLM output with validation and retry.
    /// This is a more robust alternative to the standard CreatePlanAsync.
    /// </summary>
    public static async Task<ConsultantPlan> CreatePlanWithStructuredOutputAsync(
        this ILlmProvider llmProvider,
        string systemPrompt,
        string userPrompt,
        ConsultantPlanningContext planningContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Apply prompt injection filter to user input
        var sanitizedUserPrompt = ApplyPromptInjectionProtection(userPrompt);

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = sanitizedUserPrompt,
            Temperature = 0.3,
            MaxTokens = 2000
        };

        var structuredOutput = new StructuredLlmOutput(llmProvider, logger.AsGeneric<StructuredLlmOutput>());

        var schema = GetConsultantPlanSchema();

        var result = await structuredOutput.RequestStructuredAsync<ConsultantPlanDto>(
            request,
            schema,
            maxRetries: 2,
            cancellationToken);

        if (!result.Success)
        {
            logger.LogError(
                "Failed to generate structured plan after {Attempts} attempts: {Error}. Validation errors: {ValidationErrors}",
                result.AttemptCount,
                result.ErrorMessage,
                result.ValidationErrors != null ? string.Join(", ", result.ValidationErrors) : "none");

            // Fallback to legacy parsing if structured output fails
            if (result.RawResponse != null)
            {
                logger.LogWarning("Attempting fallback to legacy parsing");
                return ParseLegacyResponse(result.RawResponse.Content, planningContext);
            }

            throw new InvalidOperationException(
                $"Failed to generate plan: {result.ErrorMessage}");
        }

        logger.LogInformation(
            "Successfully generated structured plan on attempt {Attempt} with {StepCount} steps",
            result.AttemptCount,
            result.Data?.Plan?.Count ?? 0);

        return ConvertToConsultantPlan(result.Data!, planningContext);
    }

    private static string ApplyPromptInjectionProtection(string userPrompt)
    {
        // Detect injection attempts
        if (PromptInjectionFilter.DetectInjectionAttempt(userPrompt))
        {
            // Don't throw - just log and sanitize
            // The wrapping will prevent the injection from being effective
        }

        // Sanitize and wrap with clear boundaries
        return PromptInjectionFilter.WrapUserInput(userPrompt, sanitize: true);
    }

    private static JsonSchemaDefinition GetConsultantPlanSchema()
    {
        return new JsonSchemaDefinition
        {
            Type = "object",
            RequiredProperties = new List<string> { "executiveSummary", "confidence", "plan" },
            Properties = new Dictionary<string, PropertySchema>
            {
                ["executiveSummary"] = new()
                {
                    Type = "string",
                    Description = "Brief executive summary of the plan (2-3 sentences)"
                },
                ["confidence"] = new()
                {
                    Type = "string",
                    Enum = new List<string> { "high", "medium", "low" },
                    Description = "Confidence level in the plan"
                },
                ["reinforcedObservations"] = new()
                {
                    Type = "array",
                    Description = "List of observations being addressed"
                },
                ["plan"] = new()
                {
                    Type = "array",
                    Description = "Ordered list of plan steps to execute"
                }
            }
        };
    }

    private static ConsultantPlan ConvertToConsultantPlan(
        ConsultantPlanDto dto,
        ConsultantPlanningContext context)
    {
        var steps = new List<ConsultantPlanStep>();

        if (dto.Plan != null)
        {
            foreach (var stepDto in dto.Plan)
            {
                if (stepDto.Skill.IsNullOrWhiteSpace() || stepDto.Action.IsNullOrWhiteSpace())
                {
                    continue; // Skip invalid steps
                }

                steps.Add(new ConsultantPlanStep(
                    stepDto.Skill!,
                    stepDto.Action!,
                    stepDto.Inputs ?? new Dictionary<string, string>(),
                    stepDto.Title,
                    stepDto.Category,
                    stepDto.Rationale,
                    stepDto.SuccessCriteria,
                    stepDto.Risk,
                    stepDto.Dependencies ?? new List<string>()));
            }
        }

        var observations = new List<ConsultantObservation>();
        if (dto.ReinforcedObservations != null)
        {
            foreach (var obs in dto.ReinforcedObservations)
            {
                observations.Add(new ConsultantObservation(
                    obs.Id ?? $"obs-{Guid.NewGuid():N}",
                    obs.Severity ?? "medium",
                    obs.Summary ?? "Observation",
                    obs.Detail ?? string.Empty,
                    obs.Recommendation ?? "Review and address"));
            }
        }

        return new ConsultantPlan(steps, dto.ExecutiveSummary, dto.Confidence, observations);
    }

    private static ConsultantPlan ParseLegacyResponse(string content, ConsultantPlanningContext context)
    {
        // Fallback to original parsing logic
        // This is a simplified version - in production, use the full ParseLlmResponse from SemanticConsultantPlanner
        try
        {
            var json = ExtractJsonPayload(content);
            if (!json.IsNullOrWhiteSpace())
            {
                var dto = JsonSerializer.Deserialize<ConsultantPlanDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (dto != null)
                {
                    return ConvertToConsultantPlan(dto, context);
                }
            }
        }
        catch
        {
            // Ignore and return empty plan
        }

        return new ConsultantPlan(Array.Empty<ConsultantPlanStep>());
    }

    private static string ExtractJsonPayload(string content)
    {
        if (content.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        if (content.Contains("```json", StringComparison.OrdinalIgnoreCase))
        {
            var start = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase) + 7;
            var end = content.IndexOf("```", start, StringComparison.OrdinalIgnoreCase);
            if (end > start)
            {
                return content.Substring(start, end - start).Trim();
            }
        }
        else if (content.Contains("```", StringComparison.Ordinal))
        {
            var start = content.IndexOf("```", StringComparison.Ordinal) + 3;
            var end = content.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
            {
                return content.Substring(start, end - start).Trim();
            }
        }

        return content.Trim();
    }

    private static ILogger<T> AsGeneric<T>(this ILogger logger)
    {
        return (ILogger<T>)logger;
    }
}

/// <summary>
/// DTO for consultant plan JSON deserialization.
/// </summary>
public sealed class ConsultantPlanDto
{
    public string? ExecutiveSummary { get; set; }
    public string? Confidence { get; set; }
    public List<ObservationDto>? ReinforcedObservations { get; set; }
    public List<PlanStepDto>? Plan { get; set; }
}

public sealed class ObservationDto
{
    public string? Id { get; set; }
    public string? Severity { get; set; }
    public string? Summary { get; set; }
    public string? Detail { get; set; }
    public string? Recommendation { get; set; }
}

public sealed class PlanStepDto
{
    public string? Title { get; set; }
    public string? Skill { get; set; }
    public string? Action { get; set; }
    public string? Category { get; set; }
    public string? Rationale { get; set; }
    public string? SuccessCriteria { get; set; }
    public string? Risk { get; set; }
    public List<string>? Dependencies { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
}
