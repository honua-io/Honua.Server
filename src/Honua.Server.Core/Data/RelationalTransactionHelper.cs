// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Polly;

namespace Honua.Server.Core.Data;

/// <summary>
/// Helper methods for working with relational database transactions in CRUD operations.
/// </summary>
/// <remarks>
/// This class provides utilities to extract typed connections and transactions from IDataStoreTransaction,
/// and to manage connection lifecycle in CRUD operations that support optional transactions.
/// </remarks>
public static class RelationalTransactionHelper
{
    /// <summary>
    /// Executes an operation with proper connection and transaction handling.
    /// </summary>
    /// <typeparam name="TConnection">The ADO.NET connection type.</typeparam>
    /// <typeparam name="TTransaction">The ADO.NET transaction type.</typeparam>
    /// <typeparam name="TResult">The result type of the operation.</typeparam>
    /// <param name="transaction">The optional transaction wrapper.</param>
    /// <param name="dataSource">The data source definition (used if no transaction is provided).</param>
    /// <param name="createConnectionAsync">Function to create a new connection.</param>
    /// <param name="openConnectionAsync">Function to open a connection with retry logic.</param>
    /// <param name="operationAsync">The operation to execute.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    /// <remarks>
    /// CRITICAL: This method ensures connections are ALWAYS disposed when created by the operation.
    /// If the operation is part of an existing transaction, the connection is NOT disposed here
    /// (it will be disposed when the transaction is disposed).
    /// </remarks>
    public static async Task<TResult> ExecuteWithConnectionAsync<TConnection, TTransaction, TResult>(
        IDataStoreTransaction? transaction,
        DataSourceDefinition dataSource,
        Func<DataSourceDefinition, CancellationToken, Task<TConnection>> createConnectionAsync,
        Func<TConnection, CancellationToken, Task> openConnectionAsync,
        Func<TConnection, TTransaction?, CancellationToken, Task<TResult>> operationAsync,
        CancellationToken cancellationToken)
        where TConnection : DbConnection
        where TTransaction : DbTransaction
    {
        TConnection? connection = null;
        TTransaction? dbTransaction = null;
        var shouldDisposeConnection = true;

        if (transaction is RelationalDataStoreTransaction<TConnection, TTransaction> typedTransaction)
        {
            connection = typedTransaction.Connection;
            dbTransaction = typedTransaction.Transaction;
            shouldDisposeConnection = false; // Transaction owns the connection
        }
        else
        {
            connection = await createConnectionAsync(dataSource, cancellationToken).ConfigureAwait(false);
            await openConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await operationAsync(connection, dbTransaction, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // CRITICAL: Only dispose the connection if we created it (not if it came from a transaction)
            // The transaction will dispose its own connection when it's disposed
            if (shouldDisposeConnection && connection != null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Extracts the typed connection and transaction from an IDataStoreTransaction.
    /// </summary>
    /// <typeparam name="TConnection">The ADO.NET connection type.</typeparam>
    /// <typeparam name="TTransaction">The ADO.NET transaction type.</typeparam>
    /// <param name="transaction">The transaction wrapper.</param>
    /// <returns>A tuple containing the connection and transaction, or (null, null) if the transaction is null or wrong type.</returns>
    public static (TConnection? Connection, TTransaction? Transaction) ExtractTransaction<TConnection, TTransaction>(
        IDataStoreTransaction? transaction)
        where TConnection : DbConnection
        where TTransaction : DbTransaction
    {
        if (transaction is RelationalDataStoreTransaction<TConnection, TTransaction> typedTransaction)
        {
            return (typedTransaction.Connection, typedTransaction.Transaction);
        }

        return (null, null);
    }

    /// <summary>
    /// Determines whether the operation should dispose the connection.
    /// </summary>
    /// <param name="transaction">The optional transaction.</param>
    /// <returns>True if the connection should be disposed, false if the transaction owns it.</returns>
    public static bool ShouldDisposeConnection(IDataStoreTransaction? transaction)
    {
        return transaction is null;
    }
}
