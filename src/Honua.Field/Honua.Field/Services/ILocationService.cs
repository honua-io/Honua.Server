// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Geometries;

namespace HonuaField.Services;

/// <summary>
/// Service for handling device location and GPS functionality
/// Uses MAUI Geolocation APIs for cross-platform location services
/// </summary>
public interface ILocationService : IDisposable
{
	/// <summary>
	/// Check if location services are available on the device
	/// </summary>
	Task<bool> IsLocationAvailableAsync();

	/// <summary>
	/// Check if location permission has been granted
	/// </summary>
	Task<LocationPermissionStatus> CheckPermissionAsync();

	/// <summary>
	/// Request location permission from the user
	/// </summary>
	Task<LocationPermissionStatus> RequestPermissionAsync();

	/// <summary>
	/// Get the current device location
	/// </summary>
	/// <param name="accuracy">Desired accuracy level</param>
	/// <param name="timeout">Timeout in milliseconds</param>
	Task<LocationInfo?> GetCurrentLocationAsync(
		LocationAccuracy accuracy = LocationAccuracy.Medium,
		TimeSpan? timeout = null);

	/// <summary>
	/// Start continuous location tracking
	/// </summary>
	/// <param name="accuracy">Desired accuracy level</param>
	/// <param name="minUpdateInterval">Minimum interval between updates</param>
	Task StartTrackingAsync(
		LocationAccuracy accuracy = LocationAccuracy.Medium,
		TimeSpan? minUpdateInterval = null);

	/// <summary>
	/// Stop continuous location tracking
	/// </summary>
	Task StopTrackingAsync();

	/// <summary>
	/// Check if location tracking is currently active
	/// </summary>
	bool IsTracking { get; }

	/// <summary>
	/// Event raised when a new location is received during tracking
	/// </summary>
	event EventHandler<LocationChangedEventArgs>? LocationChanged;

	/// <summary>
	/// Event raised when location tracking encounters an error
	/// </summary>
	event EventHandler<LocationErrorEventArgs>? LocationError;

	/// <summary>
	/// Convert MAUI location to NetTopologySuite Point geometry
	/// </summary>
	Point LocationToPoint(LocationInfo location);

	/// <summary>
	/// Open device location settings
	/// </summary>
	Task OpenLocationSettingsAsync();
}

/// <summary>
/// Location information from device GPS
/// </summary>
public record LocationInfo
{
	public required double Latitude { get; init; }
	public required double Longitude { get; init; }
	public double? Altitude { get; init; }
	public double? Accuracy { get; init; }
	public double? VerticalAccuracy { get; init; }
	public double? Speed { get; init; }
	public double? Course { get; init; }
	public required DateTimeOffset Timestamp { get; init; }
	public bool IsFromMockProvider { get; init; }
}

/// <summary>
/// Location accuracy levels
/// </summary>
public enum LocationAccuracy
{
	/// <summary>
	/// Lowest accuracy, best for battery life (~3000m)
	/// </summary>
	Lowest,

	/// <summary>
	/// Low accuracy (~1000m)
	/// </summary>
	Low,

	/// <summary>
	/// Medium accuracy (~100m)
	/// </summary>
	Medium,

	/// <summary>
	/// High accuracy (~10m)
	/// </summary>
	High,

	/// <summary>
	/// Best accuracy available (~0-5m), uses most battery
	/// </summary>
	Best
}

/// <summary>
/// Location permission status
/// </summary>
public enum LocationPermissionStatus
{
	Unknown,
	Denied,
	Granted,
	Restricted
}

/// <summary>
/// Event args for location changed events
/// </summary>
public class LocationChangedEventArgs : EventArgs
{
	public required LocationInfo Location { get; init; }
}

/// <summary>
/// Event args for location error events
/// </summary>
public class LocationErrorEventArgs : EventArgs
{
	public required string Message { get; init; }
	public Exception? Exception { get; init; }
}
