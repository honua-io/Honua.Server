// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Events.CEP.Models;

/// <summary>
/// Completed pattern match record (audit trail)
/// </summary>
public class PatternMatchHistory
{
    /// <summary>
    /// Unique match identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pattern that was matched
    /// </summary>
    public Guid PatternId { get; set; }

    /// <summary>
    /// Pattern name (denormalized for history)
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// Events that formed the match
    /// </summary>
    public List<Guid> MatchedEventIds { get; set; } = new();

    /// <summary>
    /// Partition key
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Full match context
    /// </summary>
    public PatternMatchContext MatchContext { get; set; } = new();

    /// <summary>
    /// Window start time
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Window end time
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Alert fingerprint (if alert was generated)
    /// </summary>
    public string? AlertFingerprint { get; set; }

    /// <summary>
    /// Alert severity
    /// </summary>
    public string AlertSeverity { get; set; } = "medium";

    /// <summary>
    /// When alert was created
    /// </summary>
    public DateTime AlertCreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pattern match statistics
/// </summary>
public class PatternMatchStatistics
{
    /// <summary>
    /// Pattern ID
    /// </summary>
    public Guid PatternId { get; set; }

    /// <summary>
    /// Pattern name
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of matches
    /// </summary>
    public long TotalMatches { get; set; }

    /// <summary>
    /// Matches by severity
    /// </summary>
    public Dictionary<string, long> MatchesBySeverity { get; set; } = new();

    /// <summary>
    /// Average events per match
    /// </summary>
    public decimal AverageEventsPerMatch { get; set; }

    /// <summary>
    /// Number of unique partitions (entities)
    /// </summary>
    public long UniquePartitions { get; set; }

    /// <summary>
    /// First match time
    /// </summary>
    public DateTime? FirstMatch { get; set; }

    /// <summary>
    /// Last match time
    /// </summary>
    public DateTime? LastMatch { get; set; }
}
