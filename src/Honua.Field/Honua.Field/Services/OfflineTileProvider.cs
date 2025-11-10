using BruTile;
using BruTile.Cache;
using Mapsui.Layers;
using System.Diagnostics;

namespace HonuaField.Services;

/// <summary>
/// Custom tile provider for Mapsui that reads from offline storage
/// Falls back to online tiles when offline tiles are not available
/// </summary>
public class OfflineTileProvider : ITileSource
{
	private readonly IOfflineMapService _offlineMapService;
	private readonly string _mapId;
	private readonly ITileSource? _onlineTileSource;
	private readonly bool _offlineOnly;

	/// <summary>
	/// Initializes a new instance of OfflineTileProvider
	/// </summary>
	/// <param name="offlineMapService">Offline map service</param>
	/// <param name="mapId">Map ID for offline tiles</param>
	/// <param name="onlineTileSource">Optional online tile source for fallback</param>
	/// <param name="offlineOnly">If true, only use offline tiles (no fallback)</param>
	public OfflineTileProvider(
		IOfflineMapService offlineMapService,
		string mapId,
		ITileSource? onlineTileSource = null,
		bool offlineOnly = false)
	{
		_offlineMapService = offlineMapService;
		_mapId = mapId;
		_onlineTileSource = onlineTileSource;
		_offlineOnly = offlineOnly;

		// Set schema from online source or create default
		Schema = onlineTileSource?.Schema ?? CreateDefaultSchema();
		Name = $"Offline-{mapId}";
		Attribution = new Attribution("Offline tiles", onlineTileSource?.Attribution?.Url);
	}

	/// <inheritdoc />
	public ITileSchema Schema { get; }

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public Attribution Attribution { get; }

	/// <inheritdoc />
	public async Task<byte[]?> GetTileAsync(TileInfo tileInfo)
	{
		try
		{
			// Convert TileInfo to tile coordinates
			var zoom = int.Parse(tileInfo.Index.Level);
			var x = tileInfo.Index.Col;
			var y = tileInfo.Index.Row;

			// Try to get tile from offline storage
			var offlineTile = await _offlineMapService.GetTileAsync(_mapId, zoom, x, y);

			if (offlineTile != null)
			{
				Debug.WriteLine($"Loaded offline tile: {zoom}/{x}/{y}");
				return offlineTile;
			}

			// Fallback to online if available and not in offline-only mode
			if (!_offlineOnly && _onlineTileSource != null)
			{
				Debug.WriteLine($"Falling back to online tile: {zoom}/{x}/{y}");
				return await _onlineTileSource.GetTileAsync(tileInfo);
			}

			Debug.WriteLine($"Tile not found: {zoom}/{x}/{y}");
			return null;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Error getting tile: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Creates a default tile schema (Web Mercator, zoom 0-19)
	/// </summary>
	private static ITileSchema CreateDefaultSchema()
	{
		return new GlobalSphericalMercator(YAxis.TMS, 0, 19);
	}
}

/// <summary>
/// Factory for creating OfflineTileProvider instances
/// </summary>
public class OfflineTileProviderFactory
{
	private readonly IOfflineMapService _offlineMapService;

	public OfflineTileProviderFactory(IOfflineMapService offlineMapService)
	{
		_offlineMapService = offlineMapService;
	}

	/// <summary>
	/// Creates an offline tile provider for a specific map
	/// </summary>
	/// <param name="mapId">Map ID</param>
	/// <param name="onlineTileSource">Optional online fallback source</param>
	/// <param name="offlineOnly">If true, only use offline tiles</param>
	/// <returns>OfflineTileProvider instance</returns>
	public OfflineTileProvider CreateProvider(
		string mapId,
		ITileSource? onlineTileSource = null,
		bool offlineOnly = false)
	{
		return new OfflineTileProvider(_offlineMapService, mapId, onlineTileSource, offlineOnly);
	}

	/// <summary>
	/// Creates an offline tile layer for Mapsui
	/// </summary>
	/// <param name="mapId">Map ID</param>
	/// <param name="layerName">Layer name</param>
	/// <param name="onlineTileSource">Optional online fallback source</param>
	/// <param name="offlineOnly">If true, only use offline tiles</param>
	/// <returns>TileLayer configured with offline provider</returns>
	public TileLayer CreateLayer(
		string mapId,
		string layerName,
		ITileSource? onlineTileSource = null,
		bool offlineOnly = false)
	{
		var provider = CreateProvider(mapId, onlineTileSource, offlineOnly);

		return new TileLayer(provider)
		{
			Name = layerName
		};
	}
}
