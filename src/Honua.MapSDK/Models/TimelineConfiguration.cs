// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Configuration for HonuaTimeline component
/// </summary>
public class TimelineConfiguration
{
    /// <summary>
    /// Component ID
    /// </summary>
    public string Id { get; set; } = $"timeline-{Guid.NewGuid():N}";

    /// <summary>
    /// Map ID to sync with
    /// </summary>
    public string? SyncWith { get; set; }

    /// <summary>
    /// Field name containing timestamp data
    /// </summary>
    public string? TimeField { get; set; }

    /// <summary>
    /// Start time of timeline
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// End time of timeline
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Current time position
    /// </summary>
    public DateTime? CurrentTime { get; set; }

    /// <summary>
    /// Custom time steps (overrides auto-generated steps)
    /// </summary>
    public List<DateTime>? TimeSteps { get; set; }

    /// <summary>
    /// Time mode: Absolute, Relative, Index, or Custom
    /// </summary>
    public TimeMode Mode { get; set; } = TimeMode.Absolute;

    /// <summary>
    /// Time format string (e.g., "yyyy-MM-dd HH:mm:ss")
    /// </summary>
    public string TimeFormat { get; set; } = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// Playback speed in milliseconds per step
    /// </summary>
    public int PlaybackSpeed { get; set; } = 1000;

    /// <summary>
    /// Playback speed multiplier (0.25x, 0.5x, 1x, 2x, 4x, 8x)
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Enable loop playback
    /// </summary>
    public bool Loop { get; set; } = true;

    /// <summary>
    /// Show speed controls
    /// </summary>
    public bool ShowSpeedControls { get; set; } = true;

    /// <summary>
    /// Show step forward/backward buttons
    /// </summary>
    public bool ShowStepButtons { get; set; } = true;

    /// <summary>
    /// Show jump to start/end buttons
    /// </summary>
    public bool ShowJumpButtons { get; set; } = true;

    /// <summary>
    /// Show date range display
    /// </summary>
    public bool ShowDateRange { get; set; } = true;

    /// <summary>
    /// Show current time display
    /// </summary>
    public bool ShowCurrentTime { get; set; } = true;

    /// <summary>
    /// Enable compact mode (reduced size)
    /// </summary>
    public bool Compact { get; set; } = false;

    /// <summary>
    /// Component width
    /// </summary>
    public string Width { get; set; } = "100%";

    /// <summary>
    /// Component position
    /// </summary>
    public TimelinePosition Position { get; set; } = TimelinePosition.BottomCenter;

    /// <summary>
    /// Auto-start playback on load
    /// </summary>
    public bool AutoPlay { get; set; } = false;

    /// <summary>
    /// Enable reverse playback
    /// </summary>
    public bool EnableReverse { get; set; } = false;

    /// <summary>
    /// Step size in milliseconds (for auto-generated steps)
    /// </summary>
    public long? StepSize { get; set; }

    /// <summary>
    /// Step unit (Minutes, Hours, Days, Weeks, Months)
    /// </summary>
    public TimeStepUnit StepUnit { get; set; } = TimeStepUnit.Auto;

    /// <summary>
    /// Number of steps (auto-calculated if not specified)
    /// </summary>
    public int? TotalSteps { get; set; }

    /// <summary>
    /// Enable time range selection
    /// </summary>
    public bool EnableRangeSelection { get; set; } = false;

    /// <summary>
    /// Time zone display (Local or UTC)
    /// </summary>
    public TimeZoneDisplay TimeZone { get; set; } = TimeZoneDisplay.Local;

    /// <summary>
    /// Theme (Light or Dark)
    /// </summary>
    public string Theme { get; set; } = "light";

    /// <summary>
    /// Keyboard shortcuts enabled
    /// </summary>
    public bool EnableKeyboardShortcuts { get; set; } = true;

    /// <summary>
    /// Show bookmarks
    /// </summary>
    public bool ShowBookmarks { get; set; } = false;

    /// <summary>
    /// Bookmarked times
    /// </summary>
    public List<TimeBookmark>? Bookmarks { get; set; }
}

/// <summary>
/// Time mode enumeration
/// </summary>
public enum TimeMode
{
    /// <summary>
    /// Absolute dates/times
    /// </summary>
    Absolute,

    /// <summary>
    /// Relative to now (e.g., "2 hours ago")
    /// </summary>
    Relative,

    /// <summary>
    /// Simple index-based (frame 1, 2, 3...)
    /// </summary>
    Index,

    /// <summary>
    /// Custom user-defined steps
    /// </summary>
    Custom
}

/// <summary>
/// Timeline position enumeration
/// </summary>
public enum TimelinePosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
    None // Inline with page content
}

/// <summary>
/// Time step unit enumeration
/// </summary>
public enum TimeStepUnit
{
    Auto,
    Milliseconds,
    Seconds,
    Minutes,
    Hours,
    Days,
    Weeks,
    Months,
    Years
}

/// <summary>
/// Time zone display enumeration
/// </summary>
public enum TimeZoneDisplay
{
    Local,
    UTC
}

/// <summary>
/// Time bookmark
/// </summary>
public class TimeBookmark
{
    /// <summary>
    /// Bookmark ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Bookmark time
    /// </summary>
    public required DateTime Time { get; set; }

    /// <summary>
    /// Bookmark label
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Bookmark description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Bookmark color
    /// </summary>
    public string Color { get; set; } = "#3b82f6";
}

/// <summary>
/// Playback state enumeration
/// </summary>
public enum PlaybackState
{
    Stopped,
    Playing,
    Paused,
    PlayingReverse
}

/// <summary>
/// Time range for range selection
/// </summary>
public class TimeRange
{
    /// <summary>
    /// Range start time
    /// </summary>
    public required DateTime StartTime { get; set; }

    /// <summary>
    /// Range end time
    /// </summary>
    public required DateTime EndTime { get; set; }
}
