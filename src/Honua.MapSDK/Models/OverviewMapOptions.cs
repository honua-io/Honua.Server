// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Configuration options for the overview map component
/// </summary>
public class OverviewMapOptions
{
    /// <summary>
    /// Width of the overview map in pixels
    /// </summary>
    public int Width { get; set; } = 200;

    /// <summary>
    /// Height of the overview map in pixels
    /// </summary>
    public int Height { get; set; } = 200;

    /// <summary>
    /// Position of overview map on the screen
    /// </summary>
    public OverviewMapPosition Position { get; set; } = OverviewMapPosition.BottomRight;

    /// <summary>
    /// Zoom offset relative to main map (negative = zoomed out)
    /// </summary>
    public int ZoomOffset { get; set; } = -5;

    /// <summary>
    /// Color of the extent box
    /// </summary>
    public string ExtentBoxColor { get; set; } = "#FF4444";

    /// <summary>
    /// Width of the extent box border in pixels
    /// </summary>
    public int ExtentBoxWidth { get; set; } = 2;

    /// <summary>
    /// Opacity of the extent box (0-1)
    /// </summary>
    public double ExtentBoxOpacity { get; set; } = 0.8;

    /// <summary>
    /// Color of the extent box fill
    /// </summary>
    public string ExtentBoxFillColor { get; set; } = "#FF4444";

    /// <summary>
    /// Opacity of the extent box fill (0-1)
    /// </summary>
    public double ExtentBoxFillOpacity { get; set; } = 0.1;

    /// <summary>
    /// Whether the overview map can be collapsed
    /// </summary>
    public bool Collapsible { get; set; } = true;

    /// <summary>
    /// Initial collapsed state
    /// </summary>
    public bool InitiallyCollapsed { get; set; } = false;

    /// <summary>
    /// Whether to rotate the overview map with the main map bearing
    /// </summary>
    public bool RotateWithBearing { get; set; } = false;

    /// <summary>
    /// Whether clicking the overview map pans the main map
    /// </summary>
    public bool ClickToPan { get; set; } = true;

    /// <summary>
    /// Whether dragging the extent box pans the main map
    /// </summary>
    public bool DragToPan { get; set; } = true;

    /// <summary>
    /// Whether scrolling over overview zooms the main map
    /// </summary>
    public bool ScrollToZoom { get; set; } = false;

    /// <summary>
    /// Custom basemap style URL for overview (null = use same as main map)
    /// </summary>
    public string? OverviewBasemap { get; set; }

    /// <summary>
    /// Horizontal offset in pixels (for custom positioning)
    /// </summary>
    public int OffsetX { get; set; } = 10;

    /// <summary>
    /// Vertical offset in pixels (for custom positioning)
    /// </summary>
    public int OffsetY { get; set; } = 10;

    /// <summary>
    /// Show toggle button to expand/collapse
    /// </summary>
    public bool ShowToggleButton { get; set; } = true;

    /// <summary>
    /// Border radius for rounded corners in pixels
    /// </summary>
    public int BorderRadius { get; set; } = 4;

    /// <summary>
    /// Box shadow for floating effect
    /// </summary>
    public string BoxShadow { get; set; } = "0 2px 8px rgba(0,0,0,0.3)";

    /// <summary>
    /// Border color
    /// </summary>
    public string BorderColor { get; set; } = "#ccc";

    /// <summary>
    /// Border width in pixels
    /// </summary>
    public int BorderWidth { get; set; } = 1;

    /// <summary>
    /// Background color of the overview map container
    /// </summary>
    public string BackgroundColor { get; set; } = "#fff";

    /// <summary>
    /// Whether to hide on mobile devices
    /// </summary>
    public bool HideOnMobile { get; set; } = false;

    /// <summary>
    /// Mobile breakpoint in pixels
    /// </summary>
    public int MobileBreakpoint { get; set; } = 768;

    /// <summary>
    /// Minimum zoom level for overview map
    /// </summary>
    public double? MinZoom { get; set; }

    /// <summary>
    /// Maximum zoom level for overview map
    /// </summary>
    public double? MaxZoom { get; set; }

    /// <summary>
    /// Update throttle in milliseconds (for performance)
    /// </summary>
    public int UpdateThrottleMs { get; set; } = 100;

    /// <summary>
    /// Whether to show navigation controls on overview map
    /// </summary>
    public bool ShowControls { get; set; } = false;

    /// <summary>
    /// Z-index for positioning
    /// </summary>
    public int ZIndex { get; set; } = 1000;
}

/// <summary>
/// Position options for overview map
/// </summary>
public enum OverviewMapPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Custom
}
