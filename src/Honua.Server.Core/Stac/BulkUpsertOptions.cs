// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Options for bulk upsert operations.
/// </summary>
public sealed class BulkUpsertOptions
{
    /// <summary>
    /// The batch size for splitting large bulk operations. Default is 1000.
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    /// Whether to report progress during the bulk operation.
    /// </summary>
    public bool ReportProgress { get; init; } = false;

    /// <summary>
    /// Progress callback invoked after each batch is processed.
    /// Parameters: (processedCount, totalCount, batchNumber)
    /// </summary>
    public Action<int, int, int>? ProgressCallback { get; init; }

    /// <summary>
    /// Whether to continue processing remaining batches if one batch fails.
    /// Default is false (rollback entire operation on first failure).
    /// </summary>
    public bool ContinueOnError { get; init; } = false;

    /// <summary>
    /// Whether to use database-specific bulk insert optimizations (COPY, BulkCopy, etc).
    /// Default is true.
    /// </summary>
    public bool UseBulkInsertOptimization { get; init; } = true;

    /// <summary>
    /// Transaction timeout in seconds for bulk operations.
    /// This allows large STAC catalogs (50+ batches) to complete without timeout.
    /// Set to 0 for no timeout (use database default).
    /// Default is 3600 seconds (1 hour).
    /// </summary>
    public int TransactionTimeoutSeconds { get; init; } = 3600;

    /// <summary>
    /// Whether to use a single transaction for the entire bulk operation.
    /// When true, all batches are wrapped in a single transaction with all-or-nothing semantics.
    /// When false, each batch gets its own transaction (NOT RECOMMENDED - can cause partial updates).
    /// Default is true for data integrity.
    /// </summary>
    public bool UseAtomicTransaction { get; init; } = true;
}
