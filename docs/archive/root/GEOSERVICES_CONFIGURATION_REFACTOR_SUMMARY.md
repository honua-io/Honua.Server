# GeoservicesREST Configuration Refactor - Summary

## Issue
MEDIUM SEVERITY - Incomplete configuration refactor for GeoservicesREST API

## Problems Fixed

1. **Hardcoded request limits** - Constants in `GeoservicesParameterResolver.cs` 
2. **Missing configuration section** - No GeoservicesREST settings in `HonuaConfiguration`
3. **Unused service** - `GeoservicesQueryService` was created but not injected into controller
4. **Manual query handling** - Controller duplicated logic instead of using service
5. **Stale backup file** - `.cs.bak` file left in repository

## Changes Made

### 1. Configuration Schema (/src/Honua.Server.Core/Configuration/HonuaConfiguration.cs)

Added `GeoservicesRESTConfiguration` class:
```csharp
public sealed class GeoservicesRESTConfiguration
{
    public bool Enabled { get; init; } = true;
    public int DefaultMaxRecordCount { get; init; } = 1000;
    public int MaxRecordCount { get; init; } = 10000;
    public string DefaultFormat { get; init; } = "json";
    public bool EnableTelemetry { get; init; } = true;
}
```

Added to `ServicesConfiguration`:
```csharp
public GeoservicesRESTConfiguration GeoservicesREST { get; init; } = GeoservicesRESTConfiguration.Default;
```

### 2. Configuration Validation (/src/Honua.Server.Core/Configuration/HonuaConfigurationValidator.cs)

Added validation rules:
- DefaultMaxRecordCount must be > 0
- MaxRecordCount must be > 0
- DefaultMaxRecordCount cannot exceed MaxRecordCount
- MaxRecordCount capped at 100,000 (DoS prevention)
- DefaultFormat cannot be empty

### 3. Parameter Resolver (/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesParameterResolver.cs)

Updated methods to accept optional configuration:
```csharp
public static (GeoservicesResponseFormat Format, bool PrettyPrint) ResolveFormat(
    IQueryCollection query,
    GeoservicesRESTConfiguration? config = null)

public static int ResolveLimit(
    IQueryCollection query,
    CatalogServiceView serviceView,
    CatalogLayerView layerView,
    GeoservicesRESTConfiguration? config = null)
```

Fallback constants renamed to `Fallback*` prefix for clarity.

### 4. Controller Refactor (/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs)

**Added dependency injection:**
```csharp
private readonly IGeoservicesQueryService _queryService;

public GeoservicesRESTFeatureServerController(
    // ... existing dependencies
    IGeoservicesQueryService queryService,
    // ... logger
)
```

**Simplified QueryAsync method:**
- Delegates JSON format queries to `_queryService.ExecuteQueryAsync()`
- Keeps export formats (Shapefile, CSV, KML, GeoJSON, TopoJSON) in controller
- Removes duplicate query logic (statistics, distinct, IDs, extent, count)

### 5. Application Settings (/src/Honua.Server.Host/appsettings.json)

Added configuration section:
```json
{
  "honua": {
    "services": {
      "geoservicesREST": {
        "enabled": true,
        "defaultMaxRecordCount": 1000,
        "maxRecordCount": 10000,
        "defaultFormat": "json",
        "enableTelemetry": true
      }
    }
  }
}
```

### 6. Cleanup

Deleted stale file:
- `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs.bak`

## Dependency Injection Status

✅ **Already Registered** - `IGeoservicesQueryService` was already registered in DI container:
```csharp
// /src/Honua.Server.Host/Extensions/ServiceCollectionExtensions.cs:377
services.AddScoped<IGeoservicesQueryService, GeoservicesQueryService>();
```

## Telemetry

✅ **Properly Wired** - Telemetry is already implemented in `GeoservicesQueryService`:
```csharp
using var activity = HonuaTelemetry.OgcProtocols.StartActivity("GeoservicesQuery");
```

## Build Status

✅ Build succeeded with 0 warnings, 0 errors

## Security Improvements

- Request limits now configurable (prevents DoS via unbounded queries)
- Validation prevents misconfiguration (max > 100k triggers warning)
- Centralized configuration makes security audits easier

## Migration Notes

**For existing deployments:**
1. Configuration is backward compatible - uses sensible defaults
2. Existing hardcoded limits (1000/10000) preserved as defaults
3. No breaking changes to API endpoints or behavior

**To customize limits:**
```json
{
  "honua": {
    "services": {
      "geoservicesREST": {
        "defaultMaxRecordCount": 500,
        "maxRecordCount": 5000
      }
    }
  }
}
```

## Files Modified

1. `/src/Honua.Server.Core/Configuration/HonuaConfiguration.cs`
2. `/src/Honua.Server.Core/Configuration/HonuaConfigurationValidator.cs`
3. `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesParameterResolver.cs`
4. `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs`
5. `/src/Honua.Server.Host/appsettings.json`

## Files Deleted

1. `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs.bak`

## Verification

- [x] Configuration classes added
- [x] Validation rules implemented
- [x] Parameter resolver updated
- [x] Controller refactored to use service
- [x] appsettings.json updated
- [x] DI registration verified
- [x] Telemetry wiring verified
- [x] Build successful
- [x] Stale files cleaned up
