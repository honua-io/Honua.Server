// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Observability;

/// <summary>
/// Service for managing runtime logging configuration changes.
/// Allows dynamic adjustment of log levels for specific categories without restart.
/// </summary>
public sealed class RuntimeLoggingConfigurationService
{
    private readonly ConcurrentDictionary<string, LogLevel> _categoryLevels = new();

    /// <summary>
    /// Sets the minimum log level for a specific category.
    /// </summary>
    public void SetLevel(string category, LogLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        _categoryLevels[category] = level;
    }

    /// <summary>
    /// Gets the configured log level for a category, if set.
    /// </summary>
    public bool TryGetLevel(string category, out LogLevel level)
    {
        return _categoryLevels.TryGetValue(category, out level);
    }

    /// <summary>
    /// Gets all configured category levels.
    /// </summary>
    public IReadOnlyDictionary<string, LogLevel> GetAllLevels()
    {
        return _categoryLevels;
    }

    /// <summary>
    /// Determines whether logging is enabled for the provided category and level based on runtime overrides.
    /// Returns null when the override is not specified, allowing the default configuration to decide.
    /// </summary>
    public bool? IsEnabled(string categoryName, LogLevel logLevel)
    {
        var category = string.IsNullOrWhiteSpace(categoryName) ? "" : categoryName;

        if (_categoryLevels.TryGetValue(category, out var minLevel))
        {
            return logLevel >= minLevel;
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var parts = category.Split('.');
            for (var i = parts.Length - 1; i > 0; i--)
            {
                var parentCategory = string.Join('.', parts[..i]);
                if (_categoryLevels.TryGetValue(parentCategory, out minLevel))
                {
                    return logLevel >= minLevel;
                }
            }
        }

        if (_categoryLevels.TryGetValue("Default", out minLevel))
        {
            return logLevel >= minLevel;
        }

        return null;
    }

    /// <summary>
    /// Removes the runtime override for a category, reverting to configuration file settings.
    /// </summary>
    public bool RemoveLevel(string category)
    {
        return _categoryLevels.TryRemove(category, out _);
    }

    /// <summary>
    /// Clears all runtime log level overrides.
    /// </summary>
    public void Clear()
    {
        _categoryLevels.Clear();
    }
}

/// <summary>
/// Log filter that uses RuntimeLoggingConfigurationService for dynamic log levels.
/// </summary>
public sealed class RuntimeLoggingFilter : ILoggerProvider, IDisposable
{
    private readonly RuntimeLoggingConfigurationService configService;
    private readonly ILoggerProvider innerProvider;

    public RuntimeLoggingFilter(RuntimeLoggingConfigurationService configService, ILoggerProvider innerProvider)
    {
        this.configService = Guard.NotNull(configService);
        this.innerProvider = Guard.NotNull(innerProvider);
    }

    public ILogger CreateLogger(string categoryName)
    {
        var innerLogger = this.innerProvider.CreateLogger(categoryName);
        return new FilteredLogger(categoryName, innerLogger, this.configService);
    }

    public void Dispose()
    {
        if (this.innerProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private sealed class FilteredLogger : ILogger
    {
        private readonly string categoryName;
        private readonly ILogger innerLogger;
        private readonly RuntimeLoggingConfigurationService configService;

        public FilteredLogger(string categoryName, ILogger innerLogger, RuntimeLoggingConfigurationService configService)
        {
            this.categoryName = categoryName;
            this.innerLogger = innerLogger;
            this.configService = configService;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return this.innerLogger.BeginScope(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            // Check if there's a runtime override
            if (this.configService.TryGetLevel(this.categoryName, out var minLevel))
            {
                return logLevel >= minLevel;
            }

            // Check parent categories (e.g., "Honua.Server" for "Honua.Server.Core.Data")
            var parts = this.categoryName.Split('.');
            for (var i = parts.Length - 1; i > 0; i--)
            {
                var parentCategory = string.Join('.', parts[..i]);
                if (this.configService.TryGetLevel(parentCategory, out minLevel))
                {
                    return logLevel >= minLevel;
                }
            }

            // Check "Default" category
            if (this.configService.TryGetLevel("Default", out minLevel))
            {
                return logLevel >= minLevel;
            }

            // Fall back to inner logger's configuration
            return this.innerLogger.IsEnabled(logLevel);
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            this.innerLogger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
