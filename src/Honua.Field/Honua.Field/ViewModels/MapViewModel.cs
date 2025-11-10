// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI;
using NetTopologySuite.Geometries;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the map view
/// Manages Mapsui map display, feature rendering, GPS tracking, and spatial operations
/// </summary>
public partial class MapViewModel : BaseViewModel, IDisposable
{
	private readonly IFeatureRepository _featureRepository;
	private readonly ICollectionRepository _collectionRepository;
	private readonly ILocationService _locationService;
	private readonly ISymbologyService _symbologyService;
	private readonly IOfflineMapService _offlineMapService;
	private readonly IMapRepository _mapRepository;

	private Map? _map;
	private WritableLayer? _featuresLayer;
	private WritableLayer? _currentLocationLayer;
	private WritableLayer? _gpsTrackLayer;
	private WritableLayer? _drawingLayer;
	private bool _disposed;

	#region Observable Properties

	[ObservableProperty]
	private bool _isMapInitialized;

	[ObservableProperty]
	private double _currentZoom = 10;

	[ObservableProperty]
	private double _currentLatitude;

	[ObservableProperty]
	private double _currentLongitude;

	[ObservableProperty]
	private double _mapCenterLatitude;

	[ObservableProperty]
	private double _mapCenterLongitude;

	[ObservableProperty]
	private bool _isTrackingLocation;

	[ObservableProperty]
	private bool _showCurrentLocation = true;

	[ObservableProperty]
	private bool _showGpsTrack;

	[ObservableProperty]
	private string? _selectedFeatureId;

	[ObservableProperty]
	private Feature? _selectedFeature;

	[ObservableProperty]
	private DrawingMode _currentDrawingMode = DrawingMode.None;

	[ObservableProperty]
	private int _featuresInView;

	[ObservableProperty]
	private double _mapRotation;

	[ObservableProperty]
	private bool _isBaseMapVisible = true;

	[ObservableProperty]
	private string _baseMapType = "OpenStreetMap";

	[ObservableProperty]
	private bool _useOfflineMap;

	[ObservableProperty]
	private string? _currentOfflineMapId;

	[ObservableProperty]
	private bool _isDownloadingOfflineMap;

	[ObservableProperty]
	private double _downloadProgress;

	[ObservableProperty]
	private string _downloadStatusMessage = string.Empty;

	#endregion

	#region Collections

	/// <summary>
	/// Observable collection of all available collections/layers
	/// </summary>
	public ObservableCollection<CollectionLayerInfo> Layers { get; } = new();

	/// <summary>
	/// GPS track points for trail display
	/// </summary>
	public ObservableCollection<LocationInfo> GpsTrackPoints { get; } = new();

	/// <summary>
	/// Currently drawn geometry points during editing
	/// </summary>
	public ObservableCollection<Coordinate> DrawingPoints { get; } = new();

	/// <summary>
	/// Available offline maps
	/// </summary>
	public ObservableCollection<OfflineMapInfo> OfflineMaps { get; } = new();

	#endregion

	public MapViewModel(
		IFeatureRepository featureRepository,
		ICollectionRepository collectionRepository,
		ILocationService locationService,
		ISymbologyService symbologyService,
		IOfflineMapService offlineMapService,
		IMapRepository mapRepository)
	{
		_featureRepository = featureRepository;
		_collectionRepository = collectionRepository;
		_locationService = locationService;
		_symbologyService = symbologyService;
		_offlineMapService = offlineMapService;
		_mapRepository = mapRepository;

		Title = "Map";
	}

	/// <summary>
	/// Initializes the Mapsui map with base layers and configuration
	/// </summary>
	public async Task InitializeMapAsync()
	{
		if (IsMapInitialized)
			return;

		try
		{
			IsBusy = true;

			// Create new map instance
			_map = new Map();

			// Initialize base map layer
			InitializeBaseMapLayer();

			// Create feature layers
			_featuresLayer = new WritableLayer
			{
				Name = "Features",
				Style = null,
				IsMapInfoLayer = true
			};
			_map.Layers.Add(_featuresLayer);

			// Create current location layer
			_currentLocationLayer = new WritableLayer
			{
				Name = "CurrentLocation",
				Style = null
			};
			_map.Layers.Add(_currentLocationLayer);

			// Create GPS track layer
			_gpsTrackLayer = new WritableLayer
			{
				Name = "GpsTrack",
				Style = null,
				IsMapInfoLayer = false
			};
			_map.Layers.Add(_gpsTrackLayer);

			// Create drawing layer for geometry editing
			_drawingLayer = new WritableLayer
			{
				Name = "Drawing",
				Style = null,
				IsMapInfoLayer = false
			};
			_map.Layers.Add(_drawingLayer);

			// Set initial map center (default to 0,0)
			_map.Home = n => n.CenterOn(0, 0);
			MapCenterLatitude = 0;
			MapCenterLongitude = 0;

			// Load collections and features
			await LoadCollectionsAsync();
			await LoadFeaturesAsync();

			IsMapInitialized = true;

			System.Diagnostics.Debug.WriteLine("Map initialized successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to initialize map");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Gets the Mapsui Map instance
	/// </summary>
	public Map? GetMap() => _map;

	/// <summary>
	/// Initializes the base map layer (OpenStreetMap by default)
	/// </summary>
	private void InitializeBaseMapLayer()
	{
		if (_map == null)
			return;

		// Add OpenStreetMap tile layer
		var tileLayer = OpenStreetMap.CreateTileLayer();
		tileLayer.Name = "BaseMap";
		_map.Layers.Add(tileLayer);

		System.Diagnostics.Debug.WriteLine("Base map layer initialized");
	}

	/// <summary>
	/// Loads all collections from the repository
	/// </summary>
	private async Task LoadCollectionsAsync()
	{
		try
		{
			var collections = await _collectionRepository.GetAllAsync();

			Layers.Clear();
			foreach (var collection in collections)
			{
				var layerInfo = new CollectionLayerInfo
				{
					CollectionId = collection.Id,
					Name = collection.Title,
					IsVisible = true,
					FeatureCount = collection.ItemsCount
				};

				Layers.Add(layerInfo);
			}

			System.Diagnostics.Debug.WriteLine($"Loaded {Layers.Count} collections");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading collections: {ex.Message}");
		}
	}

	/// <summary>
	/// Loads and renders all features on the map
	/// </summary>
	private async Task LoadFeaturesAsync()
	{
		if (_featuresLayer == null)
			return;

		try
		{
			var features = await _featureRepository.GetAllAsync();

			_featuresLayer.Clear();

			foreach (var feature in features)
			{
				var geometry = feature.GetGeometry();
				if (geometry == null)
					continue;

				// Convert to Web Mercator projection
				var projected = ProjectGeometry(geometry);
				if (projected == null)
					continue;

				// Create feature with styling
				var mapFeature = new GeometryFeature
				{
					Geometry = projected,
					["Id"] = feature.Id,
					["CollectionId"] = feature.CollectionId
				};

				// Apply collection-specific symbology
				var collection = await _collectionRepository.GetByIdAsync(feature.CollectionId);
				if (collection != null)
				{
					// Pass feature for attribute-based styling
					mapFeature.Styles.Add(CreateStyleFromCollection(collection, geometry.GeometryType, feature));
				}
				else
				{
					mapFeature.Styles.Add(CreateDefaultStyle(geometry.GeometryType));
				}

				_featuresLayer.Add(mapFeature);
			}

			System.Diagnostics.Debug.WriteLine($"Loaded {features.Count} features to map");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading features: {ex.Message}");
		}
	}

	/// <summary>
	/// Projects geometry from WGS84 (EPSG:4326) to Web Mercator (EPSG:3857)
	/// </summary>
	private NetTopologySuite.Geometries.Geometry? ProjectGeometry(NetTopologySuite.Geometries.Geometry geometry)
	{
		try
		{
			var projected = new NetTopologySuite.Geometries.GeometryFactory().CreateGeometry(geometry);

			// Project each coordinate
			foreach (var coord in projected.Coordinates)
			{
				var (x, y) = SphericalMercator.FromLonLat(coord.X, coord.Y);
				coord.X = x;
				coord.Y = y;
			}

			projected.GeometryChanged();
			return projected;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error projecting geometry: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Unprojects coordinates from Web Mercator (EPSG:3857) to WGS84 (EPSG:4326)
	/// </summary>
	private (double lon, double lat) UnprojectCoordinate(double x, double y)
	{
		return SphericalMercator.ToLonLat(x, y);
	}

	/// <summary>
	/// Creates a Mapsui style from collection symbology
	/// </summary>
	private IStyle CreateStyleFromCollection(Collection collection, string geometryType)
	{
		return CreateStyleFromCollection(collection, geometryType, null);
	}

	/// <summary>
	/// Creates a Mapsui style from collection symbology for a specific feature
	/// </summary>
	private IStyle CreateStyleFromCollection(Collection collection, string geometryType, Feature? feature)
	{
		try
		{
			if (string.IsNullOrEmpty(collection.Symbology))
				return CreateDefaultStyle(geometryType);

			// Parse symbology using symbology service
			var symbology = _symbologyService.ParseSymbology(collection.Symbology);
			if (symbology == null)
				return CreateDefaultStyle(geometryType);

			// If feature provided, use attribute-based styling
			if (feature != null)
				return _symbologyService.GetStyleForFeature(feature, symbology, geometryType);

			// Otherwise use simple styling
			return _symbologyService.GetStyleForFeature(new Feature(), symbology, geometryType);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error creating style from symbology: {ex.Message}");
			return CreateDefaultStyle(geometryType);
		}
	}

	/// <summary>
	/// Creates default style based on geometry type
	/// </summary>
	private IStyle CreateDefaultStyle(string geometryType)
	{
		if (geometryType == "Point" || geometryType == "MultiPoint")
		{
			return new SymbolStyle
			{
				SymbolScale = 0.8,
				Fill = new Brush(Mapsui.Styles.Color.Blue),
				Outline = new Pen(Mapsui.Styles.Color.White, 2)
			};
		}
		else if (geometryType == "LineString" || geometryType == "MultiLineString")
		{
			return new VectorStyle
			{
				Line = new Pen(Mapsui.Styles.Color.Blue, 3)
			};
		}
		else // Polygon
		{
			return new VectorStyle
			{
				Fill = new Brush(Mapsui.Styles.Color.FromArgb(80, 0, 102, 204)),
				Outline = new Pen(Mapsui.Styles.Color.Blue, 2)
			};
		}
	}

	/// <summary>
	/// Gets legend items for a collection's symbology
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>List of legend items</returns>
	public async Task<List<LegendItem>> GetLegendItemsAsync(string collectionId)
	{
		try
		{
			var collection = await _collectionRepository.GetByIdAsync(collectionId);
			if (collection == null || string.IsNullOrEmpty(collection.Symbology))
				return new List<LegendItem>();

			var symbology = _symbologyService.ParseSymbology(collection.Symbology);
			if (symbology == null)
				return new List<LegendItem>();

			// Determine geometry type from collection (for now, assume Point)
			// TODO: Store geometry type in Collection model
			var geometryType = "Point";

			return _symbologyService.GenerateLegend(symbology, geometryType);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error generating legend: {ex.Message}");
			return new List<LegendItem>();
		}
	}

	#region Commands

	/// <summary>
	/// Zooms the map to the current device location
	/// </summary>
	[RelayCommand]
	private async Task ZoomToLocationAsync()
	{
		if (IsBusy || _map == null)
			return;

		try
		{
			IsBusy = true;

			var location = await _locationService.GetCurrentLocationAsync();
			if (location == null)
			{
				await ShowAlertAsync("Error", "Unable to get current location");
				return;
			}

			CurrentLatitude = location.Latitude;
			CurrentLongitude = location.Longitude;

			// Project to Web Mercator
			var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

			// Center and zoom
			_map.Navigator.CenterOn(x, y);
			_map.Navigator.ZoomTo(100000); // Approximate city-level zoom

			MapCenterLatitude = location.Latitude;
			MapCenterLongitude = location.Longitude;

			// Update current location marker
			UpdateCurrentLocationMarker(location);

			System.Diagnostics.Debug.WriteLine($"Zoomed to location: {location.Latitude}, {location.Longitude}");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to zoom to location");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Zooms the map to a specific feature
	/// </summary>
	[RelayCommand]
	private async Task ZoomToFeatureAsync(string? featureId)
	{
		if (IsBusy || _map == null || string.IsNullOrEmpty(featureId))
			return;

		try
		{
			IsBusy = true;

			var feature = await _featureRepository.GetByIdAsync(featureId);
			if (feature == null)
			{
				await ShowAlertAsync("Error", "Feature not found");
				return;
			}

			var geometry = feature.GetGeometry();
			if (geometry == null)
			{
				await ShowAlertAsync("Error", "Feature has no geometry");
				return;
			}

			// Get geometry centroid
			var centroid = geometry.Centroid;
			var (x, y) = SphericalMercator.FromLonLat(centroid.X, centroid.Y);

			// Zoom to feature
			_map.Navigator.CenterOn(x, y);
			_map.Navigator.ZoomTo(50000); // Closer zoom for individual features

			MapCenterLatitude = centroid.Y;
			MapCenterLongitude = centroid.X;

			System.Diagnostics.Debug.WriteLine($"Zoomed to feature: {featureId}");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to zoom to feature");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Selects a feature on the map
	/// </summary>
	[RelayCommand]
	private async Task SelectFeatureAsync(string? featureId)
	{
		if (string.IsNullOrEmpty(featureId))
		{
			SelectedFeatureId = null;
			SelectedFeature = null;
			return;
		}

		try
		{
			var feature = await _featureRepository.GetByIdAsync(featureId);
			if (feature != null)
			{
				SelectedFeatureId = featureId;
				SelectedFeature = feature;

				// Highlight selected feature
				HighlightFeature(featureId);

				System.Diagnostics.Debug.WriteLine($"Selected feature: {featureId}");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error selecting feature: {ex.Message}");
		}
	}

	/// <summary>
	/// Starts or continues drawing a geometry on the map
	/// </summary>
	[RelayCommand]
	private void DrawFeature(DrawingMode mode)
	{
		CurrentDrawingMode = mode;
		DrawingPoints.Clear();

		if (_drawingLayer != null)
		{
			_drawingLayer.Clear();
		}

		System.Diagnostics.Debug.WriteLine($"Started drawing in mode: {mode}");
	}

	/// <summary>
	/// Toggles GPS location tracking on/off
	/// </summary>
	[RelayCommand]
	private async Task ToggleLocationTrackingAsync()
	{
		try
		{
			if (IsTrackingLocation)
			{
				_locationService.LocationChanged -= OnLocationUpdated;
				await _locationService.StopTrackingAsync();
				IsTrackingLocation = false;
				System.Diagnostics.Debug.WriteLine("Location tracking stopped");
			}
			else
			{
				_locationService.LocationChanged += OnLocationUpdated;
				await _locationService.StartTrackingAsync();
				IsTrackingLocation = true;
				System.Diagnostics.Debug.WriteLine("Location tracking started");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to toggle location tracking");
		}
	}

	/// <summary>
	/// Toggles GPS track trail visibility
	/// </summary>
	[RelayCommand]
	private void ToggleGpsTrack()
	{
		ShowGpsTrack = !ShowGpsTrack;

		if (_gpsTrackLayer != null)
		{
			_gpsTrackLayer.Enabled = ShowGpsTrack;
		}

		if (!ShowGpsTrack)
		{
			GpsTrackPoints.Clear();
		}

		System.Diagnostics.Debug.WriteLine($"GPS track visibility: {ShowGpsTrack}");
	}

	/// <summary>
	/// Toggles base map layer visibility
	/// </summary>
	[RelayCommand]
	private void ToggleBaseMap()
	{
		IsBaseMapVisible = !IsBaseMapVisible;

		if (_map != null)
		{
			var baseLayer = _map.Layers.FindLayer("BaseMap").FirstOrDefault();
			if (baseLayer != null)
			{
				baseLayer.Enabled = IsBaseMapVisible;
			}
		}

		System.Diagnostics.Debug.WriteLine($"Base map visibility: {IsBaseMapVisible}");
	}

	/// <summary>
	/// Toggles visibility of a specific collection layer
	/// </summary>
	[RelayCommand]
	private async Task ToggleLayerVisibilityAsync(string? collectionId)
	{
		if (string.IsNullOrEmpty(collectionId))
			return;

		var layer = Layers.FirstOrDefault(l => l.CollectionId == collectionId);
		if (layer != null)
		{
			layer.IsVisible = !layer.IsVisible;

			// Refresh features to apply visibility change
			await LoadFeaturesAsync();

			System.Diagnostics.Debug.WriteLine($"Toggled layer {layer.Name} visibility: {layer.IsVisible}");
		}
	}

	/// <summary>
	/// Gets all features currently visible in the map viewport
	/// </summary>
	[RelayCommand]
	private async Task GetFeaturesInViewAsync()
	{
		if (_map == null)
			return;

		try
		{
			var extent = _map.Navigator.Viewport.Extent;
			if (extent == null)
				return;

			// Unproject bounds
			var (minLon, minLat) = UnprojectCoordinate(extent.MinX, extent.MinY);
			var (maxLon, maxLat) = UnprojectCoordinate(extent.MaxX, extent.MaxY);

			var features = await _featureRepository.GetByBoundsAsync(minLon, minLat, maxLon, maxLat);
			FeaturesInView = features.Count;

			System.Diagnostics.Debug.WriteLine($"Features in view: {FeaturesInView}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting features in view: {ex.Message}");
		}
	}

	/// <summary>
	/// Finds features near a specific location
	/// </summary>
	[RelayCommand]
	private async Task<List<Feature>> FindNearbyFeaturesAsync((double latitude, double longitude, double radiusMeters) parameters)
	{
		try
		{
			var geometryFactory = new NetTopologySuite.Geometries.GeometryFactory();
			var point = geometryFactory.CreatePoint(new Coordinate(parameters.longitude, parameters.latitude));

			var features = await _featureRepository.GetWithinDistanceAsync(point, parameters.radiusMeters);

			System.Diagnostics.Debug.WriteLine($"Found {features.Count} features within {parameters.radiusMeters}m");

			return features;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error finding nearby features: {ex.Message}");
			return new List<Feature>();
		}
	}

	/// <summary>
	/// Zooms the map to show all features
	/// </summary>
	[RelayCommand]
	private async Task ZoomToAllFeaturesAsync()
	{
		if (_map == null)
			return;

		try
		{
			var extent = await _featureRepository.GetExtentAsync();
			if (extent == null)
			{
				await ShowAlertAsync("Info", "No features to display");
				return;
			}

			var (minX, minY, maxX, maxY) = extent.Value;

			// Project bounds
			var (projMinX, projMinY) = SphericalMercator.FromLonLat(minX, minY);
			var (projMaxX, projMaxY) = SphericalMercator.FromLonLat(maxX, maxY);

			// Zoom to extent with padding
			var centerX = (projMinX + projMaxX) / 2;
			var centerY = (projMinY + projMaxY) / 2;

			_map.Navigator.CenterOn(centerX, centerY);
			_map.Navigator.ZoomToBox(new MRect(projMinX, projMinY, projMaxX, projMaxY));

			System.Diagnostics.Debug.WriteLine("Zoomed to all features");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to zoom to all features");
		}
	}

	/// <summary>
	/// Adds a point to the current drawing
	/// </summary>
	public void AddDrawingPoint(double latitude, double longitude)
	{
		if (CurrentDrawingMode == DrawingMode.None)
			return;

		var coord = new Coordinate(longitude, latitude);
		DrawingPoints.Add(coord);

		UpdateDrawingLayer();

		System.Diagnostics.Debug.WriteLine($"Added drawing point: {latitude}, {longitude}");
	}

	/// <summary>
	/// Completes the current drawing and creates a feature
	/// </summary>
	[RelayCommand]
	private async Task CompleteDrawingAsync()
	{
		if (CurrentDrawingMode == DrawingMode.None || DrawingPoints.Count == 0)
			return;

		try
		{
			NetTopologySuite.Geometries.Geometry? geometry = null;
			var factory = new NetTopologySuite.Geometries.GeometryFactory();

			switch (CurrentDrawingMode)
			{
				case DrawingMode.Point:
					if (DrawingPoints.Count >= 1)
						geometry = factory.CreatePoint(DrawingPoints[0]);
					break;

				case DrawingMode.Line:
					if (DrawingPoints.Count >= 2)
						geometry = factory.CreateLineString(DrawingPoints.ToArray());
					break;

				case DrawingMode.Polygon:
					if (DrawingPoints.Count >= 3)
					{
						// Close the polygon if not already closed
						var coords = DrawingPoints.ToList();
						if (!coords[0].Equals2D(coords[^1]))
							coords.Add(coords[0]);
						geometry = factory.CreatePolygon(coords.ToArray());
					}
					break;
			}

			if (geometry != null)
			{
				// Feature creation would happen here
				// For now, just clear the drawing
				System.Diagnostics.Debug.WriteLine($"Completed drawing: {CurrentDrawingMode}");
			}

			// Reset drawing
			CurrentDrawingMode = DrawingMode.None;
			DrawingPoints.Clear();

			if (_drawingLayer != null)
			{
				_drawingLayer.Clear();
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to complete drawing");
		}
	}

	/// <summary>
	/// Cancels the current drawing
	/// </summary>
	[RelayCommand]
	private void CancelDrawing()
	{
		CurrentDrawingMode = DrawingMode.None;
		DrawingPoints.Clear();

		if (_drawingLayer != null)
		{
			_drawingLayer.Clear();
		}

		System.Diagnostics.Debug.WriteLine("Drawing cancelled");
	}

	#endregion

	#region Offline Map Commands

	/// <summary>
	/// Downloads offline map tiles for the specified area
	/// </summary>
	[RelayCommand]
	private async Task DownloadOfflineMapAsync((string mapId, double minX, double minY, double maxX, double maxY, int minZoom, int maxZoom, string tileSourceId) parameters)
	{
		if (IsBusy || IsDownloadingOfflineMap)
			return;

		try
		{
			IsDownloadingOfflineMap = true;
			IsBusy = true;
			DownloadProgress = 0;
			DownloadStatusMessage = "Preparing download...";

			// Get tile source
			var tileSources = await _offlineMapService.GetTileSourcesAsync();
			var tileSource = tileSources.FirstOrDefault(s => s.Id == parameters.tileSourceId);

			if (tileSource == null)
			{
				await ShowAlertAsync("Error", "Tile source not found");
				return;
			}

			// Estimate download size
			var (estimatedBytes, tileCount) = await _offlineMapService.GetMapSizeAsync(
				(parameters.minX, parameters.minY, parameters.maxX, parameters.maxY),
				parameters.minZoom,
				parameters.maxZoom);

			var estimatedMB = estimatedBytes / 1024.0 / 1024.0;

			// Confirm download
			var confirmed = await ShowConfirmAsync(
				"Download Offline Map",
				$"This will download approximately {tileCount} tiles (~{estimatedMB:F1} MB). Continue?",
				"Download",
				"Cancel");

			if (!confirmed)
			{
				IsDownloadingOfflineMap = false;
				IsBusy = false;
				return;
			}

			// Download tiles
			var progress = new Progress<TileDownloadProgress>(p =>
			{
				DownloadProgress = p.PercentComplete;
				DownloadStatusMessage = p.Message;
			});

			var result = await _offlineMapService.DownloadTilesAsync(
				parameters.mapId,
				(parameters.minX, parameters.minY, parameters.maxX, parameters.maxY),
				parameters.minZoom,
				parameters.maxZoom,
				tileSource,
				progress);

			if (result.Success)
			{
				await ShowAlertAsync("Success",
					$"Downloaded {result.TilesDownloaded} tiles ({result.BytesDownloaded / 1024 / 1024:F1} MB) in {result.Duration.TotalSeconds:F1}s");

				// Refresh offline maps list
				await LoadOfflineMapsAsync();
			}
			else
			{
				await ShowAlertAsync("Error",
					$"Download failed: {result.ErrorMessage}\n{result.TilesDownloaded} tiles downloaded, {result.TilesFailed} failed");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to download offline map");
		}
		finally
		{
			IsDownloadingOfflineMap = false;
			IsBusy = false;
			DownloadProgress = 0;
			DownloadStatusMessage = string.Empty;
		}
	}

	/// <summary>
	/// Deletes offline map tiles
	/// </summary>
	[RelayCommand]
	private async Task DeleteOfflineMapAsync(string? mapId)
	{
		if (IsBusy || string.IsNullOrEmpty(mapId))
			return;

		try
		{
			var confirmed = await ShowConfirmAsync(
				"Delete Offline Map",
				"This will delete all downloaded tiles for this map. Continue?",
				"Delete",
				"Cancel");

			if (!confirmed)
				return;

			IsBusy = true;

			var success = await _offlineMapService.DeleteMapAsync(mapId);

			if (success)
			{
				await ShowAlertAsync("Success", "Offline map deleted");

				// Refresh offline maps list
				await LoadOfflineMapsAsync();

				// If this was the current offline map, switch back to online
				if (CurrentOfflineMapId == mapId)
				{
					CurrentOfflineMapId = null;
					UseOfflineMap = false;
					await SwitchToOnlineMapAsync();
				}
			}
			else
			{
				await ShowAlertAsync("Error", "Failed to delete offline map");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to delete offline map");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Loads the list of available offline maps
	/// </summary>
	[RelayCommand]
	private async Task LoadOfflineMapsAsync()
	{
		try
		{
			var maps = await _offlineMapService.GetAvailableMapsAsync();

			OfflineMaps.Clear();
			foreach (var map in maps)
			{
				OfflineMaps.Add(map);
			}

			System.Diagnostics.Debug.WriteLine($"Loaded {OfflineMaps.Count} offline maps");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading offline maps: {ex.Message}");
		}
	}

	/// <summary>
	/// Switches to using an offline map
	/// </summary>
	[RelayCommand]
	private async Task SwitchToOfflineMapAsync(string? mapId)
	{
		if (IsBusy || string.IsNullOrEmpty(mapId) || _map == null)
			return;

		try
		{
			IsBusy = true;

			// Check if map exists
			var offlineMap = OfflineMaps.FirstOrDefault(m => m.MapId == mapId);
			if (offlineMap == null)
			{
				await ShowAlertAsync("Error", "Offline map not found");
				return;
			}

			// Remove current base map layer
			var currentBaseLayer = _map.Layers.FindLayer("BaseMap").FirstOrDefault();
			if (currentBaseLayer != null)
			{
				_map.Layers.Remove(currentBaseLayer);
			}

			// Create offline tile provider
			var offlineProvider = new OfflineTileProvider(_offlineMapService, mapId, null, offlineOnly: true);
			var offlineLayer = new TileLayer(offlineProvider)
			{
				Name = "BaseMap"
			};

			_map.Layers.Insert(0, offlineLayer);

			CurrentOfflineMapId = mapId;
			UseOfflineMap = true;

			System.Diagnostics.Debug.WriteLine($"Switched to offline map: {mapId}");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to switch to offline map");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Switches back to online map
	/// </summary>
	[RelayCommand]
	private async Task SwitchToOnlineMapAsync()
	{
		if (IsBusy || _map == null)
			return;

		try
		{
			IsBusy = true;

			// Remove current base map layer
			var currentBaseLayer = _map.Layers.FindLayer("BaseMap").FirstOrDefault();
			if (currentBaseLayer != null)
			{
				_map.Layers.Remove(currentBaseLayer);
			}

			// Add online tile layer
			var onlineLayer = OpenStreetMap.CreateTileLayer();
			onlineLayer.Name = "BaseMap";
			_map.Layers.Insert(0, onlineLayer);

			CurrentOfflineMapId = null;
			UseOfflineMap = false;

			System.Diagnostics.Debug.WriteLine("Switched to online map");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to switch to online map");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Gets storage information for offline maps
	/// </summary>
	[RelayCommand]
	private async Task GetOfflineMapStorageInfoAsync()
	{
		try
		{
			var totalStorage = await _offlineMapService.GetTotalStorageUsedAsync();
			var totalMB = totalStorage / 1024.0 / 1024.0;

			await ShowAlertAsync("Storage Info",
				$"Total offline map storage: {totalMB:F1} MB\n" +
				$"Number of maps: {OfflineMaps.Count}");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to get storage info");
		}
	}

	#endregion

	#region Helper Methods

	/// <summary>
	/// Callback for location updates during tracking
	/// </summary>
	private void OnLocationUpdated(object? sender, LocationChangedEventArgs e)
	{
		var location = e.Location;
		CurrentLatitude = location.Latitude;
		CurrentLongitude = location.Longitude;

		UpdateCurrentLocationMarker(location);

		if (ShowGpsTrack)
		{
			GpsTrackPoints.Add(location);
			UpdateGpsTrackLayer();
		}
	}

	/// <summary>
	/// Updates the current location marker on the map
	/// </summary>
	private void UpdateCurrentLocationMarker(LocationInfo location)
	{
		if (_currentLocationLayer == null || !ShowCurrentLocation)
			return;

		_currentLocationLayer.Clear();

		var (x, y) = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

		var factory = new NetTopologySuite.Geometries.GeometryFactory();
		var point = factory.CreatePoint(new Coordinate(x, y));

		var feature = new GeometryFeature
		{
			Geometry = point
		};

		feature.Styles.Add(new SymbolStyle
		{
			SymbolScale = 1.0,
			Fill = new Brush(Mapsui.Styles.Color.Red),
			Outline = new Pen(Mapsui.Styles.Color.White, 3)
		});

		_currentLocationLayer.Add(feature);
	}

	/// <summary>
	/// Updates the GPS track trail layer
	/// </summary>
	private void UpdateGpsTrackLayer()
	{
		if (_gpsTrackLayer == null || GpsTrackPoints.Count < 2)
			return;

		_gpsTrackLayer.Clear();

		var factory = new NetTopologySuite.Geometries.GeometryFactory();
		var coordinates = GpsTrackPoints
			.Select(p =>
			{
				var (x, y) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
				return new Coordinate(x, y);
			})
			.ToArray();

		var lineString = factory.CreateLineString(coordinates);

		var feature = new GeometryFeature
		{
			Geometry = lineString
		};

		feature.Styles.Add(new VectorStyle
		{
			Line = new Pen(Mapsui.Styles.Color.FromArgb(180, 255, 0, 0), 3)
		});

		_gpsTrackLayer.Add(feature);
	}

	/// <summary>
	/// Updates the drawing layer with current drawing points
	/// </summary>
	private void UpdateDrawingLayer()
	{
		if (_drawingLayer == null || DrawingPoints.Count == 0)
			return;

		_drawingLayer.Clear();

		var factory = new NetTopologySuite.Geometries.GeometryFactory();

		// Convert coordinates to Web Mercator
		var projectedCoords = DrawingPoints
			.Select(c =>
			{
				var (x, y) = SphericalMercator.FromLonLat(c.X, c.Y);
				return new Coordinate(x, y);
			})
			.ToArray();

		NetTopologySuite.Geometries.Geometry? geometry = null;

		if (CurrentDrawingMode == DrawingMode.Point && projectedCoords.Length >= 1)
		{
			geometry = factory.CreatePoint(projectedCoords[0]);
		}
		else if (CurrentDrawingMode == DrawingMode.Line && projectedCoords.Length >= 1)
		{
			// Show line segments as we draw
			geometry = projectedCoords.Length == 1
				? factory.CreatePoint(projectedCoords[0])
				: factory.CreateLineString(projectedCoords);
		}
		else if (CurrentDrawingMode == DrawingMode.Polygon && projectedCoords.Length >= 1)
		{
			// Show polygon outline as we draw
			if (projectedCoords.Length == 1)
			{
				geometry = factory.CreatePoint(projectedCoords[0]);
			}
			else if (projectedCoords.Length == 2)
			{
				geometry = factory.CreateLineString(projectedCoords);
			}
			else
			{
				// Close the ring for preview
				var ringCoords = projectedCoords.ToList();
				ringCoords.Add(projectedCoords[0]);
				geometry = factory.CreatePolygon(ringCoords.ToArray());
			}
		}

		if (geometry != null)
		{
			var feature = new GeometryFeature { Geometry = geometry };

			feature.Styles.Add(new VectorStyle
			{
				Fill = new Brush(Mapsui.Styles.Color.FromArgb(80, 255, 165, 0)),
				Outline = new Pen(Mapsui.Styles.Color.Orange, 3),
				Line = new Pen(Mapsui.Styles.Color.Orange, 3)
			});

			_drawingLayer.Add(feature);
		}
	}

	/// <summary>
	/// Highlights a selected feature on the map
	/// </summary>
	private void HighlightFeature(string featureId)
	{
		if (_featuresLayer == null)
			return;

		// In a full implementation, this would add a highlight style
		// to the selected feature
		System.Diagnostics.Debug.WriteLine($"Highlighting feature: {featureId}");
	}

	#endregion

	#region Lifecycle

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();

		if (!IsMapInitialized)
		{
			await InitializeMapAsync();
		}
	}

	public override async Task OnDisappearingAsync()
	{
		await base.OnDisappearingAsync();

		// Stop location tracking when view disappears
		if (IsTrackingLocation)
		{
			_locationService.LocationChanged -= OnLocationUpdated;
			await _locationService.StopTrackingAsync();
			IsTrackingLocation = false;
		}
	}

	#endregion

	#region IDisposable

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
			// Stop tracking and unsubscribe from events
			if (IsTrackingLocation)
			{
				_locationService.LocationChanged -= OnLocationUpdated;
				_locationService.StopTrackingAsync().Wait();
			}

			// Clear layers
			_featuresLayer?.Clear();
			_currentLocationLayer?.Clear();
			_gpsTrackLayer?.Clear();
			_drawingLayer?.Clear();

			// Dispose map
			_map?.Dispose();

			// Dispose location service
			_locationService?.Dispose();
		}

		_disposed = true;
	}

	#endregion
}

/// <summary>
/// Drawing mode enumeration for geometry editing
/// </summary>
public enum DrawingMode
{
	None,
	Point,
	Line,
	Polygon
}

/// <summary>
/// Collection layer information for UI binding
/// </summary>
public class CollectionLayerInfo : ObservableObject
{
	private bool _isVisible;

	public string CollectionId { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
	public int FeatureCount { get; set; }

	public bool IsVisible
	{
		get => _isVisible;
		set => SetProperty(ref _isVisible, value);
	}
}
