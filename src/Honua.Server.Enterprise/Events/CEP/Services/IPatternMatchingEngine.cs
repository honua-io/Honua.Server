// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.Events.CEP.Models;
using Honua.Server.Enterprise.Events.Models;

namespace Honua.Server.Enterprise.Events.CEP.Services;

/// <summary>
/// Complex Event Processing pattern matching engine
/// </summary>
public interface IPatternMatchingEngine
{
    /// <summary>
    /// Evaluate a geofence event against all active patterns
    /// </summary>
    /// <returns>List of pattern matches (partial or complete)</returns>
    Task<List<PatternMatchResult>> EvaluateEventAsync(
        GeofenceEvent geofenceEvent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleanup expired pattern states
    /// </summary>
    Task<CleanupResult> CleanupExpiredStatesAsync(
        int retentionHours = 24,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active pattern states for monitoring
    /// </summary>
    Task<List<ActivePatternState>> GetActiveStatesAsync(
        Guid? patternId = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Test a pattern against historical events
    /// </summary>
    Task<List<PatternMatchHistory>> TestPatternAsync(
        Guid patternId,
        DateTime startTime,
        DateTime endTime,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pattern evaluation
/// </summary>
public class PatternMatchResult
{
    /// <summary>
    /// Pattern that was evaluated
    /// </summary>
    public Guid PatternId { get; set; }

    /// <summary>
    /// Pattern name
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// Match type
    /// </summary>
    public MatchType MatchType { get; set; }

    /// <summary>
    /// State ID (if partial match)
    /// </summary>
    public Guid? StateId { get; set; }

    /// <summary>
    /// Match history ID (if complete match)
    /// </summary>
    public Guid? MatchHistoryId { get; set; }

    /// <summary>
    /// Alert fingerprint (if alert was generated)
    /// </summary>
    public string? AlertFingerprint { get; set; }
}

/// <summary>
/// Type of pattern match
/// </summary>
public enum MatchType
{
    /// <summary>
    /// No match
    /// </summary>
    None,

    /// <summary>
    /// Partial match (pattern in progress)
    /// </summary>
    Partial,

    /// <summary>
    /// Complete match (pattern satisfied, alert generated)
    /// </summary>
    Complete
}

/// <summary>
/// Result of cleanup operation
/// </summary>
public class CleanupResult
{
    /// <summary>
    /// Number of pattern states deleted
    /// </summary>
    public int PatternStatesDeleted { get; set; }

    /// <summary>
    /// Number of tumbling windows deleted
    /// </summary>
    public int TumblingWindowsDeleted { get; set; }
}

/// <summary>
/// Active pattern state information
/// </summary>
public class ActivePatternState
{
    /// <summary>
    /// State ID
    /// </summary>
    public Guid StateId { get; set; }

    /// <summary>
    /// Pattern ID
    /// </summary>
    public Guid PatternId { get; set; }

    /// <summary>
    /// Pattern name
    /// </summary>
    public string PatternName { get; set; } = string.Empty;

    /// <summary>
    /// Partition key
    /// </summary>
    public string PartitionKey { get; set; } = string.Empty;

    /// <summary>
    /// Number of events matched so far
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Window start time
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// Window end time
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Time remaining in seconds
    /// </summary>
    public int TimeRemainingSeconds { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public decimal ProgressPercent { get; set; }
}
