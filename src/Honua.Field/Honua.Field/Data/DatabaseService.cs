// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Services;
using Microsoft.Extensions.Logging;

namespace HonuaField.Data;

/// <summary>
/// Database service implementation
/// Manages HonuaFieldDatabase lifecycle and provides access to database
/// </summary>
public class DatabaseService : IDatabaseService
{
	private HonuaFieldDatabase? _database;
	private readonly string _databasePath;
	private readonly ILogger<DatabaseService> _logger;
	private readonly ILogger<HonuaFieldDatabase> _databaseLogger;

	public DatabaseService(ILogger<DatabaseService> logger, ILogger<HonuaFieldDatabase> databaseLogger)
	{
		_logger = logger;
		_databaseLogger = databaseLogger;

		// Get app data directory for database file
		var appDataPath = FileSystem.AppDataDirectory;
		_databasePath = Path.Combine(appDataPath, "honuafield.db");

		_logger.LogInformation("Database path set to: {DatabasePath}", _databasePath);
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
			_database = new HonuaFieldDatabase(_databasePath, _databaseLogger);
			await _database.InitializeAsync();

			_logger.LogInformation("DatabaseService initialized successfully");
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error initializing DatabaseService");
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
