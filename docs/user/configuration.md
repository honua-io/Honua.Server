# Honua Configuration Reference

**IMPORTANT: This document covers application settings only (appsettings.json). For service, data source, and layer configuration, you must use Configuration V2 (HCL format).**

See [Configuration V2 Reference](../configuration-v2-reference.md) for complete HCL configuration documentation.

Complete reference for configuring Honua server application settings via `appsettings.json` and environment variables.

## Table of Contents
- [Configuration V2 (Required)](#configuration-v2-required)
- [Authentication](#authentication)
- [Services](#services)
- [Attachments](#attachments)
- [OData](#odata)
- [Server](#server)
- [Observability](#observability)
- [Environment Variables](#environment-variables)

## Configuration V2 (Required)

**All service, data source, and layer configuration must use Configuration V2 (HCL format).**

Legacy metadata.json configuration is no longer supported. See:
- [Configuration V2 Reference](../configuration-v2-reference.md)
- [Migration Guide](../configuration-v2-migration.md)
- [BREAKING_CHANGES.md](../../BREAKING_CHANGES.md)

## Authentication

Control how users authenticate and access your Honua server.

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false,
      "quickStart": {
        "enabled": true
      },
      "jwt": {
        "authority": "https://your-identity-provider.com",
        "audience": "honua-api",
        "roleClaimPath": "roles",
        "requireHttpsMetadata": true
      },
      "local": {
        "sessionLifetimeMinutes": 480,
        "storePath": "data/users",
        "signingKeyPath": "data/signing-key.pem",
        "maxFailedLoginAttempts": 5,
        "lockoutDurationMinutes": 15
      },
      "bootstrap": {
        "adminUsername": "admin",
        "adminEmail": "admin@example.com",
        "adminPassword": null
      }
    }
  }
}
```

### Authentication Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| `QuickStart` | No authentication required | Development, demos, internal networks |
| `Local` | Built-in user database with JWT | Small deployments, testing |
| `Oidc` | External OpenID Connect provider | Production, enterprise SSO |

**Security Warning:** QuickStart mode provides no authentication. Never use in production.

### Mode: QuickStart *(development only)*

> ⚠️ QuickStart disables authentication and **must never** be used in shared, staging, or production environments.
> It exists solely for local prototyping. Switch to **Local** or **OIDC** before exposing Honua to other users.

```json
"authentication": {
  "mode": "QuickStart",
  "enforce": false,
  "quickStart": {
    "enabled": true
  }
}
```

Or via environment variable (local development only):
```bash
HONUA__AUTHENTICATION__MODE=QuickStart
HONUA__AUTHENTICATION__ENFORCE=false
```

### Mode: Local

Local mode maintains a database-backed user store with Argon2id-hashed passwords.

**Security**: Uses Argon2id with 64MB memory cost and 4 iterations - more secure than bcrypt.

**Storage**: Supports SQLite (default), PostgreSQL, MySQL, and SQL Server.

```json
"authentication": {
  "mode": "Local",
  "enforce": true,
  "local": {
    "sessionLifetimeMinutes": 480,
    "storePath": "data/auth",
    "signingKeyPath": "data/signing-key.pem",
    "maxFailedLoginAttempts": 5,
    "lockoutDurationMinutes": 15,
    "provider": "sqlite"
  },
  "bootstrap": {
    "adminUsername": "admin",
    "adminEmail": "admin@example.com",
    "adminPassword": null
  }
}
```

**Options:**
- `sessionLifetimeMinutes` - JWT token expiration (default: 480 / 8 hours)
- `storePath` - Directory for user database (default: `data/users`)
- `signingKeyPath` - Path to RSA signing key (auto-generated if missing)
- `maxFailedLoginAttempts` - Lockout threshold (default: 5)
- `lockoutDurationMinutes` - Account lockout duration (default: 15)

**Bootstrap Admin:**
Use the CLI to create the initial admin user:
```bash
./scripts/honua.sh auth bootstrap --mode Local
```

If `adminPassword` is null, a random password will be generated and displayed.

### Mode: OIDC

Integrate with external identity providers (Azure AD, Auth0, Keycloak, etc.)

```json
"authentication": {
  "mode": "Oidc",
  "enforce": true,
  "jwt": {
    "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
    "audience": "api://honua-production",
    "roleClaimPath": "roles",
    "requireHttpsMetadata": true
  }
}
```

**Options:**
- `authority` - OIDC authority URL (issuer)
- `audience` - Expected audience claim in JWT
- `roleClaimPath` - JSON path to role claims (default: `roles`)
- `requireHttpsMetadata` - Require HTTPS for metadata endpoint (default: true)

**Role Mapping:**
Honua expects one of these roles in the JWT:
- `viewer` - Read-only access to data
- `datapublisher` - Can edit features and upload data
- `administrator` - Full access including metadata management

## Metadata Provider

Choose how Honua loads layer and service metadata.

```json
{
  "honua": {
    "metadata": {
      "provider": "json",
      "path": "samples/ogc/metadata.json",
      "watchForChanges": false
    }
  }
}
```

### Provider: JSON

Single-file JSON metadata (default).

```json
"metadata": {
  "provider": "json",
  "path": "metadata/services.json",
  "watchForChanges": false
}
```

Environment variable:
```bash
HONUA__METADATA__PROVIDER=json
HONUA__METADATA__PATH=/app/metadata/services.json
```

### Provider: YAML

YAML format with optional hot-reload.

```json
"metadata": {
  "provider": "yaml",
  "path": "metadata/services.yaml",
  "watchForChanges": true
}
```

**Options:**
- `watchForChanges` - Auto-reload on file changes (YAML only, default: false)

See [Metadata Authoring Guide](metadata-authoring.md) for schema details.

## Services

Enable or disable specific API services.

```json
{
  "honua": {
    "services": {
      "wfs": {
        "enabled": true
      },
      "wms": {
        "enabled": true
      },
      "stac": {
        "enabled": false,
        "provider": "sqlite",
        "connectionString": null,
        "filePath": "data/stac.db"
      },
      "geometry": {
        "enabled": true,
        "maxGeometries": 100,
        "maxCoordinateCount": 10000,
        "allowedSrids": null,
        "enableGdalOperations": false
      },
      "rasterTiles": {
        "enabled": true,
        "provider": "filesystem",
        "fileSystem": {
          "basePath": "data/raster-cache"
        },
        "s3": {
          "bucket": null,
          "region": null,
          "accessKeyId": null,
          "secretAccessKey": null,
          "prefix": "tiles/"
        },
        "azure": {
          "containerName": null,
          "connectionString": null,
          "prefix": "tiles/"
        },
        "preseed": {
          "enabled": false,
          "minZoom": 0,
          "maxZoom": 12,
          "concurrency": 4
        }
      }
    }
  }
}
```

### WFS Service

OGC Web Feature Service (WFS 2.0.0) with transactional support.

```json
"wfs": {
  "enabled": true
}
```

Endpoint: `https://{host}/wfs`

### WMS Service

OGC Web Map Service (WMS 1.3.0) for raster imagery.

```json
"wms": {
  "enabled": true
}
```

Endpoint: `https://{host}/wms`

### STAC Service

SpatioTemporal Asset Catalog API for raster dataset discovery.

```json
"stac": {
  "enabled": true,
  "provider": "sqlite",
  "connectionString": null,
  "filePath": "data/stac.db"
}
```

**Providers:**
- `sqlite` - SQLite database (recommended for < 10k items)
- `postgres` - PostgreSQL (requires `connectionString`)

Endpoint: `https://{host}/stac`

### Geometry Service

GeoServices REST compatible geometry operations (project, buffer, union, etc.)

```json
"geometry": {
  "enabled": true,
  "maxGeometries": 100,
  "maxCoordinateCount": 10000,
  "allowedSrids": null,
  "enableGdalOperations": false
}
```

**Options:**
- `maxGeometries` - Max geometries per request (default: 100)
- `maxCoordinateCount` - Max coordinates per geometry (default: 10000)
- `allowedSrids` - Whitelist of allowed SRIDs (null = all allowed)
- `enableGdalOperations` - Enable GDAL-based operations (requires GDAL installation)

Endpoint: `https://{host}/rest/services/Geometry/GeometryServer`

### Raster Tile Service

Tile caching and serving for raster datasets.

```json
"rasterTiles": {
  "enabled": true,
  "provider": "filesystem",
  "fileSystem": {
    "basePath": "data/raster-cache"
  }
}
```

**Providers:**
- `filesystem` - Local file system
- `s3` - Amazon S3 or compatible (MinIO, etc.)
- `azure` - Azure Blob Storage

**FileSystem Configuration:**
```json
"fileSystem": {
  "basePath": "data/raster-cache"
}
```

**S3 Configuration:**
```json
"s3": {
  "bucket": "honua-tiles",
  "region": "us-west-2",
  "accessKeyId": "env:AWS_ACCESS_KEY_ID",
  "secretAccessKey": "env:AWS_SECRET_ACCESS_KEY",
  "prefix": "tiles/"
}
```

**Azure Blob Configuration:**
```json
"azure": {
  "containerName": "tiles",
  "connectionString": "env:AZURE_STORAGE_CONNECTION_STRING",
  "prefix": "tiles/"
}
```

**Pre-seeding:**
Automatically generate tiles on startup:
```json
"preseed": {
  "enabled": true,
  "minZoom": 0,
  "maxZoom": 12,
  "concurrency": 4
}
```

## Attachments

Configure storage for feature attachments.

```json
{
  "honua": {
    "attachments": {
      "defaultMaxSizeMiB": 10,
      "profiles": [
        {
          "name": "default",
          "provider": "filesystem",
          "fileSystem": {
            "basePath": "data/attachments"
          }
        },
        {
          "name": "s3-production",
          "provider": "s3",
          "s3": {
            "bucket": "honua-attachments",
            "region": "us-east-1",
            "accessKeyId": "env:AWS_ACCESS_KEY_ID",
            "secretAccessKey": "env:AWS_SECRET_ACCESS_KEY",
            "prefix": "attachments/"
          }
        },
        {
          "name": "database",
          "provider": "database",
          "database": {
            "connectionString": "env:HONUA_ATTACHMENT_DB",
            "schema": "attachments",
            "tableName": "files"
          }
        }
      ]
    }
  }
}
```

**Providers:**
- `filesystem` - Local file system
- `s3` - S3-compatible storage
- `azure` - Azure Blob Storage
- `database` - Store in SQLite, PostgreSQL, or SQL Server

Layers reference attachment profiles in metadata:
```json
{
  "layers": [{
    "id": "inspections",
    "attachments": {
      "enabled": true,
      "profileName": "s3-production",
      "maxSizeMiB": 25,
      "allowedExtensions": ["jpg", "png", "pdf", "doc", "docx"]
    }
  }]
}
```

## OData

Configure OData metadata query API.

```json
{
  "honua": {
    "odata": {
      "enabled": true,
      "allowWrites": false,
      "defaultPageSize": 50,
      "maxPageSize": 1000,
      "emitWktShadowProperties": true
    }
  }
}
```

**Options:**
- `enabled` - Enable OData endpoint (default: true)
- `allowWrites` - Allow POST/PUT/DELETE (default: false)
- `defaultPageSize` - Default page size (default: 50)
- `maxPageSize` - Maximum page size (default: 1000)
- `emitWktShadowProperties` - Include WKT geometry fields (default: true)

Endpoint: `https://{host}/odata`

## Server

Server-level configuration.

```json
{
  "honua": {
    "server": {
      "allowedHosts": "*",
      "cors": {
        "allowedOrigins": ["https://example.com"],
        "allowedMethods": ["GET", "POST", "PUT", "DELETE", "PATCH"],
        "allowedHeaders": ["*"],
        "allowCredentials": true,
        "maxAgeSeconds": 3600
      }
    }
  }
}
```

**CORS Configuration:**
```json
"cors": {
  "allowedOrigins": ["https://app.example.com", "https://admin.example.com"],
  "allowedMethods": ["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS"],
  "allowedHeaders": ["Authorization", "Content-Type", "Accept"],
  "allowCredentials": true,
  "maxAgeSeconds": 7200
}
```

Use `["*"]` to allow all origins (development only).

## Observability

Logging, metrics, and monitoring.

```json
{
  "observability": {
    "logging": {
      "jsonConsole": true,
      "includeScopes": true
    },
    "metrics": {
      "enabled": true,
      "endpoint": "/metrics",
      "usePrometheus": true
    }
  }
}
```

### Logging

```json
"logging": {
  "jsonConsole": true,
  "includeScopes": true,
  "logLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Honua": "Debug"
  }
}
```

**Options:**
- `jsonConsole` - Output structured JSON logs (default: true)
- `includeScopes` - Include log scopes (default: true)

### Metrics

Prometheus-compatible metrics endpoint.

```json
"metrics": {
  "enabled": true,
  "endpoint": "/metrics",
  "usePrometheus": true
}
```

Endpoint: `https://{host}/metrics`

**Available Metrics:**
- `honua_requests_total` - Total HTTP requests
- `honua_request_duration_seconds` - Request duration histogram
- `honua_features_returned_total` - Features returned per query
- `honua_cache_hits_total` - Tile cache hits
- `honua_cache_misses_total` - Tile cache misses

See [Monitoring Guide](monitoring.md) for detailed metrics reference.

## Environment Variables

All configuration can be overridden with environment variables using the pattern:
```
HONUA__SECTION__SUBSECTION__KEY=value
```

### Common Environment Variables

**Metadata:**
```bash
HONUA__METADATA__PROVIDER=json
HONUA__METADATA__PATH=/app/metadata/services.json
```

**Authentication:**
```bash
HONUA__AUTHENTICATION__MODE=Local
HONUA__AUTHENTICATION__ENFORCE=true
HONUA__AUTHENTICATION__LOCAL__SESSIONLIFETIMEMINUTES=480
```

**OIDC:**
```bash
HONUA__AUTHENTICATION__MODE=Oidc
HONUA__AUTHENTICATION__JWT__AUTHORITY=https://login.microsoftonline.com/{tenant}/v2.0
HONUA__AUTHENTICATION__JWT__AUDIENCE=api://honua
```

**Services:**
```bash
HONUA__SERVICES__WFS__ENABLED=true
HONUA__SERVICES__WMS__ENABLED=true
HONUA__SERVICES__STAC__ENABLED=false
HONUA__SERVICES__GEOMETRY__ENABLED=true
```

**Database Connection (use in metadata):**
```bash
HONUA_POSTGIS_CONN="Host=localhost;Database=gis;Username=postgres;Password=secret"
HONUA_SQLSERVER_CONN="Server=localhost;Database=gis;User Id=sa;Password=secret;TrustServerCertificate=true"
```

Reference in metadata via:
```json
"connectionSecret": "env:HONUA_POSTGIS_CONN"
```

### Docker Environment

Complete Docker example:
```bash
docker run -d \
  -e HONUA__METADATA__PROVIDER=json \
  -e HONUA__METADATA__PATH=/app/metadata/services.json \
  -e HONUA__AUTHENTICATION__MODE=QuickStart \
  -e HONUA__AUTHENTICATION__ENFORCE=false \
  -e HONUA_POSTGIS_CONN="Host=postgres;Database=gis;Username=honua;Password=secret" \
  -v $(pwd)/metadata:/app/metadata \
  -p 5000:8080 \
  honua/honua:latest
```

## Complete Example

Production configuration example:

```json
{
  "honua": {
    "metadata": {
      "provider": "json",
      "path": "/app/metadata/production.json"
    },
    "authentication": {
      "mode": "Oidc",
      "enforce": true,
      "jwt": {
        "authority": "https://login.microsoftonline.com/{tenant-id}/v2.0",
        "audience": "api://honua-production",
        "roleClaimPath": "roles",
        "requireHttpsMetadata": true
      }
    },
    "services": {
      "wfs": { "enabled": true },
      "wms": { "enabled": true },
      "stac": {
        "enabled": true,
        "provider": "postgres",
        "connectionString": "env:STAC_DB"
      },
      "geometry": {
        "enabled": true,
        "maxGeometries": 50,
        "maxCoordinateCount": 5000,
        "enableGdalOperations": false
      },
      "rasterTiles": {
        "enabled": true,
        "provider": "s3",
        "s3": {
          "bucket": "honua-prod-tiles",
          "region": "us-east-1",
          "accessKeyId": "env:AWS_ACCESS_KEY_ID",
          "secretAccessKey": "env:AWS_SECRET_ACCESS_KEY",
          "prefix": "tiles/"
        }
      }
    },
    "attachments": {
      "defaultMaxSizeMiB": 25,
      "profiles": [{
        "name": "default",
        "provider": "s3",
        "s3": {
          "bucket": "honua-prod-attachments",
          "region": "us-east-1",
          "accessKeyId": "env:AWS_ACCESS_KEY_ID",
          "secretAccessKey": "env:AWS_SECRET_ACCESS_KEY"
        }
      }]
    },
    "odata": {
      "enabled": true,
      "allowWrites": false,
      "maxPageSize": 500
    },
    "server": {
      "cors": {
        "allowedOrigins": ["https://maps.example.com"],
        "allowedMethods": ["GET", "POST", "PUT", "DELETE", "PATCH"],
        "allowCredentials": true
      }
    }
  },
  "observability": {
    "logging": {
      "jsonConsole": true
    },
    "metrics": {
      "enabled": true,
      "endpoint": "/metrics"
    }
  }
}
```

## See Also

- [Authentication Guide](authentication.md) - Detailed authentication setup
- [Metadata Authoring](metadata-authoring.md) - Layer and service configuration
- [Administrative API](admin-api.md) - Management endpoints
- [Monitoring](monitoring.md) - Metrics and observability
