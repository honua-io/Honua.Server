// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository interface for Attachment operations
/// Provides CRUD methods for feature attachments (photos, videos, documents)
/// </summary>
public interface IAttachmentRepository
{
	// CRUD Operations
	Task<Attachment?> GetByIdAsync(string id);
	Task<List<Attachment>> GetAllAsync();
	Task<List<Attachment>> GetByFeatureIdAsync(string featureId);
	Task<string> InsertAsync(Attachment attachment);
	Task<int> UpdateAsync(Attachment attachment);
	Task<int> DeleteAsync(string id);
	Task<int> DeleteByFeatureIdAsync(string featureId);

	// Upload status operations
	Task<List<Attachment>> GetByUploadStatusAsync(UploadStatus status);
	Task<int> UpdateUploadStatusAsync(string id, UploadStatus status);

	// Type filtering
	Task<List<Attachment>> GetByTypeAsync(AttachmentType type);
	Task<List<Attachment>> GetByFeatureAndTypeAsync(string featureId, AttachmentType type);

	// Batch Operations
	Task<int> InsertBatchAsync(List<Attachment> attachments);
	Task<int> UpdateBatchAsync(List<Attachment> attachments);
	Task<int> DeleteBatchAsync(List<string> ids);

	// Statistics
	Task<int> GetCountAsync();
	Task<int> GetCountByFeatureAsync(string featureId);
	Task<long> GetTotalSizeAsync();
	Task<long> GetTotalSizeByFeatureAsync(string featureId);
}
