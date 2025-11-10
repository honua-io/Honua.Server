using HonuaField.Models;

namespace HonuaField.Services;

/// <summary>
/// Service interface for offline map tile management
/// Handles tile downloading, storage, deletion, and size estimation
/// </summary>
public interface IOfflineMapService
{
	/// <summary>
	/// Downloads map tiles for the specified area and zoom levels
	/// </summary>
	/// <param name="mapId">ID of the map configuration</param>
	/// <param name="bounds">Bounding box (minX, minY, maxX, maxY) in WGS84</param>
	/// <param name="minZoom">Minimum zoom level</param>
	/// <param name="maxZoom">Maximum zoom level</param>
	/// <param name="tileSource">Tile source configuration</param>
	/// <param name="progress">Progress reporter</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Download result with statistics</returns>
	Task<TileDownloadResult> DownloadTilesAsync(
		string mapId,
		(double minX, double minY, double maxX, double maxY) bounds,
		int minZoom,
		int maxZoom,
		TileSource tileSource,
		IProgress<TileDownloadProgress>? progress = null,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Deletes all offline tiles for a map
	/// </summary>
	/// <param name="mapId">ID of the map to delete</param>
	/// <returns>True if successful</returns>
	Task<bool> DeleteMapAsync(string mapId);

	/// <summary>
	/// Gets a list of all available offline maps
	/// </summary>
	/// <returns>List of offline map information</returns>
	Task<List<OfflineMapInfo>> GetAvailableMapsAsync();

	/// <summary>
	/// Estimates the download size for the specified area and zoom levels
	/// </summary>
	/// <param name="bounds">Bounding box (minX, minY, maxX, maxY) in WGS84</param>
	/// <param name="minZoom">Minimum zoom level</param>
	/// <param name="maxZoom">Maximum zoom level</param>
	/// <returns>Estimated size in bytes and tile count</returns>
	Task<(long estimatedBytes, int tileCount)> GetMapSizeAsync(
		(double minX, double minY, double maxX, double maxY) bounds,
		int minZoom,
		int maxZoom);

	/// <summary>
	/// Gets available tile sources (OSM, satellite, etc.)
	/// </summary>
	/// <returns>List of tile sources</returns>
	Task<List<TileSource>> GetTileSourcesAsync();

	/// <summary>
	/// Gets a specific tile from offline storage
	/// </summary>
	/// <param name="mapId">ID of the map</param>
	/// <param name="zoom">Zoom level</param>
	/// <param name="x">Tile X coordinate</param>
	/// <param name="y">Tile Y coordinate</param>
	/// <returns>Tile data as byte array, or null if not found</returns>
	Task<byte[]?> GetTileAsync(string mapId, int zoom, int x, int y);

	/// <summary>
	/// Checks if a tile exists in offline storage
	/// </summary>
	/// <param name="mapId">ID of the map</param>
	/// <param name="zoom">Zoom level</param>
	/// <param name="x">Tile X coordinate</param>
	/// <param name="y">Tile Y coordinate</param>
	/// <returns>True if tile exists</returns>
	Task<bool> TileExistsAsync(string mapId, int zoom, int x, int y);

	/// <summary>
	/// Gets the storage path for offline tiles
	/// </summary>
	/// <returns>Full path to tiles directory</returns>
	string GetTilesDirectory();

	/// <summary>
	/// Gets the total storage used by all offline maps
	/// </summary>
	/// <returns>Total storage in bytes</returns>
	Task<long> GetTotalStorageUsedAsync();

	/// <summary>
	/// Clears old or unused tiles based on storage quota
	/// </summary>
	/// <param name="maxStorageBytes">Maximum storage allowed in bytes</param>
	/// <returns>Number of bytes freed</returns>
	Task<long> CleanupStorageAsync(long maxStorageBytes);
}

/// <summary>
/// Tile source configuration
/// </summary>
public class TileSource
{
	/// <summary>
	/// Unique identifier for the tile source
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// Display name
	/// </summary>
	public required string Name { get; init; }

	/// <summary>
	/// URL template (e.g., "https://tile.openstreetmap.org/{z}/{x}/{y}.png")
	/// </summary>
	public required string UrlTemplate { get; init; }

	/// <summary>
	/// Minimum zoom level
	/// </summary>
	public int MinZoom { get; init; } = 0;

	/// <summary>
	/// Maximum zoom level
	/// </summary>
	public int MaxZoom { get; init; } = 19;

	/// <summary>
	/// Attribution text
	/// </summary>
	public string? Attribution { get; init; }

	/// <summary>
	/// Whether authentication is required
	/// </summary>
	public bool RequiresAuth { get; init; }

	/// <summary>
	/// API key or token (if required)
	/// </summary>
	public string? ApiKey { get; init; }
}

/// <summary>
/// Offline map information
/// </summary>
public class OfflineMapInfo
{
	/// <summary>
	/// Map ID
	/// </summary>
	public required string MapId { get; init; }

	/// <summary>
	/// Map title
	/// </summary>
	public required string Title { get; init; }

	/// <summary>
	/// Tile source used
	/// </summary>
	public required string TileSourceId { get; init; }

	/// <summary>
	/// Download size in bytes
	/// </summary>
	public long DownloadSize { get; init; }

	/// <summary>
	/// Number of tiles downloaded
	/// </summary>
	public int TileCount { get; init; }

	/// <summary>
	/// Downloaded extent
	/// </summary>
	public string? DownloadedExtent { get; init; }

	/// <summary>
	/// Minimum zoom level
	/// </summary>
	public int MinZoom { get; init; }

	/// <summary>
	/// Maximum zoom level
	/// </summary>
	public int MaxZoom { get; init; }
}

/// <summary>
/// Tile download progress information
/// </summary>
public class TileDownloadProgress
{
	/// <summary>
	/// Current stage of download
	/// </summary>
	public TileDownloadStage Stage { get; init; }

	/// <summary>
	/// Progress message
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	/// Current tile being processed
	/// </summary>
	public int CurrentTile { get; init; }

	/// <summary>
	/// Total tiles to download
	/// </summary>
	public int TotalTiles { get; init; }

	/// <summary>
	/// Percentage complete (0-100)
	/// </summary>
	public double PercentComplete { get; init; }

	/// <summary>
	/// Current zoom level being processed
	/// </summary>
	public int CurrentZoom { get; init; }

	/// <summary>
	/// Bytes downloaded so far
	/// </summary>
	public long BytesDownloaded { get; init; }

	/// <summary>
	/// Failed tile count
	/// </summary>
	public int FailedTiles { get; init; }
}

/// <summary>
/// Tile download stages
/// </summary>
public enum TileDownloadStage
{
	Initializing,
	Calculating,
	Downloading,
	Saving,
	Completed,
	Failed,
	Cancelled
}

/// <summary>
/// Tile download result
/// </summary>
public class TileDownloadResult
{
	/// <summary>
	/// Whether the download was successful
	/// </summary>
	public bool Success { get; init; }

	/// <summary>
	/// Number of tiles successfully downloaded
	/// </summary>
	public int TilesDownloaded { get; init; }

	/// <summary>
	/// Number of tiles that failed to download
	/// </summary>
	public int TilesFailed { get; init; }

	/// <summary>
	/// Total bytes downloaded
	/// </summary>
	public long BytesDownloaded { get; init; }

	/// <summary>
	/// Error message if failed
	/// </summary>
	public string? ErrorMessage { get; init; }

	/// <summary>
	/// Duration of download
	/// </summary>
	public TimeSpan Duration { get; init; }
}
