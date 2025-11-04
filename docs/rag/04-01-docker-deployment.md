---
tags: [docker, docker-compose, deployment, containers, production, postgresql, observability]
category: deployment
difficulty: intermediate
version: 1.0.0
last_updated: 2025-10-15
---

# Docker Deployment Guide

Complete guide to deploying Honua Server with Docker and Docker Compose.

## Table of Contents
- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Docker Compose Files](#docker-compose-files)
- [Environment Configuration](#environment-configuration)
- [Basic Deployment](#basic-deployment)
- [Full Stack Deployment](#full-stack-deployment)
- [Production Configuration](#production-configuration)
- [Networking](#networking)
- [Volumes and Persistence](#volumes-and-persistence)
- [Health Checks](#health-checks)
- [Scaling](#scaling)
- [Monitoring](#monitoring)
- [Backup and Restore](#backup-and-restore)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

Honua provides production-ready Docker images and Docker Compose configurations for:
- Minimal deployments (Honua + PostgreSQL)
- Full observability stack (Prometheus, Grafana, Jaeger)
- Development and production environments

### Architecture

```
┌─────────────┐
│   Honua     │ :8080
│   Server    │
└─────┬───────┘
      │
┌─────▼───────┐
│  PostgreSQL │ :5432
│   PostGIS   │
└─────────────┘

Optional Observability:
- Prometheus (metrics)
- Grafana (dashboards)
- Jaeger (tracing)
```

## Prerequisites

### Required Software

- **Docker**: 20.10 or later
- **Docker Compose**: v2.0 or later

**Verify Installation:**
```bash
docker --version
# Docker version 24.0.0, build ...

docker compose version
# Docker Compose version v2.20.0
```

### System Requirements

**Minimum:**
- 2 CPU cores
- 4 GB RAM
- 10 GB disk space

**Recommended:**
- 4+ CPU cores
- 8+ GB RAM
- 50+ GB disk space (for data and cache)

## Quick Start

Get Honua running in 3 commands:

```bash
# 1. Clone repository
git clone https://github.com/mikemcdougall/HonuaIO.git
cd HonuaIO/docker

# 2. Copy environment file
cp .env.example .env

# 3. Start services
docker compose up --build
```

**Access the server:**
- Honua API: http://localhost:8080/ogc
- API Documentation: http://localhost:8080/swagger

## Docker Compose Files

Honua provides three Docker Compose configurations:

### 1. docker-compose.yml (Basic)

Minimal deployment with Honua and PostgreSQL.

**Location:** `/docker/docker-compose.yml`

```yaml
version: "3.9"

services:
  db:
    image: postgres:16
    container_name: honua-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-honua}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-honua_password}
      POSTGRES_DB: ${POSTGRES_DB:-honua}
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    volumes:
      - honua-postgres-data:/var/lib/postgresql/data

  web:
    build:
      context: ..
      dockerfile: Dockerfile
    container_name: honua-web
    depends_on:
      - db
    environment:
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Development}
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: Server=db;Port=5432;Database=${POSTGRES_DB:-honua};User Id=${POSTGRES_USER:-honua};Password=${POSTGRES_PASSWORD:-honua_password};
    ports:
      - "${WEB_PORT:-8080}:8080"
    env_file:
      - .env

volumes:
  honua-postgres-data:
```

**Usage:**
```bash
docker compose up --build
```

### 2. docker-compose.full.yml (Production)

Complete stack with observability tools.

**Location:** `/docker/docker-compose.full.yml`

**Includes:**
- Honua Server
- PostgreSQL/PostGIS
- Prometheus (metrics)
- Grafana (dashboards)
- Jaeger (distributed tracing)

**Usage:**
```bash
docker compose -f docker-compose.full.yml up --build
```

**Access:**
- Honua: http://localhost:5000
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin)
- Jaeger UI: http://localhost:16686

### 3. docker-compose.prometheus.yml (Monitoring)

Lightweight monitoring setup.

**Usage:**
```bash
docker compose -f docker-compose.prometheus.yml up
```

## Environment Configuration

Configure deployment using environment variables.

### Create .env File

```bash
cd docker
cp .env.example .env
nano .env  # Edit with your values
```

### .env.example

```bash
# PostgreSQL Configuration
POSTGRES_USER=honua
POSTGRES_PASSWORD=change-me-in-production
POSTGRES_DB=honua
POSTGRES_PORT=5432

# PostgreSQL Performance Tuning
POSTGRES_SHARED_BUFFERS=256MB
POSTGRES_EFFECTIVE_CACHE_SIZE=1GB
POSTGRES_MAX_CONNECTIONS=100

# Honua Server Configuration
HONUA_PORT=5000
ASPNETCORE_ENVIRONMENT=Production

# Grafana Configuration
GRAFANA_USER=admin
GRAFANA_PASSWORD=change-me-in-production

# Authentication (CHANGE FOR PRODUCTION)
# QuickStart mode is for development only!
HONUA_ALLOW_QUICKSTART=false

# Rate Limiting
RATELIMITING_PERMITLIMIT=100
RATELIMITING_WINDOW=00:01:00

# Logging
LOGGING_LOGLEVEL_DEFAULT=Information
LOGGING_LOGLEVEL_MICROSOFT=Warning
LOGGING_LOGLEVEL_HONUA_SERVER=Information
```

### Security Considerations

**NEVER commit secrets to version control!**

**Generate Secure Passwords:**
```bash
# Generate random password
openssl rand -base64 32

# Or use pwgen
pwgen -s 32 1
```

**Update .env:**
```bash
POSTGRES_PASSWORD=<generated-password>
GRAFANA_PASSWORD=<generated-password>
```

## Basic Deployment

Step-by-step basic deployment.

### Step 1: Prepare Environment

```bash
cd HonuaIO/docker

# Create .env file
cp .env.example .env

# Edit passwords
nano .env
```

### Step 2: Create Metadata Directory

```bash
# Create workspace directory
mkdir -p ../metadata

# Copy example metadata
cp -r ../examples/metadata/* ../metadata/
```

### Step 3: Start Services

```bash
# Build and start in detached mode
docker compose up --build -d

# View logs
docker compose logs -f
```

### Step 4: Verify Deployment

```bash
# Check service health
curl http://localhost:8080/healthz/ready

# Expected response:
# {"status":"Healthy","results":{...}}

# Test API
curl http://localhost:8080/ogc/collections
```

### Step 5: Stop Services

```bash
# Stop services (keep data)
docker compose stop

# Stop and remove containers
docker compose down

# Stop and remove containers + volumes (DELETES DATA!)
docker compose down -v
```

## Full Stack Deployment

Deploy with complete observability.

### Step 1: Start Full Stack

```bash
cd HonuaIO/docker

# Start all services
docker compose -f docker-compose.full.yml up --build -d

# View logs
docker compose -f docker-compose.full.yml logs -f honua
```

### Step 2: Configure Prometheus

Prometheus configuration is in `/docker/prometheus/prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'honua'
    static_configs:
      - targets: ['honua:8080']
    metrics_path: '/metrics'
```

**Test Prometheus:**
```bash
curl http://localhost:9090/api/v1/targets
```

### Step 3: Configure Grafana

**Access Grafana:**
```
URL: http://localhost:3000
Username: admin
Password: admin (change on first login)
```

**Add Prometheus Data Source:**
1. Navigate to Configuration > Data Sources
2. Add Prometheus
3. URL: `http://prometheus:9090`
4. Save & Test

**Import Dashboards:**
```bash
# Dashboards are in /docker/grafana/dashboards/
# Automatically provisioned on startup
```

### Step 4: Configure Jaeger

Jaeger is automatically configured for OTLP protocol.

**Access Jaeger UI:**
```
URL: http://localhost:16686
```

**Verify Tracing:**
1. Make API requests to Honua
2. Check Jaeger for traces
3. Traces appear under "Honua.Server" service

## Production Configuration

Best practices for production deployments.

### 1. Use Production-Ready PostgreSQL

```yaml
services:
  postgres:
    image: postgis/postgis:16-3.4
    environment:
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      # Performance tuning
      POSTGRES_SHARED_BUFFERS: 2GB
      POSTGRES_EFFECTIVE_CACHE_SIZE: 6GB
      POSTGRES_MAX_CONNECTIONS: 200
      POSTGRES_WORK_MEM: 16MB
    volumes:
      - postgres-data:/var/lib/postgresql/data
    command:
      - "postgres"
      - "-c"
      - "max_connections=200"
      - "-c"
      - "shared_buffers=2GB"
```

### 2. Enable HTTPS

```yaml
services:
  honua:
    environment:
      ASPNETCORE_URLS: https://+:443;http://+:80
      ASPNETCORE_Kestrel__Certificates__Default__Path: /app/certs/cert.pfx
      ASPNETCORE_Kestrel__Certificates__Default__Password: ${CERT_PASSWORD}
    volumes:
      - ./certs:/app/certs:ro
    ports:
      - "443:443"
      - "80:80"
```

### 3. Resource Limits

```yaml
services:
  honua:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
```

### 4. Health Checks

```yaml
services:
  honua:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

### 5. Restart Policies

```yaml
services:
  honua:
    restart: unless-stopped

  postgres:
    restart: unless-stopped
```

## Networking

### Default Network

Docker Compose creates a default bridge network.

**Internal DNS:**
- Services reference each other by service name
- `db` resolves to PostgreSQL container
- `honua` resolves to Honua container

### Custom Network

```yaml
networks:
  honua-network:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16

services:
  honua:
    networks:
      - honua-network
```

### Expose Ports

```yaml
services:
  honua:
    ports:
      - "8080:8080"  # Host:Container
      - "127.0.0.1:8080:8080"  # Bind to localhost only
```

## Volumes and Persistence

### Named Volumes

```yaml
volumes:
  postgres-data:
    driver: local
  prometheus-data:
    driver: local
  grafana-data:
    driver: local
```

**List volumes:**
```bash
docker volume ls
```

**Inspect volume:**
```bash
docker volume inspect docker_postgres-data
```

### Bind Mounts

```yaml
services:
  honua:
    volumes:
      - ./metadata:/app/metadata:ro  # Read-only
      - ./data:/app/data              # Read-write
      - ./config:/app/config:ro
```

### Backup Volumes

```bash
# Backup PostgreSQL data
docker run --rm \
  -v docker_postgres-data:/data \
  -v $(pwd):/backup \
  alpine tar czf /backup/postgres-backup.tar.gz /data

# Restore PostgreSQL data
docker run --rm \
  -v docker_postgres-data:/data \
  -v $(pwd):/backup \
  alpine sh -c "cd / && tar xzf /backup/postgres-backup.tar.gz"
```

## Health Checks

### Container Health Checks

Built into docker-compose.full.yml:

```yaml
services:
  honua:
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  postgres:
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-honua}"]
      interval: 10s
      timeout: 5s
      retries: 5
```

### Check Health Status

```bash
# View health status
docker compose ps

# Example output:
# NAME            STATUS                    PORTS
# honua-server    Up (healthy)              0.0.0.0:5000->8080/tcp
# honua-postgres  Up (healthy)              0.0.0.0:5432->5432/tcp
```

### Honua Health Endpoints

```bash
# Liveness check (container running)
curl http://localhost:8080/healthz/live

# Readiness check (ready for traffic)
curl http://localhost:8080/healthz/ready

# Startup check (initialization complete)
curl http://localhost:8080/healthz/startup
```

## Scaling

### Horizontal Scaling

Scale Honua instances:

```yaml
services:
  honua:
    deploy:
      replicas: 3
```

**Or with command:**
```bash
docker compose up --scale honua=3
```

### Load Balancing

Add nginx load balancer:

```yaml
services:
  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
    depends_on:
      - honua
```

**nginx.conf:**
```nginx
upstream honua {
    server honua-1:8080;
    server honua-2:8080;
    server honua-3:8080;
}

server {
    listen 80;

    location / {
        proxy_pass http://honua;
        proxy_set_header Host $host;
    }
}
```

## Monitoring

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f honua

# Last 100 lines
docker compose logs --tail=100 honua

# Since timestamp
docker compose logs --since 2025-10-15T10:00:00 honua
```

### Resource Usage

```bash
# Container stats
docker stats

# Specific container
docker stats honua-server
```

### Prometheus Metrics

```bash
# Query Prometheus API
curl http://localhost:9090/api/v1/query?query=honua_api_requests_total

# Metrics from Honua directly
curl http://localhost:8080/metrics
```

## Backup and Restore

### Database Backup

```bash
# Backup PostgreSQL
docker compose exec postgres pg_dump -U honua honua > backup.sql

# Or using Docker directly
docker exec honua-postgres pg_dump -U honua honua > backup.sql

# With compression
docker exec honua-postgres pg_dump -U honua honua | gzip > backup.sql.gz
```

### Database Restore

```bash
# Restore from backup
docker compose exec -T postgres psql -U honua honua < backup.sql

# Or
cat backup.sql | docker exec -i honua-postgres psql -U honua honua

# From compressed
gunzip -c backup.sql.gz | docker exec -i honua-postgres psql -U honua honua
```

### Full Backup Strategy

```bash
#!/bin/bash
# backup.sh - Complete backup script

BACKUP_DIR="/backups/honua/$(date +%Y%m%d_%H%M%S)"
mkdir -p "$BACKUP_DIR"

# 1. Backup PostgreSQL
docker exec honua-postgres pg_dump -U honua honua | gzip > "$BACKUP_DIR/database.sql.gz"

# 2. Backup volumes
docker run --rm \
  -v docker_postgres-data:/data \
  -v "$BACKUP_DIR":/backup \
  alpine tar czf /backup/volumes.tar.gz /data

# 3. Backup configuration
cp .env "$BACKUP_DIR/"
cp -r ../metadata "$BACKUP_DIR/"

echo "Backup completed: $BACKUP_DIR"
```

## Troubleshooting

### Container Won't Start

```bash
# Check logs
docker compose logs honua

# Common issues:
# 1. Port already in use
sudo lsof -i :8080
# Kill process: kill -9 <PID>

# 2. Database not ready
# Wait for PostgreSQL health check
docker compose ps postgres
```

### Connection Refused

```bash
# Verify network
docker network inspect docker_default

# Test database connection from Honua container
docker compose exec honua sh -c 'apt-get update && apt-get install -y postgresql-client'
docker compose exec honua psql -h db -U honua -d honua
```

### Out of Memory

```bash
# Check container memory usage
docker stats

# Increase memory limits in docker-compose.yml
services:
  honua:
    deploy:
      resources:
        limits:
          memory: 4G
```

### Disk Space Issues

```bash
# Check disk usage
docker system df

# Clean up
docker system prune -a  # Remove unused containers, images
docker volume prune     # Remove unused volumes (CAREFUL!)
```

### Performance Issues

**Enable query logging:**
```yaml
services:
  honua:
    environment:
      Logging__LogLevel__Honua.Server: Debug
      observability__logging__includeScopes: true
```

**PostgreSQL query logging:**
```yaml
services:
  postgres:
    command:
      - "postgres"
      - "-c"
      - "log_statement=all"
      - "-c"
      - "log_duration=on"
```

### Reset Everything

```bash
# Stop and remove all containers, networks, volumes
docker compose down -v

# Remove images
docker compose down --rmi all

# Start fresh
docker compose up --build
```

## Production Checklist

Before deploying to production:

- [ ] Change all default passwords
- [ ] Enable HTTPS with valid certificates
- [ ] Configure authentication (NOT QuickStart)
- [ ] Set up database backups
- [ ] Enable monitoring (Prometheus/Grafana)
- [ ] Configure log aggregation
- [ ] Set resource limits
- [ ] Enable health checks
- [ ] Configure restart policies
- [ ] Review rate limiting settings
- [ ] Test disaster recovery
- [ ] Document custom configuration

## Example Production Deployment

Complete production-ready docker-compose.yml:

```yaml
version: "3.9"

services:
  postgres:
    image: postgis/postgis:16-3.4
    container_name: honua-postgres
    environment:
      POSTGRES_USER: ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: ${POSTGRES_DB}
      POSTGRES_SHARED_BUFFERS: 2GB
      POSTGRES_EFFECTIVE_CACHE_SIZE: 6GB
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
      - ./backups:/backups
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 4G

  honua:
    build:
      context: ..
      dockerfile: Dockerfile
    container_name: honua-server
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: https://+:443;http://+:80
      ConnectionStrings__DefaultConnection: Server=postgres;Port=5432;Database=${POSTGRES_DB};User Id=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};
      honua__authentication__mode: Local
      honua__authentication__enforce: true
      observability__metrics__enabled: true
      observability__tracing__exporter: otlp
      observability__tracing__otlpEndpoint: http://tempo:4317
      RateLimiting__Enabled: true
    ports:
      - "443:443"
      - "80:80"
    volumes:
      - ./metadata:/app/metadata:ro
      - ./data:/app/data
      - ./certs:/app/certs:ro
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/healthz/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    restart: unless-stopped
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G

volumes:
  postgres-data:
    driver: local

networks:
  default:
    driver: bridge
```

## Related Documentation

- [Architecture Overview](01-01-architecture-overview.md) - System design
- [Configuration Reference](02-01-configuration-reference.md) - Config options
- [OGC API Features](03-01-ogc-api-features.md) - API usage
- [Common Issues](05-02-common-issues.md) - Troubleshooting

## Keywords for Search

Docker, Docker Compose, deployment, containers, PostgreSQL, PostGIS, production, observability, Prometheus, Grafana, Jaeger, monitoring, health checks, scaling, backup, restore, volumes, networking, troubleshooting, HTTPS, security

---

**Last Updated**: 2025-10-15
**Version**: 1.0.0
**Covers**: Honua Server 1.0.0-rc1
