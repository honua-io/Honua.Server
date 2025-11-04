// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Core.Data.Migrations;

/// <summary>
/// Manages database schema migrations for the Honua build orchestrator.
/// Supports applying, rollback, and migration status tracking.
/// </summary>
/// <remarks>
/// SECURITY: This class implements path traversal protection to prevent malicious migration paths.
/// All migration paths are validated against the approved base directory before any file operations.
/// Paths containing ".." or other traversal sequences are rejected.
/// </remarks>
public sealed class MigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly string _migrationsPath;
    private readonly string _approvedBasePath;

    /// <summary>
    /// Initializes a new instance of the MigrationRunner class.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="customMigrationsPath">Optional custom migrations path. If not provided, uses the default path.</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the migrations path is invalid or contains path traversal sequences.</exception>
    public MigrationRunner(
        string connectionString,
        ILogger<MigrationRunner> logger,
        string? customMigrationsPath = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Determine approved base path (application base directory)
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Could not determine assembly location");
        _approvedBasePath = Path.GetFullPath(Path.Combine(assemblyPath, "..", "..", "..", "..", ".."));

        // Default to embedded resources path
        var rawPath = customMigrationsPath ?? GetDefaultMigrationsPath();

        // SECURITY: Validate migration path to prevent path traversal attacks
        _migrationsPath = ValidateMigrationPath(rawPath);

        _logger.LogInformation(
            "MigrationRunner initialized with migrations path: {MigrationsPath}",
            _migrationsPath);
    }

    /// <summary>
    /// Gets all available migration files in order
    /// </summary>
    public IReadOnlyList<MigrationFile> GetAvailableMigrations()
    {
        var migrations = new List<MigrationFile>();

        if (!Directory.Exists(_migrationsPath))
        {
            _logger.LogWarning("Migrations directory not found: {Path}", _migrationsPath);
            return migrations;
        }

        var sqlFiles = Directory.GetFiles(_migrationsPath, "*.sql")
            .OrderBy(f => Path.GetFileName(f));

        foreach (var file in sqlFiles)
        {
            // SECURITY: Validate each file path to prevent path traversal
            try
            {
                ValidateFilePath(file);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(
                    ex,
                    "SECURITY: Skipping file due to path validation failure: {FilePath}",
                    file);
                continue;
            }

            var fileName = Path.GetFileName(file);
            var version = ExtractVersionFromFileName(fileName);

            if (string.IsNullOrEmpty(version))
            {
                _logger.LogWarning("Skipping file with invalid version format: {FileName}", fileName);
                continue;
            }

            var content = File.ReadAllText(file);
            var checksum = ComputeSha256Checksum(content);

            migrations.Add(new MigrationFile
            {
                Version = version,
                Name = ExtractNameFromFileName(fileName),
                FilePath = file,
                Sql = content,
                Checksum = checksum
            });
        }

        return migrations;
    }

    /// <summary>
    /// Gets applied migrations from the database
    /// </summary>
    public async Task<IReadOnlyList<AppliedMigration>> GetAppliedMigrationsAsync(
        CancellationToken cancellationToken = default)
    {
        var applied = new List<AppliedMigration>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if migrations table exists
        var tableExists = await CheckMigrationsTableExistsAsync(connection, cancellationToken);

        if (!tableExists)
        {
            _logger.LogInformation("Schema migrations table does not exist yet");
            return applied;
        }

        // Query applied migrations
        var sql = @"
            SELECT version, name, applied_at, applied_by, checksum, execution_time_ms
            FROM schema_migrations
            ORDER BY version";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            applied.Add(new AppliedMigration
            {
                Version = reader.GetString(0),
                Name = reader.GetString(1),
                AppliedAt = reader.GetDateTime(2),
                AppliedBy = reader.GetString(3),
                Checksum = reader.GetString(4),
                ExecutionTimeMs = reader.GetInt32(5)
            });
        }

        return applied;
    }

    /// <summary>
    /// Applies all pending migrations
    /// </summary>
    public async Task<MigrationResult> ApplyMigrationsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = new MigrationResult();
        var availableMigrations = GetAvailableMigrations();
        var appliedMigrations = await GetAppliedMigrationsAsync(cancellationToken);
        var appliedVersions = new HashSet<string>(appliedMigrations.Select(m => m.Version));

        _logger.LogInformation(
            "Found {AvailableCount} available migrations, {AppliedCount} already applied",
            availableMigrations.Count,
            appliedMigrations.Count);

        // Validate checksums for already applied migrations
        foreach (var applied in appliedMigrations)
        {
            var available = availableMigrations.FirstOrDefault(m => m.Version == applied.Version);
            if (available != null && available.Checksum != applied.Checksum)
            {
                var error = $"Migration {applied.Version} checksum mismatch! " +
                           $"Expected: {applied.Checksum}, Found: {available.Checksum}";
                _logger.LogError(error);
                result.Errors.Add(error);
                result.Success = false;
                return result;
            }
        }

        // Apply pending migrations
        var pendingMigrations = availableMigrations
            .Where(m => !appliedVersions.Contains(m.Version))
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            _logger.LogInformation("No pending migrations to apply");
            result.Success = true;
            return result;
        }

        _logger.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var migration in pendingMigrations)
        {
            var executionResult = await ApplyMigrationAsync(connection, migration, cancellationToken);
            result.AppliedMigrations.Add(executionResult);

            if (!executionResult.Success)
            {
                result.Success = false;
                result.Errors.Add($"Migration {migration.Version} failed: {executionResult.Error}");
                _logger.LogError("Migration {Version} failed, stopping migration process", migration.Version);
                return result;
            }
        }

        result.Success = true;
        _logger.LogInformation(
            "Successfully applied {Count} migrations",
            result.AppliedMigrations.Count);

        return result;
    }

    /// <summary>
    /// Applies a single migration within a transaction
    /// </summary>
    private async Task<MigrationExecutionResult> ApplyMigrationAsync(
        NpgsqlConnection connection,
        MigrationFile migration,
        CancellationToken cancellationToken)
    {
        var result = new MigrationExecutionResult
        {
            Version = migration.Version,
            Name = migration.Name
        };

        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Applying migration {Version}: {Name}",
                migration.Version,
                migration.Name);

            // Begin transaction
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                // Execute migration SQL
                await using var command = new NpgsqlCommand(migration.Sql, connection, transaction);
                command.CommandTimeout = 300; // 5 minutes timeout for migrations
                await command.ExecuteNonQueryAsync(cancellationToken);

                // Record migration in schema_migrations table
                var recordSql = @"
                    INSERT INTO schema_migrations (version, name, applied_at, applied_by, checksum, execution_time_ms)
                    VALUES (@version, @name, @appliedAt, @appliedBy, @checksum, @executionTimeMs)";

                await using var recordCommand = new NpgsqlCommand(recordSql, connection, transaction);
                recordCommand.Parameters.AddWithValue("version", migration.Version);
                recordCommand.Parameters.AddWithValue("name", migration.Name);
                recordCommand.Parameters.AddWithValue("appliedAt", startTime);
                recordCommand.Parameters.AddWithValue("appliedBy", Environment.UserName);
                recordCommand.Parameters.AddWithValue("checksum", migration.Checksum);
                recordCommand.Parameters.AddWithValue("executionTimeMs", (int)stopwatch.ElapsedMilliseconds);

                await recordCommand.ExecuteNonQueryAsync(cancellationToken);

                // Commit transaction
                await transaction.CommitAsync(cancellationToken);

                stopwatch.Stop();
                result.Success = true;
                result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation(
                    "Successfully applied migration {Version} in {ElapsedMs}ms",
                    migration.Version,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.Error = ex.Message;
            result.ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogError(
                ex,
                "Failed to apply migration {Version}: {Error}",
                migration.Version,
                ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Validates database connection and schema
    /// </summary>
    public async Task<bool> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = "SELECT version()";
            await using var command = new NpgsqlCommand(sql, connection);
            var version = await command.ExecuteScalarAsync(cancellationToken);

            _logger.LogInformation("Connected to PostgreSQL: {Version}", version);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Checks if a specific migration version has been applied
    /// </summary>
    public async Task<bool> IsMigrationAppliedAsync(
        string version,
        CancellationToken cancellationToken = default)
    {
        var applied = await GetAppliedMigrationsAsync(cancellationToken);
        return applied.Any(m => m.Version == version);
    }

    /// <summary>
    /// Gets the current schema version (latest applied migration)
    /// </summary>
    public async Task<string?> GetCurrentVersionAsync(CancellationToken cancellationToken = default)
    {
        var applied = await GetAppliedMigrationsAsync(cancellationToken);
        return applied.OrderByDescending(m => m.Version).FirstOrDefault()?.Version;
    }

    private async Task<bool> CheckMigrationsTableExistsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = 'schema_migrations'
            )";

        await using var command = new NpgsqlCommand(sql, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool exists && exists;
    }

    private static string ExtractVersionFromFileName(string fileName)
    {
        // Expected format: 001_InitialSchema.sql
        var parts = fileName.Split('_', 2);
        return parts.Length >= 1 ? parts[0] : string.Empty;
    }

    private static string ExtractNameFromFileName(string fileName)
    {
        // Expected format: 001_InitialSchema.sql
        var parts = fileName.Split('_', 2);
        if (parts.Length < 2) return fileName;

        var nameWithExtension = parts[1];
        return Path.GetFileNameWithoutExtension(nameWithExtension);
    }

    private static string ComputeSha256Checksum(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Validates a migration path to prevent path traversal attacks.
    /// </summary>
    /// <param name="path">The migration path to validate.</param>
    /// <returns>The validated, normalized absolute path.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path is invalid or contains traversal sequences.</exception>
    /// <remarks>
    /// SECURITY: This method implements multiple layers of path validation:
    /// 1. Rejects null or empty paths
    /// 2. Rejects paths containing ".." sequences (path traversal)
    /// 3. Resolves to absolute path using Path.GetFullPath
    /// 4. Verifies the resolved path is within the approved base directory
    /// 5. Logs all validation failures as security events
    /// </remarks>
    private string ValidateMigrationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogError("SECURITY: Migration path validation failed - path is null or empty");
            throw new InvalidOperationException("Migration path cannot be null or empty");
        }

        // SECURITY: Check for path traversal sequences before resolving
        if (path.Contains("..", StringComparison.Ordinal))
        {
            _logger.LogError(
                "SECURITY: Attempted path traversal detected in migration path: {Path}",
                path);
            throw new InvalidOperationException(
                $"Migration path contains invalid path traversal sequence '..': {path}");
        }

        // SECURITY: Resolve to absolute path to normalize and detect traversal
        string absolutePath;
        try
        {
            absolutePath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SECURITY: Failed to resolve migration path to absolute path: {Path}",
                path);
            throw new InvalidOperationException(
                $"Invalid migration path: {path}",
                ex);
        }

        // SECURITY: Verify the resolved path is within the approved base directory
        if (!absolutePath.StartsWith(_approvedBasePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "SECURITY: Migration path {AbsolutePath} is outside approved base directory {BasePath}",
                absolutePath,
                _approvedBasePath);
            throw new InvalidOperationException(
                $"Migration path must be within the approved base directory. " +
                $"Path: {absolutePath}, Base: {_approvedBasePath}");
        }

        _logger.LogDebug(
            "Migration path validated successfully: {Path} -> {AbsolutePath}",
            path,
            absolutePath);

        return absolutePath;
    }

    /// <summary>
    /// Validates an individual migration file path to prevent path traversal.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when the file path is invalid or outside the migrations directory.</exception>
    /// <remarks>
    /// SECURITY: Ensures individual migration files cannot escape the migrations directory.
    /// </remarks>
    private void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("File path cannot be null or empty");
        }

        // SECURITY: Resolve to absolute path
        string absoluteFilePath;
        try
        {
            absoluteFilePath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SECURITY: Failed to resolve file path to absolute path: {Path}",
                filePath);
            throw new InvalidOperationException(
                $"Invalid file path: {filePath}",
                ex);
        }

        // SECURITY: Verify file is within the validated migrations directory
        if (!absoluteFilePath.StartsWith(_migrationsPath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "SECURITY: File path {FilePath} is outside migrations directory {MigrationsPath}",
                absoluteFilePath,
                _migrationsPath);
            throw new InvalidOperationException(
                $"File path must be within the migrations directory. " +
                $"File: {absoluteFilePath}, Migrations: {_migrationsPath}");
        }
    }

    private static string GetDefaultMigrationsPath()
    {
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Could not determine assembly location");

        // Navigate to Migrations directory relative to assembly
        return Path.Combine(assemblyPath, "..", "..", "..", "..", "..", "src", "Honua.Server.Core", "Data", "Migrations");
    }
}

/// <summary>
/// Represents a migration file
/// </summary>
public sealed class MigrationFile
{
    public required string Version { get; init; }
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public required string Sql { get; init; }
    public required string Checksum { get; init; }
}

/// <summary>
/// Represents an applied migration from the database
/// </summary>
public sealed class AppliedMigration
{
    public required string Version { get; init; }
    public required string Name { get; init; }
    public required DateTime AppliedAt { get; init; }
    public required string AppliedBy { get; init; }
    public required string Checksum { get; init; }
    public required int ExecutionTimeMs { get; init; }
}

/// <summary>
/// Result of migration execution
/// </summary>
public sealed class MigrationResult
{
    public bool Success { get; set; }
    public List<MigrationExecutionResult> AppliedMigrations { get; } = new();
    public List<string> Errors { get; } = new();
}

/// <summary>
/// Result of a single migration execution
/// </summary>
public sealed class MigrationExecutionResult
{
    public required string Version { get; init; }
    public required string Name { get; init; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int ExecutionTimeMs { get; set; }
}
