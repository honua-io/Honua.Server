---
tags: [configuration, appsettings, setup, authentication, storage, cache, rate-limiting, observability]
category: configuration
difficulty: beginner
version: 1.0.0
last_updated: 2025-10-15
---

# Honua Server Configuration Reference

Complete guide to all configuration options in appsettings.json with examples and best practices.

## Table of Contents
- [Configuration Files](#configuration-files)
- [Logging Configuration](#logging-configuration)
- [Observability](#observability)
- [Rate Limiting](#rate-limiting)
- [Request Limits](#request-limits)
- [Authentication](#authentication)
- [OData Configuration](#odata-configuration)
- [CORS Configuration](#cors-configuration)
- [Storage Configuration](#storage-configuration)
- [Cache Configuration](#cache-configuration)
- [Schema Validation](#schema-validation)
- [Kestrel Web Server](#kestrel-web-server)
- [Environment Variables](#environment-variables)
- [Related Documentation](#related-documentation)

## Configuration Files

Honua uses ASP.NET Core's configuration system with hierarchical JSON files:

```
appsettings.json              # Base configuration
appsettings.Development.json  # Development overrides
appsettings.Production.json   # Production overrides
appsettings.Example.json      # Documented example
```

**Location:** `/src/Honua.Server.Host/appsettings.json`

### Configuration Hierarchy

Settings are merged in this order (later overrides earlier):
1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables
4. Command-line arguments

## Logging Configuration

Controls ASP.NET Core logging output.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.Hosting.Diagnostics": "Information",
      "System.Net.Http.HttpClient": "Warning"
    }
  }
}
```

### Log Levels

| Level | Description | Use Case |
|-------|-------------|----------|
| `Trace` | Most detailed | Deep debugging |
| `Debug` | Debugging info | Development |
| `Information` | General info | Production default |
| `Warning` | Potential issues | Production |
| `Error` | Errors/exceptions | Always log |
| `Critical` | Fatal failures | Always log |
| `None` | Disable logging | Never use |

### Examples

**Development - Verbose Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Honua.Server": "Trace"
    }
  }
}
```

**Production - Minimal Logging:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Honua.Server": "Information"
    }
  }
}
```

## Observability

Comprehensive observability configuration for metrics, logging, and tracing.

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
    },
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://localhost:4317"
    }
  }
}
```

### Logging Options

```json
{
  "observability": {
    "logging": {
      "jsonConsole": true,
      "includeScopes": true
    }
  }
}
```

**Parameters:**
- `jsonConsole` (bool): Use JSON format (true) or text format (false)
- `includeScopes` (bool): Include logging scopes for context

**Use Cases:**
- JSON logging: Production, log aggregation (Elasticsearch, CloudWatch)
- Text logging: Local development, human-readable

### Metrics Options

```json
{
  "observability": {
    "metrics": {
      "enabled": true,
      "endpoint": "/metrics",
      "usePrometheus": true
    }
  }
}
```

**Parameters:**
- `enabled` (bool): Enable metrics collection
- `endpoint` (string): HTTP endpoint for scraping (default: `/metrics`)
- `usePrometheus` (bool): Export in Prometheus format

**Example: Scrape Metrics**
```bash
# Access metrics endpoint
curl http://localhost:5000/metrics

# Example output:
# honua_api_requests_total{method="GET",endpoint="/ogc/collections"} 1234
# honua_features_returned_total{service="my-service",layer="cities"} 5678
```

### Tracing Options

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

**Parameters:**
- `exporter` (string): Trace exporter type
  - `none`: Disable tracing
  - `console`: Console output (development)
  - `otlp`: OpenTelemetry Protocol (production)
- `otlpEndpoint` (string): OTLP gRPC endpoint URL

**Supported Backends:**
- Jaeger
- Tempo (Grafana)
- Azure Application Insights
- AWS X-Ray
- Google Cloud Trace

## Rate Limiting

Protect against abuse and DoS attacks with configurable rate limits.

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
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 20
    },
    "OpenRosa": {
      "PermitLimit": 50,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 5
    },
    "Geoservices": {
      "PermitLimit": 150,
      "WindowMinutes": 1,
      "SegmentsPerWindow": 4,
      "QueueLimit": 15
    }
  }
}
```

### Parameters

- `Enabled` (bool): Enable/disable rate limiting globally
- `PermitLimit` (int): Max requests per window
- `WindowMinutes` (int): Time window in minutes
- `SegmentsPerWindow` (int): Sliding window segments (granularity)
- `QueueLimit` (int): Max queued requests when limit reached

### Rate Limiting Policies

| Policy | Default Limit | Use Case |
|--------|---------------|----------|
| `Default` | 100/min | General endpoints |
| `OgcApi` | 200/min | OGC API Features (read-heavy) |
| `OpenRosa` | 50/min | Form submissions (write-heavy) |
| `Geoservices` | 150/min | Esri REST API |

### Examples

**Strict Rate Limiting (Public API):**
```json
{
  "RateLimiting": {
    "Enabled": true,
    "OgcApi": {
      "PermitLimit": 60,
      "WindowMinutes": 1,
      "QueueLimit": 5
    }
  }
}
```

**Relaxed Rate Limiting (Internal Network):**
```json
{
  "RateLimiting": {
    "Enabled": true,
    "OgcApi": {
      "PermitLimit": 1000,
      "WindowMinutes": 1,
      "QueueLimit": 100
    }
  }
}
```

**Disable Rate Limiting (Development):**
```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

## Request Limits

Control maximum request body sizes.

```json
{
  "RequestLimits": {
    "MaxBodySize": 1073741824
  }
}
```

**Parameters:**
- `MaxBodySize` (long): Maximum request body size in bytes

**Common Values:**
- 1 MB: `1048576`
- 10 MB: `10485760`
- 100 MB: `104857600`
- 1 GB: `1073741824` (default)

**Example: Limit to 50MB:**
```json
{
  "RequestLimits": {
    "MaxBodySize": 52428800
  }
}
```

## Authentication

Configure authentication modes and security policies.

```json
{
  "honua": {
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
      }
    }
  }
}
```

### Authentication Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| `Local` | Username/password | Most deployments |
| `Jwt` | JWT bearer tokens | API integrations |
| `QuickStart` | No authentication | Development ONLY |
| `None` | Disabled | Not recommended |

### QuickStart Mode

**WARNING:** QuickStart bypasses ALL authentication. Never use in production.

```json
{
  "honua": {
    "authentication": {
      "mode": "QuickStart",
      "quickStart": {
        "enabled": true
      }
    }
  }
}
```

**Requires:**
- Environment: `ASPNETCORE_ENVIRONMENT != Production`
- Flag: `HONUA_ALLOW_QUICKSTART=true` (environment variable)

**Development Usage:**
```bash
# Enable QuickStart mode
export HONUA_ALLOW_QUICKSTART=true
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### Local Authentication

Username/password with session management.

```json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetime": "08:00:00",
        "minPasswordLength": 12,
        "requireUppercase": true,
        "requireLowercase": true,
        "requireDigit": true,
        "requireSpecialCharacter": true
      }
    }
  }
}
```

**Password Complexity Examples:**

**High Security:**
```json
{
  "local": {
    "minPasswordLength": 16,
    "requireUppercase": true,
    "requireLowercase": true,
    "requireDigit": true,
    "requireSpecialCharacter": true
  }
}
```

**Standard Security:**
```json
{
  "local": {
    "minPasswordLength": 12,
    "requireUppercase": true,
    "requireLowercase": true,
    "requireDigit": true,
    "requireSpecialCharacter": false
  }
}
```

### JWT Authentication

Token-based authentication for APIs.

```json
{
  "honua": {
    "authentication": {
      "mode": "Jwt",
      "jwt": {
        "issuer": "Honua.Server",
        "audience": "Honua.Clients",
        "signingKey": "${JWT_SIGNING_KEY}",
        "expirationMinutes": 60,
        "clockSkewMinutes": 5
      }
    }
  }
}
```

**Usage Example:**
```bash
# Request with JWT token
curl -H "Authorization: Bearer eyJhbGc..." \
  http://localhost:5000/ogc/collections
```

**Generate Signing Key:**
```bash
# Generate 256-bit key
openssl rand -base64 32
```

## OData Configuration

Configure OData query capabilities.

```json
{
  "honua": {
    "odata": {
      "enabled": true,
      "allowWrites": false,
      "defaultPageSize": 100,
      "maxPageSize": 1000,
      "emitWktShadowProperties": true
    }
  }
}
```

**Parameters:**
- `enabled` (bool): Enable OData endpoints
- `allowWrites` (bool): Allow POST/PATCH/DELETE operations
- `defaultPageSize` (int): Default page size for queries
- `maxPageSize` (int): Maximum allowed page size
- `emitWktShadowProperties` (bool): Include WKT geometry in responses

**Example OData Query:**
```bash
# Query with filtering and paging
curl "http://localhost:5000/odata/Cities?\$filter=population gt 100000&\$top=10"
```

## CORS Configuration

Cross-Origin Resource Sharing settings.

```json
{
  "honua": {
    "cors": {
      "allowAnyOrigin": false,
      "allowCredentials": true
    }
  }
}
```

**Parameters:**
- `allowAnyOrigin` (bool): Allow requests from any origin
- `allowCredentials` (bool): Allow credentials in CORS requests

**Note:** Per-service CORS policies are defined in `metadata.yaml`.

**Example Metadata CORS:**
```yaml
services:
  - id: my-service
    cors:
      allowedOrigins:
        - https://example.com
        - https://app.example.com
      allowedMethods: [GET, POST, PUT, DELETE]
      allowedHeaders: [Content-Type, Authorization]
```

## Storage Configuration

Configure cloud storage for rasters and attachments.

```json
{
  "honua": {
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
    }
  }
}
```

### Storage Types

| Type | Use Case | Configuration |
|------|----------|---------------|
| `FileSystem` | Development, on-premises | Local directory path |
| `AzureBlob` | Azure deployments | Connection string + container |
| `S3` | AWS deployments | Access keys + bucket |

### FileSystem Storage

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

**Directory Structure:**
```
./data/storage/
├── rasters/
│   ├── cog/
│   └── zarr/
└── attachments/
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

**Environment Variable:**
```bash
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=..."
```

### AWS S3 Storage

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

**Environment Variables:**
```bash
export AWS_ACCESS_KEY_ID="AKIAIOSFODNN7EXAMPLE"
export AWS_SECRET_ACCESS_KEY="wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
```

## Cache Configuration

Tile cache for performance optimization.

```json
{
  "honua": {
    "cache": {
      "enabled": true,
      "provider": "FileSystem",
      "basePath": "./data/cache",
      "maxSizeMb": 10240,
      "evictionPolicy": "LRU"
    }
  }
}
```

**Parameters:**
- `enabled` (bool): Enable tile caching
- `provider` (string): Cache backend (`FileSystem`, `Redis`, `Memory`)
- `basePath` (string): Cache directory (FileSystem only)
- `maxSizeMb` (int): Maximum cache size in MB
- `evictionPolicy` (string): Eviction strategy (`LRU`, `LFU`)

### Cache Providers

**FileSystem (Development):**
```json
{
  "cache": {
    "provider": "FileSystem",
    "basePath": "./data/cache",
    "maxSizeMb": 10240
  }
}
```

**Redis (Production):**
```json
{
  "cache": {
    "provider": "Redis",
    "redis": {
      "connectionString": "localhost:6379",
      "instanceName": "honua:"
    }
  }
}
```

**Memory (Testing):**
```json
{
  "cache": {
    "provider": "Memory",
    "maxSizeMb": 512
  }
}
```

## Schema Validation

Database schema validation on startup.

```json
{
  "SchemaValidation": {
    "Enabled": true,
    "FailOnMismatch": false,
    "LogWarnings": true
  }
}
```

**Parameters:**
- `Enabled` (bool): Run validation on startup
- `FailOnMismatch` (bool): Fail startup if schema doesn't match
- `LogWarnings` (bool): Log schema mismatches as warnings

**Recommended Settings:**

**Development:**
```json
{
  "SchemaValidation": {
    "Enabled": true,
    "FailOnMismatch": false,
    "LogWarnings": true
  }
}
```

**Production:**
```json
{
  "SchemaValidation": {
    "Enabled": true,
    "FailOnMismatch": true,
    "LogWarnings": true
  }
}
```

## Kestrel Web Server

ASP.NET Core web server configuration.

```json
{
  "Kestrel": {
    "Limits": {
      "MaxRequestBodySize": 1073741824,
      "MaxConcurrentConnections": 100,
      "MaxConcurrentUpgradedConnections": 100,
      "RequestHeadersTimeout": "00:00:30",
      "KeepAliveTimeout": "00:02:00"
    },
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      },
      "Https": {
        "Url": "https://0.0.0.0:5001",
        "Certificate": {
          "Path": "/path/to/certificate.pfx",
          "Password": "${CERT_PASSWORD}"
        }
      }
    }
  }
}
```

### Performance Tuning

**High Throughput:**
```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 1000,
      "MaxConcurrentUpgradedConnections": 1000,
      "RequestHeadersTimeout": "00:00:10"
    }
  }
}
```

**Resource Constrained:**
```json
{
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 50,
      "MaxRequestBodySize": 10485760
    }
  }
}
```

## Environment Variables

Configuration can be overridden with environment variables using double underscore notation.

### Syntax

```
{Section}__{SubSection}__{Property}=value
```

### Examples

**Override Authentication Mode:**
```bash
export honua__authentication__mode=QuickStart
export HONUA_ALLOW_QUICKSTART=true
```

**Override Database Connection:**
```bash
export ConnectionStrings__DefaultConnection="Server=db;Database=honua;..."
```

**Override Metrics Endpoint:**
```bash
export observability__metrics__endpoint=/prometheus
```

**Override Storage Type:**
```bash
export honua__storage__type=AzureBlob
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=..."
```

### Docker Compose Example

```yaml
services:
  honua:
    image: honua-server
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - honua__authentication__mode=Local
      - observability__metrics__enabled=true
      - ConnectionStrings__DefaultConnection=Server=postgres;...
```

## Complete Example Configurations

### Minimal Development

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "honua": {
    "workspacePath": "./metadata",
    "authentication": {
      "mode": "QuickStart",
      "quickStart": {
        "enabled": true
      }
    },
    "storage": {
      "type": "FileSystem",
      "basePath": "./data"
    }
  }
}
```

### Production with Observability

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
      "otlpEndpoint": "http://tempo:4317"
    }
  },
  "RateLimiting": {
    "Enabled": true,
    "Default": {
      "PermitLimit": 100,
      "WindowMinutes": 1
    }
  },
  "honua": {
    "workspacePath": "/app/metadata",
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "sessionLifetime": "08:00:00",
        "minPasswordLength": 16
      }
    },
    "storage": {
      "type": "AzureBlob",
      "azureBlob": {
        "connectionString": "${AZURE_STORAGE_CONNECTION_STRING}",
        "containerName": "honua-data"
      }
    },
    "cache": {
      "enabled": true,
      "provider": "Redis",
      "redis": {
        "connectionString": "redis:6379"
      }
    }
  },
  "Kestrel": {
    "Limits": {
      "MaxConcurrentConnections": 500
    },
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:8080"
      }
    }
  }
}
```

## Related Documentation

- [Architecture Overview](01-01-architecture-overview.md) - System design
- [Docker Deployment](04-01-docker-deployment.md) - Container deployment
- [Common Issues](05-02-common-issues.md) - Troubleshooting
- [OGC API Features](03-01-ogc-api-features.md) - API usage

## Keywords for Search

configuration, appsettings, setup, environment variables, authentication, JWT, local auth, QuickStart, rate limiting, observability, metrics, tracing, logging, Prometheus, OpenTelemetry, CORS, storage, Azure Blob, S3, cache, Redis, OData, Kestrel, performance tuning, security

---

**Last Updated**: 2025-10-15
**Version**: 1.0.0
**Covers**: Honua Server 1.0.0-rc1
