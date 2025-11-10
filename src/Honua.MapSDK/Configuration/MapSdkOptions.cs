// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.MapSDK.Services.DataLoading;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Configuration;

/// <summary>
/// Global configuration options for MapSDK.
/// </summary>
public class MapSdkOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether performance monitoring is enabled.
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = false;

    /// <summary>
    /// Gets or sets the log level for MapSDK components.
    /// </summary>
    public LogLevel LogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets cache configuration options.
    /// </summary>
    public CacheOptions Cache { get; set; } = new();

    /// <summary>
    /// Gets or sets the default map style URL.
    /// </summary>
    public string DefaultMapStyle { get; set; } = "https://api.maptiler.com/maps/basic-v2/style.json";

    /// <summary>
    /// Gets or sets geocoding configuration options.
    /// </summary>
    public GeocodingOptions Geocoding { get; set; } = new();

    /// <summary>
    /// Gets or sets timeline configuration options.
    /// </summary>
    public TimelineOptions Timeline { get; set; } = new();

    /// <summary>
    /// Gets or sets rendering configuration options.
    /// </summary>
    public RenderingOptions Rendering { get; set; } = new();

    /// <summary>
    /// Gets or sets data loading configuration options.
    /// </summary>
    public DataLoadingOptions DataLoading { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable development tools.
    /// </summary>
    public bool EnableDevTools { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable ComponentBus message logging.
    /// </summary>
    public bool EnableMessageTracing { get; set; } = false;

    /// <summary>
    /// Gets or sets accessibility configuration options.
    /// </summary>
    public AccessibilityOptions Accessibility { get; set; } = new();
}

/// <summary>
/// Geocoding configuration options.
/// </summary>
public class GeocodingOptions
{
    /// <summary>
    /// Gets or sets the geocoding API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the geocoding provider (e.g., "mapbox", "google", "nominatim").
    /// </summary>
    public string Provider { get; set; } = "nominatim";

    /// <summary>
    /// Gets or sets the geocoding API base URL.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to cache geocoding results.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache TTL for geocoding results in seconds.
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 86400; // 24 hours
}

/// <summary>
/// Timeline configuration options.
/// </summary>
public class TimelineOptions
{
    /// <summary>
    /// Gets or sets the default playback speed multiplier.
    /// </summary>
    public double DefaultPlaybackSpeed { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the default step interval in milliseconds.
    /// </summary>
    public int DefaultStepIntervalMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to loop playback by default.
    /// </summary>
    public bool DefaultLoop { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of timeline steps.
    /// </summary>
    public int MaxSteps { get; set; } = 10000;
}

/// <summary>
/// Rendering configuration options.
/// </summary>
public class RenderingOptions
{
    /// <summary>
    /// Gets or sets the threshold for enabling virtual scrolling in DataGrid.
    /// </summary>
    public int VirtualScrollThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the threshold for enabling data downsampling in charts.
    /// </summary>
    public int ChartDownsampleThreshold { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the clustering threshold for map features.
    /// </summary>
    public int MapClusteringThreshold { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the debounce delay for updates in milliseconds.
    /// </summary>
    public int DebounceDelayMs { get; set; } = 300;

    /// <summary>
    /// Gets or sets a value indicating whether to enable smooth animations.
    /// </summary>
    public bool EnableAnimations { get; set; } = true;

    /// <summary>
    /// Gets or sets the animation duration in milliseconds.
    /// </summary>
    public int AnimationDurationMs { get; set; } = 300;
}

/// <summary>
/// Data loading configuration options.
/// </summary>
public class DataLoadingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of parallel data requests.
    /// </summary>
    public int MaxParallelRequests { get; set; } = 4;

    /// <summary>
    /// Gets or sets the request timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 30000; // 30 seconds

    /// <summary>
    /// Gets or sets the chunk size for streaming large datasets.
    /// </summary>
    public int StreamingChunkSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to enable request deduplication.
    /// </summary>
    public bool EnableDeduplication { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable compression.
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable retry on failure.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Accessibility configuration options.
/// </summary>
public class AccessibilityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable screen reader announcements.
    /// </summary>
    public bool EnableScreenReaderAnnouncements { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable keyboard shortcuts.
    /// </summary>
    public bool EnableKeyboardShortcuts { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable high contrast mode detection.
    /// </summary>
    public bool EnableHighContrastMode { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable focus indicators.
    /// </summary>
    public bool EnableFocusIndicators { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to respect reduced motion preferences.
    /// </summary>
    public bool RespectReducedMotion { get; set; } = true;
}
