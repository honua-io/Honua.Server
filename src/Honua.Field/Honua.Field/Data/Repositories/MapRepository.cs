// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using SQLite;
using System.Text.Json;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository implementation for Map operations
/// Provides CRUD and query methods for offline map configurations
/// </summary>
public class MapRepository : IMapRepository
{
	private readonly HonuaFieldDatabase _database;

	public MapRepository(HonuaFieldDatabase database)
	{
		_database = database;
	}

	#region CRUD Operations

	/// <inheritdoc />
	public async Task<Map?> GetByIdAsync(string id)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Map>()
			.Where(m => m.Id == id)
			.FirstOrDefaultAsync();
	}

	/// <inheritdoc />
	public async Task<List<Map>> GetAllAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Map>().ToListAsync();
	}

	/// <inheritdoc />
	public async Task<List<Map>> GetDownloadedMapsAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Map>()
			.Where(m => m.DownloadSize > 0)
			.ToListAsync();
	}

	/// <inheritdoc />
	public async Task<List<Map>> GetByBoundsAsync(double minX, double minY, double maxX, double maxY)
	{
		// Get all maps and filter by bounds in memory
		var allMaps = await GetAllAsync();
		var mapsInBounds = new List<Map>();

		foreach (var map in allMaps)
		{
			try
			{
				if (string.IsNullOrEmpty(map.Extent))
					continue;

				var extent = JsonSerializer.Deserialize<Dictionary<string, double>>(map.Extent);
				if (extent == null)
					continue;

				// Check if map extent intersects with query bounds
				var mapMinX = extent.GetValueOrDefault("min_x", double.MinValue);
				var mapMinY = extent.GetValueOrDefault("min_y", double.MinValue);
				var mapMaxX = extent.GetValueOrDefault("max_x", double.MaxValue);
				var mapMaxY = extent.GetValueOrDefault("max_y", double.MaxValue);

				if (mapMaxX >= minX && mapMinX <= maxX &&
				    mapMaxY >= minY && mapMinY <= maxY)
				{
					mapsInBounds.Add(map);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error parsing map extent for {map.Id}: {ex.Message}");
			}
		}

		System.Diagnostics.Debug.WriteLine(
			$"Found {mapsInBounds.Count} maps in bounds ({minX},{minY}) to ({maxX},{maxY})");

		return mapsInBounds;
	}

	/// <inheritdoc />
	public async Task<string> InsertAsync(Map map)
	{
		if (string.IsNullOrEmpty(map.Id))
			map.Id = Guid.NewGuid().ToString();

		var conn = _database.GetConnection();
		await conn.InsertAsync(map);

		System.Diagnostics.Debug.WriteLine($"Map inserted: {map.Id}");
		return map.Id;
	}

	/// <inheritdoc />
	public async Task<int> UpdateAsync(Map map)
	{
		var conn = _database.GetConnection();
		var result = await conn.UpdateAsync(map);

		System.Diagnostics.Debug.WriteLine($"Map updated: {map.Id}");
		return result;
	}

	/// <inheritdoc />
	public async Task<int> DeleteAsync(string id)
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Map>()
			.Where(m => m.Id == id)
			.DeleteAsync();

		System.Diagnostics.Debug.WriteLine($"Map deleted: {id}");
		return result;
	}

	#endregion

	#region Download Management

	/// <inheritdoc />
	public async Task<int> UpdateDownloadInfoAsync(string id, string downloadedExtent, long downloadSize)
	{
		var map = await GetByIdAsync(id);
		if (map == null)
			return 0;

		map.DownloadedExtent = downloadedExtent;
		map.DownloadSize = downloadSize;

		return await UpdateAsync(map);
	}

	/// <inheritdoc />
	public async Task<int> ClearDownloadInfoAsync(string id)
	{
		var map = await GetByIdAsync(id);
		if (map == null)
			return 0;

		map.DownloadedExtent = null;
		map.DownloadSize = 0;

		return await UpdateAsync(map);
	}

	#endregion

	#region Statistics

	/// <inheritdoc />
	public async Task<int> GetCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Map>().CountAsync();
	}

	/// <inheritdoc />
	public async Task<int> GetDownloadedCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Map>()
			.Where(m => m.DownloadSize > 0)
			.CountAsync();
	}

	/// <inheritdoc />
	public async Task<long> GetTotalDownloadSizeAsync()
	{
		var conn = _database.GetConnection();
		var maps = await conn.Table<Map>()
			.Where(m => m.DownloadSize > 0)
			.ToListAsync();

		return maps.Sum(m => m.DownloadSize);
	}

	#endregion
}
