// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Import;
using Honua.Server.Core.Raster.Import;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Extension methods for mapping data ingestion administration endpoints.
/// </summary>
internal static class DataIngestionEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all data ingestion administration endpoints for uploading and importing geospatial data.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Creating ingestion jobs by uploading files (GeoJSON, Shapefile, GeoPackage, etc.)
    /// - Listing all ingestion jobs and their status
    /// - Getting details of specific ingestion jobs
    /// - Cancelling running ingestion jobs
    ///
    /// Supports multiple geospatial formats and validates file size limits.
    /// </remarks>
    /// <example>
    /// Example multipart form-data request to create an ingestion job:
    /// <code>
    /// POST /admin/ingestion/jobs
    /// Content-Type: multipart/form-data
    ///
    /// serviceId=my-service
    /// layerId=my-layer
    /// overwrite=false
    /// file=(binary data)
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapDataIngestionAdministration(this IEndpointRouteBuilder app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/ingestion")
            .RequireAuthorization("RequireDataPublisher")
            .WithTags("Data Ingestion");

        group.MapPost("/jobs", HandleCreateJob)
            .RequireRateLimiting("admin-operations")
            .DisableAntiforgery()
            .Accepts<IFormFileCollection>("multipart/form-data")
            .Produces<DataIngestionJobSnapshot>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("CreateDataIngestionJob");

        group.MapGet("/jobs", async ([FromServices] IDataIngestionService ingestionService, int pageSize = 25, string? pageToken = null, CancellationToken cancellationToken = default) =>
        {
            // Validate and constrain pageSize
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 25;

            // Get all jobs and apply pagination
            var allJobs = await ingestionService.ListJobsAsync(cancellationToken).ConfigureAwait(false);
            var startIndex = pageToken.IsNullOrEmpty() ? 0 : int.TryParse(pageToken, out var parsed) ? parsed : 0;

            // Ensure startIndex is valid
            if (startIndex < 0 || startIndex >= allJobs.Count)
            {
                startIndex = 0;
            }

            var pageJobs = allJobs.Skip(startIndex).Take(pageSize).ToList();
            var nextToken = (startIndex + pageSize < allJobs.Count) ? (startIndex + pageSize).ToString() : null;

            return Results.Ok(new PaginatedResponse<DataIngestionJobSnapshot>(
                Items: pageJobs,
                TotalCount: allJobs.Count,
                NextPageToken: nextToken
            ));
        })
            .Produces<PaginatedResponse<DataIngestionJobSnapshot>>(StatusCodes.Status200OK)
            .WithName("ListDataIngestionJobs");

        group.MapGet("/jobs/{jobId:guid}", async (Guid jobId, [FromServices] IDataIngestionService ingestionService, CancellationToken cancellationToken = default) =>
        {
            var snapshot = await ingestionService.TryGetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { job = snapshot });
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("GetDataIngestionJob");

        group.MapDelete("/jobs/{jobId:guid}", async (Guid jobId, [FromServices] IDataIngestionService ingestionService) =>
        {
            var snapshot = await ingestionService.CancelAsync(jobId, "Cancelled via API").ConfigureAwait(false);
            if (snapshot is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { job = snapshot });
        })
        .RequireRateLimiting("admin-operations")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("CancelDataIngestionJob");

        return app;
    }

    private static async Task<IResult> HandleCreateJob(HttpContext context, [FromServices] IDataIngestionService ingestionService, [FromServices] ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (!context.Request.HasFormContentType)
        {
            return ApiErrorResponse.Json.BadRequestResult("Multipart form-data upload is required.");
        }

        var form = await context.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var serviceId = form["serviceId"].FirstOrDefault();
        var layerId = form["layerId"].FirstOrDefault();
        var overwrite = TryParseBoolean(form["overwrite"].FirstOrDefault());

        if (serviceId.IsNullOrWhiteSpace() || layerId.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Json.BadRequestResult("Both serviceId and layerId form fields are required.");
        }

        if (overwrite)
        {
            return ApiErrorResponse.Json.BadRequestResult("Overwrite imports are not supported yet. Clear the destination layer before re-ingesting.");
        }

        var file = form.Files.Count switch
        {
            > 0 => form.Files[0],
            _ => null
        };

        // SECURITY: Validate file size (max 500MB per file to prevent DoS)
        const long MaxFileSizeBytes = 500L * 1024 * 1024; // 500MB

        // Validate content type for security
        var allowedContentTypes = new[]
        {
            "application/zip",
            "application/x-zip-compressed",
            "application/octet-stream", // Generic binary, allowed for backwards compatibility
            "application/json",
            "application/geo+json",
            "text/plain",
            "text/csv"
        };

        // Validate file extension
        var allowedExtensions = new[] { ".shp", ".geojson", ".json", ".gpkg", ".zip", ".kml", ".gml", ".csv" };

        var (isValid, errorMessage, safeFileName) = FormFileValidationHelper.ValidateUploadedFile(
            file,
            MaxFileSizeBytes,
            allowedContentTypes,
            allowedExtensions);

        if (!isValid)
        {
            if (errorMessage?.Contains("content type", StringComparison.OrdinalIgnoreCase) == true)
            {
                var logger = loggerFactory.CreateLogger("DataIngestion");
                logger.LogWarning("Rejected upload with invalid content type: {ContentType}", file?.ContentType);
            }
            return ApiErrorResponse.Json.BadRequestResult(errorMessage ?? "File validation failed");
        }

        var fileName = file!.FileName.IsNullOrWhiteSpace() ? "dataset" : Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(safeFileName!).ToLowerInvariant();
        var workingDirectory = CreateWorkingDirectory();
        var targetPath = Path.Combine(workingDirectory, safeFileName);

        try
        {
            await using (var stream = File.Create(targetPath))
            {
                await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            FilePermissionHelper.ApplyFilePermissions(targetPath);

            // Validate and extract zip files before processing
            if (extension == ".zip")
            {
                var zipValidationResult = await ValidateAndExtractZipFile(targetPath, workingDirectory, fileName, loggerFactory, cancellationToken).ConfigureAwait(false);
                if (!zipValidationResult.IsSuccess)
                {
                    TryCleanup(workingDirectory, loggerFactory.CreateLogger("DataIngestionCleanup"));
                    return ApiErrorResponse.Json.BadRequestResult(zipValidationResult.ErrorMessage ?? "Zip validation failed");
                }
            }

            var request = new DataIngestionRequest(serviceId, layerId, targetPath, workingDirectory, fileName, file.ContentType, overwrite);
            var snapshot = await ingestionService.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);

            // SECURITY FIX: File cleanup is now the responsibility of the ingestion service
            // The service will clean up files after processing or on error
            // Removing cleanup from here prevents race condition where files are deleted
            // before the background job can process them

            return Results.Accepted($"/admin/ingestion/jobs/{snapshot.JobId}", new { job = snapshot });
        }
        catch
        {
            // SECURITY FIX: Only cleanup on exception during enqueue operation
            // If EnqueueAsync succeeds, the service owns the files and will clean them up
            // If EnqueueAsync fails, we need to clean up the uploaded file
            TryCleanup(workingDirectory, loggerFactory.CreateLogger("DataIngestionCleanup"));
            throw;
        }
    }

    private static bool TryParseBoolean(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        return bool.TryParse(value, out var result) && result;
    }

    private static string CreateWorkingDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "honua-ingest", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        FilePermissionHelper.EnsureDirectorySecure(root);
        return root;
    }

    private static void TryCleanup(string path, ILogger logger)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove ingestion workspace {Path} during cleanup.", path);
        }
    }

    /// <summary>
    /// Validates that a ZIP entry path is within the working directory to prevent path traversal attacks.
    /// </summary>
    /// <param name="entryFullPath">The full path of the extracted entry.</param>
    /// <param name="workingDirectory">The working directory where files should be extracted.</param>
    /// <exception cref="SecurityException">Thrown if path traversal is detected.</exception>
    private static void ValidateZipEntryPath(string entryFullPath, string workingDirectory)
    {
        var normalizedEntry = Path.GetFullPath(entryFullPath);
        var normalizedWorkDir = Path.GetFullPath(workingDirectory);

        if (!normalizedEntry.StartsWith(normalizedWorkDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException($"Path traversal detected in ZIP entry");
        }
    }

    /// <summary>
    /// Sanitizes a file name for safe logging by removing path separators and traversal attempts.
    /// </summary>
    /// <param name="fileName">The file name to sanitize.</param>
    /// <returns>A sanitized file name safe for logging.</returns>
    private static string SanitizeFileName(string fileName)
    {
        return Path.GetFileName(fileName)?.Replace("..", "").Replace("/", "").Replace("\\", "") ?? "unknown";
    }

    private static async Task<ZipValidationResult> ValidateAndExtractZipFile(
        string zipFilePath,
        string workingDirectory,
        string fileName,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("ZipValidation");
        var sanitizedFileName = SanitizeFileName(fileName);

        try
        {
            // Get allowed extensions for geospatial data
            var allowedExtensions = ZipArchiveValidator.GetGeospatialExtensions();

            // SECURITY: Validate the zip archive with stricter compression ratio (30:1 instead of 100:1)
            logger.LogInformation("Validating zip archive: {SafeFileName}", sanitizedFileName);
            var validationResult = ZipArchiveValidator.ValidateZipFile(
                zipFilePath,
                allowedExtensions,
                maxUncompressedSize: 1L * 1024 * 1024 * 1024, // 1GB
                maxCompressionRatio: 30, // Reduced from 100 to 30 for better security against zip bombs
                maxEntries: 10_000);

            if (!validationResult.IsValid)
            {
                logger.LogWarning("Zip validation failed for {SafeFileName}: {Error}", sanitizedFileName, validationResult.ErrorMessage);
                return ZipValidationResult.Failure(validationResult.ErrorMessage ?? "Zip validation failed");
            }

            // Log warnings if any
            foreach (var warning in validationResult.Warnings)
            {
                logger.LogWarning("Zip validation warning for {SafeFileName}: {Warning}", sanitizedFileName, warning);
            }

            logger.LogInformation(
                "Zip archive validated successfully. File: {SafeFileName}, Entries: {Count}, Uncompressed size: {Size:N0} bytes",
                sanitizedFileName,
                validationResult.EntryCount,
                validationResult.TotalUncompressedSize);

            // SECURITY: Validate extraction paths to prevent path traversal
            var normalizedWorkDir = Path.GetFullPath(workingDirectory);

            // Extract the zip file to the working directory
            await using var zipStream = File.OpenRead(zipFilePath);
            var extractedFiles = ZipArchiveValidator.SafeExtract(zipStream, workingDirectory, validationResult);

            // SECURITY: Verify all extracted files are within the working directory (defense in depth)
            foreach (var extractedFile in extractedFiles)
            {
                ValidateZipEntryPath(extractedFile, normalizedWorkDir);
            }

            logger.LogInformation("Extracted {Count} files from zip archive {SafeFileName}", extractedFiles.Count, sanitizedFileName);

            return ZipValidationResult.Success();
        }
        catch (SecurityException ex)
        {
            logger.LogError(ex, "Security violation during zip extraction for {SafeFileName}", sanitizedFileName);
            return ZipValidationResult.Failure($"Security violation: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating or extracting zip file {SafeFileName}", sanitizedFileName);
            return ZipValidationResult.Failure($"Error processing zip file: {ex.Message}");
        }
    }

    private sealed record ZipValidationResult
    {
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }

        public static ZipValidationResult Success() => new() { IsSuccess = true };
        public static ZipValidationResult Failure(string error) => new() { IsSuccess = false, ErrorMessage = error };
    }

    private sealed record PaginatedResponse<T>(List<T> Items, int TotalCount, string? NextPageToken);
}
