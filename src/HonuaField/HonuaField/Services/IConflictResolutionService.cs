using HonuaField.Models;

namespace HonuaField.Services;

/// <summary>
/// Service for detecting and resolving synchronization conflicts
/// Provides multiple resolution strategies and three-way merge support
/// </summary>
public interface IConflictResolutionService
{
	/// <summary>
	/// Detect conflicts between local and server features
	/// </summary>
	/// <param name="localFeature">Local version of feature</param>
	/// <param name="serverFeature">Server version of feature</param>
	/// <param name="baseFeature">Base version from last sync (optional, for three-way merge)</param>
	/// <returns>Conflict information or null if no conflict</returns>
	Task<SyncConflict?> DetectConflictAsync(Feature localFeature, Feature serverFeature, Feature? baseFeature = null);

	/// <summary>
	/// Resolve a conflict using specified strategy
	/// </summary>
	/// <param name="conflict">The conflict to resolve</param>
	/// <param name="strategy">Resolution strategy to use</param>
	/// <returns>Resolved feature</returns>
	Task<Feature> ResolveConflictAsync(SyncConflict conflict, ResolutionStrategy strategy);

	/// <summary>
	/// Resolve a conflict with custom merged properties
	/// </summary>
	/// <param name="conflict">The conflict to resolve</param>
	/// <param name="mergedProperties">Manually merged properties JSON</param>
	/// <returns>Resolved feature</returns>
	Task<Feature> ResolveConflictWithCustomMergeAsync(SyncConflict conflict, string mergedProperties);

	/// <summary>
	/// Perform three-way merge between base, local, and server versions
	/// </summary>
	/// <param name="baseProperties">Properties from base version (last synced)</param>
	/// <param name="localProperties">Properties from local version</param>
	/// <param name="serverProperties">Properties from server version</param>
	/// <returns>Merge result with merged properties and remaining conflicts</returns>
	Task<MergeResult> ThreeWayMergeAsync(string baseProperties, string localProperties, string serverProperties);

	/// <summary>
	/// Resolve multiple conflicts in batch
	/// </summary>
	/// <param name="conflicts">List of conflicts to resolve</param>
	/// <param name="strategy">Resolution strategy to use for all conflicts</param>
	/// <returns>List of resolved features</returns>
	Task<List<Feature>> ResolveBatchAsync(List<SyncConflict> conflicts, ResolutionStrategy strategy);

	/// <summary>
	/// Get all unresolved conflicts from database
	/// </summary>
	/// <returns>List of unresolved conflicts</returns>
	Task<List<ConflictRecord>> GetUnresolvedConflictsAsync();

	/// <summary>
	/// Save conflict to database for later resolution
	/// </summary>
	/// <param name="conflict">Conflict to save</param>
	/// <returns>ID of saved conflict record</returns>
	Task<int> SaveConflictAsync(SyncConflict conflict);

	/// <summary>
	/// Mark conflict as resolved in database
	/// </summary>
	/// <param name="conflictId">ID of conflict record</param>
	/// <param name="strategy">Strategy used to resolve</param>
	Task MarkConflictResolvedAsync(int conflictId, ResolutionStrategy strategy);

	/// <summary>
	/// Delete resolved conflicts older than specified days
	/// </summary>
	/// <param name="days">Number of days to keep resolved conflicts</param>
	/// <returns>Number of conflicts deleted</returns>
	Task<int> CleanupResolvedConflictsAsync(int days = 30);
}

/// <summary>
/// Strategies for resolving synchronization conflicts
/// </summary>
public enum ResolutionStrategy
{
	/// <summary>
	/// Server version wins, discard local changes
	/// </summary>
	ServerWins,

	/// <summary>
	/// Local version wins, overwrite server changes
	/// </summary>
	ClientWins,

	/// <summary>
	/// Automatically merge non-conflicting changes
	/// </summary>
	AutoMerge,

	/// <summary>
	/// Manual merge required by user
	/// </summary>
	Manual
}

/// <summary>
/// Result of a three-way merge operation
/// </summary>
public record MergeResult
{
	public required bool Success { get; init; }
	public required string MergedProperties { get; init; }
	public required List<PropertyConflict> RemainingConflicts { get; init; }
	public string? ErrorMessage { get; init; }
}

/// <summary>
/// Represents a conflict in a specific property
/// </summary>
public record PropertyConflict
{
	public required string PropertyName { get; init; }
	public required object? BaseValue { get; init; }
	public required object? LocalValue { get; init; }
	public required object? ServerValue { get; init; }
	public required string Reason { get; init; }
}

/// <summary>
/// Database record for storing conflict information
/// </summary>
public class ConflictRecord
{
	public int Id { get; set; }
	public required string FeatureId { get; set; }
	public required string CollectionId { get; set; }
	public required string ConflictType { get; set; }
	public required int LocalVersion { get; set; }
	public required int ServerVersion { get; set; }
	public required long LocalModifiedAt { get; set; }
	public required long ServerModifiedAt { get; set; }
	public required string LocalProperties { get; set; }
	public required string ServerProperties { get; set; }
	public string? BaseProperties { get; set; }
	public required long DetectedAt { get; set; }
	public long? ResolvedAt { get; set; }
	public string? ResolutionStrategy { get; set; }
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
