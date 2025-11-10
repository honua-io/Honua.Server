using HonuaField.Models;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository interface for Collection operations
/// Provides CRUD methods for feature collections/layers
/// </summary>
public interface ICollectionRepository
{
	// CRUD Operations
	Task<Collection?> GetByIdAsync(string id);
	Task<List<Collection>> GetAllAsync();
	Task<string> InsertAsync(Collection collection);
	Task<int> UpdateAsync(Collection collection);
	Task<int> DeleteAsync(string id);

	// Collection-specific operations
	Task<int> UpdateItemsCountAsync(string id, int count);
	Task<int> IncrementItemsCountAsync(string id, int increment = 1);
	Task<int> UpdateExtentAsync(string id, string extentJson);

	// Batch Operations
	Task<int> InsertBatchAsync(List<Collection> collections);
	Task<int> UpdateBatchAsync(List<Collection> collections);

	// Statistics
	Task<int> GetCountAsync();
}
