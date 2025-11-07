# Platform-Specific Configurations

This directory contains platform-specific configurations for iOS and Android.

## Android (Platforms/Android)

### AndroidManifest.xml
Location permissions required for GPS functionality:
- `ACCESS_COARSE_LOCATION` - Network-based location (low accuracy)
- `ACCESS_FINE_LOCATION` - GPS-based location (high accuracy)
- `ACCESS_BACKGROUND_LOCATION` - Background location tracking (optional, commented out by default)
- `WAKE_LOCK` - Keep device awake during GPS tracking

### Required Permissions
The app requests location permissions at runtime using MAUI's Permissions API.

## iOS (Platforms/iOS)

### Info.plist
Location usage descriptions required by Apple:
- `NSLocationWhenInUseUsageDescription` - Location access while app is in use
- `NSLocationAlwaysUsageDescription` - Background location access (iOS 10)
- `NSLocationAlwaysAndWhenInUseUsageDescription` - Background location access (iOS 11+)
- `NSLocationTemporaryUsageDescriptionDictionary` - Precise location (iOS 14+)

### Background Location (Optional)
To enable background GPS tracking, uncomment `UIBackgroundModes` in Info.plist:
```xml
<key>UIBackgroundModes</key>
<array>
	<string>location</string>
</array>
```

## Testing Location Services

### iOS Simulator
- Use Debug > Location menu to simulate different locations
- "Custom Location" allows manual coordinate input
- "City Run" and "Freeway Drive" simulate movement

### Android Emulator
- Use Extended Controls (⋮ button) > Location
- Set custom GPS coordinates or load GPX/KML routes
- Use "Route" feature to simulate movement

### Physical Devices
- Enable location services in device settings
- Grant location permission when prompted
- For best GPS accuracy, test outdoors with clear sky view

## Battery Optimization

### Best Practices
1. Use appropriate accuracy levels:
   - `LocationAccuracy.Medium` for general features (~100m)
   - `LocationAccuracy.High` for precise mapping (~10m)
   - `LocationAccuracy.Best` only when necessary (~5m)

2. Adjust update intervals:
   - 5-10 seconds for active tracking
   - 30-60 seconds for passive monitoring
   - Stop tracking when not needed

3. Background location:
   - Only use when absolutely necessary
   - Clearly communicate to users why it's needed
   - Provide option to disable in app settings
