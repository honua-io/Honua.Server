// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for Honua Control Plane API management.
/// Provides AI with capabilities to manage Honua Server via HTTP API.
/// </summary>
public sealed class ControlPlanePlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ControlPlanePlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    #region Configuration Management

    [KernelFunction, Description("Gets runtime configuration status for all services and protocols")]
    public async Task<string> GetConfigurationStatus(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync("/admin/config/status");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Toggles a protocol globally (master kill switch) or for a specific service")]
    public async Task<string> ToggleProtocol(
        [Description("Protocol name: wfs, wms, wmts, csw, wcs, stac, geometry, or rasterTiles")] string protocol,
        [Description("Enable (true) or disable (false)")] bool enabled,
        [Description("Optional: Service ID to toggle protocol for specific service")] string? serviceId = null,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var endpoint = serviceId.IsNullOrWhiteSpace()
                ? $"/admin/config/services/{protocol}"
                : $"/admin/config/services/{serviceId}/{protocol}";

            var payload = JsonSerializer.Serialize(new { enabled }, JsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PatchAsync(endpoint, content);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}", details = responseBody }, JsonOptions);
            }

            return responseBody;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    #endregion

    #region Logging Management

    [KernelFunction, Description("Gets current log level configuration for all categories")]
    public async Task<string> GetLogLevels(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync("/admin/logging/categories");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Sets log level for a specific category at runtime")]
    public async Task<string> SetLogLevel(
        [Description("Category name (e.g., 'Honua.Server.Core.Data', 'Microsoft.AspNetCore')")] string category,
        [Description("Log level: Trace, Debug, Information, Warning, Error, Critical, or None")] string level,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var payload = JsonSerializer.Serialize(new { level }, JsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PatchAsync($"/admin/logging/categories/{category}", content);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}", details = responseBody }, JsonOptions);
            }

            return responseBody;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    #endregion

    #region Tracing Management

    [KernelFunction, Description("Gets current OpenTelemetry tracing configuration")]
    public async Task<string> GetTracingConfiguration(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync("/admin/observability/tracing");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Updates tracing sampling ratio (takes effect immediately)")]
    public async Task<string> SetTracingSampling(
        [Description("Sampling ratio from 0.0 to 1.0 (e.g., 0.1 = 10%)")] double ratio,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var payload = JsonSerializer.Serialize(new { ratio }, JsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PatchAsync("/admin/observability/tracing/sampling", content);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}", details = responseBody }, JsonOptions);
            }

            return responseBody;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Creates a test trace to verify tracing configuration")]
    public async Task<string> CreateTestTrace(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var payload = JsonSerializer.Serialize(new { activityName = "AIConsultantTest", duration = 1000 }, JsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/admin/observability/tracing/test", content);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}", details = responseBody }, JsonOptions);
            }

            return responseBody;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    #endregion

    #region Metadata Management

    [KernelFunction, Description("Reloads metadata from disk without restarting the server")]
    public async Task<string> ReloadMetadata(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.PostAsync("/admin/metadata/reload", null);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}", details = responseBody }, JsonOptions);
            }

            return responseBody;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Gets list of metadata snapshots")]
    public async Task<string> ListMetadataSnapshots(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync("/admin/metadata/snapshots");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Creates a metadata snapshot for backup/rollback")]
    public async Task<string> CreateMetadataSnapshot(
        [Description("Snapshot label (e.g., 'pre-migration-2025-10-15')")] string label,
        [Description("Optional notes about the snapshot")] string? notes = null,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var payload = JsonSerializer.Serialize(new { label, notes }, JsonOptions);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/admin/metadata/snapshots", content);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}", details = responseBody }, JsonOptions);
            }

            return responseBody;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    #endregion

    #region Data Ingestion

    [KernelFunction, Description("Gets status of all data ingestion jobs")]
    public async Task<string> ListIngestionJobs(
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync("/admin/ingestion/jobs");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Gets status of a specific data ingestion job")]
    public async Task<string> GetIngestionJobStatus(
        [Description("Job ID (GUID)")] string jobId,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync($"/admin/ingestion/jobs/{jobId}");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    #endregion

    #region Raster Cache

    [KernelFunction, Description("Gets raster tile cache statistics")]
    public async Task<string> GetCacheStatistics(
        [Description("Optional: Dataset ID for specific dataset stats")] string? datasetId = null,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var endpoint = datasetId.IsNullOrWhiteSpace()
                ? "/admin/raster-cache/statistics"
                : $"/admin/raster-cache/statistics/datasets/{datasetId}";

            var response = await client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    [KernelFunction, Description("Gets quota status for raster tile cache")]
    public async Task<string> GetCacheQuotaStatus(
        [Description("Dataset ID")] string datasetId,
        [Description("Honua server base URL")] string serverUrl = "http://localhost:8080",
        [Description("Bearer token for authentication")] string? bearerToken = null)
    {
        try
        {
            var client = CreateClient(serverUrl, bearerToken);
            var response = await client.GetAsync($"/admin/raster-cache/quota/{datasetId}/status");

            if (!response.IsSuccessStatusCode)
            {
                return JsonSerializer.Serialize(new { error = $"HTTP {response.StatusCode}: {response.ReasonPhrase}" }, JsonOptions);
            }

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return content;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
        }
    }

    #endregion

    #region Helper Methods

    private HttpClient CreateClient(string serverUrl, string? bearerToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(serverUrl);
        client.Timeout = TimeSpan.FromSeconds(30);

        if (bearerToken.HasValue())
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return client;
    }

    #endregion
}
