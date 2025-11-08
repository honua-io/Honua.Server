// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Migration;
using Honua.Server.Core.Migration.GeoservicesRest;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Extension methods for mapping migration administration endpoints.
/// </summary>
internal static class MigrationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all migration administration endpoints for migrating data from external services.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Creating migration jobs from Esri GeoServices REST APIs
    /// - Listing all migration jobs and their status
    /// - Getting details of specific migration jobs including layer progress
    /// - Cancelling running migration jobs
    ///
    /// Supports migrating from ArcGIS Server, ArcGIS Online, and other Geoservices REST a.k.a. Esri REST endpoints.
    /// </remarks>
    /// <example>
    /// Example request to create a migration job:
    /// <code>
    /// POST /admin/migrations/jobs
    /// {
    ///   "sourceServiceUri": "https://services.arcgis.com/xyz/arcgis/rest/services/MyService/FeatureServer",
    ///   "targetServiceId": "my-service",
    ///   "targetFolderId": "imported",
    ///   "targetDataSourceId": "postgres-main"
    /// }
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapMigrationAdministration(this IEndpointRouteBuilder app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/migrations")
            .RequireAuthorization("RequireDataPublisher")
            .WithTags("Migration");

        group.MapPost("/jobs", HandleCreateJob)
            .RequireRateLimiting("admin-operations")
            .Produces<GeoservicesRestMigrationJobSnapshot>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithName("CreateMigrationJob");

        group.MapGet("/jobs", async (IEsriServiceMigrationService migrationService, int pageSize = 25, string? pageToken = null, CancellationToken cancellationToken = default) =>
        {
            // Validate and constrain pageSize
            if (pageSize > 100) pageSize = 100;
            if (pageSize < 1) pageSize = 25;

            // Get all jobs and apply pagination
            var allJobs = await migrationService.ListJobsAsync(cancellationToken).ConfigureAwait(false);
            var startIndex = pageToken.IsNullOrEmpty() ? 0 : int.TryParse(pageToken, out var parsed) ? parsed : 0;

            // Ensure startIndex is valid
            if (startIndex < 0 || startIndex >= allJobs.Count)
            {
                startIndex = 0;
            }

            var pageJobs = allJobs.Skip(startIndex).Take(pageSize).ToList();
            var nextToken = (startIndex + pageSize < allJobs.Count) ? (startIndex + pageSize).ToString() : null;

            return Results.Ok(new PaginatedResponse<GeoservicesRestMigrationJobSnapshot>(
                Items: pageJobs,
                TotalCount: allJobs.Count,
                NextPageToken: nextToken
            ));
        })
            .Produces<PaginatedResponse<GeoservicesRestMigrationJobSnapshot>>(StatusCodes.Status200OK)
            .WithName("ListMigrationJobs");

        group.MapGet("/jobs/{jobId:guid}", async (Guid jobId, IEsriServiceMigrationService migrationService, CancellationToken cancellationToken = default) =>
        {
            var snapshot = await migrationService.TryGetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { job = snapshot });
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("GetMigrationJob");

        group.MapDelete("/jobs/{jobId:guid}", async (Guid jobId, IEsriServiceMigrationService migrationService) =>
        {
            var snapshot = await migrationService.CancelAsync(jobId, "Cancelled via API").ConfigureAwait(false);
            if (snapshot is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new { job = snapshot });
        })
        .RequireRateLimiting("admin-operations")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithName("CancelMigrationJob");

        return app;
    }

    private static async Task<IResult> HandleCreateJob(
        HttpContext context,
        IEsriServiceMigrationService migrationService,
        ILogger<IEsriServiceMigrationService> logger,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return ApiErrorResponse.Json.BadRequestResult("Request content type must be application/json.");
        }

        EsriServiceMigrationRequestDto? dto;
        try
        {
            dto = await context.Request.ReadFromJsonAsync<EsriServiceMigrationRequestDto>(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse migration job request body");
            return ApiErrorResponse.Json.BadRequestResult("Invalid request body. Please ensure the JSON is well-formed and matches the expected schema.");
        }

        if (dto is null)
        {
            return ApiErrorResponse.Json.BadRequestResult("Request body is required.");
        }

        if (dto.SourceServiceUri.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Json.BadRequestResult("sourceServiceUri is required.");
        }

        if (dto.TargetServiceId.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Json.BadRequestResult("targetServiceId is required.");
        }

        if (dto.TargetFolderId.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Json.BadRequestResult("targetFolderId is required.");
        }

        if (dto.TargetDataSourceId.IsNullOrWhiteSpace())
        {
            return ApiErrorResponse.Json.BadRequestResult("targetDataSourceId is required.");
        }

        if (!Uri.TryCreate(dto.SourceServiceUri, UriKind.Absolute, out var sourceUri))
        {
            return ApiErrorResponse.Json.BadRequestResult("sourceServiceUri must be a valid absolute URI.");
        }

        var request = new EsriServiceMigrationRequest
        {
            SourceServiceUri = sourceUri,
            TargetServiceId = dto.TargetServiceId,
            TargetFolderId = dto.TargetFolderId,
            TargetDataSourceId = dto.TargetDataSourceId,
            LayerIds = dto.LayerIds,
            IncludeData = dto.IncludeData ?? true,
            BatchSize = dto.BatchSize,
            TranslatorOptions = dto.TranslatorOptions is not null
                ? new GeoservicesRestMetadataTranslatorOptions
                {
                    ServiceTitle = dto.TranslatorOptions.ServiceTitle,
                    ServiceDescription = dto.TranslatorOptions.ServiceDescription,
                    TableNamePrefix = dto.TranslatorOptions.TableNamePrefix,
                    GeometryColumnName = dto.TranslatorOptions.GeometryColumnName ?? "shape",
                    LayerIdPrefix = dto.TranslatorOptions.LayerIdPrefix,
                    DefaultKeywordList = dto.TranslatorOptions.DefaultKeywordList,
                    UseLayerIdsForTables = dto.TranslatorOptions.UseLayerIdsForTables ?? false
                }
                : null
        };

        try
        {
            var snapshot = await migrationService.EnqueueAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Accepted($"/admin/migrations/jobs/{snapshot.JobId}", new { job = snapshot });
        }
        catch (ArgumentException ex)
        {
            // ArgumentException typically contains validation errors safe to expose
            logger.LogWarning(ex, "Migration job validation failed - SourceUri={SourceUri}, TargetService={TargetService}",
                request.SourceServiceUri, request.TargetServiceId);
            return ApiErrorResponse.Json.BadRequestResult($"Invalid migration configuration: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // InvalidOperationException may contain safe operational error details
            logger.LogWarning(ex, "Migration job operation failed - SourceUri={SourceUri}, TargetService={TargetService}",
                request.SourceServiceUri, request.TargetServiceId);
            return ApiErrorResponse.Json.BadRequestResult($"Cannot enqueue migration job: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log full exception details internally but return sanitized error to client
            logger.LogError(ex, "Failed to enqueue migration job - SourceUri={SourceUri}, TargetService={TargetService}",
                request.SourceServiceUri, request.TargetServiceId);
            return Results.Problem(
                detail: "An error occurred while enqueueing the migration job. Please check the logs for details or contact support.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private sealed record EsriServiceMigrationRequestDto(
        string? SourceServiceUri,
        string? TargetServiceId,
        string? TargetFolderId,
        string? TargetDataSourceId,
        IReadOnlyCollection<int>? LayerIds,
        bool? IncludeData,
        int? BatchSize,
        TranslatorOptionsDto? TranslatorOptions
    );

    private sealed record TranslatorOptionsDto(
        string? ServiceTitle,
        string? ServiceDescription,
        string? TableNamePrefix,
        string? GeometryColumnName,
        string? LayerIdPrefix,
        string? DefaultKeywordList,
        bool? UseLayerIdsForTables
    );
}
