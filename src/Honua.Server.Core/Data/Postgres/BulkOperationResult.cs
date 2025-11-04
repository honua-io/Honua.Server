// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Represents the result of a bulk operation with detailed success/failure tracking.
/// Provides data integrity guarantees by tracking which records succeeded and which failed.
/// </summary>
public sealed class BulkOperationResult
{
    public BulkOperationResult(
        int totalRecords,
        int successCount,
        int failureCount,
        IReadOnlyList<BulkOperationItemResult> itemResults)
    {
        TotalRecords = totalRecords;
        SuccessCount = successCount;
        FailureCount = failureCount;
        ItemResults = itemResults ?? Array.Empty<BulkOperationItemResult>();

        // Data integrity validation
        if (SuccessCount + FailureCount != TotalRecords)
        {
            throw new InvalidOperationException(
                $"BulkOperationResult integrity violation: " +
                $"SuccessCount ({SuccessCount}) + FailureCount ({FailureCount}) != TotalRecords ({TotalRecords})");
        }

        if (ItemResults.Count != TotalRecords)
        {
            throw new InvalidOperationException(
                $"BulkOperationResult integrity violation: " +
                $"ItemResults count ({ItemResults.Count}) != TotalRecords ({TotalRecords})");
        }
    }

    /// <summary>
    /// Total number of records processed.
    /// </summary>
    public int TotalRecords { get; }

    /// <summary>
    /// Number of records that succeeded.
    /// </summary>
    public int SuccessCount { get; }

    /// <summary>
    /// Number of records that failed.
    /// </summary>
    public int FailureCount { get; }

    /// <summary>
    /// Detailed results for each item in the batch.
    /// Guaranteed to have exactly TotalRecords entries.
    /// </summary>
    public IReadOnlyList<BulkOperationItemResult> ItemResults { get; }

    /// <summary>
    /// Whether all records succeeded.
    /// </summary>
    public bool AllSucceeded => FailureCount == 0;

    /// <summary>
    /// Whether any records failed.
    /// </summary>
    public bool AnyFailed => FailureCount > 0;

    /// <summary>
    /// Gets all successful item results.
    /// </summary>
    public IEnumerable<BulkOperationItemResult> Successes => ItemResults.Where(r => r.Success);

    /// <summary>
    /// Gets all failed item results.
    /// </summary>
    public IEnumerable<BulkOperationItemResult> Failures => ItemResults.Where(r => !r.Success);

    /// <summary>
    /// Creates a result for a fully successful operation.
    /// </summary>
    public static BulkOperationResult CreateSuccess(int totalRecords, IReadOnlyList<BulkOperationItemResult> itemResults)
    {
        return new BulkOperationResult(totalRecords, totalRecords, 0, itemResults);
    }

    /// <summary>
    /// Creates a result for a partially successful operation.
    /// </summary>
    public static BulkOperationResult CreatePartial(
        int totalRecords,
        int successCount,
        IReadOnlyList<BulkOperationItemResult> itemResults)
    {
        return new BulkOperationResult(totalRecords, successCount, totalRecords - successCount, itemResults);
    }

    /// <summary>
    /// Creates a result for a fully failed operation.
    /// </summary>
    public static BulkOperationResult CreateFailure(int totalRecords, IReadOnlyList<BulkOperationItemResult> itemResults)
    {
        return new BulkOperationResult(totalRecords, 0, totalRecords, itemResults);
    }
}

/// <summary>
/// Represents the result of a single item in a bulk operation.
/// </summary>
public sealed class BulkOperationItemResult
{
    public BulkOperationItemResult(
        int index,
        string? identifier,
        bool success,
        string? errorMessage = null,
        Exception? exception = null)
    {
        Index = index;
        Identifier = identifier;
        Success = success;
        ErrorMessage = errorMessage;
        Exception = exception;

        // Data integrity validation
        if (!success && string.IsNullOrWhiteSpace(errorMessage) && exception is null)
        {
            throw new InvalidOperationException(
                $"Failed bulk operation item at index {index} must have an error message or exception.");
        }
    }

    /// <summary>
    /// Zero-based index of the item in the batch.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Optional identifier for the item (e.g., feature ID, record key).
    /// </summary>
    public string? Identifier { get; }

    /// <summary>
    /// Whether the operation succeeded for this item.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Creates a successful item result.
    /// </summary>
    public static BulkOperationItemResult CreateSuccess(int index, string? identifier = null)
    {
        return new BulkOperationItemResult(index, identifier, success: true);
    }

    /// <summary>
    /// Creates a failed item result.
    /// </summary>
    public static BulkOperationItemResult CreateFailure(
        int index,
        string errorMessage,
        string? identifier = null,
        Exception? exception = null)
    {
        return new BulkOperationItemResult(index, identifier, success: false, errorMessage, exception);
    }

    public override string ToString()
    {
        var id = string.IsNullOrWhiteSpace(Identifier) ? $"Index {Index}" : $"{Identifier} (Index {Index})";
        if (Success)
        {
            return $"{id}: Success";
        }
        return $"{id}: Failed - {ErrorMessage}";
    }
}
