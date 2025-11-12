// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Services;

namespace HonuaField.Data;

/// <summary>
/// Database service implementation
/// Manages HonuaFieldDatabase lifecycle and provides access to database
/// </summary>
public class DatabaseService : IDatabaseService
{
	private HonuaFieldDatabase? _database;
	private readonly string _databasePath;

	public DatabaseService()
	{
		// Get app data directory for database file
		var appDataPath = FileSystem.AppDataDirectory;
		_databasePath = Path.Combine(appDataPath, "honuafield.db");

		System.Diagnostics.Debug.WriteLine($"Database path: {_databasePath}");
	}

	/// <summary>
	/// Initialize database and create tables
	/// </summary>
	public async Task InitializeAsync()
	{
		if (_database != null)
			return; // Already initialized

		try
		{
			_database = new HonuaFieldDatabase(_databasePath);
			await _database.InitializeAsync();

			System.Diagnostics.Debug.WriteLine("DatabaseService initialized successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing DatabaseService: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Get database instance
	/// </summary>
	public HonuaFieldDatabase GetDatabase()
	{
		if (_database == null)
		{
			throw new InvalidOperationException(
				"Database not initialized. Call InitializeAsync() first.");
		}

		return _database;
	}
}
