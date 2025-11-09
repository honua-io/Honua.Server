// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for SQL View management operations.
/// </summary>
public sealed class SqlViewApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SqlViewApiClient> _logger;

    public SqlViewApiClient(IHttpClientFactory httpClientFactory, ILogger<SqlViewApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets the SQL View configuration for a layer.
    /// </summary>
    public async Task<SqlViewModel?> GetSqlViewAsync(string layerId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/layers/{Uri.EscapeDataString(layerId)}/sqlview", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SqlViewResponse>(cancellationToken: cancellationToken);
            return result?.SqlView;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SQL View for layer {LayerId}", layerId);
            throw;
        }
    }

    /// <summary>
    /// Updates the SQL View configuration for a layer.
    /// </summary>
    public async Task UpdateSqlViewAsync(string layerId, SqlViewModel? sqlView, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpdateLayerSqlViewRequest { SqlView = sqlView };
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/layers/{Uri.EscapeDataString(layerId)}/sqlview", request, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SQL View for layer {LayerId}", layerId);
            throw;
        }
    }

    /// <summary>
    /// Tests a SQL query with sample parameters.
    /// </summary>
    public async Task<QueryTestResult> TestQueryAsync(string layerId, TestSqlQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/admin/metadata/layers/{Uri.EscapeDataString(layerId)}/sqlview/test", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<QueryTestResult>(cancellationToken: cancellationToken);
            return result ?? new QueryTestResult { Success = false, ErrorMessage = "No response from server" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SQL query for layer {LayerId}", layerId);
            return new QueryTestResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Detects the schema from a SQL query.
    /// </summary>
    public async Task<SchemaDetectionResult> DetectSchemaAsync(string layerId, TestSqlQueryRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/admin/metadata/layers/{Uri.EscapeDataString(layerId)}/sqlview/detect-schema", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<SchemaDetectionResult>(cancellationToken: cancellationToken);
            return result ?? new SchemaDetectionResult { Success = false, ErrorMessage = "No response from server" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting schema for layer {LayerId}", layerId);
            return new SchemaDetectionResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private sealed record SqlViewResponse
    {
        public SqlViewModel? SqlView { get; init; }
    }
}
