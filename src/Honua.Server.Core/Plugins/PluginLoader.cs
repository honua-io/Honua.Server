// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Plugins;

/// <summary>
/// Loads and manages Honua plugins.
/// Discovers plugins from directories, loads assemblies, and instantiates plugin classes.
/// </summary>
public sealed class PluginLoader : IDisposable
{
    private readonly ILogger<PluginLoader> logger;
    private readonly IConfiguration configuration;
    private readonly IHostEnvironment environment;
    private readonly Dictionary<string, LoadedPlugin> loadedPlugins = new();
    private readonly Dictionary<string, PluginLoadContext> loadContexts = new();

    public PluginLoader(
        ILogger<PluginLoader> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.environment = environment;
    }

    /// <summary>
    /// Discover and load all plugins from configured paths.
    /// </summary>
    public async Task<PluginLoadResult> LoadPluginsAsync(CancellationToken cancellationToken = default)
    {
        var result = new PluginLoadResult();
        var pluginPaths = this.GetPluginDiscoveryPaths();

        this.logger.LogInformation("Discovering plugins from {Count} paths", pluginPaths.Count);

        foreach (var basePath in pluginPaths)
        {
            if (!Directory.Exists(basePath))
            {
                this.logger.LogWarning("Plugin path does not exist: {Path}", basePath);
                continue;
            }

            // Each subdirectory is a plugin
            var pluginDirs = Directory.GetDirectories(basePath);
            this.logger.LogInformation("Found {Count} potential plugins in {Path}", pluginDirs.Length, basePath);

            foreach (var pluginDir in pluginDirs)
            {
                try
                {
                    var loadedPlugin = await this.LoadPluginFromDirectoryAsync(pluginDir, cancellationToken);
                    if (loadedPlugin != null)
                    {
                        this.loadedPlugins[loadedPlugin.Metadata.Id] = loadedPlugin;
                        result.LoadedPlugins.Add(loadedPlugin.Metadata);
                        this.logger.LogInformation(
                            "Loaded plugin: {PluginId} v{Version} ({Type})",
                            loadedPlugin.Metadata.Id,
                            loadedPlugin.Metadata.Version,
                            loadedPlugin.Plugin.GetType().Name);
                    }
                }
                catch (Exception ex)
                {
                    var pluginId = Path.GetFileName(pluginDir);
                    this.logger.LogError(ex, "Failed to load plugin from {Path}", pluginDir);
                    result.FailedPlugins.Add((pluginId, ex.Message));
                }
            }
        }

        this.logger.LogInformation(
            "Plugin loading complete: {Loaded} loaded, {Failed} failed",
            result.LoadedPlugins.Count,
            result.FailedPlugins.Count);

        return result;
    }

    /// <summary>
    /// Load a specific plugin from a directory.
    /// </summary>
    private async Task<LoadedPlugin?> LoadPluginFromDirectoryAsync(
        string pluginDir,
        CancellationToken cancellationToken)
    {
        // Look for plugin.json manifest
        var manifestPath = Path.Combine(pluginDir, "plugin.json");
        if (!File.Exists(manifestPath))
        {
            this.logger.LogWarning("No plugin.json found in {Path}, skipping", pluginDir);
            return null;
        }

        // Parse manifest
        var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (manifest == null)
        {
            throw new InvalidOperationException($"Failed to parse plugin manifest: {manifestPath}");
        }

        // Check if plugin should be loaded
        if (!this.ShouldLoadPlugin(manifest.Id))
        {
            this.logger.LogInformation("Plugin {PluginId} excluded by configuration", manifest.Id);
            return null;
        }

        // Load the plugin assembly
        var assemblyPath = Path.Combine(pluginDir, manifest.Assembly);
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");
        }

        // Create isolated load context for the plugin
        var loadContext = new PluginLoadContext(assemblyPath, isCollectible: this.environment.IsDevelopment());
        this.loadContexts[manifest.Id] = loadContext;

        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

        // Find and instantiate the plugin class
        var pluginType = assembly.GetType(manifest.EntryPoint);
        if (pluginType == null)
        {
            throw new TypeLoadException($"Plugin entry point not found: {manifest.EntryPoint}");
        }

        if (!typeof(IHonuaPlugin).IsAssignableFrom(pluginType))
        {
            throw new InvalidOperationException($"Plugin class must implement IHonuaPlugin: {pluginType.FullName}");
        }

        var plugin = (IHonuaPlugin?)Activator.CreateInstance(pluginType);
        if (plugin == null)
        {
            throw new InvalidOperationException($"Failed to create plugin instance: {pluginType.FullName}");
        }

        // Create plugin context
        var context = new PluginContext
        {
            PluginPath = pluginDir,
            Configuration = this.configuration,
            Environment = this.environment,
            Logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger(manifest.Id),
            LoadedPlugins = this.loadedPlugins.Values.Select(p => p.Metadata).ToDictionary(m => m.Id),
        };

        // Initialize the plugin
        await plugin.OnLoadAsync(context);

        var metadata = new PluginMetadata(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Author,
            ParsePluginType(manifest.PluginType),
            DateTime.UtcNow);

        return new LoadedPlugin(plugin, metadata, loadContext, manifest);
    }

    /// <summary>
    /// Get plugin discovery paths from configuration.
    /// </summary>
    private List<string> GetPluginDiscoveryPaths()
    {
        var paths = new List<string>();

        // Default path
        paths.Add(Path.Combine(AppContext.BaseDirectory, "plugins"));

        // Configuration V2 paths
        var configPaths = this.configuration.GetSection("honua:plugins:paths").Get<string[]>();
        if (configPaths != null)
        {
            paths.AddRange(configPaths);
        }

        // Legacy configuration paths
        var legacyPaths = this.configuration.GetSection("Plugins:Paths").Get<string[]>();
        if (legacyPaths != null)
        {
            paths.AddRange(legacyPaths);
        }

        return paths.Distinct().ToList();
    }

    /// <summary>
    /// Check if a plugin should be loaded based on configuration.
    /// </summary>
    private bool ShouldLoadPlugin(string pluginId)
    {
        // Check explicit exclusions
        var excluded = this.configuration.GetSection("honua:plugins:exclude").Get<string[]>();
        if (excluded?.Contains(pluginId) == true)
        {
            return false;
        }

        // Check explicit inclusions (if specified, only load these)
        var included = this.configuration.GetSection("honua:plugins:load").Get<string[]>();
        if (included != null && included.Length > 0)
        {
            return included.Contains(pluginId);
        }

        // Default: load all non-excluded plugins
        return true;
    }

    /// <summary>
    /// Get a loaded plugin by ID.
    /// </summary>
    public IHonuaPlugin? GetPlugin(string pluginId)
    {
        return this.loadedPlugins.TryGetValue(pluginId, out var loaded) ? loaded.Plugin : null;
    }

    /// <summary>
    /// Get all loaded plugins.
    /// </summary>
    public IReadOnlyList<LoadedPlugin> GetAllPlugins()
    {
        return this.loadedPlugins.Values.ToList();
    }

    /// <summary>
    /// Get all loaded service plugins.
    /// </summary>
    public IReadOnlyList<IServicePlugin> GetServicePlugins()
    {
        return this.loadedPlugins.Values
            .Where(p => p.Plugin is IServicePlugin)
            .Select(p => (IServicePlugin)p.Plugin)
            .ToList();
    }

    /// <summary>
    /// Unload a plugin (for hot reload).
    /// </summary>
    public async Task<bool> UnloadPluginAsync(string pluginId)
    {
        if (!this.loadedPlugins.TryGetValue(pluginId, out var loaded))
        {
            return false;
        }

        try
        {
            await loaded.Plugin.OnUnloadAsync();

            if (this.loadContexts.TryGetValue(pluginId, out var context))
            {
                context.Unload();
                this.loadContexts.Remove(pluginId);
            }

            this.loadedPlugins.Remove(pluginId);

            this.logger.LogInformation("Unloaded plugin: {PluginId}", pluginId);
            return true;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to unload plugin: {PluginId}", pluginId);
            return false;
        }
    }

    private static PluginType ParsePluginType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "service" => PluginType.Service,
            "dataprovider" => PluginType.DataProvider,
            "exporter" => PluginType.Exporter,
            "authprovider" => PluginType.AuthProvider,
            "extension" => PluginType.Extension,
            _ => PluginType.Extension,
        };
    }

    public void Dispose()
    {
        foreach (var context in this.loadContexts.Values)
        {
            context.Unload();
        }

        this.loadContexts.Clear();
        this.loadedPlugins.Clear();
    }
}

/// <summary>
/// Assembly load context for a plugin (enables isolation and hot reload).
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver resolver;

    public PluginLoadContext(string pluginPath, bool isCollectible = false)
        : base(name: Path.GetFileNameWithoutExtension(pluginPath), isCollectible: isCollectible)
    {
        this.resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve from plugin directory first
        var assemblyPath = this.resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return this.LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to default context (allows sharing Honua.Server.Core)
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = this.resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return this.LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}

/// <summary>
/// Plugin manifest (plugin.json).
/// </summary>
public sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string PluginType { get; set; } = "extension";
    public string Assembly { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = string.Empty;
    public List<PluginDependencyManifest> Dependencies { get; set; } = new();
    public string MinimumHonuaVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Plugin dependency in manifest.
/// </summary>
public sealed class PluginDependencyManifest
{
    public string PluginId { get; set; } = string.Empty;
    public string MinimumVersion { get; set; } = string.Empty;
    public bool Optional { get; set; }
}

/// <summary>
/// Represents a loaded plugin with metadata.
/// </summary>
public sealed record LoadedPlugin(
    IHonuaPlugin Plugin,
    PluginMetadata Metadata,
    PluginLoadContext LoadContext,
    PluginManifest Manifest);

/// <summary>
/// Result of plugin loading operation.
/// </summary>
public sealed class PluginLoadResult
{
    public List<PluginMetadata> LoadedPlugins { get; } = new();
    public List<(string PluginId, string Error)> FailedPlugins { get; } = new();

    public bool HasFailures => FailedPlugins.Count > 0;
}
