// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.BigQuery;

using Honua.Server.Core.Data;

internal sealed class BigQueryDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly BigQueryDataStoreCapabilities Instance = new();

    private BigQueryDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // BigQuery supports GEOGRAPHY type
    public bool SupportsNativeMvt => false; // BigQuery doesn't have native MVT generation
    public bool SupportsTransactions => false; // BigQuery doesn't support traditional transactions
    public bool SupportsSpatialIndexes => false; // BigQuery doesn't have traditional spatial indexes
    public bool SupportsServerSideGeometryOperations => true; // BigQuery GIS functions available
    public bool SupportsCrsTransformations => false; // BigQuery GEOGRAPHY is always WGS84
    public int MaxQueryParameters => 1000; // BigQuery parameter limit
    public bool SupportsReturningClause => false; // BigQuery doesn't support RETURNING
    public bool SupportsBulkOperations => false; // BigQuery bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for BigQuery
}
