// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data.Repositories;
using HonuaField.Models;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Implementation of ICollectionsService for managing feature collections/layers
/// Provides CRUD operations, schema validation, and statistics with offline-first design
/// </summary>
public class CollectionsService : ICollectionsService
{
	private readonly ICollectionRepository _collectionRepository;
	private readonly IFeatureRepository _featureRepository;

	public CollectionsService(
		ICollectionRepository collectionRepository,
		IFeatureRepository featureRepository)
	{
		_collectionRepository = collectionRepository;
		_featureRepository = featureRepository;
	}

	#region CRUD Operations

	/// <summary>
	/// Get a collection by ID
	/// </summary>
	/// <param name="id">Collection ID</param>
	/// <returns>Collection if found, null otherwise</returns>
	public async Task<Collection?> GetByIdAsync(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			System.Diagnostics.Debug.WriteLine("GetByIdAsync: ID is null or empty");
			return null;
		}

		try
		{
			return await _collectionRepository.GetByIdAsync(id);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting collection by ID {id}: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Get all collections
	/// </summary>
	/// <returns>List of all collections</returns>
	public async Task<List<Collection>> GetAllAsync()
	{
		try
		{
			return await _collectionRepository.GetAllAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting all collections: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Create a new collection
	/// </summary>
	/// <param name="collection">Collection to create</param>
	/// <returns>ID of the created collection</returns>
	/// <exception cref="ArgumentNullException">Thrown when collection is null</exception>
	/// <exception cref="ArgumentException">Thrown when collection title is empty or schema/symbology is invalid</exception>
	public async Task<string> CreateAsync(Collection collection)
	{
		if (collection == null)
		{
			throw new ArgumentNullException(nameof(collection));
		}

		if (string.IsNullOrWhiteSpace(collection.Title))
		{
			throw new ArgumentException("Collection title cannot be empty", nameof(collection));
		}

		// Validate schema
		if (!ValidateSchema(collection.Schema))
		{
			throw new ArgumentException("Invalid JSON schema", nameof(collection));
		}

		// Validate symbology
		if (!ValidateSymbology(collection.Symbology))
		{
			throw new ArgumentException("Invalid symbology JSON", nameof(collection));
		}

		try
		{
			// Ensure schema and symbology are at least empty JSON objects
			if (string.IsNullOrWhiteSpace(collection.Schema))
			{
				collection.Schema = "{}";
			}

			if (string.IsNullOrWhiteSpace(collection.Symbology))
			{
				collection.Symbology = "{}";
			}

			var id = await _collectionRepository.InsertAsync(collection);
			System.Diagnostics.Debug.WriteLine($"Collection created: {id}");
			return id;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error creating collection: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Update an existing collection
	/// </summary>
	/// <param name="collection">Collection to update</param>
	/// <returns>Number of rows affected (1 if successful, 0 if not found)</returns>
	/// <exception cref="ArgumentNullException">Thrown when collection is null</exception>
	/// <exception cref="ArgumentException">Thrown when collection ID is empty or schema/symbology is invalid</exception>
	public async Task<int> UpdateAsync(Collection collection)
	{
		if (collection == null)
		{
			throw new ArgumentNullException(nameof(collection));
		}

		if (string.IsNullOrWhiteSpace(collection.Id))
		{
			throw new ArgumentException("Collection ID cannot be empty", nameof(collection));
		}

		if (string.IsNullOrWhiteSpace(collection.Title))
		{
			throw new ArgumentException("Collection title cannot be empty", nameof(collection));
		}

		// Validate schema
		if (!ValidateSchema(collection.Schema))
		{
			throw new ArgumentException("Invalid JSON schema", nameof(collection));
		}

		// Validate symbology
		if (!ValidateSymbology(collection.Symbology))
		{
			throw new ArgumentException("Invalid symbology JSON", nameof(collection));
		}

		try
		{
			var result = await _collectionRepository.UpdateAsync(collection);
			System.Diagnostics.Debug.WriteLine($"Collection updated: {collection.Id}, rows affected: {result}");
			return result;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error updating collection {collection.Id}: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Delete a collection by ID
	/// </summary>
	/// <param name="id">Collection ID</param>
	/// <returns>Number of rows affected (1 if successful, 0 if not found)</returns>
	/// <exception cref="ArgumentException">Thrown when ID is null or empty</exception>
	public async Task<int> DeleteAsync(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("Collection ID cannot be null or empty", nameof(id));
		}

		try
		{
			var result = await _collectionRepository.DeleteAsync(id);
			System.Diagnostics.Debug.WriteLine($"Collection deleted: {id}, rows affected: {result}");
			return result;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error deleting collection {id}: {ex.Message}");
			throw;
		}
	}

	#endregion

	#region Statistics and Feature Count

	/// <summary>
	/// Get the count of features in a collection
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Number of features in the collection</returns>
	public async Task<int> GetFeatureCountAsync(string collectionId)
	{
		if (string.IsNullOrWhiteSpace(collectionId))
		{
			System.Diagnostics.Debug.WriteLine("GetFeatureCountAsync: Collection ID is null or empty");
			return 0;
		}

		try
		{
			return await _featureRepository.GetCountByCollectionAsync(collectionId);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting feature count for collection {collectionId}: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Update the feature count for a collection
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Number of rows affected</returns>
	public async Task<int> RefreshFeatureCountAsync(string collectionId)
	{
		if (string.IsNullOrWhiteSpace(collectionId))
		{
			System.Diagnostics.Debug.WriteLine("RefreshFeatureCountAsync: Collection ID is null or empty");
			return 0;
		}

		try
		{
			var count = await _featureRepository.GetCountByCollectionAsync(collectionId);
			var result = await _collectionRepository.UpdateItemsCountAsync(collectionId, count);
			System.Diagnostics.Debug.WriteLine($"Collection {collectionId} feature count refreshed: {count}");
			return result;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error refreshing feature count for collection {collectionId}: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Get statistics for a collection (feature count, extent, etc.)
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Collection statistics</returns>
	public async Task<CollectionStats?> GetStatsAsync(string collectionId)
	{
		if (string.IsNullOrWhiteSpace(collectionId))
		{
			System.Diagnostics.Debug.WriteLine("GetStatsAsync: Collection ID is null or empty");
			return null;
		}

		try
		{
			var collection = await _collectionRepository.GetByIdAsync(collectionId);
			if (collection == null)
			{
				System.Diagnostics.Debug.WriteLine($"Collection not found: {collectionId}");
				return null;
			}

			var featureCount = await _featureRepository.GetCountByCollectionAsync(collectionId);
			var extent = await _featureRepository.GetExtentAsync(collectionId);

			CollectionExtent? collectionExtent = null;
			if (extent.HasValue)
			{
				collectionExtent = new CollectionExtent
				{
					MinX = extent.Value.minX,
					MinY = extent.Value.minY,
					MaxX = extent.Value.maxX,
					MaxY = extent.Value.maxY
				};
			}

			return new CollectionStats
			{
				CollectionId = collection.Id,
				Title = collection.Title,
				FeatureCount = featureCount,
				Extent = collectionExtent
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error getting stats for collection {collectionId}: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Update the spatial extent of a collection based on its features
	/// </summary>
	/// <param name="collectionId">Collection ID</param>
	/// <returns>Number of rows affected</returns>
	public async Task<int> RefreshExtentAsync(string collectionId)
	{
		if (string.IsNullOrWhiteSpace(collectionId))
		{
			System.Diagnostics.Debug.WriteLine("RefreshExtentAsync: Collection ID is null or empty");
			return 0;
		}

		try
		{
			var extent = await _featureRepository.GetExtentAsync(collectionId);
			if (!extent.HasValue)
			{
				System.Diagnostics.Debug.WriteLine($"No features found for collection {collectionId}, extent not updated");
				return 0;
			}

			var extentJson = JsonSerializer.Serialize(new
			{
				min_x = extent.Value.minX,
				min_y = extent.Value.minY,
				max_x = extent.Value.maxX,
				max_y = extent.Value.maxY
			});

			var result = await _collectionRepository.UpdateExtentAsync(collectionId, extentJson);
			System.Diagnostics.Debug.WriteLine($"Collection {collectionId} extent refreshed");
			return result;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error refreshing extent for collection {collectionId}: {ex.Message}");
			throw;
		}
	}

	#endregion

	#region Validation

	/// <summary>
	/// Validate a JSON schema
	/// </summary>
	/// <param name="schemaJson">JSON schema string</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool ValidateSchema(string schemaJson)
	{
		// Allow empty or null schema (will default to {})
		if (string.IsNullOrWhiteSpace(schemaJson))
		{
			return true;
		}

		try
		{
			// Validate that it's valid JSON
			using var document = JsonDocument.Parse(schemaJson);

			// Basic validation: schema should be a JSON object
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				System.Diagnostics.Debug.WriteLine("Schema validation failed: root must be an object");
				return false;
			}

			// If schema has a "properties" field, it should be an object
			if (document.RootElement.TryGetProperty("properties", out var properties))
			{
				if (properties.ValueKind != JsonValueKind.Object)
				{
					System.Diagnostics.Debug.WriteLine("Schema validation failed: properties must be an object");
					return false;
				}
			}

			// If schema has a "type" field, it should be a string
			if (document.RootElement.TryGetProperty("type", out var type))
			{
				if (type.ValueKind != JsonValueKind.String)
				{
					System.Diagnostics.Debug.WriteLine("Schema validation failed: type must be a string");
					return false;
				}
			}

			return true;
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"Schema validation failed: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Validate symbology JSON
	/// </summary>
	/// <param name="symbologyJson">Symbology JSON string</param>
	/// <returns>True if valid, false otherwise</returns>
	public bool ValidateSymbology(string symbologyJson)
	{
		// Allow empty or null symbology (will default to {})
		if (string.IsNullOrWhiteSpace(symbologyJson))
		{
			return true;
		}

		try
		{
			// Validate that it's valid JSON
			using var document = JsonDocument.Parse(symbologyJson);

			// Basic validation: symbology should be a JSON object
			if (document.RootElement.ValueKind != JsonValueKind.Object)
			{
				System.Diagnostics.Debug.WriteLine("Symbology validation failed: root must be an object");
				return false;
			}

			// If symbology has a "color" field, validate it
			if (document.RootElement.TryGetProperty("color", out var color))
			{
				if (color.ValueKind != JsonValueKind.String && color.ValueKind != JsonValueKind.Object)
				{
					System.Diagnostics.Debug.WriteLine("Symbology validation failed: color must be a string or object");
					return false;
				}
			}

			// If symbology has an "icon" field, it should be a string
			if (document.RootElement.TryGetProperty("icon", out var icon))
			{
				if (icon.ValueKind != JsonValueKind.String)
				{
					System.Diagnostics.Debug.WriteLine("Symbology validation failed: icon must be a string");
					return false;
				}
			}

			// If symbology has a "style" field, it should be an object
			if (document.RootElement.TryGetProperty("style", out var style))
			{
				if (style.ValueKind != JsonValueKind.Object)
				{
					System.Diagnostics.Debug.WriteLine("Symbology validation failed: style must be an object");
					return false;
				}
			}

			return true;
		}
		catch (JsonException ex)
		{
			System.Diagnostics.Debug.WriteLine($"Symbology validation failed: {ex.Message}");
			return false;
		}
	}

	#endregion
}
