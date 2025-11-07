# HonuaField - Mobile Field Data Collection

Cross-platform mobile application for field GIS data collection, built with .NET MAUI.

## Overview

HonuaField is a mobile app for iOS, Android, Windows, and macOS that enables field workers to collect, edit, and synchronize geospatial data with Honua Server. It provides offline-first data collection with automatic synchronization when connectivity is restored.

## Features

### Authentication & Security
- **OAuth 2.0 + PKCE** - Secure authentication with Honua Server
- **Biometric Authentication** - Face ID, Touch ID, and Fingerprint support
- **Secure Storage** - Encrypted storage for tokens and sensitive data
- **Remember Me** - Optional credential persistence
- **Token Refresh** - Automatic token renewal

### Data Collection
- **Feature Management** - Create, view, edit, and delete features with full CRUD
- **Dynamic Forms** - Forms generated from JSON Schema with validation
- **Offline Storage** - SQLite database with NetTopologySuite spatial support
- **Attachments** - Photo/video/audio capture and gallery picker
- **Collections** - Organize features into collections/layers with metadata
- **Change Tracking** - Track all changes for synchronization
- **Search & Filter** - Full-text search and spatial filtering

### Mapping
- **Interactive Map** - Mapsui-powered map with pan, zoom, rotate
- **GPS Location** - Current location tracking with continuous updates
- **GPS Track Recording** - Record breadcrumb trails with statistics
- **Custom Symbology** - Simple, UniqueValue, and Graduated renderers
- **Offline Map Tiles** - Download and use maps without connectivity
- **Feature Visualization** - Points, lines, polygons with custom styling
- **Drawing Tools** - Create and edit geometries on the map
- **Spatial Queries** - Find features by bounds, nearby, nearest

### Synchronization
- **Offline-First** - Work without connectivity, sync when available
- **Bidirectional Sync** - Pull from and push to server
- **Conflict Resolution** - ServerWins, ClientWins, AutoMerge strategies
- **Three-Way Merge** - Intelligent property-level merging
- **Retry Logic** - Automatic retry with exponential backoff
- **Progress Reporting** - Real-time sync progress updates
- **Change Log** - Track all local modifications with metadata

## Technology Stack

- **.NET 8 / MAUI** - Cross-platform framework (iOS, Android, Windows, macOS)
- **SQLite-net-pcl** - Local database for offline storage
- **NetTopologySuite** - Spatial geometry operations (WKT, WKB, spatial queries)
- **Mapsui** - Open-source mapping library
- **SkiaSharp** - 2D graphics rendering for maps
- **OAuth 2.0 / PKCE** - Secure authentication
- **CommunityToolkit.Mvvm** - MVVM framework with source generators
- **CommunityToolkit.Maui** - Enhanced MAUI controls
- **Serilog** - Structured logging
- **System.Text.Json** - JSON serialization for schemas and symbology
- **xUnit, Moq, FluentAssertions** - Comprehensive testing framework

## Project Structure

```
HonuaField/
├── Data/
│   ├── HonuaFieldDatabase.cs        # SQLite database context
│   ├── DatabaseService.cs           # Database initialization
│   └── Repositories/                # Data access layer
├── Models/
│   ├── Feature.cs                   # Feature model
│   ├── Collection.cs                # Collection/layer model
│   ├── Attachment.cs                # File attachment model
│   ├── Change.cs                    # Change tracking model
│   └── Map.cs                       # Map configuration model
├── Services/
│   ├── AuthenticationService.cs     # OAuth 2.0 + PKCE authentication
│   ├── BiometricService.cs          # Biometric authentication
│   ├── ApiClient.cs                 # HTTP client for Honua Server
│   ├── SettingsService.cs           # App settings and preferences
│   └── NavigationService.cs         # Navigation management
├── ViewModels/
│   ├── BaseViewModel.cs             # Base class for ViewModels
│   └── LoginViewModel.cs            # Login screen logic
├── Views/
│   ├── LoginPage.xaml               # Login screen
│   ├── MainPage.xaml                # Main dashboard
│   ├── MapPage.xaml                 # Map view
│   ├── FeatureListPage.xaml         # Feature list
│   ├── FeatureDetailPage.xaml       # Feature details
│   ├── FeatureEditorPage.xaml       # Feature editor
│   ├── SyncPage.xaml                # Synchronization status
│   ├── SettingsPage.xaml            # App settings
│   ├── ProfilePage.xaml             # User profile
│   └── OnboardingPage.xaml          # First-run onboarding
├── Platforms/                       # Platform-specific code
│   ├── Android/                     # Android-specific
│   ├── iOS/                         # iOS-specific
│   ├── Windows/                     # Windows-specific
│   └── MacCatalyst/                 # macOS-specific
└── Resources/                       # Images, fonts, assets
```

## Platform Support

| Platform | Minimum Version | Status |
|----------|----------------|--------|
| iOS | 15.0+ | ✅ Supported |
| Android | API 21+ (Android 5.0) | ✅ Supported |
| Windows | Windows 10 19041+ | ✅ Supported |
| macOS | macOS 15.0+ (Catalyst) | ✅ Supported |

## Development

### Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or JetBrains Rider
- Platform-specific SDKs:
  - **iOS**: Xcode 14+ (macOS required)
  - **Android**: Android SDK 21+
  - **Windows**: Windows 10 SDK 19041+

### Build & Run

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run on specific platform
dotnet build -t:Run -f net8.0-android
dotnet build -t:Run -f net8.0-ios
dotnet build -t:Run -f net8.0-windows10.0.19041.0
dotnet build -t:Run -f net8.0-maccatalyst

# Run tests
cd ../HonuaField.Tests
dotnet test
```

### Configuration

Configure the app by editing `appsettings.json` or using platform-specific configuration:

```json
{
  "HonuaServer": {
    "BaseUrl": "https://your-server.honua.io",
    "ClientId": "honuafield-mobile",
    "Scopes": ["openid", "profile", "honua-api"]
  }
}
```

## Authentication Flow

1. User enters credentials or uses biometric authentication
2. App initiates OAuth 2.0 Authorization Code flow with PKCE
3. Honua Server validates credentials and returns tokens
4. Tokens are securely stored in platform keychain
5. API requests include Bearer token in Authorization header
6. Token refresh happens automatically when expired

## Offline Data Collection Workflow

1. **Download** - Sync features and collections from server
2. **Collect** - Create/edit features while offline
3. **Track** - Changes recorded in local change log
4. **Sync** - When online, push changes to server
5. **Resolve** - Handle conflicts if data changed on server

## Security

- OAuth 2.0 with PKCE (prevents authorization code interception)
- Biometric authentication (Face ID, Touch ID, Fingerprint)
- Secure token storage in platform keychain
- Certificate pinning for API communication
- Encrypted SQLite database for sensitive data

## Testing

### Test Suite Overview
**587 Total Tests** (483 unit tests + 104 integration tests)

### Unit Tests (483 tests)
**Services (13 test classes):**
- AuthenticationService, BiometricService, SettingsService
- ApiClient, NavigationService
- FeaturesService, CollectionsService
- SyncService, ConflictResolutionService
- LocationService, GpsService
- CameraService, OfflineMapService
- SymbologyService, FormBuilderService

**Repositories (5 test classes):**
- FeatureRepository, CollectionRepository, AttachmentRepository
- ChangeRepository, MapRepository

**ViewModels (10 test classes):**
- LoginViewModel, AppShellViewModel, MainViewModel
- OnboardingViewModel, SettingsViewModel, ProfileViewModel
- MapViewModel, FeatureListViewModel, FeatureDetailViewModel, FeatureEditorViewModel

### Integration Tests (104 tests)
**End-to-End Workflows (8 test classes):**
- FeatureCrudIntegrationTests - Complete feature lifecycle
- SyncWorkflowIntegrationTests - Bidirectional sync with conflicts
- OfflineMapIntegrationTests - Map tile downloads and storage
- FormBuilderIntegrationTests - Dynamic form generation
- CameraAttachmentIntegrationTests - Media capture workflows
- GpsTrackingIntegrationTests - Track recording and statistics
- AuthenticationFlowIntegrationTests - OAuth 2.0 flows
- CollectionManagementIntegrationTests - Collection CRUD

**Run Tests:**
```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter "FullyQualifiedName!~Integration"

# Integration tests only
dotnet test --filter "FullyQualifiedName~Integration"
```

## Troubleshooting

### iOS Biometric Authentication Not Working
- Ensure `NSFaceIDUsageDescription` is set in Info.plist
- Device must be enrolled in Face ID or Touch ID
- User must grant permission when prompted

### Android Build Errors
- Ensure Android SDK 21+ is installed
- Update Android SDK Build Tools to latest
- Clean and rebuild: `dotnet clean && dotnet build`

### Connection to Honua Server Failed
- Verify `BaseUrl` in configuration
- Check network connectivity
- Ensure server is reachable and running
- For iOS simulator/Android emulator, use appropriate localhost address:
  - iOS: `http://localhost:8080`
  - Android: `http://10.0.2.2:8080`

## Roadmap

### ✅ Completed (100% Feature Complete)
- ✅ Authentication (OAuth 2.0 + PKCE + Biometrics)
- ✅ Offline SQLite database with spatial support (NetTopologySuite)
- ✅ Complete UI with all views and data binding
- ✅ Feature CRUD operations with dynamic forms
- ✅ Map integration with Mapsui
- ✅ Custom symbology rendering (Simple, UniqueValue, Graduated)
- ✅ Bidirectional synchronization with conflict resolution
- ✅ Camera integration for photo/video attachments
- ✅ GPS track recording with statistics
- ✅ Dynamic form builder from JSON Schema
- ✅ Offline map tiles with multiple sources
- ✅ Comprehensive test suite (587 tests: 483 unit + 104 integration)

### 🚧 Remaining (Nice to Have)
- Background synchronization (iOS/Android background tasks)
- Push notifications for sync events
- Advanced map features (clustering, heatmaps)
- Export features to various formats (KML, GeoJSON, Shapefile)
- Advanced analytics and reporting
- Multi-user collaboration features

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for contribution guidelines.

## License

Elastic License 2.0 - See [LICENSE](../../LICENSE) for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
- **Discussions**: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- **Documentation**: [docs/](../../docs/)
