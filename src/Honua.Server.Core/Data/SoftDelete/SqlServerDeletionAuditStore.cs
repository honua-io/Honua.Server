// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data.SoftDelete;

/// <summary>
/// SQL Server implementation of the deletion audit store.
/// </summary>
internal sealed class SqlServerDeletionAuditStore : RelationalDeletionAuditStore
{
    private readonly string _connectionString;

    protected override string ProviderName => "SQL Server";

    public SqlServerDeletionAuditStore(
        string connectionString,
        IOptionsMonitor<SoftDeleteOptions> options,
        ILogger<SqlServerDeletionAuditStore> logger)
        : base(options, logger)
    {
        _connectionString = connectionString ?? throw new System.ArgumentNullException(nameof(connectionString));
    }

    protected override DbConnection CreateConnection() => new SqlConnection(_connectionString);

    protected override string LimitClause(string paramName)
    {
        // SQL Server uses OFFSET/FETCH NEXT instead of LIMIT
        return $"OFFSET 0 ROWS FETCH NEXT {paramName} ROWS ONLY";
    }

    protected override string GetInsertWithReturningIdSql(string baseSql)
    {
        // SQL Server uses OUTPUT INSERTED.id or SCOPE_IDENTITY()
        return baseSql + "; SELECT SCOPE_IDENTITY()";
    }
}
