// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Server.Enterprise.IoT.Azure.Models;

/// <summary>
/// Represents a Digital Twins Definition Language (DTDL) model.
/// </summary>
public sealed class DtdlModel
{
    /// <summary>
    /// The globally unique identifier for the model (DTMI format).
    /// </summary>
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    /// <summary>
    /// The DTDL version (currently "dtmi:dtdl:context;2" or "dtmi:dtdl:context;3").
    /// </summary>
    [JsonPropertyName("@context")]
    public string Context { get; set; } = "dtmi:dtdl:context;3";

    /// <summary>
    /// The type of the model (Interface, Schema, etc.).
    /// </summary>
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "Interface";

    /// <summary>
    /// Display name for the model.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description of the model.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Model contents (properties, telemetry, commands, relationships).
    /// </summary>
    [JsonPropertyName("contents")]
    public List<DtdlContent> Contents { get; set; } = new();

    /// <summary>
    /// Models that this model extends.
    /// </summary>
    [JsonPropertyName("extends")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Extends { get; set; }

    /// <summary>
    /// Model metadata.
    /// </summary>
    [JsonPropertyName("comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }
}

/// <summary>
/// Base class for DTDL model contents.
/// </summary>
public abstract class DtdlContent
{
    /// <summary>
    /// The type of content (Property, Telemetry, Command, Relationship, Component).
    /// </summary>
    [JsonPropertyName("@type")]
    public required string Type { get; set; }

    /// <summary>
    /// The name of the content.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }
}

/// <summary>
/// Represents a DTDL property.
/// </summary>
public sealed class DtdlProperty : DtdlContent
{
    /// <summary>
    /// Data schema for the property.
    /// </summary>
    [JsonPropertyName("schema")]
    public required object Schema { get; set; }

    /// <summary>
    /// Whether the property is writable.
    /// </summary>
    [JsonPropertyName("writable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Writable { get; set; }
}

/// <summary>
/// Represents a DTDL telemetry field.
/// </summary>
public sealed class DtdlTelemetry : DtdlContent
{
    /// <summary>
    /// Data schema for the telemetry.
    /// </summary>
    [JsonPropertyName("schema")]
    public required object Schema { get; set; }
}

/// <summary>
/// Represents a DTDL relationship.
/// </summary>
public sealed class DtdlRelationship : DtdlContent
{
    /// <summary>
    /// Target model ID (optional, can be any twin if not specified).
    /// </summary>
    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Target { get; set; }

    /// <summary>
    /// Minimum multiplicity.
    /// </summary>
    [JsonPropertyName("minMultiplicity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MinMultiplicity { get; set; }

    /// <summary>
    /// Maximum multiplicity.
    /// </summary>
    [JsonPropertyName("maxMultiplicity")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxMultiplicity { get; set; }

    /// <summary>
    /// Relationship properties.
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DtdlProperty>? Properties { get; set; }
}

/// <summary>
/// Represents a DTDL component.
/// </summary>
public sealed class DtdlComponent : DtdlContent
{
    /// <summary>
    /// Schema (model ID) for the component.
    /// </summary>
    [JsonPropertyName("schema")]
    public required string Schema { get; set; }
}

/// <summary>
/// DTDL primitive schema types.
/// </summary>
public static class DtdlSchemaType
{
    public const string Boolean = "boolean";
    public const string Date = "date";
    public const string DateTime = "dateTime";
    public const string Double = "double";
    public const string Duration = "duration";
    public const string Float = "float";
    public const string Integer = "integer";
    public const string Long = "long";
    public const string String = "string";
    public const string Time = "time";

    // Geospatial types (DTDL v3)
    public const string Point = "point";
    public const string LineString = "lineString";
    public const string Polygon = "polygon";
    public const string MultiPoint = "multiPoint";
    public const string MultiLineString = "multiLineString";
    public const string MultiPolygon = "multiPolygon";
}

/// <summary>
/// Twin synchronization metadata.
/// </summary>
public sealed class TwinSyncMetadata
{
    /// <summary>
    /// Source system identifier.
    /// </summary>
    public string Source { get; set; } = "Honua";

    /// <summary>
    /// Last synchronized timestamp.
    /// </summary>
    public DateTimeOffset LastSyncTime { get; set; }

    /// <summary>
    /// Honua service ID.
    /// </summary>
    public string? ServiceId { get; set; }

    /// <summary>
    /// Honua layer ID.
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Honua feature ID.
    /// </summary>
    public string? FeatureId { get; set; }

    /// <summary>
    /// ETag for optimistic concurrency.
    /// </summary>
    public string? ETag { get; set; }

    /// <summary>
    /// Version number for conflict resolution.
    /// </summary>
    public long Version { get; set; }

    /// <summary>
    /// Last modified timestamp from source system.
    /// </summary>
    public DateTimeOffset? SourceLastModified { get; set; }
}

/// <summary>
/// Twin sync operation result.
/// </summary>
public sealed class TwinSyncResult
{
    /// <summary>
    /// Whether the sync was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Twin ID.
    /// </summary>
    public string? TwinId { get; set; }

    /// <summary>
    /// Operation type (Created, Updated, Deleted, Skipped).
    /// </summary>
    public SyncOperationType Operation { get; set; }

    /// <summary>
    /// Error message if sync failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Error code if sync failed.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Conflict information if applicable.
    /// </summary>
    public ConflictInfo? Conflict { get; set; }
}

/// <summary>
/// Sync operation type.
/// </summary>
public enum SyncOperationType
{
    Created,
    Updated,
    Deleted,
    Skipped,
    Conflict
}

/// <summary>
/// Conflict information.
/// </summary>
public sealed class ConflictInfo
{
    /// <summary>
    /// Conflict detection timestamp.
    /// </summary>
    public DateTimeOffset DetectedAt { get; set; }

    /// <summary>
    /// Source version.
    /// </summary>
    public long SourceVersion { get; set; }

    /// <summary>
    /// Target version.
    /// </summary>
    public long TargetVersion { get; set; }

    /// <summary>
    /// Conflict resolution action taken.
    /// </summary>
    public string? ResolutionAction { get; set; }

    /// <summary>
    /// Additional conflict details.
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Batch sync statistics.
/// </summary>
public sealed class BatchSyncStatistics
{
    /// <summary>
    /// Total items processed.
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Number of failed operations.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Number of items skipped.
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Number of conflicts detected.
    /// </summary>
    public int Conflicts { get; set; }

    /// <summary>
    /// Sync start time.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Sync end time.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Total duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Breakdown by operation type.
    /// </summary>
    public Dictionary<SyncOperationType, int> OperationBreakdown { get; set; } = new();
}
