// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Models.Drone;
using Honua.Server.Core.DataOperations.Drone;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Drone;

/// <summary>
/// High-level service for managing drone survey data
/// </summary>
public class DroneDataService
{
    private readonly IDroneDataRepository _repository;
    private readonly ILogger<DroneDataService> _logger;

    public DroneDataService(
        IDroneDataRepository repository,
        ILogger<DroneDataService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Create a new drone survey
    /// </summary>
    public async Task<DroneSurvey> CreateSurveyAsync(
        CreateDroneSurveyDto dto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating new drone survey: {Name}", dto.Name);

        try
        {
            var survey = await _repository.CreateSurveyAsync(dto, cancellationToken);
            _logger.LogInformation("Created survey {SurveyId} successfully", survey.Id);
            return survey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create survey {Name}", dto.Name);
            throw;
        }
    }

    /// <summary>
    /// Get survey by ID
    /// </summary>
    public async Task<DroneSurvey?> GetSurveyAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetSurveyAsync(surveyId, cancellationToken);
    }

    /// <summary>
    /// List all surveys with pagination
    /// </summary>
    public async Task<IEnumerable<DroneSurveySummary>> ListSurveysAsync(
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        return await _repository.ListSurveysAsync(limit, offset, cancellationToken);
    }

    /// <summary>
    /// Delete a survey and all associated data
    /// </summary>
    public async Task<bool> DeleteSurveyAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting survey {SurveyId}", surveyId);

        try
        {
            var deleted = await _repository.DeleteSurveyAsync(surveyId, cancellationToken);
            if (deleted)
            {
                _logger.LogInformation("Survey {SurveyId} deleted successfully", surveyId);
            }
            else
            {
                _logger.LogWarning("Survey {SurveyId} not found", surveyId);
            }
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete survey {SurveyId}", surveyId);
            throw;
        }
    }

    /// <summary>
    /// Get statistics about a survey
    /// </summary>
    public async Task<DroneSurveyStatistics> GetSurveyStatisticsAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default)
    {
        var survey = await _repository.GetSurveyAsync(surveyId, cancellationToken);
        if (survey == null)
        {
            throw new KeyNotFoundException($"Survey {surveyId} not found");
        }

        var pcStats = await _repository.GetPointCloudStatisticsAsync(surveyId, cancellationToken);
        var orthomosaics = await _repository.ListOrthomosaicsAsync(surveyId, cancellationToken);
        var models = await _repository.List3DModelsAsync(surveyId, cancellationToken);

        return new DroneSurveyStatistics
        {
            Survey = survey,
            PointCloudStats = pcStats,
            OrthomosaicCount = orthomosaics.Count(),
            Model3DCount = models.Count()
        };
    }

    /// <summary>
    /// Import complete survey data
    /// </summary>
    public async Task<Guid> ImportSurveyAsync(
        ImportDroneSurveyRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Importing survey: {Name}", request.Name);

        // Create the survey record
        var surveyDto = new CreateDroneSurveyDto
        {
            Name = request.Name,
            Description = request.Description,
            SurveyDate = request.SurveyDate,
            FlightAltitudeM = request.FlightAltitudeM,
            GroundResolutionCm = request.GroundResolutionCm,
            CoverageArea = request.CoverageArea,
            Metadata = request.Metadata
        };

        var survey = await _repository.CreateSurveyAsync(surveyDto, cancellationToken);

        // Note: Actual file import (LAZ, COG) would be handled by specialized services
        // This is demonstrated in PointCloudService and OrthomosaicService

        _logger.LogInformation("Survey {SurveyId} imported successfully", survey.Id);
        return survey.Id;
    }
}

/// <summary>
/// Request for importing a complete drone survey
/// </summary>
public class ImportDroneSurveyRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime SurveyDate { get; set; }
    public double? FlightAltitudeM { get; set; }
    public double? GroundResolutionCm { get; set; }
    public object? CoverageArea { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    // File paths for import
    public string? LazFilePath { get; set; }
    public string? OrthophotoPath { get; set; }
    public string? DemPath { get; set; }
    public string? ModelPath { get; set; }
}

/// <summary>
/// Complete statistics for a drone survey
/// </summary>
public class DroneSurveyStatistics
{
    public required DroneSurvey Survey { get; set; }
    public required PointCloudStatistics PointCloudStats { get; set; }
    public int OrthomosaicCount { get; set; }
    public int Model3DCount { get; set; }
}
