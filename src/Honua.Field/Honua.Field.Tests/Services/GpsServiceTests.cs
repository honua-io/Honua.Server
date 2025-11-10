using FluentAssertions;
using HonuaField.Data;
using HonuaField.Models;
using HonuaField.Services;
using Moq;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for GpsService
/// Tests GPS track recording, statistics calculation, and track management
/// </summary>
public class GpsServiceTests : IAsyncLifetime
{
	private readonly Mock<ILocationService> _mockLocationService;
	private readonly Mock<IDatabaseService> _mockDatabaseService;
	private readonly HonuaFieldDatabase _testDatabase;
	private readonly string _testDatabasePath;
	private GpsService _gpsService = null!;

	public GpsServiceTests()
	{
		_mockLocationService = new Mock<ILocationService>();
		_mockDatabaseService = new Mock<IDatabaseService>();

		// Create test database in temp directory
		_testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_honuafield_{Guid.NewGuid()}.db");
		_testDatabase = new HonuaFieldDatabase(_testDatabasePath);

		// Setup mock to return test database
		_mockDatabaseService.Setup(x => x.GetDatabase()).Returns(_testDatabase);
	}

	public async Task InitializeAsync()
	{
		await _testDatabase.InitializeAsync();
		_gpsService = new GpsService(_mockLocationService.Object, _mockDatabaseService.Object);
	}

	public async Task DisposeAsync()
	{
		_gpsService?.Dispose();
		await _testDatabase.CloseAsync();

		// Delete test database file
		if (File.Exists(_testDatabasePath))
		{
			File.Delete(_testDatabasePath);
		}
	}

	[Fact]
	public void Status_ShouldBeIdle_Initially()
	{
		// Assert
		_gpsService.Status.Should().Be(GpsTrackStatus.Idle);
	}

	[Fact]
	public void CurrentTrackId_ShouldBeNull_Initially()
	{
		// Assert
		_gpsService.CurrentTrackId.Should().BeNull();
	}

	[Fact]
	public async Task StartTrackAsync_ShouldStartTracking()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		// Act
		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Assert
		trackId.Should().NotBeNullOrEmpty();
		_gpsService.Status.Should().Be(GpsTrackStatus.Recording);
		_gpsService.CurrentTrackId.Should().Be(trackId);

		_mockLocationService.Verify(
			x => x.StartTrackingAsync(LocationAccuracy.High, It.IsAny<TimeSpan?>()),
			Times.Once);
	}

	[Fact]
	public async Task StartTrackAsync_ShouldCreateTrackInDatabase()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		// Act
		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Assert
		var db = _testDatabase.GetConnection();
		var track = await db.Table<GpsTrack>().Where(t => t.Id == trackId).FirstOrDefaultAsync();
		
		track.Should().NotBeNull();
		track!.Name.Should().Be("Test Track");
		track.Status.Should().Be("Recording");
	}

	[Fact]
	public async Task StartTrackAsync_WhenAlreadyRecording_ShouldThrow()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		await _gpsService.StartTrackAsync("Track 1");

		// Act
		Func<Task> act = async () => await _gpsService.StartTrackAsync("Track 2");

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task StopTrackAsync_ShouldStopTracking()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		await _gpsService.StartTrackAsync("Test Track");

		// Act
		await _gpsService.StopTrackAsync();

		// Assert
		_gpsService.Status.Should().Be(GpsTrackStatus.Idle);
		_gpsService.CurrentTrackId.Should().BeNull();

		_mockLocationService.Verify(x => x.StopTrackingAsync(), Times.Once);
	}

	[Fact]
	public async Task StopTrackAsync_ShouldUpdateTrackInDatabase()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Act
		await _gpsService.StopTrackAsync();

		// Assert
		var db = _testDatabase.GetConnection();
		var track = await db.Table<GpsTrack>().Where(t => t.Id == trackId).FirstOrDefaultAsync();
		
		track.Should().NotBeNull();
		track!.Status.Should().Be("Completed");
		track.EndTime.Should().NotBeNull();
	}

	[Fact]
	public async Task StopTrackAsync_WhenNotRecording_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _gpsService.StopTrackAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task PauseTrackAsync_ShouldPauseRecording()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		await _gpsService.StartTrackAsync("Test Track");

		// Act
		await _gpsService.PauseTrackAsync();

		// Assert
		_gpsService.Status.Should().Be(GpsTrackStatus.Paused);
		_mockLocationService.Verify(x => x.StopTrackingAsync(), Times.Once);
	}

	[Fact]
	public async Task ResumeTrackAsync_ShouldResumeRecording()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		await _gpsService.StartTrackAsync("Test Track");
		await _gpsService.PauseTrackAsync();

		// Act
		await _gpsService.ResumeTrackAsync();

		// Assert
		_gpsService.Status.Should().Be(GpsTrackStatus.Recording);
		_mockLocationService.Verify(
			x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()),
			Times.Exactly(2));
	}

	[Fact]
	public async Task GetAllTracksAsync_ShouldReturnAllTracks()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		// Create multiple tracks
		var trackId1 = await _gpsService.StartTrackAsync("Track 1");
		await _gpsService.StopTrackAsync();

		var trackId2 = await _gpsService.StartTrackAsync("Track 2");
		await _gpsService.StopTrackAsync();

		// Act
		var tracks = await _gpsService.GetAllTracksAsync();

		// Assert
		tracks.Should().HaveCount(2);
		tracks.Should().Contain(t => t.Id == trackId1);
		tracks.Should().Contain(t => t.Id == trackId2);
	}

	[Fact]
	public async Task GetTrackAsync_ShouldReturnTrack()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Act
		var track = await _gpsService.GetTrackAsync(trackId);

		// Assert
		track.Should().NotBeNull();
		track!.Id.Should().Be(trackId);
		track.Name.Should().Be("Test Track");
	}

	[Fact]
	public async Task GetTrackAsync_WithInvalidId_ShouldReturnNull()
	{
		// Act
		var track = await _gpsService.GetTrackAsync("invalid-id");

		// Assert
		track.Should().BeNull();
	}

	[Fact]
	public async Task DeleteTrackAsync_ShouldDeleteTrack()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		var trackId = await _gpsService.StartTrackAsync("Test Track");
		await _gpsService.StopTrackAsync();

		// Act
		var result = await _gpsService.DeleteTrackAsync(trackId);

		// Assert
		result.Should().BeTrue();

		var track = await _gpsService.GetTrackAsync(trackId);
		track.Should().BeNull();
	}

	[Fact]
	public async Task DeleteTrackAsync_WithInvalidId_ShouldReturnFalse()
	{
		// Act
		var result = await _gpsService.DeleteTrackAsync("invalid-id");

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CalculateTrackStatisticsAsync_WithNoPoints_ShouldReturnNull()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Act
		var stats = await _gpsService.CalculateTrackStatisticsAsync(trackId);

		// Assert
		stats.Should().BeNull();
	}

	[Fact]
	public async Task CalculateTrackStatisticsAsync_WithInvalidId_ShouldReturnNull()
	{
		// Act
		var stats = await _gpsService.CalculateTrackStatisticsAsync("invalid-id");

		// Assert
		stats.Should().BeNull();
	}

	[Fact]
	public async Task ExportTrackAsLineStringAsync_WithNoPoints_ShouldReturnNull()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		var trackId = await _gpsService.StartTrackAsync("Test Track");

		// Act
		var lineString = await _gpsService.ExportTrackAsLineStringAsync(trackId);

		// Assert
		lineString.Should().BeNull();
	}

	[Fact]
	public async Task ExportTrackAsLineStringAsync_WithPoints_ShouldReturnLineString()
	{
		// Arrange
		var trackId = Guid.NewGuid().ToString();
		var db = _testDatabase.GetConnection();

		// Create track
		var track = new GpsTrack
		{
			Id = trackId,
			Name = "Test Track",
			StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Status = "Completed"
		};
		await db.InsertAsync(track);

		// Add track points
		var points = new[]
		{
			new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = trackId,
				Latitude = 21.3099,
				Longitude = -157.8581,
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				SequenceNumber = 0
			},
			new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = trackId,
				Latitude = 21.3100,
				Longitude = -157.8580,
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				SequenceNumber = 1
			}
		};
		await db.InsertAllAsync(points);

		// Act
		var lineString = await _gpsService.ExportTrackAsLineStringAsync(trackId);

		// Assert
		lineString.Should().NotBeNull();
		lineString!.NumPoints.Should().Be(2);
		lineString.SRID.Should().Be(4326);
	}

	[Fact]
	public async Task SimplifyTrackAsync_ShouldSimplifyTrack()
	{
		// Arrange
		var trackId = Guid.NewGuid().ToString();
		var db = _testDatabase.GetConnection();

		// Create track
		var track = new GpsTrack
		{
			Id = trackId,
			Name = "Test Track",
			StartTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Status = "Completed"
		};
		await db.InsertAsync(track);

		// Add multiple track points in a line
		var points = new List<GpsTrackPoint>();
		for (int i = 0; i < 10; i++)
		{
			points.Add(new GpsTrackPoint
			{
				Id = Guid.NewGuid().ToString(),
				TrackId = trackId,
				Latitude = 21.3099 + (i * 0.0001),
				Longitude = -157.8581 + (i * 0.0001),
				Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
				SequenceNumber = i
			});
		}
		await db.InsertAllAsync(points);

		// Act
		var simplified = await _gpsService.SimplifyTrackAsync(trackId, tolerance: 10.0);

		// Assert
		simplified.Should().NotBeNull();
		simplified!.NumPoints.Should().BeLessThan(10);
	}

	[Fact]
	public void GpsTrackStatus_ShouldHaveExpectedValues()
	{
		// Assert
		Enum.IsDefined(typeof(GpsTrackStatus), GpsTrackStatus.Idle).Should().BeTrue();
		Enum.IsDefined(typeof(GpsTrackStatus), GpsTrackStatus.Recording).Should().BeTrue();
		Enum.IsDefined(typeof(GpsTrackStatus), GpsTrackStatus.Paused).Should().BeTrue();
	}

	[Fact]
	public void GpsTrackInfo_ShouldHaveAllProperties()
	{
		// Arrange & Act
		var info = new GpsTrackInfo
		{
			Id = "test-id",
			Name = "Test Track",
			StartTime = DateTimeOffset.UtcNow,
			EndTime = DateTimeOffset.UtcNow.AddHours(1),
			PointCount = 100,
			TotalDistance = 1500.0,
			Duration = TimeSpan.FromHours(1),
			MaxSpeed = 5.0,
			AverageSpeed = 2.5,
			MinElevation = 10.0,
			MaxElevation = 50.0,
			ElevationGain = 30.0,
			ElevationLoss = 10.0
		};

		// Assert
		info.Id.Should().Be("test-id");
		info.Name.Should().Be("Test Track");
		info.PointCount.Should().Be(100);
		info.TotalDistance.Should().Be(1500.0);
		info.Duration.Should().Be(TimeSpan.FromHours(1));
		info.MaxSpeed.Should().Be(5.0);
		info.AverageSpeed.Should().Be(2.5);
	}

	[Fact]
	public void GpsTrackPointInfo_ShouldHaveAllProperties()
	{
		// Arrange & Act
		var info = new GpsTrackPointInfo
		{
			Id = "point-id",
			TrackId = "track-id",
			Latitude = 21.3099,
			Longitude = -157.8581,
			Altitude = 15.5,
			Accuracy = 10.0,
			Speed = 2.5,
			Course = 45.0,
			Timestamp = DateTimeOffset.UtcNow,
			SequenceNumber = 5
		};

		// Assert
		info.Id.Should().Be("point-id");
		info.TrackId.Should().Be("track-id");
		info.Latitude.Should().Be(21.3099);
		info.Longitude.Should().Be(-157.8581);
		info.SequenceNumber.Should().Be(5);
	}

	[Fact]
	public void TrackStatusChangedEventArgs_ShouldHaveProperties()
	{
		// Arrange & Act
		var eventArgs = new TrackStatusChangedEventArgs
		{
			OldStatus = GpsTrackStatus.Recording,
			NewStatus = GpsTrackStatus.Paused,
			TrackId = "test-track"
		};

		// Assert
		eventArgs.OldStatus.Should().Be(GpsTrackStatus.Recording);
		eventArgs.NewStatus.Should().Be(GpsTrackStatus.Paused);
		eventArgs.TrackId.Should().Be("test-track");
	}

	[Fact]
	public async Task Dispose_ShouldStopRecording()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService
			.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		await _gpsService.StartTrackAsync("Test Track");

		// Act
		_gpsService.Dispose();

		// Assert
		_gpsService.Status.Should().Be(GpsTrackStatus.Idle);
	}

	[Fact]
	public async Task TrackStatusChanged_ShouldBeRaised_WhenStatusChanges()
	{
		// Arrange
		_mockLocationService
			.Setup(x => x.StartTrackingAsync(It.IsAny<LocationAccuracy>(), It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		var statusChangedRaised = false;
		_gpsService.TrackStatusChanged += (sender, args) =>
		{
			statusChangedRaised = true;
			args.OldStatus.Should().Be(GpsTrackStatus.Idle);
			args.NewStatus.Should().Be(GpsTrackStatus.Recording);
		};

		// Act
		await _gpsService.StartTrackAsync("Test Track");

		// Assert
		statusChangedRaised.Should().BeTrue();
	}
}
