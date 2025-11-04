// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data.Common;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Honua.Server.Core.Data.Auth;

internal sealed class MySqlAuthRepository : RelationalAuthRepositoryBase
{
    private readonly string _connectionString;

    public MySqlAuthRepository(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ILogger<MySqlAuthRepository> logger,
        AuthMetrics? metrics,
        string connectionString)
        : base(authOptions, logger, metrics, DatabaseRetryPolicy.CreateMySqlRetryPipeline(), MySqlAuthDialect.Instance)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new MySqlConnection(_connectionString);

    private sealed class MySqlAuthDialect : IRelationalAuthDialect
    {
        public static readonly MySqlAuthDialect Instance = new();

        private MySqlAuthDialect()
        {
            SchemaStatements = new[]
            {
                @"CREATE TABLE IF NOT EXISTS auth_roles (
    id VARCHAR(128) PRIMARY KEY,
    name VARCHAR(256) NOT NULL,
    description VARCHAR(512) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;",
                @"CREATE TABLE IF NOT EXISTS auth_users (
    id VARCHAR(128) PRIMARY KEY,
    subject VARCHAR(256),
    username VARCHAR(256) UNIQUE,
    email VARCHAR(256) UNIQUE,
    password_hash LONGBLOB,
    password_salt LONGBLOB,
    hash_algorithm VARCHAR(128),
    hash_parameters VARCHAR(256),
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    is_locked TINYINT(1) NOT NULL DEFAULT 0,
    is_service_account TINYINT(1) NOT NULL DEFAULT 0,
    failed_attempts INT NOT NULL DEFAULT 0,
    last_failed_at DATETIME(6),
    last_login_at DATETIME(6),
    password_changed_at DATETIME(6),
    password_expires_at DATETIME(6),
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;",
                @"CREATE TABLE IF NOT EXISTS auth_user_roles (
    user_id VARCHAR(128) NOT NULL,
    role_id VARCHAR(128) NOT NULL,
    granted_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (user_id, role_id),
    CONSTRAINT FK_auth_user_roles_users FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE,
    CONSTRAINT FK_auth_user_roles_roles FOREIGN KEY (role_id) REFERENCES auth_roles(id) ON DELETE CASCADE
) ENGINE=InnoDB;",
                @"CREATE TABLE IF NOT EXISTS auth_credentials_audit (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id VARCHAR(128) NOT NULL,
    action VARCHAR(128) NOT NULL,
    details VARCHAR(1024),
    old_value VARCHAR(1024),
    new_value VARCHAR(1024),
    actor_id VARCHAR(128),
    ip_address VARCHAR(128),
    user_agent VARCHAR(256),
    occurred_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    CONSTRAINT FK_auth_credentials_audit_users FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE
) ENGINE=InnoDB;",
                @"CREATE INDEX IF NOT EXISTS idx_auth_audit_user ON auth_credentials_audit(user_id, occurred_at DESC);",
                @"CREATE INDEX IF NOT EXISTS idx_auth_audit_action ON auth_credentials_audit(action, occurred_at DESC);",
                @"CREATE TABLE IF NOT EXISTS auth_bootstrap_state (
    id INT PRIMARY KEY,
    completed_at DATETIME(6),
    mode VARCHAR(128),
    metadata TEXT
) ENGINE=InnoDB;",
                @"INSERT INTO auth_roles (id, name, description, created_at, updated_at)
SELECT 'administrator', 'Administrator', 'Full system control for Honua platform operations.', CURRENT_TIMESTAMP(6), CURRENT_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM auth_roles WHERE id = 'administrator');",
                @"INSERT INTO auth_roles (id, name, description, created_at, updated_at)
SELECT 'datapublisher', 'Data Publisher', 'Manage datasets and metadata updates within Honua.', CURRENT_TIMESTAMP(6), CURRENT_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM auth_roles WHERE id = 'datapublisher');",
                @"INSERT INTO auth_roles (id, name, description, created_at, updated_at)
SELECT 'viewer', 'Viewer', 'Read-only access to metadata inspection and observability dashboards.', CURRENT_TIMESTAMP(6), CURRENT_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM auth_roles WHERE id = 'viewer');",
                @"INSERT IGNORE INTO auth_bootstrap_state (id) VALUES (1);"
            };
        }

        public string ProviderName => "mysql";

        public IReadOnlyList<string> SchemaStatements { get; }

        public string LimitClause(string parameterName) => $"LIMIT {parameterName}";
    }
}
