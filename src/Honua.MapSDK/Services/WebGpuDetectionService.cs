using Microsoft.JSInterop;
using System.Text.Json.Serialization;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for detecting and reporting WebGPU browser capabilities.
/// Provides client-side detection of WebGPU support and GPU information.
/// </summary>
public class WebGpuDetectionService
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _webGpuModule;

    /// <summary>
    /// Initializes a new instance of the WebGpuDetectionService.
    /// </summary>
    /// <param name="jsRuntime">The JavaScript runtime for interop calls.</param>
    public WebGpuDetectionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Detects WebGPU support in the current browser.
    /// </summary>
    /// <returns>A WebGpuCapability object containing detection results.</returns>
    public async Task<WebGpuCapability> DetectWebGpuSupportAsync()
    {
        try
        {
            await EnsureModuleLoadedAsync();

            if (_webGpuModule == null)
            {
                return new WebGpuCapability
                {
                    IsSupported = false,
                    Reason = "Failed to load WebGPU detection module"
                };
            }

            var capability = await _webGpuModule.InvokeAsync<WebGpuCapability>("detectWebGpuSupport");
            return capability;
        }
        catch (Exception ex)
        {
            return new WebGpuCapability
            {
                IsSupported = false,
                Reason = $"Error detecting WebGPU: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets detailed GPU adapter information if WebGPU is supported.
    /// </summary>
    /// <returns>GPU adapter information or null if not available.</returns>
    public async Task<GpuAdapterInfo?> GetGpuAdapterInfoAsync()
    {
        try
        {
            await EnsureModuleLoadedAsync();

            if (_webGpuModule == null)
            {
                return null;
            }

            return await _webGpuModule.InvokeAsync<GpuAdapterInfo?>("getGpuAdapterInfo");
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Ensures the JavaScript module is loaded.
    /// </summary>
    private async Task EnsureModuleLoadedAsync()
    {
        if (_webGpuModule == null)
        {
            _webGpuModule = await _jsRuntime.InvokeAsync<IJSObjectReference>(
                "import",
                "./_content/Honua.MapSDK/js/webgpu-manager.js"
            );
        }
    }

    /// <summary>
    /// Disposes the service and releases JavaScript resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_webGpuModule != null)
        {
            await _webGpuModule.DisposeAsync();
        }
    }
}

/// <summary>
/// Represents WebGPU browser capability information.
/// </summary>
public class WebGpuCapability
{
    /// <summary>
    /// Gets or sets whether WebGPU is supported in the current browser.
    /// </summary>
    [JsonPropertyName("isSupported")]
    public bool IsSupported { get; set; }

    /// <summary>
    /// Gets or sets the reason why WebGPU is not supported (if applicable).
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the browser name.
    /// </summary>
    [JsonPropertyName("browser")]
    public string? Browser { get; set; }

    /// <summary>
    /// Gets or sets the browser version.
    /// </summary>
    [JsonPropertyName("browserVersion")]
    public string? BrowserVersion { get; set; }

    /// <summary>
    /// Gets or sets whether the browser has navigator.gpu API.
    /// </summary>
    [JsonPropertyName("hasNavigatorGpu")]
    public bool HasNavigatorGpu { get; set; }
}

/// <summary>
/// Represents GPU adapter information from WebGPU.
/// </summary>
public class GpuAdapterInfo
{
    /// <summary>
    /// Gets or sets the GPU vendor name (e.g., "nvidia", "amd", "intel").
    /// </summary>
    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }

    /// <summary>
    /// Gets or sets the GPU architecture or description.
    /// </summary>
    [JsonPropertyName("architecture")]
    public string? Architecture { get; set; }

    /// <summary>
    /// Gets or sets the GPU device name.
    /// </summary>
    [JsonPropertyName("device")]
    public string? Device { get; set; }

    /// <summary>
    /// Gets or sets the GPU description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Represents the rendering engine being used by the map.
/// </summary>
public enum RenderingEngine
{
    /// <summary>
    /// Automatically select the best available renderer (WebGPU with WebGL fallback).
    /// </summary>
    Auto,

    /// <summary>
    /// Force WebGPU renderer (may fail if not supported).
    /// </summary>
    WebGPU,

    /// <summary>
    /// Force WebGL renderer.
    /// </summary>
    WebGL
}

/// <summary>
/// Represents runtime renderer information.
/// </summary>
public class RendererInfo
{
    /// <summary>
    /// Gets or sets the active rendering engine.
    /// </summary>
    [JsonPropertyName("engine")]
    public string Engine { get; set; } = "WebGL";

    /// <summary>
    /// Gets or sets whether this is the user's preferred engine.
    /// </summary>
    [JsonPropertyName("isPreferred")]
    public bool IsPreferred { get; set; }

    /// <summary>
    /// Gets or sets the current frames per second.
    /// </summary>
    [JsonPropertyName("fps")]
    public double Fps { get; set; }

    /// <summary>
    /// Gets or sets the GPU vendor.
    /// </summary>
    [JsonPropertyName("gpuVendor")]
    public string? GpuVendor { get; set; }

    /// <summary>
    /// Gets or sets the GPU renderer.
    /// </summary>
    [JsonPropertyName("gpuRenderer")]
    public string? GpuRenderer { get; set; }

    /// <summary>
    /// Gets or sets whether the fallback was triggered.
    /// </summary>
    [JsonPropertyName("isFallback")]
    public bool IsFallback { get; set; }
}
