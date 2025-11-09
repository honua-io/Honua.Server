// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Honua.MapSDK.Services.Terrain;
using Honua.MapSDK.Utilities;

namespace Honua.Server.Host.API;

/// <summary>
/// REST API for terrain visualization and elevation queries.
/// Provides endpoints for terrain tiles, elevation profiles, and terrain analysis.
/// </summary>
[ApiController]
[Route("api/terrain")]
[Produces("application/json")]
public class TerrainController : ControllerBase
{
    private readonly ILogger<TerrainController> _logger;
    private readonly IElevationService _elevationService;
    private readonly ITerrainTileService _terrainTileService;

    public TerrainController(
        ILogger<TerrainController> logger,
        IElevationService elevationService,
        ITerrainTileService terrainTileService)
    {
        _logger = logger;
        _elevationService = elevationService;
        _terrainTileService = terrainTileService;
    }

    /// <summary>
    /// Query elevation at a single point.
    /// </summary>
    /// <param name="lon">Longitude in degrees</param>
    /// <param name="lat">Latitude in degrees</param>
    /// <param name="source">Optional elevation data source name</param>
    /// <response code="200">Elevation value in meters</response>
    /// <response code="404">Elevation data not available for this location</response>
    [HttpGet("elevation")]
    [ProducesResponseType(typeof(ElevationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ElevationResponse>> GetElevation(
        [FromQuery] double lon,
        [FromQuery] double lat,
        [FromQuery] string? source = null)
    {
        try
        {
            var elevation = await _elevationService.QueryElevationAsync(lon, lat, source);

            if (elevation == null)
            {
                return NotFound(new { message = "Elevation data not available for this location" });
            }

            return Ok(new ElevationResponse
            {
                Longitude = lon,
                Latitude = lat,
                Elevation = elevation.Value,
                Source = source ?? "default",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying elevation at ({Lon}, {Lat})", lon, lat);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Query elevation for multiple points in a single request.
    /// </summary>
    /// <param name="request">Batch elevation request with coordinates</param>
    /// <response code="200">Array of elevation values</response>
    [HttpPost("elevation/batch")]
    [ProducesResponseType(typeof(BatchElevationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BatchElevationResponse>> GetElevationBatch(
        [FromBody] BatchElevationRequest request)
    {
        try
        {
            var elevations = await _elevationService.QueryElevationBatchAsync(
                request.Points,
                request.Source);

            return Ok(new BatchElevationResponse
            {
                Elevations = elevations,
                Count = elevations.Length,
                Source = request.Source ?? "default",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying batch elevations");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Generate an elevation profile along a path.
    /// </summary>
    /// <param name="request">Path coordinates and options</param>
    /// <response code="200">Elevation profile data</response>
    [HttpPost("profile")]
    [ProducesResponseType(typeof(ElevationProfile), StatusCodes.Status200OK)]
    public async Task<ActionResult<ElevationProfile>> GetElevationProfile(
        [FromBody] ElevationProfileRequest request)
    {
        try
        {
            var profile = await _elevationService.QueryPathElevationAsync(
                request.Coordinates,
                request.SamplePoints ?? 100,
                request.Source);

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating elevation profile");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a terrain tile in Mapbox Terrain-RGB format.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <response code="200">PNG image in Terrain-RGB format</response>
    [HttpGet("tiles/terrain-rgb/{z}/{x}/{y}.png")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTerrainRGBTile(int z, int x, int y)
    {
        try
        {
            var tileData = await _terrainTileService.GenerateTerrainRGBTileAsync(z, x, y);
            return File(tileData, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating terrain RGB tile {Z}/{X}/{Y}", z, x, y);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Get a terrain mesh tile for 3D rendering.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="maxError">Maximum mesh error in meters</param>
    /// <response code="200">Binary mesh data</response>
    [HttpGet("tiles/mesh/{z}/{x}/{y}")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(typeof(TerrainMeshTile), StatusCodes.Status200OK)]
    public async Task<ActionResult<TerrainMeshTile>> GetTerrainMeshTile(
        int z, int x, int y,
        [FromQuery] float maxError = 1.0f)
    {
        try
        {
            var tile = await _terrainTileService.GenerateTerrainMeshTileAsync(z, x, y, maxError);
            return Ok(tile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating terrain mesh tile {Z}/{X}/{Y}", z, x, y);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a hillshade tile for terrain visualization.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="azimuth">Light azimuth in degrees (0-360)</param>
    /// <param name="altitude">Light altitude in degrees (0-90)</param>
    /// <response code="200">Grayscale hillshade PNG image</response>
    [HttpGet("tiles/hillshade/{z}/{x}/{y}.png")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHillshadeTile(
        int z, int x, int y,
        [FromQuery] double azimuth = 315,
        [FromQuery] double altitude = 45)
    {
        try
        {
            var tileData = await _terrainTileService.GenerateHillshadeTileAsync(
                z, x, y, azimuth, altitude);
            return File(tileData, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating hillshade tile {Z}/{X}/{Y}", z, x, y);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Get a slope analysis tile.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <response code="200">Slope visualization PNG image</response>
    [HttpGet("tiles/slope/{z}/{x}/{y}.png")]
    [Produces("image/png")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlopeTile(int z, int x, int y)
    {
        try
        {
            var tileData = await _terrainTileService.GenerateSlopeTileAsync(z, x, y);
            return File(tileData, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating slope tile {Z}/{X}/{Y}", z, x, y);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Get metadata for a terrain tile.
    /// </summary>
    /// <param name="z">Zoom level</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <response code="200">Tile metadata including elevation statistics</response>
    [HttpGet("tiles/metadata/{z}/{x}/{y}")]
    [ProducesResponseType(typeof(TerrainTileMetadata), StatusCodes.Status200OK)]
    public async Task<ActionResult<TerrainTileMetadata>> GetTileMetadata(int z, int x, int y)
    {
        try
        {
            var metadata = await _terrainTileService.GetTileMetadataAsync(z, x, y);
            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tile metadata {Z}/{X}/{Y}", z, x, y);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }
}

#region Request/Response DTOs

/// <summary>
/// Single elevation query response.
/// </summary>
public class ElevationResponse
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public float Elevation { get; set; }
    public required string Source { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Batch elevation request.
/// </summary>
public class BatchElevationRequest
{
    /// <summary>
    /// Array of [longitude, latitude] coordinate pairs.
    /// </summary>
    public required double[][] Points { get; set; }

    /// <summary>
    /// Optional elevation data source name.
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Batch elevation response.
/// </summary>
public class BatchElevationResponse
{
    public required float?[] Elevations { get; set; }
    public int Count { get; set; }
    public required string Source { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Elevation profile request.
/// </summary>
public class ElevationProfileRequest
{
    /// <summary>
    /// Array of [longitude, latitude] coordinate pairs defining the path.
    /// </summary>
    public required double[][] Coordinates { get; set; }

    /// <summary>
    /// Number of points to sample along the path.
    /// </summary>
    public int? SamplePoints { get; set; }

    /// <summary>
    /// Optional elevation data source name.
    /// </summary>
    public string? Source { get; set; }
}

#endregion
