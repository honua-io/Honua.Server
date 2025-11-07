using HonuaField.Models;

namespace HonuaField.Services;

/// <summary>
/// Service for managing feature collections/layers
/// Provides CRUD operations, schema validation, and statistics
/// </summary>
public interface ICollectionsService
{
	/// <summary>
	/// Get a collection by ID
	/// </summary>
	/// <param name="id">Collection ID</param>
	/// <returns>Collection if found, null otherwise</returns>
	Task<Collection?> GetByIdAsync(string id);

	/// <summary>
	/// Get all collections
	/// </summary>
	/// <returns>List of all collections</returns>
	Task<List<Collection>> GetAllAsync();

	/// <summary>
	/// Create a new collection
	/// </summary>
	/// <param name="collection">Collection to create</param>
	/// <returns>ID of the created collection</returns>
	/// <exception cref="ArgumentNullException">Thrown when collection is null</exception>
	/// <exception cref="ArgumentException">Thrown when collection title is empty or schema/symbology is invalid</exception>
	Task<string> CreateAsync(Collection collection);

	/// <summary>
	/// Update an existing collection
	/// </summary>
	/// <param name="collection">Collection to update</param>
	/// <returns>Number of rows affected (1 if successful, 0 if not found)</returns>
	/// <exception cref="ArgumentNullException">Thrown when collection is null</exception>
	/// <exception cref="ArgumentException">Thrown when collection ID is empty or schema/symbology is invalid</exception>
	Task<int> UpdateAsync(Collection collection);

	/// <summary>
	/// Delete a collection by ID
	/// </summary>
	/// <param name="id">Collection ID</param>
	/// <returns>Number of rows affected (1 if successful, 0 if not found)</returns>
	/// <exception cref="ArgumentException">Thrown when ID is null or empty</exception>
	Task<int> DeleteAsync(string id);

	/// <summary>
	/// Get the count of features in a collection
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Number of features in the collection</returns>
	Task<int> GetFeatureCountAsync(string collectionId);

	/// <summary>
	/// Update the feature count for a collection
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Number of rows affected</returns>
	Task<int> RefreshFeatureCountAsync(string collectionId);

	/// <summary>
	/// Get statistics for a collection (feature count, extent, etc.)
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Collection statistics</returns>
	Task<CollectionStats?> GetStatsAsync(string collectionId);

	/// <summary>
	/// Validate a JSON schema
	/// </summary>
	/// <param name="schemaJson">JSON schema string</param>
	/// <returns>True if valid, false otherwise</returns>
	bool ValidateSchema(string schemaJson);

	/// <summary>
	/// Validate symbology JSON
	/// </summary>
	/// <param name="symbologyJson">Symbology JSON string</param>
	/// <returns>True if valid, false otherwise</returns>
	bool ValidateSymbology(string symbologyJson);

	/// <summary>
	/// Update the spatial extent of a collection based on its features
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Number of rows affected</returns>
	Task<int> RefreshExtentAsync(string collectionId);
}

/// <summary>
/// Statistics for a collection
/// </summary>
public record CollectionStats
{
	public required string CollectionId { get; init; }
	public required string Title { get; init; }
	public required int FeatureCount { get; init; }
	public CollectionExtent? Extent { get; init; }
}

/// <summary>
/// Spatial extent of a collection
/// </summary>
public record CollectionExtent
{
	public required double MinX { get; init; }
	public required double MinY { get; init; }
	public required double MaxX { get; init; }
	public required double MaxY { get; init; }
}
