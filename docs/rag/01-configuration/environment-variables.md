# Honua Environment Variables Reference

**Keywords**: configuration, environment, settings, env vars, deployment, docker, kubernetes
**Related**: appsettings.json, Configuration Options, Docker Deployment, Kubernetes Setup

## Overview

Honua supports comprehensive configuration through environment variables, following the ASP.NET Core configuration hierarchy. Environment variables take precedence over `appsettings.json` files, making them ideal for containerized deployments, Kubernetes, and cloud environments.

## Configuration Hierarchy

Configuration sources in order of precedence (highest to lowest):

1. **Environment Variables** (highest priority)
2. **Command-line arguments**
3. **appsettings.{Environment}.json**
4. **appsettings.json**
5. **Default values in code** (lowest priority)

## Environment Variable Naming Convention

Honua follows ASP.NET Core conventions:
- Use double underscore `__` to represent nested sections
- Case-insensitive (but conventionally UPPER_CASE)
- Prefixed with `HONUA__` for Honua-specific settings

**Examples**:
```bash
# JSON: honua.metadata.provider
HONUA__METADATA__PROVIDER=json

# JSON: honua.metadata.path
HONUA__METADATA__PATH=/app/config/metadata.json

# JSON: honua.authentication.mode
HONUA__AUTHENTICATION__MODE=Local
```

## Core Configuration Variables

### Metadata Configuration

**Required**: Yes

```bash
# Metadata provider type: "json", "yaml", "database"
HONUA__METADATA__PROVIDER=json

# Path to metadata file or database connection
HONUA__METADATA__PATH=/app/config/metadata.json
```

**Valid Provider Values**:
- `json` - JSON metadata file (recommended for simple deployments)
- `yaml` - YAML metadata file (supports multi-file configuration)
- `database` - Store metadata in PostgreSQL/SQL Server

**Example Configurations**:

```bash
# JSON provider (simple)
HONUA__METADATA__PROVIDER=json
HONUA__METADATA__PATH=samples/ogc/metadata.json

# YAML provider (multi-file support)
HONUA__METADATA__PROVIDER=yaml
HONUA__METADATA__PATH=config/metadata.yaml

# Database provider
HONUA__METADATA__PROVIDER=database
HONUA__METADATA__PATH="Host=postgres;Database=honua_metadata;Username=honua;Password=secure_password"
```

### Authentication Configuration

> ⚠️ **QuickStart** is for local development only. Always use `Local` or `Oidc` in shared/staging/production environments.

```bash
# Authentication mode: "Local", "Oidc" (QuickStart = dev only)
HONUA__AUTHENTICATION__MODE=Local

# Enforce authentication (true/false)
HONUA__AUTHENTICATION__ENFORCE=true

# QuickStart mode enabled (bypass auth for testing)
HONUA__AUTHENTICATION__QUICKSTART__ENABLED=false
```

**Authentication Modes**:

| Mode | Description | Use Case |
|------|-------------|----------|
| `Local` | Built-in authentication | Production deployments |
| `OAuth` | OAuth2/OIDC integration | Enterprise SSO |
| `QuickStart` | No authentication (bypass) | Development/testing only |

**Security Warning**: Never set `HONUA__AUTHENTICATION__QUICKSTART__ENABLED=true` in production.

### Database Connection

```bash
# Primary database connection string
ConnectionStrings__DefaultConnection="Host=postgis;Port=5432;Database=geodata;Username=honua;Password=honua123"

# Read-only replica (optional)
ConnectionStrings__ReadOnlyConnection="Host=postgis-replica;Port=5432;Database=geodata;Username=readonly;Password=readonly123"
```

**Supported Databases**:
- PostgreSQL with PostGIS (recommended)
- SQL Server with spatial extensions
- SQLite with SpatialLite (development only)

**Connection String Templates**:

```bash
# PostgreSQL/PostGIS
"Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass;Pooling=true;MinPoolSize=5;MaxPoolSize=100"

# SQL Server
"Server=localhost;Database=mydb;User Id=user;Password=pass;MultipleActiveResultSets=true"

# SQLite (dev only)
"Data Source=/app/data/honua.db"
```

### OData Configuration

```bash
# Enable OData endpoints
HONUA__ODATA__ENABLED=true

# Allow write operations (POST, PUT, PATCH, DELETE)
HONUA__ODATA__ALLOWWRITES=false

# Default page size for collections
HONUA__ODATA__DEFAULTPAGESIZE=100

# Maximum page size ($top)
HONUA__ODATA__MAXPAGESIZE=1000

# Emit WKT shadow properties
HONUA__ODATA__EMITWKTSHADOWPROPERTIES=true
```

### Service Configuration

#### WFS (Web Feature Service)

```bash
# Enable WFS endpoints
HONUA__SERVICES__WFS__ENABLED=true
```

#### WMS (Web Map Service)

```bash
# Enable WMS endpoints
HONUA__SERVICES__WMS__ENABLED=true
```

#### Geometry Service

```bash
# Enable geometry operations
HONUA__SERVICES__GEOMETRY__ENABLED=true

# Maximum geometries per request
HONUA__SERVICES__GEOMETRY__MAXGEOMETRIES=1000

# Maximum coordinate count per geometry
HONUA__SERVICES__GEOMETRY__MAXCOORDINATECOUNT=100000

# Enable GDAL-based operations
HONUA__SERVICES__GEOMETRY__ENABLEGDALOPERATIONS=true

# Allowed SRIDs (comma-separated, empty = all)
HONUA__SERVICES__GEOMETRY__ALLOWEDSRIDS=4326,3857,2263
```

#### STAC (SpatioTemporal Asset Catalog)

```bash
# Enable STAC catalog
HONUA__SERVICES__STAC__ENABLED=true

# STAC storage provider: "sqlite", "postgres"
HONUA__SERVICES__STAC__PROVIDER=sqlite

# Connection string for STAC database
HONUA__SERVICES__STAC__CONNECTIONSTRING="Data Source=/app/data/stac.db"

# File path for SQLite
HONUA__SERVICES__STAC__FILEPATH=/app/data/stac.db
```

### Raster Tile Cache Configuration

```bash
# Enable raster tile caching
HONUA__SERVICES__RASTERTILES__ENABLED=true

# Cache provider: "filesystem", "s3", "azure"
HONUA__SERVICES__RASTERTILES__PROVIDER=filesystem

# Filesystem cache root
HONUA__SERVICES__RASTERTILES__FILESYSTEM__ROOTPATH=/app/data/raster-cache

# Preseed configuration
HONUA__SERVICES__RASTERTILES__PRESEED__BATCHSIZE=32
HONUA__SERVICES__RASTERTILES__PRESEED__MAXDEGREEOFPARALLELISM=4
```

#### S3 Raster Tile Cache

```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=s3

# S3 bucket configuration
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__S3__PREFIX=raster/
HONUA__SERVICES__RASTERTILES__S3__REGION=us-east-1

# S3 endpoint (for MinIO/LocalStack)
HONUA__SERVICES__RASTERTILES__S3__SERVICEURL=http://minio:9000

# S3 credentials (if not using IAM role)
HONUA__SERVICES__RASTERTILES__S3__ACCESSKEYID=minioaccess
HONUA__SERVICES__RASTERTILES__S3__SECRETACCESSKEY=miniosecret

# Path-style URLs (for MinIO)
HONUA__SERVICES__RASTERTILES__S3__FORCEPATHSTYLE=true

# Auto-create bucket
HONUA__SERVICES__RASTERTILES__S3__ENSUREBUCKET=true
```

#### Azure Blob Raster Tile Cache

```bash
HONUA__SERVICES__RASTERTILES__PROVIDER=azure

# Azure Blob storage
HONUA__SERVICES__RASTERTILES__AZURE__CONNECTIONSTRING="DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net"
HONUA__SERVICES__RASTERTILES__AZURE__CONTAINERNAME=honua-tiles
HONUA__SERVICES__RASTERTILES__AZURE__ENSURECONTAINER=true
```

### Attachment Storage Configuration

```bash
# Default max attachment size (MiB)
HONUA__ATTACHMENTS__DEFAULTMAXSIZEMIB=25

# Storage profile
HONUA__ATTACHMENTS__PROFILES__DEFAULT__PROVIDER=filesystem
HONUA__ATTACHMENTS__PROFILES__DEFAULT__FILESYSTEM__ROOTPATH=/app/data/attachments
```

#### S3 Attachment Storage

```bash
HONUA__ATTACHMENTS__PROFILES__DEFAULT__PROVIDER=s3
HONUA__ATTACHMENTS__PROFILES__DEFAULT__S3__BUCKETNAME=honua-attachments
HONUA__ATTACHMENTS__PROFILES__DEFAULT__S3__PREFIX=files/
HONUA__ATTACHMENTS__PROFILES__DEFAULT__S3__REGION=us-east-1
HONUA__ATTACHMENTS__PROFILES__DEFAULT__S3__USEINSTANCEPROFILE=true
HONUA__ATTACHMENTS__PROFILES__DEFAULT__S3__PRESIGNEXPIRYSECONDS=900
```

### Logging Configuration

```bash
# Serilog minimum log level
Serilog__MinimumLevel__Default=Information

# Override for specific namespaces
Serilog__MinimumLevel__Override__Microsoft=Warning
Serilog__MinimumLevel__Override__System=Warning

# Seq logging endpoint
Serilog__WriteTo__1__Name=Seq
Serilog__WriteTo__1__Args__serverUrl=http://seq:5341
Serilog__WriteTo__1__Args__apiKey=your-api-key
```

### Observability Configuration

```bash
# JSON console logging
OBSERVABILITY__LOGGING__JSONCONSOLE=true
OBSERVABILITY__LOGGING__INCLUDESCOPES=true

# Metrics endpoint
OBSERVABILITY__METRICS__ENABLED=true
OBSERVABILITY__METRICS__ENDPOINT=/metrics
OBSERVABILITY__METRICS__USEPROMETHEUS=true

# Distributed tracing
OBSERVABILITY__TRACING__ENABLED=true
OBSERVABILITY__TRACING__ENDPOINT=http://jaeger:4317
OBSERVABILITY__TRACING__SAMPLERATIO=0.1
```

### ASP.NET Core Settings

```bash
# URLs to listen on
ASPNETCORE_URLS=http://0.0.0.0:5000;https://0.0.0.0:5001

# Environment name
ASPNETCORE_ENVIRONMENT=Production

# HTTPS certificate
ASPNETCORE_Kestrel__Certificates__Default__Path=/app/certs/aspnetcore.pfx
ASPNETCORE_Kestrel__Certificates__Default__Password=cert_password

# Allowed hosts
ALLOWEDHOSTS=*
```

## Docker Deployment Examples

### Basic Docker Run

```bash
docker run -d \
  --name honua \
  -p 5000:5000 \
  -e HONUA__METADATA__PROVIDER=json \
  -e HONUA__METADATA__PATH=/app/config/metadata.json \
  -e ConnectionStrings__DefaultConnection="Host=postgis;Database=geodata;Username=honua;Password=honua123" \
  -e HONUA__AUTHENTICATION__MODE=QuickStart \
  -e HONUA__AUTHENTICATION__QUICKSTART__ENABLED=true \
  -v $(pwd)/metadata.json:/app/config/metadata.json \
  honua/server:latest
```

### Docker Compose

```yaml
version: '3.8'

services:
  honua:
    image: honua/server:latest
    ports:
      - "5000:5000"
    environment:
      HONUA__METADATA__PROVIDER: json
      HONUA__METADATA__PATH: /app/config/metadata.json
      ConnectionStrings__DefaultConnection: "Host=postgis;Database=geodata;Username=honua;Password=honua123"
      HONUA__AUTHENTICATION__MODE: Local
      HONUA__AUTHENTICATION__ENFORCE: "true"
      HONUA__SERVICES__RASTERTILES__PROVIDER: s3
      HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME: honua-tiles
      HONUA__SERVICES__RASTERTILES__S3__REGION: us-east-1
      OBSERVABILITY__METRICS__ENABLED: "true"
    volumes:
      - ./metadata.json:/app/config/metadata.json
      - ./data:/app/data
    depends_on:
      - postgis

  postgis:
    image: postgis/postgis:latest
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua123
      POSTGRES_DB: geodata
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

## Kubernetes Deployment Examples

### ConfigMap for Non-Sensitive Config

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: honua-config
  namespace: production
data:
  HONUA__METADATA__PROVIDER: "json"
  HONUA__METADATA__PATH: "/app/config/metadata.json"
  HONUA__AUTHENTICATION__MODE: "Local"
  HONUA__AUTHENTICATION__ENFORCE: "true"
  HONUA__ODATA__ENABLED: "true"
  HONUA__ODATA__DEFAULTPAGESIZE: "100"
  HONUA__SERVICES__WFS__ENABLED: "true"
  HONUA__SERVICES__WMS__ENABLED: "true"
  HONUA__SERVICES__RASTERTILES__PROVIDER: "s3"
  HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME: "honua-tiles-prod"
  HONUA__SERVICES__RASTERTILES__S3__REGION: "us-east-1"
  OBSERVABILITY__METRICS__ENABLED: "true"
  OBSERVABILITY__LOGGING__JSONCONSOLE: "true"
```

### Secret for Sensitive Config

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: honua-secrets
  namespace: production
type: Opaque
stringData:
  ConnectionStrings__DefaultConnection: "Host=postgis.production.svc.cluster.local;Database=geodata;Username=honua;Password=super_secret_password"
  HONUA__SERVICES__RASTERTILES__S3__ACCESSKEYID: "AKIAIOSFODNN7EXAMPLE"
  HONUA__SERVICES__RASTERTILES__S3__SECRETACCESSKEY: "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY"
```

### Deployment with Environment Variables

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
  namespace: production
spec:
  replicas: 3
  selector:
    matchLabels:
      app: honua-server
  template:
    metadata:
      labels:
        app: honua-server
    spec:
      containers:
      - name: honua
        image: honua/server:1.0.0
        ports:
        - containerPort: 5000
          name: http
        envFrom:
        - configMapRef:
            name: honua-config
        - secretRef:
            name: honua-secrets
        env:
        # Override specific values
        - name: ASPNETCORE_URLS
          value: "http://0.0.0.0:5000"
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        resources:
          requests:
            memory: "512Mi"
            cpu: "250m"
          limits:
            memory: "2Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
```

## AWS ECS Task Definition

```json
{
  "family": "honua-server",
  "containerDefinitions": [
    {
      "name": "honua",
      "image": "honua/server:1.0.0",
      "cpu": 256,
      "memory": 512,
      "essential": true,
      "portMappings": [
        {
          "containerPort": 5000,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "HONUA__METADATA__PROVIDER",
          "value": "json"
        },
        {
          "name": "HONUA__METADATA__PATH",
          "value": "/app/config/metadata.json"
        },
        {
          "name": "HONUA__SERVICES__RASTERTILES__PROVIDER",
          "value": "s3"
        },
        {
          "name": "HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME",
          "value": "honua-tiles-prod"
        },
        {
          "name": "HONUA__SERVICES__RASTERTILES__S3__REGION",
          "value": "us-east-1"
        }
      ],
      "secrets": [
        {
          "name": "ConnectionStrings__DefaultConnection",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:123456789:secret:honua/db-connection"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/honua-server",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "ecs"
        }
      }
    }
  ],
  "requiresCompatibilities": ["FARGATE"],
  "networkMode": "awsvpc",
  "cpu": "256",
  "memory": "512",
  "executionRoleArn": "arn:aws:iam::123456789:role/ecsTaskExecutionRole",
  "taskRoleArn": "arn:aws:iam::123456789:role/honuaTaskRole"
}
```

## Environment-Specific Configurations

### Development

```bash
ASPNETCORE_ENVIRONMENT=Development
HONUA__AUTHENTICATION__MODE=QuickStart
HONUA__AUTHENTICATION__QUICKSTART__ENABLED=true
HONUA__METADATA__PROVIDER=json
HONUA__METADATA__PATH=samples/ogc/metadata.json
ConnectionStrings__DefaultConnection="Host=localhost;Database=honua_dev;Username=dev;Password=dev"
OBSERVABILITY__METRICS__ENABLED=false
```

### Staging

```bash
ASPNETCORE_ENVIRONMENT=Staging
HONUA__AUTHENTICATION__MODE=Local
HONUA__AUTHENTICATION__ENFORCE=true
HONUA__METADATA__PROVIDER=database
HONUA__METADATA__PATH="Host=postgres-staging;Database=honua_metadata;Username=honua;Password=${DB_PASSWORD}"
ConnectionStrings__DefaultConnection="Host=postgis-staging;Database=geodata;Username=honua;Password=${DB_PASSWORD}"
HONUA__SERVICES__RASTERTILES__PROVIDER=s3
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles-staging
OBSERVABILITY__METRICS__ENABLED=true
Serilog__WriteTo__1__Args__serverUrl=http://seq-staging:5341
```

### Production

```bash
ASPNETCORE_ENVIRONMENT=Production
HONUA__AUTHENTICATION__MODE=OAuth
HONUA__AUTHENTICATION__ENFORCE=true
HONUA__METADATA__PROVIDER=database
HONUA__METADATA__PATH="Host=postgres-prod.us-east-1.rds.amazonaws.com;Database=honua_metadata;Username=honua;Password=${DB_PASSWORD}"
ConnectionStrings__DefaultConnection="Host=postgis-prod-primary.us-east-1.rds.amazonaws.com;Database=geodata;Username=honua;Password=${DB_PASSWORD};Pooling=true;MinPoolSize=10;MaxPoolSize=100"
ConnectionStrings__ReadOnlyConnection="Host=postgis-prod-replica.us-east-1.rds.amazonaws.com;Database=geodata;Username=readonly;Password=${DB_PASSWORD};Pooling=true;MinPoolSize=5;MaxPoolSize=50"
HONUA__SERVICES__RASTERTILES__PROVIDER=s3
HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME=honua-tiles-production
HONUA__SERVICES__RASTERTILES__S3__REGION=us-east-1
HONUA__SERVICES__RASTERTILES__S3__USEINSTANCEPROFILE=true
OBSERVABILITY__METRICS__ENABLED=true
OBSERVABILITY__TRACING__ENABLED=true
Serilog__WriteTo__1__Args__serverUrl=https://seq-prod.company.com
Serilog__WriteTo__1__Args__apiKey=${SEQ_API_KEY}
```

## Troubleshooting

### Configuration Not Being Applied

**Problem**: Environment variables don't seem to take effect

**Solution**:
1. Verify correct naming with double underscore `__`
2. Check configuration hierarchy - command line args override env vars
3. Use `--launch-settings false` to disable launchSettings.json
4. Verify container/pod has access to environment variables:
   ```bash
   docker exec honua env | grep HONUA
   kubectl exec honua-pod -- env | grep HONUA
   ```

### Connection String Issues

**Problem**: Database connection fails

**Solution**:
1. Escape special characters in passwords
2. Use quotes around connection strings in shell commands
3. For Kubernetes secrets, use base64 encoding:
   ```bash
   echo -n "Host=postgres;Database=db" | base64
   ```

### S3/Azure Access Issues

**Problem**: Cannot access cloud storage

**Solution**:
1. Verify IAM role/service principal has correct permissions
2. Check bucket/container name and region
3. For local testing (MinIO/LocalStack), set `FORCEPATHSTYLE=true`
4. Verify network connectivity to storage endpoint

## See Also

- [Configuration Options Reference](./configuration-options.md)
- [appsettings.json Guide](./appsettings-guide.md)
- [Docker Deployment Guide](../02-deployment/docker-deployment.md)
- [Kubernetes Deployment Guide](../02-deployment/kubernetes-deployment.md)
- [Security Best Practices](../04-operations/security-best-practices.md)
