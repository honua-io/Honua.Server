// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Honua.MapSDK.Models;
using Honua.Server.Core.Maps.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.API;

/// <summary>
/// AI-powered map generation API endpoints
/// </summary>
/// <remarks>
/// TODO: Re-enable when IMapGenerationAiService implementation is available.
/// The types exist in Core but the service implementation may not be registered.
/// </remarks>
/*
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/maps/ai")]
[Produces("application/json")]
[Tags("AI Map Generation")]
public class MapsAiController : ControllerBase
{
    private readonly IMapGenerationAiService mapAiService;
    private readonly ILogger<MapsAiController> logger;

    public MapsAiController(
        IMapGenerationAiService mapAiService,
        ILogger<MapsAiController> logger)
    {
        this.mapAiService = mapAiService ?? throw new ArgumentNullException(nameof(mapAiService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generate a map configuration from natural language prompt
    /// </summary>
    /// <param name="request">Map generation request with natural language prompt</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated map configuration</returns>
    /// <remarks>
    /// Generate interactive maps from natural language descriptions.
    ///
    /// Example prompts:
    /// - "Show me all schools in San Francisco"
    /// - "Show me all schools within 2 miles of industrial zones"
    /// - "Create a heatmap of traffic accidents in the last 6 months"
    /// - "Show downtown buildings in 3D"
    /// - "Map all fire stations and hospitals with a 5-mile service area"
    ///
    /// The AI will generate a complete map configuration including:
    /// - Layers with appropriate styling
    /// - Map controls (navigation, search, legend, etc.)
    /// - Filters for interactive exploration
    /// - Spatial analysis operations (when applicable)
    /// </remarks>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(MapGenerationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MapGenerationResponse>> GenerateMapAsync(
        [FromBody, Required] MapGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = "Prompt cannot be empty"
            });
        }

        try
        {
            // Check if AI service is available
            var isAvailable = await this.mapAiService.IsAvailableAsync(cancellationToken);
            if (!isAvailable)
            {
                return this.StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "AI Service Unavailable",
                    Detail = "The AI map generation service is not currently available. Please check configuration."
                });
            }

            var userId = this.User.Identity?.Name ?? "anonymous";

            this.logger.LogInformation("Generating map for user {UserId} with prompt: {Prompt}", userId, request.Prompt);

            var result = await this.mapAiService.GenerateMapAsync(
                request.Prompt,
                userId,
                cancellationToken);

            if (!result.Success || result.MapConfiguration == null)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Map Generation Failed",
                    Detail = result.ErrorMessage ?? "Failed to generate map"
                });
            }

            this.logger.LogInformation(
                "Successfully generated map with {LayerCount} layers for user {UserId}",
                result.MapConfiguration.Layers.Count,
                userId);

            return this.Ok(new MapGenerationResponse
            {
                MapConfiguration = result.MapConfiguration,
                Explanation = result.Explanation ?? "",
                Confidence = result.Confidence,
                Warnings = result.Warnings,
                SpatialOperations = result.SpatialOperations
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error generating map from prompt: {Prompt}", request.Prompt);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while generating the map"
            });
        }
    }

    /// <summary>
    /// Explain an existing map configuration in natural language
    /// </summary>
    /// <param name="request">Map configuration to explain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Natural language explanation of the map</returns>
    [HttpPost("explain")]
    [ProducesResponseType(typeof(MapExplanationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MapExplanationResponse>> ExplainMapAsync(
        [FromBody, Required] MapExplanationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MapConfiguration == null)
        {
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = "Map configuration cannot be null"
            });
        }

        try
        {
            var result = await this.mapAiService.ExplainMapAsync(
                request.MapConfiguration,
                cancellationToken);

            if (!result.Success)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Explanation Failed",
                    Detail = result.ErrorMessage ?? "Failed to explain map"
                });
            }

            return this.Ok(new MapExplanationResponse
            {
                Explanation = result.Explanation,
                KeyFeatures = result.KeyFeatures
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error explaining map: {MapId}", request.MapConfiguration.Id);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while explaining the map"
            });
        }
    }

    /// <summary>
    /// Get AI-powered suggestions for improving a map configuration
    /// </summary>
    /// <param name="request">Map configuration and optional user feedback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of improvement suggestions</returns>
    [HttpPost("suggest")]
    [ProducesResponseType(typeof(MapSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MapSuggestionResponse>> SuggestImprovementsAsync(
        [FromBody, Required] MapSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MapConfiguration == null)
        {
            return this.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Request",
                Detail = "Map configuration cannot be null"
            });
        }

        try
        {
            var result = await this.mapAiService.SuggestImprovementsAsync(
                request.MapConfiguration,
                request.UserFeedback,
                cancellationToken);

            if (!result.Success)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Suggestion Generation Failed",
                    Detail = result.ErrorMessage ?? "Failed to generate suggestions"
                });
            }

            return this.Ok(new MapSuggestionResponse
            {
                Suggestions = result.Suggestions
            });
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error generating suggestions for map: {MapId}", request.MapConfiguration.Id);
            return this.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while generating suggestions"
            });
        }
    }

    /// <summary>
    /// Get example prompts to help users get started
    /// </summary>
    /// <returns>List of example prompts</returns>
    [HttpGet("examples")]
    [ProducesResponseType(typeof(ExamplePromptsResponse), StatusCodes.Status200OK)]
    public ActionResult<ExamplePromptsResponse> GetExamplePrompts()
    {
        return this.Ok(new ExamplePromptsResponse
        {
            Examples = MapGenerationPromptTemplates.GetExamplePrompts()
        });
    }

    /// <summary>
    /// Check if the AI map generation service is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service availability status</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(ServiceHealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServiceHealthResponse>> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var isAvailable = await this.mapAiService.IsAvailableAsync(cancellationToken);

        return this.Ok(new ServiceHealthResponse
        {
            Available = isAvailable,
            Message = isAvailable
                ? "AI map generation service is available"
                : "AI map generation service is not available. Please check configuration."
        });
    }
}

// DTOs for API requests and responses

/// <summary>
/// Request to generate a map from natural language
/// </summary>
public class MapGenerationRequest
{
    /// <summary>
    /// Natural language description of the desired map
    /// </summary>
    /// <example>Show me all schools within 2 miles of industrial zones</example>
    [Required]
    public string Prompt { get; set; } = string.Empty;
}

/// <summary>
/// Response containing generated map configuration
/// </summary>
public class MapGenerationResponse
{
    /// <summary>
    /// Generated map configuration
    /// </summary>
    public required MapConfiguration MapConfiguration { get; set; }

    /// <summary>
    /// Explanation of what the map shows
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Warnings or suggestions about the generated map
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Spatial operations that will be performed
    /// </summary>
    public List<string> SpatialOperations { get; set; } = new();
}

/// <summary>
/// Request to explain a map configuration
/// </summary>
public class MapExplanationRequest
{
    /// <summary>
    /// Map configuration to explain
    /// </summary>
    [Required]
    public required MapConfiguration MapConfiguration { get; set; }
}

/// <summary>
/// Response containing map explanation
/// </summary>
public class MapExplanationResponse
{
    /// <summary>
    /// Natural language explanation
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Key features of the map
    /// </summary>
    public List<string> KeyFeatures { get; set; } = new();
}

/// <summary>
/// Request to get improvement suggestions
/// </summary>
public class MapSuggestionRequest
{
    /// <summary>
    /// Map configuration to improve
    /// </summary>
    [Required]
    public required MapConfiguration MapConfiguration { get; set; }

    /// <summary>
    /// Optional user feedback on what to improve
    /// </summary>
    public string? UserFeedback { get; set; }
}

/// <summary>
/// Response containing improvement suggestions
/// </summary>
public class MapSuggestionResponse
{
    /// <summary>
    /// List of suggestions
    /// </summary>
    public List<MapSuggestion> Suggestions { get; set; } = new();
}

/// <summary>
/// Response with example prompts
/// </summary>
public class ExamplePromptsResponse
{
    /// <summary>
    /// List of example prompts
    /// </summary>
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// Service health status response
/// </summary>
public class ServiceHealthResponse
{
    /// <summary>
    /// Whether the service is available
    /// </summary>
    public bool Available { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
*/
