// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for data import/ingestion operations.
/// </summary>
public sealed class ImportApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImportApiClient> _logger;

    public ImportApiClient(IHttpClientFactory httpClientFactory, ILogger<ImportApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Creates an import job by uploading a file.
    /// </summary>
    public async Task<ImportJobSnapshot?> CreateImportJobAsync(
        string serviceId,
        string layerId,
        IBrowserFile file,
        bool overwrite = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();

            // Add form fields
            content.Add(new StringContent(serviceId), "serviceId");
            content.Add(new StringContent(layerId), "layerId");
            content.Add(new StringContent(overwrite.ToString().ToLowerInvariant()), "overwrite");

            // Add file
            var fileContent = new StreamContent(file.OpenReadStream(maxAllowedSize: 500 * 1024 * 1024, cancellationToken));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "file", file.Name);

            // Track upload progress
            var uploadedBytes = 0L;
            var totalBytes = file.Size;

            _logger.LogInformation("Uploading file: {FileName} ({Size} bytes)", file.Name, totalBytes);

            var response = await _httpClient.PostAsync("/admin/ingestion/jobs", content, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                var result = await response.Content.ReadFromJsonAsync<CreateImportJobResponse>(cancellationToken: cancellationToken);
                progress?.Report(100);
                return result?.Job;
            }

            _logger.LogError("Import job creation failed: {StatusCode}", response.StatusCode);
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Error response: {Error}", errorContent);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating import job for file {FileName}", file.Name);
            throw;
        }
    }

    /// <summary>
    /// Gets the status of an import job.
    /// </summary>
    public async Task<ImportJobSnapshot?> GetImportJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/ingestion/jobs/{jobId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GetImportJobResponse>(cancellationToken: cancellationToken);
                return result?.Job;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching import job {JobId}", jobId);
            throw;
        }
    }

    /// <summary>
    /// Lists all import jobs with pagination.
    /// </summary>
    public async Task<PaginatedImportJobs> ListImportJobsAsync(
        int pageSize = 25,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"/admin/ingestion/jobs?pageSize={pageSize}";
            if (!string.IsNullOrEmpty(pageToken))
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PaginatedImportJobs>(cancellationToken: cancellationToken);
            return result ?? new PaginatedImportJobs();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing import jobs");
            throw;
        }
    }

    /// <summary>
    /// Cancels a running import job.
    /// </summary>
    public async Task<bool> CancelImportJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/ingestion/jobs/{jobId}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling import job {JobId}", jobId);
            throw;
        }
    }

    private sealed class CreateImportJobResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("job")]
        public ImportJobSnapshot? Job { get; set; }
    }

    private sealed class GetImportJobResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("job")]
        public ImportJobSnapshot? Job { get; set; }
    }
}
