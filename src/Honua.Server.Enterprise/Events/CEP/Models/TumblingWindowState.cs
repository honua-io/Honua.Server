// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Events.CEP.Models;

/// <summary>
/// State for a tumbling window aggregation
/// </summary>
public class TumblingWindowState
{
    /// <summary>
    /// Unique window identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pattern being aggregated
    /// </summary>
    public Guid PatternId { get; set; }

    /// <summary>
    /// Window start time (aligned to window size)
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Window end time (aligned to window size)
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Partition key
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Number of events in this window
    /// </summary>
    public int EventCount { get; set; } = 0;

    /// <summary>
    /// Event IDs in this window
    /// </summary>
    public List<Guid> EventIds { get; set; } = new();

    /// <summary>
    /// Aggregated context
    /// </summary>
    public PatternMatchContext Context { get; set; } = new();

    /// <summary>
    /// Window status
    /// </summary>
    public TumblingWindowStatus Status { get; set; } = TumblingWindowStatus.Open;

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
/// Status of a tumbling window
/// </summary>
public enum TumblingWindowStatus
{
    /// <summary>
    /// Window is open and accepting events
    /// </summary>
    Open,

    /// <summary>
    /// Window is closed and evaluation is complete
    /// </summary>
    Closed,

    /// <summary>
    /// Window matched the pattern and triggered an alert
    /// </summary>
    Matched
}
