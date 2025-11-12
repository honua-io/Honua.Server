// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
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
[Route("api/drone")]
[Produces("application/json")]
public class DroneDataController : ControllerBase
{
    private readonly DroneDataService _droneDataService;
    private readonly PointCloudService _pointCloudService;
    private readonly OrthomosaicService _orthomosaicService;
    private readonly ILogger<DroneDataController> _logger;

    public DroneDataController(
        DroneDataService droneDataService,
        PointCloudService pointCloudService,
        OrthomosaicService orthomosaicService,
        ILogger<DroneDataController> logger)
    {
        _droneDataService = droneDataService;
        _pointCloudService = pointCloudService;
        _orthomosaicService = orthomosaicService;
        _logger = logger;
    }

    #region Survey Endpoints

    /// <summary>
    /// List all drone surveys
    /// </summary>
    [HttpGet("surveys")]
    [ProducesResponseType(typeof(IEnumerable<DroneSurveySummary>), 200)]
    public async Task<ActionResult<IEnumerable<DroneSurveySummary>>> ListSurveys(
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        var surveys = await _droneDataService.ListSurveysAsync(limit, offset, cancellationToken);
        return Ok(surveys);
    }

    /// <summary>
    /// Get survey by ID
    /// </summary>
    [HttpGet("surveys/{surveyId}")]
    [ProducesResponseType(typeof(DroneSurvey), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DroneSurvey>> GetSurvey(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _droneDataService.GetSurveyAsync(surveyId, cancellationToken);

        if (survey == null)
        {
            return NotFound(new { message = $"Survey {surveyId} not found" });
        }

        return Ok(survey);
    }

    /// <summary>
    /// Create a new drone survey
    /// </summary>
    [HttpPost("surveys")]
    [ProducesResponseType(typeof(DroneSurvey), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<DroneSurvey>> CreateSurvey(
        [FromBody] CreateDroneSurveyDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var survey = await _droneDataService.CreateSurveyAsync(dto, cancellationToken);
            return CreatedAtAction(
                nameof(GetSurvey),
                new { surveyId = survey.Id },
                survey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create survey");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a survey
    /// </summary>
    [HttpDelete("surveys/{surveyId}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteSurvey(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _droneDataService.DeleteSurveyAsync(surveyId, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { message = $"Survey {surveyId} not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Get survey statistics
    /// </summary>
    [HttpGet("surveys/{surveyId}/statistics")]
    [ProducesResponseType(typeof(DroneSurveyStatistics), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DroneSurveyStatistics>> GetSurveyStatistics(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _droneDataService.GetSurveyStatisticsAsync(surveyId, cancellationToken);
            return Ok(stats);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Survey {surveyId} not found" });
        }
    }

    #endregion

    #region Point Cloud Endpoints

    /// <summary>
    /// Query point cloud data
    /// </summary>
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
        Response.ContentType = "application/geo+json-seq";

        await foreach (var point in _pointCloudService.QueryWithLodAsync(
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

            await Response.WriteAsync(
                JsonSerializer.Serialize(feature) + "\n",
                cancellationToken);
        }
    }

    /// <summary>
    /// Get point cloud statistics
    /// </summary>
    [HttpGet("surveys/{surveyId}/pointcloud/statistics")]
    [ProducesResponseType(typeof(PointCloudStatistics), 200)]
    public async Task<ActionResult<PointCloudStatistics>> GetPointCloudStatistics(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var stats = await _pointCloudService.GetStatisticsAsync(surveyId, cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// Import LAZ file for a survey
    /// </summary>
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
            return BadRequest(new { message = "No file uploaded" });
        }

        if (!file.FileName.EndsWith(".laz", StringComparison.OrdinalIgnoreCase) &&
            !file.FileName.EndsWith(".las", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only LAZ/LAS files are supported" });
        }

        try
        {
            // Save uploaded file temporarily
            var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".laz");
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var result = await _pointCloudService.ImportLazFileAsync(
                surveyId, tempPath, cancellationToken);

            // Clean up temp file
            if (System.IO.File.Exists(tempPath))
            {
                System.IO.File.Delete(tempPath);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import point cloud");
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Orthomosaic Endpoints

    /// <summary>
    /// List orthomosaics for a survey
    /// </summary>
    [HttpGet("surveys/{surveyId}/orthomosaics")]
    [ProducesResponseType(typeof(IEnumerable<DroneOrthomosaic>), 200)]
    public async Task<ActionResult<IEnumerable<DroneOrthomosaic>>> ListOrthomosaics(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var orthomosaics = await _orthomosaicService.ListOrthomosaicsAsync(surveyId, cancellationToken);
        return Ok(orthomosaics);
    }

    /// <summary>
    /// Get orthomosaic by ID
    /// </summary>
    [HttpGet("orthomosaics/{orthomosaicId}")]
    [ProducesResponseType(typeof(DroneOrthomosaic), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<DroneOrthomosaic>> GetOrthomosaic(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        var orthomosaic = await _orthomosaicService.GetOrthomosaicAsync(orthomosaicId, cancellationToken);

        if (orthomosaic == null)
        {
            return NotFound(new { message = $"Orthomosaic {orthomosaicId} not found" });
        }

        return Ok(orthomosaic);
    }

    /// <summary>
    /// Create orthomosaic record
    /// </summary>
    [HttpPost("orthomosaics")]
    [ProducesResponseType(typeof(DroneOrthomosaic), 201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<DroneOrthomosaic>> CreateOrthomosaic(
        [FromBody] CreateOrthomosaicDto dto,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orthomosaic = await _orthomosaicService.CreateOrthomosaicAsync(dto, cancellationToken);
            return CreatedAtAction(
                nameof(GetOrthomosaic),
                new { orthomosaicId = orthomosaic.Id },
                orthomosaic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create orthomosaic");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get WMTS capabilities for an orthomosaic
    /// </summary>
    [HttpGet("orthomosaics/{orthomosaicId}/wmts")]
    [ProducesResponseType(typeof(WmtsCapabilities), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<WmtsCapabilities>> GetWmtsCapabilities(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var capabilities = await _orthomosaicService.GetWmtsCapabilitiesAsync(
                orthomosaicId, cancellationToken);
            return Ok(capabilities);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Orthomosaic {orthomosaicId} not found" });
        }
    }

    #endregion

    #region Health Endpoints

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(200)]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "drone-data-api"
        });
    }

    #endregion
}
