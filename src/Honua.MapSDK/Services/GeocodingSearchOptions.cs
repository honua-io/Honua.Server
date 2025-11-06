// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Services;

/// <summary>
/// Configuration options for geocoding search control.
/// </summary>
public sealed class GeocodingSearchOptions
{
    /// <summary>
    /// Maximum number of results to return per search.
    /// Default: 10
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Minimum number of characters before triggering autocomplete.
    /// Default: 3
    /// </summary>
    public int AutocompleteMinChars { get; set; } = 3;

    /// <summary>
    /// Debounce delay in milliseconds before executing search.
    /// Prevents excessive API calls during typing.
    /// Default: 300ms
    /// </summary>
    public int DebounceDelay { get; set; } = 300;

    /// <summary>
    /// Whether to enable search history persistence in localStorage.
    /// Default: true
    /// </summary>
    public bool EnableHistory { get; set; } = true;

    /// <summary>
    /// Maximum number of recent searches to store in history.
    /// Default: 10
    /// </summary>
    public int MaxHistoryItems { get; set; } = 10;

    /// <summary>
    /// Whether to bias search results to the current map viewport.
    /// When enabled, results closer to the visible area are ranked higher.
    /// Default: true
    /// </summary>
    public bool BiasToViewport { get; set; } = true;

    /// <summary>
    /// Default geocoding provider key (e.g., "azure-maps", "nominatim").
    /// If null, uses the first available provider.
    /// </summary>
    public string? DefaultProvider { get; set; }

    /// <summary>
    /// Whether to automatically fly to result location when selected.
    /// Default: true
    /// </summary>
    public bool FlyToResultOnSelect { get; set; } = true;

    /// <summary>
    /// Zoom level to use when flying to selected result.
    /// Default: 15
    /// </summary>
    public double FlyToZoomLevel { get; set; } = 15.0;

    /// <summary>
    /// Duration in milliseconds for fly-to animation.
    /// Default: 1500ms
    /// </summary>
    public int FlyToDuration { get; set; } = 1500;

    /// <summary>
    /// Whether to add a marker to the map when a result is selected.
    /// Default: true
    /// </summary>
    public bool AddMarkerOnSelect { get; set; } = true;

    /// <summary>
    /// Whether to clear previous markers when adding a new one.
    /// Default: true
    /// </summary>
    public bool ClearPreviousMarkers { get; set; } = true;

    /// <summary>
    /// Whether to show result type icons in the dropdown.
    /// Default: true
    /// </summary>
    public bool ShowResultIcons { get; set; } = true;

    /// <summary>
    /// Whether to show distance from map center in results.
    /// Only applicable when BiasToViewport is true.
    /// Default: true
    /// </summary>
    public bool ShowDistanceInResults { get; set; } = true;

    /// <summary>
    /// Whether to show relevance scores in results (for debugging).
    /// Default: false
    /// </summary>
    public bool ShowRelevanceScores { get; set; } = false;

    /// <summary>
    /// Placeholder text for the search input.
    /// Default: "Search for a location..."
    /// </summary>
    public string PlaceholderText { get; set; } = "Search for a location...";

    /// <summary>
    /// Whether to show the provider selector dropdown.
    /// Default: false (uses default provider only)
    /// </summary>
    public bool ShowProviderSelector { get; set; } = false;

    /// <summary>
    /// Whether to show the "Search this area" toggle.
    /// Default: true
    /// </summary>
    public bool ShowSearchThisAreaToggle { get; set; } = true;

    /// <summary>
    /// Whether to enable keyboard navigation in results.
    /// Default: true
    /// </summary>
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// Whether to show export options (copy coordinates, share link).
    /// Default: true
    /// </summary>
    public bool ShowExportOptions { get; set; } = true;

    /// <summary>
    /// CSS class to apply to the search container.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Whether to enable RTL (right-to-left) layout support.
    /// Default: false
    /// </summary>
    public bool EnableRTL { get; set; } = false;

    /// <summary>
    /// Creates a new instance with default values.
    /// </summary>
    public static GeocodingSearchOptions Default => new();

    /// <summary>
    /// Creates a minimal configuration with history and advanced features disabled.
    /// </summary>
    public static GeocodingSearchOptions Minimal => new()
    {
        EnableHistory = false,
        ShowProviderSelector = false,
        ShowSearchThisAreaToggle = false,
        ShowExportOptions = false,
        ShowDistanceInResults = false
    };

    /// <summary>
    /// Creates an advanced configuration with all features enabled.
    /// </summary>
    public static GeocodingSearchOptions Advanced => new()
    {
        EnableHistory = true,
        ShowProviderSelector = true,
        ShowSearchThisAreaToggle = true,
        ShowExportOptions = true,
        ShowDistanceInResults = true,
        ShowRelevanceScores = true,
        MaxHistoryItems = 20,
        MaxResults = 15
    };
}
