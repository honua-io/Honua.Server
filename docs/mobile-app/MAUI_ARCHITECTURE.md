# .NET MAUI Architecture for Honua Field

**Updated Architecture Decision:** .NET MAUI
**Date:** February 2025
**Status:** Design Phase

---

## Executive Summary

**Decision:** Use .NET MAUI (Multi-platform App UI) instead of native Swift/Kotlin

**Rationale:**
- ✅ Single C# codebase for iOS, Android, Windows
- ✅ Aligns with existing Honua .NET stack
- ✅ Team expertise in C# (not Swift/Kotlin)
- ✅ Code sharing with backend (models, validation logic)
- ✅ Faster development (one codebase vs. two)

**Key Challenge:**
- ⚠️ **AR support is limited in MAUI** - requires workarounds

---

## AR in .NET MAUI - The Reality Check

### Current State of AR in MAUI

**The Problem:**
- ARKit (iOS) and ARCore (Android) **do not have official .NET MAUI bindings**
- No first-party AR framework from Microsoft for MAUI
- Xamarin-era AR libraries (UrhoSharp) are outdated and not MAUI-compatible
- Community AR libraries are sparse and not production-ready

### Available Options for AR

#### Option 1: Platform-Specific AR Views (Recommended ✅)

**Approach:** Hybrid - MAUI for main app, native AR views

**Implementation:**
```csharp
// Use MAUI Custom Handlers to embed native AR views

// iOS - Embed UIView with ARKit
public class ARViewHandler : ViewHandler<ARView, UIView>
{
    protected override UIView CreatePlatformView()
    {
        // Create native iOS UIView with ARSCNView
        var arView = new ARSCNView();
        // Configure ARKit session
        return arView;
    }
}

// Android - Embed native ARCore view
public class ARViewHandler : ViewHandler<ARView, Android.Views.View>
{
    protected override Android.Views.View CreatePlatformView()
    {
        // Create native Android View with ARCore
        var arFragment = new ArFragment();
        return arFragment.View;
    }
}
```

**Pros:**
- ✅ Full access to native AR capabilities (ARKit/ARCore)
- ✅ Production-ready and performant
- ✅ Can update with latest AR features
- ✅ 90% of app is shared MAUI code

**Cons:**
- ❌ Need to write platform-specific AR code
- ❌ Two implementations (iOS and Android)
- ❌ Requires some Objective-C/Swift and Java/Kotlin knowledge

**Code Sharing Estimate:**
- Shared MAUI code: ~85-90%
- Platform-specific AR: ~10-15%

---

#### Option 2: Xamarin.Forms.Nuke + UrhoSharp (Not Recommended ❌)

**Status:** Outdated, unmaintained

**Issues:**
- UrhoSharp hasn't been updated for years
- Not officially compatible with MAUI
- Limited AR capabilities
- Poor performance
- No community support

**Verdict:** Don't use this approach

---

#### Option 3: Web-based AR (AR.js or 8th Wall) (Possible ⚠️)

**Approach:** Embed WebView with web-based AR

**Implementation:**
```csharp
<WebView Source="https://yourapp.com/ar-view.html" />
```

**Pros:**
- ✅ Cross-platform (one implementation)
- ✅ No platform-specific code
- ✅ Easier to update

**Cons:**
- ❌ Limited performance compared to native
- ❌ Requires internet connection (or embedded HTML)
- ❌ Less access to device capabilities
- ❌ Not suitable for complex AR (underground utilities)

**Verdict:** OK for simple AR visualization, not suitable for advanced features

---

#### Option 4: Unity Plugin (Overkill ❌)

**Approach:** Embed Unity AR scene in MAUI app

**Issues:**
- App size bloat (100+ MB)
- Complex integration
- Overkill for our use case
- Performance overhead

**Verdict:** Too complex for field data collection app

---

### Recommended AR Strategy

**Hybrid Approach:** 80% MAUI + 20% Native AR

```
┌─────────────────────────────────────────────────┐
│           .NET MAUI Application                 │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │  Shared MAUI Code (~85%)                 │   │
│  │  - Map View (MAUI Community Toolkit)     │   │
│  │  - Forms and Data Entry                  │   │
│  │  - Collections and Lists                 │   │
│  │  - Sync Engine                          │   │
│  │  - AI/ML (ML.NET, ONNX Runtime)         │   │
│  │  - Business Logic                       │   │
│  │  - Data Access (SQLite)                 │   │
│  └──────────────────────────────────────────┘   │
│                                                 │
│  ┌──────────────────────────────────────────┐   │
│  │  Platform-Specific AR Views (~15%)       │   │
│  │                                          │   │
│  │  iOS:                                    │   │
│  │  └─ ARKit native implementation          │   │
│  │     - ARSCNView                         │   │
│  │     - ARSession                         │   │
│  │     - SceneKit rendering                │   │
│  │                                          │   │
│  │  Android:                                │   │
│  │  └─ ARCore native implementation         │   │
│  │     - ArFragment                        │   │
│  │     - ArSceneView                       │   │
│  │     - Sceneform rendering               │   │
│  └──────────────────────────────────────────┘   │
└─────────────────────────────────────────────────┘
```

**Implementation Plan:**

1. **Build entire app in MAUI** (Months 1-6)
   - All core features
   - No AR initially
   - Ship MVP without AR

2. **Add native AR views** (Months 7-9)
   - Create custom MAUI handlers
   - Implement ARKit view (iOS)
   - Implement ARCore view (Android)
   - Bridge MAUI ↔ Native AR

3. **Integrate AR features** (Months 10-12)
   - AR visualization
   - AR measurement
   - AR navigation

**Benefits:**
- Don't delay MVP waiting for AR
- AR is v1.x feature, not MVP
- Get production feedback before investing in complex AR
- Most users may not need AR (optional feature)

---

## Updated Technology Stack

### .NET MAUI Stack

#### Core Framework
- **.NET MAUI** - Multi-platform app framework
- **C# 12** - Language
- **.NET 8+** - Runtime
- **XAML** or **C# Markup** - UI definition

#### UI Framework
- **MAUI Controls** - Built-in controls
- **Community Toolkit MAUI** - Extended controls and behaviors
- **Syncfusion/DevExpress/Telerik** - Commercial UI libraries (optional)

#### Maps
- **Microsoft.Maui.Controls.Maps** - Built-in maps (basic)
- **Esri ArcGIS Maps SDK for .NET** - Full GIS capabilities (recommended)
- **Mapsui** - Open-source mapping library
- **MapLibre Native** - Open-source vector maps

#### Database
- **SQLite-net** - SQLite ORM
- **Entity Framework Core** - Advanced ORM
- **NetTopologySuite** - Spatial data types and operations
- **Spatialite** - SQLite spatial extension (via native bindings)

#### Networking
- **HttpClient** - Built-in HTTP
- **Refit** - Type-safe REST API client
- **SignalR** - Real-time communication (WebSocket)

#### AI/ML
- **ML.NET** - Microsoft's ML framework
- **ONNX Runtime** - Cross-platform ML inference
- **Platform-specific bindings:**
  - iOS: Core ML via P/Invoke
  - Android: TensorFlow Lite via bindings

#### GPS/Location
- **Microsoft.Maui.Devices.Sensors** - Built-in location services
- **GeolocatorPlugin** - Enhanced location tracking

#### Camera
- **Microsoft.Maui.Media** - Built-in camera
- **CommunityToolkit.Maui.Camera** - Enhanced camera features

#### AR (Platform-Specific)
- **iOS:** ARKit via custom handlers
- **Android:** ARCore via custom handlers
- **Shared:** AR abstraction layer in MAUI

#### Storage
- **Microsoft.Maui.Storage** - File system access
- **SecureStorage** - Encrypted key-value storage

#### Architecture
- **CommunityToolkit.Mvvm** - MVVM helpers (ObservableProperty, RelayCommand)
- **Prism** or **ReactiveUI** - Alternative MVVM frameworks

---

## Architecture Layers

### Clean Architecture with MVVM

```
┌─────────────────────────────────────────────────────────────┐
│                   Presentation Layer                        │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  MAUI Views (XAML or C# Markup)                       │  │
│  │  - MapPage, FeatureFormPage, CollectionsPage, etc.   │  │
│  └──────────────────┬────────────────────────────────────┘  │
│                     │ Data Binding                          │
│  ┌──────────────────▼────────────────────────────────────┐  │
│  │  ViewModels (MVVM)                                    │  │
│  │  - MapViewModel, FeatureFormViewModel, etc.          │  │
│  │  - Observable properties, commands                    │  │
│  └──────────────────┬────────────────────────────────────┘  │
└────────────────────┼─────────────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────────────┐
│                  Application Layer                           │
│  ┌───────────────────────────────────────────────────────┐   │
│  │  Use Cases / Services                                 │   │
│  │  - DataCollectionService                             │   │
│  │  - SyncService                                        │   │
│  │  - AIService                                          │   │
│  └──────────────────┬────────────────────────────────────┘   │
└────────────────────┼─────────────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────────────┐
│                   Domain Layer                               │
│  ┌───────────────────────────────────────────────────────┐   │
│  │  Entities (Feature, Collection, Attachment)           │   │
│  │  Repository Interfaces                                │   │
│  │  Business Rules                                       │   │
│  └──────────────────┬────────────────────────────────────┘   │
└────────────────────┼─────────────────────────────────────────┘
                     │
┌────────────────────▼─────────────────────────────────────────┐
│               Infrastructure Layer                           │
│  ┌───────────────────────────────────────────────────────┐   │
│  │  Data Access (SQLite, OGC API Client)                │   │
│  │  External Services (GPS, Camera, AI Models)          │   │
│  │  Platform Services (via Dependency Injection)        │   │
│  └───────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

### Dependency Injection

```csharp
// MauiProgram.cs
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();

    builder
        .UseMauiApp<App>()
        .UseMauiCommunityToolkit()
        .ConfigureFonts(fonts => { /* ... */ });

    // Register Services
    builder.Services.AddSingleton<IFeatureRepository, FeatureRepository>();
    builder.Services.AddSingleton<ISyncService, SyncService>();
    builder.Services.AddSingleton<IAIService, AIService>();
    builder.Services.AddTransient<ILocationService, LocationService>();

    // Register ViewModels
    builder.Services.AddTransient<MapViewModel>();
    builder.Services.AddTransient<FeatureFormViewModel>();
    builder.Services.AddTransient<CollectionsViewModel>();

    // Register Pages
    builder.Services.AddTransient<MapPage>();
    builder.Services.AddTransient<FeatureFormPage>();

    // Platform-specific services
#if ANDROID
    builder.Services.AddSingleton<IARService, AndroidARService>();
#elif IOS
    builder.Services.AddSingleton<IARService, IOSARService>();
#endif

    return builder.Build();
}
```

---

## Key Modules

### 1. Map Module

**MAUI Implementation:**

```csharp
// Using Esri ArcGIS Maps SDK for .NET
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.UI.Controls;

public class MapPage : ContentPage
{
    private MapView _mapView;

    public MapPage()
    {
        _mapView = new MapView
        {
            Map = new Map(BasemapStyle.ArcGISTopographic)
        };

        Content = _mapView;
    }
}
```

**Alternatives:**
- **Mapsui** - Open-source, lightweight
- **Microsoft.Maui.Controls.Maps** - Built-in, basic features
- **MapLibre** - Open-source vector tiles

**Recommendation:** Esri ArcGIS Maps SDK for production GIS features

---

### 2. Forms Module

**MAUI Community Toolkit:**

```csharp
// Using CommunityToolkit.Mvvm source generators
public partial class FeatureFormViewModel : ObservableObject
{
    [ObservableProperty]
    private string featureName;

    [ObservableProperty]
    private string featureType;

    [ObservableProperty]
    private ObservableCollection<string> suggestions;

    [RelayCommand]
    private async Task SaveFeature()
    {
        // Save logic
    }

    [RelayCommand]
    private async Task TakePhoto()
    {
        // Camera logic
    }
}
```

**Benefits:**
- Source generators reduce boilerplate
- Type-safe commands
- Property change notification automatic

---

### 3. Database Module

**SQLite with NetTopologySuite:**

```csharp
public class Feature
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string ServerId { get; set; }
    public string CollectionId { get; set; }

    // Store as WKB (Well-Known Binary)
    public byte[] GeometryWKB { get; set; }

    // NetTopologySuite geometry (not stored)
    [Ignore]
    public Geometry Geometry
    {
        get => GeometryWKB != null
            ? new WKBReader().Read(GeometryWKB)
            : null;
        set => GeometryWKB = value != null
            ? new WKBWriter().Write(value)
            : null;
    }

    // JSON properties
    public string PropertiesJson { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public SyncStatus SyncStatus { get; set; }
}

public class FeatureRepository : IFeatureRepository
{
    private readonly SQLiteAsyncConnection _database;

    public FeatureRepository(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        _database.CreateTableAsync<Feature>().Wait();
    }

    public async Task<List<Feature>> GetFeaturesAsync(string collectionId)
    {
        return await _database.Table<Feature>()
            .Where(f => f.CollectionId == collectionId)
            .ToListAsync();
    }

    public async Task<int> SaveFeatureAsync(Feature feature)
    {
        if (feature.Id != 0)
            return await _database.UpdateAsync(feature);
        else
            return await _database.InsertAsync(feature);
    }
}
```

---

### 4. AI Module

**ML.NET for Suggestions:**

```csharp
public class AttributeSuggestionService
{
    private PredictionEngine<FeatureInput, FeatureSuggestion> _predictionEngine;

    public AttributeSuggestionService()
    {
        // Load ML.NET model
        var modelPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "suggestion_model.zip"
        );

        var mlContext = new MLContext();
        var model = mlContext.Model.Load(modelPath, out _);
        _predictionEngine = mlContext.Model
            .CreatePredictionEngine<FeatureInput, FeatureSuggestion>(model);
    }

    public List<string> GetSuggestions(string featureType, string fieldName)
    {
        var input = new FeatureInput
        {
            FeatureType = featureType,
            FieldName = fieldName,
            Location = GetCurrentLocation()
        };

        var prediction = _predictionEngine.Predict(input);
        return prediction.TopSuggestions;
    }
}
```

**ONNX Runtime for Image Classification:**

```csharp
public class FeatureDetectionService
{
    private InferenceSession _session;

    public FeatureDetectionService()
    {
        var modelPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "feature_detector.onnx"
        );
        _session = new InferenceSession(modelPath);
    }

    public async Task<FeatureDetection> DetectFeatureAsync(byte[] imageBytes)
    {
        // Preprocess image
        var tensor = ImageToTensor(imageBytes);

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };

        using var results = _session.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        // Postprocess
        return ParseOutput(output);
    }
}
```

---

### 5. AR Module (Platform-Specific)

**Abstraction Layer:**

```csharp
// Shared interface
public interface IARService
{
    Task<bool> IsARAvailableAsync();
    Task StartARSessionAsync(ARConfiguration config);
    Task StopARSessionAsync();
    void AddFeatureMarker(Feature feature);
    void RemoveFeatureMarker(string featureId);
}

// MAUI View
public class ARView : ContentView
{
    public static readonly BindableProperty ARServiceProperty =
        BindableProperty.Create(
            nameof(ARService),
            typeof(IARService),
            typeof(ARView)
        );

    public IARService ARService
    {
        get => (IARService)GetValue(ARServiceProperty);
        set => SetValue(ARServiceProperty, value);
    }
}
```

**iOS Implementation (Platform/iOS/ARService.cs):**

```csharp
using ARKit;
using SceneKit;

public class IOSARService : IARService
{
    private ARSCNView _arView;
    private ARSession _session;

    public async Task<bool> IsARAvailableAsync()
    {
        return ARWorldTrackingConfiguration.IsSupported;
    }

    public async Task StartARSessionAsync(ARConfiguration config)
    {
        var configuration = new ARWorldTrackingConfiguration
        {
            PlaneDetection = ARPlaneDetection.Horizontal,
            WorldAlignment = ARWorldAlignment.GravityAndHeading
        };

        _session = new ARSession();
        _arView.Session = _session;
        _session.Run(configuration);
    }

    public void AddFeatureMarker(Feature feature)
    {
        // Convert GPS to AR coordinates
        var position = GPSToARPosition(feature.Geometry);

        // Create 3D marker
        var geometry = SCNBox.Create(0.1f, 0.1f, 0.1f, 0);
        var node = new SCNNode { Geometry = geometry };
        node.Position = position;

        _arView.Scene.RootNode.AddChildNode(node);
    }
}
```

**Android Implementation (Platform/Android/ARService.cs):**

```csharp
using Google.AR.Core;
using Google.AR.Sceneform;

public class AndroidARService : IARService
{
    private ArFragment _arFragment;
    private Session _session;

    public async Task<bool> IsARAvailableAsync()
    {
        var availability = ArCoreApk.Instance
            .CheckAvailability(Platform.CurrentActivity);
        return availability.IsSupported;
    }

    public async Task StartARSessionAsync(ARConfiguration config)
    {
        var sessionConfig = new Config(_session);
        sessionConfig.SetUpdateMode(Config.UpdateMode.LatestCameraImage);
        _session.Configure(sessionConfig);

        _arFragment.ArSceneView.Resume();
    }

    public void AddFeatureMarker(Feature feature)
    {
        // Convert GPS to AR coordinates
        var pose = GPSToARPose(feature.Geometry);

        // Create anchor
        var anchor = _session.CreateAnchor(pose);

        // Add 3D model
        // (Sceneform implementation)
    }
}
```

**Custom Handler to Bridge MAUI ↔ Native:**

```csharp
// Handlers/ARViewHandler.cs
#if IOS
public class ARViewHandler : ViewHandler<ARView, UIView>
{
    protected override UIView CreatePlatformView()
    {
        var arView = new ARSCNView();
        // Configure ARKit
        return arView;
    }

    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);
        // Connect MAUI view to native view
        VirtualView.ARService = new IOSARService();
    }
}
#elif ANDROID
public class ARViewHandler : ViewHandler<ARView, FrameLayout>
{
    protected override FrameLayout CreatePlatformView()
    {
        var fragment = new ArFragment();
        // Configure ARCore
        return fragment.View as FrameLayout;
    }

    protected override void ConnectHandler(FrameLayout platformView)
    {
        base.ConnectHandler(platformView);
        VirtualView.ARService = new AndroidARService();
    }
}
#endif

// Register handler in MauiProgram.cs
builder.ConfigureMauiHandlers(handlers =>
{
    handlers.AddHandler<ARView, ARViewHandler>();
});
```

---

## Offline Sync with MAUI

**Sync Service:**

```csharp
public class SyncService : ISyncService
{
    private readonly IFeatureRepository _repository;
    private readonly IOGCFeaturesClient _apiClient;
    private readonly IConnectivity _connectivity;

    public async Task<SyncResult> SyncAsync()
    {
        if (_connectivity.NetworkAccess != NetworkAccess.Internet)
            return SyncResult.NoConnection;

        var pendingChanges = await _repository.GetPendingChangesAsync();

        foreach (var change in pendingChanges)
        {
            try
            {
                switch (change.Operation)
                {
                    case ChangeOperation.Insert:
                        var newId = await _apiClient.CreateFeatureAsync(change.Feature);
                        change.Feature.ServerId = newId;
                        break;

                    case ChangeOperation.Update:
                        await _apiClient.UpdateFeatureAsync(change.Feature);
                        break;

                    case ChangeOperation.Delete:
                        await _apiClient.DeleteFeatureAsync(change.Feature.ServerId);
                        break;
                }

                await _repository.MarkAsSyncedAsync(change.Id);
            }
            catch (Exception ex)
            {
                // Handle conflict or error
                await HandleSyncError(change, ex);
            }
        }

        return SyncResult.Success;
    }
}
```

---

## Performance Considerations

### .NET MAUI Performance vs. Native

**Map Rendering:**
- MAUI: ~30-45 FPS (using third-party map controls)
- Native: 60 FPS
- **Impact:** Noticeable on complex maps with many features
- **Mitigation:** Use Esri ArcGIS SDK (better optimized)

**AR Rendering:**
- MAUI + Native AR: Same as native (AR runs in native code)
- **Impact:** None (AR is platform-specific anyway)

**UI Responsiveness:**
- MAUI: ~16-32ms for common operations
- Native: ~8-16ms
- **Impact:** Minimal for field app use cases
- **Mitigation:** Async/await, background threads

**Startup Time:**
- MAUI: 2-4 seconds
- Native: 1-2 seconds
- **Impact:** Acceptable for field apps

**Memory:**
- MAUI: 50-100MB baseline
- Native: 30-50MB baseline
- **Impact:** Acceptable on modern devices

**Battery:**
- MAUI: ~5-10% higher drain than native
- **Impact:** Noticeable on long field days
- **Mitigation:** Optimize background tasks, GPS usage

### Optimization Strategies

1. **Use Compiled Bindings:**
```xml
<Label Text="{Binding Name, Mode=OneWay}"
       x:DataType="local:FeatureViewModel" />
```

2. **CollectionView over ListView:**
```xml
<CollectionView ItemsSource="{Binding Features}">
    <!-- More performant than ListView -->
</CollectionView>
```

3. **Lazy Loading:**
```csharp
[ObservableProperty]
private AsyncRelayCommand loadFeaturesCommand;

partial void OnLoadFeaturesCommandChanged(AsyncRelayCommand value)
{
    // Load only when needed
}
```

4. **Image Optimization:**
```csharp
// Resize images before storing
var resized = await image.ResizeAsync(1920, 1080);
```

---

## Code Sharing Breakdown

### Shared Code (~85-90%)

**Core Business Logic:**
- Data models (Feature, Collection, etc.)
- Repository interfaces and implementations
- Sync engine
- Validation rules
- Business logic services

**UI:**
- All pages and views (XAML/C#)
- ViewModels
- Converters and behaviors
- Styles and themes

**Data Access:**
- SQLite database
- OGC API client
- File storage

**AI/ML:**
- ML.NET models
- ONNX Runtime inference
- Prediction logic

### Platform-Specific Code (~10-15%)

**AR Features:**
- iOS: ARKit implementation
- Android: ARCore implementation
- Custom handlers

**Native API Access:**
- High-precision GNSS (if needed)
- Advanced camera features
- Platform-specific permissions

**Platform Services:**
- Push notifications (if different)
- Deep linking
- File associations

---

## Advantages of .NET MAUI for Honua

### ✅ Team and Codebase Alignment

**Existing Skills:**
- Team already knows C#/.NET
- No need to hire Swift/Kotlin developers
- Can share developers between mobile and backend

**Code Reuse:**
- Share models between mobile and server
- Share validation logic
- Share business rules
- Common utilities and helpers

### ✅ Development Speed

**Single Codebase:**
- Write once, run on iOS, Android, Windows
- Faster feature development
- Easier maintenance
- Consistent behavior across platforms

**Tooling:**
- Visual Studio (excellent .NET tooling)
- Hot reload for rapid iteration
- NuGet package ecosystem

### ✅ Integration with Honua Ecosystem

**Natural Fit:**
- Same language as Honua Server
- Easy to integrate with .NET libraries
- Share authentication logic
- Common serialization (System.Text.Json)

### ✅ Enterprise Features

**Built-in Support:**
- Authentication (MSAL for Azure AD)
- Offline sync (local database)
- Secure storage
- App protection policies

---

## Disadvantages and Trade-offs

### ❌ AR Complexity

**Challenge:** No official AR support
**Impact:** More complex AR implementation
**Mitigation:** Platform-specific handlers (workable)

### ❌ Performance

**Challenge:** Not quite as fast as pure native
**Impact:** Map rendering 30-45 FPS vs. 60 FPS native
**Mitigation:** Use optimized libraries (Esri SDK), acceptable for field apps

### ❌ App Size

**Challenge:** Larger app size than native
**Size:** 30-50MB vs. 10-20MB native
**Impact:** Longer download time
**Mitigation:** App thinning, trim unused code

### ❌ Platform-Specific Features

**Challenge:** Latest iOS/Android features may lag
**Impact:** Can't use brand-new platform APIs immediately
**Mitigation:** Use custom handlers for new features

---

## Decision Matrix: MAUI vs. Native

| Factor | Native (Swift/Kotlin) | .NET MAUI | Winner |
|--------|----------------------|-----------|--------|
| **Development Speed** | Slower (2 codebases) | Faster (1 codebase) | ✅ MAUI |
| **Performance** | Excellent (60 FPS) | Good (30-45 FPS) | Native |
| **Team Skills** | Need to hire | Existing team | ✅ MAUI |
| **Code Sharing** | 0% between platforms | 85-90% shared | ✅ MAUI |
| **AR Support** | Full native APIs | Requires handlers | Native |
| **Maintenance** | 2 codebases | 1 codebase | ✅ MAUI |
| **Platform Features** | Immediate | May lag | Native |
| **Integration** | Custom bridges needed | Natural (.NET) | ✅ MAUI |
| **Ecosystem** | Platform SDKs | NuGet packages | ✅ MAUI |
| **App Size** | Smaller (10-20MB) | Larger (30-50MB) | Native |
| **UI Consistency** | Platform-native | Can be cross-platform | Tie |
| **Cost** | Higher (2 teams) | Lower (1 team) | ✅ MAUI |

**Score:** MAUI wins 7/12 factors

**Recommendation:** .NET MAUI is the right choice for Honua Field, with platform-specific AR implementations

---

## Revised Roadmap with MAUI

### Phase 1: MVP (6 months) - No AR

**Month 1-2: Foundation**
- ✅ .NET MAUI project setup
- ✅ MVVM architecture
- ✅ SQLite database with NetTopologySuite
- ✅ OGC Features API client
- ✅ Authentication (OAuth 2.0)

**Month 3-4: Core Features**
- ✅ Map view (Esri ArcGIS Maps SDK)
- ✅ Feature collection (point, line, polygon)
- ✅ Smart forms
- ✅ Photo attachments
- ✅ Offline editing and sync

**Month 5-6: Polish and Launch**
- ✅ GPS integration
- ✅ Search and filtering
- ✅ Settings
- ✅ Testing
- ✅ App Store / Play Store

**Deliverable:** MVP without AR (AR is v1.x feature)

---

### Phase 2: Intelligence (6 months)

**Month 7-8: AI**
- ✅ ML.NET smart suggestions
- ✅ ONNX Runtime image classification
- ✅ Voice input (built-in speech recognition)

**Month 9-10: Collaboration**
- ✅ Real-time location tracking
- ✅ Team data sharing
- ✅ Geofencing

**Month 11-12: AR Development**
- ✅ Custom ARView handlers
- ✅ iOS ARKit implementation
- ✅ Android ARCore implementation
- ✅ AR visualization features

**Deliverable:** Full-featured app with AI and AR

---

## Conclusion

### Final Recommendation: ✅ Use .NET MAUI

**Why:**
1. **Team Alignment:** Leverage existing C#/.NET skills
2. **Code Sharing:** 85-90% shared codebase
3. **Development Speed:** Faster with single codebase
4. **Integration:** Natural fit with Honua .NET stack
5. **Cost:** Lower development and maintenance costs

**AR Challenge:**
- ⚠️ AR requires platform-specific implementation
- ✅ But this is manageable with custom handlers
- ✅ AR is 10-15% of app, rest is shared
- ✅ Ship MVP first, add AR in v1.x

**Performance Trade-off:**
- Maps: 30-45 FPS (MAUI) vs. 60 FPS (native)
- ✅ Acceptable for field data collection
- ✅ Most time is spent on forms, not panning maps

**Bottom Line:**
The benefits of .NET MAUI (team skills, code sharing, speed) **outweigh** the AR complexity and minor performance trade-offs for the Honua Field use case.

---

**Next Steps:**
1. ✅ Update design documents for MAUI architecture
2. Create MAUI project structure
3. Build proof-of-concept with map and forms
4. Test AR custom handlers on both platforms
5. Proceed with Phase 1 development

---

**Document Version:** 1.0 (MAUI Architecture)
**Date:** February 2025
**Status:** Approved for Development
