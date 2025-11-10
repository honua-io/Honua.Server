// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.JSInterop;

namespace Honua.MapSDK.Utilities;

/// <summary>
/// Helper class for responsive design and screen size detection.
/// </summary>
public class ResponsiveHelper : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<ResponsiveHelper>? _objectReference;
    private bool _initialized;

    /// <summary>
    /// Gets the current breakpoint.
    /// </summary>
    public Breakpoint CurrentBreakpoint { get; private set; } = Breakpoint.Desktop;

    /// <summary>
    /// Gets a value indicating whether the device is mobile.
    /// </summary>
    public bool IsMobile => CurrentBreakpoint <= Breakpoint.Mobile;

    /// <summary>
    /// Gets a value indicating whether the device is tablet.
    /// </summary>
    public bool IsTablet => CurrentBreakpoint == Breakpoint.Tablet;

    /// <summary>
    /// Gets a value indicating whether the device is desktop.
    /// </summary>
    public bool IsDesktop => CurrentBreakpoint >= Breakpoint.Desktop;

    /// <summary>
    /// Gets a value indicating whether touch is supported.
    /// </summary>
    public bool IsTouchDevice { get; private set; }

    /// <summary>
    /// Event raised when the breakpoint changes.
    /// </summary>
    public event EventHandler<BreakpointChangedEventArgs>? BreakpointChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponsiveHelper"/> class.
    /// </summary>
    /// <param name="jsRuntime">JavaScript runtime.</param>
    public ResponsiveHelper(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Initializes the responsive helper.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _objectReference = DotNetObjectReference.Create(this);

        var screenInfo = await _jsRuntime.InvokeAsync<ScreenInfo>("HonuaMapSDK.Responsive.initialize", _objectReference);
        UpdateBreakpoint(screenInfo.Width);
        IsTouchDevice = screenInfo.IsTouchDevice;

        _initialized = true;
    }

    /// <summary>
    /// Gets the current screen dimensions.
    /// </summary>
    public async Task<(int Width, int Height)> GetScreenSizeAsync()
    {
        var info = await _jsRuntime.InvokeAsync<ScreenInfo>("HonuaMapSDK.Responsive.getScreenSize");
        return (info.Width, info.Height);
    }

    /// <summary>
    /// Checks if the screen matches a media query.
    /// </summary>
    /// <param name="mediaQuery">Media query string (e.g., "(max-width: 768px)").</param>
    public async Task<bool> MatchesMediaQueryAsync(string mediaQuery)
    {
        return await _jsRuntime.InvokeAsync<bool>("HonuaMapSDK.Responsive.matchesMediaQuery", mediaQuery);
    }

    /// <summary>
    /// Invoked from JavaScript when the window is resized.
    /// </summary>
    [JSInvokable]
    public void OnResize(int width, int height)
    {
        var oldBreakpoint = CurrentBreakpoint;
        UpdateBreakpoint(width);

        if (oldBreakpoint != CurrentBreakpoint)
        {
            BreakpointChanged?.Invoke(this, new BreakpointChangedEventArgs(oldBreakpoint, CurrentBreakpoint));
        }
    }

    /// <summary>
    /// Updates the current breakpoint based on width.
    /// </summary>
    private void UpdateBreakpoint(int width)
    {
        CurrentBreakpoint = width switch
        {
            < 640 => Breakpoint.Mobile,
            < 768 => Breakpoint.MobileLarge,
            < 1024 => Breakpoint.Tablet,
            < 1280 => Breakpoint.Desktop,
            < 1536 => Breakpoint.DesktopLarge,
            _ => Breakpoint.DesktopXLarge
        };
    }

    /// <summary>
    /// Disposes the responsive helper.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("HonuaMapSDK.Responsive.dispose");
            }
            catch
            {
                // Ignore errors during disposal
            }

            _objectReference?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private class ScreenInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsTouchDevice { get; set; }
    }
}

/// <summary>
/// Responsive breakpoints based on common device sizes.
/// </summary>
public enum Breakpoint
{
    /// <summary>Mobile devices (&lt; 640px)</summary>
    Mobile = 1,

    /// <summary>Large mobile devices (640px - 768px)</summary>
    MobileLarge = 2,

    /// <summary>Tablet devices (768px - 1024px)</summary>
    Tablet = 3,

    /// <summary>Desktop devices (1024px - 1280px)</summary>
    Desktop = 4,

    /// <summary>Large desktop devices (1280px - 1536px)</summary>
    DesktopLarge = 5,

    /// <summary>Extra large desktop devices (&gt;= 1536px)</summary>
    DesktopXLarge = 6
}

/// <summary>
/// Event args for breakpoint changes.
/// </summary>
public class BreakpointChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the old breakpoint.
    /// </summary>
    public Breakpoint OldBreakpoint { get; }

    /// <summary>
    /// Gets the new breakpoint.
    /// </summary>
    public Breakpoint NewBreakpoint { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BreakpointChangedEventArgs"/> class.
    /// </summary>
    public BreakpointChangedEventArgs(Breakpoint oldBreakpoint, Breakpoint newBreakpoint)
    {
        OldBreakpoint = oldBreakpoint;
        NewBreakpoint = newBreakpoint;
    }
}
