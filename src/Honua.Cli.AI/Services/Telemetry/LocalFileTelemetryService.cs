// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;

namespace Honua.Cli.AI.Services.Telemetry;

/// <summary>
/// Privacy-focused telemetry service that writes to local files.
/// Users can review/delete telemetry data at any time.
/// Default location: ~/.honua/telemetry/
/// </summary>
public sealed class LocalFileTelemetryService : ITelemetryService, IDisposable, IAsyncDisposable
{
    private readonly TelemetryOptions _options;
    private readonly string _telemetryPath;
    private readonly string _userId;
    private readonly string _sessionId;
    private readonly List<TelemetryEvent> _eventBatch = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Timer? _flushTimer;
    private bool _disposed;

    // Prevent concurrent timer flushes to avoid thread pool exhaustion
    private Task? _currentFlush;
    private readonly object _flushLock = new();

    public bool IsEnabled => _options.Enabled;

    public LocalFileTelemetryService(TelemetryOptions options)
    {
        _options = options;

        // Ensure user has explicitly opted in
        if (!_options.Enabled)
        {
            // Telemetry disabled - no-op mode
            _telemetryPath = string.Empty;
            _userId = string.Empty;
            _sessionId = string.Empty;
            return;
        }

        // Set up telemetry directory
        _telemetryPath = _options.LocalFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".honua",
            "telemetry");

        Directory.CreateDirectory(_telemetryPath);

        // Generate or load anonymous user ID
        _userId = GetOrCreateUserId();

        // Generate session ID
        _sessionId = Guid.NewGuid().ToString("N");

        // Start flush timer (only if ConsentTimestamp is set, indicating real use vs testing)
        if (_options.ConsentTimestamp.HasValue)
        {
            _flushTimer = new Timer(
                static state =>
                {
                    var self = (LocalFileTelemetryService)state!;
                    _ = self.FlushOnTimerAsync();
                },
                this,
                _options.FlushInterval,
                _options.FlushInterval);

            // Log that telemetry is enabled (first event) - only in real use, not tests
            _ = TrackFeatureAsync("TelemetryEnabled", new Dictionary<string, string>
            {
                ["ConsentTimestamp"] = _options.ConsentTimestamp.Value.ToString("O")
            });
        }
    }

    public async Task TrackCommandAsync(
        string commandName,
        bool success,
        TimeSpan duration,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        var telemetryEvent = new CommandTelemetryEvent
        {
            EventType = "Command",
            UserId = _userId,
            SessionId = _sessionId,
            CommandName = commandName,
            Success = success,
            Duration = duration,
            Version = GetVersion(),
            Platform = GetPlatform(),
            Properties = properties ?? new Dictionary<string, string>()
        };

        await AddEventAsync(telemetryEvent, cancellationToken);
    }

    public async Task TrackPlanAsync(
        string planType,
        int stepCount,
        bool success,
        TimeSpan duration,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        var telemetryEvent = new PlanTelemetryEvent
        {
            EventType = "Plan",
            UserId = _userId,
            SessionId = _sessionId,
            PlanType = planType,
            StepCount = stepCount,
            Success = success,
            Duration = duration,
            Version = GetVersion(),
            Platform = GetPlatform(),
            Properties = properties ?? new Dictionary<string, string>()
        };

        await AddEventAsync(telemetryEvent, cancellationToken);
    }

    public async Task TrackErrorAsync(
        string errorType,
        string? errorMessage = null,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        var telemetryEvent = new ErrorTelemetryEvent
        {
            EventType = "Error",
            UserId = _userId,
            SessionId = _sessionId,
            ErrorType = errorType,
            ErrorMessage = SanitizeErrorMessage(errorMessage),
            StackTrace = _options.CollectStackTraces ? errorMessage : null,
            Version = GetVersion(),
            Platform = GetPlatform(),
            Properties = properties ?? new Dictionary<string, string>()
        };

        await AddEventAsync(telemetryEvent, cancellationToken);
    }

    public async Task TrackFeatureAsync(
        string featureName,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        var telemetryEvent = new FeatureTelemetryEvent
        {
            EventType = "Feature",
            UserId = _userId,
            SessionId = _sessionId,
            FeatureName = featureName,
            Version = GetVersion(),
            Platform = GetPlatform(),
            Properties = properties ?? new Dictionary<string, string>()
        };

        await AddEventAsync(telemetryEvent, cancellationToken);
    }

    public async Task TrackLlmCallAsync(
        string provider,
        string model,
        int promptTokens,
        int completionTokens,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled) return;

        // Estimate cost based on known pricing
        var estimatedCost = EstimateLlmCost(provider, model, promptTokens, completionTokens);

        var telemetryEvent = new LlmTelemetryEvent
        {
            EventType = "LlmCall",
            UserId = _userId,
            SessionId = _sessionId,
            Provider = provider,
            Model = model,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            Duration = duration,
            EstimatedCost = estimatedCost,
            Version = GetVersion(),
            Platform = GetPlatform()
        };

        await AddEventAsync(telemetryEvent, cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _eventBatch.Count == 0) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await FlushInternalAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task FlushOnTimerAsync()
    {
        // Prevent concurrent flushes to avoid thread pool exhaustion
        lock (_flushLock)
        {
            if (_currentFlush?.IsCompleted == false)
            {
                // Previous flush still running, skip this cycle
                return;
            }

            _currentFlush = FlushInternalTimerAsync();
        }

        try
        {
            await _currentFlush.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log to stderr - telemetry failures should not throw but should be visible
            Console.Error.WriteLine($"Telemetry flush failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private async Task FlushInternalTimerAsync()
    {
        if (!IsEnabled || _eventBatch.Count == 0) return;

        // Use TryEnter to avoid blocking if another operation is in progress
        if (!await _lock.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await FlushInternalAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task FlushInternalAsync(CancellationToken cancellationToken)
    {
        if (_eventBatch.Count == 0) return;

        // Copy batch and clear
        var events = _eventBatch.ToList();
        _eventBatch.Clear();

        // Write to file (one file per day)
        var fileName = $"telemetry-{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
        var filePath = Path.Combine(_telemetryPath, fileName);

        // Append events as JSONL (newline-delimited JSON)
        var lines = events.Select(e => JsonSerializer.Serialize((object)e, e.GetType(), CliJsonOptions.Standard));

        await File.AppendAllLinesAsync(filePath, lines, cancellationToken);
    }

    private async Task AddEventAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _eventBatch.Add(telemetryEvent);

            // Auto-flush if batch size reached
            if (_eventBatch.Count >= _options.BatchSize)
            {
                await FlushInternalAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private string GetOrCreateUserId()
    {
        // Check if user ID already provided (e.g., in tests or config)
        if (!_options.UserId.IsNullOrEmpty())
        {
            return _options.UserId;
        }

        try
        {
            // Try to load from file
            var userIdFile = Path.Combine(_telemetryPath, ".userid");
            if (File.Exists(userIdFile))
            {
                var existingId = File.ReadAllText(userIdFile).Trim();
                if (!existingId.IsNullOrEmpty())
                {
                    return existingId;
                }
            }

            // Generate new anonymous ID
            var newUserId = Guid.NewGuid().ToString("N");
            File.WriteAllText(userIdFile, newUserId);

            return newUserId;
        }
        catch
        {
            // Fallback for environments where file I/O fails (e.g., tests, restricted environments)
            return Guid.NewGuid().ToString("N");
        }
    }

    private static string? SanitizeErrorMessage(string? errorMessage)
    {
        if (errorMessage.IsNullOrEmpty())
        {
            return null;
        }

        // Remove potential PII (file paths, connection strings, etc.)
        // This is a simple heuristic - could be improved
        var sanitized = errorMessage;

        // Remove file paths
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"[A-Za-z]:\\[\w\\\-\.]+",
            "[FILE_PATH]");

        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"/[\w/\-\.]+",
            "[FILE_PATH]");

        // Remove potential connection strings
        if (sanitized.Contains("Server=") || sanitized.Contains("Host="))
        {
            return "[CONNECTION_STRING_ERROR]";
        }

        // Truncate long messages
        if (sanitized.Length > 500)
        {
            sanitized = sanitized.Substring(0, 500) + "...";
        }

        return sanitized;
    }

    private static decimal? EstimateLlmCost(string provider, string model, int promptTokens, int completionTokens)
    {
        // Rough cost estimates (as of 2025)
        // These would ideally come from a pricing API or config
        return (provider.ToLowerInvariant(), model.ToLowerInvariant()) switch
        {
            ("openai", var m) when m.Contains("gpt-4") =>
                (promptTokens * 0.00003m + completionTokens * 0.00006m) / 1000,
            ("openai", var m) when m.Contains("gpt-3.5") =>
                (promptTokens * 0.000001m + completionTokens * 0.000002m) / 1000,
            ("anthropic", var m) when m.Contains("claude") =>
                (promptTokens * 0.00001m + completionTokens * 0.00003m) / 1000,
            _ => null
        };
    }

    private static string GetVersion()
    {
        // Get assembly version
        var assembly = typeof(LocalFileTelemetryService).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "unknown";
    }

    private static string GetPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";
        return "Unknown";
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _flushTimer?.Dispose();

        // Synchronous Dispose: Best-effort cleanup without async flush
        // Callers should prefer DisposeAsync() to ensure events are flushed
        _lock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop the flush timer
        if (_flushTimer is not null)
        {
            await _flushTimer.DisposeAsync().ConfigureAwait(false);
        }

        // Flush remaining events asynchronously
        if (_eventBatch.Count > 0)
        {
            try
            {
                await _lock.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                try
                {
                    await FlushInternalAsync(CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _lock.Release();
                }
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        _lock.Dispose();

        // Suppress finalization since we've cleaned up
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// No-op telemetry service when telemetry is disabled.
/// </summary>
public sealed class NullTelemetryService : ITelemetryService
{
    public bool IsEnabled => false;

    public Task TrackCommandAsync(
        string commandName,
        bool success,
        TimeSpan duration,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TrackPlanAsync(
        string planType,
        int stepCount,
        bool success,
        TimeSpan duration,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TrackErrorAsync(
        string errorType,
        string? errorMessage = null,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TrackFeatureAsync(
        string featureName,
        Dictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task TrackLlmCallAsync(
        string provider,
        string model,
        int promptTokens,
        int completionTokens,
        TimeSpan duration,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
