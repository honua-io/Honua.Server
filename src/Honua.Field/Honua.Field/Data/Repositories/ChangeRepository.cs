// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using SQLite;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository implementation for Change operations
/// Manages sync queue for offline edit tracking
/// </summary>
public class ChangeRepository : IChangeRepository
{
	private readonly HonuaFieldDatabase _database;

	public ChangeRepository(HonuaFieldDatabase database)
	{
		_database = database;
	}

	#region CRUD Operations

	public async Task<Change?> GetByIdAsync(int id)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.Id == id)
			.FirstOrDefaultAsync();
	}

	public async Task<List<Change>> GetAllAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.OrderBy(c => c.Timestamp)
			.ToListAsync();
	}

	public async Task<List<Change>> GetByFeatureIdAsync(string featureId)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.FeatureId == featureId)
			.OrderBy(c => c.Timestamp)
			.ToListAsync();
	}

	public async Task<int> InsertAsync(Change change)
	{
		if (change.Timestamp == 0)
			change.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		var conn = _database.GetConnection();
		var result = await conn.InsertAsync(change);

		System.Diagnostics.Debug.WriteLine(
			$"Change tracked: {change.Operation} for feature {change.FeatureId}");

		return result;
	}

	public async Task<int> DeleteAsync(int id)
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Change>()
			.Where(c => c.Id == id)
			.DeleteAsync();

		System.Diagnostics.Debug.WriteLine($"Change deleted: {id}");
		return result;
	}

	public async Task<int> DeleteByFeatureIdAsync(string featureId)
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Change>()
			.Where(c => c.FeatureId == featureId)
			.DeleteAsync();

		System.Diagnostics.Debug.WriteLine($"Deleted {result} changes for feature: {featureId}");
		return result;
	}

	#endregion

	#region Sync Queue Operations

	public async Task<List<Change>> GetPendingAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.Synced == 0)
			.OrderBy(c => c.Timestamp)
			.ToListAsync();
	}

	public async Task<List<Change>> GetSyncedAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.Synced == 1)
			.OrderBy(c => c.Timestamp)
			.ToListAsync();
	}

	public async Task<int> MarkAsSyncedAsync(int id)
	{
		var change = await GetByIdAsync(id);
		if (change == null)
			return 0;

		change.Synced = 1;

		var conn = _database.GetConnection();
		var result = await conn.UpdateAsync(change);

		System.Diagnostics.Debug.WriteLine($"Change marked as synced: {id}");
		return result;
	}

	public async Task<int> MarkBatchAsSyncedAsync(List<int> ids)
	{
		var conn = _database.GetConnection();
		var result = 0;

		foreach (var id in ids)
		{
			var change = await GetByIdAsync(id);
			if (change != null)
			{
				change.Synced = 1;
				result += await conn.UpdateAsync(change);
			}
		}

		System.Diagnostics.Debug.WriteLine($"Marked {result} changes as synced");
		return result;
	}

	#endregion

	#region Operation Filtering

	public async Task<List<Change>> GetByOperationAsync(ChangeOperation operation)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.Operation == operation.ToString())
			.OrderBy(c => c.Timestamp)
			.ToListAsync();
	}

	public async Task<List<Change>> GetPendingByOperationAsync(ChangeOperation operation)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.Operation == operation.ToString() && c.Synced == 0)
			.OrderBy(c => c.Timestamp)
			.ToListAsync();
	}

	#endregion

	#region Batch Operations

	public async Task<int> InsertBatchAsync(List<Change> changes)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// Ensure all changes have timestamps
		foreach (var change in changes)
		{
			if (change.Timestamp == 0)
				change.Timestamp = timestamp;
		}

		var conn = _database.GetConnection();
		var result = await conn.InsertAllAsync(changes);

		System.Diagnostics.Debug.WriteLine($"Batch inserted {result} changes");
		return result;
	}

	public async Task<int> DeleteBatchAsync(List<int> ids)
	{
		var conn = _database.GetConnection();
		var result = 0;

		foreach (var id in ids)
		{
			result += await conn.Table<Change>()
				.Where(c => c.Id == id)
				.DeleteAsync();
		}

		System.Diagnostics.Debug.WriteLine($"Batch deleted {result} changes");
		return result;
	}

	public async Task<int> ClearSyncedAsync()
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Change>()
			.Where(c => c.Synced == 1)
			.DeleteAsync();

		System.Diagnostics.Debug.WriteLine($"Cleared {result} synced changes");
		return result;
	}

	#endregion

	#region Statistics

	public async Task<int> GetCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>().CountAsync();
	}

	public async Task<int> GetPendingCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.Synced == 0)
			.CountAsync();
	}

	public async Task<Change?> GetLatestByFeatureIdAsync(string featureId)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Change>()
			.Where(c => c.FeatureId == featureId)
			.OrderByDescending(c => c.Timestamp)
			.FirstOrDefaultAsync();
	}

	#endregion
}
