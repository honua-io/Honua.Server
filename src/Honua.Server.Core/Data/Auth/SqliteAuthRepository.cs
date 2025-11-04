// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data.Auth;

internal sealed class SqliteAuthRepository : RelationalAuthRepositoryBase
{
    private readonly string _basePath;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _authOptions;
    private readonly DataAccessOptions _dataAccessOptions;
    private string? _connectionString;

    public SqliteAuthRepository(
        string basePath,
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILogger<SqliteAuthRepository> logger,
        AuthMetrics? metrics = null,
        IOptions<DataAccessOptions>? dataAccessOptions = null)
        : base(authOptions, logger, metrics, DatabaseRetryPolicy.CreateSqliteRetryPipeline(), SqliteAuthDialect.Instance)
    {
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _authOptions = authOptions ?? throw new ArgumentNullException(nameof(authOptions));
        _dataAccessOptions = dataAccessOptions?.Value ?? new DataAccessOptions();
    }

    protected override DbConnection CreateConnection()
    {
        var storePath = ResolveStorePath();
        _connectionString ??= BuildConnectionString(storePath);
        return new SqliteConnection(_connectionString);
    }

    protected override async Task ConfigureConnectionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection is not SqliteConnection sqlite)
        {
            return;
        }

        await using (var pragma = sqlite.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (_dataAccessOptions.Sqlite.EnableWalMode)
        {
            await using var pragmaWal = sqlite.CreateCommand();
            pragmaWal.CommandText = "PRAGMA journal_mode = WAL;";
            await pragmaWal.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private string ResolveStorePath()
    {
        var configured = _authOptions.CurrentValue.Local.StorePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = Path.Combine("data", "auth", "auth.db");
        }

        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        if (string.IsNullOrWhiteSpace(_basePath))
        {
            return Path.GetFullPath(configured);
        }

        return Path.GetFullPath(Path.Combine(_basePath, configured));
    }

    private string BuildConnectionString(string storePath)
    {
        var directory = Path.GetDirectoryName(storePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new SqliteConnectionStringBuilder
        {
            DataSource = storePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = _dataAccessOptions.Sqlite.CacheMode switch
            {
                "Private" => SqliteCacheMode.Private,
                "Shared" => SqliteCacheMode.Shared,
                _ => SqliteCacheMode.Default
            },
            Pooling = _dataAccessOptions.Sqlite.Pooling,
            DefaultTimeout = _dataAccessOptions.Sqlite.DefaultTimeout
        }.ConnectionString;
    }

    private sealed class SqliteAuthDialect : IRelationalAuthDialect
    {
        public static readonly SqliteAuthDialect Instance = new();

        public string ProviderName => "sqlite";

        public IReadOnlyList<string> SchemaStatements { get; } = new[]
        {
            @"CREATE TABLE IF NOT EXISTS auth_roles (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);",
            @"CREATE TABLE IF NOT EXISTS auth_users (
    id TEXT PRIMARY KEY,
    subject TEXT,
    username TEXT,
    email TEXT,
    password_hash BLOB,
    password_salt BLOB,
    hash_algorithm TEXT,
    hash_parameters TEXT,
    is_active INTEGER NOT NULL DEFAULT 1,
    is_locked INTEGER NOT NULL DEFAULT 0,
    is_service_account INTEGER NOT NULL DEFAULT 0,
    failed_attempts INTEGER NOT NULL DEFAULT 0,
    last_failed_at TEXT,
    last_login_at TEXT,
    password_changed_at TEXT,
    password_expires_at TEXT,
    created_at TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at TEXT NOT NULL DEFAULT (datetime('now')),
    UNIQUE(username),
    UNIQUE(email),
    UNIQUE(subject)
);",
            @"CREATE TABLE IF NOT EXISTS auth_user_roles (
    user_id TEXT NOT NULL,
    role_id TEXT NOT NULL,
    granted_at TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES auth_roles(id) ON DELETE CASCADE
);",
            @"CREATE TABLE IF NOT EXISTS auth_credentials_audit (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id TEXT NOT NULL,
    action TEXT NOT NULL,
    details TEXT,
    old_value TEXT,
    new_value TEXT,
    actor_id TEXT,
    ip_address TEXT,
    user_agent TEXT,
    occurred_at TEXT NOT NULL DEFAULT (datetime('now')),
    FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE
);",
            @"CREATE INDEX IF NOT EXISTS idx_audit_user_id ON auth_credentials_audit(user_id, occurred_at DESC);",
            @"CREATE INDEX IF NOT EXISTS idx_audit_action ON auth_credentials_audit(action, occurred_at DESC);",
            @"CREATE TABLE IF NOT EXISTS auth_bootstrap_state (
    id INTEGER PRIMARY KEY,
    completed_at TEXT,
    mode TEXT,
    metadata TEXT
);",
            @"INSERT INTO auth_roles (id, name, description, created_at, updated_at)
SELECT 'administrator', 'Administrator', 'Full system control for Honua platform operations.', datetime('now'), datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM auth_roles WHERE id = 'administrator');",
            @"INSERT INTO auth_roles (id, name, description, created_at, updated_at)
SELECT 'datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.', datetime('now'), datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM auth_roles WHERE id = 'datapublisher');",
            @"INSERT INTO auth_roles (id, name, description, created_at, updated_at)
SELECT 'viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.', datetime('now'), datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM auth_roles WHERE id = 'viewer');",
            @"INSERT OR IGNORE INTO auth_bootstrap_state (id) VALUES (1);"
        };

        public string LimitClause(string parameterName) => $"LIMIT {parameterName}";
    }
}
