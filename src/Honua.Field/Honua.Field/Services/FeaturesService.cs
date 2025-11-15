// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data.Repositories;
using HonuaField.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Service implementation for Feature operations
/// Provides high-level business logic with offline-first design
/// </summary>
public class FeaturesService : IFeaturesService
{
	private readonly IFeatureRepository _featureRepository;
	private readonly IAttachmentRepository _attachmentRepository;
	private readonly IChangeRepository _changeRepository;
	private readonly ICollectionRepository _collectionRepository;
	private readonly ILogger<FeaturesService> _logger;

	public FeaturesService(
		IFeatureRepository featureRepository,
		IAttachmentRepository attachmentRepository,
		IChangeRepository changeRepository,
		ICollectionRepository collectionRepository,
		ILogger<FeaturesService> logger)
	{
		_featureRepository = featureRepository;
		_attachmentRepository = attachmentRepository;
		_changeRepository = changeRepository;
		_collectionRepository = collectionRepository;
		_logger = logger;
	}

	#region CRUD Operations

	/// <summary>
	/// Get a feature by its ID
	/// </summary>
	public async Task<Feature?> GetFeatureByIdAsync(string id)
	{
		try
		{
			return await _featureRepository.GetByIdAsync(id);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting feature {FeatureId}", id);
			throw;
		}
	}

	/// <summary>
	/// Get features by collection with pagination
	/// </summary>
	public async Task<List<Feature>> GetFeaturesByCollectionIdAsync(string collectionId, int skip = 0, int take = 50)
	{
		try
		{
			var allFeatures = await _featureRepository.GetByCollectionIdAsync(collectionId);

			// Apply pagination
			return allFeatures
				.Skip(skip)
				.Take(take)
				.ToList();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting features for collection {CollectionId}", collectionId);
			throw;
		}
	}

	/// <summary>
	/// Create a new feature with change tracking
	/// </summary>
	public async Task<string> CreateFeatureAsync(Feature feature)
	{
		try
		{
			// Validate that collection exists
			var collection = await _collectionRepository.GetByIdAsync(feature.CollectionId);
			if (collection == null)
			{
				throw new InvalidOperationException($"Collection {feature.CollectionId} does not exist");
			}

			// Insert feature (repository handles change tracking)
			var featureId = await _featureRepository.InsertAsync(feature);

			// Update collection item count
			await _collectionRepository.IncrementItemsCountAsync(feature.CollectionId, 1);

			return featureId;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error creating feature");
			throw;
		}
	}

	/// <summary>
	/// Update an existing feature with change tracking
	/// </summary>
	public async Task<bool> UpdateFeatureAsync(Feature feature)
	{
		try
		{
			var result = await _featureRepository.UpdateAsync(feature);
			return result > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error updating feature {FeatureId}", feature.Id);
			throw;
		}
	}

	/// <summary>
	/// Delete a feature and all its attachments with change tracking
	/// </summary>
	public async Task<bool> DeleteFeatureAsync(string id)
	{
		try
		{
			// Get feature to update collection count
			var feature = await _featureRepository.GetByIdAsync(id);
			if (feature == null)
			{
				return false;
			}

			// Delete all attachments first
			await _attachmentRepository.DeleteByFeatureIdAsync(id);

			// Delete feature (repository handles change tracking)
			var result = await _featureRepository.DeleteAsync(id);

			// Update collection item count
			if (result > 0)
			{
				await _collectionRepository.IncrementItemsCountAsync(feature.CollectionId, -1);
			}

			return result > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting feature {FeatureId}", id);
			throw;
		}
	}

	#endregion

	#region Search and Filtering

	/// <summary>
	/// Search features by text in properties
	/// </summary>
	public async Task<List<Feature>> SearchFeaturesAsync(string collectionId, string searchText)
	{
		try
		{
			if (string.IsNullOrWhiteSpace(searchText))
			{
				return await GetFeaturesByCollectionIdAsync(collectionId);
			}

			var allFeatures = await _featureRepository.GetByCollectionIdAsync(collectionId);
			var searchLower = searchText.ToLower();

			// Search in feature properties (JSON)
			var matchingFeatures = allFeatures.Where(f =>
			{
				try
				{
					// Simple JSON string search (can be improved with structured search)
					return f.Properties.ToLower().Contains(searchLower);
				}
				catch
				{
					return false;
				}
			}).ToList();

			return matchingFeatures;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error searching features");
			throw;
		}
	}

	/// <summary>
	/// Get features by property value
	/// </summary>
	public async Task<List<Feature>> GetFeaturesByPropertyAsync(string collectionId, string propertyName, object value)
	{
		try
		{
			var allFeatures = await _featureRepository.GetByCollectionIdAsync(collectionId);

			var matchingFeatures = allFeatures.Where(f =>
			{
				try
				{
					var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(f.Properties);
					if (properties == null || !properties.ContainsKey(propertyName))
					{
						return false;
					}

					var propertyValue = properties[propertyName];

					// Compare based on value type
					if (value is string strValue)
					{
						return propertyValue.GetString() == strValue;
					}
					else if (value is int intValue)
					{
						return propertyValue.GetInt32() == intValue;
					}
					else if (value is double doubleValue)
					{
						const double NumericComparisonTolerance = 0.0001;
						return Math.Abs(propertyValue.GetDouble() - doubleValue) < NumericComparisonTolerance;
					}
					else if (value is bool boolValue)
					{
						return propertyValue.GetBoolean() == boolValue;
					}

					return false;
				}
				catch
				{
					return false;
				}
			}).ToList();

			return matchingFeatures;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting features by property");
			throw;
		}
	}

	#endregion

	#region Spatial Queries

	/// <summary>
	/// Get features within bounding box
	/// </summary>
	public async Task<List<Feature>> GetFeaturesInBoundsAsync(string collectionId, double minX, double minY, double maxX, double maxY)
	{
		try
		{
			var featuresInBounds = await _featureRepository.GetByBoundsAsync(minX, minY, maxX, maxY);

			// Filter by collection if specified
			if (!string.IsNullOrEmpty(collectionId))
			{
				featuresInBounds = featuresInBounds
					.Where(f => f.CollectionId == collectionId)
					.ToList();
			}

			return featuresInBounds;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting features in bounds");
			throw;
		}
	}

	/// <summary>
	/// Get features within radius of a point
	/// </summary>
	public async Task<List<Feature>> GetFeaturesNearbyAsync(double latitude, double longitude, double radiusMeters, string? collectionId = null)
	{
		try
		{
			var point = new Point(longitude, latitude);
			var nearbyFeatures = await _featureRepository.GetWithinDistanceAsync(point, radiusMeters);

			// Filter by collection if specified
			if (!string.IsNullOrEmpty(collectionId))
			{
				nearbyFeatures = nearbyFeatures
					.Where(f => f.CollectionId == collectionId)
					.ToList();
			}

			return nearbyFeatures;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting nearby features");
			throw;
		}
	}

	/// <summary>
	/// Get nearest feature to a point
	/// </summary>
	public async Task<Feature?> GetNearestFeatureAsync(double latitude, double longitude, string? collectionId = null)
	{
		try
		{
			var point = new Point(longitude, latitude);

			// If collection specified, get nearest from that collection
			if (!string.IsNullOrEmpty(collectionId))
			{
				var collectionFeatures = await _featureRepository.GetByCollectionIdAsync(collectionId);

				Feature? nearestFeature = null;
				double minDistance = double.MaxValue;

				foreach (var feature in collectionFeatures)
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

				return nearestFeature;
			}
			else
			{
				return await _featureRepository.GetNearestAsync(point);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting nearest feature");
			throw;
		}
	}

	#endregion

	#region Attachment Management

	/// <summary>
	/// Get all attachments for a feature
	/// </summary>
	public async Task<List<Attachment>> GetFeatureAttachmentsAsync(string featureId)
	{
		try
		{
			return await _attachmentRepository.GetByFeatureIdAsync(featureId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting attachments for feature {FeatureId}", featureId);
			throw;
		}
	}

	/// <summary>
	/// Add an attachment to a feature
	/// </summary>
	public async Task<string> AddAttachmentAsync(Attachment attachment)
	{
		try
		{
			// Validate that feature exists
			var feature = await _featureRepository.GetByIdAsync(attachment.FeatureId);
			if (feature == null)
			{
				throw new InvalidOperationException($"Feature {attachment.FeatureId} does not exist");
			}

			return await _attachmentRepository.InsertAsync(attachment);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error adding attachment");
			throw;
		}
	}

	/// <summary>
	/// Delete an attachment
	/// </summary>
	public async Task<bool> DeleteAttachmentAsync(string attachmentId)
	{
		try
		{
			var result = await _attachmentRepository.DeleteAsync(attachmentId);
			return result > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error deleting attachment {AttachmentId}", attachmentId);
			throw;
		}
	}

	/// <summary>
	/// Get attachments by type for a feature
	/// </summary>
	public async Task<List<Attachment>> GetAttachmentsByTypeAsync(string featureId, AttachmentType type)
	{
		try
		{
			return await _attachmentRepository.GetByFeatureAndTypeAsync(featureId, type);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting attachments by type");
			throw;
		}
	}

	#endregion

	#region Sync and Change Tracking

	/// <summary>
	/// Get all features pending sync
	/// </summary>
	public async Task<List<Feature>> GetPendingSyncFeaturesAsync()
	{
		try
		{
			return await _featureRepository.GetPendingSyncAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting pending sync features");
			throw;
		}
	}

	/// <summary>
	/// Get count of pending changes
	/// </summary>
	public async Task<int> GetPendingChangesCountAsync()
	{
		try
		{
			return await _changeRepository.GetPendingCountAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting pending changes count");
			throw;
		}
	}

	/// <summary>
	/// Mark a feature as synced
	/// </summary>
	public async Task<bool> MarkFeatureAsSyncedAsync(string featureId)
	{
		try
		{
			var result = await _featureRepository.UpdateSyncStatusAsync(featureId, SyncStatus.Synced);
			return result > 0;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error marking feature as synced");
			throw;
		}
	}

	#endregion

	#region Statistics

	/// <summary>
	/// Get count of features in a collection
	/// </summary>
	public async Task<int> GetFeatureCountAsync(string collectionId)
	{
		try
		{
			return await _featureRepository.GetCountByCollectionAsync(collectionId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting feature count");
			throw;
		}
	}

	/// <summary>
	/// Get spatial extent of a collection
	/// </summary>
	public async Task<(double minX, double minY, double maxX, double maxY)?> GetCollectionExtentAsync(string collectionId)
	{
		try
		{
			return await _featureRepository.GetExtentAsync(collectionId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting collection extent");
			throw;
		}
	}

	/// <summary>
	/// Get total size of attachments for a feature
	/// </summary>
	public async Task<long> GetAttachmentsSizeAsync(string featureId)
	{
		try
		{
			return await _attachmentRepository.GetTotalSizeByFeatureAsync(featureId);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting attachments size");
			throw;
		}
	}

	#endregion
}
