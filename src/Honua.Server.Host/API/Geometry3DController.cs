// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Asp.Versioning;
using Honua.Server.Core.Models.Geometry3D;
using Honua.Server.Core.Services.Geometry3D;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.API;

/// <summary>
/// API endpoints for managing complex 3D geometries (meshes, solids).
/// Supports AEC workflows with OBJ, STL, glTF file formats.
/// Part of Phase 1.2: Complex 3D Geometry Support (AEC Technical Enablers)
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "RequireEditor")]
[Route("api/v{version:apiVersion}/geometry/3d")]
[Produces("application/json")]
public class Geometry3DController : ControllerBase
{
    private readonly IGeometry3DService _geometryService;
    private readonly IMeshConverter _meshConverter;
    private readonly ILogger<Geometry3DController> _logger;

    public Geometry3DController(
        IGeometry3DService geometryService,
        IMeshConverter meshConverter,
        ILogger<Geometry3DController> logger)
    {
        _geometryService = geometryService;
        _meshConverter = meshConverter;
        _logger = logger;
    }

    /// <summary>
    /// Upload a 3D geometry file (OBJ, STL, glTF, FBX, etc.)
    /// </summary>
    /// <param name="file">3D model file</param>
    /// <param name="featureId">Optional feature ID to associate with</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload response with geometry ID and statistics</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadGeometry3DResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadGeometry(
        IFormFile file,
        [FromQuery] Guid? featureId,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var supportedExtensions = new[] { ".obj", ".stl", ".gltf", ".glb", ".fbx", ".dae", ".ply" };

        if (!supportedExtensions.Contains(extension))
        {
            return BadRequest($"Unsupported file format: {extension}. Supported formats: {string.Join(", ", supportedExtensions)}");
        }

        // Validate file size (max 100 MB for proof-of-concept)
        const long maxSizeBytes = 100 * 1024 * 1024;
        if (file.Length > maxSizeBytes)
        {
            return BadRequest($"File too large. Maximum size: {maxSizeBytes / (1024 * 1024)} MB");
        }

        var request = new UploadGeometry3DRequest
        {
            FeatureId = featureId,
            Format = extension.TrimStart('.')
        };

        using var stream = file.OpenReadStream();
        var response = await _geometryService.ImportGeometryAsync(
            stream,
            file.FileName,
            request,
            cancellationToken);

        if (!response.Success)
        {
            return BadRequest(response.ErrorMessage);
        }

        return Ok(response);
    }

    /// <summary>
    /// Get geometry metadata by ID
    /// </summary>
    /// <param name="id">Geometry ID</param>
    /// <param name="includeMesh">Whether to include full mesh data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ComplexGeometry3D), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGeometry(
        Guid id,
        [FromQuery] bool includeMesh = false,
        CancellationToken cancellationToken = default)
    {
        var geometry = await _geometryService.GetGeometryAsync(id, includeMesh, cancellationToken);

        if (geometry == null)
        {
            return NotFound($"Geometry {id} not found");
        }

        return Ok(geometry);
    }

    /// <summary>
    /// Export geometry to a specific format
    /// </summary>
    /// <param name="id">Geometry ID</param>
    /// <param name="format">Target format (obj, stl, gltf, glb, ply)</param>
    /// <param name="binary">Use binary format (for STL)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{id}/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportGeometry(
        Guid id,
        [FromQuery] string format = "obj",
        [FromQuery] bool binary = true,
        CancellationToken cancellationToken = default)
    {
        var geometry = await _geometryService.GetGeometryAsync(id, includeMeshData: false, cancellationToken);

        if (geometry == null)
        {
            return NotFound($"Geometry {id} not found");
        }

        var options = new ExportGeometry3DOptions
        {
            Format = format,
            BinaryFormat = binary
        };

        try
        {
            var stream = await _geometryService.ExportGeometryAsync(id, options, cancellationToken);

            var contentType = format.ToLowerInvariant() switch
            {
                "obj" => "model/obj",
                "stl" => "model/stl",
                "gltf" => "model/gltf+json",
                "glb" => "model/gltf-binary",
                "ply" => "application/ply",
                _ => "application/octet-stream"
            };

            var fileName = $"geometry_{id}.{format}";
            return File(stream, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export geometry {GeometryId} to format {Format}", id, format);
            return BadRequest($"Export failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a geometry
    /// </summary>
    /// <param name="id">Geometry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGeometry(
        Guid id,
        CancellationToken cancellationToken)
    {
        var geometry = await _geometryService.GetGeometryAsync(id, includeMeshData: false, cancellationToken);

        if (geometry == null)
        {
            return NotFound($"Geometry {id} not found");
        }

        await _geometryService.DeleteGeometryAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Get all geometries for a feature
    /// </summary>
    /// <param name="featureId">Feature ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("feature/{featureId}")]
    [ProducesResponseType(typeof(IEnumerable<ComplexGeometry3D>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGeometriesForFeature(
        Guid featureId,
        CancellationToken cancellationToken)
    {
        var geometries = await _geometryService.GetGeometriesForFeatureAsync(featureId, cancellationToken);
        return Ok(geometries);
    }

    /// <summary>
    /// Update geometry metadata
    /// </summary>
    /// <param name="id">Geometry ID</param>
    /// <param name="metadata">New metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPatch("{id}/metadata")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] Dictionary<string, object> metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            await _geometryService.UpdateGeometryMetadataAsync(id, metadata, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound($"Geometry {id} not found");
        }
    }

    /// <summary>
    /// Search geometries by bounding box
    /// </summary>
    /// <param name="minX">Minimum X coordinate</param>
    /// <param name="minY">Minimum Y coordinate</param>
    /// <param name="minZ">Minimum Z coordinate</param>
    /// <param name="maxX">Maximum X coordinate</param>
    /// <param name="maxY">Maximum Y coordinate</param>
    /// <param name="maxZ">Maximum Z coordinate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("search/bbox")]
    [ProducesResponseType(typeof(IEnumerable<ComplexGeometry3D>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchByBoundingBox(
        [FromQuery] double minX,
        [FromQuery] double minY,
        [FromQuery] double minZ,
        [FromQuery] double maxX,
        [FromQuery] double maxY,
        [FromQuery] double maxZ,
        CancellationToken cancellationToken)
    {
        var bbox = new BoundingBox3D(minX, minY, minZ, maxX, maxY, maxZ);
        var geometries = await _geometryService.FindGeometriesByBoundingBoxAsync(bbox, cancellationToken);
        return Ok(geometries);
    }

    /// <summary>
    /// Get mesh preview data for web rendering
    /// Returns optimized mesh data suitable for Deck.gl visualization
    /// </summary>
    /// <param name="id">Geometry ID</param>
    /// <param name="format">Preview format: 'simple' (SimpleMeshLayer) or 'gltf' (ScenegraphLayer)</param>
    /// <param name="lod">Level of detail (0-100, where 0 is highest quality)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{id}/preview")]
    [ProducesResponseType(typeof(MeshPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMeshPreview(
        Guid id,
        [FromQuery] string format = "simple",
        [FromQuery] int lod = 0,
        CancellationToken cancellationToken = default)
    {
        // Validate format
        if (format != "simple" && format != "gltf")
        {
            return BadRequest("Format must be 'simple' or 'gltf'");
        }

        // Validate LOD
        if (lod < 0 || lod > 100)
        {
            return BadRequest("Level of detail must be between 0 and 100");
        }

        // Get geometry with mesh data
        var geometry = await _geometryService.GetGeometryAsync(id, includeMeshData: true, cancellationToken);

        if (geometry == null)
        {
            return NotFound($"Geometry {id} not found");
        }

        if (geometry.Mesh == null)
        {
            return BadRequest("Geometry has no mesh data available");
        }

        try
        {
            MeshPreviewResponse response;

            if (format == "simple")
            {
                response = await _meshConverter.ToSimpleMeshAsync(
                    geometry.Mesh,
                    lod,
                    geometry.Id,
                    geometry.SourceFormat);
            }
            else
            {
                response = await _meshConverter.ToGltfJsonAsync(
                    geometry.Mesh,
                    lod,
                    geometry.Id,
                    geometry.SourceFormat);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate mesh preview for geometry {GeometryId} with format {Format}", id, format);
            return BadRequest($"Failed to generate preview: {ex.Message}");
        }
    }
}
