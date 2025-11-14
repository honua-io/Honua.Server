// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Geometries;
using NetTopologySuite.Simplify;
using HonuaField.Models;
using Microsoft.Extensions.Logging;

namespace HonuaField.Services;

/// <summary>
/// GPS service implementation for track recording and management
/// Integrates with LocationService for GPS data collection
/// </summary>
public class GpsService : IGpsService
{
	private readonly ILocationService _locationService;
	private readonly IDatabaseService _databaseService;
	private bool _disposed;
	private readonly object _lockObject = new();

	private string? _currentTrackId;
	private GpsTrackStatus _status = GpsTrackStatus.Idle;
	private GpsTrackPoint? _lastTrackPoint;
	private readonly List<GpsTrackPoint> _pendingPoints = new();

	/// <summary>
	/// Event raised when a new track point is recorded
	/// </summary>
	public event EventHandler<TrackPointRecordedEventArgs>? TrackPointRecorded;

	/// <summary>
	/// Event raised when track recording status changes
	/// </summary>
	public event EventHandler<TrackStatusChangedEventArgs>? TrackStatusChanged;

	/// <summary>
	/// Get the current recording status
	/// </summary>
	public GpsTrackStatus Status
	{
		get
		{
			lock (_lockObject)
			{
				return _status;
			}
		}
	}

	/// <summary>
	/// Get the current track ID (null if not recording)
	/// </summary>
	public string? CurrentTrackId
	{
		get
		{
			lock (_lockObject)
			{
				return _currentTrackId;
			}
		}
	}

	public GpsService(ILocationService locationService, IDatabaseService databaseService, ILogger<GpsService> logger)
	{
		_logger = logger;
		_locationService = locationService;
		_databaseService = databaseService;

		// Subscribe to location updates
		_locationService.LocationChanged += OnLocationChanged;
	}

	/// <summary>
	/// Start recording a new GPS track
	/// </summary>
	public async Task<string> StartTrackAsync(string? trackName = null)
	{
		lock (_lockObject)
		{
			if (_status != GpsTrackStatus.Idle)
			{
				throw new InvalidOperationException("A track is already being recorded");
			}

			_currentTrackId = Guid.NewGuid().ToString();
			_lastTrackPoint = null;
			_pendingPoints.Clear();
			
			ChangeStatus(GpsTrackStatus.Recording);
		}

		// Create track in database
		var track = new GpsTrack
		{
			Id = _currentTrackId,
			Name = trackName,
			StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Status = "Recording"
		};

		var db = _databaseService.GetDatabase().GetConnection();
		await db.InsertAsync(track);

		// Start location tracking
		await _locationService.StartTrackingAsync(
			LocationAccuracy.High,
			TimeSpan.FromSeconds(2));

		_logger.LogInformation("Started GPS track recording: {TrackId}", _currentTrackId);

		return _currentTrackId;
	}

	/// <summary>
	/// Stop the current GPS track recording
	/// </summary>
	public async Task StopTrackAsync()
	{
		string? trackId;
		
		lock (_lockObject)
		{
			if (_status == GpsTrackStatus.Idle)
			{
				return;
			}

			trackId = _currentTrackId;
			ChangeStatus(GpsTrackStatus.Idle);
			_currentTrackId = null;
			_lastTrackPoint = null;
		}

		// Stop location tracking
		await _locationService.StopTrackingAsync();

		if (trackId != null)
		{
			// Save any pending points
			await SavePendingPointsAsync();

			// Update track in database
			var db = _databaseService.GetDatabase().GetConnection();
			var track = await db.Table<GpsTrack>().Where(t => t.Id == trackId).FirstOrDefaultAsync();
			
			if (track != null)
			{
				track.EndTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				track.Status = "Completed";
				track.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
				
				// Calculate final statistics
				var stats = await CalculateTrackStatisticsAsync(trackId);
				if (stats != null)
				{
					track.TotalDistance = stats.TotalDistance;
					track.DurationSeconds = (long)stats.Duration.TotalSeconds;
					track.MaxSpeed = stats.MaxSpeed;
					track.AverageSpeed = stats.AverageSpeed;
					track.MinElevation = stats.MinElevation;
					track.MaxElevation = stats.MaxElevation;
					track.ElevationGain = stats.ElevationGain;
					track.ElevationLoss = stats.ElevationLoss;
				}

				await db.UpdateAsync(track);
			}

			_logger.LogInformation("Stopped GPS track recording: {TrackId}", trackId);
		}
	}

	/// <summary>
	/// Pause the current GPS track recording
	/// </summary>
	public async Task PauseTrackAsync()
	{
		lock (_lockObject)
		{
			if (_status != GpsTrackStatus.Recording)
			{
				return;
			}

			ChangeStatus(GpsTrackStatus.Paused);
		}

		await _locationService.StopTrackingAsync();

		_logger.LogInformation("Paused GPS track recording: {TrackId}", _currentTrackId);
	}

	/// <summary>
	/// Resume a paused GPS track recording
	/// </summary>
	public async Task ResumeTrackAsync()
	{
		lock (_lockObject)
		{
			if (_status != GpsTrackStatus.Paused)
			{
				return;
			}

			ChangeStatus(GpsTrackStatus.Recording);
		}

		await _locationService.StartTrackingAsync(
			LocationAccuracy.High,
			TimeSpan.FromSeconds(2));

		_logger.LogInformation("Resumed GPS track recording: {TrackId}", _currentTrackId);
	}

	/// <summary>
	/// Get statistics for the current recording track
	/// </summary>
	public async Task<GpsTrackStatistics?> GetCurrentTrackStatisticsAsync()
	{
		var trackId = CurrentTrackId;
		if (trackId == null)
		{
			return null;
		}

		return await CalculateTrackStatisticsAsync(trackId);
	}

	/// <summary>
	/// Get all recorded tracks
	/// </summary>
	public async Task<List<GpsTrackInfo>> GetAllTracksAsync()
	{
		var db = _databaseService.GetDatabase().GetConnection();
		var tracks = await db.Table<GpsTrack>()
			.OrderByDescending(t => t.StartTime)
			.ToListAsync();

		return tracks.Select(t => t.ToInfo()).ToList();
	}

	/// <summary>
	/// Get a specific track by ID
	/// </summary>
	public async Task<GpsTrackInfo?> GetTrackAsync(string trackId)
	{
		var db = _databaseService.GetDatabase().GetConnection();
		var track = await db.Table<GpsTrack>()
			.Where(t => t.Id == trackId)
			.FirstOrDefaultAsync();

		return track?.ToInfo();
	}

	/// <summary>
	/// Get track points for a specific track
	/// </summary>
	public async Task<List<GpsTrackPointInfo>> GetTrackPointsAsync(string trackId)
	{
		var db = _databaseService.GetDatabase().GetConnection();
		var points = await db.Table<GpsTrackPoint>()
			.Where(p => p.TrackId == trackId)
			.OrderBy(p => p.SequenceNumber)
			.ToListAsync();

		return points.Select(p => p.ToInfo()).ToList();
	}

	/// <summary>
	/// Export track as LineString geometry
	/// </summary>
	public async Task<LineString?> ExportTrackAsLineStringAsync(string trackId)
	{
		var points = await GetTrackPointsAsync(trackId);
		
		if (points.Count < 2)
		{
			return null;
		}

		var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
		var coordinates = points
			.Select(p => new Coordinate(p.Longitude, p.Latitude))
			.ToArray();

		return geometryFactory.CreateLineString(coordinates);
	}

	/// <summary>
	/// Simplify track using Douglas-Peucker algorithm
	/// </summary>
	public async Task<LineString?> SimplifyTrackAsync(string trackId, double tolerance = 5.0)
	{
		var lineString = await ExportTrackAsLineStringAsync(trackId);
		
		if (lineString == null)
		{
			return null;
		}

		// Convert tolerance from meters to degrees (approximate)
		// At equator, 1 degree ≈ 111,320 meters
		var toleranceDegrees = tolerance / 111320.0;

		var simplifier = new DouglasPeuckerSimplifier(lineString)
		{
			DistanceTolerance = toleranceDegrees
		};

		return (LineString)simplifier.GetResultGeometry();
	}

	/// <summary>
	/// Delete a track
	/// </summary>
	public async Task<bool> DeleteTrackAsync(string trackId)
	{
		try
		{
			var db = _databaseService.GetDatabase().GetConnection();

			// Delete track points first
			await db.ExecuteAsync("DELETE FROM gps_track_points WHERE track_id = ?", trackId);

			// Delete track
			var deletedCount = await db.ExecuteAsync("DELETE FROM gps_tracks WHERE id = ?", trackId);

			_logger.LogInformation("Deleted GPS track: {TrackId}", trackId);

			return deletedCount > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting track");
			return false;
		}
	}

	/// <summary>
	/// Calculate statistics for a track
	/// </summary>
	public async Task<GpsTrackStatistics?> CalculateTrackStatisticsAsync(string trackId)
	{
		try
		{
			var db = _databaseService.GetDatabase().GetConnection();
			var points = await db.Table<GpsTrackPoint>()
				.Where(p => p.TrackId == trackId)
				.OrderBy(p => p.SequenceNumber)
				.ToListAsync();

			if (points.Count == 0)
			{
				return null;
			}

			var track = await db.Table<GpsTrack>()
				.Where(t => t.Id == trackId)
				.FirstOrDefaultAsync();

			if (track == null)
			{
				return null;
			}

			// Calculate total distance
			double totalDistance = points.Sum(p => p.DistanceFromPrevious);

			// Calculate duration
			var startTime = DateTimeOffset.FromUnixTimeSeconds(track.StartTime);
			var endTime = track.EndTime.HasValue
				? DateTimeOffset.FromUnixTimeSeconds(track.EndTime.Value)
				: DateTimeOffset.UtcNow;
			var duration = endTime - startTime;

			// Calculate moving time (exclude stops)
			long movingTimeSeconds = 0;
			foreach (var point in points)
			{
				// Consider moving if speed > 0.5 m/s (1.8 km/h)
				if (point.Speed.HasValue && point.Speed.Value > 0.5)
				{
					movingTimeSeconds += point.TimeFromPrevious;
				}
			}

			// Calculate speeds
			var speedPoints = points.Where(p => p.Speed.HasValue).ToList();
			double? maxSpeed = speedPoints.Any() ? speedPoints.Max(p => p.Speed) : null;
			double? averageSpeed = duration.TotalSeconds > 0 ? totalDistance / duration.TotalSeconds : null;
			double? movingAverageSpeed = movingTimeSeconds > 0 ? totalDistance / movingTimeSeconds : null;

			// Calculate elevation statistics
			var elevationPoints = points.Where(p => p.Altitude.HasValue).ToList();
			double? minElevation = elevationPoints.Any() ? elevationPoints.Min(p => p.Altitude) : null;
			double? maxElevation = elevationPoints.Any() ? elevationPoints.Max(p => p.Altitude) : null;

			// Calculate elevation gain/loss
			double elevationGain = 0;
			double elevationLoss = 0;
			for (int i = 1; i < elevationPoints.Count; i++)
			{
				var diff = elevationPoints[i].Altitude!.Value - elevationPoints[i - 1].Altitude!.Value;
				if (diff > 0)
					elevationGain += diff;
				else
					elevationLoss += Math.Abs(diff);
			}

			// Calculate average accuracy
			var accuracyPoints = points.Where(p => p.Accuracy.HasValue).ToList();
			double? averageAccuracy = accuracyPoints.Any() ? accuracyPoints.Average(p => p.Accuracy) : null;

			return new GpsTrackStatistics
			{
				TrackId = trackId,
				PointCount = points.Count,
				TotalDistance = totalDistance,
				Duration = duration,
				MovingTime = TimeSpan.FromSeconds(movingTimeSeconds),
				MaxSpeed = maxSpeed,
				AverageSpeed = averageSpeed,
				MovingAverageSpeed = movingAverageSpeed,
				MinElevation = minElevation,
				MaxElevation = maxElevation,
				ElevationGain = elevationPoints.Count > 1 ? elevationGain : null,
				ElevationLoss = elevationPoints.Count > 1 ? elevationLoss : null,
				AverageAccuracy = averageAccuracy
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error calculating track statistics");
			return null;
		}
	}

	/// <summary>
	/// Handle location changed events from LocationService
	/// </summary>
	private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
	{
		Task.Run(async () => await ProcessLocationUpdateAsync(e.Location));
	}

	/// <summary>
	/// Process a location update and add to current track
	/// </summary>
	private async Task ProcessLocationUpdateAsync(LocationInfo location)
	{
		string? trackId;
		GpsTrackStatus status;

		lock (_lockObject)
		{
			trackId = _currentTrackId;
			status = _status;
		}

		// Only process if recording
		if (status != GpsTrackStatus.Recording || trackId == null)
		{
			return;
		}

		try
		{
			var db = _databaseService.GetDatabase().GetConnection();
			
			// Get current sequence number
			var pointCount = await db.Table<GpsTrackPoint>()
				.Where(p => p.TrackId == trackId)
				.CountAsync();

			// Calculate distance and time from previous point
			double distanceFromPrevious = 0;
			long timeFromPrevious = 0;

			if (_lastTrackPoint != null)
			{
				distanceFromPrevious = CalculateDistance(
					_lastTrackPoint.Latitude, _lastTrackPoint.Longitude,
					location.Latitude, location.Longitude);

				timeFromPrevious = location.Timestamp.ToUnixTimeSeconds() - _lastTrackPoint.Timestamp;
			}

			// Create track point
			var trackPoint = new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = trackId,
				Latitude = location.Latitude,
				Longitude = location.Longitude,
				Altitude = location.Altitude,
				Accuracy = location.Accuracy,
				Speed = location.Speed,
				Course = location.Course,
				Timestamp = location.Timestamp.ToUnixTimeSeconds(),
				SequenceNumber = pointCount,
				DistanceFromPrevious = distanceFromPrevious,
				TimeFromPrevious = timeFromPrevious
			};

			// Save to database
			await db.InsertAsync(trackPoint);

			// Update track point count
			await db.ExecuteAsync(
				"UPDATE gps_tracks SET point_count = point_count + 1, updated_at = ? WHERE id = ?",
				DateTimeOffset.UtcNow.ToUnixTimeSeconds(), trackId);

			lock (_lockObject)
			{
				_lastTrackPoint = trackPoint;
			}

			// Calculate current statistics
			var stats = await CalculateTrackStatisticsAsync(trackId);
			if (stats != null)
			{
				TrackPointRecorded?.Invoke(this, new TrackPointRecordedEventArgs
				{
					Point = trackPoint.ToInfo(),
					Statistics = stats
				});
			}

			_logger.LogInformation("Recorded track point: {SequenceNumber} at ({Latitude}, {Longitude})",
				trackPoint.SequenceNumber, location.Latitude, location.Longitude);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing location update");
		}
	}

	/// <summary>
	/// Save any pending points to database
	/// </summary>
	private async Task SavePendingPointsAsync()
	{
		List<GpsTrackPoint> points;
		
		lock (_lockObject)
		{
			points = new List<GpsTrackPoint>(_pendingPoints);
			_pendingPoints.Clear();
		}

		if (points.Count == 0)
		{
			return;
		}

		try
		{
			var db = _databaseService.GetDatabase().GetConnection();
			await db.InsertAllAsync(points);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error saving pending points");
		}
	}

	/// <summary>
	/// Change track recording status and raise event
	/// </summary>
	private void ChangeStatus(GpsTrackStatus newStatus)
	{
		var oldStatus = _status;
		_status = newStatus;

		TrackStatusChanged?.Invoke(this, new TrackStatusChangedEventArgs
		{
			OldStatus = oldStatus,
			NewStatus = newStatus,
			TrackId = _currentTrackId
		});
	}

	/// <summary>
	/// Calculate distance between two GPS points using Haversine formula
	/// </summary>
	private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
	{
		const double earthRadius = 6371000; // meters

		var dLat = ToRadians(lat2 - lat1);
		var dLon = ToRadians(lon2 - lon1);

		var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
				Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
				Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

		var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

		return earthRadius * c;
	}

	/// <summary>
	/// Convert degrees to radians
	/// </summary>
	private static double ToRadians(double degrees)
	{
		return degrees * Math.PI / 180.0;
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
			// Stop recording if active
			if (Status != GpsTrackStatus.Idle)
			{
				StopTrackAsync().GetAwaiter().GetResult();
			}

			// Unsubscribe from location updates
			_locationService.LocationChanged -= OnLocationChanged;
		}

		_disposed = true;
	}
}
