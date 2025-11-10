# 14. Multi-Cloud Object Storage Support

Date: 2025-10-17

Status: Accepted

## Context

Honua caches raster tiles and stores large datasets in cloud object storage. Different deployments use different cloud providers:
- **AWS**: S3
- **Azure**: Blob Storage
- **GCP**: Cloud Storage
- **On-premises**: MinIO, Ceph

**Requirements:**
- Unified API across cloud providers
- Efficient HTTP range requests for COG/Zarr
- Configurable per deployment
- Fallback to local filesystem
- Production-grade error handling

**Existing Evidence:**
- S3 provider: `/src/Honua.Server.Core/Raster/Caching/S3RasterTileCacheProvider.cs`
- Azure provider: `/src/Honua.Server.Core/Raster/Caching/AzureBlobRasterTileCacheProvider.cs`
- GCS provider: `/src/Honua.Server.Core/Raster/Caching/GcsRasterTileCacheProvider.cs`
- SDK dependencies:
  - AWS: `AWSSDK.S3`
  - Azure: `Azure.Storage.Blobs`
  - GCP: `Google.Cloud.Storage.V1`

## Decision

Implement **provider pattern for object storage** with native SDKs for each cloud platform.

**Supported Providers:**
1. **AWS S3** (primary)
2. **Azure Blob Storage**
3. **Google Cloud Storage**
4. **Local Filesystem** (development/fallback)

**Common Interface:**
```csharp
public interface IRasterTileCacheProvider
{
    Task<byte[]?> GetTileAsync(string key, CancellationToken ct);
    Task PutTileAsync(string key, byte[] data, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
    Task DeleteTileAsync(string key, CancellationToken ct);
}
```

## Consequences

### Positive

- **Cloud Agnostic**: Deploy to any cloud provider
- **Performance**: Native SDKs optimized for each platform
- **Flexibility**: Choose provider based on cost/features
- **Multi-Cloud**: Different providers for different purposes
- **Standards**: Object storage is industry-standard pattern

### Negative

- **Maintenance**: Must maintain three cloud provider implementations
- **Testing**: Need test coverage for all providers
- **Feature Parity**: Differences in cloud provider capabilities
- **Credentials**: Different auth patterns per provider

### Neutral

- Must configure credentials per provider
- Performance characteristics vary by provider

## Alternatives Considered

### 1. S3-Compatible API Only (MinIO Protocol)
**Verdict:** Rejected - not all providers fully S3-compatible

### 2. Single Cloud Provider
**Verdict:** Rejected - vendor lock-in

### 3. Abstraction Library (e.g., Cloud Storage Abstraction)
**Verdict:** Rejected - adds dependency, limited by library features

## Implementation

**S3 Example:**
```csharp
public class S3RasterTileCacheProvider : IRasterTileCacheProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly ResiliencePipeline _resilience;

    public async Task<byte[]?> GetTileAsync(string key, CancellationToken ct)
    {
        return await _resilience.ExecuteAsync(async ct =>
        {
            var response = await _s3Client.GetObjectAsync(
                _bucketName, key, ct);
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }, ct);
    }
}
```

**Configuration:**
```json
{
  "RasterCache": {
    "Provider": "S3",  // "S3", "AzureBlob", "GCS", "FileSystem"
    "S3": {
      "BucketName": "honua-tiles",
      "Region": "us-east-1"
    }
  }
}
```

## Code Reference

- S3: `/src/Honua.Server.Core/Raster/Caching/S3RasterTileCacheProvider.cs`
- Azure: `/src/Honua.Server.Core/Raster/Caching/AzureBlobRasterTileCacheProvider.cs`
- GCS: `/src/Honua.Server.Core/Raster/Caching/GcsRasterTileCacheProvider.cs`
- Resilience: `/src/Honua.Server.Core/Raster/Caching/ExternalServiceResiliencePolicies.cs`

## References

- [AWS S3 Documentation](https://docs.aws.amazon.com/s3/)
- [Azure Blob Storage](https://docs.microsoft.com/en-us/azure/storage/blobs/)
- [Google Cloud Storage](https://cloud.google.com/storage/docs)

## Notes

Multi-cloud support is essential for avoiding vendor lock-in and supporting diverse customer requirements. The provider pattern enables clean abstraction while using native SDKs for optimal performance.
