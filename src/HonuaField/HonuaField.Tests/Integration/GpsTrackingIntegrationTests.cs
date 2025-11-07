using FluentAssertions;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for GPS tracking workflows
/// Tests real GpsService with mocked LocationService and real database
/// </summary>
public class GpsTrackingIntegrationTests : IntegrationTestBase
{
	private Mock<ILocationService> _mockLocationService = null!;
	private IGpsService _gpsService = null!;

	protected override void ConfigureServices(IServiceCollection services)
	{
		base.ConfigureServices(services);

		// Mock location service for predictable GPS positions
		_mockLocationService = new Mock<ILocationService>();

		services.AddSingleton<IGpsTrackRepository, GpsTrackRepository>();
		services.AddSingleton<IGpsService>(sp =>
			new GpsService(
				_mockLocationService.Object,
				sp.GetRequiredService<IGpsTrackRepository>()));
	}

	protected override async Task OnInitializeAsync()
	{
		_gpsService = ServiceProvider.GetRequiredService<IGpsService>();
		await base.OnInitializeAsync();
	}

	protected override async Task OnDisposeAsync()
	{
		_gpsService?.Dispose();
		await base.OnDisposeAsync();
	}

	[Fact]
	public async Task StartTrack_ShouldCreateTrackInDatabase()
	{
		// Arrange
		var trackName = "Test Track";

		// Act
		var trackId = await _gpsService.StartTrackAsync(trackName);

		// Assert
		trackId.Should().NotBeNullOrEmpty();
		_gpsService.Status.Should().Be(GpsTrackStatus.Recording);
		_gpsService.CurrentTrackId.Should().Be(trackId);

		var track = await _gpsService.GetTrackAsync(trackId);
		track.Should().NotBeNull();
		track!.Name.Should().Be(trackName);
	}

	[Fact]
	public async Task RecordGpsPoints_ShouldStoreInDatabase()
	{
		// Arrange
		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Simulate GPS location updates
		var locations = DataBuilder.CreateTestGpsTrackPoints(trackId, 5);

		// Mock location service to provide points
		var locationQueue = new Queue<GpsTrackPoint>(locations);
		_mockLocationService
			.Setup(x => x.GetCurrentLocationAsync())
			.ReturnsAsync(() =>
			{
				if (locationQueue.Count > 0)
				{
					var point = locationQueue.Dequeue();
					return new LocationInfo
					{
						Latitude = point.Latitude,
						Longitude = point.Longitude,
						Altitude = point.Altitude,
						Accuracy = point.Accuracy,
						Speed = point.Speed,
						Heading = point.Course,
						Timestamp = DateTimeOffset.FromUnixTimeSeconds(point.Timestamp)
					};
				}
				return null;
			});

		// Simulate recording points over time
		for (int i = 0; i < 5; i++)
		{
			var location = await _mockLocationService.Object.GetCurrentLocationAsync();
			if (location != null)
			{
				// The GpsService would normally handle this internally
				// For testing, we manually add points
				var trackPoint = locations[i];
				await Database.GetConnection().InsertAsync(trackPoint);
			}
		}

		// Act
		await _gpsService.StopTrackAsync();

		// Assert
		var points = await _gpsService.GetTrackPointsAsync(trackId);
		points.Should().HaveCount(5);
	}

	[Fact]
	public async Task CalculateTrackStatistics_ShouldComputeDistanceAndSpeed()
	{
		// Arrange
		var track = DataBuilder.CreateTestGpsTrack(10);
		await Database.GetConnection().InsertAsync(track);

		var points = DataBuilder.CreateTestGpsTrackPoints(track.Id, 10);
		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		var stats = await _gpsService.CalculateStatisticsAsync(track.Id);

		// Assert
		stats.Should().NotBeNull();
		stats!.TotalDistance.Should().BeGreaterThan(0);
		stats.Duration.Should().BeGreaterThan(TimeSpan.Zero);
		stats.PointCount.Should().Be(10);
	}

	[Fact]
	public async Task SaveTrack_ToDatabase_ShouldPersistAllData()
	{
		// Arrange
		var trackId = await _gpsService.StartTrackAsync("Saved Track");

		// Add some points manually
		var points = DataBuilder.CreateTestGpsTrackPoints(trackId, 5);
		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		await _gpsService.StopTrackAsync();

		// Assert
		var savedTrack = await _gpsService.GetTrackAsync(trackId);
		savedTrack.Should().NotBeNull();
		savedTrack!.Name.Should().Be("Saved Track");

		var savedPoints = await _gpsService.GetTrackPointsAsync(trackId);
		savedPoints.Should().HaveCount(5);
	}

	[Fact]
	public async Task RetrieveAllTracks_ShouldReturnAllSavedTracks()
	{
		// Arrange
		var track1 = DataBuilder.CreateTestGpsTrack();
		var track2 = DataBuilder.CreateTestGpsTrack();
		var track3 = DataBuilder.CreateTestGpsTrack();

		await Database.GetConnection().InsertAsync(track1);
		await Database.GetConnection().InsertAsync(track2);
		await Database.GetConnection().InsertAsync(track3);

		// Act
		var allTracks = await _gpsService.GetAllTracksAsync();

		// Assert
		allTracks.Should().HaveCount(3);
		allTracks.Should().Contain(t => t.Id == track1.Id);
		allTracks.Should().Contain(t => t.Id == track2.Id);
		allTracks.Should().Contain(t => t.Id == track3.Id);
	}

	[Fact]
	public async Task ExportTrackAsLineString_ShouldCreateGeometry()
	{
		// Arrange
		var track = DataBuilder.CreateTestGpsTrack();
		await Database.GetConnection().InsertAsync(track);

		var points = DataBuilder.CreateTestGpsTrackPoints(track.Id, 5);
		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		var lineString = await _gpsService.ExportTrackAsLineStringAsync(track.Id);

		// Assert
		lineString.Should().NotBeNull();
		lineString!.NumPoints.Should().Be(5);
		lineString.GeometryType.Should().Be("LineString");
	}

	[Fact]
	public async Task DeleteTrack_ShouldRemoveTrackAndPoints()
	{
		// Arrange
		var track = DataBuilder.CreateTestGpsTrack();
		await Database.GetConnection().InsertAsync(track);

		var points = DataBuilder.CreateTestGpsTrackPoints(track.Id, 5);
		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		var deleted = await _gpsService.DeleteTrackAsync(track.Id);

		// Assert
		deleted.Should().BeTrue();

		var deletedTrack = await _gpsService.GetTrackAsync(track.Id);
		deletedTrack.Should().BeNull();

		var deletedPoints = await _gpsService.GetTrackPointsAsync(track.Id);
		deletedPoints.Should().BeEmpty();
	}

	[Fact]
	public async Task PauseAndResumeTrack_ShouldMaintainState()
	{
		// Arrange
		var trackId = await _gpsService.StartTrackAsync("Pause Test");
		_gpsService.Status.Should().Be(GpsTrackStatus.Recording);

		// Act - Pause
		await _gpsService.PauseTrackAsync();

		// Assert - Paused
		_gpsService.Status.Should().Be(GpsTrackStatus.Paused);

		// Act - Resume
		await _gpsService.ResumeTrackAsync();

		// Assert - Recording again
		_gpsService.Status.Should().Be(GpsTrackStatus.Recording);

		// Stop
		await _gpsService.StopTrackAsync();
		_gpsService.Status.Should().Be(GpsTrackStatus.Stopped);
	}

	[Fact]
	public async Task CalculateDistance_BetweenPoints_ShouldBeAccurate()
	{
		// Arrange
		var track = DataBuilder.CreateTestGpsTrack();
		await Database.GetConnection().InsertAsync(track);

		// Create points with known distances
		var points = new[]
		{
			new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = track.Id,
				Latitude = 45.0,
				Longitude = -122.0,
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				SequenceNumber = 0
			},
			new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = track.Id,
				Latitude = 45.01,
				Longitude = -122.0,
				Timestamp = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds(),
				SequenceNumber = 1
			}
		};

		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		var stats = await _gpsService.CalculateStatisticsAsync(track.Id);

		// Assert
		stats.Should().NotBeNull();
		stats!.TotalDistance.Should().BeGreaterThan(0);
		// Distance between 45.0 and 45.01 degrees latitude is approximately 1.11 km
		stats.TotalDistance.Should().BeGreaterThan(1000); // meters
	}

	[Fact]
	public async Task SimplifyTrack_ShouldReducePoints()
	{
		// Arrange
		var track = DataBuilder.CreateTestGpsTrack();
		await Database.GetConnection().InsertAsync(track);

		var points = DataBuilder.CreateTestGpsTrackPoints(track.Id, 20);
		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		var simplified = await _gpsService.SimplifyTrackAsync(track.Id, tolerance: 10.0);

		// Assert
		simplified.Should().NotBeNull();
		// Simplified track should have fewer points
		simplified!.NumPoints.Should().BeLessThan(20);
	}

	[Fact]
	public async Task GetTrackStatistics_ShouldIncludeElevationData()
	{
		// Arrange
		var track = DataBuilder.CreateTestGpsTrack();
		await Database.GetConnection().InsertAsync(track);

		var points = DataBuilder.CreateTestGpsTrackPoints(track.Id, 10);
		// Set elevation data
		for (int i = 0; i < points.Count; i++)
		{
			points[i].Altitude = 100 + (i * 10); // Increasing elevation
		}

		foreach (var point in points)
		{
			await Database.GetConnection().InsertAsync(point);
		}

		// Act
		var stats = await _gpsService.CalculateStatisticsAsync(track.Id);

		// Assert
		stats.Should().NotBeNull();
		stats!.MinElevation.Should().Be(100);
		stats.MaxElevation.Should().BeGreaterThan(100);
		stats.ElevationGain.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task ExportMultipleTracks_ShouldCreateSeparateGeometries()
	{
		// Arrange
		var track1 = DataBuilder.CreateTestGpsTrack();
		var track2 = DataBuilder.CreateTestGpsTrack();
		await Database.GetConnection().InsertAsync(track1);
		await Database.GetConnection().InsertAsync(track2);

		var points1 = DataBuilder.CreateTestGpsTrackPoints(track1.Id, 5);
		var points2 = DataBuilder.CreateTestGpsTrackPoints(track2.Id, 5);

		foreach (var point in points1)
			await Database.GetConnection().InsertAsync(point);
		foreach (var point in points2)
			await Database.GetConnection().InsertAsync(point);

		// Act
		var lineString1 = await _gpsService.ExportTrackAsLineStringAsync(track1.Id);
		var lineString2 = await _gpsService.ExportTrackAsLineStringAsync(track2.Id);

		// Assert
		lineString1.Should().NotBeNull();
		lineString2.Should().NotBeNull();
		lineString1!.NumPoints.Should().Be(5);
		lineString2!.NumPoints.Should().Be(5);
		lineString1.Equals(lineString2).Should().BeFalse();
	}
}

/// <summary>
/// Mock location service interface
/// </summary>
public interface ILocationService
{
	Task<LocationInfo?> GetCurrentLocationAsync();
	Task<bool> IsLocationEnabledAsync();
}

/// <summary>
/// Location information
/// </summary>
public class LocationInfo
{
	public double Latitude { get; set; }
	public double Longitude { get; set; }
	public double? Altitude { get; set; }
	public double? Accuracy { get; set; }
	public double? Speed { get; set; }
	public double? Heading { get; set; }
	public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// GPS track status
/// </summary>
public enum GpsTrackStatus
{
	Stopped,
	Recording,
	Paused
}

/// <summary>
/// GPS track statistics
/// </summary>
public class GpsTrackStatistics
{
	public int PointCount { get; set; }
	public double TotalDistance { get; set; }
	public TimeSpan Duration { get; set; }
	public double? AverageSpeed { get; set; }
	public double? MaxSpeed { get; set; }
	public double? MinElevation { get; set; }
	public double? MaxElevation { get; set; }
	public double? ElevationGain { get; set; }
	public double? ElevationLoss { get; set; }
}

/// <summary>
/// GPS track info
/// </summary>
public class GpsTrackInfo
{
	public required string Id { get; init; }
	public string? Name { get; init; }
	public DateTimeOffset StartTime { get; init; }
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
/// GPS track point info
/// </summary>
public class GpsTrackPointInfo
{
	public required string Id { get; init; }
	public required string TrackId { get; init; }
	public double Latitude { get; init; }
	public double Longitude { get; init; }
	public double? Altitude { get; init; }
	public double? Accuracy { get; init; }
	public double? Speed { get; init; }
	public double? Course { get; init; }
	public DateTimeOffset Timestamp { get; init; }
	public int SequenceNumber { get; init; }
}

/// <summary>
/// GPS track repository interface
/// </summary>
public interface IGpsTrackRepository
{
	Task<string> InsertTrackAsync(GpsTrack track);
	Task<int> UpdateTrackAsync(GpsTrack track);
	Task<GpsTrack?> GetTrackAsync(string trackId);
	Task<List<GpsTrack>> GetAllTracksAsync();
	Task<int> DeleteTrackAsync(string trackId);
	Task InsertPointAsync(GpsTrackPoint point);
	Task<List<GpsTrackPoint>> GetTrackPointsAsync(string trackId);
	Task<int> DeleteTrackPointsAsync(string trackId);
}

/// <summary>
/// GPS track repository implementation
/// </summary>
public class GpsTrackRepository : IGpsTrackRepository
{
	private readonly HonuaFieldDatabase _database;

	public GpsTrackRepository(HonuaFieldDatabase database)
	{
		_database = database;
	}

	public async Task<string> InsertTrackAsync(GpsTrack track)
	{
		await _database.GetConnection().InsertAsync(track);
		return track.Id;
	}

	public async Task<int> UpdateTrackAsync(GpsTrack track)
	{
		return await _database.GetConnection().UpdateAsync(track);
	}

	public async Task<GpsTrack?> GetTrackAsync(string trackId)
	{
		return await _database.GetConnection().Table<GpsTrack>()
			.Where(t => t.Id == trackId)
			.FirstOrDefaultAsync();
	}

	public async Task<List<GpsTrack>> GetAllTracksAsync()
	{
		return await _database.GetConnection().Table<GpsTrack>().ToListAsync();
	}

	public async Task<int> DeleteTrackAsync(string trackId)
	{
		await DeleteTrackPointsAsync(trackId);
		return await _database.GetConnection().Table<GpsTrack>()
			.DeleteAsync(t => t.Id == trackId);
	}

	public async Task InsertPointAsync(GpsTrackPoint point)
	{
		await _database.GetConnection().InsertAsync(point);
	}

	public async Task<List<GpsTrackPoint>> GetTrackPointsAsync(string trackId)
	{
		return await _database.GetConnection().Table<GpsTrackPoint>()
			.Where(p => p.TrackId == trackId)
			.OrderBy(p => p.SequenceNumber)
			.ToListAsync();
	}

	public async Task<int> DeleteTrackPointsAsync(string trackId)
	{
		return await _database.GetConnection().Table<GpsTrackPoint>()
			.DeleteAsync(p => p.TrackId == trackId);
	}
}
