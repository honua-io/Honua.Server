// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository interface for Change operations
/// Manages sync queue for offline edit tracking
/// </summary>
public interface IChangeRepository
{
	// CRUD Operations
	Task<Change?> GetByIdAsync(int id);
	Task<List<Change>> GetAllAsync();
	Task<List<Change>> GetByFeatureIdAsync(string featureId);
	Task<int> InsertAsync(Change change);
	Task<int> DeleteAsync(int id);
	Task<int> DeleteByFeatureIdAsync(string featureId);

	// Sync queue operations
	Task<List<Change>> GetPendingAsync();
	Task<List<Change>> GetSyncedAsync();
	Task<int> MarkAsSyncedAsync(int id);
	Task<int> MarkBatchAsSyncedAsync(List<int> ids);

	// Operation filtering
	Task<List<Change>> GetByOperationAsync(ChangeOperation operation);
	Task<List<Change>> GetPendingByOperationAsync(ChangeOperation operation);

	// Batch Operations
	Task<int> InsertBatchAsync(List<Change> changes);
	Task<int> DeleteBatchAsync(List<int> ids);
	Task<int> ClearSyncedAsync();

	// Statistics
	Task<int> GetCountAsync();
	Task<int> GetPendingCountAsync();
	Task<Change?> GetLatestByFeatureIdAsync(string featureId);
}
