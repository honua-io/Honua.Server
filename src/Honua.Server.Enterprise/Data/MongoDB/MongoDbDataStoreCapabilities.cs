// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.MongoDB;

using Honua.Server.Core.Data;

internal sealed class MongoDbDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly MongoDbDataStoreCapabilities Instance = new();

    private MongoDbDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // MongoDB supports GeoJSON
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true; // MongoDB supports multi-document transactions
    public bool SupportsSpatialIndexes => true; // MongoDB 2dsphere indexes
    public bool SupportsServerSideGeometryOperations => true; // $geoIntersects, $geoWithin available
    public bool SupportsCrsTransformations => false; // MongoDB uses WGS84
    public int MaxQueryParameters => 10000; // MongoDB has no strict limit
    public bool SupportsReturningClause => false;
    public bool SupportsBulkOperations => false; // MongoDB bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for MongoDB
}
