// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for FeatureListViewModel
/// Tests feature listing, search, filtering, pagination, and navigation
/// </summary>
public class FeatureListViewModelTests
{
	private readonly Mock<IFeaturesService> _mockFeaturesService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<ICollectionRepository> _mockCollectionRepository;
	private readonly FeatureListViewModel _viewModel;

	public FeatureListViewModelTests()
	{
		_mockFeaturesService = new Mock<IFeaturesService>();
		_mockNavigationService = new Mock<INavigationService>();
		_mockCollectionRepository = new Mock<ICollectionRepository>();

		_viewModel = new FeatureListViewModel(
			_mockFeaturesService.Object,
			_mockNavigationService.Object,
			_mockCollectionRepository.Object);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Features");
		_viewModel.Features.Should().BeEmpty();
		_viewModel.IsBusy.Should().BeFalse();
		_viewModel.IsRefreshing.Should().BeFalse();
	}

	[Fact]
	public async Task InitializeAsync_ShouldLoadCollection()
	{
		// Arrange
		var collectionId = "coll-1";
		var collection = new Collection { Id = collectionId, Title = "Test Collection" };
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId }
		};

		_mockCollectionRepository
			.Setup(x => x.GetByIdAsync(collectionId))
			.ReturnsAsync(collection);

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(2);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(features);

		// Act
		await _viewModel.InitializeAsync(collectionId);

		// Assert
		_viewModel.CollectionId.Should().Be(collectionId);
		_viewModel.Collection.Should().NotBeNull();
		_viewModel.Title.Should().Be("Test Collection");
		_viewModel.Features.Should().HaveCount(2);
	}

	[Fact]
	public async Task LoadFeaturesAsync_ShouldLoadFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId },
			new() { Id = "f3", CollectionId = collectionId }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(3);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(features);

		_viewModel.CollectionId = collectionId;

		// Act
		await _viewModel.LoadFeaturesCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Features.Should().HaveCount(3);
		_viewModel.TotalCount.Should().Be(3);
		_viewModel.isEmpty.Should().BeFalse();
	}

	[Fact]
	public async Task LoadFeaturesAsync_ShouldSetIsEmpty_WhenNoFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>();

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(0);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(features);

		_viewModel.CollectionId = collectionId;

		// Act
		await _viewModel.LoadFeaturesCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Features.Should().BeEmpty();
		_viewModel.isEmpty.Should().BeTrue();
	}

	[Fact]
	public async Task LoadMoreFeaturesAsync_ShouldLoadNextPage()
	{
		// Arrange
		var collectionId = "coll-1";

		// First page
		var firstPageFeatures = Enumerable.Range(1, 50)
			.Select(i => new Feature { Id = $"f{i}", CollectionId = collectionId })
			.ToList();

		// Second page
		var secondPageFeatures = Enumerable.Range(51, 25)
			.Select(i => new Feature { Id = $"f{i}", CollectionId = collectionId })
			.ToList();

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(75);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(firstPageFeatures);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 50, 50))
			.ReturnsAsync(secondPageFeatures);

		_viewModel.CollectionId = collectionId;

		// Act
		await _viewModel.LoadFeaturesCommand.ExecuteAsync(null);
		await _viewModel.LoadMoreFeaturesCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Features.Should().HaveCount(75);
	}

	[Fact]
	public async Task RefreshAsync_ShouldReloadFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var features = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(1);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(features);

		_viewModel.CollectionId = collectionId;

		// Act
		await _viewModel.RefreshCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Features.Should().HaveCount(1);
		_viewModel.IsRefreshing.Should().BeFalse();
	}

	[Fact]
	public async Task SearchAsync_ShouldFilterFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var searchResults = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId, Properties = "{\"name\":\"Park\"}" }
		};

		_mockFeaturesService
			.Setup(x => x.SearchFeaturesAsync(collectionId, "park"))
			.ReturnsAsync(searchResults);

		_viewModel.CollectionId = collectionId;
		_viewModel.SearchText = "park";

		// Act
		await _viewModel.SearchCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Features.Should().HaveCount(1);
		_viewModel.FilterText.Should().Contain("park");
	}

	[Fact]
	public async Task SearchAsync_ShouldResetToAllFeatures_WhenSearchTextIsEmpty()
	{
		// Arrange
		var collectionId = "coll-1";
		var allFeatures = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(2);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(allFeatures);

		_viewModel.CollectionId = collectionId;
		_viewModel.SearchText = "";

		// Act
		await _viewModel.SearchCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Features.Should().HaveCount(2);
	}

	[Fact]
	public async Task ClearSearchAsync_ShouldResetSearch()
	{
		// Arrange
		var collectionId = "coll-1";
		var allFeatures = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeatureCountAsync(collectionId))
			.ReturnsAsync(2);

		_mockFeaturesService
			.Setup(x => x.GetFeaturesByCollectionIdAsync(collectionId, 0, 50))
			.ReturnsAsync(allFeatures);

		_viewModel.CollectionId = collectionId;
		_viewModel.SearchText = "something";

		// Act
		await _viewModel.ClearSearchCommand.ExecuteAsync(null);

		// Assert
		_viewModel.SearchText.Should().BeEmpty();
		_viewModel.FilterText.Should().Be("All Features");
		_viewModel.Features.Should().HaveCount(2);
	}

	[Fact]
	public async Task FilterByBoundsAsync_ShouldFilterFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var featuresInBounds = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeaturesInBoundsAsync(collectionId, -180, -90, 180, 90))
			.ReturnsAsync(featuresInBounds);

		_viewModel.CollectionId = collectionId;

		// Act
		await _viewModel.FilterByBoundsAsync(-180, -90, 180, 90);

		// Assert
		_viewModel.Features.Should().HaveCount(1);
		_viewModel.FilterText.Should().Be("Map Bounds");
	}

	[Fact]
	public async Task FilterNearbyAsync_ShouldFilterNearbyFeatures()
	{
		// Arrange
		var collectionId = "coll-1";
		var nearbyFeatures = new List<Feature>
		{
			new() { Id = "f1", CollectionId = collectionId },
			new() { Id = "f2", CollectionId = collectionId }
		};

		_mockFeaturesService
			.Setup(x => x.GetFeaturesNearbyAsync(0, 0, 1000, collectionId))
			.ReturnsAsync(nearbyFeatures);

		_viewModel.CollectionId = collectionId;

		// Act
		await _viewModel.FilterNearbyAsync(0, 0, 1000);

		// Assert
		_viewModel.Features.Should().HaveCount(2);
		_viewModel.FilterText.Should().Contain("1000m");
	}

	[Fact]
	public async Task ViewFeatureDetailCommand_ShouldNavigateToDetail()
	{
		// Arrange
		var feature = new Feature { Id = "f1", CollectionId = "coll-1" };

		// Act
		await _viewModel.ViewFeatureDetailCommand.ExecuteAsync(feature);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync(
				"FeatureDetail",
				It.Is<IDictionary<string, object>>(p => p.ContainsKey("featureId"))),
			Times.Once);
	}

	[Fact]
	public async Task CreateNewFeatureCommand_ShouldNavigateToEditor()
	{
		// Arrange
		_viewModel.CollectionId = "coll-1";

		// Act
		await _viewModel.CreateNewFeatureCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync(
				"FeatureEditor",
				It.Is<IDictionary<string, object>>(p =>
					p.ContainsKey("collectionId") && p.ContainsKey("mode"))),
			Times.Once);
	}

	[Fact]
	public async Task ShowOnMapCommand_ShouldNavigateToMap()
	{
		// Arrange
		var feature = new Feature { Id = "f1", CollectionId = "coll-1" };

		// Act
		await _viewModel.ShowOnMapCommand.ExecuteAsync(feature);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync(
				"Map",
				It.Is<IDictionary<string, object>>(p =>
					p.ContainsKey("featureId") && p.ContainsKey("zoomToFeature"))),
			Times.Once);
	}

	[Fact]
	public void Dispose_ShouldClearCollections()
	{
		// Arrange
		_viewModel.Features.Add(new Feature { Id = "f1" });

		// Act
		_viewModel.Dispose();

		// Assert
		_viewModel.Features.Should().BeEmpty();
	}
}
