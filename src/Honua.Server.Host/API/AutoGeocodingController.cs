// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Asp.Versioning;
using Honua.MapSDK.Models.Import;
using Honua.MapSDK.Services.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.API;

/// <summary>
/// REST API for automatic geocoding on data upload.
/// Provides endpoints for address detection, geocoding preview, and batch geocoding execution.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/geocoding/auto")]
[Produces("application/json")]
public class AutoGeocodingController : ControllerBase
{
    private readonly ILogger<AutoGeocodingController> _logger;
    private readonly AutoGeocodingService _autoGeocodingService;
    private readonly AddressDetectionService _addressDetectionService;

    public AutoGeocodingController(
        ILogger<AutoGeocodingController> logger,
        AutoGeocodingService autoGeocodingService,
        AddressDetectionService addressDetectionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _autoGeocodingService = autoGeocodingService ?? throw new ArgumentNullException(nameof(autoGeocodingService));
        _addressDetectionService = addressDetectionService ?? throw new ArgumentNullException(nameof(addressDetectionService));
    }

    // ==================== Address Detection ====================

    /// <summary>
    /// Detects address columns in uploaded data.
    /// Returns candidates for user confirmation with confidence scores.
    /// </summary>
    /// <param name="request">Detection request with parsed data</param>
    /// <response code="200">Address columns detected successfully</response>
    /// <response code="400">Invalid request or data format</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("detect")]
    [ProducesResponseType(typeof(AddressDetectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetectAddressColumns(
        [FromBody, Required] DetectAddressColumnsRequest request)
    {
        try
        {
            if (request.ParsedData == null || request.ParsedData.Features.Count == 0)
            {
                return BadRequest(new { error = "No data provided or data is empty" });
            }

            var response = await _autoGeocodingService.DetectAddressColumnsAsync(
                request.DatasetId,
                request.ParsedData,
                HttpContext.RequestAborted);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect address columns for dataset {DatasetId}", request.DatasetId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to detect address columns", details = ex.Message });
        }
    }

    /// <summary>
    /// Analyzes specific fields for address patterns.
    /// Useful for validating custom column selections.
    /// </summary>
    /// <param name="request">Analysis request with field data</param>
    /// <response code="200">Field analysis completed</response>
    /// <response code="400">Invalid request</response>
    [HttpPost("analyze-field")]
    [ProducesResponseType(typeof(FieldAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult AnalyzeField([FromBody, Required] AnalyzeFieldRequest request)
    {
        try
        {
            // This would use the AddressDetectionService to analyze specific fields
            // For now, return a simple response
            return Ok(new FieldAnalysisResponse
            {
                FieldName = request.FieldName,
                IsLikelyAddress = true,
                Confidence = 0.8,
                SuggestedType = AddressColumnType.FullAddress
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze field {FieldName}", request.FieldName);
            return BadRequest(new { error = "Failed to analyze field", details = ex.Message });
        }
    }

    // ==================== Geocoding Operations ====================

    /// <summary>
    /// Starts automatic geocoding operation.
    /// Processes addresses and adds geometry to features.
    /// </summary>
    /// <param name="request">Geocoding request with configuration</param>
    /// <response code="202">Geocoding started successfully</response>
    /// <response code="400">Invalid request or configuration</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(AutoGeocodingResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> StartGeocoding(
        [FromBody, Required] AutoGeocodingRequest request)
    {
        try
        {
            if (request.ParsedData == null || request.AddressConfiguration == null)
            {
                return BadRequest(new { error = "Missing required data or configuration" });
            }

            // Start geocoding (this could be made async with background processing)
            var result = await _autoGeocodingService.StartGeocodingAsync(
                request,
                progress: null, // TODO: Implement SignalR for real-time progress
                HttpContext.RequestAborted);

            return Accepted(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start geocoding for dataset {DatasetId}", request.DatasetId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to start geocoding", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the status of a geocoding session.
    /// </summary>
    /// <param name="sessionId">Geocoding session ID</param>
    /// <response code="200">Session status retrieved</response>
    /// <response code="404">Session not found</response>
    [HttpGet("session/{sessionId}")]
    [ProducesResponseType(typeof(AutoGeocodingSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSessionStatus([FromRoute, Required] string sessionId)
    {
        try
        {
            var session = _autoGeocodingService.GetSession(sessionId);

            if (session == null)
            {
                return NotFound(new { error = $"Session {sessionId} not found" });
            }

            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get session status for {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get session status", details = ex.Message });
        }
    }

    /// <summary>
    /// Retries failed geocoding operations.
    /// </summary>
    /// <param name="request">Retry request with session info</param>
    /// <response code="202">Retry started successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="404">Session not found</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("retry")]
    [ProducesResponseType(typeof(AutoGeocodingResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RetryGeocoding(
        [FromBody, Required] RetryGeocodingRequest request)
    {
        try
        {
            var result = await _autoGeocodingService.RetryFailedGeocodingAsync(
                request,
                HttpContext.RequestAborted);

            return Accepted(result);
        }
        catch (NotImplementedException)
        {
            return StatusCode(StatusCodes.Status501NotImplemented,
                new { error = "Retry functionality is not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retry geocoding for session {SessionId}", request.SessionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to retry geocoding", details = ex.Message });
        }
    }

    // ==================== Configuration & Help ====================

    /// <summary>
    /// Gets available geocoding providers and their configurations.
    /// </summary>
    /// <response code="200">Providers retrieved successfully</response>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(List<GeocodingProviderInfo>), StatusCodes.Status200OK)]
    public IActionResult GetProviders()
    {
        var providers = new List<GeocodingProviderInfo>
        {
            new GeocodingProviderInfo
            {
                Name = "nominatim",
                DisplayName = "OpenStreetMap Nominatim",
                Description = "Free geocoding service (rate limited)",
                RequiresApiKey = false,
                RateLimit = "1 request/second",
                IsDefault = true
            },
            new GeocodingProviderInfo
            {
                Name = "mapbox",
                DisplayName = "Mapbox Geocoding",
                Description = "Commercial geocoding service",
                RequiresApiKey = true,
                RateLimit = "50 requests/second",
                IsDefault = false
            },
            new GeocodingProviderInfo
            {
                Name = "google",
                DisplayName = "Google Maps Geocoding",
                Description = "Google's geocoding service",
                RequiresApiKey = true,
                RateLimit = "50 requests/second",
                IsDefault = false
            }
        };

        return Ok(providers);
    }

    /// <summary>
    /// Gets example address configurations for different scenarios.
    /// </summary>
    /// <response code="200">Examples retrieved successfully</response>
    [HttpGet("examples")]
    [ProducesResponseType(typeof(List<AddressConfigurationExample>), StatusCodes.Status200OK)]
    public IActionResult GetExamples()
    {
        var examples = new List<AddressConfigurationExample>
        {
            new AddressConfigurationExample
            {
                Name = "Single Address Column",
                Description = "One column contains complete address",
                Configuration = new AddressConfiguration
                {
                    Type = AddressConfigurationType.SingleColumn,
                    SingleColumnName = "Address",
                    Confidence = 1.0
                },
                SampleData = new[] { "123 Main St, San Francisco, CA 94102" }
            },
            new AddressConfigurationExample
            {
                Name = "Multi-Column Address",
                Description = "Address split across multiple columns",
                Configuration = new AddressConfiguration
                {
                    Type = AddressConfigurationType.MultiColumn,
                    MultiColumnNames = new List<string> { "Street", "City", "State", "Zip" },
                    Separator = ", ",
                    Confidence = 1.0
                },
                SampleData = new[] { "123 Main St", "San Francisco", "CA", "94102" }
            }
        };

        return Ok(examples);
    }
}

// ==================== Request/Response DTOs ====================

/// <summary>
/// Request to detect address columns in parsed data.
/// </summary>
public class DetectAddressColumnsRequest
{
    /// <summary>
    /// Dataset or upload session ID
    /// </summary>
    [Required]
    public required string DatasetId { get; set; }

    /// <summary>
    /// Parsed data from file upload
    /// </summary>
    [Required]
    public required ParsedData ParsedData { get; set; }
}

/// <summary>
/// Request to analyze a specific field for address patterns.
/// </summary>
public class AnalyzeFieldRequest
{
    /// <summary>
    /// Field name to analyze
    /// </summary>
    [Required]
    public required string FieldName { get; set; }

    /// <summary>
    /// Sample values from the field
    /// </summary>
    [Required]
    public required List<string> SampleValues { get; set; }
}

/// <summary>
/// Response for field analysis.
/// </summary>
public class FieldAnalysisResponse
{
    public required string FieldName { get; set; }
    public bool IsLikelyAddress { get; set; }
    public double Confidence { get; set; }
    public AddressColumnType SuggestedType { get; set; }
}

/// <summary>
/// Information about a geocoding provider.
/// </summary>
public class GeocodingProviderInfo
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public bool RequiresApiKey { get; set; }
    public string? RateLimit { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// Example address configuration with sample data.
/// </summary>
public class AddressConfigurationExample
{
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required AddressConfiguration Configuration { get; set; }
    public required string[] SampleData { get; set; }
}
