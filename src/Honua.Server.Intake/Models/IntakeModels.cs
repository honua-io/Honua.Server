// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Intake.Models;

/// <summary>
/// Response from starting a new intake conversation.
/// </summary>
public sealed class ConversationResponse
{
    /// <summary>
    /// Unique identifier for the conversation.
    /// </summary>
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>
    /// Initial AI message to the customer.
    /// </summary>
    public string InitialMessage { get; init; } = string.Empty;

    /// <summary>
    /// When the conversation was started.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Customer identifier (if authenticated).
    /// </summary>
    public string? CustomerId { get; init; }
}

/// <summary>
/// Response from processing a user message in the intake conversation.
/// </summary>
public sealed class IntakeResponse
{
    /// <summary>
    /// The conversation identifier.
    /// </summary>
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>
    /// AI assistant's response message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Indicates if the AI has gathered enough information to complete intake.
    /// </summary>
    public bool IntakeComplete { get; init; }

    /// <summary>
    /// Extracted build requirements (populated when IntakeComplete is true).
    /// </summary>
    public BuildRequirements? Requirements { get; init; }

    /// <summary>
    /// Estimated monthly cost in USD (populated when requirements are available).
    /// </summary>
    public decimal? EstimatedMonthlyCost { get; init; }

    /// <summary>
    /// Cost breakdown by component.
    /// </summary>
    public Dictionary<string, decimal>? CostBreakdown { get; init; }

    /// <summary>
    /// When the message was processed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Customer requirements extracted from the AI conversation.
/// </summary>
public sealed class BuildRequirements
{
    /// <summary>
    /// Required protocols/APIs.
    /// </summary>
    public List<string> Protocols { get; init; } = new();

    /// <summary>
    /// Required database integrations.
    /// </summary>
    public List<string> Databases { get; init; } = new();

    /// <summary>
    /// Target cloud provider (aws, azure, gcp, on-premises).
    /// </summary>
    public string CloudProvider { get; init; } = string.Empty;

    /// <summary>
    /// Target architecture (linux-arm64, linux-x64, windows-x64).
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// Expected load characteristics.
    /// </summary>
    public ExpectedLoad? Load { get; init; }

    /// <summary>
    /// License tier (Core, Pro, Enterprise, Enterprise ASP).
    /// </summary>
    public string Tier { get; init; } = string.Empty;

    /// <summary>
    /// Advanced features requested.
    /// </summary>
    public List<string>? AdvancedFeatures { get; init; }

    /// <summary>
    /// Additional notes from the conversation.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Expected load characteristics for sizing the deployment.
/// </summary>
public sealed class ExpectedLoad
{
    /// <summary>
    /// Peak concurrent users.
    /// </summary>
    public int ConcurrentUsers { get; init; }

    /// <summary>
    /// Expected requests per second.
    /// </summary>
    public double RequestsPerSecond { get; init; }

    /// <summary>
    /// Data volume in GB (if applicable).
    /// </summary>
    public double? DataVolumeGb { get; init; }

    /// <summary>
    /// Load classification (light, moderate, heavy).
    /// </summary>
    public string? Classification { get; init; }
}

/// <summary>
/// Request to trigger a build from completed intake.
/// </summary>
public sealed class TriggerBuildRequest
{
    /// <summary>
    /// The conversation identifier.
    /// </summary>
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>
    /// Customer identifier.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// Override the auto-detected requirements (optional).
    /// </summary>
    public BuildRequirements? RequirementsOverride { get; init; }

    /// <summary>
    /// Custom build name/label.
    /// </summary>
    public string? BuildName { get; init; }

    /// <summary>
    /// Additional build tags.
    /// </summary>
    public List<string>? Tags { get; init; }
}

/// <summary>
/// Response from triggering a build.
/// </summary>
public sealed class TriggerBuildResponse
{
    /// <summary>
    /// Indicates if the build was successfully triggered.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The build identifier for tracking.
    /// </summary>
    public string? BuildId { get; init; }

    /// <summary>
    /// The generated manifest that will be built.
    /// </summary>
    public BuildManifest? Manifest { get; init; }

    /// <summary>
    /// Registry provisioning result.
    /// </summary>
    public RegistryProvisioningResult? RegistryResult { get; init; }

    /// <summary>
    /// Error message if build trigger failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When the build was triggered.
    /// </summary>
    public DateTimeOffset TriggeredAt { get; init; }
}

/// <summary>
/// Build status response.
/// </summary>
public sealed class BuildStatusResponse
{
    /// <summary>
    /// The build identifier.
    /// </summary>
    public string BuildId { get; init; } = string.Empty;

    /// <summary>
    /// Current build status (pending, building, completed, failed).
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Progress { get; init; }

    /// <summary>
    /// Current build stage/step.
    /// </summary>
    public string? CurrentStage { get; init; }

    /// <summary>
    /// Completed image reference (if build succeeded).
    /// </summary>
    public string? ImageReference { get; init; }

    /// <summary>
    /// Error message (if build failed).
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Build logs URL.
    /// </summary>
    public string? LogsUrl { get; init; }

    /// <summary>
    /// When the build was started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// When the build completed or failed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// Generated build manifest from requirements.
/// </summary>
public sealed class BuildManifest
{
    /// <summary>
    /// Manifest version.
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Build name/identifier.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Target architecture.
    /// </summary>
    public string Architecture { get; init; } = string.Empty;

    /// <summary>
    /// Enabled modules/protocols.
    /// </summary>
    public List<string> Modules { get; init; } = new();

    /// <summary>
    /// Database connectors to include.
    /// </summary>
    public List<string> DatabaseConnectors { get; init; } = new();

    /// <summary>
    /// Cloud deployment targets.
    /// </summary>
    public List<CloudTarget>? CloudTargets { get; init; }

    /// <summary>
    /// Resource requirements.
    /// </summary>
    public ResourceRequirements? Resources { get; init; }

    /// <summary>
    /// Environment variables to configure.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>
    /// License tier.
    /// </summary>
    public string Tier { get; init; } = string.Empty;

    /// <summary>
    /// Build tags for metadata.
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// When the manifest was generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>
/// Cloud deployment target configuration.
/// </summary>
public sealed class CloudTarget
{
    /// <summary>
    /// Cloud provider (aws, azure, gcp).
    /// </summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Target region.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Recommended instance type.
    /// </summary>
    public string? InstanceType { get; init; }

    /// <summary>
    /// Container registry configuration.
    /// </summary>
    public string? RegistryUrl { get; init; }

    /// <summary>
    /// Deployment configuration metadata.
    /// </summary>
    public Dictionary<string, string>? Configuration { get; init; }
}

/// <summary>
/// Resource requirements for the build.
/// </summary>
public sealed class ResourceRequirements
{
    /// <summary>
    /// Minimum CPU cores.
    /// </summary>
    public double MinCpu { get; init; }

    /// <summary>
    /// Minimum memory in GB.
    /// </summary>
    public double MinMemoryGb { get; init; }

    /// <summary>
    /// Recommended CPU cores.
    /// </summary>
    public double RecommendedCpu { get; init; }

    /// <summary>
    /// Recommended memory in GB.
    /// </summary>
    public double RecommendedMemoryGb { get; init; }

    /// <summary>
    /// Storage requirements in GB.
    /// </summary>
    public double? StorageGb { get; init; }
}

/// <summary>
/// Stored conversation in the database.
/// </summary>
public sealed record ConversationRecord
{
    /// <summary>
    /// Unique conversation identifier.
    /// </summary>
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>
    /// Customer identifier (if authenticated).
    /// </summary>
    public string? CustomerId { get; init; }

    /// <summary>
    /// JSON array of conversation messages.
    /// </summary>
    public string MessagesJson { get; init; } = string.Empty;

    /// <summary>
    /// Current conversation status (active, completed, abandoned).
    /// </summary>
    public string Status { get; init; } = "active";

    /// <summary>
    /// Extracted requirements JSON (if intake is complete).
    /// </summary>
    public string? RequirementsJson { get; init; }

    /// <summary>
    /// When the conversation was started.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the conversation was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// When the conversation was completed (if applicable).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }
}

/// <summary>
/// AI conversation message.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>
    /// Message role (system, user, assistant, function).
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Message content.
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// Function call (for OpenAI function calling).
    /// </summary>
    [JsonPropertyName("function_call")]
    public FunctionCall? FunctionCall { get; init; }

    /// <summary>
    /// Function name (for function result messages).
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// OpenAI function call representation.
/// </summary>
public sealed class FunctionCall
{
    /// <summary>
    /// Function name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Function arguments as JSON string.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; init; } = string.Empty;
}
