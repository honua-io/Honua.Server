using Microsoft.JSInterop;

namespace Honua.MapSDK.Services;

/// <summary>
/// Manages keyboard shortcuts for MapSDK components.
/// </summary>
public class KeyboardShortcuts : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, List<Func<Task>>> _handlers = new();
    private DotNetObjectReference<KeyboardShortcuts>? _objectReference;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardShortcuts"/> class.
    /// </summary>
    /// <param name="jsRuntime">JavaScript runtime for interop.</param>
    public KeyboardShortcuts(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>
    /// Initializes the keyboard shortcuts service.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _objectReference = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("HonuaMapSDK.KeyboardShortcuts.initialize", _objectReference);
        _initialized = true;
    }

    /// <summary>
    /// Registers a keyboard shortcut handler.
    /// </summary>
    /// <param name="shortcut">Shortcut key combination (e.g., "Ctrl+F", "Ctrl+Shift+E").</param>
    /// <param name="handler">Handler to invoke when the shortcut is pressed.</param>
    public void Register(string shortcut, Func<Task> handler)
    {
        if (string.IsNullOrEmpty(shortcut))
            throw new ArgumentNullException(nameof(shortcut));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var normalizedShortcut = NormalizeShortcut(shortcut);

        if (!_handlers.ContainsKey(normalizedShortcut))
        {
            _handlers[normalizedShortcut] = new List<Func<Task>>();
        }

        _handlers[normalizedShortcut].Add(handler);
    }

    /// <summary>
    /// Registers a synchronous keyboard shortcut handler.
    /// </summary>
    /// <param name="shortcut">Shortcut key combination.</param>
    /// <param name="handler">Handler to invoke when the shortcut is pressed.</param>
    public void Register(string shortcut, Action handler)
    {
        Register(shortcut, () =>
        {
            handler();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Unregisters all handlers for a shortcut.
    /// </summary>
    /// <param name="shortcut">Shortcut key combination.</param>
    public void Unregister(string shortcut)
    {
        var normalizedShortcut = NormalizeShortcut(shortcut);
        _handlers.Remove(normalizedShortcut);
    }

    /// <summary>
    /// Unregisters a specific handler for a shortcut.
    /// </summary>
    /// <param name="shortcut">Shortcut key combination.</param>
    /// <param name="handler">Handler to unregister.</param>
    public void Unregister(string shortcut, Func<Task> handler)
    {
        var normalizedShortcut = NormalizeShortcut(shortcut);
        if (_handlers.TryGetValue(normalizedShortcut, out var handlers))
        {
            handlers.Remove(handler);
            if (!handlers.Any())
            {
                _handlers.Remove(normalizedShortcut);
            }
        }
    }

    /// <summary>
    /// Registers default MapSDK keyboard shortcuts.
    /// </summary>
    /// <param name="onSearch">Handler for Ctrl+F (focus search).</param>
    /// <param name="onToggleFilters">Handler for Ctrl+E (toggle filters).</param>
    /// <param name="onToggleLegend">Handler for Ctrl+L (toggle legend).</param>
    /// <param name="onToggleHelp">Handler for Ctrl+H (toggle help).</param>
    /// <param name="onPlayPause">Handler for Space (play/pause timeline).</param>
    /// <param name="onClearSelection">Handler for Escape (clear selection).</param>
    public void RegisterDefaultShortcuts(
        Func<Task>? onSearch = null,
        Func<Task>? onToggleFilters = null,
        Func<Task>? onToggleLegend = null,
        Func<Task>? onToggleHelp = null,
        Func<Task>? onPlayPause = null,
        Func<Task>? onClearSelection = null)
    {
        if (onSearch != null)
            Register("Ctrl+F", onSearch);

        if (onToggleFilters != null)
            Register("Ctrl+E", onToggleFilters);

        if (onToggleLegend != null)
            Register("Ctrl+L", onToggleLegend);

        if (onToggleHelp != null)
            Register("Ctrl+H", onToggleHelp);

        if (onPlayPause != null)
            Register("Space", onPlayPause);

        if (onClearSelection != null)
            Register("Escape", onClearSelection);
    }

    /// <summary>
    /// Invoked from JavaScript when a keyboard shortcut is pressed.
    /// </summary>
    /// <param name="shortcut">The shortcut that was pressed.</param>
    [JSInvokable]
    public async Task OnShortcutPressed(string shortcut)
    {
        var normalizedShortcut = NormalizeShortcut(shortcut);

        if (_handlers.TryGetValue(normalizedShortcut, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    await handler();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error executing keyboard shortcut handler: {ex}");
                }
            }
        }
    }

    /// <summary>
    /// Normalizes a shortcut string to a consistent format.
    /// </summary>
    private static string NormalizeShortcut(string shortcut)
    {
        var parts = shortcut.Split('+').Select(p => p.Trim()).ToList();

        // Sort modifiers alphabetically for consistency
        var modifiers = parts.Where(p => p is "Ctrl" or "Alt" or "Shift" or "Meta").OrderBy(p => p).ToList();
        var key = parts.FirstOrDefault(p => p is not "Ctrl" and not "Alt" and not "Shift" and not "Meta");

        if (key == null)
            throw new ArgumentException($"Invalid shortcut: {shortcut}");

        modifiers.Add(key);
        return string.Join("+", modifiers);
    }

    /// <summary>
    /// Disposes the keyboard shortcuts service.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("HonuaMapSDK.KeyboardShortcuts.dispose");
            }
            catch
            {
                // Ignore errors during disposal
            }

            _objectReference?.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Common keyboard shortcuts for MapSDK.
/// </summary>
public static class CommonShortcuts
{
    public const string FocusSearch = "Ctrl+F";
    public const string ToggleFilters = "Ctrl+E";
    public const string ToggleLegend = "Ctrl+L";
    public const string ToggleHelp = "Ctrl+H";
    public const string PlayPause = "Space";
    public const string ClearSelection = "Escape";
    public const string ZoomIn = "Ctrl+Plus";
    public const string ZoomOut = "Ctrl+Minus";
    public const string ResetView = "Ctrl+0";
    public const string Save = "Ctrl+S";
    public const string Export = "Ctrl+Shift+E";
    public const string Print = "Ctrl+P";
}
