# Configuration V2 + Plugin System Integration

**Date**: 2025-11-11

This document explains how Configuration V2 and the Plugin Architecture work together to provide a powerful, flexible, and performant service configuration system.

---

## Overview

The integration creates a two-layer system:

1. **Configuration V2 (Declarative Layer)** - Declares WHAT services should run and HOW they should behave
2. **Plugin System (Implementation Layer)** - Provides the HOW (actual service implementations)

Together they enable:
- **Fast builds** - Only compile the plugins you're working on
- **Custom deployments** - Only include the plugins you need
- **Single configuration** - One `.honua` file controls everything
- **Dynamic loading** - Services load/unload based on configuration

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     honua.config.hcl                        │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ service "wfs" {                                        │ │
│  │   enabled = true                                       │ │
│  │   version = "2.0.0"                                    │ │
│  │   max_features = 10000                                 │ │
│  │ }                                                      │ │
│  │                                                        │ │
│  │ service "odata" { enabled = false }                    │ │
│  └────────────────────────────────────────────────────────┘ │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ↓ Loaded at startup
                        │
        ┌───────────────┴────────────────┐
        │   Configuration V2 Loader      │
        │   - Parses .honua file         │
        │   - Validates configuration    │
        │   - Registers HonuaConfig      │
        └───────────────┬────────────────┘
                        │
                        ↓ Determines which services to load
                        │
        ┌───────────────┴────────────────┐
        │      Plugin Loader             │
        │   - Discovers plugins          │
        │   - Filters by enabled         │
        │   - Loads assemblies           │
        │   - Initializes plugins        │
        └───────────────┬────────────────┘
                        │
            ┌───────────┴───────────┐
            │                       │
    ┌───────▼──────┐        ┌──────▼──────┐
    │ WFS Plugin   │        │ OData Plugin│
    │  LOADED ✅   │        │  SKIPPED ❌ │
    └───────┬──────┘        └─────────────┘
            │
            ↓ Reads configuration
            │
    ┌───────▼──────────────────┐
    │ WfsServicePlugin         │
    │  ConfigureServices()     │
    │  - Reads max_features    │
    │  - Registers WFS service │
    │  MapEndpoints()          │
    │  - Maps /wfs routes      │
    └──────────────────────────┘
```

---

## Integration Points

### 1. Service Discovery and Loading

**Configuration V2 declares services:**
```hcl
# honua.config.hcl
service "wfs" {
  enabled = true
  version = "2.0.0"
}

service "wms" {
  enabled = true
  version = "1.3.0"
}

service "odata" {
  enabled = false
}
```

**Plugin Loader uses Configuration V2:**
```csharp
// In ConfigurationV2Extensions.cs
public static IServiceCollection AddHonuaConfigurationV2(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    // 1. Load Configuration V2
    var honuaConfig = LoadHonuaConfig(configuration, environment);
    if (honuaConfig == null)
    {
        return services; // Fall back to legacy
    }

    services.AddSingleton(honuaConfig);

    // 2. Initialize plugin loader
    var pluginLoader = new PluginLoader(
        services.BuildServiceProvider().GetRequiredService<ILogger<PluginLoader>>(),
        configuration,
        environment);

    // 3. Determine which services should be loaded
    var enabledServices = honuaConfig.Services
        .Where(s => s.Value.Enabled)
        .Select(s => s.Key)
        .ToHashSet();

    // 4. Load only enabled plugins
    var loadResult = await pluginLoader.LoadPluginsAsync(
        serviceFilter: serviceId => enabledServices.Contains(serviceId));

    // 5. Register loaded plugins
    services.AddSingleton(pluginLoader);

    // 6. Let each plugin configure its services
    foreach (var loadedPlugin in pluginLoader.GetServicePlugins())
    {
        var pluginContext = new PluginContext
        {
            Configuration = configuration,
            Environment = environment,
            LoadedPlugins = loadResult.LoadedPlugins.ToDictionary(p => p.Id)
        };

        loadedPlugin.ConfigureServices(services, configuration, pluginContext);
    }

    return services;
}
```

### 2. Service Configuration

**Configuration V2 provides settings:**
```hcl
service "wfs" {
  enabled = true
  version = "2.0.0"
  max_features = 10000
  default_count = 100

  output_formats = ["gml", "geojson", "csv"]

  transactions {
    enabled = true
    max_batch_size = 100
  }
}
```

**Plugin reads Configuration V2:**
```csharp
public class WfsServicePlugin : IServicePlugin
{
    public string ServiceId => "wfs";

    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        // Read service configuration from Configuration V2
        var serviceConfig = configuration
            .GetSection($"honua:services:{ServiceId}");

        // Get settings with defaults
        var maxFeatures = serviceConfig.GetValue("max_features", 10000);
        var defaultCount = serviceConfig.GetValue("default_count", 100);
        var version = serviceConfig.GetValue("version", "2.0.0");

        // Configure WFS service with these settings
        services.AddWfsService(options =>
        {
            options.MaxFeatures = maxFeatures;
            options.DefaultCount = defaultCount;
            options.Version = version;

            // Read nested configuration
            var transactionsConfig = serviceConfig.GetSection("transactions");
            if (transactionsConfig.GetValue("enabled", false))
            {
                options.EnableTransactions = true;
                options.MaxBatchSize = transactionsConfig.GetValue("max_batch_size", 100);
            }
        });

        context.Logger.LogInformation(
            "Configured WFS service: version={Version}, maxFeatures={MaxFeatures}",
            version, maxFeatures);
    }
}
```

### 3. Endpoint Mapping

**Configuration V2 enables services:**
```hcl
service "wfs" {
  enabled = true
  base_path = "/wfs"  # Optional: override default path
}
```

**Plugin maps endpoints conditionally:**
```csharp
public class WfsServicePlugin : IServicePlugin
{
    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        // Plugin only called if service is enabled in Configuration V2

        var serviceConfig = context.Configuration
            .GetSection($"honua:services:{ServiceId}");

        var basePath = serviceConfig.GetValue("base_path", "/wfs");

        // Map WFS endpoints
        var wfsGroup = endpoints.MapGroup(basePath)
            .WithTags("WFS");

        wfsGroup.MapGet("", HandleWfsRequest);
        wfsGroup.MapPost("", HandleWfsRequest);

        context.Logger.LogInformation(
            "Mapped WFS endpoints at {BasePath}", basePath);
    }
}
```

### 4. Layer Configuration

**Configuration V2 declares layers:**
```hcl
data_source "gis_db" {
  provider = "postgresql"
  connection = env("DATABASE_URL")
}

layer "roads" {
  title = "Road Network"
  data_source = data_source.gis_db
  table = "roads"

  geometry {
    column = "geom"
    type = "LineString"
    srid = 4326
  }

  services = [
    service.wfs,
    service.wms,
    service.ogc_api
  ]
}
```

**Plugin accesses layer configuration:**
```csharp
public class WfsServicePlugin : IServicePlugin
{
    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        // Get HonuaConfig to access layers
        var honuaConfig = configuration
            .GetSection("honua")
            .Get<HonuaConfig>();

        // Find layers that include this service
        var wfsLayers = honuaConfig.Layers
            .Where(layer => layer.Value.Services.Contains($"service.{ServiceId}"))
            .ToList();

        context.Logger.LogInformation(
            "Found {Count} layers for WFS service", wfsLayers.Count);

        // Register each layer with WFS service
        services.Configure<WfsOptions>(options =>
        {
            foreach (var (layerId, layerConfig) in wfsLayers)
            {
                options.AddLayer(new WfsLayerConfig
                {
                    Id = layerId,
                    Title = layerConfig.Title,
                    DataSource = layerConfig.DataSource,
                    Table = layerConfig.Table,
                    GeometryColumn = layerConfig.Geometry.Column,
                    GeometryType = layerConfig.Geometry.Type,
                    Srid = layerConfig.Geometry.Srid
                });
            }
        });
    }
}
```

### 5. Validation

**Plugin validates Configuration V2 before loading:**
```csharp
public class WfsServicePlugin : IServicePlugin
{
    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();

        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        // Check if service is configured
        if (!serviceConfig.Exists())
        {
            result.AddError("WFS service not configured in Configuration V2");
            return result;
        }

        // Validate version
        var version = serviceConfig.GetValue<string>("version");
        if (!IsValidWfsVersion(version))
        {
            result.AddError($"Invalid WFS version: {version}. Must be 1.0.0, 1.1.0, or 2.0.0");
        }

        // Validate max_features
        var maxFeatures = serviceConfig.GetValue<int>("max_features", 10000);
        if (maxFeatures <= 0 || maxFeatures > 100000)
        {
            result.AddWarning($"max_features={maxFeatures} is outside recommended range (1-100000)");
        }

        // Check for layers
        var honuaConfig = configuration.GetSection("honua").Get<HonuaConfig>();
        var wfsLayers = honuaConfig?.Layers
            .Where(layer => layer.Value.Services.Contains($"service.{ServiceId}"))
            .ToList();

        if (wfsLayers == null || wfsLayers.Count == 0)
        {
            result.AddWarning("No layers configured for WFS service");
        }

        return result;
    }
}
```

---

## Startup Sequence

Here's the complete startup flow showing how Configuration V2 and plugins integrate:

```
1. Program.cs starts
   │
   ├─ builder.Services.AddHonuaConfigurationV2(...)
   │  │
   │  ├─ Load Configuration V2 (.honua file)
   │  │  └─ Parse HCL → HonuaConfig object
   │  │
   │  ├─ Register HonuaConfig as singleton
   │  │
   │  ├─ Initialize PluginLoader
   │  │
   │  ├─ Determine enabled services from HonuaConfig
   │  │  └─ enabledServices = ["wfs", "wms", "ogc_api"]
   │  │
   │  ├─ Load plugins
   │  │  │
   │  │  ├─ Discover plugins in ./plugins/
   │  │  ├─ Filter to only enabled services
   │  │  ├─ Load assemblies via AssemblyLoadContext
   │  │  └─ Instantiate plugin classes
   │  │
   │  └─ For each loaded plugin:
   │     │
   │     ├─ Call plugin.ValidateConfiguration(configuration)
   │     │  └─ Plugin checks its Configuration V2 settings
   │     │
   │     └─ Call plugin.ConfigureServices(services, configuration, context)
   │        └─ Plugin registers its DI services using Configuration V2 settings
   │
   ├─ builder.ConfigureHonuaServices() [Legacy - still works]
   │
   └─ var app = builder.Build()
      │
      ├─ app.MapConditionalServiceEndpoints() [Legacy - still works]
      │
      └─ app.MapHonuaConfigurationV2Endpoints()
         │
         └─ For each loaded plugin:
            │
            └─ Call plugin.MapEndpoints(endpoints, context)
               └─ Plugin maps its HTTP endpoints
```

---

## Configuration Precedence

When both Configuration V2 and legacy configuration exist:

```
Configuration V2 > Legacy Configuration
```

**Example:**
```csharp
// Legacy configuration (appsettings.json)
{
  "Wfs": {
    "Enabled": true,
    "MaxFeatures": 5000  // ← Will be IGNORED if Configuration V2 exists
  }
}
```

```hcl
# Configuration V2 (honua.config.hcl)
service "wfs" {
  enabled = true
  max_features = 10000  # ← WINS (Configuration V2 takes precedence)
}
```

**Result**: WFS service uses `max_features = 10000` from Configuration V2.

---

## Development Workflow

### Scenario: Adding a New Service Setting

**1. Update Configuration V2 Schema:**
```hcl
# Add new setting to honua.config.hcl
service "wfs" {
  enabled = true
  max_features = 10000

  # New setting
  enable_stored_queries = true  # ← New feature
}
```

**2. Update Plugin:**
```csharp
// In WfsServicePlugin.cs
public void ConfigureServices(...)
{
    var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

    // Read new setting
    var enableStoredQueries = serviceConfig.GetValue("enable_stored_queries", false);

    services.AddWfsService(options =>
    {
        options.EnableStoredQueries = enableStoredQueries;
    });
}
```

**3. Rebuild Plugin:**
```bash
# Only rebuild the WFS plugin (2-5 seconds)
dotnet build src/plugins/Honua.Server.Services.Wfs
```

**4. Restart Server:**
```bash
dotnet run --project src/Honua.Server.Host
# Plugin automatically reloaded with new setting
```

### Scenario: Disabling a Service

**1. Update Configuration V2:**
```hcl
service "odata" {
  enabled = false  # ← Disable OData
}
```

**2. Restart Server:**
```bash
dotnet run --project src/Honua.Server.Host
# OData plugin NOT loaded
# Endpoints NOT mapped
# Memory NOT allocated
```

No code changes required!

### Scenario: Custom Deployment

**1. Create production configuration:**
```hcl
# production.honua
honua { version = "1.0" environment = "production" }

# Only enable what you need
service "ogc_api" { enabled = true }
service "wfs" { enabled = true }
# Everything else implicitly disabled
```

**2. Build deployment:**
```bash
# Include only needed plugins in Docker image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Copy core
COPY bin/Release/net9.0/publish/ .

# Copy ONLY needed plugins
COPY plugins/ogc-api/ ./plugins/ogc-api/
COPY plugins/wfs/ ./plugins/wfs/
# Don't copy odata, wms, wmts, etc.

COPY production.honua honua.config.hcl

ENTRYPOINT ["dotnet", "Honua.Server.Host.dll"]
```

**Result:**
- 50% smaller image (80 MB vs 150 MB)
- Faster startup
- Reduced attack surface

---

## Benefits Summary

### 1. Single Source of Truth
- One `.honua` file controls everything
- No duplicate configuration
- No sync issues

### 2. Fast Development
- Compile only what you're working on (2-5 sec vs 30-60 sec)
- Hot reload support
- Immediate feedback

### 3. Custom Deployments
- Include only needed services
- Smaller Docker images (50% reduction)
- Reduced attack surface

### 4. Flexible Architecture
- Add new services without changing core
- Third-party plugins possible
- Gradual migration path

### 5. Validation at Multiple Levels
- Configuration V2 validates .honua file
- Plugin validates its specific settings
- Runtime validates actual operation

---

## Example: Complete Integration

Here's a complete example showing Configuration V2 + Plugin integration for WFS:

**honua.config.hcl:**
```hcl
honua { version = "1.0" }

data_source "gis_db" {
  provider = "postgresql"
  connection = env("DATABASE_URL")
}

service "wfs" {
  enabled = true
  version = "2.0.0"
  max_features = 10000
  default_count = 100

  transactions {
    enabled = true
    max_batch_size = 50
  }
}

layer "roads" {
  title = "Road Network"
  data_source = data_source.gis_db
  table = "roads"

  geometry {
    column = "geom"
    type = "LineString"
    srid = 4326
  }

  services = [service.wfs]
}
```

**plugins/wfs/plugin.json:**
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
  "dependencies": [],
  "minimumHonuaVersion": "1.0.0"
}
```

**WfsServicePlugin.cs:**
```csharp
namespace Honua.Server.Services.Wfs;

public class WfsServicePlugin : IServicePlugin
{
    public string Id => "honua.services.wfs";
    public string Name => "WFS Service Plugin";
    public string Version => "1.0.0";
    public string Description => "OGC Web Feature Service implementation";
    public string Author => "HonuaIO";
    public string ServiceId => "wfs";
    public ServiceType ServiceType => ServiceType.OGC;
    public IReadOnlyList<PluginDependency> Dependencies => Array.Empty<PluginDependency>();
    public string MinimumHonuaVersion => "1.0.0";

    public Task OnLoadAsync(PluginContext context)
    {
        context.Logger.LogInformation("WFS plugin loading...");
        return Task.CompletedTask;
    }

    public void ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration,
        PluginContext context)
    {
        // Read Configuration V2 settings
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");
        var honuaConfig = configuration.GetSection("honua").Get<HonuaConfig>();

        var version = serviceConfig.GetValue("version", "2.0.0");
        var maxFeatures = serviceConfig.GetValue("max_features", 10000);
        var defaultCount = serviceConfig.GetValue("default_count", 100);

        var transactionsEnabled = serviceConfig.GetValue("transactions:enabled", false);
        var maxBatchSize = serviceConfig.GetValue("transactions:max_batch_size", 100);

        // Get layers for this service
        var wfsLayers = honuaConfig.Layers
            .Where(l => l.Value.Services.Contains($"service.{ServiceId}"))
            .ToList();

        // Register WFS service
        services.AddWfsService(options =>
        {
            options.Version = version;
            options.MaxFeatures = maxFeatures;
            options.DefaultCount = defaultCount;
            options.EnableTransactions = transactionsEnabled;
            options.MaxBatchSize = maxBatchSize;

            // Register layers
            foreach (var (layerId, layer) in wfsLayers)
            {
                options.AddLayer(new WfsLayerConfig
                {
                    Id = layerId,
                    Title = layer.Title,
                    DataSource = layer.DataSource,
                    Table = layer.Table,
                    GeometryColumn = layer.Geometry.Column,
                    GeometryType = layer.Geometry.Type,
                    Srid = layer.Geometry.Srid
                });
            }
        });

        context.Logger.LogInformation(
            "Configured WFS service: version={Version}, layers={LayerCount}",
            version, wfsLayers.Count);
    }

    public void MapEndpoints(
        IEndpointRouteBuilder endpoints,
        PluginContext context)
    {
        var serviceConfig = context.Configuration.GetSection($"honua:services:{ServiceId}");
        var basePath = serviceConfig.GetValue("base_path", "/wfs");

        var wfsGroup = endpoints.MapGroup(basePath)
            .WithTags("WFS", "OGC");

        wfsGroup.MapGet("", HandleWfsRequest);
        wfsGroup.MapPost("", HandleWfsRequest);

        context.Logger.LogInformation("Mapped WFS endpoints at {BasePath}", basePath);
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        var result = new PluginValidationResult();
        var serviceConfig = configuration.GetSection($"honua:services:{ServiceId}");

        if (!serviceConfig.Exists())
        {
            result.AddError("WFS service not configured");
            return result;
        }

        var version = serviceConfig.GetValue<string>("version");
        if (!new[] { "1.0.0", "1.1.0", "2.0.0" }.Contains(version))
        {
            result.AddError($"Invalid WFS version: {version}");
        }

        return result;
    }

    public Task OnUnloadAsync()
    {
        return Task.CompletedTask;
    }

    private static Task<IResult> HandleWfsRequest(HttpContext context)
    {
        // WFS implementation...
        return Task.FromResult(Results.Ok("WFS Response"));
    }
}
```

**Result:**
- Configuration V2 declares WFS is enabled with specific settings
- Plugin system loads WFS plugin
- Plugin reads Configuration V2 for its settings
- Plugin registers services and maps endpoints
- Server runs with WFS available at `/wfs`

---

## Conclusion

Configuration V2 and the Plugin System are designed to work together seamlessly:

- **Configuration V2** = Declarative "what" and "settings"
- **Plugin System** = Dynamic "how" and "implementation"

Together they provide:
1. Fast development (compile only what you change)
2. Flexible deployment (include only what you need)
3. Single source of truth (one .honua file)
4. Clean architecture (services decoupled from core)

This is a powerful foundation for Honua Server's modular architecture.
