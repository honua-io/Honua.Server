// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Map projection types for rendering
/// </summary>
public enum MapProjection
{
    /// <summary>
    /// Standard Mercator projection (2D)
    /// </summary>
    Mercator,

    /// <summary>
    /// Globe projection (3D) - requires MapLibre GL JS v5.0+
    /// </summary>
    Globe
}

/// <summary>
/// Configuration options for globe projection
/// </summary>
public class GlobeProjectionOptions
{
    /// <summary>
    /// Enable atmospheric halo effect around the globe
    /// </summary>
    public bool EnableAtmosphere { get; set; } = true;

    /// <summary>
    /// Color of the atmosphere (CSS color string)
    /// </summary>
    public string AtmosphereColor { get; set; } = "#87CEEB";

    /// <summary>
    /// Enable space/stars background
    /// </summary>
    public bool EnableSpace { get; set; } = true;

    /// <summary>
    /// Transition duration in milliseconds when switching projections
    /// </summary>
    public int TransitionDuration { get; set; } = 1000;

    /// <summary>
    /// Enable smooth animated transition between projections
    /// </summary>
    public bool EnableTransition { get; set; } = true;

    /// <summary>
    /// Automatically adjust camera when switching to globe projection
    /// </summary>
    public bool AutoAdjustCamera { get; set; } = true;

    /// <summary>
    /// Default zoom level when switching to globe projection (if AutoAdjustCamera is true)
    /// </summary>
    public double? GlobeDefaultZoom { get; set; } = 1.5;
}
