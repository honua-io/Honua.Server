// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Telemetry;

/// <summary>
/// Telemetry service for collecting usage analytics.
/// PRIVACY-FIRST: Opt-in only, no PII, user can disable at any time.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Whether telemetry is enabled (user has opted in).
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Tracks a command execution.
    /// </summary>
    Task TrackCommandAsync(
        string commandName,
        bool success,
        TimeSpan duration,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks a plan execution.
    /// </summary>
    Task TrackPlanAsync(
        string planType,
        int stepCount,
        bool success,
        TimeSpan duration,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks an error/exception.
    /// </summary>
    Task TrackErrorAsync(
        string errorType,
        string? errorMessage = null,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks a feature usage.
    /// </summary>
    Task TrackFeatureAsync(
        string featureName,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks an LLM API call (for cost/usage monitoring).
    /// </summary>
    Task TrackLlmCallAsync(
        string provider,
        string model,
        int promptTokens,
        int completionTokens,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending telemetry (call before app exit).
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Telemetry configuration options.
/// </summary>
public sealed class TelemetryOptions
{
    /// <summary>
    /// Whether telemetry is enabled (default: false - opt-in required).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// User's consent timestamp (when they opted in).
    /// </summary>
    public DateTime? ConsentTimestamp { get; set; }

    /// <summary>
    /// Anonymous user ID (generated, not tied to real identity).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Telemetry backend (local file, Application Insights, custom endpoint).
    /// </summary>
    public TelemetryBackend Backend { get; set; } = TelemetryBackend.LocalFile;

    /// <summary>
    /// Path for local file telemetry (default: ~/.honua/telemetry/).
    /// </summary>
    public string? LocalFilePath { get; set; }

    /// <summary>
    /// Application Insights instrumentation key (if using Azure).
    /// </summary>
    public string? ApplicationInsightsKey { get; set; }

    /// <summary>
    /// Custom telemetry endpoint URL.
    /// </summary>
    public string? CustomEndpoint { get; set; }

    /// <summary>
    /// Batch size for sending telemetry (default: 10).
    /// </summary>
    public int BatchSize { get; set; } = 10;

    /// <summary>
    /// Maximum time to wait before sending batch (default: 30 seconds).
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to collect detailed error stack traces (default: false for privacy).
    /// </summary>
    public bool CollectStackTraces { get; set; } = false;
}

public enum TelemetryBackend
{
    /// <summary>
    /// Disabled - no telemetry collected.
    /// </summary>
    None,

    /// <summary>
    /// Local file storage (for offline analysis).
    /// </summary>
    LocalFile,

    /// <summary>
    /// Azure Application Insights.
    /// </summary>
    ApplicationInsights,

    /// <summary>
    /// Custom HTTP endpoint.
    /// </summary>
    CustomEndpoint
}

/// <summary>
/// Telemetry event base class.
/// </summary>
public abstract class TelemetryEvent
{
    /// <summary>
    /// When the event occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Anonymous user ID.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Session ID (tracks a single CLI session).
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// Event type discriminator.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Additional properties.
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = new();

    /// <summary>
    /// Honua version.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// OS platform (Linux, Windows, macOS).
    /// </summary>
    public string? Platform { get; init; }
}

/// <summary>
/// Command execution telemetry event.
/// </summary>
public sealed class CommandTelemetryEvent : TelemetryEvent
{
    public required string CommandName { get; init; }
    public required bool Success { get; init; }
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Plan execution telemetry event.
/// </summary>
public sealed class PlanTelemetryEvent : TelemetryEvent
{
    public required string PlanType { get; init; }
    public required int StepCount { get; init; }
    public required bool Success { get; init; }
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Error telemetry event.
/// </summary>
public sealed class ErrorTelemetryEvent : TelemetryEvent
{
    public required string ErrorType { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
}

/// <summary>
/// Feature usage telemetry event.
/// </summary>
public sealed class FeatureTelemetryEvent : TelemetryEvent
{
    public required string FeatureName { get; init; }
}

/// <summary>
/// LLM API call telemetry event (for cost tracking).
/// </summary>
public sealed class LlmTelemetryEvent : TelemetryEvent
{
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required int PromptTokens { get; init; }
    public required int CompletionTokens { get; init; }
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Estimated cost in USD (if known).
    /// </summary>
    public decimal? EstimatedCost { get; init; }
}
