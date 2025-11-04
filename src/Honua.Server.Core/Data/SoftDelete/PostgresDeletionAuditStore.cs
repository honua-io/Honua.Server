// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// PostgreSQL implementation of the deletion audit store.
/// </summary>
internal sealed class PostgresDeletionAuditStore : RelationalDeletionAuditStore
{
    private readonly string _connectionString;

    protected override string ProviderName => "PostgreSQL";

    public PostgresDeletionAuditStore(
        string connectionString,
        IOptionsMonitor<SoftDeleteOptions> options,
        ILogger<PostgresDeletionAuditStore> logger)
        : base(options, logger)
    {
        _connectionString = connectionString ?? throw new System.ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    protected override string LimitClause(string paramName) => $"LIMIT {paramName}";

    protected override string GetInsertWithReturningIdSql(string baseSql)
    {
        return baseSql + " RETURNING id";
    }
}
