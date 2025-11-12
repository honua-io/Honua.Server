// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using NetTopologySuite.Geometries;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository interface for Feature operations
/// Provides CRUD and spatial query methods
/// </summary>
public interface IFeatureRepository
{
	// CRUD Operations
	Task<Feature?> GetByIdAsync(string id);
	Task<List<Feature>> GetAllAsync();
	Task<List<Feature>> GetByCollectionIdAsync(string collectionId);
	Task<string> InsertAsync(Feature feature);
	Task<int> UpdateAsync(Feature feature);
	Task<int> DeleteAsync(string id);

	// Spatial Queries
	Task<List<Feature>> GetByBoundsAsync(double minX, double minY, double maxX, double maxY);
	Task<List<Feature>> GetWithinDistanceAsync(Point point, double distanceMeters);
	Task<Feature?> GetNearestAsync(Point point);
	Task<List<Feature>> GetIntersectingAsync(Geometry geometry);

	// Sync Operations
	Task<List<Feature>> GetPendingSyncAsync();
	Task<List<Feature>> GetBySync StatusAsync(SyncStatus status);
	Task<int> UpdateSyncStatusAsync(string id, SyncStatus status);
	Task<int> UpdateServerIdAsync(string localId, string serverId);

	// Batch Operations
	Task<int> InsertBatchAsync(List<Feature> features);
	Task<int> UpdateBatchAsync(List<Feature> features);
	Task<int> DeleteBatchAsync(List<string> ids);

	// Statistics
	Task<int> GetCountAsync();
	Task<int> GetCountByCollectionAsync(string collectionId);
	Task<(double minX, double minY, double maxX, double maxY)?> GetExtentAsync(string? collectionId = null);
}
