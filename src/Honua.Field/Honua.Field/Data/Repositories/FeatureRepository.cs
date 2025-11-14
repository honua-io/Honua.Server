// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using NetTopologySuite.Geometries;
using SQLite;
using System.Text.Json;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository implementation for Feature operations
/// Provides CRUD and spatial query methods with NetTopologySuite integration
/// </summary>
public class FeatureRepository : IFeatureRepository
{
	private readonly HonuaFieldDatabase _database;
	private readonly IChangeRepository _changeRepository;

	public FeatureRepository(HonuaFieldDatabase database, IChangeRepository changeRepository)
	{
		_database = database;
		_changeRepository = changeRepository;
	}

	#region CRUD Operations

	public async Task<Feature?> GetByIdAsync(string id)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Feature>()
			.Where(f => f.Id == id)
			.FirstOrDefaultAsync();
	}

	public async Task<List<Feature>> GetAllAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Feature>().ToListAsync();
	}

	public async Task<List<Feature>> GetByCollectionIdAsync(string collectionId)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Feature>()
			.Where(f => f.CollectionId == collectionId)
			.ToListAsync();
	}

	public async Task<string> InsertAsync(Feature feature)
	{
		if (string.IsNullOrEmpty(feature.Id))
			feature.Id = Guid.NewGuid().ToString();

		feature.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		feature.UpdatedAt = feature.CreatedAt;
		feature.SyncStatus = SyncStatus.Pending.ToString();

		var conn = _database.GetConnection();
		await conn.InsertAsync(feature);

		// Track change for sync
		await _changeRepository.InsertAsync(new Change
		{
			FeatureId = feature.Id,
			Operation = ChangeOperation.Insert.ToString(),
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		});

		_logger.LogInformation("Feature inserted: {FeatureId}", feature.Id);
		return feature.Id;
	}

	public async Task<int> UpdateAsync(Feature feature)
	{
		feature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		feature.Version++;
		feature.SyncStatus = SyncStatus.Pending.ToString();

		var conn = _database.GetConnection();
		var result = await conn.UpdateAsync(feature);

		// Track change for sync
		await _changeRepository.InsertAsync(new Change
		{
			FeatureId = feature.Id,
			Operation = ChangeOperation.Update.ToString(),
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		});

		_logger.LogInformation("Feature updated: {FeatureId}", feature.Id);
		return result;
	}

	public async Task<int> DeleteAsync(string id)
	{
		// Track change for sync before deleting
		await _changeRepository.InsertAsync(new Change
		{
			FeatureId = id,
			Operation = ChangeOperation.Delete.ToString(),
			Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
		});

		var conn = _database.GetConnection();
		var result = await conn.Table<Feature>()
			.Where(f => f.Id == id)
			.DeleteAsync();

		_logger.LogInformation("Feature deleted: {FeatureId}", id);
		return result;
	}

	#endregion

	#region Spatial Queries

	public async Task<List<Feature>> GetByBoundsAsync(double minX, double minY, double maxX, double maxY)
	{
		// Get all features and filter by bounds in memory
		// Note: For better performance with large datasets, implement R-tree spatial indexing
		var allFeatures = await GetAllAsync();

		var featuresInBounds = new List<Feature>();

		foreach (var feature in allFeatures)
		{
			var bounds = feature.GetBounds();
			if (bounds == null)
				continue;

			// Check if feature bounds intersect with query bounds
			if (bounds.Value.maxX >= minX && bounds.Value.minX <= maxX &&
			    bounds.Value.maxY >= minY && bounds.Value.minY <= maxY)
			{
				featuresInBounds.Add(feature);
			}
		}

		_logger.LogInformation("Found {Count} features in bounds ({MinX},{MinY}) to ({MaxX},{MaxY})",
			featuresInBounds.Count, minX, minY, maxX, maxY);

		return featuresInBounds;
	}

	public async Task<List<Feature>> GetWithinDistanceAsync(Point point, double distanceMeters)
	{
		// Get all features and filter by distance in memory
		var allFeatures = await GetAllAsync();
		var featuresWithinDistance = new List<Feature>();

		foreach (var feature in allFeatures)
		{
			var geometry = feature.GetGeometry();
			if (geometry == null)
				continue;

			// Calculate distance (this is a simple Euclidean distance approximation)
			// For accurate geodesic distance, use NetTopologySuite.Operation.Distance
			var distance = point.Distance(geometry);

			// Convert degrees to meters (rough approximation: 1 degree ≈ 111,000 meters at equator)
			var distanceInMeters = distance * 111000;

			if (distanceInMeters <= distanceMeters)
			{
				featuresWithinDistance.Add(feature);
			}
		}

		_logger.LogInformation("Found {Count} features within {DistanceMeters}m of point",
			featuresWithinDistance.Count, distanceMeters);

		return featuresWithinDistance;
	}

	public async Task<Feature?> GetNearestAsync(Point point)
	{
		var allFeatures = await GetAllAsync();

		Feature? nearestFeature = null;
		double minDistance = double.MaxValue;

		foreach (var feature in allFeatures)
		{
			var geometry = feature.GetGeometry();
			if (geometry == null)
				continue;

			var distance = point.Distance(geometry);

			if (distance < minDistance)
			{
				minDistance = distance;
				nearestFeature = feature;
			}
		}

		if (nearestFeature != null)
		{
			_logger.LogInformation("Nearest feature: {FeatureId}, distance: {DistanceMeters}m",
				nearestFeature.Id, minDistance * 111000);
		}

		return nearestFeature;
	}

	public async Task<List<Feature>> GetIntersectingAsync(Geometry geometry)
	{
		var allFeatures = await GetAllAsync();
		var intersectingFeatures = new List<Feature>();

		foreach (var feature in allFeatures)
		{
			var featureGeometry = feature.GetGeometry();
			if (featureGeometry == null)
				continue;

			if (geometry.Intersects(featureGeometry))
			{
				intersectingFeatures.Add(feature);
			}
		}

		_logger.LogInformation("Found {Count} features intersecting with geometry",
			intersectingFeatures.Count);

		return intersectingFeatures;
	}

	#endregion

	#region Sync Operations

	public async Task<List<Feature>> GetPendingSyncAsync()
	{
		return await GetBySyncStatusAsync(SyncStatus.Pending);
	}

	public async Task<List<Feature>> GetBySyncStatusAsync(SyncStatus status)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Feature>()
			.Where(f => f.SyncStatus == status.ToString())
			.ToListAsync();
	}

	public async Task<int> UpdateSyncStatusAsync(string id, SyncStatus status)
	{
		var feature = await GetByIdAsync(id);
		if (feature == null)
			return 0;

		feature.SyncStatus = status.ToString();
		feature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		var conn = _database.GetConnection();
		return await conn.UpdateAsync(feature);
	}

	public async Task<int> UpdateServerIdAsync(string localId, string serverId)
	{
		var feature = await GetByIdAsync(localId);
		if (feature == null)
			return 0;

		feature.ServerId = serverId;
		feature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		var conn = _database.GetConnection();
		return await conn.UpdateAsync(feature);
	}

	#endregion

	#region Batch Operations

	public async Task<int> InsertBatchAsync(List<Feature> features)
	{
		var conn = _database.GetConnection();
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// Prepare features for insert
		foreach (var feature in features)
		{
			if (string.IsNullOrEmpty(feature.Id))
				feature.Id = Guid.NewGuid().ToString();

			feature.CreatedAt = timestamp;
			feature.UpdatedAt = timestamp;

			if (string.IsNullOrEmpty(feature.SyncStatus))
				feature.SyncStatus = SyncStatus.Pending.ToString();
		}

		var result = await conn.InsertAllAsync(features);

		// Track changes for sync
		var changes = features.Select(f => new Change
		{
			FeatureId = f.Id,
			Operation = ChangeOperation.Insert.ToString(),
			Timestamp = timestamp
		}).ToList();

		await _changeRepository.InsertBatchAsync(changes);

		_logger.LogInformation("Batch inserted {Count} features", result);
		return result;
	}

	public async Task<int> UpdateBatchAsync(List<Feature> features)
	{
		var conn = _database.GetConnection();
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// Prepare features for update
		foreach (var feature in features)
		{
			feature.UpdatedAt = timestamp;
			feature.Version++;
			feature.SyncStatus = SyncStatus.Pending.ToString();
		}

		var result = await conn.UpdateAllAsync(features);

		// Track changes for sync
		var changes = features.Select(f => new Change
		{
			FeatureId = f.Id,
			Operation = ChangeOperation.Update.ToString(),
			Timestamp = timestamp
		}).ToList();

		await _changeRepository.InsertBatchAsync(changes);

		_logger.LogInformation("Batch updated {Count} features", result);
		return result;
	}

	public async Task<int> DeleteBatchAsync(List<string> ids)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// Track changes for sync before deleting
		var changes = ids.Select(id => new Change
		{
			FeatureId = id,
			Operation = ChangeOperation.Delete.ToString(),
			Timestamp = timestamp
		}).ToList();

		await _changeRepository.InsertBatchAsync(changes);

		var conn = _database.GetConnection();
		var result = 0;

		foreach (var id in ids)
		{
			result += await conn.Table<Feature>()
				.Where(f => f.Id == id)
				.DeleteAsync();
		}

		_logger.LogInformation("Batch deleted {Count} features", result);
		return result;
	}

	#endregion

	#region Statistics

	public async Task<int> GetCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Feature>().CountAsync();
	}

	public async Task<int> GetCountByCollectionAsync(string collectionId)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Feature>()
			.Where(f => f.CollectionId == collectionId)
			.CountAsync();
	}

	public async Task<(double minX, double minY, double maxX, double maxY)?> GetExtentAsync(
		string? collectionId = null)
	{
		List<Feature> features;

		if (string.IsNullOrEmpty(collectionId))
		{
			features = await GetAllAsync();
		}
		else
		{
			features = await GetByCollectionIdAsync(collectionId);
		}

		if (features.Count == 0)
			return null;

		double minX = double.MaxValue;
		double minY = double.MaxValue;
		double maxX = double.MinValue;
		double maxY = double.MinValue;

		foreach (var feature in features)
		{
			var bounds = feature.GetBounds();
			if (bounds == null)
				continue;

			minX = Math.Min(minX, bounds.Value.minX);
			minY = Math.Min(minY, bounds.Value.minY);
			maxX = Math.Max(maxX, bounds.Value.maxX);
			maxY = Math.Max(maxY, bounds.Value.maxY);
		}

		if (minX == double.MaxValue)
			return null;

		return (minX, minY, maxX, maxY);
	}

	#endregion
}
