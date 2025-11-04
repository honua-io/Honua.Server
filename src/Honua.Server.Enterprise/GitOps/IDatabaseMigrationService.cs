// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.GitOps;

/// <summary>
/// Service for managing database migrations during GitOps reconciliation
/// </summary>
public interface IDatabaseMigrationService
{
    /// <summary>
    /// Apply database migrations from the datasources configuration
    /// </summary>
    /// <param name="datasourcesJson">The datasources configuration JSON</param>
    /// <param name="environment">The target environment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ApplyMigrationsAsync(string datasourcesJson, string environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate database connections from datasources configuration
    /// </summary>
    /// <param name="datasourcesJson">The datasources configuration JSON</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> ValidateConnectionsAsync(string datasourcesJson, CancellationToken cancellationToken = default);
}
