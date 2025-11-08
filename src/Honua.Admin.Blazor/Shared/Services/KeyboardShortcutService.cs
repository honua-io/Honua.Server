// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.JSInterop;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Manages global keyboard shortcuts for the Admin application.
/// </summary>
public class KeyboardShortcutService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, List<ShortcutHandler>> _handlers = new();
    private DotNetObjectReference<KeyboardShortcutService>? _objectReference;
    private bool _initialized;
    private string _sequenceBuffer = string.Empty;
    private DateTime _lastKeyTime = DateTime.MinValue;
    private const int SequenceTimeoutMs = 1000; // 1 second timeout for key sequences

    public KeyboardShortcutService(IJSRuntime jsRuntime)
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
        await _jsRuntime.InvokeVoidAsync("HonuaAdmin.KeyboardShortcuts.initialize", _objectReference);
        _initialized = true;
    }

    /// <summary>
    /// Registers a keyboard shortcut handler.
    /// </summary>
    /// <param name="shortcut">Shortcut key combination (e.g., "Ctrl+K", "G S").</param>
    /// <param name="handler">Handler to invoke when the shortcut is pressed.</param>
    /// <param name="description">Description of what the shortcut does.</param>
    /// <param name="category">Category for grouping shortcuts in help.</param>
    public void Register(string shortcut, Func<Task> handler, string description, string category = "General")
    {
        if (string.IsNullOrEmpty(shortcut))
            throw new ArgumentNullException(nameof(shortcut));

        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var normalizedShortcut = NormalizeShortcut(shortcut);

        if (!_handlers.ContainsKey(normalizedShortcut))
        {
            _handlers[normalizedShortcut] = new List<ShortcutHandler>();
        }

        _handlers[normalizedShortcut].Add(new ShortcutHandler
        {
            Handler = handler,
            Description = description,
            Category = category,
            Shortcut = shortcut
        });
    }

    /// <summary>
    /// Registers a synchronous keyboard shortcut handler.
    /// </summary>
    public void Register(string shortcut, Action handler, string description, string category = "General")
    {
        Register(shortcut, () =>
        {
            handler();
            return Task.CompletedTask;
        }, description, category);
    }

    /// <summary>
    /// Unregisters all handlers for a shortcut.
    /// </summary>
    public void Unregister(string shortcut)
    {
        var normalizedShortcut = NormalizeShortcut(shortcut);
        _handlers.Remove(normalizedShortcut);
    }

    /// <summary>
    /// Gets all registered shortcuts grouped by category.
    /// </summary>
    public Dictionary<string, List<ShortcutInfo>> GetAllShortcuts()
    {
        var shortcuts = _handlers
            .SelectMany(kvp => kvp.Value.Select(h => new ShortcutInfo
            {
                Shortcut = h.Shortcut,
                Description = h.Description,
                Category = h.Category
            }))
            .GroupBy(s => s.Category)
            .ToDictionary(g => g.Key, g => g.ToList());

        return shortcuts;
    }

    /// <summary>
    /// Invoked from JavaScript when a keyboard shortcut is pressed.
    /// </summary>
    [JSInvokable]
    public async Task OnShortcutPressed(string key, bool isModified)
    {
        try
        {
            // Handle sequence shortcuts (e.g., "G S")
            if (!isModified)
            {
                await HandleSequenceKey(key);
                return;
            }

            // Handle direct shortcuts (e.g., "Ctrl+K")
            var normalizedShortcut = NormalizeShortcut(key);
            await ExecuteHandlers(normalizedShortcut);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error executing keyboard shortcut: {ex}");
        }
    }

    /// <summary>
    /// Handles sequence-based shortcuts like "G S" (press G, then S).
    /// </summary>
    private async Task HandleSequenceKey(string key)
    {
        var now = DateTime.Now;

        // Reset buffer if timeout expired
        if ((now - _lastKeyTime).TotalMilliseconds > SequenceTimeoutMs)
        {
            _sequenceBuffer = string.Empty;
        }

        _lastKeyTime = now;
        _sequenceBuffer += key.ToUpper();

        // Try to match sequence
        var sequence = string.Join(" ", _sequenceBuffer.ToCharArray());
        var normalizedSequence = NormalizeShortcut(sequence);

        if (_handlers.ContainsKey(normalizedSequence))
        {
            await ExecuteHandlers(normalizedSequence);
            _sequenceBuffer = string.Empty;
        }
        else if (_sequenceBuffer.Length >= 2)
        {
            // No match and buffer is full, reset
            _sequenceBuffer = string.Empty;
        }
    }

    /// <summary>
    /// Executes all handlers for a given shortcut.
    /// </summary>
    private async Task ExecuteHandlers(string normalizedShortcut)
    {
        if (_handlers.TryGetValue(normalizedShortcut, out var handlers))
        {
            foreach (var handler in handlers)
            {
                try
                {
                    await handler.Handler();
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
        // Handle sequence shortcuts (e.g., "G S")
        if (shortcut.Contains(' '))
        {
            var keys = shortcut.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().ToUpper());
            return string.Join(" ", keys);
        }

        // Handle modified shortcuts (e.g., "Ctrl+K")
        var parts = shortcut.Split('+').Select(p => p.Trim()).ToList();

        // Sort modifiers alphabetically for consistency
        var modifiers = parts.Where(p => p is "Ctrl" or "Cmd" or "Alt" or "Shift" or "Meta")
            .Select(p => p == "Cmd" ? "Ctrl" : p) // Normalize Cmd to Ctrl
            .OrderBy(p => p)
            .ToList();

        var key = parts.FirstOrDefault(p => p is not "Ctrl" and not "Cmd" and not "Alt" and not "Shift" and not "Meta");

        if (key == null)
            throw new ArgumentException($"Invalid shortcut: {shortcut}");

        modifiers.Add(key.ToUpper());
        return string.Join("+", modifiers);
    }

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("HonuaAdmin.KeyboardShortcuts.dispose");
            }
            catch
            {
                // Ignore errors during disposal
            }

            _objectReference?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private class ShortcutHandler
    {
        public required Func<Task> Handler { get; init; }
        public required string Description { get; init; }
        public required string Category { get; init; }
        public required string Shortcut { get; init; }
    }
}

/// <summary>
/// Information about a registered keyboard shortcut.
/// </summary>
public class ShortcutInfo
{
    public required string Shortcut { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
}

/// <summary>
/// Common keyboard shortcuts for the Admin application.
/// </summary>
public static class AdminShortcuts
{
    public const string FocusSearch = "Ctrl+K";
    public const string NewItem = "N";
    public const string Refresh = "R";
    public const string CloseDialog = "Escape";
    public const string GoToServices = "G S";
    public const string GoToLayers = "G L";
    public const string GoToMaps = "G M";
    public const string ShowHelp = "?";
}
