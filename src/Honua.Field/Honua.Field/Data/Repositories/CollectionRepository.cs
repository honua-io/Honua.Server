// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using SQLite;
using Microsoft.Extensions.Logging;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository implementation for Collection operations
/// Provides CRUD methods for feature collections/layers
/// </summary>
public class CollectionRepository : ICollectionRepository
{
	private readonly HonuaFieldDatabase _database;
	private readonly ILogger<CollectionRepository> _logger;

	public CollectionRepository(HonuaFieldDatabase database, ILogger<CollectionRepository> logger)
	{
		_database = database;
		_logger = logger;
	}

	#region CRUD Operations

	public async Task<Collection?> GetByIdAsync(string id)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Collection>()
			.Where(c => c.Id == id)
			.FirstOrDefaultAsync();
	}

	public async Task<List<Collection>> GetAllAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Collection>().ToListAsync();
	}

	public async Task<string> InsertAsync(Collection collection)
	{
		if (string.IsNullOrEmpty(collection.Id))
			collection.Id = Guid.NewGuid().ToString();

		var conn = _database.GetConnection();
		await conn.InsertAsync(collection);

		_logger.LogInformation("Collection inserted: {CollectionId}", collection.Id);
		return collection.Id;
	}

	public async Task<int> UpdateAsync(Collection collection)
	{
		var conn = _database.GetConnection();
		var result = await conn.UpdateAsync(collection);

		_logger.LogInformation("Collection updated: {CollectionId}", collection.Id);
		return result;
	}

	public async Task<int> DeleteAsync(string id)
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Collection>()
			.Where(c => c.Id == id)
			.DeleteAsync();

		_logger.LogInformation("Collection deleted: {CollectionId}", id);
		return result;
	}

	#endregion

	#region Collection-specific Operations

	public async Task<int> UpdateItemsCountAsync(string id, int count)
	{
		var collection = await GetByIdAsync(id);
		if (collection == null)
			return 0;

		collection.ItemsCount = count;

		var conn = _database.GetConnection();
		return await conn.UpdateAsync(collection);
	}

	public async Task<int> IncrementItemsCountAsync(string id, int increment = 1)
	{
		var collection = await GetByIdAsync(id);
		if (collection == null)
			return 0;

		collection.ItemsCount += increment;

		var conn = _database.GetConnection();
		return await conn.UpdateAsync(collection);
	}

	public async Task<int> UpdateExtentAsync(string id, string extentJson)
	{
		var collection = await GetByIdAsync(id);
		if (collection == null)
			return 0;

		collection.Extent = extentJson;

		var conn = _database.GetConnection();
		return await conn.UpdateAsync(collection);
	}

	#endregion

	#region Batch Operations

	public async Task<int> InsertBatchAsync(List<Collection> collections)
	{
		// Ensure all collections have IDs
		foreach (var collection in collections)
		{
			if (string.IsNullOrEmpty(collection.Id))
				collection.Id = Guid.NewGuid().ToString();
		}

		var conn = _database.GetConnection();
		var result = await conn.InsertAllAsync(collections);

		_logger.LogInformation("Batch inserted {Count} collections", result);
		return result;
	}

	public async Task<int> UpdateBatchAsync(List<Collection> collections)
	{
		var conn = _database.GetConnection();
		var result = await conn.UpdateAllAsync(collections);

		_logger.LogInformation("Batch updated {Count} collections", result);
		return result;
	}

	#endregion

	#region Statistics

	public async Task<int> GetCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Collection>().CountAsync();
	}

	#endregion
}
