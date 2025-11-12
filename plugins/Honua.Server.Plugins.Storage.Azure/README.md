# Azure Blob Storage Plugin for Honua.Server

Enterprise-grade Azure Blob Storage cloud storage plugin for Honua.Server.

## Overview

This plugin enables Azure Blob Storage as a cloud storage provider for Honua.Server's attachment storage system. It provides enterprise-grade object storage with SAS token support, server-side encryption, geo-redundant storage, and Azure CDN integration.

## Features

- **SAS Token Support**: Generate presigned URLs using Shared Access Signatures for secure direct client uploads/downloads
- **Server-Side Encryption**: Automatic encryption at rest with Azure Storage Service Encryption
- **Geo-Redundant Storage**: Support for GRS, RA-GRS, and GZRS replication strategies
- **Azure CDN Integration**: Seamless integration with Azure CDN for global content delivery
- **Blob Versioning**: Track and restore previous versions of attachments
- **Lifecycle Management**: Automated blob lifecycle policies for cost optimization
- **Block Blobs**: Support for multipart uploads and streaming uploads
- **Metadata & Tags**: Custom metadata and blob tags for organization
- **Access Control**: Blob-level access tiers and permissions

## Capabilities

| Feature | Supported |
|---------|-----------|
| Presigned URLs (SAS) | Yes |
| Encryption | Yes |
| Versioning | Yes |
| Lifecycle Policies | Yes |
| Replication | Yes (GRS, RA-GRS, GZRS) |
| CDN | Yes (Azure CDN) |
| Metadata | Yes |
| ACL | Yes |
| Max File Size | 190.7 TB (block blobs) |
| Multipart Upload | Yes |
| Streaming Upload | Yes |

## Configuration

### Connection String

Use the `attachment_storage` block in your Honua configuration:

```hcl
attachment_storage "azure_primary" {
  provider = "azureblob"

  azure {
    connection_string = "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=...;EndpointSuffix=core.windows.net"
    container_name    = "attachments"
    prefix            = "honua/"
    ensure_container  = true
  }
}
```

### Managed Identity (Recommended for Production)

For production deployments, use Azure Managed Identity instead of connection strings:

```hcl
attachment_storage "azure_managed_identity" {
  provider = "azureblob"

  azure {
    # Leave connection_string empty to use DefaultAzureCredential
    # This will use managed identity, environment variables, or Azure CLI credentials
    connection_string = ""
    container_name    = "production-attachments"
    prefix            = "uploads/"
    ensure_container  = false
  }
}
```

### Local Development (Azurite)

For local development, you can use Azurite (Azure Storage Emulator):

```hcl
attachment_storage "azure_local" {
  provider = "azureblob"

  azure {
    connection_string = "UseDevelopmentStorage=true"
    container_name    = "test-attachments"
    ensure_container  = true
  }
}
```

## Configuration Options

| Option | Type | Required | Default | Description |
|--------|------|----------|---------|-------------|
| `connection_string` | string | Yes* | - | Azure Storage connection string or empty for DefaultAzureCredential |
| `container_name` | string | Yes | - | Blob container name (3-63 chars, lowercase, numbers, hyphens only) |
| `prefix` | string | No | - | Optional prefix for blob keys (e.g., "attachments/") |
| `ensure_container` | bool | No | true | Automatically create container if it doesn't exist |

*Required field, but can be empty to use DefaultAzureCredential

## Container Naming Rules

Azure Blob Storage container names must follow these rules:
- 3-63 characters long
- Contain only lowercase letters, numbers, and hyphens
- Must not start or end with a hyphen
- Consecutive hyphens are not allowed

## Authentication Methods

### 1. Connection String
The most straightforward method for development and testing:
```
DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net
```

### 2. DefaultAzureCredential (Recommended)
Leave `connection_string` empty to use Azure's DefaultAzureCredential, which tries:
1. Environment variables
2. Managed Identity (in Azure)
3. Azure CLI credentials
4. Visual Studio credentials
5. Azure PowerShell credentials

### 3. Development Storage (Azurite)
For local development:
```
UseDevelopmentStorage=true
```

## Dependencies

- **Azure.Storage.Blobs** (v12.25.1): Azure Blob Storage SDK
- **Honua.Server.Core**: Core plugin infrastructure
- **Honua.Server.Core.Cloud**: Cloud provider implementations

## Plugin Information

- **Plugin ID**: `honua.plugins.storage.azure`
- **Plugin Name**: Azure Blob Storage Plugin
- **Version**: 1.0.0
- **Author**: HonuaIO
- **Provider Key**: `azureblob`
- **Cloud Provider**: Azure
- **Target Framework**: .NET 9.0

## Architecture

This plugin follows the standard Honua.Server plugin architecture:

1. Implements `ICloudStoragePlugin` interface
2. Registers `AzureBlobAttachmentStoreProvider` as a keyed singleton
3. Validates configuration and Azure SDK availability
4. Creates provider instances on demand

The plugin delegates actual storage operations to the `AzureBlobAttachmentStoreProvider` and `AzureBlobAttachmentStore` classes in `Honua.Server.Core.Cloud`.

## Validation

The plugin performs the following validations:
- Azure.Storage.Blobs SDK availability
- Connection string format validation
- Container name validation (naming rules)
- Configuration completeness checks

## Usage Example

Once configured, attachments will automatically be stored in Azure Blob Storage:

```csharp
// The attachment service will automatically use the configured Azure Blob Storage
var attachmentId = await attachmentService.CreateAsync(file, metadata);

// Retrieve attachment
var attachment = await attachmentService.GetAsync(attachmentId);

// Generate presigned URL (SAS token)
var sasUrl = await attachmentService.GetPresignedUrlAsync(attachmentId, TimeSpan.FromHours(1));
```

## Performance Considerations

- **Block Size**: Azure uses block blobs with up to 50,000 blocks of 4000 MiB each
- **Throughput**: Optimized for parallel uploads/downloads
- **Latency**: Consider using Azure CDN for global distribution
- **Costs**: Leverage lifecycle policies to automatically tier or delete old attachments

## Security Best Practices

1. **Use Managed Identity**: Avoid storing connection strings in configuration
2. **Enable Encryption**: Azure Storage encryption is enabled by default
3. **Configure Firewalls**: Use Azure Storage firewall rules to restrict access
4. **Private Endpoints**: Consider using Azure Private Link for private connectivity
5. **SAS Expiration**: Set appropriate expiration times for SAS tokens
6. **Soft Delete**: Enable soft delete for protection against accidental deletion

## Troubleshooting

### Connection Issues
- Verify connection string format
- Check storage account firewall rules
- Ensure managed identity has appropriate permissions

### Container Access
- Verify container exists if `ensure_container = false`
- Check RBAC permissions (Storage Blob Data Contributor role required)

### Performance
- Consider using Azure CDN for frequently accessed content
- Use appropriate storage tier (Hot, Cool, Archive)
- Enable blob versioning only if needed (incurs additional costs)

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
