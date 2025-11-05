// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for export/import operations.
/// </summary>
public sealed class ExportImportApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExportImportApiClient> _logger;

    public ExportImportApiClient(IHttpClientFactory httpClientFactory, ILogger<ExportImportApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Exports metadata configuration.
    /// </summary>
    public async Task<ExportResponse> ExportAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/export", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ExportResponse>(cancellationToken: cancellationToken);
            return result ?? throw new InvalidOperationException("Export returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting configuration");
            throw;
        }
    }

    /// <summary>
    /// Exports the entire catalog.
    /// </summary>
    public async Task<ExportResponse> ExportAllAsync(string format = "json", bool prettyPrint = true, CancellationToken cancellationToken = default)
    {
        var request = new ExportRequest
        {
            Format = format,
            Scope = ExportScope.All,
            IncludeRelated = true,
            IncludeMetadata = true,
            PrettyPrint = prettyPrint
        };

        return await ExportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Exports specific services with their layers.
    /// </summary>
    public async Task<ExportResponse> ExportServicesAsync(
        List<string> serviceIds,
        string format = "json",
        bool includeRelated = true,
        CancellationToken cancellationToken = default)
    {
        var request = new ExportRequest
        {
            Format = format,
            Scope = ExportScope.Services,
            ItemIds = serviceIds,
            IncludeRelated = includeRelated,
            IncludeMetadata = true,
            PrettyPrint = true
        };

        return await ExportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Exports specific layers.
    /// </summary>
    public async Task<ExportResponse> ExportLayersAsync(
        List<string> layerIds,
        string format = "json",
        CancellationToken cancellationToken = default)
    {
        var request = new ExportRequest
        {
            Format = format,
            Scope = ExportScope.Layers,
            ItemIds = layerIds,
            IncludeRelated = false,
            IncludeMetadata = true,
            PrettyPrint = true
        };

        return await ExportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Exports specific folders.
    /// </summary>
    public async Task<ExportResponse> ExportFoldersAsync(
        List<string> folderIds,
        string format = "json",
        CancellationToken cancellationToken = default)
    {
        var request = new ExportRequest
        {
            Format = format,
            Scope = ExportScope.Folders,
            ItemIds = folderIds,
            IncludeRelated = false,
            IncludeMetadata = true,
            PrettyPrint = true
        };

        return await ExportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Exports specific styles.
    /// </summary>
    public async Task<ExportResponse> ExportStylesAsync(
        List<string> styleIds,
        string format = "json",
        CancellationToken cancellationToken = default)
    {
        var request = new ExportRequest
        {
            Format = format,
            Scope = ExportScope.Styles,
            ItemIds = styleIds,
            IncludeRelated = false,
            IncludeMetadata = true,
            PrettyPrint = true
        };

        return await ExportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Imports metadata configuration.
    /// </summary>
    public async Task<ImportResponse> ImportAsync(ImportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/import", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ImportResponse>(cancellationToken: cancellationToken);
            return result ?? throw new InvalidOperationException("Import returned null response");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing configuration");
            throw;
        }
    }

    /// <summary>
    /// Validates import without applying changes (dry-run).
    /// </summary>
    public async Task<ImportResponse> ValidateImportAsync(
        string content,
        string mode = ImportMode.Merge,
        CancellationToken cancellationToken = default)
    {
        var request = new ImportRequest
        {
            Content = content,
            Mode = mode,
            DryRun = true,
            SkipValidation = false,
            CreateBackup = false
        };

        return await ImportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Imports and applies configuration.
    /// </summary>
    public async Task<ImportResponse> ApplyImportAsync(
        string content,
        string mode = ImportMode.Merge,
        bool createBackup = true,
        string? backupLabel = null,
        CancellationToken cancellationToken = default)
    {
        var request = new ImportRequest
        {
            Content = content,
            Mode = mode,
            DryRun = false,
            SkipValidation = false,
            CreateBackup = createBackup,
            BackupLabel = backupLabel
        };

        return await ImportAsync(request, cancellationToken);
    }

    /// <summary>
    /// Downloads export as file (triggers browser download).
    /// </summary>
    public async Task<byte[]> DownloadExportAsync(ExportResponse export, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert content to bytes
            var bytes = Encoding.UTF8.GetBytes(export.Content);
            return bytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading export");
            throw;
        }
    }

    /// <summary>
    /// Reads file content from uploaded file.
    /// </summary>
    public async Task<string> ReadFileContentAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var reader = new StreamReader(fileStream);
            var content = await reader.ReadToEndAsync(cancellationToken);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file content");
            throw;
        }
    }

    /// <summary>
    /// Gets suggested file name for export.
    /// </summary>
    public string GetSuggestedFileName(ExportRequest request)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var extension = request.Format.ToLowerInvariant();
        var scope = request.Scope.ToLowerInvariant();

        if (scope == ExportScope.All)
        {
            return $"honua-catalog-{timestamp}.{extension}";
        }

        if (request.ItemIds.Count == 1)
        {
            return $"honua-{scope}-{request.ItemIds[0]}-{timestamp}.{extension}";
        }

        return $"honua-{scope}-{timestamp}.{extension}";
    }

    /// <summary>
    /// Detects format from file content.
    /// </summary>
    public string DetectFormat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ExportFormat.Json;
        }

        var trimmed = content.TrimStart();

        // JSON starts with { or [
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            return ExportFormat.Json;
        }

        // YAML typically doesn't start with { or [ and has key: value format
        if (trimmed.Contains(":") && !trimmed.StartsWith("{"))
        {
            return ExportFormat.Yaml;
        }

        return ExportFormat.Json;
    }

    /// <summary>
    /// Validates file size before upload.
    /// </summary>
    public bool ValidateFileSize(long fileSizeBytes, out string errorMessage)
    {
        const long maxSizeBytes = 50 * 1024 * 1024; // 50 MB

        if (fileSizeBytes > maxSizeBytes)
        {
            errorMessage = $"File size ({FormatBytes(fileSizeBytes)}) exceeds maximum allowed size ({FormatBytes(maxSizeBytes)})";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Formats bytes to human-readable string.
    /// </summary>
    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
