using HonuaField.Models;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository interface for Map operations
/// Provides CRUD and query methods for offline map configurations
/// </summary>
public interface IMapRepository
{
	// CRUD Operations
	/// <summary>
	/// Gets a map by its ID
	/// </summary>
	Task<Map?> GetByIdAsync(string id);

	/// <summary>
	/// Gets all maps
	/// </summary>
	Task<List<Map>> GetAllAsync();

	/// <summary>
	/// Gets all downloaded maps (maps with offline tiles)
	/// </summary>
	Task<List<Map>> GetDownloadedMapsAsync();

	/// <summary>
	/// Gets maps that intersect with the specified bounds
	/// </summary>
	Task<List<Map>> GetByBoundsAsync(double minX, double minY, double maxX, double maxY);

	/// <summary>
	/// Inserts a new map
	/// </summary>
	Task<string> InsertAsync(Map map);

	/// <summary>
	/// Updates an existing map
	/// </summary>
	Task<int> UpdateAsync(Map map);

	/// <summary>
	/// Deletes a map by ID
	/// </summary>
	Task<int> DeleteAsync(string id);

	// Download Management
	/// <summary>
	/// Updates the download extent and size for a map
	/// </summary>
	Task<int> UpdateDownloadInfoAsync(string id, string downloadedExtent, long downloadSize);

	/// <summary>
	/// Clears download information for a map
	/// </summary>
	Task<int> ClearDownloadInfoAsync(string id);

	// Statistics
	/// <summary>
	/// Gets the total count of maps
	/// </summary>
	Task<int> GetCountAsync();

	/// <summary>
	/// Gets the total count of downloaded maps
	/// </summary>
	Task<int> GetDownloadedCountAsync();

	/// <summary>
	/// Gets the total storage size used by all offline maps
	/// </summary>
	Task<long> GetTotalDownloadSizeAsync();
}
