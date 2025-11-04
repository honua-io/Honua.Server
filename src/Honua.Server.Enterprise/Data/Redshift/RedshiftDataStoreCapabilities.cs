// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Redshift;

using Honua.Server.Core.Data;

internal sealed class RedshiftDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly RedshiftDataStoreCapabilities Instance = new();

    private RedshiftDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // Redshift supports PostGIS-compatible spatial functions
    public bool SupportsNativeMvt => false; // Redshift doesn't have ST_AsMVT
    public bool SupportsTransactions => true; // Redshift supports transactions
    public bool SupportsSpatialIndexes => false; // Redshift doesn't support spatial indexes (columnar storage)
    public bool SupportsServerSideGeometryOperations => true; // PostGIS-compatible functions available
    public bool SupportsCrsTransformations => true; // ST_Transform available
    public int MaxQueryParameters => 32767; // PostgreSQL-compatible limit
    public bool SupportsReturningClause => true; // Redshift supports RETURNING
    public bool SupportsBulkOperations => false; // Redshift bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for Redshift
}
