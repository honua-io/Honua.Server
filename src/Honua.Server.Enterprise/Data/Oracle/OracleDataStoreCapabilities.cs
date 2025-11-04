// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Oracle;

using Honua.Server.Core.Data;

internal sealed class OracleDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly OracleDataStoreCapabilities Instance = new();

    private OracleDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // Oracle SDO_GEOMETRY
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true; // R-tree spatial indexes
    public bool SupportsServerSideGeometryOperations => true; // SDO_* functions
    public bool SupportsCrsTransformations => true; // SDO_CS.TRANSFORM
    public int MaxQueryParameters => 32767;
    public bool SupportsReturningClause => true; // Oracle supports RETURNING INTO
    public bool SupportsBulkOperations => false; // Oracle bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for Oracle
}
