using HonuaField.Data.Repositories;
using HonuaField.Models;
using SQLite;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Implementation of IConflictResolutionService for detecting and resolving sync conflicts
/// Provides multiple resolution strategies and three-way merge support
/// </summary>
public class ConflictResolutionService : IConflictResolutionService
{
	private readonly IFeatureRepository _featureRepository;
	private readonly SQLiteAsyncConnection _connection;
	private readonly JsonSerializerOptions _jsonOptions;

	public ConflictResolutionService(
		IFeatureRepository featureRepository,
		IDatabaseService databaseService)
	{
		_featureRepository = featureRepository;
		_connection = databaseService.GetDatabase().GetConnection();

		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = false
		};

		// Create conflict_records table
		InitializeConflictTableAsync().Wait();
	}

	#region Public Methods

	/// <inheritdoc />
	public async Task<SyncConflict?> DetectConflictAsync(
		Feature localFeature,
		Feature serverFeature,
		Feature? baseFeature = null)
	{
		// No conflict if versions match
		if (localFeature.Version == serverFeature.Version)
		{
			return null;
		}

		// No conflict if local is older and hasn't been modified
		if (localFeature.Version < serverFeature.Version &&
			localFeature.UpdatedAt <= serverFeature.UpdatedAt)
		{
			return null;
		}

		// Detect conflict type
		ConflictType conflictType;

		if (localFeature.SyncStatus == SyncStatus.Pending.ToString() &&
			serverFeature.UpdatedAt > localFeature.UpdatedAt)
		{
			conflictType = ConflictType.ModifyModify;
		}
		else
		{
			conflictType = ConflictType.ModifyModify;
		}

		return new SyncConflict
		{
			FeatureId = localFeature.Id,
			CollectionId = localFeature.CollectionId,
			Type = conflictType,
			LocalVersion = localFeature.Version,
			ServerVersion = serverFeature.Version,
			LocalModifiedAt = DateTimeOffset.FromUnixTimeSeconds(localFeature.UpdatedAt),
			ServerModifiedAt = DateTimeOffset.FromUnixTimeSeconds(serverFeature.UpdatedAt),
			LocalProperties = localFeature.Properties,
			ServerProperties = serverFeature.Properties,
			Message = $"Feature modified on both client and server. Local v{localFeature.Version}, Server v{serverFeature.Version}"
		};
	}

	/// <inheritdoc />
	public async Task<Feature> ResolveConflictAsync(SyncConflict conflict, ResolutionStrategy strategy)
	{
		var localFeature = await _featureRepository.GetByIdAsync(conflict.FeatureId);
		if (localFeature == null)
		{
			throw new InvalidOperationException($"Local feature {conflict.FeatureId} not found");
		}

		Feature resolvedFeature;

		switch (strategy)
		{
			case ResolutionStrategy.ServerWins:
				resolvedFeature = await ResolveServerWinsAsync(conflict, localFeature);
				break;

			case ResolutionStrategy.ClientWins:
				resolvedFeature = await ResolveClientWinsAsync(conflict, localFeature);
				break;

			case ResolutionStrategy.AutoMerge:
				resolvedFeature = await ResolveAutoMergeAsync(conflict, localFeature);
				break;

			case ResolutionStrategy.Manual:
				throw new InvalidOperationException("Manual resolution requires custom merged properties");

			default:
				throw new ArgumentException($"Unknown resolution strategy: {strategy}");
		}

		// Update feature in database
		await _featureRepository.UpdateAsync(resolvedFeature);

		// Mark conflict as resolved
		var conflictRecords = await GetConflictRecordsByFeatureIdAsync(conflict.FeatureId);
		foreach (var record in conflictRecords)
		{
			await MarkConflictResolvedAsync(record.Id, strategy);
		}

		return resolvedFeature;
	}

	/// <inheritdoc />
	public async Task<Feature> ResolveConflictWithCustomMergeAsync(
		SyncConflict conflict,
		string mergedProperties)
	{
		var localFeature = await _featureRepository.GetByIdAsync(conflict.FeatureId);
		if (localFeature == null)
		{
			throw new InvalidOperationException($"Local feature {conflict.FeatureId} not found");
		}

		// Validate merged properties JSON
		try
		{
			JsonDocument.Parse(mergedProperties);
		}
		catch (JsonException ex)
		{
			throw new ArgumentException($"Invalid merged properties JSON: {ex.Message}", ex);
		}

		// Update feature with merged properties
		localFeature.Properties = mergedProperties;
		localFeature.Version = Math.Max(conflict.LocalVersion, conflict.ServerVersion) + 1;
		localFeature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		localFeature.SyncStatus = SyncStatus.Pending.ToString();

		await _featureRepository.UpdateAsync(localFeature);

		// Mark conflict as resolved
		var conflictRecords = await GetConflictRecordsByFeatureIdAsync(conflict.FeatureId);
		foreach (var record in conflictRecords)
		{
			await MarkConflictResolvedAsync(record.Id, ResolutionStrategy.Manual);
		}

		return localFeature;
	}

	/// <inheritdoc />
	public async Task<MergeResult> ThreeWayMergeAsync(
		string baseProperties,
		string localProperties,
		string serverProperties)
	{
		try
		{
			var baseProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(baseProperties, _jsonOptions)
				?? new Dictionary<string, JsonElement>();
			var localProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(localProperties, _jsonOptions)
				?? new Dictionary<string, JsonElement>();
			var serverProps = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(serverProperties, _jsonOptions)
				?? new Dictionary<string, JsonElement>();

			var merged = new Dictionary<string, JsonElement>();
			var conflicts = new List<PropertyConflict>();

			// Get all property keys
			var allKeys = baseProps.Keys
				.Union(localProps.Keys)
				.Union(serverProps.Keys)
				.Distinct()
				.ToList();

			foreach (var key in allKeys)
			{
				var hasBase = baseProps.TryGetValue(key, out var baseValue);
				var hasLocal = localProps.TryGetValue(key, out var localValue);
				var hasServer = serverProps.TryGetValue(key, out var serverValue);

				if (!hasLocal && !hasServer)
				{
					// Property removed by both - skip
					continue;
				}
				else if (!hasBase && hasLocal && hasServer)
				{
					// Property added by both
					if (JsonElement.DeepEquals(localValue, serverValue))
					{
						// Same value added - no conflict
						merged[key] = localValue;
					}
					else
					{
						// Different values added - conflict
						conflicts.Add(new PropertyConflict
						{
							PropertyName = key,
							BaseValue = null,
							LocalValue = GetJsonValue(localValue),
							ServerValue = GetJsonValue(serverValue),
							Reason = "Property added with different values"
						});
						// Default to local value
						merged[key] = localValue;
					}
				}
				else if (hasBase && hasLocal && !hasServer)
				{
					// Property removed on server
					if (JsonElement.DeepEquals(baseValue, localValue))
					{
						// Local unchanged, accept server deletion
						continue;
					}
					else
					{
						// Local changed, server deleted - conflict
						conflicts.Add(new PropertyConflict
						{
							PropertyName = key,
							BaseValue = GetJsonValue(baseValue),
							LocalValue = GetJsonValue(localValue),
							ServerValue = null,
							Reason = "Property deleted on server but modified locally"
						});
						// Keep local value
						merged[key] = localValue;
					}
				}
				else if (hasBase && !hasLocal && hasServer)
				{
					// Property removed locally
					if (JsonElement.DeepEquals(baseValue, serverValue))
					{
						// Server unchanged, accept local deletion
						continue;
					}
					else
					{
						// Server changed, local deleted - conflict
						conflicts.Add(new PropertyConflict
						{
							PropertyName = key,
							BaseValue = GetJsonValue(baseValue),
							LocalValue = null,
							ServerValue = GetJsonValue(serverValue),
							Reason = "Property deleted locally but modified on server"
						});
						// Keep server value
						merged[key] = serverValue;
					}
				}
				else if (hasBase && hasLocal && hasServer)
				{
					// Property exists in all three versions
					var localChanged = !JsonElement.DeepEquals(baseValue, localValue);
					var serverChanged = !JsonElement.DeepEquals(baseValue, serverValue);

					if (!localChanged && !serverChanged)
					{
						// No changes - use base
						merged[key] = baseValue;
					}
					else if (localChanged && !serverChanged)
					{
						// Only local changed - use local
						merged[key] = localValue;
					}
					else if (!localChanged && serverChanged)
					{
						// Only server changed - use server
						merged[key] = serverValue;
					}
					else
					{
						// Both changed - check if same
						if (JsonElement.DeepEquals(localValue, serverValue))
						{
							// Same change - no conflict
							merged[key] = localValue;
						}
						else
						{
							// Different changes - conflict
							conflicts.Add(new PropertyConflict
							{
								PropertyName = key,
								BaseValue = GetJsonValue(baseValue),
								LocalValue = GetJsonValue(localValue),
								ServerValue = GetJsonValue(serverValue),
								Reason = "Property modified differently on client and server"
							});
							// Default to local value
							merged[key] = localValue;
						}
					}
				}
				else if (!hasBase && hasLocal)
				{
					// New property locally
					merged[key] = localValue;
				}
				else if (!hasBase && hasServer)
				{
					// New property on server
					merged[key] = serverValue;
				}
			}

			var mergedJson = JsonSerializer.Serialize(merged, _jsonOptions);

			return new MergeResult
			{
				Success = !conflicts.Any(),
				MergedProperties = mergedJson,
				RemainingConflicts = conflicts
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Three-way merge failed: {ex.Message}");
			return new MergeResult
			{
				Success = false,
				MergedProperties = localProperties,
				RemainingConflicts = new List<PropertyConflict>(),
				ErrorMessage = ex.Message
			};
		}
	}

	/// <inheritdoc />
	public async Task<List<Feature>> ResolveBatchAsync(
		List<SyncConflict> conflicts,
		ResolutionStrategy strategy)
	{
		var resolvedFeatures = new List<Feature>();

		foreach (var conflict in conflicts)
		{
			try
			{
				var resolved = await ResolveConflictAsync(conflict, strategy);
				resolvedFeatures.Add(resolved);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to resolve conflict for feature {conflict.FeatureId}: {ex.Message}");
			}
		}

		return resolvedFeatures;
	}

	/// <inheritdoc />
	public async Task<List<ConflictRecord>> GetUnresolvedConflictsAsync()
	{
		return await _connection.Table<ConflictRecord>()
			.Where(c => c.ResolvedAt == null)
			.OrderByDescending(c => c.DetectedAt)
			.ToListAsync();
	}

	/// <inheritdoc />
	public async Task<int> SaveConflictAsync(SyncConflict conflict)
	{
		// Check if conflict already exists for this feature
		var existing = await _connection.Table<ConflictRecord>()
			.Where(c => c.FeatureId == conflict.FeatureId && c.ResolvedAt == null)
			.FirstOrDefaultAsync();

		if (existing != null)
		{
			// Update existing conflict
			existing.LocalVersion = conflict.LocalVersion;
			existing.ServerVersion = conflict.ServerVersion;
			existing.LocalModifiedAt = conflict.LocalModifiedAt.ToUnixTimeSeconds();
			existing.ServerModifiedAt = conflict.ServerModifiedAt.ToUnixTimeSeconds();
			existing.LocalProperties = conflict.LocalProperties ?? "{}";
			existing.ServerProperties = conflict.ServerProperties ?? "{}";
			existing.Message = conflict.Message;

			await _connection.UpdateAsync(existing);
			return existing.Id;
		}
		else
		{
			// Insert new conflict
			var record = ConflictRecord.FromSyncConflict(conflict);
			await _connection.InsertAsync(record);
			return record.Id;
		}
	}

	/// <inheritdoc />
	public async Task MarkConflictResolvedAsync(int conflictId, ResolutionStrategy strategy)
	{
		var conflict = await _connection.Table<ConflictRecord>()
			.Where(c => c.Id == conflictId)
			.FirstOrDefaultAsync();

		if (conflict != null)
		{
			conflict.ResolvedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			conflict.ResolutionStrategy = strategy.ToString();
			await _connection.UpdateAsync(conflict);

			System.Diagnostics.Debug.WriteLine($"Conflict {conflictId} marked as resolved using {strategy}");
		}
	}

	/// <inheritdoc />
	public async Task<int> CleanupResolvedConflictsAsync(int days = 30)
	{
		var cutoffTime = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeSeconds();

		var toDelete = await _connection.Table<ConflictRecord>()
			.Where(c => c.ResolvedAt != null && c.ResolvedAt < cutoffTime)
			.ToListAsync();

		foreach (var conflict in toDelete)
		{
			await _connection.DeleteAsync(conflict);
		}

		System.Diagnostics.Debug.WriteLine($"Cleaned up {toDelete.Count} resolved conflicts older than {days} days");
		return toDelete.Count;
	}

	#endregion

	#region Private Helper Methods

	/// <summary>
	/// Initialize conflict_records table
	/// </summary>
	private async Task InitializeConflictTableAsync()
	{
		await _connection.CreateTableAsync<ConflictRecord>();
	}

	/// <summary>
	/// Get conflict records by feature ID
	/// </summary>
	private async Task<List<ConflictRecord>> GetConflictRecordsByFeatureIdAsync(string featureId)
	{
		return await _connection.Table<ConflictRecord>()
			.Where(c => c.FeatureId == featureId && c.ResolvedAt == null)
			.ToListAsync();
	}

	/// <summary>
	/// Resolve conflict with server wins strategy
	/// </summary>
	private async Task<Feature> ResolveServerWinsAsync(SyncConflict conflict, Feature localFeature)
	{
		// Update local feature with server data
		localFeature.Properties = conflict.ServerProperties ?? "{}";
		localFeature.Version = conflict.ServerVersion;
		localFeature.UpdatedAt = conflict.ServerModifiedAt.ToUnixTimeSeconds();
		localFeature.SyncStatus = SyncStatus.Synced.ToString();

		return localFeature;
	}

	/// <summary>
	/// Resolve conflict with client wins strategy
	/// </summary>
	private async Task<Feature> ResolveClientWinsAsync(SyncConflict conflict, Feature localFeature)
	{
		// Keep local data, increment version
		localFeature.Version = Math.Max(conflict.LocalVersion, conflict.ServerVersion) + 1;
		localFeature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		localFeature.SyncStatus = SyncStatus.Pending.ToString(); // Needs to be pushed

		return localFeature;
	}

	/// <summary>
	/// Resolve conflict with auto-merge strategy
	/// </summary>
	private async Task<Feature> ResolveAutoMergeAsync(SyncConflict conflict, Feature localFeature)
	{
		// Attempt three-way merge if we have base properties
		var mergeResult = await ThreeWayMergeAsync(
			"{}", // We don't have base properties stored, use empty as fallback
			conflict.LocalProperties ?? "{}",
			conflict.ServerProperties ?? "{}");

		if (mergeResult.Success)
		{
			localFeature.Properties = mergeResult.MergedProperties;
			localFeature.Version = Math.Max(conflict.LocalVersion, conflict.ServerVersion) + 1;
			localFeature.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			localFeature.SyncStatus = SyncStatus.Pending.ToString();
			return localFeature;
		}
		else
		{
			// Auto-merge failed, fall back to server wins
			System.Diagnostics.Debug.WriteLine($"Auto-merge failed for feature {conflict.FeatureId}, falling back to server wins");
			return await ResolveServerWinsAsync(conflict, localFeature);
		}
	}

	/// <summary>
	/// Get value from JsonElement as object
	/// </summary>
	private object? GetJsonValue(JsonElement element)
	{
		return element.ValueKind switch
		{
			JsonValueKind.String => element.GetString(),
			JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Null => null,
			_ => element.GetRawText()
		};
	}

	#endregion
}

/// <summary>
/// SQLite table for storing conflict records
/// </summary>
[Table("conflict_records")]
public class ConflictRecord
{
	[PrimaryKey, AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	[Column("feature_id")]
	[NotNull]
	public required string FeatureId { get; set; }

	[Column("collection_id")]
	[NotNull]
	public required string CollectionId { get; set; }

	[Column("conflict_type")]
	[NotNull]
	public required string ConflictType { get; set; }

	[Column("local_version")]
	[NotNull]
	public required int LocalVersion { get; set; }

	[Column("server_version")]
	[NotNull]
	public required int ServerVersion { get; set; }

	[Column("local_modified_at")]
	[NotNull]
	public required long LocalModifiedAt { get; set; }

	[Column("server_modified_at")]
	[NotNull]
	public required long ServerModifiedAt { get; set; }

	[Column("local_properties")]
	[NotNull]
	public required string LocalProperties { get; set; }

	[Column("server_properties")]
	[NotNull]
	public required string ServerProperties { get; set; }

	[Column("base_properties")]
	public string? BaseProperties { get; set; }

	[Column("detected_at")]
	[NotNull]
	public required long DetectedAt { get; set; }

	[Column("resolved_at")]
	public long? ResolvedAt { get; set; }

	[Column("resolution_strategy")]
	public string? ResolutionStrategy { get; set; }

	[Column("message")]
	public string? Message { get; set; }

	/// <summary>
	/// Convert to SyncConflict for resolution
	/// </summary>
	public SyncConflict ToSyncConflict()
	{
		return new SyncConflict
		{
			FeatureId = FeatureId,
			CollectionId = CollectionId,
			Type = Enum.Parse<ConflictType>(ConflictType),
			LocalVersion = LocalVersion,
			ServerVersion = ServerVersion,
			LocalModifiedAt = DateTimeOffset.FromUnixTimeSeconds(LocalModifiedAt),
			ServerModifiedAt = DateTimeOffset.FromUnixTimeSeconds(ServerModifiedAt),
			LocalProperties = LocalProperties,
			ServerProperties = ServerProperties,
			Message = Message
		};
	}

	/// <summary>
	/// Create from SyncConflict
	/// </summary>
	public static ConflictRecord FromSyncConflict(SyncConflict conflict)
	{
		return new ConflictRecord
		{
			FeatureId = conflict.FeatureId,
			CollectionId = conflict.CollectionId,
			ConflictType = conflict.Type.ToString(),
			LocalVersion = conflict.LocalVersion,
			ServerVersion = conflict.ServerVersion,
			LocalModifiedAt = conflict.LocalModifiedAt.ToUnixTimeSeconds(),
			ServerModifiedAt = conflict.ServerModifiedAt.ToUnixTimeSeconds(),
			LocalProperties = conflict.LocalProperties ?? "{}",
			ServerProperties = conflict.ServerProperties ?? "{}",
			DetectedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
			Message = conflict.Message
		};
	}
}
