// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// SQLite implementation of the deletion audit store.
/// </summary>
internal sealed class SqliteDeletionAuditStore : RelationalDeletionAuditStore
{
    private readonly string _connectionString;

    protected override string ProviderName => "SQLite";

    public SqliteDeletionAuditStore(
        string connectionString,
        IOptionsMonitor<SoftDeleteOptions> options,
        ILogger<SqliteDeletionAuditStore> logger)
        : base(options, logger)
    {
        _connectionString = connectionString ?? throw new System.ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new SqliteConnection(_connectionString);

    protected override string LimitClause(string paramName) => $"LIMIT {paramName}";

    protected override string GetInsertWithReturningIdSql(string baseSql)
    {
        // SQLite uses RETURNING clause similar to PostgreSQL (SQLite 3.35+)
        return baseSql + "; SELECT last_insert_rowid()";
    }
}
