# Migration Guide

This guide helps you upgrade between versions of Honua.MapSDK, covering breaking changes, deprecations, and new features.

## Table of Contents

1. [Upgrading from Pre-1.0](#upgrading-from-pre-10)
2. [Version History](#version-history)
3. [Breaking Changes](#breaking-changes)
4. [Deprecation Notices](#deprecation-notices)
5. [Feature Additions](#feature-additions)

## Current Version

**Latest Stable:** 1.0.0

## Upgrading from Pre-1.0

If you're upgrading from an early development version, follow these steps:

### 1. Update Package Reference

```bash
dotnet add package Honua.MapSDK --version 1.0.0
```

### 2. Update Service Registration

**Before:**
```csharp
services.AddScoped<ComponentBus>();
services.AddScoped<DataLoader>();
// ... manual registrations
```

**After:**
```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    // Optional configuration
    options.Cache.MaxSizeMB = 100;
});
```

### 3. Update Component Inheritance

**Before:**
```csharp
public class MyComponent : ComponentBase, IDisposable
{
    private IDisposable? _subscription;

    protected override void OnInitialized()
    {
        _subscription = Bus.Subscribe<TMessage>(HandleMessage);
    }

    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
```

**After:**
```csharp
public class MyComponent : DisposableComponentBase
{
    protected override void OnInitialized()
    {
        SubscribeToMessage<TMessage>(HandleMessage);
        // Automatic cleanup on disposal
    }
}
```

### 4. Update Message Types

Some message classes have been renamed for consistency:

| Old Name | New Name |
|----------|----------|
| `MapMovedMessage` | `MapExtentChangedMessage` |
| `FeatureClickMessage` | `FeatureClickedMessage` |
| `FilterChangedMessage` | `FilterAppliedMessage` |
| `DataLoadMessage` | `DataLoadedMessage` |

**Before:**
```csharp
Bus.Subscribe<MapMovedMessage>(HandleMapMove);
```

**After:**
```csharp
Bus.Subscribe<MapExtentChangedMessage>(HandleExtentChange);
```

### 5. Update Configuration API

Configuration has moved to a unified options pattern:

**Before:**
```csharp
services.Configure<CacheOptions>(options =>
{
    options.MaxSizeMB = 100;
});

services.Configure<RenderingOptions>(options =>
{
    options.VirtualScrollThreshold = 1000;
});
```

**After:**
```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.Cache.MaxSizeMB = 100;
    options.Rendering.VirtualScrollThreshold = 1000;
});
```

### 6. Update Import Statements

Some namespaces have been reorganized:

**Before:**
```csharp
using Honua.MapSDK.Core;
using Honua.MapSDK.Components;
using Honua.MapSDK.Messages;
```

**After:**
```csharp
using Honua.MapSDK;
using Honua.MapSDK.Components;
using Honua.MapSDK.Core;
using Honua.MapSDK.Core.Messages;
```

## Version History

### Version 1.0.0 (Current)

**Release Date:** 2025-01-XX

**Major Features:**
- 20+ production-ready components
- ComponentBus messaging architecture
- Comprehensive caching and performance optimizations
- Full .NET 9 support
- MudBlazor integration
- Extensive documentation

**Components Added:**
- All core components
- Search & navigation components
- Drawing & editing components
- Layer management components
- Data management components
- Visualization components

## Breaking Changes

### v1.0.0

#### 1. Service Registration

**Breaking:** Manual service registration no longer supported.

**Migration:**
```csharp
// Old (no longer supported)
services.AddScoped<ComponentBus>();

// New (required)
builder.Services.AddHonuaMapSDK();
```

#### 2. Message Type Names

**Breaking:** Several message types renamed.

**Migration:** Search and replace:
- `MapMovedMessage` → `MapExtentChangedMessage`
- `FeatureClickMessage` → `FeatureClickedMessage`
- `FilterChangedMessage` → `FilterAppliedMessage`
- `DataLoadMessage` → `DataLoadedMessage`

#### 3. Component Base Classes

**Breaking:** Custom components should inherit from `DisposableComponentBase`.

**Migration:**
```csharp
// Old
public class MyComponent : ComponentBase, IDisposable
{
    public void Dispose() { /* manual cleanup */ }
}

// New
public class MyComponent : DisposableComponentBase
{
    protected override void OnDispose() { /* optional cleanup */ }
}
```

#### 4. Configuration Options

**Breaking:** Separate configuration options consolidated.

**Migration:**
```csharp
// Old
services.Configure<CacheOptions>(...);
services.Configure<RenderingOptions>(...);

// New
builder.Services.AddHonuaMapSDK(options =>
{
    options.Cache. ...
    options.Rendering. ...
});
```

## Deprecation Notices

### Current Deprecations

No current deprecations in v1.0.0.

### Future Deprecations

The following features are planned for deprecation in future versions:

1. **ComponentBus.Publish (sync)** - Use `PublishAsync` instead
   - Deprecated in: v2.0.0 (planned)
   - Removed in: v3.0.0 (planned)
   - Migration: Replace all `Publish()` calls with `await PublishAsync()`

## Feature Additions

### v1.0.0

#### New Components
- `HonuaMap` - Core map component
- `HonuaDataGrid` - Data grid with map sync
- `HonuaChart` - Interactive charts
- `HonuaLegend` - Dynamic legend
- `HonuaFilterPanel` - Advanced filtering
- `HonuaSearch` - Geocoding search
- `HonuaBookmarks` - Saved views
- `HonuaCoordinateDisplay` - Coordinate tracking
- `HonuaTimeline` - Temporal visualization
- `HonuaDraw` - Drawing tools
- `HonuaEditor` - Feature editing
- `HonuaBasemapGallery` - Basemap selector
- `HonuaLayerList` - Layer TOC
- `HonuaPopup` - Feature popups
- `HonuaImportWizard` - Data import
- `HonuaAttributeTable` - Attribute editing
- `HonuaOverviewMap` - Minimap
- `HonuaHeatmap` - Density visualization
- `HonuaElevationProfile` - Terrain profiles
- `HonuaCompare` - Map comparison
- `HonuaPrint` - PDF export

#### New Services
- `DataLoader` - HTTP data loading with caching
- `StreamingLoader` - Large dataset streaming
- `DataCache` - LRU cache
- `PerformanceMonitor` - Performance tracking
- `KeyboardShortcuts` - Keyboard navigation
- `MapSdkLogger` - Structured logging

#### New Utilities
- `GeometryUtils` - Spatial calculations
- `ColorUtils` - Color manipulation
- `TimeUtils` - Temporal utilities
- `DataTransform` - Data format conversion
- `ValidationUtils` - Input validation
- `ResponsiveHelper` - Responsive design

#### New Testing Tools
- `MapSdkTestContext` - bUnit test context
- `MockDataGenerator` - Test data generation

## Migration Checklist

When upgrading to v1.0.0, complete these steps:

- [ ] Update package reference to 1.0.0
- [ ] Replace manual service registration with `AddHonuaMapSDK()`
- [ ] Update message type names (MapMovedMessage → MapExtentChangedMessage, etc.)
- [ ] Migrate components to inherit from `DisposableComponentBase`
- [ ] Consolidate configuration options
- [ ] Update namespaces and imports
- [ ] Run all tests
- [ ] Review deprecation warnings
- [ ] Update documentation

## Getting Help

If you encounter issues during migration:

1. Check the [Troubleshooting Guide](Troubleshooting.md)
2. Review the [documentation](README.md)
3. Search [GitHub Issues](https://github.com/honua/Honua.Server/issues)
4. Ask in [GitHub Discussions](https://github.com/honua/Honua.Server/discussions)
5. Email support: support@honua.io

## Upgrade Tips

### Test Before Upgrading

1. Create a git branch for the upgrade
2. Run full test suite before upgrading
3. Upgrade in development environment first
4. Review all deprecation warnings
5. Test thoroughly before deploying to production

### Incremental Upgrade

For large projects, consider upgrading incrementally:

1. Update package reference
2. Fix breaking changes one component at a time
3. Run tests after each change
4. Commit working changes
5. Continue until complete

### Automated Migration

For large codebases, consider creating migration scripts:

```bash
# Example: Replace old message types
find . -type f -name "*.cs" -exec sed -i 's/MapMovedMessage/MapExtentChangedMessage/g' {} +
find . -type f -name "*.cs" -exec sed -i 's/FeatureClickMessage/FeatureClickedMessage/g' {} +
```

## Release Notes

Detailed release notes are available on GitHub:
https://github.com/honua/Honua.Server/releases

## Roadmap

Upcoming features in future versions:

### v1.1.0 (Planned)
- WebGPU acceleration
- 3D terrain visualization
- Real-time collaboration
- Additional chart types

### v2.0.0 (Planned)
- Plugin architecture
- Cloud sync for configurations
- Mobile-optimized components
- Advanced analytics

---

**Stay Updated:** Star the repository on GitHub to receive notifications about new releases.
