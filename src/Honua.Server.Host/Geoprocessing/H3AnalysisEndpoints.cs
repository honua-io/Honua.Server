// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
// TODO: Re-enable when Honua.Server.Enterprise is available
// using Honua.Server.Enterprise.Geoprocessing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Geoprocessing;

/// <summary>
/// H3 Hexagonal Binning Analysis endpoints
/// Provides REST API for H3 spatial aggregation operations
/// </summary>
/// <remarks>
/// TODO: Re-enable when Honua.Server.Enterprise is available.
/// This endpoint depends on IControlPlane and other Enterprise features.
/// </remarks>
/*
public static class H3AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapH3AnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/analysis/h3")
            .WithTags("H3 Analysis")
            .RequireAuthorization();

        // POST /api/analysis/h3/bin - Bin data into H3 hexagons
        group.MapPost("/bin", BinDataToH3)
            .WithName("BinDataToH3")
            .WithOpenApi(op =>
            {
                op.Summary = "Bin point data into H3 hexagons";
                op.Description = "Aggregates point data into H3 hexagonal grid cells with statistical functions. " +
                                "Supports resolutions 0-15 and various aggregation types (count, sum, avg, min, max).";
                return op;
            });

        // POST /api/analysis/h3/info - Get information about H3 resolution
        group.MapPost("/info", GetH3Info)
            .WithName("GetH3Info")
            .WithOpenApi(op =>
            {
                op.Summary = "Get H3 resolution information";
                op.Description = "Returns details about average hexagon size, area, and edge length for a given H3 resolution";
                return op;
            });

        // POST /api/analysis/h3/boundary - Get boundary for H3 index
        group.MapPost("/boundary", GetH3Boundary)
            .WithName("GetH3Boundary")
            .WithOpenApi(op =>
            {
                op.Summary = "Get H3 hexagon boundary";
                op.Description = "Returns the polygon boundary for a given H3 index";
                return op;
            });

        // POST /api/analysis/h3/neighbors - Get neighboring hexagons
        group.MapPost("/neighbors", GetH3Neighbors)
            .WithName("GetH3Neighbors")
            .WithOpenApi(op =>
            {
                op.Summary = "Get H3 neighboring hexagons";
                op.Description = "Returns neighboring hexagons for a given H3 index with optional ring distance";
                return op;
            });

        return endpoints;
    }

    private static async Task<IResult> BinDataToH3(
        [FromBody] H3BinRequest request,
        [FromServices] IControlPlane controlPlane,
        [FromServices] ILogger<H3BinRequest> logger,
        HttpContext context,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        try
        {
            // Extract user info
            var tenantId = Guid.Parse(user.FindFirstValue("tenant_id") ?? throw new UnauthorizedAccessException("Tenant ID not found"));
            var userId = Guid.Parse(user.FindFirstValue("sub") ?? throw new UnauthorizedAccessException("User ID not found"));
            var userEmail = user.FindFirstValue("email");

            // Build execution request
            var execRequest = new ProcessExecutionRequest
            {
                ProcessId = "h3_binning",
                TenantId = tenantId,
                UserId = userId,
                UserEmail = userEmail,
                Inputs = new Dictionary<string, object>
                {
                    ["resolution"] = request.Resolution,
                    ["aggregation"] = request.Aggregation,
                    ["valueField"] = request.ValueField ?? "",
                    ["includeBoundaries"] = request.IncludeBoundaries,
                    ["includeStatistics"] = request.IncludeStatistics
                },
                Mode = request.Async ? ExecutionMode.Async : ExecutionMode.Sync,
                Metadata = new Dictionary<string, object>
                {
                    ["source"] = "H3 API",
                    ["resolution"] = request.Resolution
                }
            };

            // Add input data
            execRequest.InputData = new List<GeoprocessingInput>
            {
                new()
                {
                    Name = "points",
                    Type = request.InputType ?? "geojson",
                    Source = request.InputSource ?? ""
                }
            };

            // Admission control
            var admission = await controlPlane.AdmitAsync(execRequest, ct);

            if (!admission.Admitted)
            {
                return Results.BadRequest(new
                {
                    error = "Request denied",
                    reasons = admission.DenialReasons
                });
            }

            // Execute based on mode
            if (admission.ExecutionMode == ExecutionMode.Sync)
            {
                var result = await controlPlane.ExecuteInlineAsync(admission, ct);

                return Results.Ok(new H3BinResponse
                {
                    JobId = result.JobId,
                    Status = "completed",
                    Result = result.Output
                });
            }
            else
            {
                var run = await controlPlane.EnqueueAsync(admission, ct);

                context.Response.Headers.Location = $"/api/analysis/h3/jobs/{run.JobId}";

                return Results.Accepted($"/api/analysis/h3/jobs/{run.JobId}", new H3BinResponse
                {
                    JobId = run.JobId,
                    Status = "accepted",
                    Message = "Job queued for processing"
                });
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized access attempt");
            return Results.Unauthorized();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to execute H3 binning");
            return Results.Problem("Failed to execute H3 binning", statusCode: 500);
        }
    }

    private static async Task<IResult> GetH3Info(
        [FromBody] H3InfoRequest request,
        [FromServices] ILogger<H3InfoRequest> logger,
        CancellationToken ct)
    {
        try
        {
            if (request.Resolution < 0 || request.Resolution > 15)
            {
                return Results.BadRequest(new { error = "Resolution must be between 0 and 15" });
            }

            var h3Service = new Enterprise.Geoprocessing.Operations.H3Service();

            var info = new H3InfoResponse
            {
                Resolution = request.Resolution,
                AverageAreaKm2 = h3Service.GetAverageArea(request.Resolution) / 1_000_000, // Convert m² to km²
                AverageAreaM2 = h3Service.GetAverageArea(request.Resolution),
                AverageEdgeLengthKm = h3Service.GetAverageEdgeLength(request.Resolution) / 1000, // Convert m to km
                AverageEdgeLengthM = h3Service.GetAverageEdgeLength(request.Resolution),
                TotalCells = GetTotalCellsForResolution(request.Resolution)
            };

            return Results.Ok(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get H3 info for resolution {Resolution}", request.Resolution);
            return Results.Problem("Failed to get H3 information", statusCode: 500);
        }
    }

    private static async Task<IResult> GetH3Boundary(
        [FromBody] H3BoundaryRequest request,
        [FromServices] ILogger<H3BoundaryRequest> logger,
        CancellationToken ct)
    {
        try
        {
            var h3Service = new Enterprise.Geoprocessing.Operations.H3Service();

            if (!h3Service.IsValidH3Index(request.H3Index))
            {
                return Results.BadRequest(new { error = "Invalid H3 index" });
            }

            var boundary = h3Service.GetH3Boundary(request.H3Index);
            var center = h3Service.GetH3Center(request.H3Index);
            var area = h3Service.GetH3Area(request.H3Index);
            var resolution = h3Service.GetH3Resolution(request.H3Index);

            var response = new H3BoundaryResponse
            {
                H3Index = request.H3Index,
                Resolution = resolution,
                Boundary = ConvertPolygonToGeoJSON(boundary),
                Center = new[] { center.X, center.Y },
                AreaM2 = area,
                AreaKm2 = area / 1_000_000
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get H3 boundary for index {H3Index}", request.H3Index);
            return Results.Problem("Failed to get H3 boundary", statusCode: 500);
        }
    }

    private static async Task<IResult> GetH3Neighbors(
        [FromBody] H3NeighborsRequest request,
        [FromServices] ILogger<H3NeighborsRequest> logger,
        CancellationToken ct)
    {
        try
        {
            var h3Service = new Enterprise.Geoprocessing.Operations.H3Service();

            if (!h3Service.IsValidH3Index(request.H3Index))
            {
                return Results.BadRequest(new { error = "Invalid H3 index" });
            }

            var neighbors = request.RingDistance > 1
                ? h3Service.GetH3Ring(request.H3Index, request.RingDistance)
                : h3Service.GetH3Neighbors(request.H3Index);

            var response = new H3NeighborsResponse
            {
                H3Index = request.H3Index,
                RingDistance = request.RingDistance,
                Neighbors = neighbors,
                Count = neighbors.Count
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get H3 neighbors for index {H3Index}", request.H3Index);
            return Results.Problem("Failed to get H3 neighbors", statusCode: 500);
        }
    }

    // Helper methods

    private static long GetTotalCellsForResolution(int resolution)
    {
        // Approximate number of H3 cells at each resolution
        // Formula: 2 + 120 * 7^resolution
        return 2 + (long)(120 * Math.Pow(7, resolution));
    }

    private static object ConvertPolygonToGeoJSON(NetTopologySuite.Geometries.Polygon polygon)
    {
        var coordinates = new List<List<double[]>>();
        var ring = new List<double[]>();

        foreach (var coord in polygon.Coordinates)
        {
            ring.Add(new[] { coord.X, coord.Y });
        }
        coordinates.Add(ring);

        return new
        {
            type = "Polygon",
            coordinates
        };
    }
}

// Request/Response models

public record H3BinRequest
{
    public int Resolution { get; init; } = 7;
    public string Aggregation { get; init; } = "count";
    public string? ValueField { get; init; }
    public bool IncludeBoundaries { get; init; } = true;
    public bool IncludeStatistics { get; init; } = false;
    public bool Async { get; init; } = false;
    public string? InputType { get; init; } = "geojson";
    public string? InputSource { get; init; }
}

public record H3BinResponse
{
    public required string JobId { get; init; }
    public required string Status { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, object>? Result { get; init; }
}

public record H3InfoRequest
{
    public int Resolution { get; init; }
}

public record H3InfoResponse
{
    public int Resolution { get; init; }
    public double AverageAreaKm2 { get; init; }
    public double AverageAreaM2 { get; init; }
    public double AverageEdgeLengthKm { get; init; }
    public double AverageEdgeLengthM { get; init; }
    public long TotalCells { get; init; }
}

public record H3BoundaryRequest
{
    public required string H3Index { get; init; }
}

public record H3BoundaryResponse
{
    public required string H3Index { get; init; }
    public int Resolution { get; init; }
    public required object Boundary { get; init; }
    public required double[] Center { get; init; }
    public double AreaM2 { get; init; }
    public double AreaKm2 { get; init; }
}

public record H3NeighborsRequest
{
    public required string H3Index { get; init; }
    public int RingDistance { get; init; } = 1;
}

public record H3NeighborsResponse
{
    public required string H3Index { get; init; }
    public int RingDistance { get; init; }
    public required List<string> Neighbors { get; init; }
    public int Count { get; init; }
}
*/
