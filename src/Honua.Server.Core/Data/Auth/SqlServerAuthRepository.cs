// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using Honua.Server.Core.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data.Auth;

internal sealed class SqlServerAuthRepository : RelationalAuthRepositoryBase
{
    private readonly string _connectionString;

    public SqlServerAuthRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILogger<SqlServerAuthRepository> logger,
        AuthMetrics? metrics,
        string connectionString,
        string? schema = null)
        : base(authOptions, logger, metrics, DatabaseRetryPolicy.CreateSqlServerRetryPipeline(), new SqlServerAuthDialect(schema))
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new SqlConnection(_connectionString);

    private sealed class SqlServerAuthDialect : IRelationalAuthDialect
    {
        private readonly IReadOnlyList<string> _statements;

        public SqlServerAuthDialect(string? schema)
        {
            var normalizedSchema = string.IsNullOrWhiteSpace(schema) ? "dbo" : schema.Trim();
            var schemaPrefix = string.IsNullOrWhiteSpace(normalizedSchema) ? string.Empty : $"[{normalizedSchema}].";

            var rolesTable = $"{schemaPrefix}[auth_roles]";
            var usersTable = $"{schemaPrefix}[auth_users]";
            var userRolesTable = $"{schemaPrefix}[auth_user_roles]";
            var auditTable = $"{schemaPrefix}[auth_credentials_audit]";
            var bootstrapTable = $"{schemaPrefix}[auth_bootstrap_state]";

            var statements = new List<string>
            {
                $"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{normalizedSchema}') EXEC('CREATE SCHEMA [{normalizedSchema}]');",
                $@"IF OBJECT_ID('{rolesTable}', 'U') IS NULL
BEGIN
    CREATE TABLE {rolesTable} (
        id NVARCHAR(128) PRIMARY KEY,
        name NVARCHAR(256) NOT NULL,
        description NVARCHAR(512) NOT NULL,
        created_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;",
                $@"IF OBJECT_ID('{usersTable}', 'U') IS NULL
BEGIN
    CREATE TABLE {usersTable} (
        id NVARCHAR(128) PRIMARY KEY,
        subject NVARCHAR(256),
        username NVARCHAR(256) UNIQUE,
        email NVARCHAR(256) UNIQUE,
        password_hash VARBINARY(MAX),
        password_salt VARBINARY(MAX),
        hash_algorithm NVARCHAR(128),
        hash_parameters NVARCHAR(256),
        is_active BIT NOT NULL DEFAULT 1,
        is_locked BIT NOT NULL DEFAULT 0,
        is_service_account BIT NOT NULL DEFAULT 0,
        failed_attempts INT NOT NULL DEFAULT 0,
        last_failed_at DATETIMEOFFSET,
        last_login_at DATETIMEOFFSET,
        password_changed_at DATETIMEOFFSET,
        password_expires_at DATETIMEOFFSET,
        created_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;",
                $@"IF OBJECT_ID('{userRolesTable}', 'U') IS NULL
BEGIN
    CREATE TABLE {userRolesTable} (
        user_id NVARCHAR(128) NOT NULL,
        role_id NVARCHAR(128) NOT NULL,
        granted_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_auth_user_roles PRIMARY KEY (user_id, role_id),
        CONSTRAINT FK_auth_user_roles_users FOREIGN KEY (user_id) REFERENCES {usersTable}(id) ON DELETE CASCADE,
        CONSTRAINT FK_auth_user_roles_roles FOREIGN KEY (role_id) REFERENCES {rolesTable}(id) ON DELETE CASCADE
    );
END;",
                $@"IF OBJECT_ID('{auditTable}', 'U') IS NULL
BEGIN
    CREATE TABLE {auditTable} (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        user_id NVARCHAR(128) NOT NULL,
        action NVARCHAR(128) NOT NULL,
        details NVARCHAR(1024),
        old_value NVARCHAR(1024),
        new_value NVARCHAR(1024),
        actor_id NVARCHAR(128),
        ip_address NVARCHAR(128),
        user_agent NVARCHAR(256),
        occurred_at DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_auth_credentials_audit_users FOREIGN KEY (user_id) REFERENCES {usersTable}(id) ON DELETE CASCADE
    );
END;",
                $@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_audit_user' AND object_id = OBJECT_ID('{auditTable}'))
BEGIN
    CREATE INDEX idx_auth_audit_user ON {auditTable}(user_id, occurred_at DESC);
END;",
                $@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_auth_audit_action' AND object_id = OBJECT_ID('{auditTable}'))
BEGIN
    CREATE INDEX idx_auth_audit_action ON {auditTable}(action, occurred_at DESC);
END;",
                $@"IF OBJECT_ID('{bootstrapTable}', 'U') IS NULL
BEGIN
    CREATE TABLE {bootstrapTable} (
        id INT PRIMARY KEY,
        completed_at DATETIMEOFFSET,
        mode NVARCHAR(128),
        metadata NVARCHAR(MAX)
    );
END;",
                $@"IF NOT EXISTS (SELECT 1 FROM {rolesTable} WHERE id = 'administrator')
BEGIN
    INSERT INTO {rolesTable} (id, name, description, created_at, updated_at)
    VALUES ('administrator', 'Administrator', 'Full system control for Honua platform operations.', SYSUTCDATETIME(), SYSUTCDATETIME());
END;",
                $@"IF NOT EXISTS (SELECT 1 FROM {rolesTable} WHERE id = 'datapublisher')
BEGIN
    INSERT INTO {rolesTable} (id, name, description, created_at, updated_at)
    VALUES ('datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.', SYSUTCDATETIME(), SYSUTCDATETIME());
END;",
                $@"IF NOT EXISTS (SELECT 1 FROM {rolesTable} WHERE id = 'viewer')
BEGIN
    INSERT INTO {rolesTable} (id, name, description, created_at, updated_at)
    VALUES ('viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.', SYSUTCDATETIME(), SYSUTCDATETIME());
END;",
                $@"IF NOT EXISTS (SELECT 1 FROM {bootstrapTable} WHERE id = 1)
BEGIN
    INSERT INTO {bootstrapTable} (id) VALUES (1);
END;"
            };

            _statements = statements;
        }

        public string ProviderName => "sqlserver";

        public IReadOnlyList<string> SchemaStatements => _statements;

        public string LimitClause(string parameterName) => $"OFFSET 0 ROWS FETCH NEXT {parameterName} ROWS ONLY";
    }
}
