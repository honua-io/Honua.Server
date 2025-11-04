// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Data;

/// <summary>
/// Generic base class for relational database transactions.
/// Provides a single, consistent implementation of transaction management across all ADO.NET providers.
/// </summary>
/// <typeparam name="TConnection">The ADO.NET connection type (e.g., NpgsqlConnection, MySqlConnection).</typeparam>
/// <typeparam name="TTransaction">The ADO.NET transaction type (e.g., NpgsqlTransaction, MySqlTransaction).</typeparam>
/// <remarks>
/// SECURITY CRITICAL: This class ensures connections are always disposed in all code paths,
/// preventing connection pool exhaustion which can cause application-wide failures.
/// </remarks>
public sealed class RelationalDataStoreTransaction<TConnection, TTransaction> : IDataStoreTransaction
    where TConnection : DbConnection
    where TTransaction : DbTransaction
{
    private readonly TConnection _connection;
    private readonly TTransaction _transaction;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RelationalDataStoreTransaction{TConnection, TTransaction}"/> class.
    /// </summary>
    /// <param name="connection">The database connection. Must not be null.</param>
    /// <param name="transaction">The database transaction. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when connection or transaction is null.</exception>
    public RelationalDataStoreTransaction(TConnection connection, TTransaction transaction)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Gets the underlying database connection.
    /// </summary>
    /// <remarks>
    /// This property is used by CRUD operations that need to execute commands within the transaction.
    /// </remarks>
    public TConnection Connection => _connection;

    /// <summary>
    /// Gets the underlying database transaction.
    /// </summary>
    /// <remarks>
    /// This property is used to associate commands with this transaction.
    /// </remarks>
    public TTransaction Transaction => _transaction;

    /// <summary>
    /// Commits the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the transaction has been disposed.</exception>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Rolls back the transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the transaction has been disposed.</exception>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the underlying transaction object for provider-specific operations.
    /// </summary>
    /// <returns>The underlying ADO.NET transaction instance.</returns>
    /// <exception cref="ObjectDisposedException">Thrown if the transaction has been disposed.</exception>
    public object GetUnderlyingTransaction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _transaction;
    }

    /// <summary>
    /// Disposes the transaction and connection asynchronously.
    /// </summary>
    /// <remarks>
    /// CRITICAL: This method ensures both the transaction AND connection are disposed,
    /// preventing connection pool leaks that would eventually exhaust the pool.
    /// The order matters: transaction first, then connection.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Dispose transaction first (it may need to rollback uncommitted work)
        await _transaction.DisposeAsync().ConfigureAwait(false);

        // Then dispose connection (returns it to the pool)
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
