# Google Cloud Storage Attachment Provider

The GCS attachment provider is now fully integrated with the HonuaIO attachment storage system, following the same provider pattern as S3 and Azure Blob Storage.

## Configuration

Add a GCS storage profile to your `appsettings.json`:

```json
{
  "Attachments": {
    "DefaultMaxSizeMiB": 25,
    "Profiles": {
      "production-gcs": {
        "Provider": "gcs",
        "Gcs": {
          "BucketName": "my-honua-attachments",
          "Prefix": "attachments/",
          "ProjectId": "my-gcp-project",
          "UseApplicationDefaultCredentials": true
        }
      },
      "dev-gcs": {
        "Provider": "gcs",
        "Gcs": {
          "BucketName": "dev-attachments",
          "Prefix": "dev/",
          "CredentialsPath": "/path/to/service-account.json",
          "UseApplicationDefaultCredentials": false
        }
      }
    }
  }
}
```

## Configuration Options

### Required

- **`BucketName`** (string): The GCS bucket name where attachments will be stored

### Optional

- **`Prefix`** (string): Key prefix for all attachments (default: `"attachments/"`)
- **`ProjectId`** (string): GCP project ID (optional, usually auto-detected)
- **`CredentialsPath`** (string): Path to service account JSON credentials file
- **`UseApplicationDefaultCredentials`** (boolean): Use Application Default Credentials (default: `true`)

## Authentication Methods

### 1. Application Default Credentials (Recommended)

Set `UseApplicationDefaultCredentials: true` (default). This supports:

- **`GOOGLE_APPLICATION_CREDENTIALS`** environment variable pointing to service account JSON
- **gcloud CLI** credentials (`gcloud auth application-default login`)
- **GCE/GKE/Cloud Run** metadata server (automatic in Google Cloud environments)

```json
{
  "Gcs": {
    "BucketName": "my-bucket",
    "UseApplicationDefaultCredentials": true
  }
}
```

### 2. Service Account File

Explicitly specify a service account credentials file:

```json
{
  "Gcs": {
    "BucketName": "my-bucket",
    "CredentialsPath": "/secrets/gcs-service-account.json",
    "UseApplicationDefaultCredentials": false
  }
}
```

## Layer Configuration

Specify which storage profile a layer should use in the catalog:

```json
{
  "layers": [
    {
      "id": "photos",
      "attachments": {
        "enabled": true,
        "storageProfile": "production-gcs",
        "maxSizeBytes": 10485760,
        "allowedMimeTypes": ["image/jpeg", "image/png"]
      }
    }
  ]
}
```

## Required IAM Permissions

The service account or default credentials must have the following GCS permissions:

- `storage.objects.create` - Upload attachments
- `storage.objects.get` - Download attachments
- `storage.objects.delete` - Delete attachments
- `storage.objects.list` - List attachments

These are provided by the **Storage Object Admin** (`roles/storage.objectAdmin`) role.

## Deployment Examples

### Kubernetes with Workload Identity

```yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: honua-server
  annotations:
    iam.gke.io/gcp-service-account: honua-server@PROJECT_ID.iam.gserviceaccount.com
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  template:
    spec:
      serviceAccountName: honua-server
      containers:
      - name: server
        image: honua-server:latest
        env:
        - name: Attachments__Profiles__default__Provider
          value: "gcs"
        - name: Attachments__Profiles__default__Gcs__BucketName
          value: "honua-prod-attachments"
```

### Docker with Service Account

```bash
docker run -d \
  -e GOOGLE_APPLICATION_CREDENTIALS=/secrets/gcs-sa.json \
  -e Attachments__Profiles__default__Provider=gcs \
  -e Attachments__Profiles__default__Gcs__BucketName=honua-attachments \
  -v /path/to/gcs-sa.json:/secrets/gcs-sa.json:ro \
  honua-server:latest
```

### Cloud Run (Automatic)

Cloud Run automatically provides Application Default Credentials. No additional configuration needed:

```json
{
  "Attachments": {
    "Profiles": {
      "default": {
        "Provider": "gcs",
        "Gcs": {
          "BucketName": "honua-cloudrun-attachments"
        }
      }
    }
  }
}
```

## Multi-Cloud Deployment Example

Use different providers for different layers:

```json
{
  "Attachments": {
    "Profiles": {
      "gcs-media": {
        "Provider": "gcs",
        "Gcs": { "BucketName": "media-assets" }
      },
      "s3-backups": {
        "Provider": "s3",
        "S3": { "BucketName": "disaster-recovery" }
      },
      "azure-archives": {
        "Provider": "azureblob",
        "Azure": { "ConnectionString": "..." }
      }
    }
  },
  "Catalog": {
    "Layers": [
      { "id": "photos", "attachments": { "storageProfile": "gcs-media" } },
      { "id": "surveys", "attachments": { "storageProfile": "s3-backups" } },
      { "id": "historical", "attachments": { "storageProfile": "azure-archives" } }
    ]
  }
}
```

## Health Checks

The GCS health check is automatically registered when a GCS profile is configured. Access at:

```
GET /health
```

Health check verifies bucket access and credentials.

## Implementation Details

**Provider Class**: `GcsAttachmentStoreProvider`
**Store Class**: `GcsAttachmentStore`
**Configuration**: `AttachmentGcsStorageConfiguration`
**Provider Key**: `"gcs"` (`AttachmentStoreProviderKeys.Gcs`)

**Features**:
- ✅ Full CRUD operations (Create, Read, Delete, List)
- ✅ Metadata storage (filename, checksum, custom metadata)
- ✅ SHA256 checksum validation
- ✅ MIME type preservation
- ✅ Path traversal protection
- ✅ Application Default Credentials support
- ✅ Service account file support
- ✅ Client caching and connection pooling
- ✅ Proper disposal and resource cleanup

## Migration from Legacy Configuration

**Old configuration** (IConfiguration-based):

```json
{
  "GoogleCloud": {
    "Storage": {
      "AttachmentBucket": "my-bucket",
      "AttachmentPrefix": "attachments/"
    }
  }
}
```

**New configuration** (Profile-based):

```json
{
  "Attachments": {
    "Profiles": {
      "default": {
        "Provider": "gcs",
        "Gcs": {
          "BucketName": "my-bucket",
          "Prefix": "attachments/"
        }
      }
    }
  }
}
```

The new approach allows multiple GCS profiles with different buckets, credentials, and prefixes.

## See Also

- [Architecture Decision Record: Multi-Cloud Object Storage](../architecture/decisions/0014-multi-cloud-object-storage.md)
- [S3 Attachment Storage](S3_ATTACHMENT_STORAGE.md)
- [Azure Blob Attachment Storage](AZURE_ATTACHMENT_STORAGE.md)
