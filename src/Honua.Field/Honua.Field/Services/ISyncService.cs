namespace HonuaField.Services;

/// <summary>
/// Service for synchronizing local data with the Honua Server
/// Handles bidirectional sync with conflict detection and retry logic
/// </summary>
public interface ISyncService
{
	/// <summary>
	/// Synchronize all data (pull from server, then push local changes)
	/// </summary>
	/// <param name="progress">Optional progress reporter for sync operations</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Sync result with statistics and conflicts</returns>
	Task<SyncResult> SynchronizeAsync(IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Pull changes from server (download new/updated features and collections)
	/// </summary>
	/// <param name="progress">Optional progress reporter</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Pull result with downloaded item counts</returns>
	Task<PullResult> PullAsync(IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Push local changes to server (upload created/updated/deleted features)
	/// </summary>
	/// <param name="progress">Optional progress reporter</param>
	/// <param name="cancellationToken">Cancellation token</param>
	/// <returns>Push result with uploaded item counts and conflicts</returns>
	Task<PushResult> PushAsync(IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Check if network connectivity is available
	/// </summary>
	/// <returns>True if network is available</returns>
	Task<bool> IsNetworkAvailableAsync();

	/// <summary>
	/// Get count of pending local changes awaiting sync
	/// </summary>
	/// <returns>Count of pending changes</returns>
	Task<int> GetPendingChangesCountAsync();

	/// <summary>
	/// Get last sync timestamp
	/// </summary>
	/// <returns>Last sync timestamp or null if never synced</returns>
	Task<DateTimeOffset?> GetLastSyncTimeAsync();

	/// <summary>
	/// Schedule background sync operation
	/// </summary>
	/// <param name="intervalMinutes">Sync interval in minutes</param>
	Task ScheduleBackgroundSyncAsync(int intervalMinutes);

	/// <summary>
	/// Cancel scheduled background sync
	/// </summary>
	Task CancelBackgroundSyncAsync();
}

/// <summary>
/// Result of a full synchronization operation
/// </summary>
public record SyncResult
{
	public required bool Success { get; init; }
	public required PullResult PullResult { get; init; }
	public required PushResult PushResult { get; init; }
	public required DateTimeOffset SyncTime { get; init; }
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a pull operation from server
/// </summary>
public record PullResult
{
	public required bool Success { get; init; }
	public required int CollectionsDownloaded { get; init; }
	public required int FeaturesDownloaded { get; init; }
	public required int FeaturesUpdated { get; init; }
	public required int FeaturesDeleted { get; init; }
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of a push operation to server
/// </summary>
public record PushResult
{
	public required bool Success { get; init; }
	public required int FeaturesCreated { get; init; }
	public required int FeaturesUpdated { get; init; }
	public required int FeaturesDeleted { get; init; }
	public required int ChangesSynced { get; init; }
	public required List<SyncConflict> Conflicts { get; init; }
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Progress report for sync operations
/// </summary>
public record SyncProgress
{
	public required SyncStage Stage { get; init; }
	public required string Message { get; init; }
	public required int CurrentItem { get; init; }
	public required int TotalItems { get; init; }
	public required double PercentComplete { get; init; }
}

/// <summary>
/// Stages of synchronization process
/// </summary>
public enum SyncStage
{
	Starting,
	CheckingNetwork,
	PullingCollections,
	PullingFeatures,
	PushingChanges,
	ResolvingConflicts,
	Completing,
	Completed,
	Failed
}

/// <summary>
/// Represents a synchronization conflict
/// </summary>
public record SyncConflict
{
	public required string FeatureId { get; init; }
	public required string CollectionId { get; init; }
	public required ConflictType Type { get; init; }
	public required int LocalVersion { get; init; }
	public required int ServerVersion { get; init; }
	public required DateTimeOffset LocalModifiedAt { get; init; }
	public required DateTimeOffset ServerModifiedAt { get; init; }
	public string? LocalProperties { get; init; }
	public string? ServerProperties { get; init; }
	public string? Message { get; init; }
}

/// <summary>
/// Types of synchronization conflicts
/// </summary>
public enum ConflictType
{
	/// <summary>
	/// Both local and server versions modified since last sync
	/// </summary>
	ModifyModify,

	/// <summary>
	/// Feature deleted on server but modified locally
	/// </summary>
	ModifyDelete,

	/// <summary>
	/// Feature modified on server but deleted locally
	/// </summary>
	DeleteModify
}
