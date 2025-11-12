# Honua Configuration Guide

Complete configuration reference for Honua Server.

## Configuration Sources

Honua uses ASP.NET Core configuration with the following precedence (highest to lowest):

1. Command-line arguments
2. Environment variables
3. `appsettings.{Environment}.json`
4. `appsettings.json`

## Environment Variables

Use double underscore `__` to represent nested JSON structure:

```bash
# Metadata configuration
export honua__workspacePath=/app/metadata
export honua__metadata__provider=json

# Authentication
export honua__authentication__mode=OIDC
export honua__authentication__jwt__authority=https://auth.example.com

# Database connection
export ConnectionStrings__DefaultConnection="Host=db;Database=honua;..."

# Observability
export honua__observability__metrics__enabled=true
export honua__observability__tracing__exporter=otlp
```

## appsettings.json Structure

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },

  "observability": {
    "logging": {
      "jsonConsole": true,
      "includeScopes": true
    },
    "metrics": {
      "enabled": true,
      "endpoint": "/metrics",
      "usePrometheus": true
    },
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://jaeger:4317",
      "samplingRatio": 0.1
    }
  },

  "ConnectionStrings": {
    "DefaultConnection": "Host=db;Database=honua;Username=user;Password=pass"
  },

  "honua": {
    "workspacePath": "./metadata",

    "authentication": {
      "mode": "Local",
      "enforce": true,

      "quickStart": {
        "enabled": false
      },

      "local": {
        "sessionLifetime": "08:00:00",
        "requireEmailVerification": false,
        "minPasswordLength": 12,
        "requireUppercase": true,
        "requireLowercase": true,
        "requireDigit": true,
        "requireSpecialCharacter": true
      },

      "jwt": {
        "issuer": "Honua.Server",
        "audience": "Honua.Clients",
        "signingKey": "${JWT_SIGNING_KEY}",
        "expirationMinutes": 60,
        "clockSkewMinutes": 5
      },

      "oidc": {
        "authority": "https://auth.example.com",
        "clientId": "honua-client",
        "clientSecret": "${OIDC_CLIENT_SECRET}",
        "responseType": "code",
        "scopes": ["openid", "profile", "email"]
      }
    },

    "odata": {
      "enabled": true,
      "allowWrites": false,
      "defaultPageSize": 100,
      "maxPageSize": 1000,
      "emitWktShadowProperties": true
    },

    "cors": {
      "allowAnyOrigin": false,
      "allowCredentials": true
    },

    "storage": {
      "type": "FileSystem",
      "basePath": "./data/storage",

      "azureBlob": {
        "connectionString": "${AZURE_STORAGE_CONNECTION_STRING}",
        "containerName": "honua-data"
      },

      "s3": {
        "accessKey": "${AWS_ACCESS_KEY_ID}",
        "secretKey": "${AWS_SECRET_ACCESS_KEY}",
        "region": "us-west-2",
        "bucketName": "honua-data"
      }
    },

    "cache": {
      "enabled": true,
      "provider": "FileSystem",
      "basePath": "./data/cache",
      "maxSizeMb": 10240,
      "evictionPolicy": "LRU"
    }
  },

  "RateLimiting": {
    "Enabled": true,
    "Default": {
      "PermitLimit": 100,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 10
    },
    "OgcApi": {
      "PermitLimit": 200,
      "WindowMinutes": 1
    }
  },

  "RequestLimits": {
    "MaxBodySize": 1073741824
  },

  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 1073741824,
      "MaxConcurrentConnections": 100,
      "RequestHeadersTimeout": "00:00:30",
      "KeepAliveTimeout": "00:02:00"
    }
  }
}
```

## Authentication Modes

### QuickStart Mode
**For local development and testing ONLY - NO authentication**

QuickStart mode disables JWT validation and is **BLOCKED in Production environments** for security.

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "enforce": false
    }
  }
}
```

**SECURITY WARNING:**
- QuickStart mode is **automatically blocked** when `ASPNETCORE_ENVIRONMENT=Production`
- Attempting to use QuickStart in Production will throw an exception on startup
- A prominent warning is logged in Development/Staging environments
- **Use Local or OIDC mode for production deployments**

**When to use QuickStart:**
- Local development with `ASPNETCORE_ENVIRONMENT=Development`
- Automated testing environments
- Demo/evaluation installations with no sensitive data

**Safe alternatives for production:**
- **Local Mode** - Username/password with JWT tokens (see below)
- **OIDC Mode** - Integration with Auth0, Okta, Azure AD, Keycloak

### Local Mode (JWT)
**Username/password with JWT tokens**

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetime": "08:00:00",
        "minPasswordLength": 12
      }
    }
  }
}
```

Bootstrap admin user:
```bash
honua auth bootstrap --mode Local
```

### OIDC Mode
**OAuth2/OpenID Connect (Auth0, Keycloak, Azure AD, Okta)**

```json
{
  "honua": {
    "authentication": {
      "mode": "OIDC",
      "enforce": true,
      "oidc": {
        "authority": "https://auth.example.com",
        "clientId": "honua-client",
        "clientSecret": "${OIDC_CLIENT_SECRET}"
      }
    }
  }
}
```

## Database Configuration

### PostgreSQL/PostGIS
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=honua;Username=user;Password=pass;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
  }
}
```

### SQLite/SpatiaLite
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=honua.db"
  }
}
```

### SQL Server
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=honua;User Id=sa;Password=pass;TrustServerCertificate=true"
  }
}
```

## Observability Configuration

**IMPORTANT: Production Defaults**
- Metrics are **ENABLED by default** in `appsettings.Production.json`
- Request logging is **ENABLED by default** in production for audit trails
- Tracing exporter is set to `none` by default (configure OTLP endpoint to enable)

### Metrics

Metrics are enabled by default in production and provide Prometheus-compatible metrics for monitoring.

```json
{
  "observability": {
    "metrics": {
      "enabled": true,
      "_comment": "ENABLED by default in production",
      "usePrometheus": true,
      "endpoint": "/metrics"
    }
  }
}
```

**Access the metrics endpoint:**
- URL: `http://your-server:port/metrics`
- Authentication: Required (Viewer role or higher) except in QuickStart mode
- Format: Prometheus text format

**Environment variable override:**
```bash
export observability__metrics__enabled=false  # Disable if needed
export observability__metrics__endpoint=/custom-metrics
```

### Distributed Tracing

Tracing is configured via the `tracing` section. Set exporter to `otlp` and configure an endpoint for production.

```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://jaeger:4317"
    }
  }
}
```

**Exporter options:**
- `none` - Disabled (default, minimal overhead ~0.5%)
- `console` - Log to stdout (development/debugging, ~1-2% overhead)
- `otlp` - OpenTelemetry Protocol (Jaeger, Tempo, etc., ~2-5% overhead)

**Production tracing setup:**
```bash
# Set via environment variables
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317
```

**Common OTLP endpoints:**
- **Jaeger (local):** `http://jaeger:4317`
- **Tempo (local):** `http://tempo:4317`
- **Grafana Cloud:** `https://otlp-gateway-prod-us-central-0.grafana.net/otlp`
- **Honeycomb:** `https://api.honeycomb.io:443`
- **New Relic:** `https://otlp.nr-data.net:4317`

### Request Logging

Request logging is enabled by default in production for audit trails and troubleshooting.

```json
{
  "observability": {
    "requestLogging": {
      "enabled": true,
      "logHeaders": false,
      "slowThresholdMs": 5000
    }
  }
}
```

**Settings:**
- `enabled`: Log all HTTP requests/responses (default: true in production)
- `logHeaders`: Include request/response headers (default: false, may expose sensitive data)
- `slowThresholdMs`: Log requests exceeding this duration (default: 5000ms)

**Environment variable override:**
```bash
export observability__requestLogging__enabled=false
export observability__requestLogging__slowThresholdMs=3000
```

### Runtime Configuration

Change tracing at runtime via Admin API:
```bash
curl -X PATCH http://localhost:8080/admin/observability/tracing/exporter \
  -H "Content-Type: application/json" \
  -d '{"exporter": "otlp"}'

curl -X PATCH http://localhost:8080/admin/observability/tracing/sampling \
  -H "Content-Type: application/json" \
  -d '{"ratio": 0.1}'
```

Restart required for exporter changes, sampling takes effect immediately.

## Storage Configuration

### File System
```json
{
  "honua": {
    "storage": {
      "type": "FileSystem",
      "basePath": "./data/storage"
    }
  }
}
```

### Azure Blob Storage
```json
{
  "honua": {
    "storage": {
      "type": "AzureBlob",
      "azureBlob": {
        "connectionString": "${AZURE_STORAGE_CONNECTION_STRING}",
        "containerName": "honua-data"
      }
    }
  }
}
```

### AWS S3
```json
{
  "honua": {
    "storage": {
      "type": "S3",
      "s3": {
        "accessKey": "${AWS_ACCESS_KEY_ID}",
        "secretKey": "${AWS_SECRET_ACCESS_KEY}",
        "region": "us-west-2",
        "bucketName": "honua-data"
      }
    }
  }
}
```

## Rate Limiting

```json
{
  "RateLimiting": {
    "Enabled": true,
    "Default": {
      "PermitLimit": 100,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 10
    },
    "OgcApi": {
      "PermitLimit": 200,
      "WindowMinutes": 1
    },
    "OpenRosa": {
      "PermitLimit": 50,
      "WindowMinutes": 1
    }
  }
}
```

## Tile Caching Configuration

Honua supports caching for both raster and vector tiles with multiple storage backends.

### Raster Tile Cache

#### File System (Default)
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "filesystem",
        "fileSystem": {
          "rootPath": "./data/raster-cache"
        },
        "preseed": {
          "batchSize": 32,
          "maxDegreeOfParallelism": 4
        }
      }
    }
  }
}
```

#### AWS S3
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "s3",
        "s3": {
          "bucketName": "honua-raster-tiles",
          "prefix": "tiles/",
          "region": "us-west-2",
          "accessKeyId": "${AWS_ACCESS_KEY_ID}",
          "secretAccessKey": "${AWS_SECRET_ACCESS_KEY}",
          "ensureBucket": false,
          "forcePathStyle": false
        }
      }
    }
  }
}
```

**Environment Variables:**
```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=s3
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__S3__REGION=us-west-2
```

#### Azure Blob Storage
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "azure",
        "azure": {
          "connectionString": "${AZURE_STORAGE_CONNECTION_STRING}",
          "containerName": "raster-tiles",
          "ensureContainer": true
        }
      }
    }
  }
}
```

**Environment Variables:**
```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=azure
HONUA__SERVICES__RASTERTILES__AZURE__CONNECTIONSTRING="DefaultEndpointsProtocol=https;..."
HONUA__SERVICES__RASTERTILES__AZURE__CONTAINERNAME=raster-tiles
```

#### Google Cloud Storage
```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "enabled": true,
        "provider": "gcs",
        "gcs": {
          "bucketName": "honua-raster-tiles",
          "prefix": "tiles/",
          "projectId": "my-gcp-project",
          "credentialsJson": "${GCS_CREDENTIALS_JSON}",
          "ensureBucket": false
        }
      }
    }
  }
}
```

**Environment Variables:**
```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=gcs
HONUA__SERVICES__RASTERTILES__GCS__BUCKETNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__GCS__PROJECTID=my-project
HONUA__SERVICES__RASTERTILES__GCS__CREDENTIALSJSON='{"type":"service_account",...}'

# Or use Application Default Credentials
GOOGLE_APPLICATION_CREDENTIALS=/path/to/service-account-key.json
```

### Vector Tile Cache

Vector tile configuration follows the same pattern as raster tiles:

```json
{
  "honua": {
    "services": {
      "vectorTiles": {
        "enabled": true,
        "provider": "gcs",  // or "filesystem", "s3", "azure"
        "gcs": {
          "bucketName": "honua-vector-tiles",
          "prefix": "vector/",
          "projectId": "my-gcp-project",
          "ensureBucket": false
        }
      }
    }
  }
}
```

### Tile Preseed Configuration

Control parallel tile generation:

```json
{
  "honua": {
    "services": {
      "rasterTiles": {
        "preseed": {
          "batchSize": 32,              // Tiles per batch
          "maxDegreeOfParallelism": 4   // Concurrent workers
        }
      }
    }
  }
}
```

**Recommendations:**
- **CPU-bound** (complex rendering): `maxDegreeOfParallelism = CPU cores - 1`
- **I/O-bound** (network storage): `maxDegreeOfParallelism = 2-4x CPU cores`
- **8-core server**: `batchSize: 32, maxDegreeOfParallelism: 6`

## GitOps Configuration

Enable write-through to Git for runtime configuration changes:

```json
{
  "GitOps": {
    "Enabled": true,
    "RepositoryPath": "/var/honua/config",
    "Branch": "main",
    "AppsettingsPath": "appsettings.json",
    "MetadataPath": "metadata.json",
    "AutoPush": true,
    "TriggerReconciliation": true,
    "GitUsername": "honua-server",
    "GitEmail": "honua@example.com",
    "GitPassword": "${GIT_TOKEN}"
  }
}
```

**Environment Variables:**
```bash
GITOPS__ENABLED=true
GITOPS__REPOSITORYPATH=/var/honua/config
GITOPS__GITPASSWORD="${GIT_TOKEN}"
```

When enabled:
- ✅ Runtime API changes persist to Git
- ✅ Full audit trail via Git commits
- ✅ Rollback capability with `git revert`
- ✅ Changes survive restarts

When disabled (legacy mode):
- ⚠️ Changes are in-memory only
- ⚠️ Lost on restart
- ⚠️ No audit trail

## CORS Configuration

CORS policies are defined per-service in metadata.json:

```json
{
  "services": [
    {
      "id": "my-service",
      "cors": {
        "allowedOrigins": ["https://example.com"],
        "allowedMethods": ["GET", "POST"],
        "allowedHeaders": ["Content-Type", "Authorization"],
        "allowCredentials": true
      }
    }
  ]
}
```

## Complete Example

See `src/Honua.Server.Host/appsettings.Example.json` for a fully documented configuration template with all available options.

---

## See Also

- **[Deployment Guide](../deployment/)** - Deployment-specific configuration
- **[Storage Integration Tests](../../tests/Honua.Server.Core.Tests/STORAGE_INTEGRATION_TESTS.md)** - Testing tile cache providers with emulators
- **[Tile Caching Architecture](../archive/2025-10-15/rag/03-architecture/tile-caching.md)** - Detailed tile caching implementation
- **[GitOps Write-Through Documentation](../gitops-write-through.md)** - GitOps configuration persistence
