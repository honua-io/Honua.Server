// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Core.Data.Auth;

internal sealed class PostgresAuthRepository : RelationalAuthRepositoryBase
{
    private readonly string _connectionString;

    public PostgresAuthRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILogger<PostgresAuthRepository> logger,
        AuthMetrics? metrics,
        string connectionString,
        string? schema = null)
        : base(authOptions, logger, metrics, DatabaseRetryPolicy.CreatePostgresRetryPipeline(), new PostgresAuthDialect(schema))
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    private sealed class PostgresAuthDialect : IRelationalAuthDialect
    {
        private readonly string _schemaPrefix;
        private readonly IReadOnlyList<string> _statements;

        public PostgresAuthDialect(string? schema)
        {
            var normalizedSchema = string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim();
            _schemaPrefix = string.IsNullOrWhiteSpace(normalizedSchema) ? string.Empty : $"\"{normalizedSchema}\".";

            var rolesTable = $"{_schemaPrefix}\"auth_roles\"";
            var usersTable = $"{_schemaPrefix}\"auth_users\"";
            var userRolesTable = $"{_schemaPrefix}\"auth_user_roles\"";
            var auditTable = $"{_schemaPrefix}\"auth_credentials_audit\"";
            var bootstrapTable = $"{_schemaPrefix}\"auth_bootstrap_state\"";

            var statements = new List<string>
            {
                $"CREATE SCHEMA IF NOT EXISTS \"{normalizedSchema}\";",
                $@"CREATE TABLE IF NOT EXISTS {rolesTable} (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);",
                $@"CREATE TABLE IF NOT EXISTS {usersTable} (
    id TEXT PRIMARY KEY,
    subject TEXT,
    username TEXT UNIQUE,
    email TEXT UNIQUE,
    password_hash BYTEA,
    password_salt BYTEA,
    hash_algorithm TEXT,
    hash_parameters TEXT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    is_locked BOOLEAN NOT NULL DEFAULT FALSE,
    is_service_account BOOLEAN NOT NULL DEFAULT FALSE,
    failed_attempts INTEGER NOT NULL DEFAULT 0,
    last_failed_at TIMESTAMPTZ,
    last_login_at TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    password_expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);",
                $@"CREATE TABLE IF NOT EXISTS {userRolesTable} (
    user_id TEXT NOT NULL,
    role_id TEXT NOT NULL,
    granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (user_id, role_id),
    FOREIGN KEY (user_id) REFERENCES {usersTable}(id) ON DELETE CASCADE,
    FOREIGN KEY (role_id) REFERENCES {rolesTable}(id) ON DELETE CASCADE
);",
                $@"CREATE TABLE IF NOT EXISTS {auditTable} (
    id BIGSERIAL PRIMARY KEY,
    user_id TEXT NOT NULL,
    action TEXT NOT NULL,
    details TEXT,
    old_value TEXT,
    new_value TEXT,
    actor_id TEXT,
    ip_address TEXT,
    user_agent TEXT,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    FOREIGN KEY (user_id) REFERENCES {usersTable}(id) ON DELETE CASCADE
);",
                $@"CREATE INDEX IF NOT EXISTS idx_auth_audit_user ON {auditTable}(user_id, occurred_at DESC);",
                $@"CREATE INDEX IF NOT EXISTS idx_auth_audit_action ON {auditTable}(action, occurred_at DESC);",
                $@"CREATE TABLE IF NOT EXISTS {bootstrapTable} (
    id INTEGER PRIMARY KEY,
    completed_at TIMESTAMPTZ,
    mode TEXT,
    metadata TEXT
);",
                $@"INSERT INTO {rolesTable} (id, name, description, created_at, updated_at)
VALUES ('administrator', 'Administrator', 'Full system control for Honua platform operations.', NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description, updated_at = NOW();",
                $@"INSERT INTO {rolesTable} (id, name, description, created_at, updated_at)
VALUES ('datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.', NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description, updated_at = NOW();",
                $@"INSERT INTO {rolesTable} (id, name, description, created_at, updated_at)
VALUES ('viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.', NOW(), NOW())
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description, updated_at = NOW();",
                $@"INSERT INTO {bootstrapTable} (id) VALUES (1)
ON CONFLICT (id) DO NOTHING;"
            };

            _statements = statements;
        }

        public string ProviderName => "postgres";

        public IReadOnlyList<string> SchemaStatements => _statements;

        public string LimitClause(string parameterName) => $"LIMIT {parameterName}";
    }

    public string ConnectionString => _connectionString;
}
