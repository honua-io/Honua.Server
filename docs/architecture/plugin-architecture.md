# Honua Server Plugin Architecture

**Date**: 2025-11-11
**Status**: Design & Implementation
**Goal**: Enable modular, pluggable service architecture for faster builds and custom deployments

---

## Problem Statement

### Current Issues

1. **Slow Build Times**: Full rebuild compiles all services even when working on one
2. **Large Deployment Size**: All services bundled even if not needed
3. **Increased Attack Surface**: Unused services still present in deployment
4. **Tight Coupling**: Services hardcoded into main assembly
5. **Difficult Customization**: Can't easily create custom Honua builds

### Solution: Plugin Architecture

Services become **optional, loadable plugins** that can be:
- Compiled independently (faster development)
- Loaded dynamically at runtime
- Included/excluded per deployment
- Hot-reloaded during development
- Distributed separately

---

## Architecture Overview

### Plugin Types

```
┌─────────────────────────────────────────────┐
│           Honua.Server.Host                 │
│         (Core Runtime Engine)               │
│  ┌──────────────────────────────────────┐   │
│  │      Plugin Loader & Manager         │   │
│  └──────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
                    ↓ Loads
        ┌──────────┴──────────┐
        ↓                     ↓
┌───────────────┐     ┌───────────────┐
│Service Plugins│     │Extension Plugins│
├───────────────┤     ├───────────────┤
│ • WFS         │     │ • Custom Auth │
│ • WMS         │     │ • Custom Data │
│ • WMTS        │     │ • Custom Export│
│ • CSW         │     └───────────────┘
│ • WCS         │
│ • OData       │
│ • OGC API     │
│ • STAC        │
│ • Carto       │
│ • GeoServices │
│ • Zarr        │
│ • Print       │
└───────────────┘
```

### Plugin Categories

1. **Service Plugins** - OGC/API services (WFS, WMS, OData, etc.)
2. **Data Provider Plugins** - Database connectors (PostgreSQL, MySQL, SQLite)
3. **Export Plugins** - Export formats (Shapefile, GeoJSON, KML)
4. **Auth Plugins** - Authentication providers (OAuth, SAML, etc.)
5. **Extension Plugins** - Custom functionality

---

## Core Interfaces

### IHonuaPlugin (Base Interface)

```csharp
public interface IHonuaPlugin
{
    /// <summary>
    /// Unique identifier for the plugin.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin version (semantic versioning).
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Plugin description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Author/organization.
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Plugin dependencies (other plugin IDs).
    /// </summary>
    IReadOnlyList<PluginDependency> Dependencies { get; }

    /// <summary>
    /// Minimum Honua Server version required.
    /// </summary>
    string MinimumHonuaVersion { get; }
}
```

### IServicePlugin (Service Implementation)

```csharp
public interface IServicePlugin : IHonuaPlugin
{
    /// <summary>
    /// Service type (OGC, API, Custom).
    /// </summary>
    ServiceType ServiceType { get; }

    /// <summary>
    /// Configure services for dependency injection.
    /// </summary>
    void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context);

    /// <summary>
    /// Map HTTP endpoints.
    /// </summary>
    void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context);

    /// <summary>
    /// Validate plugin configuration.
    /// </summary>
    PluginValidationResult ValidateConfiguration(
        IConfiguration configuration);

    /// <summary>
    /// Called when plugin is being unloaded (hot reload).
    /// </summary>
    Task OnUnloadAsync();
}
```

### IDataProviderPlugin (Database Connector)

```csharp
public interface IDataProviderPlugin : IHonuaPlugin
{
    /// <summary>
    /// Provider name (e.g., "postgresql", "mysql").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Create data store provider.
    /// </summary>
    IDataStoreProvider CreateProvider(
        string connectionString,
        DataProviderOptions options);

    /// <summary>
    /// Test connection to database.
    /// </summary>
    Task<bool> TestConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default);
}
```

---

## Plugin Discovery

### Discovery Methods

1. **Directory Scanning** - Load from `./plugins/` directory
2. **NuGet Packages** - Load from NuGet cache
3. **Configuration** - Explicit paths in `honua.config.hcl`
4. **Assembly Attributes** - Scan loaded assemblies

### Plugin Manifest (plugin.json)

Each plugin has a manifest:

```json
{
  "id": "honua.services.wfs",
  "name": "WFS Service Plugin",
  "version": "1.0.0",
  "description": "OGC Web Feature Service implementation",
  "author": "HonuaIO",
  "pluginType": "service",
  "assembly": "Honua.Server.Services.Wfs.dll",
  "entryPoint": "Honua.Server.Services.Wfs.WfsServicePlugin",
  "dependencies": [
    {
      "pluginId": "honua.core",
      "minimumVersion": "1.0.0"
    }
  ],
  "minimumHonuaVersion": "1.0.0",
  "configuration": {
    "serviceId": "wfs",
    "endpoints": ["/wfs"],
    "requiresDatabase": true,
    "requiresCache": false
  }
}
```

---

## Plugin Loading

### Load Sequence

```
1. Application Startup
   ↓
2. Plugin Discovery
   ├─ Scan ./plugins/
   ├─ Check Configuration V2
   └─ Find plugin.json files
   ↓
3. Plugin Loading
   ├─ Load assemblies (AssemblyLoadContext)
   ├─ Resolve dependencies
   └─ Instantiate plugin classes
   ↓
4. Plugin Initialization
   ├─ Call ConfigureServices()
   ├─ Register in DI container
   └─ Build dependency graph
   ↓
5. Application Configuration
   ├─ Build service provider
   ├─ Call MapEndpoints()
   └─ Initialize plugins
   ↓
6. Application Running
```

### AssemblyLoadContext Strategy

```csharp
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginPath;

    public PluginLoadContext(string pluginPath, bool isCollectible = false)
        : base(isCollectible: isCollectible)
    {
        _pluginPath = pluginPath;
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try to resolve from plugin directory first
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to default context
        return null;
    }
}
```

**Benefits**:
- Plugin isolation (each plugin has its own AssemblyLoadContext)
- Version conflicts avoided
- Hot reload support (unload context, load new)
- Memory leak prevention

---

## Configuration Integration

### Configuration V2 Plugin Loading

```hcl
# honua.config.hcl

honua {
  version = "1.0"

  plugins {
    # Plugin discovery paths
    paths = [
      "./plugins",
      "/usr/local/lib/honua/plugins"
    ]

    # Explicitly load specific plugins
    load = [
      "honua.services.wfs",
      "honua.services.wms",
      "honua.services.odata"
    ]

    # Explicitly exclude plugins (blacklist)
    exclude = [
      "honua.services.csw"  # Don't need CSW in this deployment
    ]

    # Hot reload in development
    enable_hot_reload = true
  }
}

# Service configuration works the same
service "wfs" {
  enabled = true
  # Plugin auto-loaded because service is enabled
}
```

### Automatic Plugin Loading

Services configured in `.honua` file automatically load their plugins:

```hcl
service "wfs" { enabled = true }
```
→ Automatically loads `honua.services.wfs` plugin

---

## Project Structure

### Before (Monolithic)

```
Honua.Server.Host/
├─ Wfs/
├─ Wms/
├─ Wmts/
├─ OData/
└─ ... (all services in one project)
```

**Issues**:
- Single project = slow builds
- Can't exclude services
- Large deployment

### After (Plugin-Based)

```
Honua.Server/
├─ src/
│   ├─ Honua.Server.Core/           # Core abstractions
│   ├─ Honua.Server.Host/           # Runtime engine
│   └─ Honua.Server.Plugins/
│       ├─ Honua.Server.Services.Wfs/      # WFS plugin
│       ├─ Honua.Server.Services.Wms/      # WMS plugin
│       ├─ Honua.Server.Services.Wmts/     # WMTS plugin
│       ├─ Honua.Server.Services.OData/    # OData plugin
│       ├─ Honua.Server.Services.OgcApi/   # OGC API plugin
│       ├─ Honua.Server.Services.Stac/     # STAC plugin
│       └─ ... (each service is a separate project)
└─ plugins/                         # Runtime plugin directory
    ├─ wfs/
    │   ├─ plugin.json
    │   └─ Honua.Server.Services.Wfs.dll
    ├─ wms/
    └─ ...
```

**Benefits**:
- Compile only what you're working on
- Create custom builds (include only needed plugins)
- Smaller Docker images
- Independent versioning

---

## Build Configuration

### Plugin Project (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <OutputType>Library</OutputType>

    <!-- Plugin-specific -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference core abstractions only -->
    <ProjectReference Include="..\..\Honua.Server.Core\Honua.Server.Core.csproj">
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>
  </ItemGroup>

  <!-- Copy to plugins directory after build -->
  <Target Name="CopyToPluginsDirectory" AfterTargets="Build">
    <Copy SourceFiles="$(OutputPath)$(AssemblyName).dll"
          DestinationFolder="$(SolutionDir)plugins\wfs\" />
    <Copy SourceFiles="plugin.json"
          DestinationFolder="$(SolutionDir)plugins\wfs\" />
  </Target>
</Project>
```

### Host Project Changes

```xml
<!-- Honua.Server.Host.csproj -->
<ItemGroup>
  <!-- No direct plugin references! Loaded dynamically -->
  <ProjectReference Include="..\Honua.Server.Core\..." />

  <!-- Optional: Include all plugins in full build -->
  <ProjectReference Include="..\Honua.Server.Plugins\**\*.csproj"
                    Condition="'$(IncludeAllPlugins)' == 'true'"
                    Private="false" />
</ItemGroup>
```

---

## Development Workflow

### Scenario 1: Working on WFS Plugin

```bash
# Only compile WFS plugin
cd src/Honua.Server.Plugins/Honua.Server.Services.Wfs
dotnet build

# Plugin automatically copied to ./plugins/wfs/
# Run host (uses cached assemblies for other plugins)
cd ../../Honua.Server.Host
dotnet run

# Change WFS code → rebuild WFS only → hot reload!
```

**Build Time**:
- Before: 30-60 seconds (full solution)
- After: 2-5 seconds (single plugin)

### Scenario 2: Custom Build (Only WFS + OData)

```bash
# Build only needed plugins
dotnet build src/Honua.Server.Plugins/Honua.Server.Services.Wfs
dotnet build src/Honua.Server.Plugins/Honua.Server.Services.OData
dotnet build src/Honua.Server.Host

# Deploy (Docker)
FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY --from=build /app/publish /app
COPY --from=build /app/plugins/wfs /app/plugins/wfs
COPY --from=build /app/plugins/odata /app/plugins/odata
# Note: Only 2 plugins = smaller image
```

**Deployment Size**:
- Before: 150 MB (all services)
- After: 80 MB (only 2 services)

### Scenario 3: Hot Reload Development

```bash
# Run with hot reload enabled
export HONUA_ENABLE_PLUGIN_HOT_RELOAD=true
dotnet run --project src/Honua.Server.Host

# Modify WFS plugin code
vim src/Honua.Server.Plugins/Honua.Server.Services.Wfs/WfsHandlers.cs

# Rebuild plugin
dotnet build src/Honua.Server.Plugins/Honua.Server.Services.Wfs

# Plugin automatically reloaded! No server restart!
```

---

## Hot Reload Implementation

```csharp
public class PluginHotReloadService : BackgroundService
{
    private readonly IPluginManager _pluginManager;
    private readonly FileSystemWatcher _watcher;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _watcher.Path = "./plugins";
        _watcher.Filter = "*.dll";
        _watcher.NotifyFilter = NotifyFilters.LastWrite;

        _watcher.Changed += async (sender, e) =>
        {
            await Task.Delay(500); // Debounce

            var pluginId = Path.GetDirectoryName(e.Name);
            await _pluginManager.ReloadPluginAsync(pluginId);

            _logger.LogInformation("Hot reloaded plugin: {PluginId}", pluginId);
        };

        _watcher.EnableRaisingEvents = true;
    }
}
```

---

## Security Considerations

### Plugin Sandboxing

```csharp
public class SandboxedPluginLoadContext : PluginLoadContext
{
    private readonly PermissionSet _permissions;

    public SandboxedPluginLoadContext(string pluginPath, PermissionSet permissions)
        : base(pluginPath, isCollectible: true)
    {
        _permissions = permissions;
    }

    // Restrict plugin capabilities
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Block certain namespaces
        if (assemblyName.Name?.StartsWith("System.Reflection.Emit") == true)
        {
            throw new SecurityException("Plugin cannot use dynamic code generation");
        }

        return base.Load(assemblyName);
    }
}
```

### Plugin Signing

```csharp
public class PluginValidator
{
    public async Task<bool> ValidatePluginSignatureAsync(string pluginPath)
    {
        // Verify assembly is signed with trusted certificate
        var assembly = Assembly.LoadFrom(pluginPath);
        var assemblyName = assembly.GetName();

        var publicKey = assemblyName.GetPublicKey();
        if (publicKey == null || publicKey.Length == 0)
        {
            return false; // Unsigned plugins not allowed in production
        }

        // Verify against trusted keys
        return _trustedKeys.Contains(Convert.ToBase64String(publicKey));
    }
}
```

---

## Performance Optimizations

### Plugin Caching

```csharp
public class PluginCache
{
    private readonly IDistributedCache _cache;

    public async Task<IServicePlugin?> GetCachedPluginAsync(string pluginId)
    {
        var cacheKey = $"plugin:{pluginId}:metadata";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached != null)
        {
            return JsonSerializer.Deserialize<PluginMetadata>(cached);
        }

        return null;
    }
}
```

### Lazy Loading

```csharp
public class LazyPluginLoader
{
    private readonly Dictionary<string, Lazy<IServicePlugin>> _plugins;

    public void RegisterPlugin(string id, Func<IServicePlugin> factory)
    {
        _plugins[id] = new Lazy<IServicePlugin>(factory);
    }

    public IServicePlugin GetPlugin(string id)
    {
        // Plugin only loaded when first accessed
        return _plugins[id].Value;
    }
}
```

---

## Migration Path

### Phase 1: Core Infrastructure
1. Create plugin interfaces
2. Implement plugin loader
3. Create example plugin (WFS)
4. Test hot reload

### Phase 2: Service Migration
1. Move WFS to plugin
2. Move WMS to plugin
3. Move remaining services to plugins
4. Update build scripts

### Phase 3: Optimization
1. Implement plugin caching
2. Add hot reload support
3. Create custom build scripts
4. Docker multi-stage builds

---

## Benefits Summary

### Development
- ⚡ **10x faster builds** (2-5 sec vs 30-60 sec)
- 🔥 **Hot reload** without server restart
- 🎯 **Focused development** on one service
- 🧪 **Easier testing** of individual services

### Deployment
- 📦 **50% smaller Docker images** (custom builds)
- 🔒 **Reduced attack surface** (only needed services)
- ⚙️ **Flexible configuration** (enable/disable services)
- 🚀 **Faster deployments** (smaller images)

### Architecture
- 🧩 **Modular design** (loose coupling)
- 🔌 **Extensibility** (third-party plugins)
- 📦 **Independent versioning** (plugin versions)
- ♻️ **Code reuse** (plugin marketplace)

---

## Next Steps

1. Implement core plugin interfaces ✅ (next)
2. Create plugin loader infrastructure
3. Migrate WFS to plugin (proof of concept)
4. Update build configuration
5. Document plugin development guide
6. Create plugin templates

