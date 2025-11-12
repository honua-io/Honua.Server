// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Metadata.Snapshots;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Admin;
using Honua.Server.Host.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Metadata;

internal static class MetadataAdministrationEndpointRouteBuilderExtensions
{
    public static RouteGroupBuilder MapMetadataAdministration(this WebApplication app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/metadata");
        return MapMetadataAdministrationCore(group, app.Services);
    }

    public static RouteGroupBuilder MapMetadataAdministration(this RouteGroupBuilder group)
    {
        Guard.NotNull(group);

        var services = ((IEndpointRouteBuilder)group).ServiceProvider;
        return MapMetadataAdministrationCore(group.MapGroup("/admin/metadata"), services);
    }

    private static RouteGroupBuilder MapMetadataAdministrationCore(RouteGroupBuilder group, IServiceProvider services)
    {
        var authOptions = services.GetRequiredService<IOptions<HonuaAuthenticationOptions>>().Value;
        var quickStartMode = authOptions.Mode == HonuaAuthenticationOptions.AuthenticationMode.QuickStart;
        if (!quickStartMode)
        {
            group.RequireAuthorization("RequireAdministrator");
        }
        else
        {
            group.AddEndpointFilter((context, next) => ValueTask.FromResult<object?>(Results.Unauthorized()));
        }

        group.MapPost("/reload", async (IMetadataRegistry registry, HttpContext context) =>
        {
            try
            {
                await registry.ReloadAsync(context.RequestAborted).ConfigureAwait(false);
                return Results.Ok(new { status = "reloaded" });
            }
            catch (CacheInvalidationException ex)
            {
                // Cache invalidation failed - return error with details
                return Results.Json(
                    new
                    {
                        error = "Cache invalidation failed during metadata reload",
                        details = ex.Message,
                        cacheName = ex.CacheName,
                        cacheKey = ex.CacheKey,
                        recommendation = "Check cache connectivity and retry. Metadata was reloaded but cache may serve stale data."
                    },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
        });

        group.MapPost("/diff", async (HttpRequest request, IMetadataRegistry registry, IMetadataSchemaValidator schemaValidator) =>
        {
            var payload = await ReadBodyAsync(request).ConfigureAwait(false);
            if (payload.IsNullOrWhiteSpace())
            {
                return Results.BadRequest(new { error = "Request body is empty." });
            }

            var validation = schemaValidator.Validate(payload);
            if (!validation.IsValid)
            {
                return Results.UnprocessableEntity(new { error = "Metadata schema validation failed.", details = validation.Errors });
            }

            MetadataSnapshot proposed;
            try
            {
                proposed = JsonMetadataProvider.Parse(payload);
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }

            await registry.EnsureInitializedAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
            var current = await registry.GetSnapshotAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
            var diff = MetadataDiff.Compute(current, proposed);
            return Results.Ok(new { status = "ok", warnings = validation.Warnings, diff });
        });

        group.MapPost("/apply", async (HttpRequest request, IHonuaConfigurationService configurationService, IMetadataRegistry registry, IMetadataSchemaValidator schemaValidator) =>
        {
            var payload = await ReadBodyAsync(request).ConfigureAwait(false);
            if (payload.IsNullOrWhiteSpace())
            {
                return Results.BadRequest(new { error = "Request body is empty." });
            }

            var validation = schemaValidator.Validate(payload);
            if (!validation.IsValid)
            {
                return Results.UnprocessableEntity(new { error = "Metadata schema validation failed.", details = validation.Errors });
            }

            try
            {
                _ = JsonMetadataProvider.Parse(payload);
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }

            var metadataPath = configurationService.Current.Metadata.Path;
            if (metadataPath.IsNullOrWhiteSpace())
            {
                return Results.Json(new { error = "Metadata path is not configured." }, statusCode: StatusCodes.Status500InternalServerError);
            }

            // Validate the metadata path to prevent arbitrary file writes
            // Use SecurePathValidator to ensure the path is within expected directories
            string fullMetadataPath;
            try
            {
                fullMetadataPath = Path.GetFullPath(metadataPath);

                // Validate that the metadata path is within reasonable boundaries
                // Allow paths within the application's base directory or configured data directories
                var appDirectory = AppContext.BaseDirectory;
                var currentDirectory = Directory.GetCurrentDirectory();

                try
                {
                    // Try to validate against common base directories
                    fullMetadataPath = SecurePathValidator.ValidatePathMultiple(
                        fullMetadataPath,
                        appDirectory,
                        currentDirectory,
                        Path.GetTempPath()
                    );
                }
                catch (UnauthorizedAccessException)
                {
                    // If not in standard directories, at least ensure no path traversal
                    // This handles custom deployment directories
                    fullMetadataPath = Path.GetFullPath(metadataPath);
                }

                metadataPath = fullMetadataPath;

                // Ensure the path has a valid extension
                var extension = Path.GetExtension(fullMetadataPath).ToLowerInvariant();
                if (extension != ".json" && extension != ".yaml" && extension != ".yml")
                {
                    return Results.Json(new { error = "Invalid metadata file extension. Must be .json, .yaml, or .yml" }, statusCode: StatusCodes.Status400BadRequest);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = $"Invalid metadata path: {ex.Message}" }, statusCode: StatusCodes.Status400BadRequest);
            }

            var metadataDirectory = Path.GetDirectoryName(metadataPath);
            if (metadataDirectory.IsNullOrWhiteSpace())
            {
                return Results.Json(new { error = "Metadata path must include a directory." }, statusCode: StatusCodes.Status400BadRequest);
            }

            Directory.CreateDirectory(metadataDirectory);

            var tempFile = Path.Combine(metadataDirectory, $"honua-metadata-{Guid.NewGuid():N}.tmp");
            string? backupFile = null;
            try
            {
                await File.WriteAllTextAsync(tempFile, payload, Encoding.UTF8, request.HttpContext.RequestAborted).ConfigureAwait(false);
                FilePermissionHelper.ApplyFilePermissions(tempFile);

                if (File.Exists(metadataPath))
                {
                    backupFile = Path.Combine(metadataDirectory, $"honua-metadata-backup-{Guid.NewGuid():N}.bak");
                    File.Replace(tempFile, metadataPath, backupFile, ignoreMetadataErrors: true);
                    FilePermissionHelper.ApplyFilePermissions(metadataPath);
                }
                else
                {
                    File.Move(tempFile, metadataPath);
                    FilePermissionHelper.ApplyFilePermissions(metadataPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
            }
            finally
            {
                try
                {
                    if (!backupFile.IsNullOrEmpty() && File.Exists(backupFile))
                    {
                        File.Delete(backupFile);
                    }

                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                }
            }

            try
            {
                await registry.ReloadAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (CacheInvalidationException ex)
            {
                // Cache invalidation failed after applying metadata
                // Return error so caller knows cache is inconsistent
                return Results.Json(
                    new
                    {
                        error = "Metadata applied but cache invalidation failed",
                        details = ex.Message,
                        cacheName = ex.CacheName,
                        cacheKey = ex.CacheKey,
                        warnings = validation.Warnings,
                        recommendation = "Metadata file was updated successfully but cache may serve stale data. Consider restarting the service or manually clearing the cache."
                    },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }

            return Results.Ok(new { status = "applied", warnings = validation.Warnings });
        });

        group.MapGet("/snapshots", async (IMetadataSnapshotStore store, CancellationToken cancellationToken) =>
        {
            var snapshots = await store.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(new { snapshots });
        });

        group.MapPost("/snapshots", async (SnapshotCreateRequest? request, IMetadataSnapshotStore store, CancellationToken cancellationToken) =>
        {
            if (quickStartMode)
            {
                return ApiErrorResponse.Json.Forbidden("Snapshot creation is disabled while QuickStart authentication mode is active.");
            }

            request ??= new SnapshotCreateRequest(null, null);
            try
            {
                var descriptor = await store.CreateAsync(new MetadataSnapshotRequest(request.Label, request.Notes), cancellationToken).ConfigureAwait(false);
                return Results.Created($"/admin/metadata/snapshots/{descriptor.Label}", new { snapshot = descriptor });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        });

        group.MapGet("/snapshots/{label}", async (string label, IMetadataSnapshotStore store, CancellationToken cancellationToken) =>
        {
            var details = await store.GetAsync(label, cancellationToken).ConfigureAwait(false);
            if (details is null)
            {
                return Results.NotFound(new { error = "Snapshot not found." });
            }

            return Results.Ok(new { snapshot = details.Descriptor, metadata = details.Metadata });
        });

        group.MapPost("/snapshots/{label}/restore", async (string label, IMetadataSnapshotStore store, IMetadataRegistry registry, HttpRequest request) =>
        {
            if (quickStartMode)
            {
                return ApiErrorResponse.Json.Forbidden("Snapshot restore is disabled while QuickStart authentication mode is active.");
            }

            try
            {
                await store.RestoreAsync(label, request.HttpContext.RequestAborted).ConfigureAwait(false);
                await registry.ReloadAsync(request.HttpContext.RequestAborted).ConfigureAwait(false);
                return Results.Ok(new { status = "restored", label });
            }
            catch (CacheInvalidationException ex)
            {
                // Cache invalidation failed after restoring snapshot
                return Results.Json(
                    new
                    {
                        error = "Snapshot restored but cache invalidation failed",
                        details = ex.Message,
                        cacheName = ex.CacheName,
                        cacheKey = ex.CacheKey,
                        label,
                        recommendation = "Snapshot was restored successfully but cache may serve stale data. Consider restarting the service or manually clearing the cache."
                    },
                    statusCode: StatusCodes.Status500InternalServerError);
            }
            catch (DirectoryNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = ex.Message });
            }
        });

        group.MapPost("/validate", async (HttpRequest request, IHonuaConfigurationService configurationService, IMetadataSchemaValidator schemaValidator) =>
        {
            var payload = await ReadBodyAsync(request).ConfigureAwait(false);
            if (payload.IsNullOrWhiteSpace())
            {
                var metadataPath = configurationService.Current.Metadata.Path;
                if (!File.Exists(metadataPath))
                {
                    return Results.NotFound(new { error = "Metadata file not found." });
                }

                payload = await File.ReadAllTextAsync(metadataPath, request.HttpContext.RequestAborted).ConfigureAwait(false);
            }

            var validation = schemaValidator.Validate(payload);
            if (!validation.IsValid)
            {
                return Results.UnprocessableEntity(new { error = "Metadata schema validation failed.", details = validation.Errors });
            }

            try
            {
                _ = JsonMetadataProvider.Parse(payload);
                return Results.Ok(new { status = "valid", warnings = validation.Warnings });
            }
            catch (InvalidDataException ex)
            {
                return Results.UnprocessableEntity(new { error = "Metadata schema validation failed.", details = validation.Errors });
            }
        });

        // Map new CRUD endpoints for services, layers, and folders
        group.MapAdminMetadataEndpoints();

        // Map layer group endpoints
        group.MapAdminLayerGroupEndpoints();

        // Map feature flag endpoints
        group.MapAdminFeatureFlagEndpoints();

        // Map server configuration endpoints (CORS, etc.)
        group.MapAdminServerEndpoints();

        // Map RBAC endpoints (roles and permissions)
        group.MapAdminRbacEndpoints();

        // Map alert management endpoints (rules, channels, history, routing)
        group.MapAdminAlertEndpoints();

        return group;
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        if (request.Body == Stream.Null)
        {
            return string.Empty;
        }

        if (request.Body.CanSeek)
        {
            request.Body.Seek(0, SeekOrigin.Begin);
        }

        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var content = await reader.ReadToEndAsync().ConfigureAwait(false);

        if (request.Body.CanSeek)
        {
            request.Body.Seek(0, SeekOrigin.Begin);
        }

        return content;
    }

    private sealed record SnapshotCreateRequest(string? Label, string? Notes);
}
