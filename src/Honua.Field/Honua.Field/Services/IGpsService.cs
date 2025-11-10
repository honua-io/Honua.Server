using NetTopologySuite.Geometries;

namespace HonuaField.Services;

/// <summary>
/// Service for GPS track recording and management
/// Provides breadcrumb trail recording, track statistics, and track persistence
/// </summary>
public interface IGpsService : IDisposable
{
	/// <summary>
	/// Start recording a new GPS track
	/// </summary>
	/// <param name="trackName">Optional name for the track</param>
	Task<string> StartTrackAsync(string? trackName = null);

	/// <summary>
	/// Stop the current GPS track recording
	/// </summary>
	Task StopTrackAsync();

	/// <summary>
	/// Pause the current GPS track recording
	/// </summary>
	Task PauseTrackAsync();

	/// <summary>
	/// Resume a paused GPS track recording
	/// </summary>
	Task ResumeTrackAsync();

	/// <summary>
	/// Get the current recording status
	/// </summary>
	GpsTrackStatus Status { get; }

	/// <summary>
	/// Get the current track ID (null if not recording)
	/// </summary>
	string? CurrentTrackId { get; }

	/// <summary>
	/// Get statistics for the current recording track
	/// </summary>
	Task<GpsTrackStatistics?> GetCurrentTrackStatisticsAsync();

	/// <summary>
	/// Get all recorded tracks
	/// </summary>
	Task<List<GpsTrackInfo>> GetAllTracksAsync();

	/// <summary>
	/// Get a specific track by ID
	/// </summary>
	Task<GpsTrackInfo?> GetTrackAsync(string trackId);

	/// <summary>
	/// Get track points for a specific track
	/// </summary>
	Task<List<GpsTrackPointInfo>> GetTrackPointsAsync(string trackId);

	/// <summary>
	/// Export track as LineString geometry
	/// </summary>
	Task<LineString?> ExportTrackAsLineStringAsync(string trackId);

	/// <summary>
	/// Simplify track using Douglas-Peucker algorithm
	/// </summary>
	/// <param name="trackId">Track to simplify</param>
	/// <param name="tolerance">Simplification tolerance in meters</param>
	Task<LineString?> SimplifyTrackAsync(string trackId, double tolerance = 5.0);

	/// <summary>
	/// Delete a track
	/// </summary>
	Task<bool> DeleteTrackAsync(string trackId);

	/// <summary>
	/// Calculate statistics for a track
	/// </summary>
	Task<GpsTrackStatistics?> CalculateTrackStatisticsAsync(string trackId);

	/// <summary>
	/// Event raised when a new track point is recorded
	/// </summary>
	event EventHandler<TrackPointRecordedEventArgs>? TrackPointRecorded;

	/// <summary>
	/// Event raised when track recording status changes
	/// </summary>
	event EventHandler<TrackStatusChangedEventArgs>? TrackStatusChanged;
}

/// <summary>
/// GPS track recording status
/// </summary>
public enum GpsTrackStatus
{
	Idle,
	Recording,
	Paused
}

/// <summary>
/// Information about a GPS track
/// </summary>
public record GpsTrackInfo
{
	public required string Id { get; init; }
	public string? Name { get; init; }
	public required DateTimeOffset StartTime { get; init; }
	public DateTimeOffset? EndTime { get; init; }
	public int PointCount { get; init; }
	public double TotalDistance { get; init; }
	public TimeSpan Duration { get; init; }
	public double? MaxSpeed { get; init; }
	public double? AverageSpeed { get; init; }
	public double? MinElevation { get; init; }
	public double? MaxElevation { get; init; }
	public double? ElevationGain { get; init; }
	public double? ElevationLoss { get; init; }
}

/// <summary>
/// Information about a GPS track point
/// </summary>
public record GpsTrackPointInfo
{
	public required string Id { get; init; }
	public required string TrackId { get; init; }
	public required double Latitude { get; init; }
	public required double Longitude { get; init; }
	public double? Altitude { get; init; }
	public double? Accuracy { get; init; }
	public double? Speed { get; init; }
	public double? Course { get; init; }
	public required DateTimeOffset Timestamp { get; init; }
	public int SequenceNumber { get; init; }
}

/// <summary>
/// GPS track statistics
/// </summary>
public record GpsTrackStatistics
{
	public required string TrackId { get; init; }
	public int PointCount { get; init; }
	public double TotalDistance { get; init; }
	public TimeSpan Duration { get; init; }
	public TimeSpan MovingTime { get; init; }
	public double? MaxSpeed { get; init; }
	public double? AverageSpeed { get; init; }
	public double? MovingAverageSpeed { get; init; }
	public double? MinElevation { get; init; }
	public double? MaxElevation { get; init; }
	public double? ElevationGain { get; init; }
	public double? ElevationLoss { get; init; }
	public double? AverageAccuracy { get; init; }
}

/// <summary>
/// Event args for track point recorded events
/// </summary>
public class TrackPointRecordedEventArgs : EventArgs
{
	public required GpsTrackPointInfo Point { get; init; }
	public required GpsTrackStatistics Statistics { get; init; }
}

/// <summary>
/// Event args for track status changed events
/// </summary>
public class TrackStatusChangedEventArgs : EventArgs
{
	public required GpsTrackStatus OldStatus { get; init; }
	public required GpsTrackStatus NewStatus { get; init; }
	public string? TrackId { get; init; }
}
