using FluentAssertions;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for MapViewModel
/// Tests map initialization, feature rendering, GPS tracking, and spatial operations
/// </summary>
public class MapViewModelTests : IDisposable
{
	private readonly Mock<IFeatureRepository> _mockFeatureRepo;
	private readonly Mock<ICollectionRepository> _mockCollectionRepo;
	private readonly Mock<ILocationService> _mockLocationService;
	private readonly MapViewModel _viewModel;
	private bool _disposed;

	public MapViewModelTests()
	{
		_mockFeatureRepo = new Mock<IFeatureRepository>();
		_mockCollectionRepo = new Mock<ICollectionRepository>();
		_mockLocationService = new Mock<ILocationService>();

		_viewModel = new MapViewModel(
			_mockFeatureRepo.Object,
			_mockCollectionRepo.Object,
			_mockLocationService.Object
		);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Map");
		_viewModel.IsMapInitialized.Should().BeFalse();
		_viewModel.ShowCurrentLocation.Should().BeTrue();
		_viewModel.ShowGpsTrack.Should().BeFalse();
		_viewModel.IsTrackingLocation.Should().BeFalse();
		_viewModel.CurrentDrawingMode.Should().Be(DrawingMode.None);
		_viewModel.IsBaseMapVisible.Should().BeTrue();
		_viewModel.BaseMapType.Should().Be("OpenStreetMap");
		_viewModel.Layers.Should().BeEmpty();
		_viewModel.GpsTrackPoints.Should().BeEmpty();
		_viewModel.DrawingPoints.Should().BeEmpty();
	}

	#endregion

	#region Map Initialization Tests

	[Fact]
	public async Task InitializeMapAsync_ShouldSetIsMapInitialized()
	{
		// Arrange
		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		// Act
		await _viewModel.InitializeMapAsync();

		// Assert
		_viewModel.IsMapInitialized.Should().BeTrue();
		_viewModel.GetMap().Should().NotBeNull();
	}

	[Fact]
	public async Task InitializeMapAsync_ShouldLoadCollections()
	{
		// Arrange
		var collections = new List<Collection>
		{
			new Collection { Id = "col1", Title = "Collection 1", ItemsCount = 5 },
			new Collection { Id = "col2", Title = "Collection 2", ItemsCount = 10 }
		};

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(collections);
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		// Act
		await _viewModel.InitializeMapAsync();

		// Assert
		_viewModel.Layers.Should().HaveCount(2);
		_viewModel.Layers[0].Name.Should().Be("Collection 1");
		_viewModel.Layers[0].FeatureCount.Should().Be(5);
		_viewModel.Layers[0].IsVisible.Should().BeTrue();
	}

	[Fact]
	public async Task InitializeMapAsync_ShouldNotReinitializeIfAlreadyInitialized()
	{
		// Arrange
		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		await _viewModel.InitializeMapAsync();

		// Assert
		_mockCollectionRepo.Verify(x => x.GetAllAsync(), Times.Once);
	}

	[Fact]
	public async Task InitializeMapAsync_ShouldHandleErrors()
	{
		// Arrange
		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ThrowsAsync(new Exception("Database error"));

		// Act
		Func<Task> act = async () => await _viewModel.InitializeMapAsync();

		// Assert
		await act.Should().NotThrowAsync();
		_viewModel.IsBusy.Should().BeFalse();
	}

	#endregion

	#region Location Tests

	[Fact]
	public async Task ZoomToLocationCommand_ShouldGetCurrentLocation()
	{
		// Arrange
		var location = new LocationInfo
		{
			Latitude = 37.7749,
			Longitude = -122.4194,
			Timestamp = DateTimeOffset.UtcNow
		};

		_mockLocationService.Setup(x => x.GetCurrentLocationAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()))
			.ReturnsAsync(location);

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		await _viewModel.ZoomToLocationCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentLatitude.Should().Be(37.7749);
		_viewModel.CurrentLongitude.Should().Be(-122.4194);
		_mockLocationService.Verify(x => x.GetCurrentLocationAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()), Times.Once);
	}

	[Fact]
	public async Task ZoomToLocationCommand_ShouldHandleNoLocation()
	{
		// Arrange
		_mockLocationService.Setup(x => x.GetCurrentLocationAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()))
			.ReturnsAsync((LocationInfo?)null);

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		await _viewModel.ZoomToLocationCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentLatitude.Should().Be(0);
		_viewModel.CurrentLongitude.Should().Be(0);
	}

	[Fact]
	public async Task ToggleLocationTrackingCommand_ShouldStartTracking()
	{
		// Arrange
		_mockLocationService.Setup(x => x.StartTrackingAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);

		// Act
		await _viewModel.ToggleLocationTrackingCommand.ExecuteAsync(null);

		// Assert
		_viewModel.IsTrackingLocation.Should().BeTrue();
		_mockLocationService.Verify(x => x.StartTrackingAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()), Times.Once);
	}

	[Fact]
	public async Task ToggleLocationTrackingCommand_ShouldStopTracking()
	{
		// Arrange
		_mockLocationService.Setup(x => x.StartTrackingAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		await _viewModel.ToggleLocationTrackingCommand.ExecuteAsync(null);

		// Act
		await _viewModel.ToggleLocationTrackingCommand.ExecuteAsync(null);

		// Assert
		_viewModel.IsTrackingLocation.Should().BeFalse();
		_mockLocationService.Verify(x => x.StopTrackingAsync(), Times.Once);
	}

	[Fact]
	public void AddDrawingPoint_ShouldAddCoordinate()
	{
		// Arrange
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Point);

		// Act
		_viewModel.AddDrawingPoint(37.7749, -122.4194);

		// Assert
		_viewModel.DrawingPoints.Should().HaveCount(1);
		_viewModel.DrawingPoints[0].X.Should().Be(-122.4194);
		_viewModel.DrawingPoints[0].Y.Should().Be(37.7749);
	}

	[Fact]
	public void AddDrawingPoint_ShouldNotAddWhenDrawingModeIsNone()
	{
		// Act
		_viewModel.AddDrawingPoint(37.7749, -122.4194);

		// Assert
		_viewModel.DrawingPoints.Should().BeEmpty();
	}

	#endregion

	#region Feature Tests

	[Fact]
	public async Task ZoomToFeatureCommand_ShouldZoomToFeature()
	{
		// Arrange
		var geometryFactory = new GeometryFactory();
		var feature = new Feature
		{
			Id = "feature1",
			CollectionId = "col1"
		};
		feature.SetGeometry(geometryFactory.CreatePoint(new Coordinate(-122.4194, 37.7749)));

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feature1"))
			.ReturnsAsync(feature);

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		await _viewModel.ZoomToFeatureCommand.ExecuteAsync("feature1");

		// Assert
		_mockFeatureRepo.Verify(x => x.GetByIdAsync("feature1"), Times.Once);
	}

	[Fact]
	public async Task ZoomToFeatureCommand_ShouldHandleFeatureNotFound()
	{
		// Arrange
		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feature1"))
			.ReturnsAsync((Feature?)null);

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		Func<Task> act = async () => await _viewModel.ZoomToFeatureCommand.ExecuteAsync("feature1");

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task SelectFeatureCommand_ShouldSelectFeature()
	{
		// Arrange
		var feature = new Feature
		{
			Id = "feature1",
			CollectionId = "col1"
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feature1"))
			.ReturnsAsync(feature);

		// Act
		await _viewModel.SelectFeatureCommand.ExecuteAsync("feature1");

		// Assert
		_viewModel.SelectedFeatureId.Should().Be("feature1");
		_viewModel.SelectedFeature.Should().NotBeNull();
		_viewModel.SelectedFeature?.Id.Should().Be("feature1");
	}

	[Fact]
	public async Task SelectFeatureCommand_ShouldClearSelectionWhenIdIsNull()
	{
		// Arrange
		var feature = new Feature
		{
			Id = "feature1",
			CollectionId = "col1"
		};

		_mockFeatureRepo.Setup(x => x.GetByIdAsync("feature1"))
			.ReturnsAsync(feature);

		await _viewModel.SelectFeatureCommand.ExecuteAsync("feature1");

		// Act
		await _viewModel.SelectFeatureCommand.ExecuteAsync(null);

		// Assert
		_viewModel.SelectedFeatureId.Should().BeNull();
		_viewModel.SelectedFeature.Should().BeNull();
	}

	[Fact]
	public async Task GetFeaturesInViewCommand_ShouldQueryRepository()
	{
		// Arrange
		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());
		_mockFeatureRepo.Setup(x => x.GetByBoundsAsync(
			It.IsAny<double>(), It.IsAny<double>(),
			It.IsAny<double>(), It.IsAny<double>()))
			.ReturnsAsync(new List<Feature>
			{
				new Feature { Id = "f1" },
				new Feature { Id = "f2" },
				new Feature { Id = "f3" }
			});

		await _viewModel.InitializeMapAsync();

		// Act
		await _viewModel.GetFeaturesInViewCommand.ExecuteAsync(null);

		// Assert
		_viewModel.FeaturesInView.Should().Be(3);
	}

	[Fact]
	public async Task FindNearbyFeaturesCommand_ShouldFindFeaturesNearLocation()
	{
		// Arrange
		var nearbyFeatures = new List<Feature>
		{
			new Feature { Id = "f1" },
			new Feature { Id = "f2" }
		};

		_mockFeatureRepo.Setup(x => x.GetWithinDistanceAsync(
			It.IsAny<Point>(),
			It.IsAny<double>()))
			.ReturnsAsync(nearbyFeatures);

		// Act
		var result = await _viewModel.FindNearbyFeaturesCommand.ExecuteAsync((37.7749, -122.4194, 1000.0));

		// Assert
		result.Should().HaveCount(2);
		_mockFeatureRepo.Verify(x => x.GetWithinDistanceAsync(
			It.IsAny<Point>(),
			1000.0), Times.Once);
	}

	[Fact]
	public async Task ZoomToAllFeaturesCommand_ShouldZoomToExtent()
	{
		// Arrange
		_mockFeatureRepo.Setup(x => x.GetExtentAsync(null))
			.ReturnsAsync((-180.0, -90.0, 180.0, 90.0));

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		await _viewModel.ZoomToAllFeaturesCommand.ExecuteAsync(null);

		// Assert
		_mockFeatureRepo.Verify(x => x.GetExtentAsync(null), Times.Once);
	}

	[Fact]
	public async Task ZoomToAllFeaturesCommand_ShouldHandleNoFeatures()
	{
		// Arrange
		_mockFeatureRepo.Setup(x => x.GetExtentAsync(null))
			.ReturnsAsync((ValueTuple<double, double, double, double>?)null);

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		Func<Task> act = async () => await _viewModel.ZoomToAllFeaturesCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	#endregion

	#region Drawing Tests

	[Fact]
	public void DrawFeatureCommand_ShouldSetDrawingMode()
	{
		// Act
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Point);

		// Assert
		_viewModel.CurrentDrawingMode.Should().Be(DrawingMode.Point);
		_viewModel.DrawingPoints.Should().BeEmpty();
	}

	[Fact]
	public void DrawFeatureCommand_ShouldClearPreviousDrawing()
	{
		// Arrange
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Point);
		_viewModel.AddDrawingPoint(37.7749, -122.4194);

		// Act
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Line);

		// Assert
		_viewModel.CurrentDrawingMode.Should().Be(DrawingMode.Line);
		_viewModel.DrawingPoints.Should().BeEmpty();
	}

	[Fact]
	public async Task CompleteDrawingCommand_ShouldResetDrawingMode()
	{
		// Arrange
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Point);
		_viewModel.AddDrawingPoint(37.7749, -122.4194);

		// Act
		await _viewModel.CompleteDrawingCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentDrawingMode.Should().Be(DrawingMode.None);
		_viewModel.DrawingPoints.Should().BeEmpty();
	}

	[Fact]
	public async Task CompleteDrawingCommand_ShouldHandleInsufficientPoints()
	{
		// Arrange
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Line);
		_viewModel.AddDrawingPoint(37.7749, -122.4194);
		// Only one point, need 2 for line

		// Act
		await _viewModel.CompleteDrawingCommand.ExecuteAsync(null);

		// Assert - should still reset
		_viewModel.CurrentDrawingMode.Should().Be(DrawingMode.None);
	}

	[Fact]
	public void CancelDrawingCommand_ShouldResetDrawing()
	{
		// Arrange
		_viewModel.DrawFeatureCommand.Execute(DrawingMode.Polygon);
		_viewModel.AddDrawingPoint(37.7749, -122.4194);
		_viewModel.AddDrawingPoint(37.7750, -122.4195);

		// Act
		_viewModel.CancelDrawingCommand.Execute(null);

		// Assert
		_viewModel.CurrentDrawingMode.Should().Be(DrawingMode.None);
		_viewModel.DrawingPoints.Should().BeEmpty();
	}

	#endregion

	#region Layer Management Tests

	[Fact]
	public void ToggleGpsTrackCommand_ShouldToggleGpsTrackVisibility()
	{
		// Arrange
		var initialState = _viewModel.ShowGpsTrack;

		// Act
		_viewModel.ToggleGpsTrackCommand.Execute(null);

		// Assert
		_viewModel.ShowGpsTrack.Should().Be(!initialState);
	}

	[Fact]
	public void ToggleGpsTrackCommand_ShouldClearTrackPointsWhenDisabled()
	{
		// Arrange
		_viewModel.ShowGpsTrack = true;
		_viewModel.GpsTrackPoints.Add(new LocationInfo
		{
			Latitude = 37.7749,
			Longitude = -122.4194,
			Timestamp = DateTimeOffset.UtcNow
		});

		// Act
		_viewModel.ToggleGpsTrackCommand.Execute(null);

		// Assert
		_viewModel.ShowGpsTrack.Should().BeFalse();
		_viewModel.GpsTrackPoints.Should().BeEmpty();
	}

	[Fact]
	public void ToggleBaseMapCommand_ShouldToggleBaseMapVisibility()
	{
		// Arrange
		var initialState = _viewModel.IsBaseMapVisible;

		// Act
		_viewModel.ToggleBaseMapCommand.Execute(null);

		// Assert
		_viewModel.IsBaseMapVisible.Should().Be(!initialState);
	}

	[Fact]
	public async Task ToggleLayerVisibilityCommand_ShouldToggleLayerVisibility()
	{
		// Arrange
		var collections = new List<Collection>
		{
			new Collection { Id = "col1", Title = "Collection 1", ItemsCount = 5 }
		};

		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(collections);
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		var initialVisibility = _viewModel.Layers[0].IsVisible;

		// Act
		await _viewModel.ToggleLayerVisibilityCommand.ExecuteAsync("col1");

		// Assert
		_viewModel.Layers[0].IsVisible.Should().Be(!initialVisibility);
	}

	[Fact]
	public async Task ToggleLayerVisibilityCommand_ShouldHandleInvalidCollectionId()
	{
		// Arrange
		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		await _viewModel.InitializeMapAsync();

		// Act
		Func<Task> act = async () => await _viewModel.ToggleLayerVisibilityCommand.ExecuteAsync("invalid");

		// Assert
		await act.Should().NotThrowAsync();
	}

	#endregion

	#region Lifecycle Tests

	[Fact]
	public async Task OnAppearingAsync_ShouldInitializeMapIfNotInitialized()
	{
		// Arrange
		_mockCollectionRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Collection>());
		_mockFeatureRepo.Setup(x => x.GetAllAsync())
			.ReturnsAsync(new List<Feature>());

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.IsMapInitialized.Should().BeTrue();
	}

	[Fact]
	public async Task OnDisappearingAsync_ShouldStopLocationTracking()
	{
		// Arrange
		_mockLocationService.Setup(x => x.StartTrackingAsync(
			It.IsAny<LocationAccuracy>(),
			It.IsAny<TimeSpan?>()))
			.Returns(Task.CompletedTask);
		_mockLocationService.Setup(x => x.StopTrackingAsync())
			.Returns(Task.CompletedTask);

		await _viewModel.ToggleLocationTrackingCommand.ExecuteAsync(null);

		// Act
		await _viewModel.OnDisappearingAsync();

		// Assert
		_viewModel.IsTrackingLocation.Should().BeFalse();
		_mockLocationService.Verify(x => x.StopTrackingAsync(), Times.Once);
	}

	#endregion

	#region Observable Properties Tests

	[Fact]
	public void ObservableProperties_ShouldNotifyPropertyChanged()
	{
		// This test verifies that the MVVM properties work correctly
		var propertyChangedEvents = new List<string>();
		_viewModel.PropertyChanged += (s, e) => propertyChangedEvents.Add(e.PropertyName ?? string.Empty);

		// Act
		_viewModel.CurrentZoom = 15;
		_viewModel.CurrentLatitude = 37.7749;
		_viewModel.CurrentLongitude = -122.4194;
		_viewModel.IsTrackingLocation = true;

		// Assert
		propertyChangedEvents.Should().Contain("CurrentZoom");
		propertyChangedEvents.Should().Contain("CurrentLatitude");
		propertyChangedEvents.Should().Contain("CurrentLongitude");
		propertyChangedEvents.Should().Contain("IsTrackingLocation");
	}

	#endregion

	#region Dispose

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			_viewModel?.Dispose();
		}

		_disposed = true;
	}

	#endregion
}
