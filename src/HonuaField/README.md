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

### Data Collection
- **Feature Management** - Create, view, edit, and delete features
- **Offline Storage** - SQLite database for offline data collection
- **Attachments** - Associate photos and files with features
- **Collections** - Organize features into collections/layers
- **Change Tracking** - Track all changes for synchronization

### Mapping
- **Map View** - Display features on an interactive map
- **GPS Location** - Current location tracking
- **Feature Visualization** - View features with proper symbology

### Synchronization
- **Offline-First** - Work without connectivity
- **Background Sync** - Automatic synchronization when online
- **Conflict Resolution** - Handle conflicts when syncing changes
- **Change Log** - Track all local modifications

## Technology Stack

- **.NET 8 / MAUI** - Cross-platform framework
- **SQLite** - Local database for offline storage
- **OAuth 2.0 / PKCE** - Secure authentication
- **Platform APIs** - Native biometric authentication
- **Xamarin.Forms / MAUI** - UI framework

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

See [HonuaField.Tests/README.md](../HonuaField.Tests/README.md) for testing documentation.

**Test Coverage:**
- AuthenticationService - OAuth 2.0 + PKCE flow
- BiometricService - Platform biometric APIs
- SettingsService - Secure storage
- ApiClient - HTTP communication
- LoginViewModel - UI logic

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

### Current (Sprint 1-2)
- ✅ Authentication (OAuth 2.0 + PKCE + Biometrics)
- ✅ Offline SQLite database
- ✅ Basic UI structure
- 🚧 Feature CRUD operations
- 🚧 Map integration
- 🚧 Synchronization

### Future
- Camera integration for attachments
- GPS track recording
- Form builder for custom data collection
- Advanced mapping features
- Offline map tiles
- Background synchronization

## Contributing

See [CONTRIBUTING.md](../../CONTRIBUTING.md) for contribution guidelines.

## License

Elastic License 2.0 - See [LICENSE](../../LICENSE) for details.

## Support

- **Issues**: [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
- **Discussions**: [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
- **Documentation**: [docs/](../../docs/)
