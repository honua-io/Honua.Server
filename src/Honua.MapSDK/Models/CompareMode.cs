namespace Honua.MapSDK.Models;

/// <summary>
/// Comparison modes for HonuaCompare component
/// </summary>
public enum CompareMode
{
    /// <summary>
    /// Side-by-side split view with vertical divider
    /// </summary>
    SideBySide,

    /// <summary>
    /// Swipe tool with draggable divider
    /// </summary>
    Swipe,

    /// <summary>
    /// Overlay mode with opacity control
    /// </summary>
    Overlay,

    /// <summary>
    /// Flicker mode - toggle between views
    /// </summary>
    Flicker,

    /// <summary>
    /// Spy glass - magnifying circle view
    /// </summary>
    SpyGlass
}

/// <summary>
/// Orientation for split/swipe modes
/// </summary>
public enum CompareOrientation
{
    /// <summary>
    /// Vertical divider (left/right split)
    /// </summary>
    Vertical,

    /// <summary>
    /// Horizontal divider (top/bottom split)
    /// </summary>
    Horizontal
}

/// <summary>
/// Configuration for HonuaCompare component
/// </summary>
public class CompareConfig
{
    /// <summary>
    /// Left/before map style URL
    /// </summary>
    public required string LeftMapStyle { get; set; }

    /// <summary>
    /// Right/after map style URL
    /// </summary>
    public required string RightMapStyle { get; set; }

    /// <summary>
    /// Comparison mode
    /// </summary>
    public CompareMode Mode { get; set; } = CompareMode.Swipe;

    /// <summary>
    /// Divider position (0-1)
    /// </summary>
    public double DividerPosition { get; set; } = 0.5;

    /// <summary>
    /// Orientation for split/swipe modes
    /// </summary>
    public CompareOrientation Orientation { get; set; } = CompareOrientation.Vertical;

    /// <summary>
    /// Sync navigation between maps
    /// </summary>
    public bool SyncNavigation { get; set; } = true;

    /// <summary>
    /// Initial center coordinates
    /// </summary>
    public double[]? Center { get; set; }

    /// <summary>
    /// Initial zoom level
    /// </summary>
    public double? Zoom { get; set; }

    /// <summary>
    /// Left map label
    /// </summary>
    public string LeftLabel { get; set; } = "Before";

    /// <summary>
    /// Right map label
    /// </summary>
    public string RightLabel { get; set; } = "After";

    /// <summary>
    /// Show labels
    /// </summary>
    public bool ShowLabels { get; set; } = true;

    /// <summary>
    /// Overlay opacity (for overlay mode)
    /// </summary>
    public double OverlayOpacity { get; set; } = 0.5;

    /// <summary>
    /// Spy glass radius in pixels
    /// </summary>
    public int SpyGlassRadius { get; set; } = 150;

    /// <summary>
    /// Flicker interval in milliseconds
    /// </summary>
    public int FlickerInterval { get; set; } = 1000;
}

/// <summary>
/// Timestamp information for temporal comparison
/// </summary>
public class CompareTimestamp
{
    /// <summary>
    /// Timestamp
    /// </summary>
    public required DateTime Time { get; set; }

    /// <summary>
    /// Display label
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }
}
