// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution;

public class DatabaseExecutionPlugin
{
    private readonly IPluginExecutionContext _context;
    private readonly DatabaseService _databaseService;

    public DatabaseExecutionPlugin(IPluginExecutionContext context)
    {
        _context = context;
        _databaseService = new DatabaseService();
    }

    [KernelFunction, Description("Execute SQL against a database")]
    public async Task<string> ExecuteSQL(
        [Description("Connection string or container name")] string connection,
        [Description("SQL to execute")] string sql,
        [Description("Description of what this SQL does")] string description,
        [Description("Database type: postgres, mysql, sqlserver, sqlite")] string dbType = "postgres",
        [Description("Database user (for container mode)")] string user = "postgres",
        [Description("Database password (for container mode)")] string password = "postgres",
        [Description("Database name (for container mode)")] string? database = null)
    {
        // Validate inputs FIRST to prevent command injection (even in dry-run mode)
        try
        {
            CommandArgumentValidator.ValidateSQL(sql, nameof(sql));

            if (connection.StartsWith("postgres://") || connection.Contains("@"))
            {
                // Connection string
                CommandArgumentValidator.ValidateConnectionString(connection, nameof(connection));
            }
            else
            {
                // Container name
                CommandArgumentValidator.ValidateIdentifier(connection, nameof(connection));
            }
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Database", "ExecuteSQL", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        if (_context.DryRun)
        {
            _context.RecordAction("Database", "ExecuteSQL", $"[DRY-RUN] Would execute: {description}\nSQL: {sql}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, description, sql });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Execute SQL",
                $"{description}\n\nSQL:\n{sql}",
                new[] { connection });

            if (!approved)
            {
                _context.RecordAction("Database", "ExecuteSQL", "User rejected SQL execution", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            string result;
            if (connection.StartsWith("postgres://") || connection.Contains("@") || connection.Contains("="))
            {
                // Connection string - use DatabaseService for safe execution with proper driver
                // Determine if this is a SELECT query or DDL
                var normalized = sql.Trim().ToUpperInvariant();
                if (normalized.StartsWith("SELECT"))
                {
                    result = await _databaseService.ExecuteQueryAsync(connection, sql, dbType);
                }
                else
                {
                    result = await _databaseService.ExecuteDdlAsync(connection, sql, dbType);
                }
            }
            else
            {
                // Container name - fallback to Docker execution for compatibility
                // Still uses ArgumentList for protection against command injection
                result = await ExecuteInDockerAsync(connection, sql, dbType, user, password, database);
            }

            _context.RecordAction("Database", "ExecuteSQL", $"Executed: {description}", true);

            return JsonSerializer.Serialize(new { success = true, description, output = result });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Database", "ExecuteSQL", $"Failed to execute: {description}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Create a PostGIS database")]
    public async Task<string> CreatePostGISDatabase(
        [Description("Container name or admin connection string (to postgres database)")] string connectionOrContainer,
        [Description("Database name to create")] string databaseName,
        [Description("PostgreSQL user (only for container mode)")] string user = "postgres",
        [Description("PostgreSQL password (only for container mode)")] string password = "postgres")
    {
        // Validate inputs FIRST to prevent command injection (even in dry-run mode)
        try
        {
            CommandArgumentValidator.ValidateDatabaseName(databaseName, nameof(databaseName));

            // Validate connection or container
            if (connectionOrContainer.Contains("=") || connectionOrContainer.Contains("://"))
            {
                // Connection string
                CommandArgumentValidator.ValidateConnectionString(connectionOrContainer, nameof(connectionOrContainer));
            }
            else
            {
                // Container name
                CommandArgumentValidator.ValidateIdentifier(connectionOrContainer, nameof(connectionOrContainer));
                CommandArgumentValidator.ValidateIdentifier(user, nameof(user));
            }
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Database", "CreatePostGIS", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var isConnectionString = connectionOrContainer.Contains("=") || connectionOrContainer.Contains("://");
        var steps = new[]
        {
            $"CREATE DATABASE {databaseName};",
            isConnectionString ? $"Connect to {databaseName}" : $"\\c {databaseName}",
            "CREATE EXTENSION IF NOT EXISTS postgis;",
            "CREATE EXTENSION IF NOT EXISTS postgis_topology;",
            "SELECT PostGIS_version();"
        };

        if (_context.DryRun)
        {
            var target = isConnectionString ? "database server" : $"container {connectionOrContainer}";
            _context.RecordAction("Database", "CreatePostGIS", $"[DRY-RUN] Would create PostGIS database {databaseName} in {target}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, connection = connectionOrContainer, databaseName, steps });
        }

        if (_context.RequireApproval)
        {
            var target = isConnectionString ? "database server" : $"container '{connectionOrContainer}'";
            var approved = await _context.RequestApprovalAsync(
                "Create PostGIS Database",
                $"Create database '{databaseName}' with PostGIS extensions in {target}",
                new[] { connectionOrContainer, databaseName });

            if (!approved)
            {
                _context.RecordAction("Database", "CreatePostGIS", "User rejected PostGIS database creation", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            string postgisVersion;
            string finalConnectionString;

            if (isConnectionString)
            {
                // Use DatabaseService for safe, library-based execution
                var result = await _databaseService.CreatePostGisDatabaseAsync(connectionOrContainer, databaseName);

                // Extract version from result
                postgisVersion = result.Contains("PostGIS version:")
                    ? result.Substring(result.IndexOf("PostGIS version:") + 16).Trim()
                    : "Unknown";

                // Build connection string for new database
                var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionOrContainer)
                {
                    Database = databaseName
                };
                finalConnectionString = builder.ToString();
            }
            else
            {
                // Container mode - use Docker execution for backward compatibility
                // Create database
                await ExecutePsqlInDockerAsync(connectionOrContainer, $"CREATE DATABASE {databaseName};", user, null, password);

                // Add PostGIS extensions
                await ExecutePsqlInDockerAsync(connectionOrContainer,
                    "CREATE EXTENSION IF NOT EXISTS postgis; CREATE EXTENSION IF NOT EXISTS postgis_topology;",
                    user,
                    databaseName,
                    password);

                // Get PostGIS version
                postgisVersion = await ExecutePsqlInDockerAsync(connectionOrContainer,
                    "SELECT PostGIS_version();",
                    user,
                    databaseName,
                    password);

                finalConnectionString = $"Host={connectionOrContainer};Database={databaseName};Username={user};Password={password}";
            }

            var target = isConnectionString ? "database server" : $"container {connectionOrContainer}";
            _context.RecordAction("Database", "CreatePostGIS", $"Created PostGIS database {databaseName} in {target}", true);

            return JsonSerializer.Serialize(new
            {
                success = true,
                databaseName,
                connectionString = finalConnectionString,
                postgisVersion = postgisVersion.Trim()
            });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Database", "CreatePostGIS", $"Failed to create PostGIS database {databaseName}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Execute database command in Docker container (safe from command injection using ArgumentList)
    /// Supports PostgreSQL, MySQL, SQL Server, and SQLite
    /// Used for backward compatibility when working with containers
    /// </summary>
    private async Task<string> ExecuteInDockerAsync(string containerName, string sql, string dbType = "postgres", string user = "postgres", string password = "postgres", string? database = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Set password environment variable based on database type
        var normalizedDbType = dbType.ToLowerInvariant();
        if (normalizedDbType == "postgres" || normalizedDbType == "postgresql" || normalizedDbType == "pg")
        {
            psi.EnvironmentVariables["PGPASSWORD"] = password;
        }
        else if (normalizedDbType == "mysql" || normalizedDbType == "mariadb")
        {
            psi.EnvironmentVariables["MYSQL_PWD"] = password;
        }
        // SQL Server doesn't use environment variables for password, it's passed via command line
        // SQLite doesn't use authentication

        // Use ArgumentList to prevent command injection
        psi.ArgumentList.Add("exec");

        // Add environment variable flag if needed (for MySQL/PostgreSQL)
        if (normalizedDbType == "postgres" || normalizedDbType == "postgresql" || normalizedDbType == "pg" ||
            normalizedDbType == "mysql" || normalizedDbType == "mariadb")
        {
            psi.ArgumentList.Add("-e");
            if (normalizedDbType == "postgres" || normalizedDbType == "postgresql" || normalizedDbType == "pg")
            {
                psi.ArgumentList.Add($"PGPASSWORD={password}");
            }
            else
            {
                psi.ArgumentList.Add($"MYSQL_PWD={password}");
            }
        }

        psi.ArgumentList.Add(containerName);

        // Add database-specific client command and arguments
        if (normalizedDbType == "postgres" || normalizedDbType == "postgresql" || normalizedDbType == "pg")
        {
            psi.ArgumentList.Add("psql");
            psi.ArgumentList.Add("-U");
            psi.ArgumentList.Add(user);

            if (!database.IsNullOrEmpty())
            {
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(database);
            }

            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(sql);
        }
        else if (normalizedDbType == "mysql" || normalizedDbType == "mariadb")
        {
            psi.ArgumentList.Add("mysql");
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(user);

            if (!database.IsNullOrEmpty())
            {
                psi.ArgumentList.Add("-D");
                psi.ArgumentList.Add(database);
            }

            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add(sql);
        }
        else if (normalizedDbType == "sqlserver" || normalizedDbType == "mssql" || normalizedDbType == "sql")
        {
            psi.ArgumentList.Add("/opt/mssql-tools/bin/sqlcmd");
            psi.ArgumentList.Add("-U");
            psi.ArgumentList.Add(user);
            psi.ArgumentList.Add("-P");
            psi.ArgumentList.Add(password);

            if (!database.IsNullOrEmpty())
            {
                psi.ArgumentList.Add("-d");
                psi.ArgumentList.Add(database);
            }

            psi.ArgumentList.Add("-Q");
            psi.ArgumentList.Add(sql);
        }
        else if (normalizedDbType == "sqlite" || normalizedDbType == "sqlite3")
        {
            psi.ArgumentList.Add("sqlite3");

            if (!database.IsNullOrEmpty())
            {
                psi.ArgumentList.Add(database);
            }
            else
            {
                psi.ArgumentList.Add(":memory:");
            }

            psi.ArgumentList.Add(sql);
        }
        else
        {
            throw new ArgumentException($"Unsupported database type: {dbType}. Supported types: postgres, mysql, sqlserver, sqlite");
        }

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start docker exec process");

        // Read stdout and stderr concurrently to prevent deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        var output = await stdoutTask;
        var error = await stderrTask;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"docker exec {normalizedDbType} command failed: {error}");

        return output;
    }

    /// <summary>
    /// Execute psql command in Docker container (backward compatibility wrapper)
    /// </summary>
    private async Task<string> ExecutePsqlInDockerAsync(string containerName, string sql, string user = "postgres", string? database = null, string password = "postgres")
    {
        return await ExecuteInDockerAsync(containerName, sql, "postgres", user, password, database);
    }
}
