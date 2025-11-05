// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for style management operations.
/// </summary>
public class StyleApiClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public StyleApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Gets all styles, optionally filtered by layer.
    /// </summary>
    public async Task<List<StyleListItem>> GetStylesAsync(
        string? layerId = null,
        CancellationToken cancellationToken = default)
    {
        var url = "/admin/styles";
        if (!string.IsNullOrEmpty(layerId))
            url += $"?layerId={Uri.EscapeDataString(layerId)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var styles = await response.Content.ReadFromJsonAsync<List<StyleListItem>>(_jsonOptions, cancellationToken);
        return styles ?? new List<StyleListItem>();
    }

    /// <summary>
    /// Gets a specific style by ID.
    /// </summary>
    public async Task<StyleDefinition?> GetStyleByIdAsync(
        string styleId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/admin/styles/{Uri.EscapeDataString(styleId)}", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StyleDefinition>(_jsonOptions, cancellationToken);
    }

    /// <summary>
    /// Gets the default style for a layer.
    /// </summary>
    public async Task<StyleDefinition?> GetDefaultStyleAsync(
        string layerId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/admin/layers/{Uri.EscapeDataString(layerId)}/default-style",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StyleDefinition>(_jsonOptions, cancellationToken);
    }

    /// <summary>
    /// Creates a new style.
    /// </summary>
    public async Task<StyleDefinition> CreateStyleAsync(
        CreateStyleRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/styles", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var style = await response.Content.ReadFromJsonAsync<StyleDefinition>(_jsonOptions, cancellationToken);
        return style ?? throw new InvalidOperationException("Failed to deserialize created style");
    }

    /// <summary>
    /// Updates an existing style.
    /// </summary>
    public async Task<StyleDefinition> UpdateStyleAsync(
        string styleId,
        UpdateStyleRequest request,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PutAsync(
            $"/admin/styles/{Uri.EscapeDataString(styleId)}",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var style = await response.Content.ReadFromJsonAsync<StyleDefinition>(_jsonOptions, cancellationToken);
        return style ?? throw new InvalidOperationException("Failed to deserialize updated style");
    }

    /// <summary>
    /// Deletes a style.
    /// </summary>
    public async Task DeleteStyleAsync(
        string styleId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"/admin/styles/{Uri.EscapeDataString(styleId)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Sets a style as the default for its layer.
    /// </summary>
    public async Task SetDefaultStyleAsync(
        string styleId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"/admin/styles/{Uri.EscapeDataString(styleId)}/set-default",
            null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Exports a style to SLD format.
    /// </summary>
    public async Task<string> ExportToSldAsync(
        string styleId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/admin/styles/{Uri.EscapeDataString(styleId)}/export/sld",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Exports a style to MapBox Style Spec format.
    /// </summary>
    public async Task<string> ExportToMapBoxAsync(
        string styleId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"/admin/styles/{Uri.EscapeDataString(styleId)}/export/mapbox",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Imports a style from SLD format.
    /// </summary>
    public async Task<StyleDefinition> ImportFromSldAsync(
        string layerId,
        string sldContent,
        string? styleName = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            layerId,
            sldContent,
            styleName
        };

        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/styles/import/sld", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var style = await response.Content.ReadFromJsonAsync<StyleDefinition>(_jsonOptions, cancellationToken);
        return style ?? throw new InvalidOperationException("Failed to deserialize imported style");
    }

    /// <summary>
    /// Gets available style templates.
    /// </summary>
    public async Task<List<StyleTemplate>> GetTemplatesAsync(
        string? geometryType = null,
        CancellationToken cancellationToken = default)
    {
        var url = "/admin/styles/templates";
        if (!string.IsNullOrEmpty(geometryType))
            url += $"?geometryType={Uri.EscapeDataString(geometryType)}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var templates = await response.Content.ReadFromJsonAsync<List<StyleTemplate>>(_jsonOptions, cancellationToken);
        return templates ?? new List<StyleTemplate>();
    }

    /// <summary>
    /// Duplicates an existing style.
    /// </summary>
    public async Task<StyleDefinition> DuplicateStyleAsync(
        string styleId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var request = new { name = newName };
        var json = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"/admin/styles/{Uri.EscapeDataString(styleId)}/duplicate",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var style = await response.Content.ReadFromJsonAsync<StyleDefinition>(_jsonOptions, cancellationToken);
        return style ?? throw new InvalidOperationException("Failed to deserialize duplicated style");
    }

    /// <summary>
    /// Validates a style definition.
    /// </summary>
    public async Task<ValidationResult> ValidateStyleAsync(
        StyleDefinition style,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(style, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/admin/styles/validate", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ValidationResult>(_jsonOptions, cancellationToken);
        return result ?? new ValidationResult { IsValid = true };
    }

    /// <summary>
    /// Gets unique field values for categorized styling.
    /// </summary>
    public async Task<List<string>> GetUniqueFieldValuesAsync(
        string layerId,
        string fieldName,
        int? limit = 100,
        CancellationToken cancellationToken = default)
    {
        var url = $"/admin/layers/{Uri.EscapeDataString(layerId)}/fields/{Uri.EscapeDataString(fieldName)}/unique-values";
        if (limit.HasValue)
            url += $"?limit={limit.Value}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var values = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken);
        return values ?? new List<string>();
    }
}

/// <summary>
/// Style validation result.
/// </summary>
public sealed class ValidationResult
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
