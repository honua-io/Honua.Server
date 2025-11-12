// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using NetTopologySuite.Geometries;

namespace HonuaField.Services;

/// <summary>
/// Service interface for Feature operations
/// Provides high-level business logic for feature management
/// </summary>
public interface IFeaturesService
{
	// CRUD Operations
	Task<Feature?> GetFeatureByIdAsync(string id);
	Task<List<Feature>> GetFeaturesByCollectionIdAsync(string collectionId, int skip = 0, int take = 50);
	Task<string> CreateFeatureAsync(Feature feature);
	Task<bool> UpdateFeatureAsync(Feature feature);
	Task<bool> DeleteFeatureAsync(string id);

	// Search and Filtering
	Task<List<Feature>> SearchFeaturesAsync(string collectionId, string searchText);
	Task<List<Feature>> GetFeaturesByPropertyAsync(string collectionId, string propertyName, object value);

	// Spatial Queries
	Task<List<Feature>> GetFeaturesInBoundsAsync(string collectionId, double minX, double minY, double maxX, double maxY);
	Task<List<Feature>> GetFeaturesNearbyAsync(double latitude, double longitude, double radiusMeters, string? collectionId = null);
	Task<Feature?> GetNearestFeatureAsync(double latitude, double longitude, string? collectionId = null);

	// Attachment Management
	Task<List<Attachment>> GetFeatureAttachmentsAsync(string featureId);
	Task<string> AddAttachmentAsync(Attachment attachment);
	Task<bool> DeleteAttachmentAsync(string attachmentId);
	Task<List<Attachment>> GetAttachmentsByTypeAsync(string featureId, AttachmentType type);

	// Sync and Change Tracking
	Task<List<Feature>> GetPendingSyncFeaturesAsync();
	Task<int> GetPendingChangesCountAsync();
	Task<bool> MarkFeatureAsSyncedAsync(string featureId);

	// Statistics
	Task<int> GetFeatureCountAsync(string collectionId);
	Task<(double minX, double minY, double maxX, double maxY)?> GetCollectionExtentAsync(string collectionId);
	Task<long> GetAttachmentsSizeAsync(string featureId);
}
