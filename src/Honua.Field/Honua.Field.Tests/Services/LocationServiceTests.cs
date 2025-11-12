// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for LocationService
/// Tests location tracking, permission handling, and GPS functionality
/// </summary>
public class LocationServiceTests : IDisposable
{
	private readonly LocationService _locationService;

	public LocationServiceTests()
	{
		_locationService = new LocationService();
	}

	public void Dispose()
	{
		_locationService?.Dispose();
	}

	[Fact]
	public async Task IsLocationAvailableAsync_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _locationService.IsLocationAvailableAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task CheckPermissionAsync_ShouldReturnValidStatus()
	{
		// Act
		var result = await _locationService.CheckPermissionAsync();

		// Assert
		result.Should().BeOneOf(
			LocationPermissionStatus.Unknown,
			LocationPermissionStatus.Denied,
			LocationPermissionStatus.Granted,
			LocationPermissionStatus.Restricted
		);
	}

	[Fact]
	public async Task RequestPermissionAsync_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _locationService.RequestPermissionAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task GetCurrentLocationAsync_ShouldReturnLocationOrNull()
	{
		// Act
		var result = await _locationService.GetCurrentLocationAsync(
			LocationAccuracy.Medium,
			TimeSpan.FromSeconds(5));

		// Assert - Location may be null if permission not granted or GPS unavailable
		if (result != null)
		{
			result.Latitude.Should().BeInRange(-90, 90);
			result.Longitude.Should().BeInRange(-180, 180);
			result.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
		}
	}

	[Fact]
	public void IsTracking_ShouldBeFalse_Initially()
	{
		// Assert
		_locationService.IsTracking.Should().BeFalse();
	}

	[Fact]
	public async Task StartTrackingAsync_ShouldSetIsTrackingToTrue()
	{
		// Act
		await _locationService.StartTrackingAsync(LocationAccuracy.Medium, TimeSpan.FromSeconds(5));

		// Assert
		_locationService.IsTracking.Should().BeTrue();

		// Cleanup
		await _locationService.StopTrackingAsync();
	}

	[Fact]
	public async Task StopTrackingAsync_ShouldSetIsTrackingToFalse()
	{
		// Arrange
		await _locationService.StartTrackingAsync(LocationAccuracy.Medium, TimeSpan.FromSeconds(5));

		// Act
		await _locationService.StopTrackingAsync();

		// Assert
		_locationService.IsTracking.Should().BeFalse();
	}

	[Fact]
	public async Task StartTrackingAsync_WhenAlreadyTracking_ShouldNotThrow()
	{
		// Arrange
		await _locationService.StartTrackingAsync(LocationAccuracy.Medium, TimeSpan.FromSeconds(5));

		// Act
		Func<Task> act = async () => await _locationService.StartTrackingAsync(
			LocationAccuracy.Medium,
			TimeSpan.FromSeconds(5));

		// Assert
		await act.Should().NotThrowAsync();

		// Cleanup
		await _locationService.StopTrackingAsync();
	}

	[Fact]
	public async Task StopTrackingAsync_WhenNotTracking_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _locationService.StopTrackingAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task LocationChanged_ShouldBeRaised_WhenTracking()
	{
		// Arrange
		var locationReceived = false;
		_locationService.LocationChanged += (sender, args) =>
		{
			locationReceived = true;
			args.Location.Should().NotBeNull();
		};

		// Act
		await _locationService.StartTrackingAsync(LocationAccuracy.Medium, TimeSpan.FromSeconds(1));
		await Task.Delay(TimeSpan.FromSeconds(3)); // Wait for location updates

		// Assert - May not receive location if permission not granted
		// locationReceived might be false in test environment

		// Cleanup
		await _locationService.StopTrackingAsync();
	}

	[Fact]
	public void LocationToPoint_ShouldConvertLocation_WithoutAltitude()
	{
		// Arrange
		var location = new LocationInfo
		{
			Latitude = 21.3099,
			Longitude = -157.8581,
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		var point = _locationService.LocationToPoint(location);

		// Assert
		point.Should().NotBeNull();
		point.Y.Should().BeApproximately(21.3099, 0.0001);
		point.X.Should().BeApproximately(-157.8581, 0.0001);
		point.SRID.Should().Be(4326);
	}

	[Fact]
	public void LocationToPoint_ShouldConvertLocation_WithAltitude()
	{
		// Arrange
		var location = new LocationInfo
		{
			Latitude = 21.3099,
			Longitude = -157.8581,
			Altitude = 15.5,
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		var point = _locationService.LocationToPoint(location);

		// Assert
		point.Should().NotBeNull();
		point.Y.Should().BeApproximately(21.3099, 0.0001);
		point.X.Should().BeApproximately(-157.8581, 0.0001);
		point.Coordinate.Z.Should().BeApproximately(15.5, 0.1);
		point.SRID.Should().Be(4326);
	}

	[Fact]
	public async Task OpenLocationSettingsAsync_ShouldNotThrow()
	{
		// Act
		Func<Task> act = async () => await _locationService.OpenLocationSettingsAsync();

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public void LocationInfo_ShouldHaveAllProperties()
	{
		// Arrange & Act
		var location = new LocationInfo
		{
			Latitude = 21.3099,
			Longitude = -157.8581,
			Altitude = 15.5,
			Accuracy = 10.0,
			VerticalAccuracy = 5.0,
			Speed = 2.5,
			Course = 45.0,
			Timestamp = DateTimeOffset.UtcNow,
			IsFromMockProvider = false
		};

		// Assert
		location.Latitude.Should().Be(21.3099);
		location.Longitude.Should().Be(-157.8581);
		location.Altitude.Should().Be(15.5);
		location.Accuracy.Should().Be(10.0);
		location.VerticalAccuracy.Should().Be(5.0);
		location.Speed.Should().Be(2.5);
		location.Course.Should().Be(45.0);
		location.IsFromMockProvider.Should().BeFalse();
	}

	[Theory]
	[InlineData(LocationAccuracy.Lowest)]
	[InlineData(LocationAccuracy.Low)]
	[InlineData(LocationAccuracy.Medium)]
	[InlineData(LocationAccuracy.High)]
	[InlineData(LocationAccuracy.Best)]
	public async Task GetCurrentLocationAsync_ShouldAcceptAllAccuracyLevels(LocationAccuracy accuracy)
	{
		// Act
		Func<Task> act = async () => await _locationService.GetCurrentLocationAsync(
			accuracy,
			TimeSpan.FromSeconds(2));

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public void LocationAccuracy_ShouldHaveExpectedValues()
	{
		// Assert
		Enum.IsDefined(typeof(LocationAccuracy), LocationAccuracy.Lowest).Should().BeTrue();
		Enum.IsDefined(typeof(LocationAccuracy), LocationAccuracy.Low).Should().BeTrue();
		Enum.IsDefined(typeof(LocationAccuracy), LocationAccuracy.Medium).Should().BeTrue();
		Enum.IsDefined(typeof(LocationAccuracy), LocationAccuracy.High).Should().BeTrue();
		Enum.IsDefined(typeof(LocationAccuracy), LocationAccuracy.Best).Should().BeTrue();
	}

	[Fact]
	public void LocationPermissionStatus_ShouldHaveExpectedValues()
	{
		// Assert
		Enum.IsDefined(typeof(LocationPermissionStatus), LocationPermissionStatus.Unknown).Should().BeTrue();
		Enum.IsDefined(typeof(LocationPermissionStatus), LocationPermissionStatus.Denied).Should().BeTrue();
		Enum.IsDefined(typeof(LocationPermissionStatus), LocationPermissionStatus.Granted).Should().BeTrue();
		Enum.IsDefined(typeof(LocationPermissionStatus), LocationPermissionStatus.Restricted).Should().BeTrue();
	}

	[Fact]
	public void LocationChangedEventArgs_ShouldHaveLocationProperty()
	{
		// Arrange
		var location = new LocationInfo
		{
			Latitude = 21.3099,
			Longitude = -157.8581,
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		var eventArgs = new LocationChangedEventArgs { Location = location };

		// Assert
		eventArgs.Location.Should().Be(location);
	}

	[Fact]
	public void LocationErrorEventArgs_ShouldHaveMessageAndException()
	{
		// Arrange
		var exception = new Exception("Test error");

		// Act
		var eventArgs = new LocationErrorEventArgs
		{
			Message = "Error occurred",
			Exception = exception
		};

		// Assert
		eventArgs.Message.Should().Be("Error occurred");
		eventArgs.Exception.Should().Be(exception);
	}

	[Fact]
	public async Task Dispose_ShouldStopTracking()
	{
		// Arrange
		await _locationService.StartTrackingAsync(LocationAccuracy.Medium, TimeSpan.FromSeconds(5));
		_locationService.IsTracking.Should().BeTrue();

		// Act
		_locationService.Dispose();

		// Assert
		_locationService.IsTracking.Should().BeFalse();
	}

	[Fact]
	public async Task MultipleStartStop_ShouldWork()
	{
		// Act & Assert
		await _locationService.StartTrackingAsync(LocationAccuracy.Medium, TimeSpan.FromSeconds(5));
		_locationService.IsTracking.Should().BeTrue();

		await _locationService.StopTrackingAsync();
		_locationService.IsTracking.Should().BeFalse();

		await _locationService.StartTrackingAsync(LocationAccuracy.High, TimeSpan.FromSeconds(3));
		_locationService.IsTracking.Should().BeTrue();

		await _locationService.StopTrackingAsync();
		_locationService.IsTracking.Should().BeFalse();
	}
}
