# Docker Deployment Guide

**Keywords**: docker, container, deployment, docker-compose, multi-stage, security, volumes, networking, health-checks, resource-limits
**Related**: environment-variables, kubernetes-deployment, local-development

## Overview

Honua provides production-ready Docker images optimized for containerized deployments. The multi-stage Dockerfile uses .NET 9.0 SDK for building and the chiseled Ubuntu runtime for minimal attack surface and optimal performance.

**Key Features**:
- Multi-stage builds for minimal image size
- Non-root user execution (app:app)
- Chiseled Ubuntu base (minimal dependencies)
- Health check support
- Volume mounting for configuration and data
- Docker Compose templates for full-stack deployment
- Resource limits and security constraints

## Quick Start

### Single Container Deployment

```bash
# Pull or build the image
docker build -t honua:latest .

# Run with QuickStart authentication (development only)
docker run -d \
  --name honua-server \
  -p 8080:8080 \
  -e HONUA__METADATA__PROVIDER=json \
  -e HONUA__METADATA__PATH=/app/config/metadata.json \
  -e HONUA__AUTHENTICATION__MODE=QuickStart \
  -e HONUA__AUTHENTICATION__ENFORCE=false \
  -v $(pwd)/samples/ogc:/app/config:ro \
  honua:latest
```

### Access the Server

```bash
# Check health
curl http://localhost:8080/health

# OGC API Features landing page
curl http://localhost:8080/ogc/

# List collections
curl http://localhost:8080/ogc/collections
```

## Production Dockerfile

The official Honua Dockerfile uses best practices for production deployments:

```dockerfile
# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Restore dependencies first (layer caching)
COPY NuGet.Config ./
COPY src/Honua.Server.Core/Honua.Server.Core.csproj src/Honua.Server.Core/
COPY src/Honua.Server.Host/Honua.Server.Host.csproj src/Honua.Server.Host/
RUN dotnet restore src/Honua.Server.Host/Honua.Server.Host.csproj

# Build and publish
COPY . ./
RUN dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Runtime image - chiseled Ubuntu (minimal, no shell, non-root)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

# Copy as non-root user
COPY --from=build --chown=app:app /app/publish ./

EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Honua.Server.Host.dll"]
```

**Security Features**:
- ✅ Chiseled Ubuntu base (no package manager, minimal CVE surface)
- ✅ Non-root user execution
- ✅ Read-only file system compatible
- ✅ No shell (prevents shell injection attacks)
- ✅ Minimal layer count

## Environment Variables

Honua is configured entirely through environment variables in Docker deployments. See [environment-variables.md](../01-configuration/environment-variables.md) for comprehensive reference.

### Essential Variables

```bash
# Metadata provider
HONUA__METADATA__PROVIDER=json|yaml|database
HONUA__METADATA__PATH=/app/config/metadata.json

# Authentication (choose one mode)
# ⚠️ QuickStart is for local development only.
HONUA__AUTHENTICATION__MODE=Local|Oidc
HONUA__AUTHENTICATION__ENFORCE=true

# Database connection (if using database metadata provider)
HONUA__DATABASE__CONNECTIONSTRING=Host=postgis;Database=honua;Username=honua;Password=honua123

# Logging level
Serilog__MinimumLevel__Default=Information
```

## Volume Mounts

### Configuration Files

Mount metadata and configuration files as read-only volumes:

```bash
docker run -d \
  --name honua-server \
  -p 8080:8080 \
  -v $(pwd)/config/metadata.json:/app/config/metadata.json:ro \
  -v $(pwd)/config/appsettings.Production.json:/app/appsettings.Production.json:ro \
  -e HONUA__METADATA__PROVIDER=json \
  -e HONUA__METADATA__PATH=/app/config/metadata.json \
  honua:latest
```

### Raster Tile Cache

For file-based tile caching:

```bash
docker run -d \
  --name honua-server \
  -p 8080:8080 \
  -v honua-tiles:/app/tiles \
  -e HONUA__SERVICES__RASTERTILES__PROVIDER=filesystem \
  -e HONUA__SERVICES__RASTERTILES__FILESYSTEM__PATH=/app/tiles \
  honua:latest
```

### Attachment Storage

For file-based attachment storage:

```bash
docker run -d \
  --name honua-server \
  -p 8080:8080 \
  -v honua-attachments:/app/attachments \
  -e HONUA__ATTACHMENTS__PROVIDER=filesystem \
  -e HONUA__ATTACHMENTS__FILESYSTEM__PATH=/app/attachments \
  honua:latest
```

## Docker Compose Deployments

### Full Stack with PostGIS

Production-ready stack with PostGIS database, connection pooling, and health checks:

```yaml
version: '3.8'

services:
  postgis:
    image: postgis/postgis:16-3.4
    container_name: honua-postgis
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-honua123}
      POSTGRES_DB: honuadb
      POSTGRES_INITDB_ARGS: "--encoding=UTF8 --locale=en_US.UTF-8"
    volumes:
      - postgis-data:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua -d honuadb"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    restart: unless-stopped
    networks:
      - honua-network

  honua:
    image: honua:latest
    container_name: honua-server
    depends_on:
      postgis:
        condition: service_healthy
    environment:
      # Metadata configuration
      HONUA__METADATA__PROVIDER: database
      HONUA__METADATA__PATH: /app/config

      # Database connection
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;Port=5432;Database=honuadb;Username=honua;Password=${POSTGRES_PASSWORD:-honua123};Pooling=true;MinPoolSize=5;MaxPoolSize=50;ConnectionIdleLifetime=300"

      # Authentication
      HONUA__AUTHENTICATION__MODE: Local
      HONUA__AUTHENTICATION__ENFORCE: true

      # OData
      HONUA__ODATA__ENABLED: true
      HONUA__ODATA__DEFAULTPAGESIZE: 100
      HONUA__ODATA__MAXPAGESIZE: 1000

      # Services
      HONUA__SERVICES__WFS__ENABLED: true
      HONUA__SERVICES__WMS__ENABLED: true

      # Logging
      Serilog__MinimumLevel__Default: Information
      Serilog__MinimumLevel__Override__Microsoft: Warning

      # ASP.NET Core
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
    volumes:
      - ./config:/app/config:ro
      - honua-tiles:/app/tiles
      - honua-attachments:/app/attachments
    ports:
      - "8080:8080"
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    restart: unless-stopped
    networks:
      - honua-network
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G

volumes:
  postgis-data:
    driver: local
  honua-tiles:
    driver: local
  honua-attachments:
    driver: local

networks:
  honua-network:
    driver: bridge
```

### Usage

```bash
# Start all services
docker-compose up -d

# View logs
docker-compose logs -f honua

# Check service health
docker-compose ps

# Stop all services
docker-compose down

# Stop and remove volumes (data loss!)
docker-compose down -v
```

### With NGINX Reverse Proxy

Add SSL termination and load balancing with NGINX:

```yaml
version: '3.8'

services:
  postgis:
    # ... (same as above)

  honua:
    # ... (same as above, but remove ports section)
    # Services communicate via internal network only

  nginx:
    image: nginx:alpine
    container_name: honua-nginx
    depends_on:
      - honua
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
      - /var/log/nginx:/var/log/nginx
    ports:
      - "80:80"
      - "443:443"
    healthcheck:
      test: ["CMD", "wget", "--quiet", "--tries=1", "--spider", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped
    networks:
      - honua-network

networks:
  honua-network:
    driver: bridge
```

**nginx.conf** example:

```nginx
events {
    worker_connections 1024;
}

http {
    upstream honua_backend {
        least_conn;
        server honua:8080 max_fails=3 fail_timeout=30s;
    }

    # Rate limiting
    limit_req_zone $binary_remote_addr zone=api_limit:10m rate=10r/s;
    limit_req_status 429;

    server {
        listen 80;
        server_name honua.example.com;

        # Redirect to HTTPS
        location / {
            return 301 https://$server_name$request_uri;
        }
    }

    server {
        listen 443 ssl http2;
        server_name honua.example.com;

        ssl_certificate /etc/nginx/ssl/cert.pem;
        ssl_certificate_key /etc/nginx/ssl/key.pem;
        ssl_protocols TLSv1.2 TLSv1.3;
        ssl_ciphers HIGH:!aNULL:!MD5;

        # Security headers
        add_header X-Frame-Options "SAMEORIGIN" always;
        add_header X-Content-Type-Options "nosniff" always;
        add_header X-XSS-Protection "1; mode=block" always;
        add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;

        # Compression
        gzip on;
        gzip_types application/json application/geo+json text/plain text/css application/javascript;
        gzip_min_length 1000;

        location / {
            limit_req zone=api_limit burst=20 nodelay;

            proxy_pass http://honua_backend;
            proxy_http_version 1.1;

            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;

            proxy_connect_timeout 60s;
            proxy_send_timeout 60s;
            proxy_read_timeout 60s;

            proxy_buffering on;
            proxy_buffer_size 4k;
            proxy_buffers 8 4k;
            proxy_busy_buffers_size 8k;
        }

        # Health check endpoint (no rate limiting)
        location /health {
            proxy_pass http://honua_backend;
            access_log off;
        }
    }
}
```

### With Redis Caching

Add Redis for distributed caching:

```yaml
version: '3.8'

services:
  postgis:
    # ... (same as above)

  redis:
    image: redis:7-alpine
    container_name: honua-redis
    command: redis-server --appendonly yes --maxmemory 512mb --maxmemory-policy allkeys-lru
    volumes:
      - redis-data:/data
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - honua-network

  honua:
    # ... (same as above, add Redis connection)
    environment:
      # ... (other env vars)
      HONUA__CACHE__PROVIDER: redis
      HONUA__CACHE__REDIS__CONNECTIONSTRING: "redis:6379,abortConnect=false,connectTimeout=5000"

volumes:
  postgis-data:
  redis-data:
  honua-tiles:
  honua-attachments:
```

## Docker Networking

### Bridge Network (Default)

Containers on the same bridge network can communicate by service name:

```bash
docker network create honua-network

docker run -d \
  --name postgis \
  --network honua-network \
  -e POSTGRES_USER=honua \
  -e POSTGRES_PASSWORD=honua123 \
  -e POSTGRES_DB=honuadb \
  postgis/postgis:latest

docker run -d \
  --name honua \
  --network honua-network \
  -p 8080:8080 \
  -e HONUA__DATABASE__CONNECTIONSTRING="Host=postgis;Database=honuadb;Username=honua;Password=honua123" \
  honua:latest
```

### Host Network

Use host networking for maximum performance (Linux only):

```bash
docker run -d \
  --name honua \
  --network host \
  -e ASPNETCORE_URLS=http://0.0.0.0:8080 \
  honua:latest
```

**Note**: Host networking bypasses Docker's network isolation. Use with caution.

### Custom DNS

Configure custom DNS for service discovery:

```bash
docker run -d \
  --name honua \
  --dns 8.8.8.8 \
  --dns 8.8.4.4 \
  --dns-search example.com \
  honua:latest
```

## Health Checks

### Built-in Health Check

The Dockerfile includes a health check endpoint:

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=40s \
  CMD curl -f http://localhost:8080/health || exit 1
```

### Custom Health Check Script

For advanced health checks:

```bash
#!/bin/bash
# healthcheck.sh

# Check HTTP endpoint
if ! curl -f -s http://localhost:8080/health > /dev/null; then
  exit 1
fi

# Check database connectivity (if using database metadata)
if ! curl -f -s http://localhost:8080/ogc/collections > /dev/null; then
  exit 1
fi

exit 0
```

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=40s \
  CMD ["/app/healthcheck.sh"]
```

## Resource Limits

### Memory Limits

Prevent OOM kills by setting memory limits:

```bash
docker run -d \
  --name honua \
  --memory="2g" \
  --memory-reservation="1g" \
  --memory-swap="3g" \
  --oom-kill-disable=false \
  honua:latest
```

### CPU Limits

Limit CPU usage:

```bash
docker run -d \
  --name honua \
  --cpus="2.0" \
  --cpu-shares=1024 \
  honua:latest
```

### Combined Resource Constraints

```yaml
services:
  honua:
    image: honua:latest
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
      restart_policy:
        condition: on-failure
        delay: 5s
        max_attempts: 3
        window: 120s
```

## Security Best Practices

### Run as Non-Root User

The official Dockerfile already uses a non-root user (`app:app`):

```dockerfile
USER app
ENTRYPOINT ["dotnet", "Honua.Server.Host.dll"]
```

### Read-Only Root Filesystem

Run with read-only root filesystem:

```bash
docker run -d \
  --name honua \
  --read-only \
  --tmpfs /tmp:rw,noexec,nosuid,size=100m \
  --tmpfs /app/logs:rw,noexec,nosuid,size=500m \
  -v $(pwd)/config:/app/config:ro \
  honua:latest
```

### Drop Capabilities

Remove unnecessary Linux capabilities:

```bash
docker run -d \
  --name honua \
  --cap-drop=ALL \
  --cap-add=NET_BIND_SERVICE \
  --security-opt=no-new-privileges:true \
  honua:latest
```

### Secrets Management

Use Docker secrets for sensitive configuration:

```yaml
version: '3.8'

services:
  honua:
    image: honua:latest
    secrets:
      - db_password
      - oauth_secret
    environment:
      HONUA__DATABASE__CONNECTIONSTRING: "Host=postgis;Database=honuadb;Username=honua;Password_File=/run/secrets/db_password"

secrets:
  db_password:
    file: ./secrets/db_password.txt
  oauth_secret:
    file: ./secrets/oauth_secret.txt
```

### Scan for Vulnerabilities

Regularly scan images for CVEs:

```bash
# Using Trivy
trivy image honua:latest

# Using Docker Scout
docker scout cves honua:latest

# Using Snyk
snyk container test honua:latest
```

## Multi-Architecture Builds

Build for multiple platforms (amd64, arm64):

```bash
# Set up buildx
docker buildx create --use --name honua-builder

# Build multi-arch image
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --tag honua:latest \
  --push \
  .
```

## Image Optimization

### Multi-Stage Build Optimization

The Honua Dockerfile already uses multi-stage builds. Key optimizations:

1. **Layer Caching**: Restore dependencies before copying source code
2. **Minimal Runtime**: Chiseled Ubuntu base (no shell, minimal packages)
3. **Self-Contained**: No external dependencies at runtime

### Reduce Image Size

```bash
# View image layers
docker history honua:latest

# Check image size
docker images honua:latest

# Remove intermediate layers
docker image prune -a -f --filter "until=24h"
```

### Build Cache

Speed up builds with BuildKit cache:

```bash
# Enable BuildKit
export DOCKER_BUILDKIT=1

# Build with cache
docker build \
  --cache-from honua:latest \
  --tag honua:latest \
  .

# Use external cache (for CI/CD)
docker buildx build \
  --cache-from type=registry,ref=honua:buildcache \
  --cache-to type=registry,ref=honua:buildcache,mode=max \
  --tag honua:latest \
  .
```

## Logging

### JSON Structured Logging

Configure Serilog for JSON output (ideal for log aggregation):

```bash
docker run -d \
  --name honua \
  -e Serilog__Using__0=Serilog.Sinks.Console \
  -e Serilog__WriteTo__0__Name=Console \
  -e Serilog__WriteTo__0__Args__formatter=Serilog.Formatting.Compact.CompactJsonFormatter,Serilog.Formatting.Compact \
  honua:latest
```

### Log to File (Volume)

```yaml
services:
  honua:
    image: honua:latest
    volumes:
      - honua-logs:/app/logs
    environment:
      Serilog__WriteTo__0__Name: File
      Serilog__WriteTo__0__Args__path: /app/logs/honua-.log
      Serilog__WriteTo__0__Args__rollingInterval: Day
      Serilog__WriteTo__0__Args__retainedFileCountLimit: 30

volumes:
  honua-logs:
```

### Centralized Logging

Forward logs to external systems:

```yaml
services:
  honua:
    image: honua:latest
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
        labels: "app,environment"
        env: "ASPNETCORE_ENVIRONMENT"
```

**Using Fluentd**:

```yaml
services:
  honua:
    image: honua:latest
    logging:
      driver: fluentd
      options:
        fluentd-address: fluentd:24224
        tag: honua.{{.Name}}
```

## Monitoring

### Prometheus Metrics

Enable Prometheus endpoint:

```yaml
services:
  honua:
    image: honua:latest
    environment:
      observability__metrics__enabled: "true"
      observability__metrics__endpoint: /metrics
      observability__metrics__usePrometheus: "true"
    ports:
      - "8080:8080"

  prometheus:
    image: prom/prometheus:latest
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    ports:
      - "9090:9090"
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
    networks:
      - honua-network

volumes:
  prometheus-data:
```

**prometheus.yml**:

```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'honua'
    static_configs:
      - targets: ['honua:8080']
    metrics_path: '/metrics'
```

## Troubleshooting

### Container Won't Start

```bash
# View container logs
docker logs honua

# View last 100 lines
docker logs --tail 100 honua

# Follow logs in real-time
docker logs -f honua

# Check container exit code
docker inspect honua --format='{{.State.ExitCode}}'

# View container configuration
docker inspect honua
```

### Connection Issues

```bash
# Test network connectivity from container
docker exec honua ping postgis

# Check DNS resolution
docker exec honua nslookup postgis

# Verify port bindings
docker port honua

# Test from host
curl http://localhost:8080/health
```

### Performance Issues

```bash
# View resource usage
docker stats honua

# View processes inside container
docker top honua

# Execute shell (if available, not in chiseled image)
docker exec -it honua /bin/bash

# For chiseled images, copy files out for inspection
docker cp honua:/app/logs/honua.log ./
```

### Database Connection Failures

```bash
# Verify database is accessible from container
docker exec honua dotnet tool install --global dotnet-sql-cache
docker exec honua dotnet sql-cache create "Host=postgis;Database=honuadb;Username=honua;Password=honua123" dbo SqlCache

# Check environment variables
docker exec honua env | grep HONUA

# Test raw PostgreSQL connection
docker run --rm --network honua-network postgres:latest \
  psql -h postgis -U honua -d honuadb -c "SELECT version();"
```

### Out of Memory

```bash
# Check memory usage
docker stats honua --no-stream

# View memory limits
docker inspect honua --format='{{.HostConfig.Memory}}'

# Increase memory limit
docker update --memory="4g" honua
```

### Permission Denied Errors

The chiseled image runs as user `app:app` (UID 1654). Ensure volumes have correct ownership:

```bash
# On host, set ownership
sudo chown -R 1654:1654 ./config
sudo chown -R 1654:1654 ./tiles

# Or use chmod for read-only mounts
chmod -R 755 ./config
```

## CI/CD Integration

### GitHub Actions

```yaml
name: Build and Push Docker Image

on:
  push:
    branches: [main]
    tags: ['v*']

jobs:
  docker:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2

      - name: Login to Docker Hub
        uses: docker/login-action@v2
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Build and push
        uses: docker/build-push-action@v4
        with:
          context: .
          platforms: linux/amd64,linux/arm64
          push: true
          tags: |
            honua/server:latest
            honua/server:${{ github.sha }}
          cache-from: type=registry,ref=honua/server:buildcache
          cache-to: type=registry,ref=honua/server:buildcache,mode=max
```

### GitLab CI

```yaml
stages:
  - build
  - test
  - deploy

build:
  stage: build
  image: docker:latest
  services:
    - docker:dind
  script:
    - docker build -t $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA .
    - docker push $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA

test:
  stage: test
  image: docker:latest
  services:
    - docker:dind
  script:
    - docker run --rm $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA dotnet test

deploy:
  stage: deploy
  script:
    - docker tag $CI_REGISTRY_IMAGE:$CI_COMMIT_SHA $CI_REGISTRY_IMAGE:latest
    - docker push $CI_REGISTRY_IMAGE:latest
  only:
    - main
```

## See Also

- [Environment Variables](../01-configuration/environment-variables.md) - Complete environment variable reference
- [Kubernetes Deployment](kubernetes-deployment.md) - Orchestrated container deployment
- [AWS ECS Deployment](aws-ecs-deployment.md) - AWS container service
- [Local Development](local-development.md) - Development environment setup
- [Monitoring and Observability](../04-operations/monitoring-observability.md) - Production monitoring
- [Security Best Practices](../04-operations/security-best-practices.md) - Container security hardening
