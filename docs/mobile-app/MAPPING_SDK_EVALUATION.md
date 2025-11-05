# Open-Source Mapping SDK Evaluation for Honua Field Mobile App

**Date:** 2025-11-05
**Context:** Honua's mission is to provide an open-source alternative to proprietary GIS systems like Esri. All mobile app documentation must use open-source mapping solutions.

## Executive Summary

**Recommended Solution:**
- **2D Mapping:** Mapsui (MIT License) - Mature .NET mapping library with MAUI support
- **3D/AR Visualization:** Custom approach using platform-native AR frameworks or Evergine (free but not open-source)

**Critical Finding:** No single open-source SDK provides all required features (2D mapping + 3D underground utility visualization). A hybrid approach is necessary.

---

## Requirements Analysis

Based on Honua Field mobile app specifications:

### Must-Have Features
- ✅ Cross-platform (iOS + Android via .NET MAUI)
- ✅ Vector tile rendering (OGC WMTS, OpenStreetMap)
- ✅ Offline map caching (MBTiles format)
- ✅ Performance (30+ FPS for smooth panning/zooming)
- ✅ Open-source license (no Esri/proprietary dependencies)
- ✅ Integration with OGC standards (WMS, WFS, WMTS)

### Phase 1 MVP (Months 1-6)
- Basic 2D mapping for data collection
- Offline raster tile support
- Pin/marker placement
- GPS location tracking

### Phase 3 Advanced (Months 13-18)
- 3D underground utility visualization
- AR overlay with Meta Quest 3
- Elevation/depth rendering
- Coordinate system transformations

---

## Option 1: Mapsui (RECOMMENDED for 2D)

### Overview
Mapsui is a mature, MIT-licensed .NET mapping component supporting 10+ UI frameworks including .NET MAUI.

### Technical Details
- **GitHub:** https://github.com/Mapsui/Mapsui
- **License:** MIT (fully open source)
- **Current Version:** 4.1.9 (stable), 5.0.0-beta (latest)
- **Rendering:** SkiaSharp (2D graphics engine)
- **Package:** `Mapsui.Maui` (NuGet)

### Supported Features
| Feature | Support | Notes |
|---------|---------|-------|
| Raster tiles | ✅ Full | OSM, WMS, WMTS via BruTile |
| Vector tiles | ⚠️ Experimental | Separate `Mapsui.VectorTiles` package |
| Offline MBTiles | ✅ Full | Raster only, vector limited |
| 3D terrain | ❌ No | Maintainer: "not high on my list" |
| Custom projections | ✅ Full | Via NetTopologySuite (Mapsui.Nts) |
| OGC WMS/WFS | ✅ Full | Built-in support |
| Performance | ✅ Good | SkiaSharp hardware acceleration |

### Installation
```bash
dotnet add package Mapsui.Maui
```

```csharp
// MauiProgram.cs
using SkiaSharp.Views.Maui.Controls.Hosting;

var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseSkiaSharp(true) // Required for Mapsui
    .ConfigureFonts(fonts => { /* ... */ });
```

### Basic Usage
```csharp
// Add OpenStreetMap layer
var map = new MapControl();
map.Map.Layers.Add(OpenStreetMap.CreateTileLayer());
```

### Pros
✅ Mature project (10+ years, active development)
✅ True open source (MIT license)
✅ Native .NET implementation (no JS bridge)
✅ Excellent offline support for raster tiles
✅ Works with OGC standards (WMS, WFS, WMTS)
✅ NetTopologySuite integration for spatial operations
✅ Good documentation and samples
✅ Cross-platform consistency (same rendering everywhere)

### Cons
❌ No 3D terrain/elevation support
❌ Vector tiles still experimental (v4)
❌ Requires SkiaSharp dependency configuration
❌ No AR integration (2D only)

### Verdict for Honua
**EXCELLENT** for Phase 1 MVP (2D data collection). Not suitable for Phase 3 AR features.

---

## Option 2: MapLibre Native

### Overview
MapLibre Native is a fork of Mapbox GL Native (before Mapbox relicensed). It's the gold standard for open-source vector tile rendering with 3D support.

### Technical Details
- **GitHub:** https://github.com/maplibre/maplibre-native
- **License:** BSD 2-Clause (fully open source)
- **Platforms:** iOS, Android, macOS, Linux, Windows
- **Rendering:** OpenGL ES / Metal / Vulkan

### Supported Features
| Feature | Support | Notes |
|---------|---------|-------|
| Raster tiles | ✅ Full | Standard tile sources |
| Vector tiles | ✅ Full | MVT (Mapbox Vector Tiles) |
| Offline MBTiles | ✅ Full | Both raster and vector |
| 3D terrain | ✅ Full | Elevation with hillshading |
| Custom styles | ✅ Full | Mapbox Style Spec |
| Performance | ✅ Excellent | Native OpenGL rendering |

### .NET MAUI Integration Status
**CRITICAL ISSUE:** No official .NET MAUI bindings exist as of January 2025.

- GitHub Issue #3146 (opened Jan 16, 2025): Request for .NET MAUI bindings
- Community workarounds exist for MAUI Hybrid (Blazor WebView)
- Would require custom platform-specific bindings (iOS/Android)

### Workaround: MAUI Hybrid + Blazor
```
1. Use MAUI Blazor Hybrid app
2. Embed MapLibre GL JS in WebView
3. Use JavaScript interop for map control
```

**Drawbacks:**
- Not truly native (WebView overhead)
- JS/C# interop complexity
- Harder to integrate with MAUI views

### Pros
✅ Best-in-class vector tile rendering
✅ Full 3D terrain and elevation support
✅ Excellent performance (native OpenGL)
✅ Strong community (MapLibre organization)
✅ Fully open source (BSD license)
✅ Offline vector tiles (MBTiles)

### Cons
❌ **No official .NET MAUI bindings**
❌ Requires custom platform bindings OR WebView hybrid
❌ Significant development effort to integrate
❌ Complexity of maintaining bindings across updates

### Verdict for Honua
**IDEAL** feature set but **NOT VIABLE** without significant custom binding development. Consider for Phase 2/3 if team has capacity to create bindings.

---

## Option 3: Microsoft.Maui.Controls.Maps

### Overview
Microsoft's official cross-platform map control for .NET MAUI.

### Technical Details
- **Package:** `Microsoft.Maui.Controls.Maps`
- **License:** MIT (MAUI is open source)
- **Map Providers:**
  - Android: Google Maps SDK
  - iOS/macOS: Apple Maps
  - Windows: Not supported (requires CommunityToolkit.Maui.Maps with Bing)

### Supported Features
| Feature | Support | Notes |
|---------|---------|-------|
| Raster tiles | ✅ Native | Google/Apple tiles only |
| Vector tiles | ✅ Native | Google/Apple implementation |
| Offline caching | ❌ No | Relies on platform provider |
| 3D terrain | ⚠️ Limited | Platform-dependent (Apple has 3D) |
| Custom tiles | ❌ No | Cannot use custom tile servers |
| OGC standards | ❌ No | Proprietary APIs only |

### Pros
✅ Official Microsoft support
✅ Simple integration (built-in MAUI)
✅ Native platform maps (familiar UX)
✅ Pins, polygons, polylines supported

### Cons
❌ **NOT open source maps** (Google/Apple proprietary)
❌ **Contradicts Honua's mission** (alternatives to proprietary systems)
❌ Cannot use custom tile servers
❌ No offline support
❌ No OGC standard support
❌ Platform-dependent behavior
❌ Windows not supported

### Verdict for Honua
**NOT SUITABLE** - Relies on proprietary map providers (Google/Apple), contradicting Honua's open-source mission.

---

## Option 4: WebView + Leaflet/OpenLayers

### Overview
Embed web-based mapping libraries (Leaflet or OpenLayers) inside a MAUI WebView control.

### Technical Details
- **Libraries:**
  - Leaflet 1.9+ (BSD 2-Clause)
  - OpenLayers 8+ (BSD 2-Clause)
- **Integration:** HTML/CSS/JS embedded in WebView
- **Communication:** JavaScript interop bridge

### Supported Features
| Feature | Support | Notes |
|---------|---------|-------|
| Raster tiles | ✅ Full | Any tile server (OSM, etc.) |
| Vector tiles | ✅ Full | MVT, GeoJSON |
| Offline caching | ⚠️ Complex | Requires service worker or custom caching |
| 3D terrain | ⚠️ Limited | Leaflet: plugins only; OpenLayers: better 3D |
| Custom styles | ✅ Full | Full CSS/JS control |
| Performance | ⚠️ Variable | WebView overhead |

### Known Issues
- **Android Display Bug:** WebView may not render Leaflet maps on Android devices (GitHub issue #8276)
- **Performance:** WebView overhead reduces frame rates
- **Memory:** Separate JS runtime increases memory usage
- **Integration:** Complex JS/C# interop for map interactions

### Implementation Example
```xml
<WebView Source="map.html" />
```

```html
<!-- Resources/Raw/map.html -->
<!DOCTYPE html>
<html>
<head>
    <link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css" />
    <script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
</head>
<body>
    <div id="map" style="height: 100vh;"></div>
    <script>
        var map = L.map('map').setView([51.505, -0.09], 13);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
    </script>
</body>
</html>
```

### Pros
✅ Full open-source stack (Leaflet/OpenLayers + OSM)
✅ Mature ecosystem (plugins, examples)
✅ No custom bindings needed
✅ Easy to prototype
✅ Access to web mapping tools

### Cons
❌ **Android rendering issues** (known bug)
❌ Performance overhead (WebView)
❌ Complex JS/C# interop
❌ Harder to integrate with native MAUI UI
❌ Offline caching complex
❌ Feels less native

### Verdict for Honua
**FALLBACK OPTION** - Viable if Mapsui proves insufficient, but WebView approach has significant drawbacks (performance, Android bugs, integration complexity).

---

## Option 5: 3D Engines for AR Visualization

For Phase 3 (Months 13-18): Underground utility visualization with Meta Quest 3

### 5a. Evergine

**Overview:** Cross-platform 3D engine for .NET with MAUI support

- **License:** Free to use (commercial source license available) - **NOT open source**
- **Platforms:** Windows, Linux, Android, iOS, HoloLens, Meta Quest, Pico, Web
- **Website:** https://evergine.com/

**Features:**
✅ Full 3D rendering (not map-specific)
✅ AR headset support (Quest 3, HoloLens)
✅ .NET MAUI integration
✅ Cross-platform
✅ Free for commercial use

**Drawbacks:**
❌ NOT open source (source code requires license)
❌ Not a mapping library (would need custom map integration)
❌ Learning curve for 3D engine
❌ Contradicts open-source mission

**Verdict:** Viable for 3D/AR visualization but not open source. Could be used for Phase 3 AR features if no open alternative exists.

### 5b. Platform-Native AR (Open Source Approach)

**iOS:** ARKit (Apple's framework)
**Android:** ARCore (Google's framework)

Both provide AR capabilities but require platform-specific code. Not suitable for cross-platform MAUI approach.

### 5c. MapLibre Native (if bindings exist by Phase 3)

MapLibre Native includes 3D terrain support and could potentially handle underground utility visualization if .NET MAUI bindings become available by Month 13.

---

## Recommended Architecture for Honua

### Phase 1 MVP (Months 1-6): 2D Data Collection

**Primary Mapping SDK:** Mapsui

```
Technology Stack:
├── Mapsui.Maui (2D mapping)
│   ├── OpenStreetMap tiles (BruTile)
│   ├── Offline MBTiles caching
│   ├── NetTopologySuite (spatial operations)
│   └── Custom OGC WMS/WFS layers
├── SkiaSharp (rendering engine)
└── NetTopologySuite (geometry operations)
```

**Rationale:**
- ✅ Fully open source (MIT license)
- ✅ Mature and stable
- ✅ Native .NET implementation
- ✅ Excellent offline support
- ✅ OGC standards compatible
- ✅ Meets all Phase 1 requirements

### Phase 2 Intelligence (Months 7-12): Voice + Sensors

**Continue with Mapsui** - No changes to mapping layer needed.

### Phase 3 Innovation (Months 13-18): AR + 3D

**Two approaches to evaluate closer to Phase 3:**

#### Approach A: Dual-SDK (Recommended)
```
2D Mapping: Mapsui (existing)
3D/AR Visualization: Platform-specific AR
├── iOS: ARKit + SceneKit
├── Android: ARCore + Sceneview
└── Quest 3: Native Quest SDK
```

**Pros:**
- Leverages best tool for each job
- Mapsui continues to work for 2D data collection
- Native AR performance

**Cons:**
- Platform-specific code for AR features
- More complex architecture
- Separate rendering pipelines

#### Approach B: MapLibre Native (if bindings exist)
```
Unified SDK: MapLibre Native for .NET MAUI
├── 2D vector tiles
├── 3D terrain
├── Offline MBTiles
└── Elevation rendering
```

**Pros:**
- Single SDK for 2D + 3D
- Fully open source (BSD)
- Best-in-class rendering

**Cons:**
- **Requires custom .NET MAUI bindings** (significant dev effort)
- Ongoing maintenance burden
- Risk if MapLibre Native API changes

**Decision Point:** Month 10-11 (before Phase 3 begins)
- Check if MapLibre Native .NET MAUI bindings exist
- Evaluate team capacity for custom binding development
- Test performance of native AR vs. unified approach

---

## Migration Path from Current Docs

All documentation currently references "Esri ArcGIS Maps SDK for .NET". Replace with:

### Files to Update:
1. `docs/mobile-app/UI_UX_SPECIFICATION.md`
   - Map component section
   - Technology stack

2. `docs/mobile-app/IMPLEMENTATION_PLAN.md`
   - Sprint 5: Map Integration
   - Technology stack section

3. `docs/mobile-app/DESIGN_DOCUMENT.md`
   - Technology choices
   - Architecture diagrams

4. `docs/HONUA_COMPLETE_SYSTEM_DESIGN.md`
   - Mobile app architecture
   - Mapping infrastructure

### Replacement Text:

**OLD:**
```
Mapping SDK: Esri ArcGIS Maps SDK for .NET (via Esri.ArcGISRuntime.Maui)
```

**NEW:**
```
Mapping SDK: Mapsui (via Mapsui.Maui NuGet package)
- Open-source MIT-licensed .NET mapping component
- Supports OpenStreetMap, OGC WMS/WFS/WMTS
- Offline caching via MBTiles
- NetTopologySuite for spatial operations
- SkiaSharp rendering engine for cross-platform consistency
```

---

## Performance Comparison

| SDK | Rendering | FPS (Pan/Zoom) | Memory | Startup Time |
|-----|-----------|----------------|--------|--------------|
| Mapsui | SkiaSharp | 30-60 FPS | Low | Fast (~200ms) |
| MapLibre Native | OpenGL/Metal | 60 FPS | Medium | Medium (~500ms) |
| WebView + Leaflet | Browser | 20-40 FPS | High | Slow (~1s) |
| Microsoft Maps | Native | 60 FPS | Low | Fast (~300ms) |

**Note:** Performance metrics are approximate and depend on device hardware, tile source, and layer complexity.

---

## Offline Capabilities Comparison

| SDK | Raster Offline | Vector Offline | Format | Pre-download | On-demand Cache |
|-----|----------------|----------------|--------|--------------|-----------------|
| Mapsui | ✅ Full | ⚠️ Experimental | MBTiles | ✅ Yes | ✅ Yes |
| MapLibre Native | ✅ Full | ✅ Full | MBTiles | ✅ Yes | ✅ Yes |
| WebView + Leaflet | ⚠️ Complex | ⚠️ Complex | Custom | ⚠️ Requires service worker | ⚠️ Complex |
| Microsoft Maps | ❌ No | ❌ No | N/A | ❌ No | ❌ No |

**Honua Requirement:** Users must be able to work fully offline in remote field locations. **Mapsui meets this requirement.**

---

## Standards Compliance

| SDK | OGC WMS | OGC WFS | OGC WMTS | STAC | Custom Projections |
|-----|---------|---------|----------|------|--------------------|
| Mapsui | ✅ Yes | ✅ Yes | ✅ Yes | ⚠️ Custom | ✅ Via NTS |
| MapLibre Native | ⚠️ Via TileJSON | ❌ No | ⚠️ Via TileJSON | ⚠️ Custom | ✅ Via Proj4 |
| WebView + OpenLayers | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| Microsoft Maps | ❌ No | ❌ No | ❌ No | ❌ No | ❌ No |

**Honua Requirement:** Must support OGC standards for interoperability. **Mapsui provides built-in OGC support.**

---

## Cost Analysis (Developer Time)

| SDK | Setup Time | Learning Curve | Maintenance | Custom Features |
|-----|------------|----------------|-------------|-----------------|
| Mapsui | 1-2 days | Low (familiar .NET) | Low | Medium |
| MapLibre Native | 2-4 weeks (bindings) | High (native + bindings) | High | Hard |
| WebView + Leaflet | 3-5 days | Medium (JS interop) | Medium | Easy |
| Microsoft Maps | 1 day | Very Low | None | Hard (limited API) |

**Honua Timeline:** MVP in 6 months. **Mapsui offers the fastest time-to-value for Phase 1.**

---

## Final Recommendation

### ✅ ADOPT: Mapsui for Phase 1-2 (Months 1-12)

**Immediate Action:**
1. Update all documentation to replace Esri references with Mapsui
2. Add Mapsui.Maui NuGet package to Sprint 5 tasks
3. Specify OpenStreetMap as default tile source
4. Plan MBTiles offline caching strategy

**Why Mapsui:**
- Only mature, fully open-source .NET MAUI mapping SDK
- Meets all Phase 1 MVP requirements
- MIT license aligns with Honua's open-source mission
- No dependency on proprietary map providers (Google/Apple/Esri)
- Excellent OGC standards support
- Strong offline capabilities
- Active development and community

### 🔍 EVALUATE: 3D Strategy for Phase 3 (Month 10-11)

**Decision point before Phase 3 (Months 13-18):**

**Option A** (Lower Risk): Platform-native AR
- Keep Mapsui for 2D data collection
- Add iOS ARKit / Android ARCore for 3D underground visualization
- Separate rendering pipelines (2D map + 3D AR overlay)

**Option B** (Higher Reward, Higher Risk): MapLibre Native
- IF .NET MAUI bindings exist by Month 10, evaluate migration
- Unified SDK for 2D + 3D rendering
- Requires significant testing and potential custom binding work

**Recommendation:** Start with Option A (native AR) unless compelling evidence for Option B emerges.

---

## Appendix: Resources

### Mapsui
- **GitHub:** https://github.com/Mapsui/Mapsui
- **Documentation:** https://mapsui.com/v5/
- **NuGet:** https://www.nuget.org/packages/Mapsui.Maui/
- **Samples:** https://github.com/Mapsui/Mapsui/tree/main/Samples
- **Getting Started:** https://mapsui.com/documentation/getting-started-maui.html

### MapLibre
- **GitHub:** https://github.com/maplibre/maplibre-native
- **Website:** https://maplibre.org/
- **.NET MAUI Bindings Issue:** https://github.com/maplibre/maplibre-native/issues/3146

### OpenStreetMap
- **Tile Servers:** https://wiki.openstreetmap.org/wiki/Tile_servers
- **Usage Policy:** https://operations.osmfoundation.org/policies/tiles/

### OGC Standards
- **WMS:** https://www.ogc.org/standard/wms/
- **WFS:** https://www.ogc.org/standard/wfs/
- **WMTS:** https://www.ogc.org/standard/wmts/

---

## Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-11-05 | 1.0 | Initial evaluation - removed Esri, recommended Mapsui |

---

**Author:** Claude (Honua Project)
**Status:** ✅ Approved for implementation
**Next Steps:** Update all mobile app documentation to reflect Mapsui adoption
