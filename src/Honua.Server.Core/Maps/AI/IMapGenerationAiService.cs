// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models;

namespace Honua.Server.Core.Maps.AI;

/// <summary>
/// AI service for generating map configurations from natural language
/// </summary>
public interface IMapGenerationAiService
{
    /// <summary>
    /// Generates a map configuration from a natural language prompt
    /// </summary>
    /// <param name="prompt">Natural language description of desired map (e.g., "Show me all schools within 2 miles of industrial zones")</param>
    /// <param name="userId">User ID creating the map</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated map configuration</returns>
    Task<MapGenerationResult> GenerateMapAsync(
        string prompt,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explains a map configuration in natural language
    /// </summary>
    /// <param name="mapConfiguration">Map configuration to explain</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Natural language explanation of the map</returns>
    Task<MapExplanationResult> ExplainMapAsync(
        MapConfiguration mapConfiguration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggests improvements or enhancements to an existing map configuration
    /// </summary>
    /// <param name="mapConfiguration">Current map configuration</param>
    /// <param name="userFeedback">Optional user feedback on what to improve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Suggestions for map improvements</returns>
    Task<MapSuggestionResult> SuggestImprovementsAsync(
        MapConfiguration mapConfiguration,
        string? userFeedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if the AI service is properly configured and available
    /// </summary>
    /// <returns>True if service is available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of map generation
/// </summary>
public class MapGenerationResult
{
    /// <summary>
    /// Generated map configuration
    /// </summary>
    public MapConfiguration? MapConfiguration { get; set; }

    /// <summary>
    /// Whether generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Explanation of what the map shows
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Warnings or suggestions about the generated map
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Spatial queries or operations that will be performed
    /// </summary>
    public List<string> SpatialOperations { get; set; } = new();

    public static MapGenerationResult Succeed(
        MapConfiguration mapConfiguration,
        string explanation,
        double confidence = 1.0,
        List<string>? spatialOperations = null)
    {
        return new MapGenerationResult
        {
            Success = true,
            MapConfiguration = mapConfiguration,
            Explanation = explanation,
            Confidence = confidence,
            SpatialOperations = spatialOperations ?? new List<string>()
        };
    }

    public static MapGenerationResult Fail(string errorMessage)
    {
        return new MapGenerationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Result of map explanation
/// </summary>
public class MapExplanationResult
{
    /// <summary>
    /// Natural language explanation of the map
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// Key features of the map (layers, data sources, etc.)
    /// </summary>
    public List<string> KeyFeatures { get; set; } = new();

    /// <summary>
    /// Whether explanation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if explanation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static MapExplanationResult Succeed(string explanation, List<string> keyFeatures)
    {
        return new MapExplanationResult
        {
            Success = true,
            Explanation = explanation,
            KeyFeatures = keyFeatures
        };
    }

    public static MapExplanationResult Fail(string errorMessage)
    {
        return new MapExplanationResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// Result of map suggestions
/// </summary>
public class MapSuggestionResult
{
    /// <summary>
    /// List of suggested improvements
    /// </summary>
    public List<MapSuggestion> Suggestions { get; set; } = new();

    /// <summary>
    /// Whether suggestion generation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if suggestion generation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static MapSuggestionResult Succeed(List<MapSuggestion> suggestions)
    {
        return new MapSuggestionResult
        {
            Success = true,
            Suggestions = suggestions
        };
    }

    public static MapSuggestionResult Fail(string errorMessage)
    {
        return new MapSuggestionResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

/// <summary>
/// A single map improvement suggestion
/// </summary>
public class MapSuggestion
{
    /// <summary>
    /// Type of suggestion (e.g., "layer", "style", "filter", "performance")
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Human-readable description of the suggestion
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Priority level (1-5, where 5 is highest)
    /// </summary>
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Optional code or configuration snippet showing how to implement the suggestion
    /// </summary>
    public string? Implementation { get; set; }
}
