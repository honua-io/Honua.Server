// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Cli.AI.HealthChecks;

/// <summary>
/// Health check for Ollama local LLM server.
/// Verifies that Ollama is running and the configured model is available.
/// </summary>
public class OllamaHealthCheck : HealthCheckBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly LlmProviderOptions _options;

    public OllamaHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmProviderOptions> options,
        ILogger<OllamaHealthCheck> logger)
        : base(logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        // Skip health check if Ollama is not the active provider
        if (!_options.Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) &&
            !_options.FallbackProvider?.Equals("Ollama", StringComparison.OrdinalIgnoreCase) == true)
        {
            return HealthCheckResult.Healthy("Ollama is not configured as a provider");
        }

        var client = _httpClientFactory.CreateClient("Ollama");
        var endpoint = _options.Ollama?.EndpointUrl ?? "http://localhost:11434";

        Logger.LogDebug("Checking Ollama health at {Endpoint}", endpoint);

        try
        {
            // Call /api/tags to list available models
            var response = await client.GetAsync("/api/tags", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning(
                    "Ollama health check failed with status code {StatusCode}",
                    response.StatusCode);

                data["endpoint"] = endpoint;
                data["status_code"] = response.StatusCode;

                return HealthCheckResult.Unhealthy(
                    $"Ollama returned status code {response.StatusCode}",
                    data: data);
            }

            var modelsResponse = await response.Content.ReadFromJsonAsync<OllamaModelsResponse>(cancellationToken: cancellationToken);

            if (modelsResponse?.Models is null || modelsResponse.Models.Count == 0)
            {
                Logger.LogWarning("Ollama is running but no models are available");

                data["endpoint"] = endpoint;
                data["model_count"] = 0;
                data["suggestion"] = $"Run: ollama pull {_options.Ollama?.DefaultModel ?? "llama3.2"}";

                return HealthCheckResult.Degraded(
                    "Ollama is running but no models are installed. Run 'ollama pull <model>' to download a model.",
                    data: data);
            }

            // Check if the configured model is available
            var configuredModel = _options.Ollama?.DefaultModel ?? "llama3.2";
            var modelExists = modelsResponse.Models.Any(m =>
                m.Name != null && m.Name.StartsWith(configuredModel, StringComparison.OrdinalIgnoreCase));

            var modelNames = modelsResponse.Models
                .Where(m => m.Name.HasValue())
                .Select(m => m.Name!)
                .ToList();

            if (!modelExists)
            {
                Logger.LogWarning(
                    "Configured model '{Model}' is not available. Available models: {AvailableModels}",
                    configuredModel,
                    string.Join(", ", modelNames));

                data["endpoint"] = endpoint;
                data["configured_model"] = configuredModel;
                data["model_count"] = modelsResponse.Models.Count;
                data["available_models"] = modelNames;
                data["suggestion"] = $"Run: ollama pull {configuredModel}";

                return HealthCheckResult.Degraded(
                    $"Configured model '{configuredModel}' is not installed",
                    data: data);
            }

            Logger.LogDebug(
                "Ollama health check passed. Model {Model} is available among {Count} total models",
                configuredModel,
                modelsResponse.Models.Count);

            // Find the configured model for size information
            var modelInfo = modelsResponse.Models.FirstOrDefault(m =>
                m.Name != null && m.Name.StartsWith(configuredModel, StringComparison.OrdinalIgnoreCase));

            data["endpoint"] = endpoint;
            data["configured_model"] = configuredModel;
            data["model_count"] = modelsResponse.Models.Count;
            data["available_models"] = modelNames;
            data["model_size_bytes"] = modelInfo?.Size ?? 0;

            return HealthCheckResult.Healthy(
                $"Ollama is running with {modelsResponse.Models.Count} model(s). Model '{configuredModel}' is available.",
                data: data);
        }
        catch (HttpRequestException ex)
        {
            data["endpoint"] = endpoint;
            data["suggestion"] = "Run: ollama serve";

            Logger.LogError(ex, "Cannot connect to Ollama at {Endpoint}", endpoint);
            return HealthCheckResult.Unhealthy(
                $"Cannot connect to Ollama at {endpoint}. Make sure Ollama is running with 'ollama serve'.",
                ex,
                data: data);
        }
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        var endpoint = _options.Ollama?.EndpointUrl ?? "http://localhost:11434";
        data["endpoint"] = endpoint;
        data["error_type"] = ex.GetType().Name;

        return HealthCheckResult.Unhealthy(
            "Unexpected error during Ollama health check",
            ex,
            data);
    }

    #region DTOs

    private sealed class OllamaModelsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = new();
    }

    private sealed class OllamaModel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }

        [JsonPropertyName("modified_at")]
        public string? ModifiedAt { get; set; }
    }

    #endregion
}
