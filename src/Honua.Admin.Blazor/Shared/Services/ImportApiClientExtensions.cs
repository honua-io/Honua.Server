// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using System.Text.Json;
using Honua.MapSDK.Models.Import;
using Honua.MapSDK.Services.Import;
using Microsoft.AspNetCore.Components.Forms;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Extensions for ImportApiClient to support instant upload with format detection
/// </summary>
public static class ImportApiClientExtensions
{
    /// <summary>
    /// Parse file locally with format detection (client-side)
    /// </summary>
    public static async Task<ParsedData> ParseFileAsync(
        this ImportApiClient client,
        IBrowserFile file,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(10);

        // Read file content
        using var stream = file.OpenReadStream(500 * 1024 * 1024, cancellationToken);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var content = ms.ToArray();

        progress?.Report(40);

        // Detect format
        var formatDetector = new EnhancedFormatDetectionService();
        var detectionResult = await formatDetector.DetectFormatAsync(content, file.Name, cancellationToken);

        if (detectionResult.Format == ImportFormat.Unknown)
        {
            throw new InvalidOperationException("Unable to detect file format");
        }

        progress?.Report(60);

        // Parse data
        var parserFactory = new FileParserFactory();
        var parser = parserFactory.GetParser(detectionResult.Format);

        if (parser == null)
        {
            throw new InvalidOperationException($"No parser available for format: {detectionResult.Format}");
        }

        var parsedData = await parser.ParseAsync(content, file.Name, cancellationToken);

        progress?.Report(100);

        return parsedData;
    }

    /// <summary>
    /// Detect file format without parsing
    /// </summary>
    public static async Task<EnhancedFormatDetectionResult> DetectFormatAsync(
        this ImportApiClient client,
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        // Read first 2KB for format detection
        using var stream = file.OpenReadStream(500 * 1024 * 1024, cancellationToken);
        var buffer = new byte[2048];
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        var content = buffer.Take(bytesRead).ToArray();

        var formatDetector = new EnhancedFormatDetectionService();
        return await formatDetector.DetectFormatAsync(content, file.Name, cancellationToken);
    }

    /// <summary>
    /// Generate style for parsed data
    /// </summary>
    public static StyleDefinition GenerateStyle(
        this ImportApiClient client,
        ParsedData data,
        string? geometryType = null)
    {
        var stylingService = new AutoStylingService();
        return stylingService.GenerateStyle(data, geometryType);
    }

    /// <summary>
    /// Create import job with instant preview (parse locally first, then upload)
    /// </summary>
    public static async Task<InstantUploadResult> CreateInstantImportJobAsync(
        this ImportApiClient client,
        string serviceId,
        string layerId,
        IBrowserFile file,
        bool overwrite = false,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new InstantUploadResult();

        try
        {
            // Phase 1: Parse locally for instant preview (0-50%)
            progress?.Report(5);
            result.ParsedData = await client.ParseFileAsync(
                file,
                new Progress<double>(p => progress?.Report(p * 0.5)),
                cancellationToken);

            progress?.Report(50);

            // Phase 2: Generate style (50-60%)
            result.Style = client.GenerateStyle(result.ParsedData);
            progress?.Report(60);

            // Phase 3: Upload to server (60-100%)
            result.Job = await client.CreateImportJobAsync(
                serviceId,
                layerId,
                file,
                overwrite,
                new Progress<double>(p => progress?.Report(60 + (p * 0.4))),
                cancellationToken);

            result.Success = result.Job != null;
            progress?.Report(100);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }
}

/// <summary>
/// Result of instant upload with preview
/// </summary>
public class InstantUploadResult
{
    public bool Success { get; set; }
    public ParsedData? ParsedData { get; set; }
    public StyleDefinition? Style { get; set; }
    public ImportJobSnapshot? Job { get; set; }
    public string? Error { get; set; }
}
