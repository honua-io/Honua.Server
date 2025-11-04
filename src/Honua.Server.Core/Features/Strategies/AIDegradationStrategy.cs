// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features.Strategies;

/// <summary>
/// Adaptive AI service that gracefully handles AI feature unavailability.
/// </summary>
public sealed class AdaptiveAIService
{
    private readonly AdaptiveFeatureService _adaptiveFeature;
    private readonly ILogger<AdaptiveAIService> _logger;

    public AdaptiveAIService(
        AdaptiveFeatureService adaptiveFeature,
        ILogger<AdaptiveAIService> logger)
    {
        _adaptiveFeature = adaptiveFeature ?? throw new ArgumentNullException(nameof(adaptiveFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an AI operation with fallback handling.
    /// </summary>
    public async Task<AIResponse> ExecuteAsync(
        string prompt,
        Func<string, CancellationToken, Task<string>> aiFunc,
        Func<string, CancellationToken, Task<string>>? fallbackFunc = null,
        CancellationToken cancellationToken = default)
    {
        var isAvailable = await _adaptiveFeature.IsAIAvailableAsync(cancellationToken);

        if (!isAvailable)
        {
            _logger.LogDebug("AI features unavailable");

            if (fallbackFunc != null)
            {
                try
                {
                    var fallbackResult = await fallbackFunc(prompt, cancellationToken);
                    return new AIResponse
                    {
                        Success = true,
                        Response = fallbackResult,
                        Mode = AIMode.Fallback,
                        Warning = "AI features are currently unavailable. Using fallback processing."
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AI fallback processing failed for prompt: OperationName={OperationName}", "ExecuteAsync");
                }
            }

            return new AIResponse
            {
                Success = false,
                Response = string.Empty,
                Mode = AIMode.Unavailable,
                Error = "AI features are currently unavailable. Please try again later or configure your deployment manually.",
                SuggestedActions = new[]
                {
                    "Check the documentation at /docs",
                    "Use the manual configuration wizard",
                    "Contact support if the issue persists"
                }
            };
        }

        // AI is available, execute normally
        try
        {
            var result = await aiFunc(prompt, cancellationToken);
            return new AIResponse
            {
                Success = true,
                Response = result,
                Mode = AIMode.Normal
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI processing failed: OperationName={OperationName}", "ExecuteAsync");

            // Try fallback if available
            if (fallbackFunc != null)
            {
                try
                {
                    var fallbackResult = await fallbackFunc(prompt, cancellationToken);
                    return new AIResponse
                    {
                        Success = true,
                        Response = fallbackResult,
                        Mode = AIMode.Fallback,
                        Warning = "AI processing encountered an error. Using fallback processing."
                    };
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "AI fallback processing also failed after primary failure: OperationName={OperationName}", "ExecuteAsync");
                }
            }

            return new AIResponse
            {
                Success = false,
                Response = string.Empty,
                Mode = AIMode.Error,
                Error = $"AI processing failed: {ex.Message}",
                SuggestedActions = new[]
                {
                    "Retry the operation",
                    "Use manual configuration",
                    "Check system logs for details"
                }
            };
        }
    }

    /// <summary>
    /// Checks if AI-assisted deployment is available.
    /// </summary>
    public async Task<bool> CanAssistWithDeploymentAsync(CancellationToken cancellationToken = default)
    {
        return await _adaptiveFeature.IsAIAvailableAsync(cancellationToken);
    }

    /// <summary>
    /// Checks if AI-assisted recommendations are available.
    /// </summary>
    public async Task<bool> CanProvideRecommendationsAsync(CancellationToken cancellationToken = default)
    {
        return await _adaptiveFeature.IsAIAvailableAsync(cancellationToken);
    }
}

/// <summary>
/// AI operation response with degradation information.
/// </summary>
public sealed class AIResponse
{
    public required bool Success { get; init; }
    public required string Response { get; init; }
    public required AIMode Mode { get; init; }
    public string? Warning { get; init; }
    public string? Error { get; init; }
    public string[]? SuggestedActions { get; init; }
}

/// <summary>
/// AI operation mode.
/// </summary>
public enum AIMode
{
    /// <summary>
    /// Normal AI processing.
    /// </summary>
    Normal,

    /// <summary>
    /// Using fallback processing.
    /// </summary>
    Fallback,

    /// <summary>
    /// AI unavailable.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Error during processing.
    /// </summary>
    Error
}
