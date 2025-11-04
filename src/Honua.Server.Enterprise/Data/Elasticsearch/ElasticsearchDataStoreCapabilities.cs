// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Data;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Elasticsearch;

internal sealed class ElasticsearchDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly ElasticsearchDataStoreCapabilities Instance = new();

    private ElasticsearchDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // geo_point and geo_shape field support
    public bool SupportsNativeMvt => false; // vector tile API available but not core data-store output
    public bool SupportsTransactions => false; // Elasticsearch does not support multi-document transactions
    public bool SupportsSpatialIndexes => true; // BKD tree based geo indexes
    public bool SupportsServerSideGeometryOperations => true; // geo queries and aggregations available
    public bool SupportsCrsTransformations => false; // limited to WGS84/LonLat for geo fields
    public int MaxQueryParameters => 10000; // aligns with default search.max_buckets / script parameter guidance
    public bool SupportsReturningClause => false;
    public bool SupportsBulkOperations => false; // Elasticsearch bulk operations not yet implemented
    public bool SupportsSoftDelete => false; // Soft delete not yet implemented for Elasticsearch
}
