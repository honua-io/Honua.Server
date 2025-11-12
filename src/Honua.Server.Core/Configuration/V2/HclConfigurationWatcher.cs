// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Watches .hcl configuration files for changes and provides change notifications via IChangeToken.
/// Implements debouncing to avoid multiple rapid reloads when files are modified.
/// </summary>
public sealed class HclConfigurationWatcher : IDisposable, IAsyncDisposable
{
    private readonly string _filePath;
    private readonly ILogger<HclConfigurationWatcher> _logger;
    private readonly TimeSpan _debounceDelay;
    private readonly object _lock = new();

    private FileSystemWatcher? _fileWatcher;
    private ConfigurationChangeToken? _currentChangeToken;
    private Timer? _debounceTimer;
    private bool _disposed;
    private bool _isStarted;

    /// <summary>
    /// Gets the current change token that will be triggered when configuration changes are detected.
    /// </summary>
    public IChangeToken CurrentChangeToken
    {
        get
        {
            lock (_lock)
            {
                if (_currentChangeToken == null || _currentChangeToken.HasChanged)
                {
                    _currentChangeToken = new ConfigurationChangeToken();
                }

                return _currentChangeToken;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HclConfigurationWatcher"/> class.
    /// </summary>
    /// <param name="filePath">The path to the .hcl configuration file to watch.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <param name="debounceDelay">
    /// The delay to wait after a file change is detected before triggering a reload.
    /// This prevents multiple rapid reloads when a file is saved multiple times in quick succession.
    /// Defaults to 500ms if not specified.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when filePath or logger is null.</exception>
    /// <exception cref="ArgumentException">Thrown when filePath is empty or whitespace.</exception>
    public HclConfigurationWatcher(
        string filePath,
        ILogger<HclConfigurationWatcher> logger,
        TimeSpan? debounceDelay = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        _filePath = filePath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceDelay = debounceDelay ?? TimeSpan.FromMilliseconds(500);

        if (_debounceDelay < TimeSpan.Zero)
        {
            throw new ArgumentException("Debounce delay cannot be negative.", nameof(debounceDelay));
        }
    }

    /// <summary>
    /// Starts watching the configuration file for changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the start operation.</param>
    /// <exception cref="FileNotFoundException">Thrown when the configuration file does not exist.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the watcher has been disposed.</exception>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_isStarted)
            {
                _logger.LogDebug("Configuration watcher already started for: {FilePath}", _filePath);
                return Task.CompletedTask;
            }

            if (!File.Exists(_filePath))
            {
                throw new FileNotFoundException($"Configuration file not found: {_filePath}");
            }

            var directory = Path.GetDirectoryName(_filePath);
            var fileName = Path.GetFileName(_filePath);

            if (string.IsNullOrEmpty(directory))
            {
                directory = Directory.GetCurrentDirectory();
            }

            _fileWatcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = false
            };

            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Renamed += OnFileRenamed;
            _fileWatcher.Deleted += OnFileDeleted;
            _fileWatcher.Error += OnFileWatcherError;

            _fileWatcher.EnableRaisingEvents = true;
            _isStarted = true;

            _logger.LogInformation(
                "Started watching configuration file: {FilePath} (debounce: {DebounceMs}ms)",
                _filePath,
                _debounceDelay.TotalMilliseconds);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops watching the configuration file for changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the stop operation.</param>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_isStarted)
            {
                return Task.CompletedTask;
            }

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Renamed -= OnFileRenamed;
                _fileWatcher.Deleted -= OnFileDeleted;
                _fileWatcher.Error -= OnFileWatcherError;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _debounceTimer?.Dispose();
            _debounceTimer = null;

            _isStarted = false;

            _logger.LogInformation("Stopped watching configuration file: {FilePath}", _filePath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles file change events with debouncing to prevent multiple rapid reloads.
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Configuration file changed: {FilePath} ({ChangeType})", e.FullPath, e.ChangeType);
        TriggerDebounce();
    }

    /// <summary>
    /// Handles file rename events.
    /// </summary>
    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogInformation(
            "Configuration file renamed: {OldPath} -> {NewPath}",
            e.OldFullPath,
            e.FullPath);
        TriggerDebounce();
    }

    /// <summary>
    /// Handles file deletion events.
    /// </summary>
    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogWarning("Configuration file deleted: {FilePath}", e.FullPath);
        TriggerDebounce();
    }

    /// <summary>
    /// Handles file watcher errors.
    /// </summary>
    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        _logger.LogError(
            exception,
            "Error occurred while watching configuration file: {FilePath}",
            _filePath);

        // Try to restart the watcher if it failed
        lock (_lock)
        {
            if (_fileWatcher != null && !_disposed)
            {
                try
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.EnableRaisingEvents = true;
                    _logger.LogInformation("Successfully restarted file watcher after error");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restart file watcher after error");
                }
            }
        }
    }

    /// <summary>
    /// Triggers the debounce timer to delay configuration reload.
    /// If the timer is already running, it will be reset.
    /// </summary>
    private void TriggerDebounce()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            // Dispose existing timer if it exists
            _debounceTimer?.Dispose();

            // Create new timer that will trigger after debounce delay
            _debounceTimer = new Timer(
                callback: _ => OnDebouncedChange(),
                state: null,
                dueTime: _debounceDelay,
                period: Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Called after the debounce delay has elapsed to trigger the change token.
    /// </summary>
    private void OnDebouncedChange()
    {
        lock (_lock)
        {
            if (_disposed || _currentChangeToken == null)
            {
                return;
            }

            _logger.LogInformation(
                "Configuration change detected after debounce delay. Triggering reload for: {FilePath}",
                _filePath);

            try
            {
                _currentChangeToken.OnChange();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while triggering configuration change token for: {FilePath}",
                    _filePath);
            }

            // Create a new token for the next change
            _currentChangeToken = new ConfigurationChangeToken();
        }
    }

    /// <summary>
    /// Throws an ObjectDisposedException if the watcher has been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HclConfigurationWatcher));
        }
    }

    /// <summary>
    /// Disposes the watcher and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _debounceTimer?.Dispose();
            _debounceTimer = null;

            _currentChangeToken?.Dispose();
            _currentChangeToken = null;
        }

        _logger.LogDebug("Configuration watcher disposed: {FilePath}", _filePath);
    }

    /// <summary>
    /// Asynchronously disposes the watcher and releases all resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        Dispose();
    }
}
