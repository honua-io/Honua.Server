// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Events.CEP.Models;

/// <summary>
/// Tracks a partial pattern match in progress
/// </summary>
public class PatternMatchState
{
    /// <summary>
    /// Unique state identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pattern being matched
    /// </summary>
    public Guid PatternId { get; set; }

    /// <summary>
    /// Partition key for grouping (e.g., entity_id for entity-based patterns)
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Event IDs that have matched so far (ordered)
    /// </summary>
    public List<Guid> MatchedEventIds { get; set; } = new();

    /// <summary>
    /// Current condition index in sequence patterns
    /// </summary>
    public int CurrentConditionIndex { get; set; } = 0;

    /// <summary>
    /// Window start time
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Window end time
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Last event time (for session window gap detection)
    /// </summary>
    public DateTime LastEventTime { get; set; }

    /// <summary>
    /// Accumulated context data
    /// </summary>
    public PatternMatchContext Context { get; set; } = new();

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Updated timestamp
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Context accumulated during pattern matching
/// </summary>
public class PatternMatchContext
{
    /// <summary>
    /// Entity IDs involved in this match
    /// </summary>
    public List<string> EntityIds { get; set; } = new();

    /// <summary>
    /// Geofence IDs involved in this match
    /// </summary>
    public List<Guid> GeofenceIds { get; set; } = new();

    /// <summary>
    /// Geofence names involved in this match
    /// </summary>
    public List<string> GeofenceNames { get; set; } = new();

    /// <summary>
    /// Event types that have occurred
    /// </summary>
    public List<string> EventTypes { get; set; } = new();

    /// <summary>
    /// Event timestamps
    /// </summary>
    public List<DateTime> EventTimes { get; set; } = new();

    /// <summary>
    /// Dwell times (for exit events)
    /// </summary>
    public List<int?> DwellTimesSeconds { get; set; } = new();

    /// <summary>
    /// Number of unique entities (for correlation patterns)
    /// </summary>
    public int UniqueEntityCount { get; set; } = 0;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
