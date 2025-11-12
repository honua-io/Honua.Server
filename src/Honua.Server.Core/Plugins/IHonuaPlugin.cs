// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Plugins;

/// <summary>
/// Base interface for all Honua plugins.
/// All plugins must implement this interface to be discovered and loaded.
/// </summary>
public interface IHonuaPlugin
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// Format: reverse-DNS style (e.g., "honua.services.wfs").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable name for the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin version using semantic versioning.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Description of the plugin's functionality.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Author or organization that created the plugin.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Plugin dependencies (other plugins that must be loaded first).
    /// </summary>
    IReadOnlyList<PluginDependency> Dependencies { get; }

    /// <summary>
    /// Minimum Honua Server version required for this plugin.
    /// </summary>
    string MinimumHonuaVersion { get; }

    /// <summary>
    /// Called when the plugin is being loaded.
    /// Perform any initialization that doesn't require DI services here.
    /// </summary>
    /// <param name="context">Plugin loading context.</param>
    Task OnLoadAsync(PluginContext context);

    /// <summary>
    /// Called when the plugin is being unloaded (e.g., hot reload).
    /// Cleanup resources, save state, etc.
    /// </summary>
    Task OnUnloadAsync();
}

/// <summary>
/// Represents a dependency on another plugin.
/// </summary>
public sealed record PluginDependency(
    string PluginId,
    string MinimumVersion,
    bool Optional = false);

/// <summary>
/// Context provided to plugins during load/unload.
/// </summary>
public sealed class PluginContext
{
    /// <summary>
    /// Path to the plugin's directory.
    /// </summary>
    public string PluginPath { get; init; } = string.Empty;

    /// <summary>
    /// Application's configuration.
    /// </summary>
    public IConfiguration Configuration { get; init; } = null!;

    /// <summary>
    /// Hosting environment information.
    /// </summary>
    public IHostEnvironment Environment { get; init; } = null!;

    /// <summary>
    /// Logger for the plugin.
    /// </summary>
    public ILogger Logger { get; init; } = null!;

    /// <summary>
    /// Service provider (available after ConfigureServices phase).
    /// </summary>
    public IServiceProvider? ServiceProvider { get; set; }

    /// <summary>
    /// Metadata about other loaded plugins.
    /// </summary>
    public IReadOnlyDictionary<string, PluginMetadata> LoadedPlugins { get; init; } =
        new Dictionary<string, PluginMetadata>();
}

/// <summary>
/// Metadata about a loaded plugin.
/// </summary>
public sealed record PluginMetadata(
    string Id,
    string Name,
    string Version,
    string Author,
    PluginType Type,
    DateTime LoadedAt);

/// <summary>
/// Type of plugin.
/// </summary>
public enum PluginType
{
    /// <summary>
    /// Service plugin (WFS, WMS, OData, etc.)
    /// </summary>
    Service,

    /// <summary>
    /// Data provider plugin (PostgreSQL, MySQL, etc.)
    /// </summary>
    DataProvider,

    /// <summary>
    /// Export format plugin (Shapefile, GeoJSON, etc.)
    /// </summary>
    Exporter,

    /// <summary>
    /// Authentication provider plugin (OAuth, SAML, etc.)
    /// </summary>
    AuthProvider,

    /// <summary>
    /// Custom extension plugin.
    /// </summary>
    Extension
}
