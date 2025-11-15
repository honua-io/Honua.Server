// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Honua.Server.Enterprise.Events.CEP.Models;

namespace Honua.Server.Enterprise.Events.CEP.Dto;

/// <summary>
/// Request to create a new CEP pattern
/// </summary>
public class CreatePatternRequest
{
    /// <summary>
    /// Pattern name
    /// </summary>
    [Required]
    [StringLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pattern description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of pattern
    /// </summary>
    [Required]
    public PatternType PatternType { get; set; }

    /// <summary>
    /// Pattern conditions
    /// </summary>
    [Required]
    [MinLength(1)]
    public List<EventCondition> Conditions { get; set; } = new();

    /// <summary>
    /// Window duration in seconds
    /// </summary>
    [Required]
    [Range(1, 86400)] // 1 second to 24 hours
    public int WindowDurationSeconds { get; set; }

    /// <summary>
    /// Window type
    /// </summary>
    [Required]
    public WindowType WindowType { get; set; } = WindowType.Sliding;

    /// <summary>
    /// Session gap in seconds (for session windows)
    /// </summary>
    [Range(1, 3600)]
    public int? SessionGapSeconds { get; set; }

    /// <summary>
    /// Alert name
    /// </summary>
    [Required]
    [StringLength(500)]
    public string AlertName { get; set; } = string.Empty;

    /// <summary>
    /// Alert severity
    /// </summary>
    [Required]
    [RegularExpression("^(critical|high|medium|low|info)$")]
    public string AlertSeverity { get; set; } = "medium";

    /// <summary>
    /// Alert description template
    /// </summary>
    public string? AlertDescription { get; set; }

    /// <summary>
    /// Additional alert labels
    /// </summary>
    public Dictionary<string, string>? AlertLabels { get; set; }

    /// <summary>
    /// Notification channel IDs
    /// </summary>
    public List<string>? NotificationChannelIds { get; set; }

    /// <summary>
    /// Pattern priority (higher = evaluated first)
    /// </summary>
    [Range(0, 100)]
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Response containing pattern details
/// </summary>
public class PatternResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PatternType PatternType { get; set; }
    public bool Enabled { get; set; }
    public List<EventCondition> Conditions { get; set; } = new();
    public int WindowDurationSeconds { get; set; }
    public WindowType WindowType { get; set; }
    public int? SessionGapSeconds { get; set; }
    public string AlertName { get; set; } = string.Empty;
    public string AlertSeverity { get; set; } = string.Empty;
    public string? AlertDescription { get; set; }
    public Dictionary<string, string>? AlertLabels { get; set; }
    public List<string>? NotificationChannelIds { get; set; }
    public int Priority { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Request to test a pattern against historical data
/// </summary>
public class TestPatternRequest
{
    /// <summary>
    /// Start time for historical replay
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// End time for historical replay
    /// </summary>
    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    [Range(1, 1000)]
    public int Limit { get; set; } = 100;
}

/// <summary>
/// Response containing pattern match history
/// </summary>
public class PatternMatchResponse
{
    public Guid MatchId { get; set; }
    public Guid PatternId { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public List<Guid> MatchedEventIds { get; set; } = new();
    public string PartitionKey { get; set; } = string.Empty;
    public PatternMatchContext MatchContext { get; set; } = new();
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public string? AlertFingerprint { get; set; }
    public string AlertSeverity { get; set; } = string.Empty;
    public DateTime AlertCreatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
