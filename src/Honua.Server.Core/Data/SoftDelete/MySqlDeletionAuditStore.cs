// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// MySQL implementation of the deletion audit store.
/// </summary>
internal sealed class MySqlDeletionAuditStore : RelationalDeletionAuditStore
{
    private readonly string _connectionString;

    protected override string ProviderName => "MySQL";

    public MySqlDeletionAuditStore(
        string connectionString,
        IOptionsMonitor<SoftDeleteOptions> options,
        ILogger<MySqlDeletionAuditStore> logger)
        : base(options, logger)
    {
        _connectionString = connectionString ?? throw new System.ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new MySqlConnection(_connectionString);

    protected override string LimitClause(string paramName) => $"LIMIT {paramName}";

    protected override string GetInsertWithReturningIdSql(string baseSql)
    {
        // MySQL uses LAST_INSERT_ID()
        return baseSql + "; SELECT LAST_INSERT_ID()";
    }
}
