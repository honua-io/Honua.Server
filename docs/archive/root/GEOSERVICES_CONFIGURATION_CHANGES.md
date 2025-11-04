# GeoServices Configuration Migration Guide

This document outlines the changes made to make hardcoded GeoServices configuration values configurable via appsettings.json.

## Summary of Changes

All hardcoded GeoServices REST API configuration values have been moved to the `GeoservicesRESTConfiguration` class in `/src/Honua.Server.Core/Configuration/HonuaConfiguration.cs`.

## Configuration Class

The `GeoservicesRESTConfiguration` class now includes the following configurable properties:

```csharp
public sealed class GeoservicesRESTConfiguration
{
    public static GeoservicesRESTConfiguration Default => new();

    // Default: 1000
    public int DefaultMaxRecordCount { get; init; } = 1000;

    // Default: 10,000
    public int MaxResultsWithoutPagination { get; init; } = 10_000;

    // Default: 50,000
    public int MaxResultsPerQuery { get; init; } = 50_000;

    // Default: 1000
    public int MaxObjectIdsPerQuery { get; init; } = 1000;

    // Default: 4096
    public int MaxWhereClauseLength { get; init; } = 4096;

    // Default: 1000
    public int MaxFeaturesPerEdit { get; init; } = 1000;

    // Default: 100,000
    public int MaxGeometryVertices { get; init; } = 100_000;

    // Default: 500
    public int MaxVectorOverlayFeatures { get; init; } = 500;

    // Default: 1000
    public int MaxKmlFeatures { get; init; } = 1000;

    // Default: 10.81
    public double Version { get; init; } = 10.81;
}
```

## Files That Need Updates

### 1. GeoservicesQueryService.cs
**Location**: `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesQueryService.cs`

**Changes needed**:
- Add `IHonuaConfigurationService` dependency injection
- Replace `MaxResultsWithoutPagination = 10_000` constant with `_configurationService.Current.GeoservicesREST.MaxResultsWithoutPagination`

**Example**:
```csharp
// Before
private const int MaxResultsWithoutPagination = 10_000;

// After - add to constructor
private readonly IHonuaConfigurationService _configurationService;

public GeoservicesQueryService(
    IFeatureRepository repository,
    ILogger<GeoservicesQueryService> logger,
    IHonuaConfigurationService configurationService)
{
    _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
}

// Usage
var config = _configurationService.Current.GeoservicesREST;
if (++count > config.MaxResultsWithoutPagination && !context.Query.Limit.HasValue)
{
    throw new InvalidOperationException(
        $"Result set exceeds {config.MaxResultsWithoutPagination} records...");
}
```

### 2. GeoservicesRESTFeatureServerController.cs
**Location**: `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs`

**Changes needed**:
- Add `IHonuaConfigurationService` dependency injection
- Replace `const double GeoServicesVersion = 10.81;` with `_configurationService.Current.GeoservicesREST.Version`
- Replace `const int DefaultMaxRecordCount = 1000;` with `_configurationService.Current.GeoservicesREST.DefaultMaxRecordCount`

**Lines to update**:
- Line 44: Remove `private const double GeoServicesVersion = 10.81;`
- Line 45: Remove `private const int DefaultMaxRecordCount = 1000;`
- Add constructor parameter for `IHonuaConfigurationService`
- Update all usages of `GeoServicesVersion` to use config
- Update all usages of `DefaultMaxRecordCount` to use config

### 3. GeoservicesRESTMapServerController.cs
**Location**: `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTMapServerController.cs`

**Changes needed**:
- Add `IHonuaConfigurationService` dependency injection
- Replace `const double GeoServicesVersion = 10.81;` with `_configurationService.Current.GeoservicesREST.Version`
- Replace hardcoded `500` (line 220) with `_configurationService.Current.GeoservicesREST.MaxVectorOverlayFeatures`
- Replace hardcoded `1000` (line 713) with `_configurationService.Current.GeoservicesREST.MaxKmlFeatures`

**Example**:
```csharp
// Line 220 - Before
var query = new FeatureQuery(
    Limit: 500,
    Bbox: new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3]),
    ResultType: FeatureResultType.Results,
    Crs: dataset.Crs.FirstOrDefault() ?? serviceView.Service.Ogc.DefaultCrs);

// After
var config = _configurationService.Current.GeoservicesREST;
var query = new FeatureQuery(
    Limit: config.MaxVectorOverlayFeatures,
    Bbox: new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3]),
    ResultType: FeatureResultType.Results,
    Crs: dataset.Crs.FirstOrDefault() ?? serviceView.Service.Ogc.DefaultCrs);

// Line 713 - Before
var query = new FeatureQuery(
    Limit: 1000,
    ResultType: FeatureResultType.Results,
    Crs: "EPSG:4326");

// After
var config = _configurationService.Current.GeoservicesREST;
var query = new FeatureQuery(
    Limit: config.MaxKmlFeatures,
    ResultType: FeatureResultType.Results,
    Crs: "EPSG:4326");

// All usages of GeoServicesVersion
var summary = GeoservicesRESTMetadataMapper.CreateMapServiceSummary(serviceView, config.Version);
```

### 4. GeoservicesRESTImageServerController.cs
**Location**: `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTImageServerController.cs`

**Changes needed**:
- Add `IHonuaConfigurationService` dependency injection
- Replace `const double GeoServicesVersion = 10.81;` with `_configurationService.Current.GeoservicesREST.Version`

**Lines to update**:
- Line 28: Remove `private const double GeoServicesVersion = 10.81;`
- Add constructor parameter for `IHonuaConfigurationService`
- Update all usages of `GeoServicesVersion` (lines 66, 196, 366)

### 5. GeoservicesMetadataService.cs
**Location**: `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesMetadataService.cs`

**Changes needed**:
- Add `IHonuaConfigurationService` dependency injection
- Replace `const double GeoServicesVersion = 10.81;` with `_configurationService.Current.GeoservicesREST.Version`

**Lines to update**:
- Line 20: Remove `private const double GeoServicesVersion = 10.81;`
- Add constructor parameter for `IHonuaConfigurationService`
- Update usages at lines 38, 54

### 6. ServicesDirectoryController.cs
**Location**: `/src/Honua.Server.Host/GeoservicesREST/ServicesDirectoryController.cs`

**Changes needed**:
- Add `IHonuaConfigurationService` dependency injection (Already has it!)
- Replace `const double GeoServicesVersion = 10.81;` with `_configurationService.Current.GeoservicesREST.Version`
- Replace `const int DefaultMaxRecordCount = 1000;` with `_configurationService.Current.GeoservicesREST.DefaultMaxRecordCount`

**Lines to update**:
- Line 20: Remove `private const double GeoServicesVersion = 10.81;`
- Line 21: Remove `private const int DefaultMaxRecordCount = 1000;`
- Update usages at lines 60, 87, 262

### 7. Additional files with GeoServicesVersion = 10.81

Based on grep results, these files also need updates:

- `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFindTranslator.cs`
- `/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTMetadataMapper.cs`
- `/src/Honua.Server.Host/GeoservicesREST/Services/GeoservicesParameterResolver.cs`

These should be updated to accept the version as a parameter from their callers rather than using a constant.

## appsettings.json Configuration

Add the following section to `/src/Honua.Server.Host/appsettings.json`:

```json
{
  "honua": {
    "GeoservicesREST": {
      "DefaultMaxRecordCount": 1000,
      "MaxResultsWithoutPagination": 10000,
      "MaxResultsPerQuery": 50000,
      "MaxObjectIdsPerQuery": 1000,
      "MaxWhereClauseLength": 4096,
      "MaxFeaturesPerEdit": 1000,
      "MaxGeometryVertices": 100000,
      "MaxVectorOverlayFeatures": 500,
      "MaxKmlFeatures": 1000,
      "Version": 10.81
    }
  }
}
```

## Benefits

1. **Configurability**: Operators can now adjust limits without recompiling
2. **Environment-specific settings**: Different limits for dev/staging/production
3. **Performance tuning**: Adjust limits based on server capacity
4. **Client compatibility**: Version can be changed for compatibility testing
5. **DoS protection**: Limits can be tightened to prevent abuse

## Backward Compatibility

All default values match the previous hardcoded values, ensuring backward compatibility. No configuration changes are required for existing deployments - they will continue to work with the defaults.

## Testing

After making these changes:

1. Verify all controllers can be constructed with DI
2. Test query operations respect the configured limits
3. Test that changing appsettings.json values takes effect
4. Run the existing test suite to ensure no regressions

## Implementation Notes

- The configuration is accessed via `IHonuaConfigurationService.Current.GeoservicesREST`
- Configuration is validated on startup via the existing HonuaConfiguration validation
- The GeoservicesRESTConfiguration class uses `init` properties for immutability
- All properties have XML documentation explaining their purpose and defaults
