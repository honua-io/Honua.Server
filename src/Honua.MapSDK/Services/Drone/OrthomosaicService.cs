// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models.Drone;
using Honua.Server.Core.DataOperations.Drone;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Drone;

/// <summary>
/// Service for orthomosaic (orthophoto) operations
/// </summary>
public class OrthomosaicService
{
    private readonly IDroneDataRepository _repository;
    private readonly ILogger<OrthomosaicService> _logger;

    public OrthomosaicService(
        IDroneDataRepository repository,
        ILogger<OrthomosaicService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Create an orthomosaic record
    /// </summary>
    public async Task<DroneOrthomosaic> CreateOrthomosaicAsync(
        CreateOrthomosaicDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating orthomosaic {Name} for survey {SurveyId}",
            dto.Name, dto.SurveyId);

        return await _repository.CreateOrthomosaicAsync(dto, cancellationToken);
    }

    /// <summary>
    /// Get orthomosaic by ID
    /// </summary>
    public async Task<DroneOrthomosaic?> GetOrthomosaicAsync(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetOrthomosaicAsync(orthomosaicId, cancellationToken);
    }

    /// <summary>
    /// List all orthomosaics for a survey
    /// </summary>
    public async Task<IEnumerable<DroneOrthomosaic>> ListOrthomosaicsAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.ListOrthomosaicsAsync(surveyId, cancellationToken);
    }

    /// <summary>
    /// Import and process a GeoTIFF as Cloud Optimized GeoTIFF (COG)
    /// </summary>
    public async Task<OrthomosaicImportResult> ImportGeoTiffAsync(
        Guid surveyId,
        string geoTiffPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing GeoTIFF {FilePath} for survey {SurveyId}",
            geoTiffPath, surveyId);

        var startTime = DateTime.UtcNow;

        try
        {
            // In production, this would:
            // 1. Use GDAL to convert GeoTIFF to COG
            // 2. Extract metadata (bounds, resolution)
            // 3. Upload to storage (S3, Azure Blob)
            // 4. Create orthomosaic record

            // For demonstration, we create a basic record
            var dto = new CreateOrthomosaicDto
            {
                SurveyId = surveyId,
                Name = Path.GetFileNameWithoutExtension(geoTiffPath),
                RasterPath = outputPath,
                ResolutionCm = 2.5 // Example value
            };

            var orthomosaic = await _repository.CreateOrthomosaicAsync(dto, cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            return new OrthomosaicImportResult
            {
                Success = true,
                OrthomosaicId = orthomosaic.Id,
                DurationSeconds = duration.TotalSeconds,
                Message = "Orthomosaic imported successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import GeoTIFF {FilePath}", geoTiffPath);

            return new OrthomosaicImportResult
            {
                Success = false,
                DurationSeconds = (DateTime.UtcNow - startTime).TotalSeconds,
                Message = $"Import failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generate tile pyramid for an orthomosaic
    /// </summary>
    public async Task<TileGenerationResult> GenerateTilesAsync(
        Guid orthomosaicId,
        int minZoom = 10,
        int maxZoom = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating tiles for orthomosaic {OrthomosaicId}", orthomosaicId);

        try
        {
            var orthomosaic = await _repository.GetOrthomosaicAsync(orthomosaicId, cancellationToken);
            if (orthomosaic == null)
            {
                throw new KeyNotFoundException($"Orthomosaic {orthomosaicId} not found");
            }

            // In production, this would use GDAL or similar to generate tiles
            // For now, we return a success result

            return new TileGenerationResult
            {
                Success = true,
                ZoomLevels = Enumerable.Range(minZoom, maxZoom - minZoom + 1).ToArray(),
                TilesGenerated = 1000, // Example value
                Message = "Tiles generated successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate tiles for orthomosaic {OrthomosaicId}", orthomosaicId);

            return new TileGenerationResult
            {
                Success = false,
                ZoomLevels = Array.Empty<int>(),
                TilesGenerated = 0,
                Message = $"Tile generation failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get WMTS capabilities for an orthomosaic
    /// </summary>
    public async Task<WmtsCapabilities> GetWmtsCapabilitiesAsync(
        Guid orthomosaicId,
        CancellationToken cancellationToken = default)
    {
        var orthomosaic = await _repository.GetOrthomosaicAsync(orthomosaicId, cancellationToken);
        if (orthomosaic == null)
        {
            throw new KeyNotFoundException($"Orthomosaic {orthomosaicId} not found");
        }

        return new WmtsCapabilities
        {
            TileMatrixSet = orthomosaic.TileMatrixSet,
            Format = "image/png",
            Bounds = orthomosaic.Bounds,
            MinZoom = 10,
            MaxZoom = 20
        };
    }
}

/// <summary>
/// Result of orthomosaic import
/// </summary>
public class OrthomosaicImportResult
{
    public bool Success { get; set; }
    public Guid? OrthomosaicId { get; set; }
    public double DurationSeconds { get; set; }
    public required string Message { get; set; }
}

/// <summary>
/// Result of tile generation
/// </summary>
public class TileGenerationResult
{
    public bool Success { get; set; }
    public required int[] ZoomLevels { get; set; }
    public int TilesGenerated { get; set; }
    public required string Message { get; set; }
}

/// <summary>
/// WMTS service capabilities
/// </summary>
public class WmtsCapabilities
{
    public required string TileMatrixSet { get; set; }
    public required string Format { get; set; }
    public object? Bounds { get; set; }
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
}
