// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.CosmosDb;

using Honua.Server.Core.Data;

internal sealed class CosmosDbDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly CosmosDbDataStoreCapabilities Instance = new();

    private CosmosDbDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // Cosmos DB supports GeoJSON
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true; // Cosmos DB supports transactions within a partition
    public bool SupportsSpatialIndexes => true; // Cosmos DB geospatial indexing
    public bool SupportsServerSideGeometryOperations => true; // ST_DISTANCE, ST_WITHIN available
    public bool SupportsCrsTransformations => false; // Cosmos DB uses WGS84 only
    public int MaxQueryParameters => 2000; // Cosmos DB SQL parameter limit
    public bool SupportsReturningClause => false;
    public bool SupportsBulkOperations => false; // Cosmos DB bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for Cosmos DB
}
