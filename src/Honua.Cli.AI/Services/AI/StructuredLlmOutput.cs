// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.AI;

/// <summary>
/// Provides structured LLM output with JSON schema validation.
/// This reduces parsing errors and improves reliability.
/// </summary>
/// <remarks>
/// OpenAI and Anthropic support function calling / structured outputs that guarantee valid JSON.
/// This wrapper provides a unified interface across providers.
///
/// Benefits:
/// - Guaranteed valid JSON from LLM (when supported)
/// - Schema validation before parsing
/// - Clear error messages for validation failures
/// - Retry logic with self-correction
/// </remarks>
public class StructuredLlmOutput
{
    private readonly ILlmProvider _provider;
    private readonly ILogger<StructuredLlmOutput> _logger;

    public StructuredLlmOutput(ILlmProvider provider, ILogger<StructuredLlmOutput> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Requests structured output from LLM with automatic retry and self-correction.
    /// </summary>
    /// <typeparam name="T">The expected response type</typeparam>
    /// <param name="request">The LLM request</param>
    /// <param name="schema">JSON schema for validation (optional)</param>
    /// <param name="maxRetries">Maximum retry attempts for parsing failures (default: 2)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed and validated response</returns>
    public async Task<StructuredLlmResult<T>> RequestStructuredAsync<T>(
        LlmRequest request,
        JsonSchemaDefinition? schema = null,
        int maxRetries = 2,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Check if provider supports function calling / structured output
        var supportsStructured = SupportsStructuredOutput(_provider.ProviderName);

        LlmResponse? rawResponse = null;
        Exception? lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Get response from LLM
                rawResponse = await _provider.CompleteAsync(request, cancellationToken);

                if (!rawResponse.Success)
                {
                    return StructuredLlmResult<T>.Failure(
                        $"LLM request failed: {rawResponse.ErrorMessage}",
                        rawResponse: rawResponse);
                }

                // Extract JSON payload
                var jsonContent = ExtractJsonPayload(rawResponse.Content);

                if (jsonContent.IsNullOrWhiteSpace())
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("LLM response contained no valid JSON, retrying with correction prompt (attempt {Attempt}/{MaxRetries})",
                            attempt + 1, maxRetries);

                        // Add correction to the request
                        request = AddCorrectionPrompt(request, rawResponse.Content, "Response must be valid JSON");
                        continue;
                    }

                    return StructuredLlmResult<T>.Failure(
                        "LLM response did not contain valid JSON",
                        rawResponse: rawResponse);
                }

                // Validate against schema if provided
                if (schema != null)
                {
                    var schemaValidation = ValidateJsonSchema(jsonContent, schema);
                    if (!schemaValidation.IsValid)
                    {
                        if (attempt < maxRetries)
                        {
                            _logger.LogWarning("JSON failed schema validation, retrying with correction (attempt {Attempt}/{MaxRetries}): {Errors}",
                                attempt + 1, maxRetries, string.Join(", ", schemaValidation.Errors));

                            request = AddCorrectionPrompt(request, jsonContent,
                                $"JSON schema validation failed: {string.Join(", ", schemaValidation.Errors)}");
                            continue;
                        }

                        return StructuredLlmResult<T>.Failure(
                            $"JSON schema validation failed: {string.Join(", ", schemaValidation.Errors)}",
                            rawResponse: rawResponse,
                            validationErrors: schemaValidation.Errors);
                    }
                }

                // Parse into target type
                var parsed = JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (parsed == null)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning("Deserialization returned null, retrying (attempt {Attempt}/{MaxRetries})",
                            attempt + 1, maxRetries);

                        request = AddCorrectionPrompt(request, jsonContent,
                            $"JSON must deserialize to type {typeof(T).Name}");
                        continue;
                    }

                    return StructuredLlmResult<T>.Failure(
                        $"Failed to deserialize JSON to type {typeof(T).Name}",
                        rawResponse: rawResponse);
                }

                _logger.LogDebug("Successfully parsed structured output on attempt {Attempt}", attempt + 1);

                return StructuredLlmResult<T>.Success(parsed, rawResponse, attemptCount: attempt + 1);
            }
            catch (JsonException ex)
            {
                lastException = ex;

                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "JSON parsing failed, retrying with correction (attempt {Attempt}/{MaxRetries})",
                        attempt + 1, maxRetries);

                    request = AddCorrectionPrompt(request, rawResponse?.Content ?? string.Empty,
                        $"JSON parsing error: {ex.Message}");
                    continue;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                break; // Don't retry on unexpected exceptions
            }
        }

        return StructuredLlmResult<T>.Failure(
            $"Failed to parse structured output after {maxRetries + 1} attempts: {lastException?.Message}",
            exception: lastException,
            rawResponse: rawResponse);
    }

    private static bool SupportsStructuredOutput(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "openai" => true,
            "azure" => true,
            "anthropic" => true, // Claude supports structured outputs
            "ollama" => false,
            "localai" => false,
            _ => false
        };
    }

    private static string ExtractJsonPayload(string content)
    {
        if (content.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        // Remove markdown code blocks
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

        // Try to find JSON object/array boundaries
        var trimmed = content.Trim();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            return trimmed;
        }

        return content.Trim();
    }

    private static SchemaValidationResult ValidateJsonSchema(string json, JsonSchemaDefinition schema)
    {
        // TODO: Integrate with JSON schema validation library (e.g., JsonSchema.Net)
        // For now, basic validation

        var errors = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Validate required properties
            if (schema.RequiredProperties != null)
            {
                foreach (var required in schema.RequiredProperties)
                {
                    if (!root.TryGetProperty(required, out _))
                    {
                        errors.Add($"Missing required property: {required}");
                    }
                }
            }

            // Validate property types
            if (schema.Properties != null)
            {
                foreach (var (propName, propSchema) in schema.Properties)
                {
                    if (root.TryGetProperty(propName, out var propValue))
                    {
                        if (!ValidatePropertyType(propValue, propSchema.Type))
                        {
                            errors.Add($"Property '{propName}' has invalid type. Expected: {propSchema.Type}, Got: {propValue.ValueKind}");
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
        }

        return new SchemaValidationResult(errors.Count == 0, errors);
    }

    private static bool ValidatePropertyType(JsonElement element, string expectedType)
    {
        return expectedType.ToLowerInvariant() switch
        {
            "string" => element.ValueKind == JsonValueKind.String,
            "number" => element.ValueKind == JsonValueKind.Number,
            "integer" => element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out _),
            "boolean" => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False,
            "array" => element.ValueKind == JsonValueKind.Array,
            "object" => element.ValueKind == JsonValueKind.Object,
            "null" => element.ValueKind == JsonValueKind.Null,
            _ => true // Unknown type, allow
        };
    }

    private LlmRequest AddCorrectionPrompt(LlmRequest original, string previousResponse, string error)
    {
        var correctionPrompt = $@"
Your previous response had an error: {error}

Previous response:
{previousResponse}

Please provide a corrected response that:
1. Is valid JSON (properly formatted with correct syntax)
2. Matches the expected schema
3. Contains all required properties

Generate ONLY the JSON response, no additional text or explanation.";

        return new LlmRequest
        {
            SystemPrompt = original.SystemPrompt,
            UserPrompt = original.UserPrompt + "\n\n" + correctionPrompt,
            Temperature = original.Temperature,
            MaxTokens = original.MaxTokens,
            Model = original.Model
        };
    }
}

/// <summary>
/// Result of a structured LLM request.
/// </summary>
public sealed class StructuredLlmResult<T> where T : class
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
    public LlmResponse? RawResponse { get; init; }
    public List<string>? ValidationErrors { get; init; }
    public int AttemptCount { get; init; } = 1;

    public static StructuredLlmResult<T> Success(T data, LlmResponse? rawResponse = null, int attemptCount = 1)
    {
        return new StructuredLlmResult<T>
        {
            Success = true,
            Data = data,
            RawResponse = rawResponse,
            AttemptCount = attemptCount
        };
    }

    public static StructuredLlmResult<T> Failure(
        string errorMessage,
        Exception? exception = null,
        LlmResponse? rawResponse = null,
        List<string>? validationErrors = null)
    {
        return new StructuredLlmResult<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            RawResponse = rawResponse,
            ValidationErrors = validationErrors
        };
    }
}

/// <summary>
/// JSON schema definition for validation.
/// </summary>
public sealed class JsonSchemaDefinition
{
    public string Type { get; init; } = "object";
    public Dictionary<string, PropertySchema>? Properties { get; init; }
    public List<string>? RequiredProperties { get; init; }
}

public sealed class PropertySchema
{
    public string Type { get; init; } = "string";
    public List<string>? Enum { get; init; }
    public string? Description { get; init; }
}

public sealed class SchemaValidationResult
{
    public bool IsValid { get; }
    public List<string> Errors { get; }

    public SchemaValidationResult(bool isValid, List<string> errors)
    {
        IsValid = isValid;
        Errors = errors ?? new List<string>();
    }
}

/// <summary>
/// Extension methods for using structured outputs in SemanticConsultantPlanner.
/// </summary>
public static class StructuredLlmExtensions
{
    /// <summary>
    /// Gets the consultant plan schema for validation.
    /// </summary>
    public static JsonSchemaDefinition GetConsultantPlanSchema()
    {
        return new JsonSchemaDefinition
        {
            Type = "object",
            RequiredProperties = new List<string> { "executiveSummary", "confidence", "plan" },
            Properties = new Dictionary<string, PropertySchema>
            {
                ["executiveSummary"] = new() { Type = "string", Description = "Brief summary of the plan" },
                ["confidence"] = new() { Type = "string", Enum = new List<string> { "high", "medium", "low" } },
                ["reinforcedObservations"] = new() { Type = "array" },
                ["plan"] = new()
                {
                    Type = "array",
                    Description = "List of plan steps with skill, action, inputs"
                }
            }
        };
    }

    /// <summary>
    /// Gets the intent analysis schema for validation.
    /// </summary>
    public static JsonSchemaDefinition GetIntentAnalysisSchema()
    {
        return new JsonSchemaDefinition
        {
            Type = "object",
            RequiredProperties = new List<string> { "primaryIntent", "requiredAgents", "requiresMultipleAgents" },
            Properties = new Dictionary<string, PropertySchema>
            {
                ["primaryIntent"] = new()
                {
                    Type = "string",
                    Enum = new List<string>
                    {
                        "architecture", "setup", "deployment", "data", "performance",
                        "benchmark", "security", "upgrade", "troubleshooting", "metadata",
                        "migration", "spa"
                    }
                },
                ["requiredAgents"] = new() { Type = "array" },
                ["requiresMultipleAgents"] = new() { Type = "boolean" },
                ["reasoning"] = new() { Type = "string" }
            }
        };
    }
}
