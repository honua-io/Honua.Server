// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Data;

/// <summary>
/// Describes the capabilities of a data store provider.
/// </summary>
public interface IDataStoreCapabilities
{
    /// <summary>
    /// Gets whether the provider supports native geometry types.
    /// </summary>
    bool SupportsNativeGeometry { get; }

    /// <summary>
    /// Gets whether the provider supports native MVT (Mapbox Vector Tile) generation.
    /// </summary>
    bool SupportsNativeMvt { get; }

    /// <summary>
    /// Gets whether the provider supports database transactions.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// Gets whether the provider supports spatial indexes.
    /// </summary>
    bool SupportsSpatialIndexes { get; }

    /// <summary>
    /// Gets whether the provider supports server-side geometry operations.
    /// </summary>
    bool SupportsServerSideGeometryOperations { get; }

    /// <summary>
    /// Gets whether the provider supports coordinate reference system (CRS) transformations.
    /// </summary>
    bool SupportsCrsTransformations { get; }

    /// <summary>
    /// Gets the maximum number of parameters supported in a single query.
    /// </summary>
    int MaxQueryParameters { get; }

    /// <summary>
    /// Gets whether the provider supports returning generated keys after insert.
    /// </summary>
    bool SupportsReturningClause { get; }

    /// <summary>
    /// Gets whether the provider supports bulk operations (BulkInsertAsync, BulkUpdateAsync, BulkDeleteAsync).
    /// When true, the provider can efficiently handle batch operations.
    /// When false, operations fall back to individual insert/update/delete calls.
    /// </summary>
    bool SupportsBulkOperations { get; }

    /// <summary>
    /// Gets whether the provider supports soft delete and restore operations (SoftDeleteAsync, RestoreAsync).
    /// When true, the provider can mark records as deleted without permanently removing them.
    /// When false, calling SoftDeleteAsync or RestoreAsync will throw NotSupportedException.
    /// </summary>
    bool SupportsSoftDelete { get; }
}
