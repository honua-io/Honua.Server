// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for AI assistant operations in Admin UI.
/// Integrates with backend AdaptiveAIService infrastructure.
/// </summary>
public sealed class AIAssistantApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIAssistantApiClient> _logger;
    private AICapabilitiesResponse? _cachedCapabilities;
    private DateTimeOffset _capabilitiesCachedAt;

    public AIAssistantApiClient(IHttpClientFactory httpClientFactory, ILogger<AIAssistantApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Checks if AI assistant is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilities = await GetCapabilitiesAsync(cancellationToken);
            return capabilities?.Available ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AI assistant availability check failed");
            return false;
        }
    }

    /// <summary>
    /// Gets AI assistant capabilities (with 5-minute cache).
    /// </summary>
    public async Task<AICapabilitiesResponse?> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        // Return cached capabilities if less than 5 minutes old
        if (_cachedCapabilities != null &&
            DateTimeOffset.UtcNow - _capabilitiesCachedAt < TimeSpan.FromMinutes(5))
        {
            return _cachedCapabilities;
        }

        try
        {
            var response = await _httpClient.GetAsync("/admin/ai/capabilities", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("AI capabilities endpoint returned {StatusCode}", response.StatusCode);
                return new AICapabilitiesResponse { Available = false, Message = "AI assistant is not configured" };
            }

            _cachedCapabilities = await response.Content.ReadFromJsonAsync<AICapabilitiesResponse>(cancellationToken: cancellationToken);
            _capabilitiesCachedAt = DateTimeOffset.UtcNow;

            return _cachedCapabilities;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get AI capabilities");
            return new AICapabilitiesResponse { Available = false, Message = "Unable to connect to AI service" };
        }
    }

    /// <summary>
    /// Performs natural language search.
    /// </summary>
    public async Task<NaturalLanguageSearchResponse> SearchAsync(
        NaturalLanguageSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/ai/search", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<NaturalLanguageSearchResponse>(cancellationToken: cancellationToken)
                ?? new NaturalLanguageSearchResponse { Interpretation = "No results found" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Natural language search failed");
            throw;
        }
    }

    /// <summary>
    /// Gets smart suggestions based on context.
    /// </summary>
    public async Task<List<SmartSuggestion>> GetSuggestionsAsync(
        SmartSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/ai/suggestions", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<List<SmartSuggestion>>(cancellationToken: cancellationToken)
                ?? new List<SmartSuggestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get suggestions for type {Type}", request.SuggestionType);
            return new List<SmartSuggestion>();
        }
    }

    /// <summary>
    /// Gets CRS suggestions for a layer.
    /// </summary>
    public async Task<List<SmartSuggestion>> GetCRSSuggestionsAsync(
        string? geometryType = null,
        string? region = null,
        string? dataSource = null,
        CancellationToken cancellationToken = default)
    {
        var context = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(geometryType)) context["geometryType"] = geometryType;
        if (!string.IsNullOrEmpty(region)) context["region"] = region;
        if (!string.IsNullOrEmpty(dataSource)) context["dataSource"] = dataSource;

        var request = new SmartSuggestionRequest
        {
            SuggestionType = SuggestionType.CRS,
            Context = context
        };

        return await GetSuggestionsAsync(request, cancellationToken);
    }

    /// <summary>
    /// Gets style suggestions for a layer.
    /// </summary>
    public async Task<List<SmartSuggestion>> GetStyleSuggestionsAsync(
        string geometryType,
        string? layerName = null,
        Dictionary<string, string>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var context = new Dictionary<string, string> { ["geometryType"] = geometryType };
        if (!string.IsNullOrEmpty(layerName)) context["layerName"] = layerName;
        if (attributes != null)
        {
            foreach (var kvp in attributes)
            {
                context[$"attr_{kvp.Key}"] = kvp.Value;
            }
        }

        var request = new SmartSuggestionRequest
        {
            SuggestionType = SuggestionType.Style,
            Context = context
        };

        return await GetSuggestionsAsync(request, cancellationToken);
    }

    /// <summary>
    /// Generates metadata fields.
    /// </summary>
    public async Task<GenerateMetadataResponse> GenerateMetadataAsync(
        GenerateMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/ai/generate-metadata", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<GenerateMetadataResponse>(cancellationToken: cancellationToken)
                ?? new GenerateMetadataResponse { Metadata = new(), Quality = 0 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata generation failed for {ItemType}", request.ItemType);
            throw;
        }
    }

    /// <summary>
    /// Generates a title for a service or layer.
    /// </summary>
    public async Task<string?> GenerateTitleAsync(
        string itemType,
        Dictionary<string, string> context,
        CancellationToken cancellationToken = default)
    {
        var request = new GenerateMetadataRequest
        {
            ItemType = itemType,
            Fields = new List<string> { "title" },
            Context = context
        };

        var result = await GenerateMetadataAsync(request, cancellationToken);
        return result.Metadata.TryGetValue("title", out var title) ? title : null;
    }

    /// <summary>
    /// Generates an abstract/description.
    /// </summary>
    public async Task<string?> GenerateAbstractAsync(
        string itemType,
        Dictionary<string, string> context,
        Dictionary<string, string>? existingMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var request = new GenerateMetadataRequest
        {
            ItemType = itemType,
            Fields = new List<string> { "abstract" },
            Context = context,
            ExistingMetadata = existingMetadata ?? new()
        };

        var result = await GenerateMetadataAsync(request, cancellationToken);
        return result.Metadata.TryGetValue("abstract", out var abstract_) ? abstract_ : null;
    }

    /// <summary>
    /// Generates keywords.
    /// </summary>
    public async Task<List<string>> GenerateKeywordsAsync(
        string itemType,
        Dictionary<string, string> context,
        Dictionary<string, string>? existingMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var request = new GenerateMetadataRequest
        {
            ItemType = itemType,
            Fields = new List<string> { "keywords" },
            Context = context,
            ExistingMetadata = existingMetadata ?? new()
        };

        var result = await GenerateMetadataAsync(request, cancellationToken);

        if (result.Metadata.TryGetValue("keywords", out var keywordsStr))
        {
            return keywordsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }

        return new List<string>();
    }

    /// <summary>
    /// Gets troubleshooting help.
    /// </summary>
    public async Task<TroubleshootResponse> TroubleshootAsync(
        TroubleshootRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/ai/troubleshoot", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<TroubleshootResponse>(cancellationToken: cancellationToken)
                ?? new TroubleshootResponse { Diagnosis = "Unable to diagnose the problem", Confidence = 0 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Troubleshooting request failed");
            throw;
        }
    }

    /// <summary>
    /// Sends a chat message to AI assistant.
    /// </summary>
    public async Task<AIChatResponse> ChatAsync(
        AIChatRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/ai/chat", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AIChatResponse>(cancellationToken: cancellationToken)
                ?? new AIChatResponse { Message = "I'm having trouble processing your request right now." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat request failed");
            throw;
        }
    }

    /// <summary>
    /// Invalidates cached capabilities (call after configuration changes).
    /// </summary>
    public void InvalidateCapabilitiesCache()
    {
        _cachedCapabilities = null;
    }
}
