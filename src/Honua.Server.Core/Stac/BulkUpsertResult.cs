// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Result of a bulk upsert operation.
/// </summary>
public sealed class BulkUpsertResult
{
    /// <summary>
    /// Total number of items successfully inserted or updated.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Total number of items that failed to insert or update.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// List of items that failed to insert or update with their error messages.
    /// </summary>
    public IReadOnlyList<BulkUpsertItemFailure> Failures { get; init; } = Array.Empty<BulkUpsertItemFailure>();

    /// <summary>
    /// Total time taken for the bulk operation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Throughput in items per second.
    /// </summary>
    public double ItemsPerSecond => Duration.TotalSeconds > 0 ? SuccessCount / Duration.TotalSeconds : 0;

    /// <summary>
    /// Whether all items were successfully processed.
    /// </summary>
    public bool IsSuccess => FailureCount == 0;
}

/// <summary>
/// Information about a single item that failed during bulk upsert.
/// </summary>
public sealed class BulkUpsertItemFailure
{
    /// <summary>
    /// The item ID that failed.
    /// </summary>
    public required string ItemId { get; init; }

    /// <summary>
    /// The collection ID of the item that failed.
    /// </summary>
    public required string CollectionId { get; init; }

    /// <summary>
    /// The error message describing why the item failed.
    /// </summary>
    public required string ErrorMessage { get; init; }

    /// <summary>
    /// The exception that caused the failure, if available.
    /// </summary>
    public Exception? Exception { get; init; }
}
