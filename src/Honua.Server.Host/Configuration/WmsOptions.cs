// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Configuration options for WMS (Web Map Service) implementation.
/// Controls image size limits, timeouts, and memory management for GetMap operations.
/// </summary>
public sealed class WmsOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Wms";

    /// <summary>
    /// Maximum allowed width in pixels for WMS GetMap requests.
    /// Larger images consume more memory and processing time.
    /// Default: 4096 pixels.
    /// </summary>
    [Range(256, 16384)]
    public int MaxWidth { get; set; } = 4096;

    /// <summary>
    /// Maximum allowed height in pixels for WMS GetMap requests.
    /// Larger images consume more memory and processing time.
    /// Default: 4096 pixels.
    /// </summary>
    [Range(256, 16384)]
    public int MaxHeight { get; set; } = 4096;

    /// <summary>
    /// Maximum total number of pixels (width * height) for WMS GetMap requests.
    /// This prevents excessive memory usage from non-square but large images.
    /// Default: 16,777,216 pixels (4096x4096).
    /// </summary>
    [Range(65536, 268435456)]
    public long MaxTotalPixels { get; set; } = 16_777_216; // 4096x4096

    /// <summary>
    /// Maximum timeout in seconds for rendering a single WMS GetMap request.
    /// Prevents long-running operations from consuming resources indefinitely.
    /// Default: 60 seconds.
    /// </summary>
    [Range(5, 300)]
    public int RenderTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Size threshold in bytes above which responses will be streamed directly
    /// instead of being cached in memory. Images smaller than this threshold
    /// may be buffered for caching purposes.
    /// Default: 2 MB (2,097,152 bytes).
    /// </summary>
    [Range(262144, 10485760)] // 256KB to 10MB
    public int StreamingThresholdBytes { get; set; } = 2_097_152; // 2MB

    /// <summary>
    /// Enable streaming responses for large images to reduce memory buffering.
    /// When enabled, large images are written directly to the response stream
    /// instead of being fully buffered in memory.
    /// Default: true.
    /// </summary>
    public bool EnableStreaming { get; set; } = true;
}
