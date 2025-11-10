// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using System.Collections.ObjectModel;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the feature list page
/// Displays features from a collection with search, filtering, and pagination
/// </summary>
public partial class FeatureListViewModel : BaseViewModel, IDisposable
{
	private readonly IFeaturesService _featuresService;
	private readonly INavigationService _navigationService;
	private readonly ICollectionRepository _collectionRepository;

	[ObservableProperty]
	private string _collectionId = string.Empty;

	[ObservableProperty]
	private Collection? _collection;

	[ObservableProperty]
	private ObservableCollection<Feature> _features = new();

	[ObservableProperty]
	private string _searchText = string.Empty;

	[ObservableProperty]
	private bool _isRefreshing;

	[ObservableProperty]
	private bool _isEmpty;

	[ObservableProperty]
	private int _totalCount;

	[ObservableProperty]
	private string _filterText = "All Features";

	[ObservableProperty]
	private bool _hasMoreItems;

	private int _currentPage = 0;
	private const int PageSize = 50;
	private List<Feature> _allFeatures = new();

	public FeatureListViewModel(
		IFeaturesService featuresService,
		INavigationService navigationService,
		ICollectionRepository collectionRepository)
	{
		_featuresService = featuresService;
		_navigationService = navigationService;
		_collectionRepository = collectionRepository;

		Title = "Features";
	}

	/// <summary>
	/// Initialize with collection ID
	/// </summary>
	public async Task InitializeAsync(string collectionId)
	{
		CollectionId = collectionId;
		Collection = await _collectionRepository.GetByIdAsync(collectionId);

		if (Collection != null)
		{
			Title = Collection.Title;
		}

		await LoadFeaturesAsync();
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();

		// Reload if we already have a collection
		if (!string.IsNullOrEmpty(CollectionId))
		{
			await RefreshAsync();
		}
	}

	/// <summary>
	/// Load features from service
	/// </summary>
	[RelayCommand]
	private async Task LoadFeaturesAsync()
	{
		if (IsBusy || string.IsNullOrEmpty(CollectionId))
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			_currentPage = 0;
			_allFeatures.Clear();
			Features.Clear();

			// Get total count
			TotalCount = await _featuresService.GetFeatureCountAsync(CollectionId);

			// Load first page
			var features = await _featuresService.GetFeaturesByCollectionIdAsync(
				CollectionId,
				skip: _currentPage * PageSize,
				take: PageSize);

			_allFeatures.AddRange(features);

			foreach (var feature in features)
			{
				Features.Add(feature);
			}

			HasMoreItems = features.Count >= PageSize;
			isEmpty = Features.Count == 0;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to load features");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Load more features (pagination)
	/// </summary>
	[RelayCommand]
	private async Task LoadMoreFeaturesAsync()
	{
		if (IsBusy || !HasMoreItems || string.IsNullOrEmpty(CollectionId))
			return;

		IsBusy = true;

		try
		{
			_currentPage++;

			var features = await _featuresService.GetFeaturesByCollectionIdAsync(
				CollectionId,
				skip: _currentPage * PageSize,
				take: PageSize);

			_allFeatures.AddRange(features);

			foreach (var feature in features)
			{
				Features.Add(feature);
			}

			HasMoreItems = features.Count >= PageSize;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to load more features");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Refresh feature list (pull-to-refresh)
	/// </summary>
	[RelayCommand]
	private async Task RefreshAsync()
	{
		IsRefreshing = true;

		try
		{
			await LoadFeaturesAsync();
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	/// <summary>
	/// Search features by text
	/// </summary>
	[RelayCommand]
	private async Task SearchAsync()
	{
		if (IsBusy || string.IsNullOrEmpty(CollectionId))
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			Features.Clear();

			if (string.IsNullOrWhiteSpace(SearchText))
			{
				// Reset to all features
				await LoadFeaturesAsync();
			}
			else
			{
				// Search features
				var searchResults = await _featuresService.SearchFeaturesAsync(CollectionId, SearchText);

				foreach (var feature in searchResults)
				{
					Features.Add(feature);
				}

				FilterText = $"Search: \"{SearchText}\"";
			}

			isEmpty = Features.Count == 0;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to search features");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Clear search and show all features
	/// </summary>
	[RelayCommand]
	private async Task ClearSearchAsync()
	{
		SearchText = string.Empty;
		FilterText = "All Features";
		await LoadFeaturesAsync();
	}

	/// <summary>
	/// Filter features within current map bounds
	/// </summary>
	public async Task FilterByBoundsAsync(double minX, double minY, double maxX, double maxY)
	{
		if (IsBusy || string.IsNullOrEmpty(CollectionId))
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			Features.Clear();

			var featuresInBounds = await _featuresService.GetFeaturesInBoundsAsync(
				CollectionId, minX, minY, maxX, maxY);

			foreach (var feature in featuresInBounds)
			{
				Features.Add(feature);
			}

			FilterText = "Map Bounds";
			isEmpty = Features.Count == 0;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to filter features");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Filter features nearby current location
	/// </summary>
	public async Task FilterNearbyAsync(double latitude, double longitude, double radiusMeters)
	{
		if (IsBusy || string.IsNullOrEmpty(CollectionId))
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			Features.Clear();

			var nearbyFeatures = await _featuresService.GetFeaturesNearbyAsync(
				latitude, longitude, radiusMeters, CollectionId);

			foreach (var feature in nearbyFeatures)
			{
				Features.Add(feature);
			}

			FilterText = $"Within {radiusMeters}m";
			isEmpty = Features.Count == 0;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to filter nearby features");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Navigate to feature detail view
	/// </summary>
	[RelayCommand]
	private async Task ViewFeatureDetailAsync(Feature feature)
	{
		if (feature == null)
			return;

		try
		{
			var parameters = new Dictionary<string, object>
			{
				{ "featureId", feature.Id }
			};

			await _navigationService.NavigateToAsync("FeatureDetail", parameters);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to navigate to feature detail");
		}
	}

	/// <summary>
	/// Navigate to create new feature
	/// </summary>
	[RelayCommand]
	private async Task CreateNewFeatureAsync()
	{
		if (string.IsNullOrEmpty(CollectionId))
			return;

		try
		{
			var parameters = new Dictionary<string, object>
			{
				{ "collectionId", CollectionId },
				{ "mode", "create" }
			};

			await _navigationService.NavigateToAsync("FeatureEditor", parameters);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to navigate to feature editor");
		}
	}

	/// <summary>
	/// Show feature on map
	/// </summary>
	[RelayCommand]
	private async Task ShowOnMapAsync(Feature feature)
	{
		if (feature == null)
			return;

		try
		{
			var parameters = new Dictionary<string, object>
			{
				{ "featureId", feature.Id },
				{ "zoomToFeature", true }
			};

			await _navigationService.NavigateToAsync("Map", parameters);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to show feature on map");
		}
	}

	public void Dispose()
	{
		Features.Clear();
		_allFeatures.Clear();
	}
}
