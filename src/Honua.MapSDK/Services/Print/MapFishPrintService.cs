// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Honua.MapSDK.Models;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Print;

/// <summary>
/// Service for integrating with MapFish Print backend
/// </summary>
public class MapFishPrintService : IMapFishPrintService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MapFishPrintService> _logger;
    private readonly string _printApp;

    public MapFishPrintService(
        HttpClient httpClient,
        ILogger<MapFishPrintService> logger,
        string printApp = "default")
    {
        _httpClient = httpClient;
        _logger = logger;
        _printApp = printApp;
    }

    /// <summary>
    /// Get print capabilities from MapFish Print server
    /// </summary>
    public async Task<PrintCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/print/{_printApp}/capabilities.json",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PrintCapabilities>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get print capabilities");
            return null;
        }
    }

    /// <summary>
    /// Submit a print job
    /// </summary>
    public async Task<string?> SubmitPrintJobAsync(
        PrintConfiguration config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Build MapFish Print request
            var request = BuildPrintRequest(config);

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"/print/{_printApp}/report.{FormatToExtension(config.Format)}",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<PrintJobResponse>(cancellationToken);

            return result?.Ref;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit print job");
            return null;
        }
    }

    /// <summary>
    /// Get print job status
    /// </summary>
    public async Task<PrintJobStatus?> GetJobStatusAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/print/{_printApp}/status/{jobId}.json",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MapFishPrintStatusResponse>(cancellationToken);

            if (result == null)
                return null;

            return new PrintJobStatus
            {
                JobId = jobId,
                Status = MapStatusToJobState(result.Status),
                Progress = result.Done ? 100 : 50,
                Message = result.Error ?? "Processing...",
                DownloadUrl = result.Done && string.IsNullOrEmpty(result.Error)
                    ? $"/print/{_printApp}/report/{jobId}"
                    : null,
                Error = result.Error
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job status for {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Download print output
    /// </summary>
    public async Task<byte[]?> DownloadPrintAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/print/{_printApp}/report/{jobId}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download print for {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Cancel a print job
    /// </summary>
    public async Task<bool> CancelPrintJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(
                $"/print/{_printApp}/cancel/{jobId}",
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return false;
        }
    }

    /// <summary>
    /// Build MapFish Print request from configuration
    /// </summary>
    private object BuildPrintRequest(PrintConfiguration config)
    {
        var attributes = new Dictionary<string, object>
        {
            ["map"] = new
            {
                center = config.Center ?? new[] { 0.0, 0.0 },
                scale = config.Scale ?? 25000,
                dpi = config.Dpi,
                rotation = config.Bearing,
                // Additional map config
            }
        };

        // Add optional attributes
        if (!string.IsNullOrEmpty(config.Title))
            attributes["title"] = config.Title;

        if (!string.IsNullOrEmpty(config.Description))
            attributes["description"] = config.Description;

        if (!string.IsNullOrEmpty(config.Author))
            attributes["author"] = config.Author;

        if (!string.IsNullOrEmpty(config.Copyright))
            attributes["copyright"] = config.Copyright;

        // Merge custom attributes
        if (config.Attributes != null)
        {
            foreach (var (key, value) in config.Attributes)
            {
                attributes[key] = value;
            }
        }

        return new
        {
            layout = config.Layout,
            outputFormat = FormatToMimeType(config.Format),
            attributes
        };
    }

    private string FormatToExtension(PrintFormat format) => format switch
    {
        PrintFormat.Pdf => "pdf",
        PrintFormat.Png => "png",
        PrintFormat.Jpeg => "jpg",
        _ => "pdf"
    };

    private string FormatToMimeType(PrintFormat format) => format switch
    {
        PrintFormat.Pdf => "application/pdf",
        PrintFormat.Png => "image/png",
        PrintFormat.Jpeg => "image/jpeg",
        _ => "application/pdf"
    };

    private PrintJobState MapStatusToJobState(string? status) => status?.ToLowerInvariant() switch
    {
        "pending" => PrintJobState.Pending,
        "running" => PrintJobState.Processing,
        "finished" => PrintJobState.Completed,
        "error" => PrintJobState.Failed,
        "cancelled" => PrintJobState.Cancelled,
        _ => PrintJobState.Pending
    };
}

/// <summary>
/// Interface for MapFish Print service
/// </summary>
public interface IMapFishPrintService
{
    Task<PrintCapabilities?> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<string?> SubmitPrintJobAsync(PrintConfiguration config, CancellationToken cancellationToken = default);
    Task<PrintJobStatus?> GetJobStatusAsync(string jobId, CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadPrintAsync(string jobId, CancellationToken cancellationToken = default);
    Task<bool> CancelPrintJobAsync(string jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// MapFish Print API response models
/// </summary>
internal class PrintJobResponse
{
    public string? Ref { get; set; }
    public string? StatusURL { get; set; }
    public string? DownloadURL { get; set; }
}

internal class MapFishPrintStatusResponse
{
    public bool Done { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public long? ElapsedTime { get; set; }
    public long? WaitingTime { get; set; }
    public string? DownloadURL { get; set; }
}
