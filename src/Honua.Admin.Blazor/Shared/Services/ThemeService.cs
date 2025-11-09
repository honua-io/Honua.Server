using Microsoft.JSInterop;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for managing application theme (light/dark mode) with localStorage persistence and system theme detection
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _isDarkMode;
    private bool _initialized;

    /// <summary>
    /// Event fired when theme changes
    /// </summary>
    public event Action? OnThemeChanged;

    /// <summary>
    /// Gets whether dark mode is currently enabled
    /// </summary>
    public bool IsDarkMode => _isDarkMode;

    public ThemeService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Initializes the theme service by loading saved preference or detecting system theme
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            // Check localStorage first for saved preference
            var stored = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "theme");

            if (!string.IsNullOrEmpty(stored))
            {
                _isDarkMode = stored == "dark";
            }
            else
            {
                // Detect system preference if no saved preference
                _isDarkMode = await GetSystemThemeAsync();
            }

            await ApplyThemeAsync();
            _initialized = true;
        }
        catch (Exception)
        {
            // If JS interop fails (e.g., during prerendering), default to light mode
            _isDarkMode = false;
            _initialized = true;
        }
    }

    /// <summary>
    /// Toggles between light and dark mode
    /// </summary>
    public async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        await SavePreferenceAsync();
        await ApplyThemeAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Sets a specific theme mode
    /// </summary>
    public async Task SetThemeAsync(bool isDarkMode)
    {
        if (_isDarkMode == isDarkMode)
            return;

        _isDarkMode = isDarkMode;
        await SavePreferenceAsync();
        await ApplyThemeAsync();
        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Gets the system theme preference
    /// </summary>
    private async Task<bool> GetSystemThemeAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<bool>("ThemeHelper.getSystemTheme") == "dark";
        }
        catch
        {
            // If helper doesn't exist yet, use basic media query check
            try
            {
                var matches = await _jsRuntime.InvokeAsync<bool>("eval",
                    "window.matchMedia('(prefers-color-scheme: dark)').matches");
                return matches;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Saves theme preference to localStorage
    /// </summary>
    private async Task SavePreferenceAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "theme", _isDarkMode ? "dark" : "light");
        }
        catch
        {
            // Silently fail if localStorage is not available
        }
    }

    /// <summary>
    /// Applies the theme by setting data attribute on document element
    /// </summary>
    private async Task ApplyThemeAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("eval",
                $"document.documentElement.setAttribute('data-theme', '{(_isDarkMode ? "dark" : "light")}')");
        }
        catch
        {
            // Silently fail if DOM is not available (e.g., during prerendering)
        }
    }

    public void Dispose()
    {
        OnThemeChanged = null;
    }
}
