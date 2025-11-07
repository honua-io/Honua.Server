using SQLite;
using HonuaField.Models;

namespace HonuaField.Data;

/// <summary>
/// SQLite database context for Honua Field mobile app
/// Manages database connection, tables, and schema migrations
/// </summary>
public class HonuaFieldDatabase
{
	private readonly SQLiteAsyncConnection _database;
	private readonly string _databasePath;
	private bool _isInitialized = false;

	public HonuaFieldDatabase(string databasePath)
	{
		_databasePath = databasePath;
		_database = new SQLiteAsyncConnection(databasePath,
			SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
	}

	/// <summary>
	/// Initialize database and create tables if they don't exist
	/// </summary>
	public async Task InitializeAsync()
	{
		if (_isInitialized)
			return;

		try
		{
			// Create tables
			await _database.CreateTableAsync<Feature>();
			await _database.CreateTableAsync<Collection>();
			await _database.CreateTableAsync<Attachment>();
			await _database.CreateTableAsync<Change>();
			await _database.CreateTableAsync<Map>();
			await _database.CreateTableAsync<GpsTrack>();
			await _database.CreateTableAsync<GpsTrackPoint>();

			// Create indexes for performance
			await CreateIndexesAsync();

			// Run migrations if needed
			await RunMigrationsAsync();

			_isInitialized = true;

			System.Diagnostics.Debug.WriteLine($"Database initialized at: {_databasePath}");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing database: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Create database indexes for query performance
	/// </summary>
	private async Task CreateIndexesAsync()
	{
		try
		{
			// Index on collection_id for fast feature lookups by collection
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_features_collection_id ON features(collection_id)");

			// Index on sync_status for filtering synced/pending features
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_features_sync_status ON features(sync_status)");

			// Index on feature_id in attachments for fast lookup
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_attachments_feature_id ON attachments(feature_id)");

			// Index on feature_id in changes for sync queue
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_changes_feature_id ON changes(feature_id)");

			// Index on synced status in changes for filtering
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_changes_synced ON changes(synced)");

			// Index on track_id in GPS track points for fast lookup
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_gps_track_points_track_id ON gps_track_points(track_id)");

			// Index on timestamp in GPS track points for temporal queries
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_gps_track_points_timestamp ON gps_track_points(timestamp)");

			// Index on status in GPS tracks for filtering
			await _database.ExecuteAsync(
				"CREATE INDEX IF NOT EXISTS idx_gps_tracks_status ON gps_tracks(status)");

			// Note: Spatial R-tree indexes for geometry will be created separately
			// in the repository layer using NetTopologySuite extensions

			System.Diagnostics.Debug.WriteLine("Database indexes created successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error creating indexes: {ex.Message}");
			// Don't throw - indexes are optional for functionality
		}
	}

	/// <summary>
	/// Run database schema migrations
	/// Handles schema version upgrades
	/// </summary>
	private async Task RunMigrationsAsync()
	{
		try
		{
			// Get current schema version
			var version = await GetSchemaVersionAsync();

			System.Diagnostics.Debug.WriteLine($"Current database schema version: {version}");

			// Run migrations in sequence
			if (version < 1)
			{
				// Initial schema is version 1 - already created by CreateTable calls
				await SetSchemaVersionAsync(1);
			}

			// Future migrations will be added here:
			// if (version < 2) { await MigrateTo2Async(); }
			// if (version < 3) { await MigrateTo3Async(); }
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error running migrations: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Get current schema version from database
	/// </summary>
	private async Task<int> GetSchemaVersionAsync()
	{
		try
		{
			// Create schema_info table if it doesn't exist
			await _database.ExecuteAsync(
				"CREATE TABLE IF NOT EXISTS schema_info (version INTEGER PRIMARY KEY)");

			var version = await _database.ExecuteScalarAsync<int?>(
				"SELECT version FROM schema_info LIMIT 1");

			return version ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	/// <summary>
	/// Set schema version in database
	/// </summary>
	private async Task SetSchemaVersionAsync(int version)
	{
		await _database.ExecuteAsync("DELETE FROM schema_info");
		await _database.ExecuteAsync(
			"INSERT INTO schema_info (version) VALUES (?)", version);

		System.Diagnostics.Debug.WriteLine($"Schema version updated to: {version}");
	}

	/// <summary>
	/// Get SQLite connection for direct queries
	/// </summary>
	public SQLiteAsyncConnection GetConnection()
	{
		if (!_isInitialized)
		{
			throw new InvalidOperationException(
				"Database not initialized. Call InitializeAsync() first.");
		}

		return _database;
	}

	/// <summary>
	/// Close database connection
	/// </summary>
	public async Task CloseAsync()
	{
		await _database.CloseAsync();
		_isInitialized = false;

		System.Diagnostics.Debug.WriteLine("Database connection closed");
	}

	/// <summary>
	/// Delete all data from all tables (for testing or reset)
	/// </summary>
	public async Task ClearAllDataAsync()
	{
		await _database.DeleteAllAsync<Change>();
		await _database.DeleteAllAsync<Attachment>();
		await _database.DeleteAllAsync<Feature>();
		await _database.DeleteAllAsync<Collection>();
		await _database.DeleteAllAsync<Map>();
		await _database.DeleteAllAsync<GpsTrackPoint>();
		await _database.DeleteAllAsync<GpsTrack>();

		System.Diagnostics.Debug.WriteLine("All database data cleared");
	}

	/// <summary>
	/// Get database statistics
	/// </summary>
	public async Task<DatabaseStats> GetStatsAsync()
	{
		var featureCount = await _database.Table<Feature>().CountAsync();
		var collectionCount = await _database.Table<Collection>().CountAsync();
		var attachmentCount = await _database.Table<Attachment>().CountAsync();
		var pendingChanges = await _database.Table<Change>()
			.Where(c => c.Synced == 0)
			.CountAsync();
		var gpsTrackCount = await _database.Table<GpsTrack>().CountAsync();
		var gpsTrackPointCount = await _database.Table<GpsTrackPoint>().CountAsync();

		return new DatabaseStats
		{
			FeatureCount = featureCount,
			CollectionCount = collectionCount,
			AttachmentCount = attachmentCount,
			PendingChanges = pendingChanges,
			GpsTrackCount = gpsTrackCount,
			GpsTrackPointCount = gpsTrackPointCount
		};
	}
}

/// <summary>
/// Database statistics record
/// </summary>
public record DatabaseStats
{
	public int FeatureCount { get; init; }
	public int CollectionCount { get; init; }
	public int AttachmentCount { get; init; }
	public int PendingChanges { get; init; }
	public int GpsTrackCount { get; init; }
	public int GpsTrackPointCount { get; init; }
}
