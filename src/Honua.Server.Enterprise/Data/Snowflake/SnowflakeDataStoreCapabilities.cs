// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Snowflake;

using Honua.Server.Core.Data;

internal sealed class SnowflakeDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly SnowflakeDataStoreCapabilities Instance = new();

    private SnowflakeDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // Snowflake supports GEOGRAPHY and GEOMETRY types
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true; // Snowflake supports transactions
    public bool SupportsSpatialIndexes => false; // Snowflake uses automatic clustering, not traditional indexes
    public bool SupportsServerSideGeometryOperations => true; // ST_* functions available
    public bool SupportsCrsTransformations => false; // Snowflake uses WGS84 for GEOGRAPHY
    public int MaxQueryParameters => 16384; // Snowflake parameter limit
    public bool SupportsReturningClause => false; // Snowflake doesn't support RETURNING clause
    public bool SupportsBulkOperations => false; // Snowflake bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for Snowflake
}
