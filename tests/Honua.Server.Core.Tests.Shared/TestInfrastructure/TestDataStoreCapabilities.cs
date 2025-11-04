using Honua.Server.Core.Data;

namespace Honua.Server.Core.Tests.Shared;

public sealed class TestDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly TestDataStoreCapabilities Instance = new();

    private TestDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true;
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true;
    public bool SupportsServerSideGeometryOperations => true;
    public bool SupportsCrsTransformations => true;
    public int MaxQueryParameters => 1000;
    public bool SupportsReturningClause => true;
    public bool SupportsBulkOperations => true;
    public bool SupportsSoftDelete => true; // Test provider supports all features
}
