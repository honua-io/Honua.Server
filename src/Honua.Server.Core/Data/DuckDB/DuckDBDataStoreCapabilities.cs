// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Data.DuckDB;

internal sealed class DuckDBDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly DuckDBDataStoreCapabilities Instance = new();

    private DuckDBDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // Via spatial extension
    public bool SupportsNativeMvt => false; // No native MVT support
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true; // Via spatial extension
    public bool SupportsServerSideGeometryOperations => true; // Via spatial extension (PostGIS-compatible functions)
    public bool SupportsCrsTransformations => true; // Via spatial extension
    public int MaxQueryParameters => 65535; // DuckDB limit
    public bool SupportsReturningClause => true; // DuckDB supports RETURNING
    public bool SupportsBulkOperations => true; // Excellent bulk operation support
    public bool SupportsSoftDelete => true;
}
