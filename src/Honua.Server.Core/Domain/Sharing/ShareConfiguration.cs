// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing;

/// <summary>
/// Value object representing the configuration for embedded shared maps.
/// Encapsulates all display and interaction settings for the embedded view.
/// </summary>
public sealed class ShareConfiguration : ValueObject
{
    /// <summary>
    /// Gets the width of the embedded map (e.g., "100%", "800px")
    /// </summary>
    public string Width { get; }

    /// <summary>
    /// Gets the height of the embedded map (e.g., "600px", "100vh")
    /// </summary>
    public string Height { get; }

    /// <summary>
    /// Gets whether to show zoom controls
    /// </summary>
    public bool ShowZoomControls { get; }

    /// <summary>
    /// Gets whether to show layer switcher
    /// </summary>
    public bool ShowLayerSwitcher { get; }

    /// <summary>
    /// Gets whether to show search box
    /// </summary>
    public bool ShowSearch { get; }

    /// <summary>
    /// Gets whether to show scale bar
    /// </summary>
    public bool ShowScaleBar { get; }

    /// <summary>
    /// Gets whether to show attribution
    /// </summary>
    public bool ShowAttribution { get; }

    /// <summary>
    /// Gets whether to allow fullscreen mode
    /// </summary>
    public bool AllowFullscreen { get; }

    /// <summary>
    /// Gets the custom CSS for the embedded map
    /// </summary>
    public string? CustomCss { get; }

    /// <summary>
    /// Private constructor for creating a ShareConfiguration
    /// </summary>
    private ShareConfiguration(
        string width,
        string height,
        bool showZoomControls,
        bool showLayerSwitcher,
        bool showSearch,
        bool showScaleBar,
        bool showAttribution,
        bool allowFullscreen,
        string? customCss)
    {
        Width = width;
        Height = height;
        ShowZoomControls = showZoomControls;
        ShowLayerSwitcher = showLayerSwitcher;
        ShowSearch = showSearch;
        ShowScaleBar = showScaleBar;
        ShowAttribution = showAttribution;
        AllowFullscreen = allowFullscreen;
        CustomCss = customCss;
    }

    /// <summary>
    /// Creates a default ShareConfiguration with standard settings
    /// </summary>
    /// <returns>A new ShareConfiguration with default values</returns>
    public static ShareConfiguration CreateDefault()
    {
        return new ShareConfiguration(
            width: "100%",
            height: "600px",
            showZoomControls: true,
            showLayerSwitcher: true,
            showSearch: false,
            showScaleBar: true,
            showAttribution: true,
            allowFullscreen: true,
            customCss: null);
    }

    /// <summary>
    /// Creates a custom ShareConfiguration with specified settings
    /// </summary>
    /// <param name="width">Width of the embedded map</param>
    /// <param name="height">Height of the embedded map</param>
    /// <param name="showZoomControls">Whether to show zoom controls</param>
    /// <param name="showLayerSwitcher">Whether to show layer switcher</param>
    /// <param name="showSearch">Whether to show search box</param>
    /// <param name="showScaleBar">Whether to show scale bar</param>
    /// <param name="showAttribution">Whether to show attribution</param>
    /// <param name="allowFullscreen">Whether to allow fullscreen mode</param>
    /// <param name="customCss">Custom CSS for the embedded map</param>
    /// <returns>A new ShareConfiguration instance</returns>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid</exception>
    public static ShareConfiguration Create(
        string width,
        string height,
        bool showZoomControls = true,
        bool showLayerSwitcher = true,
        bool showSearch = false,
        bool showScaleBar = true,
        bool showAttribution = true,
        bool allowFullscreen = true,
        string? customCss = null)
    {
        if (string.IsNullOrWhiteSpace(width))
            throw new ArgumentException("Width cannot be empty", nameof(width));

        if (string.IsNullOrWhiteSpace(height))
            throw new ArgumentException("Height cannot be empty", nameof(height));

        if (!IsValidDimension(width))
            throw new ArgumentException($"Width '{width}' is not a valid dimension (e.g., '100%', '800px')", nameof(width));

        if (!IsValidDimension(height))
            throw new ArgumentException($"Height '{height}' is not a valid dimension (e.g., '600px', '100vh')", nameof(height));

        if (customCss?.Length > 10000)
            throw new ArgumentException("Custom CSS must not exceed 10000 characters", nameof(customCss));

        return new ShareConfiguration(
            width,
            height,
            showZoomControls,
            showLayerSwitcher,
            showSearch,
            showScaleBar,
            showAttribution,
            allowFullscreen,
            customCss);
    }

    /// <summary>
    /// Validates a dimension value (e.g., "100%", "800px", "100vh")
    /// </summary>
    private static bool IsValidDimension(string dimension)
    {
        if (string.IsNullOrWhiteSpace(dimension))
            return false;

        // Basic validation for common CSS dimension formats
        var validUnits = new[] { "%", "px", "em", "rem", "vh", "vw" };
        return validUnits.Any(unit => dimension.EndsWith(unit, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Width;
        yield return Height;
        yield return ShowZoomControls;
        yield return ShowLayerSwitcher;
        yield return ShowSearch;
        yield return ShowScaleBar;
        yield return ShowAttribution;
        yield return AllowFullscreen;
        yield return CustomCss;
    }

    /// <summary>
    /// Returns a string representation of the configuration
    /// </summary>
    public override string ToString()
    {
        return $"ShareConfiguration: {Width}x{Height}, Controls={ShowZoomControls}, Layers={ShowLayerSwitcher}";
    }
}
