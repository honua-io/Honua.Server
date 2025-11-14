// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Geometries;
using Microsoft.Extensions.Logging;

namespace HonuaField.Services;

/// <summary>
/// Location service implementation using .NET MAUI Geolocation API
/// Provides GPS tracking, permission handling, and location updates
/// </summary>
public class LocationService : ILocationService
{
	private CancellationTokenSource? _trackingCts;
	private Task? _trackingTask;
	private bool _disposed;
	private readonly object _lockObject = new();

	/// <summary>
	/// Event raised when a new location is received during tracking
	/// </summary>
	public event EventHandler<LocationChangedEventArgs>? LocationChanged;

	/// <summary>
	/// Event raised when location tracking encounters an error
	/// </summary>
	public event EventHandler<LocationErrorEventArgs>? LocationError;

	/// <summary>
	/// Check if location tracking is currently active
	/// </summary>
	public bool IsTracking { get; private set; }

	/// <summary>
	/// Check if location services are available on the device
	/// </summary>
	public async Task<bool> IsLocationAvailableAsync()
	{
		try
		{
			// Try to get a quick location with a short timeout
			var request = new GeolocationRequest(GeolocationAccuracy.Lowest, TimeSpan.FromSeconds(1));
			var location = await Geolocation.Default.GetLocationAsync(request);
			return location != null;
		}
		catch (FeatureNotSupportedException)
		{
			_logger.LogWarning("Geolocation is not supported on this device");
			return false;
		}
		catch (FeatureNotEnabledException)
		{
			_logger.LogWarning("Geolocation is not enabled on this device");
			return false;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error checking location availability");
			return false;
		}
	}

	/// <summary>
	/// Check if location permission has been granted
	/// </summary>
	public async Task<LocationPermissionStatus> CheckPermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
			return ConvertPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error checking location permission");
			return LocationPermissionStatus.Unknown;
		}
	}

	/// <summary>
	/// Request location permission from the user
	/// </summary>
	public async Task<LocationPermissionStatus> RequestPermissionAsync()
	{
		try
		{
			var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			return ConvertPermissionStatus(status);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error requesting location permission");
			return LocationPermissionStatus.Denied;
		}
	}

	/// <summary>
	/// Get the current device location
	/// </summary>
	public async Task<LocationInfo?> GetCurrentLocationAsync(
		LocationAccuracy accuracy = LocationAccuracy.Medium,
		TimeSpan? timeout = null)
	{
		try
		{
			// Check permission first
			var permissionStatus = await CheckPermissionAsync();
			if (permissionStatus != LocationPermissionStatus.Granted)
			{
				_logger.LogWarning("Location permission not granted: {PermissionStatus}", permissionStatus);
				return null;
			}

			var request = new GeolocationRequest(
				ConvertAccuracy(accuracy),
				timeout ?? TimeSpan.FromSeconds(10));

			var location = await Geolocation.Default.GetLocationAsync(request);

			if (location == null)
			{
				_logger.LogWarning("No location data received");
				return null;
			}

			return new LocationInfo
			{
				Latitude = location.Latitude,
				Longitude = location.Longitude,
				Altitude = location.Altitude,
				Accuracy = location.Accuracy,
				VerticalAccuracy = location.VerticalAccuracy,
				Speed = location.Speed,
				Course = location.Course,
				Timestamp = location.Timestamp,
				IsFromMockProvider = location.IsFromMockProvider
			};
		}
		catch (FeatureNotSupportedException ex)
		{
			_logger.LogError(ex, "Geolocation not supported");
			return null;
		}
		catch (FeatureNotEnabledException ex)
		{
			_logger.LogError(ex, "Geolocation not enabled");
			return null;
		}
		catch (PermissionException ex)
		{
			_logger.LogError(ex, "Location permission error");
			return null;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting location");
			return null;
		}
	}

	/// <summary>
	/// Start continuous location tracking
	/// </summary>
	public async Task StartTrackingAsync(
		LocationAccuracy accuracy = LocationAccuracy.Medium,
		TimeSpan? minUpdateInterval = null)
	{
		lock (_lockObject)
		{
			if (IsTracking)
			{
				_logger.LogInformation("Location tracking already active");
				return;
			}

			IsTracking = true;
			_trackingCts = new CancellationTokenSource();
		}

		var updateInterval = minUpdateInterval ?? TimeSpan.FromSeconds(5);

		_trackingTask = Task.Run(async () =>
		{
			try
			{
				while (!_trackingCts.Token.IsCancellationRequested)
				{
					try
					{
						var location = await GetCurrentLocationAsync(accuracy, TimeSpan.FromSeconds(10));

						if (location != null)
						{
							LocationChanged?.Invoke(this, new LocationChangedEventArgs { Location = location });
						}
						else
						{
							LocationError?.Invoke(this, new LocationErrorEventArgs
							{
								Message = "Unable to retrieve location"
							});
						}

						await Task.Delay(updateInterval, _trackingCts.Token);
					}
					catch (OperationCanceledException)
					{
						// Expected when stopping
						break;
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Error in tracking loop");
						LocationError?.Invoke(this, new LocationErrorEventArgs
						{
							Message = ex.Message,
							Exception = ex
						});

						// Wait before retrying on error
						await Task.Delay(TimeSpan.FromSeconds(5), _trackingCts.Token);
					}
				}
			}
			catch (OperationCanceledException)
			{
				// Expected when stopping
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Fatal error in location tracking");
				LocationError?.Invoke(this, new LocationErrorEventArgs
				{
					Message = ex.Message,
					Exception = ex
				});
			}
		}, _trackingCts.Token);

		await Task.CompletedTask;
	}

	/// <summary>
	/// Stop continuous location tracking
	/// </summary>
	public async Task StopTrackingAsync()
	{
		lock (_lockObject)
		{
			if (!IsTracking)
			{
				return;
			}

			IsTracking = false;
		}

		try
		{
			_trackingCts?.Cancel();

			if (_trackingTask != null)
			{
				// Wait for tracking task to complete with timeout
				await Task.WhenAny(_trackingTask, Task.Delay(TimeSpan.FromSeconds(5)));
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error stopping location tracking");
		}
		finally
		{
			_trackingCts?.Dispose();
			_trackingCts = null;
			_trackingTask = null;
		}
	}

	/// <summary>
	/// Convert MAUI location to NetTopologySuite Point geometry
	/// </summary>
	public Point LocationToPoint(LocationInfo location)
	{
		// EPSG:4326 (WGS84) - standard GPS coordinate system
		var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

		if (location.Altitude.HasValue)
		{
			// Create 3D point (XYZ)
			return geometryFactory.CreatePoint(
				new CoordinateZ(location.Longitude, location.Latitude, location.Altitude.Value));
		}
		else
		{
			// Create 2D point (XY)
			return geometryFactory.CreatePoint(
				new Coordinate(location.Longitude, location.Latitude));
		}
	}

	/// <summary>
	/// Open device location settings
	/// </summary>
	public async Task OpenLocationSettingsAsync()
	{
		try
		{
			await Launcher.Default.OpenAsync("app-settings:");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error opening location settings");
		}
	}

	/// <summary>
	/// Convert LocationAccuracy enum to MAUI GeolocationAccuracy
	/// </summary>
	private static GeolocationAccuracy ConvertAccuracy(LocationAccuracy accuracy)
	{
		return accuracy switch
		{
			LocationAccuracy.Lowest => GeolocationAccuracy.Lowest,
			LocationAccuracy.Low => GeolocationAccuracy.Low,
			LocationAccuracy.Medium => GeolocationAccuracy.Medium,
			LocationAccuracy.High => GeolocationAccuracy.High,
			LocationAccuracy.Best => GeolocationAccuracy.Best,
			_ => GeolocationAccuracy.Medium
		};
	}

	/// <summary>
	/// Convert MAUI PermissionStatus to LocationPermissionStatus
	/// </summary>
	private static LocationPermissionStatus ConvertPermissionStatus(PermissionStatus status)
	{
		return status switch
		{
			PermissionStatus.Granted => LocationPermissionStatus.Granted,
			PermissionStatus.Denied => LocationPermissionStatus.Denied,
			PermissionStatus.Disabled => LocationPermissionStatus.Restricted,
			PermissionStatus.Restricted => LocationPermissionStatus.Restricted,
			_ => LocationPermissionStatus.Unknown
		};
	}

	/// <summary>
	/// Dispose resources
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Dispose pattern implementation
	/// </summary>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			// Stop tracking if active
			if (IsTracking)
			{
				StopTrackingAsync().GetAwaiter().GetResult();
			}

			_trackingCts?.Dispose();
			_trackingCts = null;
		}

		_disposed = true;
	}
}
