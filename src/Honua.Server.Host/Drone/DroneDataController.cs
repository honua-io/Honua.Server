// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Asp.Versioning;
using Honua.Server.Core.Models.Drone;
using Honua.MapSDK.Services.Drone;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Honua.Server.Host.Drone;

/// <summary>
/// REST API controller for drone survey data
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/drones")]
[Produces("application/json")]
public class DroneDataController : ControllerBase
{
    private readonly DroneDataService droneDataService;
    private readonly PointCloudService pointCloudService;
    private readonly OrthomosaicService orthomosaicService;
    private readonly ILogger<DroneDataController> logger;

    public DroneDataController(
        DroneDataService droneDataService,
        PointCloudService pointCloudService,
        OrthomosaicService orthomosaicService,
        ILogger<DroneDataController> logger)
    {
        this.droneDataService = droneDataService;
        this.pointCloudService = pointCloudService;
        this.orthomosaicService = orthomosaicService;
        this.logger = logger;
    }

    #region Survey Endpoints

    /// <summary>
    /// Retrieves a paginated list of all drone surveys.
    /// </summary>
    /// <param name="limit">Maximum number of surveys to return (default: 100, max: 1000).</param>
    /// <param name="offset">Number of surveys to skip for pagination (default: 0).</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>A collection of drone survey summaries.</returns>
    /// <response code="200">Survey list retrieved successfully</response>
    /// <remarks>
    /// Use limit and offset parameters for pagination through large survey collections.
    /// Each summary includes basic survey information without detailed data.
    /// </remarks>
    [HttpGet("surveys")]
    [ProducesResponseType(typeof(IEnumerable<DroneSurveySummary>), 200)]
    public async Task<ActionResult<IEnumerable<DroneSurveySummary>>> ListSurveys(
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var surveys = await this.droneDataService.ListSurveysAsync(limit, offset, cancellationToken);
        return this.Ok(surveys);
    }

    /// <summary>
    /// Retrieves detailed information for a specific drone survey.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Complete survey details including metadata and configuration.</returns>
    /// <response code="200">Survey found and returned successfully</response>
    /// <response code="404">Survey with the specified ID not found</response>
    [HttpGet("surveys/{surveyId}")]
    [ProducesResponseType(typeof(DroneSurvey), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DroneSurvey>> GetSurvey(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var survey = await this.droneDataService.GetSurveyAsync(surveyId, cancellationToken);

        if (survey == null)
        {
            return this.NotFound(new { message = $"Survey {surveyId} not found" });
        }

        return this.Ok(survey);
    }

    /// <summary>
    /// Creates a new drone survey project.
    /// </summary>
    /// <param name="dto">Survey creation request containing survey metadata and configuration.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>The newly created survey with assigned ID and timestamps.</returns>
    /// <response code="201">Survey created successfully</response>
    /// <response code="400">Invalid survey data or configuration</response>
    /// <remarks>
    /// A survey serves as a container for drone-collected data including:
    /// - Point clouds from LiDAR or photogrammetry
    /// - Orthomosaics from aerial imagery
    /// - Flight metadata and capture parameters
    /// </remarks>
    [HttpPost("surveys")]
    [ProducesResponseType(typeof(DroneSurvey), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<DroneSurvey>> CreateSurvey(
        [FromBody] CreateDroneSurveyDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var survey = await this.droneDataService.CreateSurveyAsync(dto, cancellationToken);
            return this.CreatedAtAction(
                nameof(GetSurvey),
                new { surveyId = survey.Id },
                survey);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to create survey");
            return this.BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a drone survey and all associated data.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey to delete.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <response code="204">Survey deleted successfully</response>
    /// <response code="404">Survey with the specified ID not found</response>
    /// <remarks>
    /// WARNING: This operation is irreversible and will delete all associated data including:
    /// - Point cloud data
    /// - Orthomosaics
    /// - Flight metadata
    /// - Processing results
    /// </remarks>
    [HttpDelete("surveys/{surveyId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteSurvey(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await this.droneDataService.DeleteSurveyAsync(surveyId, cancellationToken);

        if (!deleted)
        {
            return this.NotFound(new { message = $"Survey {surveyId} not found" });
        }

        return this.NoContent();
    }

    /// <summary>
    /// Retrieves statistical information about a drone survey's data.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Survey statistics including point counts, coverage area, and data quality metrics.</returns>
    /// <response code="200">Statistics retrieved successfully</response>
    /// <response code="404">Survey with the specified ID not found</response>
    /// <remarks>
    /// Statistics include:
    /// - Total point count in point clouds
    /// - Coverage area in square meters
    /// - Ground sampling distance (GSD)
    /// - Elevation range
    /// - Classification distribution
    /// </remarks>
    [HttpGet("surveys/{surveyId}/statistics")]
    [ProducesResponseType(typeof(DroneSurveyStatistics), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DroneSurveyStatistics>> GetSurveyStatistics(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await this.droneDataService.GetSurveyStatisticsAsync(surveyId, cancellationToken);
            return this.Ok(stats);
        }
        catch (KeyNotFoundException)
        {
            return this.NotFound(new { message = $"Survey {surveyId} not found" });
        }
    }

    #endregion

    #region Point Cloud Endpoints

    /// <summary>
    /// Queries point cloud data within a specified 3D bounding box with optional filtering.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey containing the point cloud.</param>
    /// <param name="minX">Minimum X coordinate of the bounding box.</param>
    /// <param name="minY">Minimum Y coordinate of the bounding box.</param>
    /// <param name="maxX">Maximum X coordinate of the bounding box.</param>
    /// <param name="maxY">Maximum Y coordinate of the bounding box.</param>
    /// <param name="minZ">Minimum Z coordinate (elevation) of the bounding box (default: -10000).</param>
    /// <param name="maxZ">Maximum Z coordinate (elevation) of the bounding box (default: 10000).</param>
    /// <param name="lod">Level of detail for point decimation (0=full resolution, higher values reduce point density).</param>
    /// <param name="classifications">Optional array of LAS classification codes to filter results (e.g., 2=ground, 6=building).</param>
    /// <param name="limit">Maximum number of points to return (default: 100000).</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <response code="200">Point cloud data streamed as newline-delimited GeoJSON features</response>
    /// <remarks>
    /// This endpoint streams point cloud data as GeoJSON-Seq (newline-delimited JSON) for efficient transfer.
    /// Each line is a GeoJSON Feature with:
    /// - Point geometry with XYZ coordinates
    /// - Properties including classification, intensity, RGB color values
    ///
    /// LAS Classification Codes (ASPRS Standard):
    /// - 0: Never Classified / Created
    /// - 1: Unclassified
    /// - 2: Ground
    /// - 3: Low Vegetation
    /// - 4: Medium Vegetation
    /// - 5: High Vegetation
    /// - 6: Building
    /// - 7: Low Point (noise)
    /// - 9: Water
    ///
    /// Use the lod parameter to reduce point density for better performance with large datasets.
    /// </remarks>
    [HttpGet("surveys/{surveyId}/pointcloud")]
    [Produces("application/geo+json")]
    [ProducesResponseType(200)]
    public async Task QueryPointCloud(
        Guid surveyId,
        [FromQuery] double minX,
        [FromQuery] double minY,
        [FromQuery] double maxX,
        [FromQuery] double maxY,
        [FromQuery] double minZ = -10000,
        [FromQuery] double maxZ = 10000,
        [FromQuery] int lod = 0,
        [FromQuery] int[]? classifications = null,
        [FromQuery] int limit = 100000,
        CancellationToken cancellationToken = default)
    {
        var bbox = new BoundingBox3D(minX, minY, minZ, maxX, maxY, maxZ);

        var options = new PointCloudQueryOptions
        {
            BoundingBox = bbox,
            LodLevel = (PointCloudLodLevel)lod,
            ClassificationFilter = classifications,
            Limit = limit
        };

        // Stream as GeoJSON-Seq (newline-delimited JSON)
        this.Response.ContentType = "application/geo+json-seq";

        await foreach (var point in this.pointCloudService.QueryWithLodAsync(
            surveyId, options, cancellationToken))
        {
            var feature = new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { point.X, point.Y, point.Z }
                },
                properties = new
                {
                    classification = point.Classification,
                    classificationName = point.GetClassificationName(),
                    intensity = point.Intensity,
                    red = point.Red,
                    green = point.Green,
                    blue = point.Blue
                }
            };

            await this.Response.WriteAsync(
                JsonSerializer.Serialize(feature) + "\n",
                cancellationToken);
        }
    }

    /// <summary>
    /// Retrieves statistical information about a survey's point cloud data.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Point cloud statistics including point count, spatial extent, and classification distribution.</returns>
    /// <response code="200">Point cloud statistics retrieved successfully</response>
    /// <remarks>
    /// Statistics include:
    /// - Total point count
    /// - Bounding box (3D extent)
    /// - Point density
    /// - Classification histogram
    /// - Intensity and RGB value ranges
    /// </remarks>
    [HttpGet("surveys/{surveyId}/pointcloud/statistics")]
    [ProducesResponseType(typeof(PointCloudStatistics), 200)]
    public async Task<ActionResult<PointCloudStatistics>> GetPointCloudStatistics(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var stats = await this.pointCloudService.GetStatisticsAsync(surveyId, cancellationToken);
        return this.Ok(stats);
    }

    /// <summary>
    /// Imports a LAS/LAZ point cloud file into a survey.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey to import into.</param>
    /// <param name="file">The LAS or LAZ file to import (LASzip compressed format recommended for faster upload).</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Import results including point count, processing time, and any warnings or errors.</returns>
    /// <response code="200">Point cloud imported successfully</response>
    /// <response code="400">Invalid file format or corrupted data</response>
    /// <remarks>
    /// Supported formats:
    /// - LAS: ASPRS LAS format (uncompressed)
    /// - LAZ: LASzip compressed format (recommended - typically 7-20x smaller)
    ///
    /// The import process:
    /// 1. Validates file format and header
    /// 2. Extracts point data with XYZ coordinates
    /// 3. Preserves classification, intensity, and RGB values
    /// 4. Builds spatial index for efficient querying
    /// 5. Generates level-of-detail (LOD) representations
    ///
    /// Large files may take several minutes to process. Consider splitting very large datasets
    /// (>1 billion points) into multiple tiles for better performance.
    /// </remarks>
    [HttpPost("surveys/{surveyId}/pointcloud/import")]
    [ProducesResponseType(typeof(PointCloudImportResult), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<PointCloudImportResult>> ImportPointCloud(
        Guid surveyId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return this.BadRequest(new { message = "No file uploaded" });
        }

        if (!file.FileName.EndsWith(".laz", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".las", StringComparison.OrdinalIgnoreCase))
        {
            return this.BadRequest(new { message = "Only LAZ/LAS files are supported" });
        }

        try
        {
            // Save uploaded file temporarily
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".laz");
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var result = await this.pointCloudService.ImportLazFileAsync(
                surveyId, tempPath, cancellationToken);

            // Clean up temp file
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }

            return this.Ok(result);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to import point cloud");
            return this.BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Orthomosaic Endpoints

    /// <summary>
    /// Retrieves all orthomosaic imagery associated with a survey.
    /// </summary>
    /// <param name="surveyId">The unique identifier of the survey.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Collection of orthomosaic metadata including resolution, extent, and tile service URLs.</returns>
    /// <response code="200">Orthomosaic list retrieved successfully</response>
    /// <remarks>
    /// Orthomosaics are georeferenced aerial imagery products created from drone photos.
    /// Each orthomosaic includes:
    /// - Ground sampling distance (GSD/resolution)
    /// - Spatial extent and coordinate system
    /// - Tile service endpoints for web mapping
    /// - Band information (RGB, multispectral, etc.)
    /// </remarks>
    [HttpGet("surveys/{surveyId}/orthomosaics")]
    [ProducesResponseType(typeof(IEnumerable<DroneOrthomosaic>), 200)]
    public async Task<ActionResult<IEnumerable<DroneOrthomosaic>>> ListOrthomosaics(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var orthomosaics = await this.orthomosaicService.ListOrthomosaicsAsync(surveyId, cancellationToken);
        return this.Ok(orthomosaics);
    }

    /// <summary>
    /// Retrieves detailed information for a specific orthomosaic.
    /// </summary>
    /// <param name="orthomosaicId">The unique identifier of the orthomosaic.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>Complete orthomosaic metadata and service endpoints.</returns>
    /// <response code="200">Orthomosaic found and returned successfully</response>
    /// <response code="404">Orthomosaic with the specified ID not found</response>
    [HttpGet("orthomosaics/{orthomosaicId}")]
    [ProducesResponseType(typeof(DroneOrthomosaic), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DroneOrthomosaic>> GetOrthomosaic(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        var orthomosaic = await this.orthomosaicService.GetOrthomosaicAsync(orthomosaicId, cancellationToken);

        if (orthomosaic == null)
        {
            return this.NotFound(new { message = $"Orthomosaic {orthomosaicId} not found" });
        }

        return this.Ok(orthomosaic);
    }

    /// <summary>
    /// Creates a new orthomosaic record and associates it with a survey.
    /// </summary>
    /// <param name="dto">Orthomosaic creation request with metadata and tile service configuration.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>The newly created orthomosaic with assigned ID and service endpoints.</returns>
    /// <response code="201">Orthomosaic created successfully</response>
    /// <response code="400">Invalid orthomosaic configuration or missing required fields</response>
    /// <remarks>
    /// This endpoint registers an orthomosaic that has been processed externally.
    /// You must provide:
    /// - Survey ID to associate with
    /// - Spatial reference system (EPSG code)
    /// - Ground sampling distance (GSD)
    /// - Bounding box coordinates
    /// - Tile service URL or raster file path
    /// </remarks>
    [HttpPost("orthomosaics")]
    [ProducesResponseType(typeof(DroneOrthomosaic), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<DroneOrthomosaic>> CreateOrthomosaic(
        [FromBody] CreateOrthomosaicDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orthomosaic = await this.orthomosaicService.CreateOrthomosaicAsync(dto, cancellationToken);
            return this.CreatedAtAction(
                nameof(GetOrthomosaic),
                new { orthomosaicId = orthomosaic.Id },
                orthomosaic);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to create orthomosaic");
            return this.BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Retrieves WMTS (Web Map Tile Service) capabilities document for an orthomosaic.
    /// </summary>
    /// <param name="orthomosaicId">The unique identifier of the orthomosaic.</param>
    /// <param name="cancellationToken">Cancellation token for request cancellation.</param>
    /// <returns>WMTS capabilities including tile matrix sets, formats, and service endpoints.</returns>
    /// <response code="200">WMTS capabilities retrieved successfully</response>
    /// <response code="404">Orthomosaic with the specified ID not found</response>
    /// <remarks>
    /// The WMTS capabilities document describes:
    /// - Available tile matrix sets (zoom levels)
    /// - Supported image formats (PNG, JPEG, WebP)
    /// - Tile dimensions and resolutions
    /// - Bounding box for each zoom level
    ///
    /// Use this information to configure web mapping clients (OpenLayers, Leaflet, MapLibre, etc.)
    /// to display the orthomosaic as a tile layer.
    /// </remarks>
    [HttpGet("orthomosaics/{orthomosaicId}/wmts")]
    [ProducesResponseType(typeof(WmtsCapabilities), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<WmtsCapabilities>> GetWmtsCapabilities(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilities = await this.orthomosaicService.GetWmtsCapabilitiesAsync(
                orthomosaicId, cancellationToken);
            return this.Ok(capabilities);
        }
        catch (KeyNotFoundException)
        {
            return this.NotFound(new { message = $"Orthomosaic {orthomosaicId} not found" });
        }
    }

    #endregion

    #region Health Endpoints

    /// <summary>
    /// Health check endpoint to verify drone data service availability.
    /// </summary>
    /// <returns>Service health status and timestamp.</returns>
    /// <response code="200">Service is healthy and operational</response>
    /// <remarks>
    /// Use this endpoint for monitoring and load balancer health checks.
    /// </remarks>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public IActionResult HealthCheck()
    {
        return this.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "drone-data-api"
        });
    }

    #endregion
}
