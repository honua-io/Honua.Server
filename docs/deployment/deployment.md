# Deployment Guide

Complete guide for deploying Honua Server in production using Docker Compose or Kubernetes.

## Deployment Options

HonuaIO offers **two deployment configurations** optimized for different use cases:

### Full Deployment (Dockerfile)

**Best for:** Traditional deployments, full feature set, long-running containers

**Features:**
- ✅ **All features enabled**: Vector + Raster + Cloud integration
- ✅ GDAL support for raster data (GeoTIFF, COG, satellite imagery)
- ✅ Cloud storage providers (AWS S3, Azure Blob, Google Cloud Storage)
- ✅ OData protocol
- ✅ Advanced geoprocessing

**Specifications:**
- **Base Image:** `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled` (ultra-secure, no shell)
- **Size:** ~150MB
- **Startup Time:** 3-5 seconds (with ReadyToRun optimization)
- **Memory:** Recommended 1GB+ RAM

**When to use:**
- Traditional deployments (Docker Compose, Kubernetes, VMs)
- Need raster data processing (satellite imagery, elevation data)
- Require cloud storage integration
- Long-running containers with infrequent restarts

### Lite Deployment (Dockerfile.lite)

**Best for:** Serverless platforms, fast cold starts, vector-only workloads

**Features:**
- ✅ **Vector data**: GeoJSON, Shapefiles, GeoPackage, PostGIS
- ✅ **OData protocol** support
- ✅ **Vector tiles** and basic rendering
- ✅ **OGC APIs** (WFS, WMTS vector)
- ❌ **No GDAL** (no raster data sources)
- ❌ **No cloud SDKs** (no direct S3/Blob/GCS integration)

**Specifications:**
- **Base Image:** `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` (lightweight)
- **Size:** ~50-60MB (**60% smaller** than Full)
- **Startup Time:** <2 seconds (**50%+ faster** than Full)
- **Memory:** Runs on 256MB-512MB RAM

**When to use:**
- **Serverless**: Google Cloud Run, AWS Lambda (Container), Azure Container Apps
- **Cost optimization**: Faster cold starts = lower serverless costs
- **Vector-only data**: No raster processing needed
- **High-scale deployments**: Smaller images = faster scaling

### Quick Comparison

| Feature | Full | Lite |
|---------|------|------|
| **Image Size** | ~150MB | ~50-60MB |
| **Cold Start** | 3-5s | <2s |
| **Vector Data** | ✅ | ✅ |
| **Raster Data (GDAL)** | ✅ | ❌ |
| **Cloud Storage SDKs** | ✅ | ❌ |
| **OData Protocol** | ✅ | ✅ |
| **Memory Usage** | 500MB-2GB | 256MB-512MB |
| **Startup Optimization** | ReadyToRun | ReadyToRun |
| **Base Image** | Chiseled (no shell) | Alpine (has shell) |
| **Security** | Maximum | High |
| **Best For** | Traditional deployments | Serverless |

### Building Images

**Full deployment:**
```bash
docker build -t honua:latest .
```

**Lite deployment:**
```bash
docker build -t honua:lite -f Dockerfile.lite .
```

### Feature Detection

HonuaIO automatically detects available features at runtime and adjusts accordingly:

**Full deployment logs:**
```
✅ Raster data sources enabled (GDAL available)
✅ Cloud storage providers enabled (AWS/Azure/GCP SDKs loaded)
✅ OData endpoints enabled
✅ Vector data sources enabled
```

**Lite deployment logs:**
```
ℹ️ Raster data sources disabled (GDAL not available)
ℹ️ Cloud storage providers disabled (SDKs not loaded)
✅ OData endpoints enabled
✅ Vector data sources enabled
```

---

## Quick Start (Docker Compose)

### Prerequisites

- Docker 20.10+
- Docker Compose v2.0+
- 4GB+ RAM
- 20GB+ disk space

### 1. Clone and Configure

```bash
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO/docker

# Copy and edit environment variables
cp .env.example .env
nano .env  # Edit with your settings
```

### 2. Start Services

```bash
# Full stack (Honua + PostgreSQL + Jaeger + Prometheus + Grafana)
docker compose -f docker-compose.full.yml up -d

# Basic stack (Honua + PostgreSQL only)
docker compose up -d
```

### 3. Verify Deployment

```bash
# Check service health
docker compose -f docker-compose.full.yml ps

# View logs
docker compose -f docker-compose.full.yml logs -f honua

# Test endpoints
curl http://localhost:5000/healthz/ready
curl http://localhost:5000/ogc
```

### 4. Access Services

| Service | URL | Credentials |
|---------|-----|-------------|
| Honua Server | http://localhost:5000 | See authentication config |
| Swagger UI | http://localhost:5000/swagger | - |
| Jaeger UI | http://localhost:16686 | - |
| Prometheus | http://localhost:9090 | - |
| Grafana | http://localhost:3000 | admin / admin (change!) |

## Production Deployment

### Security Checklist

- [ ] Change all default passwords in `.env`
- [ ] Disable QuickStart authentication mode
- [ ] Configure Local or OIDC authentication
- [ ] Enable HTTPS/TLS (reverse proxy required)
- [ ] Set restrictive CORS policies in metadata
- [ ] Configure rate limiting appropriately
- [ ] Enable security headers
- [ ] Review and harden database permissions
- [ ] Set up backup strategy
- [ ] Configure log aggregation
- [ ] Set up monitoring and alerting

### Environment Configuration

#### Required Settings

```bash
# .env file
POSTGRES_PASSWORD=strong-random-password-here
GRAFANA_PASSWORD=another-strong-password
ASPNETCORE_ENVIRONMENT=Production
HONUA_ALLOW_QUICKSTART=false
```

#### Authentication Setup

**Option 1: Local Authentication (Recommended for small deployments)**

```json
// appsettings.Production.json
{
  "honua": {
    "authentication": {
      "mode": "Local",
      "enforce": true,
      "local": {
        "enabled": true
      }
    }
  }
}
```

Create users via CLI:
```bash
docker exec -it honua-server dotnet Honua.Server.Host.dll user create \
  --username admin \
  --password "StrongPassword123!" \
  --role administrator
```

**Option 2: OIDC Authentication (Recommended for enterprise)**

```json
{
  "honua": {
    "authentication": {
      "mode": "OIDC",
      "enforce": true,
      "oidc": {
        "authority": "https://your-idp.com",
        "clientId": "honua-server",
        "clientSecret": "your-client-secret",
        "scope": "openid profile email"
      }
    }
  }
}
```

### Reverse Proxy (Nginx)

Add TLS termination and additional security:

```nginx
server {
    listen 443 ssl http2;
    server_name honua.example.com;

    ssl_certificate /etc/ssl/certs/honua.crt;
    ssl_certificate_key /etc/ssl/private/honua.key;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Frame-Options "SAMEORIGIN" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Rate limiting
    limit_req_zone $binary_remote_addr zone=honua_limit:10m rate=10r/s;
    limit_req zone=honua_limit burst=20 nodelay;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;

        # Timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }

    # Optimize static assets
    location ~* \.(jpg|jpeg|png|gif|ico|css|js)$ {
        proxy_pass http://localhost:5000;
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

### Resource Recommendations

#### Minimum (Development/Testing)
- 2 CPU cores
- 4GB RAM
- 20GB disk

#### Small Production (<100 req/s)
- 4 CPU cores
- 8GB RAM
- 100GB SSD

#### Medium Production (100-500 req/s)
- 8 CPU cores
- 16GB RAM
- 200GB SSD
- Consider read replicas for database

#### Large Production (>500 req/s)
- 16+ CPU cores
- 32GB+ RAM
- 500GB+ SSD
- Database replication
- Load balancer
- CDN for tile serving

### Database Tuning

For PostgreSQL with PostGIS, adjust based on available RAM:

```bash
# docker-compose.full.yml environment section
POSTGRES_SHARED_BUFFERS=2GB              # 25% of total RAM
POSTGRES_EFFECTIVE_CACHE_SIZE=6GB        # 75% of total RAM
POSTGRES_MAINTENANCE_WORK_MEM=512MB
POSTGRES_MAX_CONNECTIONS=200
POSTGRES_WAL_BUFFERS=16MB
POSTGRES_RANDOM_PAGE_COST=1.1            # For SSD storage
```

Or mount custom `postgresql.conf`:

```yaml
volumes:
  - ./postgres.conf:/etc/postgresql/postgresql.conf:ro
```

### Backup Strategy

#### Automated Database Backups

```bash
#!/bin/bash
# backup.sh
BACKUP_DIR="/backups/postgres"
DATE=$(date +%Y%m%d_%H%M%S)

docker exec honua-postgres pg_dump \
  -U honua \
  -F c \
  -b \
  -v \
  -f "/tmp/honua_backup_${DATE}.dump" \
  honua

docker cp honua-postgres:/tmp/honua_backup_${DATE}.dump ${BACKUP_DIR}/

# Retain last 7 days
find ${BACKUP_DIR} -name "honua_backup_*.dump" -mtime +7 -delete
```

Add to crontab:
```bash
0 2 * * * /path/to/backup.sh
```

#### Backup Metadata

```bash
# Backup metadata.json
docker exec honua-server cp /app/config/metadata.json /app/data/backups/metadata_$(date +%Y%m%d).json
```

### Monitoring

#### Health Checks

```bash
# Startup health
curl http://localhost:5000/healthz/startup

# Liveness (is app running?)
curl http://localhost:5000/healthz/live

# Readiness (can app serve traffic?)
curl http://localhost:5000/healthz/ready
```

#### Prometheus Alerts

Create `prometheus/alerts/honua.yml`:

```yaml
groups:
  - name: honua
    interval: 30s
    rules:
      - alert: HonuaHighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"

      - alert: HonuaDown
        expr: up{job="honua-server"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Honua server is down"

      - alert: DatabaseConnectionPoolExhausted
        expr: honua_database_connection_pool_usage > 0.9
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Database connection pool >90% utilized"
```

### Scaling

#### Horizontal Scaling

Run multiple Honua instances behind a load balancer:

```yaml
# docker-compose.scale.yml
version: "3.9"
services:
  honua:
    # ... existing config ...
    deploy:
      replicas: 3
```

```bash
docker compose -f docker-compose.scale.yml up -d --scale honua=3
```

**Session Affinity:** Not required - Honua is stateless (JWT tokens)

#### Database Read Replicas

For read-heavy workloads, add PostgreSQL read replicas:

```yaml
postgres-replica:
  image: postgis/postgis:16-3.4
  environment:
    POSTGRES_PRIMARY_HOST: postgres
    POSTGRES_PRIMARY_PORT: 5432
    # ... replication config ...
```

Configure Honua to use read replicas for queries.

### Logging

#### Centralized Logging (ELK Stack)

```yaml
# Add to docker-compose.full.yml
elasticsearch:
  image: docker.elastic.co/elasticsearch/elasticsearch:8.11.0
  environment:
    - discovery.type=single-node

logstash:
  image: docker.elastic.co/logstash/logstash:8.11.0
  volumes:
    - ./logstash/pipeline:/usr/share/logstash/pipeline:ro

kibana:
  image: docker.elastic.co/kibana/kibana:8.11.0
  ports:
    - "5601:5601"

honua:
  logging:
    driver: "json-file"
    options:
      max-size: "10m"
      max-file: "3"
      labels: "service=honua"
```

#### Simple File Logging

```yaml
honua:
  volumes:
    - ./logs:/app/logs
```

Configure in `appsettings.Production.json`:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/honua-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

## Kubernetes Deployment

### Helm Chart (Future)

Coming soon - full Helm chart for Kubernetes deployment.

### Manual Kubernetes Deployment

```yaml
# honua-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
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
        image: honua-server:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: database-connection
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /healthz/live
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /healthz/ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: honua-service
spec:
  selector:
    app: honua-server
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker compose logs honua

# Check health
docker compose ps

# Inspect container
docker inspect honua-server
```

Common issues:
- Database not ready (wait for health check)
- Configuration errors (check appsettings.json)
- Port conflicts (change HONUA_PORT in .env)

### High Memory Usage

```bash
# Monitor memory
docker stats honua-server

# Check for memory leaks
docker exec honua-server dotnet-dump collect -p 1
```

### Slow Performance

1. Enable tracing to find bottlenecks
2. Check database query performance
3. Verify cache hit rates
4. Review connection pool usage
5. Run load tests to establish baseline

## Migration from Existing System

See [Migration Guide](./MIGRATION.md) for migrating from ArcGIS Server or GeoServer.

## Next Steps

- [Performance Testing](../tests/load/README.md)
- [API Documentation](./API_DOCUMENTATION.md)
- [Security Guide](./SECURITY.md)
- [Monitoring](./TRACING.md)
