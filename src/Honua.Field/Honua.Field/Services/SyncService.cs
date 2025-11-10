// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Data.Repositories;
using HonuaField.Models;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Implementation of ISyncService for synchronizing local data with Honua Server
/// Handles bidirectional sync with conflict detection, retry logic, and progress reporting
/// </summary>
public class SyncService : ISyncService
{
	private const string LAST_SYNC_KEY = "last_sync_time";
	private const int MAX_RETRY_ATTEMPTS = 3;
	private const int RETRY_DELAY_MS = 1000;
	private const int BATCH_SIZE = 50;

	private readonly IApiClient _apiClient;
	private readonly IAuthenticationService _authService;
	private readonly ISettingsService _settingsService;
	private readonly IFeatureRepository _featureRepository;
	private readonly ICollectionRepository _collectionRepository;
	private readonly IChangeRepository _changeRepository;
	private readonly IConflictResolutionService _conflictResolutionService;

	public SyncService(
		IApiClient apiClient,
		IAuthenticationService authService,
		ISettingsService settingsService,
		IFeatureRepository featureRepository,
		ICollectionRepository collectionRepository,
		IChangeRepository changeRepository,
		IConflictResolutionService conflictResolutionService)
	{
		_apiClient = apiClient;
		_authService = authService;
		_settingsService = settingsService;
		_featureRepository = featureRepository;
		_collectionRepository = collectionRepository;
		_changeRepository = changeRepository;
		_conflictResolutionService = conflictResolutionService;
	}

	#region Public Methods

	/// <inheritdoc />
	public async Task<SyncResult> SynchronizeAsync(
		IProgress<SyncProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		var syncTime = DateTimeOffset.UtcNow;

		try
		{
			// Check authentication
			if (!await _authService.IsAuthenticatedAsync())
			{
				return new SyncResult
				{
					Success = false,
					PullResult = new PullResult
					{
						Success = false,
						CollectionsDownloaded = 0,
						FeaturesDownloaded = 0,
						FeaturesUpdated = 0,
						FeaturesDeleted = 0,
						ErrorMessage = "Not authenticated"
					},
					PushResult = new PushResult
					{
						Success = false,
						FeaturesCreated = 0,
						FeaturesUpdated = 0,
						FeaturesDeleted = 0,
						ChangesSynced = 0,
						Conflicts = new List<SyncConflict>(),
						ErrorMessage = "Not authenticated"
					},
					SyncTime = syncTime,
					ErrorMessage = "Not authenticated. Please log in first."
				};
			}

			ReportProgress(progress, SyncStage.Starting, "Starting synchronization...", 0, 100, 0);

			// Check network connectivity
			ReportProgress(progress, SyncStage.CheckingNetwork, "Checking network connectivity...", 0, 100, 5);
			if (!await IsNetworkAvailableAsync())
			{
				throw new InvalidOperationException("No network connectivity available");
			}

			cancellationToken.ThrowIfCancellationRequested();

			// Pull changes from server
			var pullResult = await PullAsync(progress, cancellationToken);
			if (!pullResult.Success)
			{
				return new SyncResult
				{
					Success = false,
					PullResult = pullResult,
					PushResult = new PushResult
					{
						Success = false,
						FeaturesCreated = 0,
						FeaturesUpdated = 0,
						FeaturesDeleted = 0,
						ChangesSynced = 0,
						Conflicts = new List<SyncConflict>()
					},
					SyncTime = syncTime,
					ErrorMessage = $"Pull failed: {pullResult.ErrorMessage}"
				};
			}

			cancellationToken.ThrowIfCancellationRequested();

			// Push local changes to server
			var pushResult = await PushAsync(progress, cancellationToken);

			// Update last sync time
			await _settingsService.SetAsync(LAST_SYNC_KEY, syncTime.ToUnixTimeSeconds());

			ReportProgress(progress, SyncStage.Completed, "Synchronization completed", 100, 100, 100);

			return new SyncResult
			{
				Success = pullResult.Success && pushResult.Success,
				PullResult = pullResult,
				PushResult = pushResult,
				SyncTime = syncTime,
				ErrorMessage = !pushResult.Success ? $"Push completed with errors: {pushResult.ErrorMessage}" : null
			};
		}
		catch (OperationCanceledException)
		{
			System.Diagnostics.Debug.WriteLine("Synchronization cancelled by user");
			ReportProgress(progress, SyncStage.Failed, "Synchronization cancelled", 0, 100, 0);
			throw;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Synchronization failed: {ex.Message}");
			ReportProgress(progress, SyncStage.Failed, $"Synchronization failed: {ex.Message}", 0, 100, 0);

			return new SyncResult
			{
				Success = false,
				PullResult = new PullResult
				{
					Success = false,
					CollectionsDownloaded = 0,
					FeaturesDownloaded = 0,
					FeaturesUpdated = 0,
					FeaturesDeleted = 0,
					ErrorMessage = ex.Message
				},
				PushResult = new PushResult
				{
					Success = false,
					FeaturesCreated = 0,
					FeaturesUpdated = 0,
					FeaturesDeleted = 0,
					ChangesSynced = 0,
					Conflicts = new List<SyncConflict>(),
					ErrorMessage = ex.Message
				},
				SyncTime = syncTime,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <inheritdoc />
	public async Task<PullResult> PullAsync(
		IProgress<SyncProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		var collectionsDownloaded = 0;
		var featuresDownloaded = 0;
		var featuresUpdated = 0;
		var featuresDeleted = 0;

		try
		{
			var accessToken = await _authService.GetAccessTokenAsync();
			if (string.IsNullOrEmpty(accessToken))
			{
				throw new InvalidOperationException("No access token available");
			}

			// Pull collections
			ReportProgress(progress, SyncStage.PullingCollections, "Downloading collections...", 0, 2, 25);
			var collections = await RetryAsync(() =>
				_apiClient.GetAsync<List<CollectionDto>>("/api/collections", accessToken));

			if (collections != null && collections.Any())
			{
				foreach (var collectionDto in collections)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var existingCollection = await _collectionRepository.GetByIdAsync(collectionDto.Id);
					var collection = MapToCollection(collectionDto);

					if (existingCollection == null)
					{
						await _collectionRepository.InsertAsync(collection);
						collectionsDownloaded++;
					}
					else
					{
						await _collectionRepository.UpdateAsync(collection);
					}
				}
			}

			// Pull features
			ReportProgress(progress, SyncStage.PullingFeatures, "Downloading features...", 1, 2, 50);

			var lastSyncTime = await GetLastSyncTimeAsync();
			var query = lastSyncTime.HasValue
				? $"/api/features?since={lastSyncTime.Value.ToUnixTimeSeconds()}"
				: "/api/features";

			var featureResponse = await RetryAsync(() =>
				_apiClient.GetAsync<FeatureListResponse>(query, accessToken));

			if (featureResponse != null && featureResponse.Features != null)
			{
				var totalFeatures = featureResponse.Features.Count;
				var processedFeatures = 0;

				foreach (var featureDto in featureResponse.Features)
				{
					cancellationToken.ThrowIfCancellationRequested();

					var existingFeature = await _featureRepository.GetByIdAsync(featureDto.Id);

					if (featureDto.Deleted)
					{
						// Feature was deleted on server
						if (existingFeature != null)
						{
							await _featureRepository.DeleteAsync(featureDto.Id);
							await _changeRepository.DeleteByFeatureIdAsync(featureDto.Id);
							featuresDeleted++;
						}
					}
					else
					{
						var feature = MapToFeature(featureDto);

						if (existingFeature == null)
						{
							// New feature from server
							await _featureRepository.InsertAsync(feature);
							featuresDownloaded++;
						}
						else if (existingFeature.Version < feature.Version)
						{
							// Server version is newer - check for conflicts
							var hasLocalChanges = await HasPendingChangesAsync(featureDto.Id);

							if (hasLocalChanges)
							{
								// Potential conflict - let ConflictResolutionService handle it
								var conflict = await _conflictResolutionService.DetectConflictAsync(
									existingFeature, feature);

								if (conflict != null)
								{
									// Save conflict for later resolution
									await _conflictResolutionService.SaveConflictAsync(conflict);
									await _featureRepository.UpdateSyncStatusAsync(feature.Id, SyncStatus.Conflict);
								}
								else
								{
									// No conflict detected, safe to update
									await _featureRepository.UpdateAsync(feature);
									featuresUpdated++;
								}
							}
							else
							{
								// No local changes, safe to update
								await _featureRepository.UpdateAsync(feature);
								featuresUpdated++;
							}
						}
					}

					processedFeatures++;
					var featureProgress = 50 + (int)((processedFeatures / (double)totalFeatures) * 25);
					ReportProgress(progress, SyncStage.PullingFeatures,
						$"Processing features... ({processedFeatures}/{totalFeatures})",
						processedFeatures, totalFeatures, featureProgress);
				}
			}

			return new PullResult
			{
				Success = true,
				CollectionsDownloaded = collectionsDownloaded,
				FeaturesDownloaded = featuresDownloaded,
				FeaturesUpdated = featuresUpdated,
				FeaturesDeleted = featuresDeleted
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Pull failed: {ex.Message}");
			return new PullResult
			{
				Success = false,
				CollectionsDownloaded = collectionsDownloaded,
				FeaturesDownloaded = featuresDownloaded,
				FeaturesUpdated = featuresUpdated,
				FeaturesDeleted = featuresDeleted,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <inheritdoc />
	public async Task<PushResult> PushAsync(
		IProgress<SyncProgress>? progress = null,
		CancellationToken cancellationToken = default)
	{
		var featuresCreated = 0;
		var featuresUpdated = 0;
		var featuresDeleted = 0;
		var changesSynced = 0;
		var conflicts = new List<SyncConflict>();

		try
		{
			var accessToken = await _authService.GetAccessTokenAsync();
			if (string.IsNullOrEmpty(accessToken))
			{
				throw new InvalidOperationException("No access token available");
			}

			ReportProgress(progress, SyncStage.PushingChanges, "Uploading local changes...", 0, 3, 75);

			// Get all pending changes
			var pendingChanges = await _changeRepository.GetPendingAsync();
			if (!pendingChanges.Any())
			{
				return new PushResult
				{
					Success = true,
					FeaturesCreated = 0,
					FeaturesUpdated = 0,
					FeaturesDeleted = 0,
					ChangesSynced = 0,
					Conflicts = new List<SyncConflict>()
				};
			}

			var totalChanges = pendingChanges.Count;
			var processedChanges = 0;

			// Group changes by operation type
			var insertChanges = pendingChanges.Where(c => c.Operation == ChangeOperation.Insert.ToString()).ToList();
			var updateChanges = pendingChanges.Where(c => c.Operation == ChangeOperation.Update.ToString()).ToList();
			var deleteChanges = pendingChanges.Where(c => c.Operation == ChangeOperation.Delete.ToString()).ToList();

			// Process inserts
			foreach (var change in insertChanges)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					var feature = await _featureRepository.GetByIdAsync(change.FeatureId);
					if (feature == null) continue;

					var featureDto = MapToFeatureDto(feature);
					var result = await RetryAsync(() =>
						_apiClient.PostAsync<FeatureDto>("/api/features", featureDto, accessToken));

					if (result != null)
					{
						// Update local feature with server ID and version
						feature.ServerId = result.Id;
						feature.Version = result.Version;
						feature.SyncStatus = SyncStatus.Synced.ToString();
						await _featureRepository.UpdateAsync(feature);
						await _changeRepository.MarkAsSyncedAsync(change.Id);

						featuresCreated++;
						changesSynced++;
					}
				}
				catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
				{
					// Conflict detected by server
					conflicts.Add(await CreateConflictFromApiErrorAsync(change.FeatureId, ex));
					await _featureRepository.UpdateSyncStatusAsync(change.FeatureId, SyncStatus.Conflict);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to push insert for feature {change.FeatureId}: {ex.Message}");
					await _featureRepository.UpdateSyncStatusAsync(change.FeatureId, SyncStatus.Error);
				}

				processedChanges++;
				var pushProgress = 75 + (int)((processedChanges / (double)totalChanges) * 20);
				ReportProgress(progress, SyncStage.PushingChanges,
					$"Uploading changes... ({processedChanges}/{totalChanges})",
					processedChanges, totalChanges, pushProgress);
			}

			// Process updates
			foreach (var change in updateChanges)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					var feature = await _featureRepository.GetByIdAsync(change.FeatureId);
					if (feature == null) continue;

					var featureDto = MapToFeatureDto(feature);
					var serverId = feature.ServerId ?? feature.Id;
					var result = await RetryAsync(() =>
						_apiClient.PutAsync<FeatureDto>($"/api/features/{serverId}", featureDto, accessToken));

					if (result != null)
					{
						feature.Version = result.Version;
						feature.SyncStatus = SyncStatus.Synced.ToString();
						await _featureRepository.UpdateAsync(feature);
						await _changeRepository.MarkAsSyncedAsync(change.Id);

						featuresUpdated++;
						changesSynced++;
					}
				}
				catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
				{
					// Conflict detected by server
					conflicts.Add(await CreateConflictFromApiErrorAsync(change.FeatureId, ex));
					await _featureRepository.UpdateSyncStatusAsync(change.FeatureId, SyncStatus.Conflict);
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to push update for feature {change.FeatureId}: {ex.Message}");
					await _featureRepository.UpdateSyncStatusAsync(change.FeatureId, SyncStatus.Error);
				}

				processedChanges++;
				var pushProgress = 75 + (int)((processedChanges / (double)totalChanges) * 20);
				ReportProgress(progress, SyncStage.PushingChanges,
					$"Uploading changes... ({processedChanges}/{totalChanges})",
					processedChanges, totalChanges, pushProgress);
			}

			// Process deletes
			foreach (var change in deleteChanges)
			{
				cancellationToken.ThrowIfCancellationRequested();

				try
				{
					var feature = await _featureRepository.GetByIdAsync(change.FeatureId);
					var serverId = feature?.ServerId ?? change.FeatureId;

					var success = await RetryAsync(() =>
						_apiClient.DeleteAsync($"/api/features/{serverId}", accessToken));

					if (success)
					{
						await _changeRepository.MarkAsSyncedAsync(change.Id);
						featuresDeleted++;
						changesSynced++;
					}
				}
				catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					// Feature already deleted on server, mark as synced
					await _changeRepository.MarkAsSyncedAsync(change.Id);
					featuresDeleted++;
					changesSynced++;
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Failed to push delete for feature {change.FeatureId}: {ex.Message}");
				}

				processedChanges++;
				var pushProgress = 75 + (int)((processedChanges / (double)totalChanges) * 20);
				ReportProgress(progress, SyncStage.PushingChanges,
					$"Uploading changes... ({processedChanges}/{totalChanges})",
					processedChanges, totalChanges, pushProgress);
			}

			return new PushResult
			{
				Success = true,
				FeaturesCreated = featuresCreated,
				FeaturesUpdated = featuresUpdated,
				FeaturesDeleted = featuresDeleted,
				ChangesSynced = changesSynced,
				Conflicts = conflicts
			};
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Push failed: {ex.Message}");
			return new PushResult
			{
				Success = false,
				FeaturesCreated = featuresCreated,
				FeaturesUpdated = featuresUpdated,
				FeaturesDeleted = featuresDeleted,
				ChangesSynced = changesSynced,
				Conflicts = conflicts,
				ErrorMessage = ex.Message
			};
		}
	}

	/// <inheritdoc />
	public async Task<bool> IsNetworkAvailableAsync()
	{
		try
		{
			var current = Connectivity.Current.NetworkAccess;
			return await Task.FromResult(current == NetworkAccess.Internet);
		}
		catch
		{
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<int> GetPendingChangesCountAsync()
	{
		return await _changeRepository.GetPendingCountAsync();
	}

	/// <inheritdoc />
	public async Task<DateTimeOffset?> GetLastSyncTimeAsync()
	{
		var lastSync = await _settingsService.GetAsync<long>(LAST_SYNC_KEY, 0);
		return lastSync > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastSync) : null;
	}

	/// <inheritdoc />
	public async Task ScheduleBackgroundSyncAsync(int intervalMinutes)
	{
		// Store sync interval preference
		await _settingsService.SetAsync("background_sync_interval", intervalMinutes);
		await _settingsService.SetAsync("background_sync_enabled", true);

		System.Diagnostics.Debug.WriteLine($"Background sync scheduled for every {intervalMinutes} minutes");
		// Note: Actual background task scheduling would be platform-specific
		// and would be implemented in platform-specific code
	}

	/// <inheritdoc />
	public async Task CancelBackgroundSyncAsync()
	{
		await _settingsService.SetAsync("background_sync_enabled", false);
		System.Diagnostics.Debug.WriteLine("Background sync cancelled");
	}

	#endregion

	#region Private Helper Methods

	/// <summary>
	/// Execute an async operation with retry logic
	/// </summary>
	private async Task<T?> RetryAsync<T>(Func<Task<T?>> operation, int maxAttempts = MAX_RETRY_ATTEMPTS)
	{
		Exception? lastException = null;

		for (int attempt = 1; attempt <= maxAttempts; attempt++)
		{
			try
			{
				return await operation();
			}
			catch (Exception ex)
			{
				lastException = ex;
				System.Diagnostics.Debug.WriteLine($"Attempt {attempt} failed: {ex.Message}");

				if (attempt < maxAttempts)
				{
					await Task.Delay(RETRY_DELAY_MS * attempt);
				}
			}
		}

		throw lastException ?? new InvalidOperationException("Retry operation failed");
	}

	/// <summary>
	/// Check if a feature has pending local changes
	/// </summary>
	private async Task<bool> HasPendingChangesAsync(string featureId)
	{
		var changes = await _changeRepository.GetByFeatureIdAsync(featureId);
		return changes.Any(c => c.Synced == 0);
	}

	/// <summary>
	/// Create a SyncConflict from an API error response
	/// </summary>
	private async Task<SyncConflict> CreateConflictFromApiErrorAsync(string featureId, ApiException ex)
	{
		var feature = await _featureRepository.GetByIdAsync(featureId);

		return new SyncConflict
		{
			FeatureId = featureId,
			CollectionId = feature?.CollectionId ?? string.Empty,
			Type = ConflictType.ModifyModify,
			LocalVersion = feature?.Version ?? 1,
			ServerVersion = 0, // Unknown from error
			LocalModifiedAt = DateTimeOffset.FromUnixTimeSeconds(feature?.UpdatedAt ?? 0),
			ServerModifiedAt = DateTimeOffset.UtcNow,
			LocalProperties = feature?.Properties,
			Message = ex.Message
		};
	}

	/// <summary>
	/// Report progress to the provided progress reporter
	/// </summary>
	private void ReportProgress(
		IProgress<SyncProgress>? progress,
		SyncStage stage,
		string message,
		int current,
		int total,
		double percentComplete)
	{
		progress?.Report(new SyncProgress
		{
			Stage = stage,
			Message = message,
			CurrentItem = current,
			TotalItems = total,
			PercentComplete = percentComplete
		});
	}

	#endregion

	#region Mapping Methods

	/// <summary>
	/// Map CollectionDto to Collection entity
	/// </summary>
	private Collection MapToCollection(CollectionDto dto)
	{
		return new Collection
		{
			Id = dto.Id,
			Title = dto.Title,
			Description = dto.Description,
			Schema = dto.Schema ?? "{}",
			Symbology = dto.Symbology ?? "{}",
			Extent = dto.Extent,
			ItemsCount = dto.ItemsCount
		};
	}

	/// <summary>
	/// Map FeatureDto to Feature entity
	/// </summary>
	private Feature MapToFeature(FeatureDto dto)
	{
		var feature = new Feature
		{
			Id = dto.Id,
			ServerId = dto.Id,
			CollectionId = dto.CollectionId,
			Properties = dto.Properties ?? "{}",
			CreatedAt = dto.CreatedAt,
			UpdatedAt = dto.UpdatedAt,
			CreatedBy = dto.CreatedBy ?? string.Empty,
			Version = dto.Version,
			SyncStatus = SyncStatus.Synced.ToString()
		};

		// Convert GeoJSON geometry to WKB
		if (dto.Geometry != null)
		{
			try
			{
				var reader = new NetTopologySuite.IO.GeoJsonReader();
				var geometry = reader.Read<NetTopologySuite.Geometries.Geometry>(dto.Geometry);
				feature.SetGeometry(geometry);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to parse geometry: {ex.Message}");
			}
		}

		return feature;
	}

	/// <summary>
	/// Map Feature entity to FeatureDto
	/// </summary>
	private FeatureDto MapToFeatureDto(Feature feature)
	{
		string? geometryJson = null;

		// Convert WKB to GeoJSON
		var geometry = feature.GetGeometry();
		if (geometry != null)
		{
			try
			{
				var writer = new NetTopologySuite.IO.GeoJsonWriter();
				geometryJson = writer.Write(geometry);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Failed to write geometry: {ex.Message}");
			}
		}

		return new FeatureDto
		{
			Id = feature.ServerId ?? feature.Id,
			CollectionId = feature.CollectionId,
			Geometry = geometryJson,
			Properties = feature.Properties,
			CreatedAt = feature.CreatedAt,
			UpdatedAt = feature.UpdatedAt,
			CreatedBy = feature.CreatedBy,
			Version = feature.Version,
			Deleted = false
		};
	}

	#endregion
}

#region DTOs

/// <summary>
/// Collection data transfer object for API communication
/// </summary>
internal record CollectionDto
{
	public required string Id { get; init; }
	public required string Title { get; init; }
	public string? Description { get; init; }
	public string? Schema { get; init; }
	public string? Symbology { get; init; }
	public string? Extent { get; init; }
	public int ItemsCount { get; init; }
}

/// <summary>
/// Feature data transfer object for API communication
/// </summary>
internal record FeatureDto
{
	public required string Id { get; init; }
	public required string CollectionId { get; init; }
	public string? Geometry { get; init; }
	public string? Properties { get; init; }
	public long CreatedAt { get; init; }
	public long UpdatedAt { get; init; }
	public string? CreatedBy { get; init; }
	public int Version { get; init; }
	public bool Deleted { get; init; }
}

/// <summary>
/// Response for feature list queries
/// </summary>
internal record FeatureListResponse
{
	public required List<FeatureDto> Features { get; init; }
	public int TotalCount { get; init; }
	public long? Timestamp { get; init; }
}

#endregion
