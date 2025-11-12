// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models;
using SQLite;

namespace HonuaField.Data.Repositories;

/// <summary>
/// Repository implementation for Attachment operations
/// Provides CRUD methods for feature attachments (photos, videos, documents)
/// </summary>
public class AttachmentRepository : IAttachmentRepository
{
	private readonly HonuaFieldDatabase _database;

	public AttachmentRepository(HonuaFieldDatabase database)
	{
		_database = database;
	}

	#region CRUD Operations

	public async Task<Attachment?> GetByIdAsync(string id)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>()
			.Where(a => a.Id == id)
			.FirstOrDefaultAsync();
	}

	public async Task<List<Attachment>> GetAllAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>().ToListAsync();
	}

	public async Task<List<Attachment>> GetByFeatureIdAsync(string featureId)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>()
			.Where(a => a.FeatureId == featureId)
			.ToListAsync();
	}

	public async Task<string> InsertAsync(Attachment attachment)
	{
		if (string.IsNullOrEmpty(attachment.Id))
			attachment.Id = Guid.NewGuid().ToString();

		var conn = _database.GetConnection();
		await conn.InsertAsync(attachment);

		System.Diagnostics.Debug.WriteLine($"Attachment inserted: {attachment.Id}");
		return attachment.Id;
	}

	public async Task<int> UpdateAsync(Attachment attachment)
	{
		var conn = _database.GetConnection();
		var result = await conn.UpdateAsync(attachment);

		System.Diagnostics.Debug.WriteLine($"Attachment updated: {attachment.Id}");
		return result;
	}

	public async Task<int> DeleteAsync(string id)
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Attachment>()
			.Where(a => a.Id == id)
			.DeleteAsync();

		System.Diagnostics.Debug.WriteLine($"Attachment deleted: {id}");
		return result;
	}

	public async Task<int> DeleteByFeatureIdAsync(string featureId)
	{
		var conn = _database.GetConnection();
		var result = await conn.Table<Attachment>()
			.Where(a => a.FeatureId == featureId)
			.DeleteAsync();

		System.Diagnostics.Debug.WriteLine($"Deleted {result} attachments for feature: {featureId}");
		return result;
	}

	#endregion

	#region Upload Status Operations

	public async Task<List<Attachment>> GetByUploadStatusAsync(UploadStatus status)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>()
			.Where(a => a.UploadStatus == status.ToString())
			.ToListAsync();
	}

	public async Task<int> UpdateUploadStatusAsync(string id, UploadStatus status)
	{
		var attachment = await GetByIdAsync(id);
		if (attachment == null)
			return 0;

		attachment.UploadStatus = status.ToString();

		var conn = _database.GetConnection();
		return await conn.UpdateAsync(attachment);
	}

	#endregion

	#region Type Filtering

	public async Task<List<Attachment>> GetByTypeAsync(AttachmentType type)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>()
			.Where(a => a.Type == type.ToString())
			.ToListAsync();
	}

	public async Task<List<Attachment>> GetByFeatureAndTypeAsync(string featureId, AttachmentType type)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>()
			.Where(a => a.FeatureId == featureId && a.Type == type.ToString())
			.ToListAsync();
	}

	#endregion

	#region Batch Operations

	public async Task<int> InsertBatchAsync(List<Attachment> attachments)
	{
		// Ensure all attachments have IDs
		foreach (var attachment in attachments)
		{
			if (string.IsNullOrEmpty(attachment.Id))
				attachment.Id = Guid.NewGuid().ToString();
		}

		var conn = _database.GetConnection();
		var result = await conn.InsertAllAsync(attachments);

		System.Diagnostics.Debug.WriteLine($"Batch inserted {result} attachments");
		return result;
	}

	public async Task<int> UpdateBatchAsync(List<Attachment> attachments)
	{
		var conn = _database.GetConnection();
		var result = await conn.UpdateAllAsync(attachments);

		System.Diagnostics.Debug.WriteLine($"Batch updated {result} attachments");
		return result;
	}

	public async Task<int> DeleteBatchAsync(List<string> ids)
	{
		var conn = _database.GetConnection();
		var result = 0;

		foreach (var id in ids)
		{
			result += await conn.Table<Attachment>()
				.Where(a => a.Id == id)
				.DeleteAsync();
		}

		System.Diagnostics.Debug.WriteLine($"Batch deleted {result} attachments");
		return result;
	}

	#endregion

	#region Statistics

	public async Task<int> GetCountAsync()
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>().CountAsync();
	}

	public async Task<int> GetCountByFeatureAsync(string featureId)
	{
		var conn = _database.GetConnection();
		return await conn.Table<Attachment>()
			.Where(a => a.FeatureId == featureId)
			.CountAsync();
	}

	public async Task<long> GetTotalSizeAsync()
	{
		var conn = _database.GetConnection();
		var attachments = await conn.Table<Attachment>().ToListAsync();
		return attachments.Sum(a => a.Size);
	}

	public async Task<long> GetTotalSizeByFeatureAsync(string featureId)
	{
		var attachments = await GetByFeatureIdAsync(featureId);
		return attachments.Sum(a => a.Size);
	}

	#endregion
}
