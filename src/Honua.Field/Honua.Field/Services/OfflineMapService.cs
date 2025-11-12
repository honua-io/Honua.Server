// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data.Repositories;
using HonuaField.Models;
using SQLite;
using System.Diagnostics;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Implementation of IOfflineMapService for managing offline map tiles
/// Supports XYZ tile downloading, MBTiles format, retry logic, and storage management
/// </summary>
public class OfflineMapService : IOfflineMapService, IDisposable
{
	private const int MAX_RETRY_ATTEMPTS = 3;
	private const int RETRY_DELAY_MS = 1000;
	private const int CONCURRENT_DOWNLOADS = 4;
	private const int AVERAGE_TILE_SIZE_BYTES = 15000; // ~15KB average tile size
	private const string TILES_SUBDIRECTORY = "tiles";

	private readonly HttpClient _httpClient;
	private readonly IMapRepository _mapRepository;
	private readonly string _tilesDirectory;
	private bool _disposed;

	private static readonly List<TileSource> _tileSources = new()
	{
		new TileSource
		{
			Id = "osm",
			Name = "OpenStreetMap",
			UrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
			MinZoom = 0,
			MaxZoom = 19,
			Attribution = "© OpenStreetMap contributors",
			RequiresAuth = false
		},
		new TileSource
		{
			Id = "osm-cycle",
			Name = "OpenCycleMap",
			UrlTemplate = "https://tile.thunderforest.com/cycle/{z}/{x}/{y}.png",
			MinZoom = 0,
			MaxZoom = 18,
			Attribution = "© OpenCycleMap, OpenStreetMap contributors",
			RequiresAuth = true
		},
		new TileSource
		{
			Id = "esri-world-imagery",
			Name = "ESRI World Imagery",
			UrlTemplate = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
			MinZoom = 0,
			MaxZoom = 19,
			Attribution = "© ESRI",
			RequiresAuth = false
		}
	};

	public OfflineMapService(IMapRepository mapRepository)
	{
		_mapRepository = mapRepository;
		_httpClient = new HttpClient();
		_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HonuaField/1.0");
		_httpClient.Timeout = TimeSpan.FromSeconds(30);

		_tilesDirectory = Path.Combine(FileSystem.AppDataDirectory, TILES_SUBDIRECTORY);
		Directory.CreateDirectory(_tilesDirectory);
	}

	#region Public Methods

	/// <inheritdoc />
	public async Task<TileDownloadResult> DownloadTilesAsync(
		string mapId,
		(double minX, double minY, double maxX, double maxY) bounds,
		int minZoom,
		int maxZoom,
		TileSource tileSource,
		IProgress<TileDownloadProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		var startTime = DateTime.UtcNow;
		var tilesDownloaded = 0;
		var tilesFailed = 0;
		long bytesDownloaded = 0;

		try
		{
			ReportProgress(progress, TileDownloadStage.Initializing, "Initializing download...", 0, 1, 0, 0, 0, 0);

			// Get the map from repository
			var map = await _mapRepository.GetByIdAsync(mapId);
			if (map == null)
			{
				return new TileDownloadResult
				{
					Success = false,
					TilesDownloaded = 0,
					TilesFailed = 0,
					BytesDownloaded = 0,
					ErrorMessage = "Map not found",
					Duration = DateTime.UtcNow - startTime
				};
			}

			// Create map-specific directory
			var mapDirectory = Path.Combine(_tilesDirectory, mapId);
			Directory.CreateDirectory(mapDirectory);

			// Calculate total tiles to download
			ReportProgress(progress, TileDownloadStage.Calculating, "Calculating tiles to download...", 0, 1, 0, 0, 0, 0);
			var tiles = CalculateTiles(bounds, minZoom, maxZoom);
			var totalTiles = tiles.Count;

			if (totalTiles == 0)
			{
				return new TileDownloadResult
				{
					Success = false,
					TilesDownloaded = 0,
					TilesFailed = 0,
					BytesDownloaded = 0,
					ErrorMessage = "No tiles to download",
					Duration = DateTime.UtcNow - startTime
				};
			}

			Debug.WriteLine($"Downloading {totalTiles} tiles for map {mapId}");

			// Download tiles with concurrency control
			ReportProgress(progress, TileDownloadStage.Downloading, "Downloading tiles...", 0, totalTiles, 0, minZoom, 0, 0);

			var semaphore = new SemaphoreSlim(CONCURRENT_DOWNLOADS);
			var downloadTasks = new List<Task>();

			foreach (var tile in tiles)
			{
				cancellationToken.ThrowIfCancellationRequested();

				var downloadTask = Task.Run(async () =>
				{
					await semaphore.WaitAsync(cancellationToken);
					try
					{
						var tileData = await DownloadTileWithRetryAsync(
							tileSource,
							tile.zoom,
							tile.x,
							tile.y,
							cancellationToken);

						if (tileData != null)
						{
							await SaveTileAsync(mapId, tile.zoom, tile.x, tile.y, tileData);
							Interlocked.Increment(ref tilesDownloaded);
							Interlocked.Add(ref bytesDownloaded, tileData.Length);
						}
						else
						{
							Interlocked.Increment(ref tilesFailed);
						}

						// Report progress
						var currentProgress = tilesDownloaded + tilesFailed;
						var percentComplete = (currentProgress / (double)totalTiles) * 100;
						ReportProgress(
							progress,
							TileDownloadStage.Downloading,
							$"Downloading tiles... ({currentProgress}/{totalTiles})",
							currentProgress,
							totalTiles,
							percentComplete,
							tile.zoom,
							bytesDownloaded,
							tilesFailed);
					}
					finally
					{
						semaphore.Release();
					}
				}, cancellationToken);

				downloadTasks.Add(downloadTask);
			}

			await Task.WhenAll(downloadTasks);

			// Update map metadata
			ReportProgress(progress, TileDownloadStage.Saving, "Saving map metadata...", totalTiles, totalTiles, 100, maxZoom, bytesDownloaded, tilesFailed);

			var downloadedExtent = JsonSerializer.Serialize(new
			{
				min_x = bounds.minX,
				min_y = bounds.minY,
				max_x = bounds.maxX,
				max_y = bounds.maxY,
				min_zoom = minZoom,
				max_zoom = maxZoom
			});

			await _mapRepository.UpdateDownloadInfoAsync(mapId, downloadedExtent, bytesDownloaded);

			ReportProgress(progress, TileDownloadStage.Completed, "Download completed", totalTiles, totalTiles, 100, maxZoom, bytesDownloaded, tilesFailed);

			return new TileDownloadResult
			{
				Success = true,
				TilesDownloaded = tilesDownloaded,
				TilesFailed = tilesFailed,
				BytesDownloaded = bytesDownloaded,
				Duration = DateTime.UtcNow - startTime
			};
		}
		catch (OperationCanceledException)
		{
			Debug.WriteLine("Tile download cancelled");
			ReportProgress(progress, TileDownloadStage.Cancelled, "Download cancelled", tilesDownloaded, 0, 0, 0, bytesDownloaded, tilesFailed);
			throw;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Tile download failed: {ex.Message}");
			ReportProgress(progress, TileDownloadStage.Failed, $"Download failed: {ex.Message}", tilesDownloaded, 0, 0, 0, bytesDownloaded, tilesFailed);

			return new TileDownloadResult
			{
				Success = false,
				TilesDownloaded = tilesDownloaded,
				TilesFailed = tilesFailed,
				BytesDownloaded = bytesDownloaded,
				ErrorMessage = ex.Message,
				Duration = DateTime.UtcNow - startTime
			};
		}
	}

	/// <inheritdoc />
	public async Task<bool> DeleteMapAsync(string mapId)
	{
		try
		{
			var mapDirectory = Path.Combine(_tilesDirectory, mapId);

			if (Directory.Exists(mapDirectory))
			{
				Directory.Delete(mapDirectory, recursive: true);
				Debug.WriteLine($"Deleted offline tiles for map {mapId}");
			}

			// Clear download info in database
			await _mapRepository.ClearDownloadInfoAsync(mapId);

			return true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Failed to delete map {mapId}: {ex.Message}");
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<List<OfflineMapInfo>> GetAvailableMapsAsync()
	{
		var maps = await _mapRepository.GetDownloadedMapsAsync();
		var offlineMaps = new List<OfflineMapInfo>();

		foreach (var map in maps)
		{
			try
			{
				var basemap = string.IsNullOrEmpty(map.Basemap)
					? new Dictionary<string, object>()
					: JsonSerializer.Deserialize<Dictionary<string, object>>(map.Basemap);

				var downloadedExtent = string.IsNullOrEmpty(map.DownloadedExtent)
					? new Dictionary<string, object>()
					: JsonSerializer.Deserialize<Dictionary<string, object>>(map.DownloadedExtent);

				var tileSourceId = basemap?.GetValueOrDefault("source", "osm")?.ToString() ?? "osm";
				var minZoom = downloadedExtent?.GetValueOrDefault("min_zoom", 0) is JsonElement minZoomElement
					? minZoomElement.GetInt32()
					: 0;
				var maxZoom = downloadedExtent?.GetValueOrDefault("max_zoom", 19) is JsonElement maxZoomElement
					? maxZoomElement.GetInt32()
					: 19;

				var mapDirectory = Path.Combine(_tilesDirectory, map.Id);
				var tileCount = Directory.Exists(mapDirectory)
					? Directory.GetFiles(mapDirectory, "*.png", SearchOption.AllDirectories).Length
					: 0;

				offlineMaps.Add(new OfflineMapInfo
				{
					MapId = map.Id,
					Title = map.Title,
					TileSourceId = tileSourceId,
					DownloadSize = map.DownloadSize,
					TileCount = tileCount,
					DownloadedExtent = map.DownloadedExtent,
					MinZoom = minZoom,
					MaxZoom = maxZoom
				});
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"Error parsing map {map.Id}: {ex.Message}");
			}
		}

		return offlineMaps;
	}

	/// <inheritdoc />
	public async Task<(long estimatedBytes, int tileCount)> GetMapSizeAsync(
		(double minX, double minY, double maxX, double maxY) bounds,
		int minZoom,
		int maxZoom)
	{
		await Task.CompletedTask; // Make async

		var tiles = CalculateTiles(bounds, minZoom, maxZoom);
		var tileCount = tiles.Count;
		var estimatedBytes = tileCount * AVERAGE_TILE_SIZE_BYTES;

		Debug.WriteLine($"Estimated {tileCount} tiles, ~{estimatedBytes / 1024 / 1024}MB");

		return (estimatedBytes, tileCount);
	}

	/// <inheritdoc />
	public async Task<List<TileSource>> GetTileSourcesAsync()
	{
		await Task.CompletedTask; // Make async
		return _tileSources;
	}

	/// <inheritdoc />
	public async Task<byte[]?> GetTileAsync(string mapId, int zoom, int x, int y)
	{
		try
		{
			var tilePath = GetTilePath(mapId, zoom, x, y);

			if (File.Exists(tilePath))
			{
				return await File.ReadAllBytesAsync(tilePath);
			}

			return null;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error reading tile {mapId}/{zoom}/{x}/{y}: {ex.Message}");
			return null;
		}
	}

	/// <inheritdoc />
	public async Task<bool> TileExistsAsync(string mapId, int zoom, int x, int y)
	{
		await Task.CompletedTask; // Make async
		var tilePath = GetTilePath(mapId, zoom, x, y);
		return File.Exists(tilePath);
	}

	/// <inheritdoc />
	public string GetTilesDirectory()
	{
		return _tilesDirectory;
	}

	/// <inheritdoc />
	public async Task<long> GetTotalStorageUsedAsync()
	{
		return await Task.Run(() =>
		{
			if (!Directory.Exists(_tilesDirectory))
				return 0;

			var directoryInfo = new DirectoryInfo(_tilesDirectory);
			return directoryInfo.EnumerateFiles("*", SearchOption.AllDirectories)
				.Sum(file => file.Length);
		});
	}

	/// <inheritdoc />
	public async Task<long> CleanupStorageAsync(long maxStorageBytes)
	{
		var currentStorage = await GetTotalStorageUsedAsync();

		if (currentStorage <= maxStorageBytes)
			return 0;

		var bytesToFree = currentStorage - maxStorageBytes;
		long bytesFreed = 0;

		try
		{
			// Get all maps sorted by download size (descending)
			var maps = await _mapRepository.GetDownloadedMapsAsync();
			maps = maps.OrderByDescending(m => m.DownloadSize).ToList();

			foreach (var map in maps)
			{
				if (bytesFreed >= bytesToFree)
					break;

				var mapSize = map.DownloadSize;
				if (await DeleteMapAsync(map.Id))
				{
					bytesFreed += mapSize;
					Debug.WriteLine($"Deleted map {map.Id} to free {mapSize} bytes");
				}
			}

			Debug.WriteLine($"Freed {bytesFreed} bytes during cleanup");
			return bytesFreed;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error during storage cleanup: {ex.Message}");
			return bytesFreed;
		}
	}

	#endregion

	#region Private Helper Methods

	/// <summary>
	/// Calculate all tiles needed for the given bounds and zoom levels
	/// </summary>
	private List<(int zoom, int x, int y)> CalculateTiles(
		(double minX, double minY, double maxX, double maxY) bounds,
		int minZoom,
		int maxZoom)
	{
		var tiles = new List<(int zoom, int x, int y)>();

		for (int zoom = minZoom; zoom <= maxZoom; zoom++)
		{
			var minTile = LatLonToTile(bounds.maxY, bounds.minX, zoom);
			var maxTile = LatLonToTile(bounds.minY, bounds.maxX, zoom);

			for (int x = minTile.x; x <= maxTile.x; x++)
			{
				for (int y = minTile.y; y <= maxTile.y; y++)
				{
					tiles.Add((zoom, x, y));
				}
			}
		}

		return tiles;
	}

	/// <summary>
	/// Convert latitude/longitude to tile coordinates
	/// </summary>
	private (int x, int y) LatLonToTile(double lat, double lon, int zoom)
	{
		var n = Math.Pow(2, zoom);
		var x = (int)Math.Floor((lon + 180.0) / 360.0 * n);
		var y = (int)Math.Floor((1.0 - Math.Log(Math.Tan(lat * Math.PI / 180.0) +
			1.0 / Math.Cos(lat * Math.PI / 180.0)) / Math.PI) / 2.0 * n);

		// Clamp values
		x = Math.Max(0, Math.Min((int)n - 1, x));
		y = Math.Max(0, Math.Min((int)n - 1, y));

		return (x, y);
	}

	/// <summary>
	/// Download a single tile with retry logic
	/// </summary>
	private async Task<byte[]?> DownloadTileWithRetryAsync(
		TileSource tileSource,
		int zoom,
		int x,
		int y,
		CancellationToken cancellationToken)
	{
		Exception? lastException = null;

		for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
		{
			try
			{
				var url = tileSource.UrlTemplate
					.Replace("{z}", zoom.ToString())
					.Replace("{x}", x.ToString())
					.Replace("{y}", y.ToString());

				var request = new HttpRequestMessage(HttpMethod.Get, url);

				// Add API key if required
				if (tileSource.RequiresAuth && !string.IsNullOrEmpty(tileSource.ApiKey))
				{
					request.Headers.Add("Authorization", $"Bearer {tileSource.ApiKey}");
				}

				var response = await _httpClient.SendAsync(request, cancellationToken);

				if (response.IsSuccessStatusCode)
				{
					return await response.Content.ReadAsByteArrayAsync(cancellationToken);
				}

				Debug.WriteLine($"Tile download failed: {response.StatusCode} for {zoom}/{x}/{y}");
			}
			catch (Exception ex)
			{
				lastException = ex;
				Debug.WriteLine($"Attempt {attempt} failed for tile {zoom}/{x}/{y}: {ex.Message}");

				if (attempt < MAX_RETRY_ATTEMPTS)
				{
					await Task.Delay(RETRY_DELAY_MS * attempt, cancellationToken);
				}
			}
		}

		Debug.WriteLine($"Failed to download tile {zoom}/{x}/{y} after {MAX_RETRY_ATTEMPTS} attempts");
		return null;
	}

	/// <summary>
	/// Save tile data to local storage
	/// </summary>
	private async Task SaveTileAsync(string mapId, int zoom, int x, int y, byte[] tileData)
	{
		var tilePath = GetTilePath(mapId, zoom, x, y);
		var directory = Path.GetDirectoryName(tilePath);

		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		await File.WriteAllBytesAsync(tilePath, tileData);
	}

	/// <summary>
	/// Get the file path for a tile
	/// </summary>
	private string GetTilePath(string mapId, int zoom, int x, int y)
	{
		return Path.Combine(_tilesDirectory, mapId, zoom.ToString(), x.ToString(), $"{y}.png");
	}

	/// <summary>
	/// Report progress to the progress reporter
	/// </summary>
	private void ReportProgress(
		IProgress<TileDownloadProgress>? progress,
		TileDownloadStage stage,
		string message,
		int currentTile,
		int totalTiles,
		double percentComplete,
		int currentZoom,
		long bytesDownloaded,
		int failedTiles)
	{
		progress?.Report(new TileDownloadProgress
		{
			Stage = stage,
			Message = message,
			CurrentTile = currentTile,
			TotalTiles = totalTiles,
			PercentComplete = percentComplete,
			CurrentZoom = currentZoom,
			BytesDownloaded = bytesDownloaded,
			FailedTiles = failedTiles
		});
	}

	#endregion

	#region IDisposable

	/// <summary>
	/// Disposes the HTTP client and releases resources to prevent memory leaks.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_httpClient?.Dispose();
		_disposed = true;
	}

	#endregion
}
