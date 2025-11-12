// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for file watching and change detection behavior.
/// Controls how the system monitors and responds to configuration file changes.
/// </summary>
public sealed class ConfigurationWatcherOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "ConfigurationWatcher";

    /// <summary>
    /// Gets or sets whether file watching is enabled for configuration files.
    /// When true, the system monitors configuration files for changes and reloads automatically.
    /// When false, configuration changes require application restart.
    /// Default: true.
    /// </summary>
    public bool EnableFileWatching { get; set; } = true;

    /// <summary>
    /// Gets or sets the debounce time in milliseconds before processing a configuration change.
    /// Prevents multiple rapid file changes from triggering redundant reloads.
    /// For example, a text editor may save multiple times in quick succession.
    /// Default: 500ms.
    /// </summary>
    [Range(0, 10000)]
    public int DebounceMilliseconds { get; set; } = 500;

    /// <summary>
    /// Gets or sets whether to log configuration change events.
    /// Useful for debugging configuration reload issues.
    /// Default: true.
    /// </summary>
    public bool LogChangeEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate configuration after reload.
    /// When true, configuration changes that fail validation are rejected.
    /// Default: true.
    /// </summary>
    public bool ValidateOnReload { get; set; } = true;
}
