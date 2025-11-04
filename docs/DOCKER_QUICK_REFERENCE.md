# Docker Quick Reference

Quick commands and examples for building, running, and deploying Honua Server containers.

## Table of Contents

- [Building Images](#building-images)
- [Running Containers](#running-containers)
- [Docker Compose](#docker-compose)
- [Multi-Architecture Builds](#multi-architecture-builds)
- [Registry Operations](#registry-operations)
- [Troubleshooting](#troubleshooting)

---

## Building Images

### Using Build Script (Recommended)

```bash
# Build Full image for x64
./scripts/docker-build.sh full amd64

# Build Lite image for ARM64
./scripts/docker-build.sh lite arm64

# Build multi-arch with buildx
./scripts/docker-build.sh full multi

# Build and push with version tag
VERSION=1.0.0 PUSH=true ./scripts/docker-build.sh full multi
```

### Manual Docker Build

```bash
# Build Full image
docker build -t honua-server:latest -f Dockerfile .

# Build Lite image
docker build -t honua-server:lite -f Dockerfile.lite .

# Build for specific architecture
docker build --platform linux/amd64 -t honua-server:amd64 .
docker build --platform linux/arm64 -t honua-server:arm64 .
```

### Build with Version Tags

```bash
# Build with build arguments
docker build \
  --build-arg VERSION=1.0.0 \
  --build-arg BUILD_DATE=$(date -u +'%Y-%m-%dT%H:%M:%SZ') \
  --build-arg VCS_REF=$(git rev-parse --short HEAD) \
  -t honua-server:1.0.0 \
  -f Dockerfile .
```

---

## Running Containers

### Full Image

```bash
# Basic run
docker run -p 8080:8080 honua-server:latest

# With environment variables
docker run -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Development \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;Username=honua;Password=pass" \
  -e HONUA__METADATA__PROVIDER=postgres \
  honua-server:latest

# With volume mounts
docker run -p 8080:8080 \
  -v $(pwd)/data:/data:ro \
  -v $(pwd)/logs:/app/logs \
  honua-server:latest

# Detached with restart policy
docker run -d \
  --name honua-server \
  --restart unless-stopped \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  honua-server:latest
```

### Lite Image

```bash
# Run Lite variant
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;..." \
  honua-server:lite

# Run from GHCR
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="..." \
  ghcr.io/honuaio/honua-server:lite
```

### Health Checks

```bash
# Check if container is healthy
docker ps --filter "name=honua-server" --format "{{.Status}}"

# View health check logs
docker inspect --format='{{json .State.Health}}' honua-server | jq

# Manual health check
curl http://localhost:8080/healthz/live
curl http://localhost:8080/healthz/ready
```

---

## Docker Compose

### Basic Usage

```bash
# Start all services
docker compose up

# Start in detached mode
docker compose up -d

# View logs
docker compose logs -f

# View logs for specific service
docker compose logs -f honua-server

# Stop services
docker compose down

# Stop and remove volumes (clean slate)
docker compose down -v
```

### With Observability Stack

```bash
# Start with observability services (Seq, Jaeger, Prometheus, Grafana)
docker compose --profile observability up

# Start specific services
docker compose up postgres redis honua-server

# Scale service
docker compose up --scale honua-server=3
```

### Rebuild and Restart

```bash
# Rebuild image and restart
docker compose up --build

# Force recreate containers
docker compose up --force-recreate

# Rebuild specific service
docker compose build honua-server
docker compose up honua-server
```

---

## Multi-Architecture Builds

### Setup Buildx (One-Time)

```bash
# Create builder
docker buildx create --name multiarch \
  --driver docker-container \
  --bootstrap

# Use the builder
docker buildx use multiarch

# Verify
docker buildx inspect --bootstrap
```

### Build for Multiple Architectures

```bash
# Build for both amd64 and arm64
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t honua-server:latest \
  --load \
  -f Dockerfile .

# Build and push to registry
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t ghcr.io/honuaio/honua-server:1.0.0 \
  --push \
  -f Dockerfile .
```

### Create Multi-Arch Manifest

```bash
# Build and push individual images
docker build --platform linux/amd64 -t honua-server:1.0.0-amd64 .
docker push honua-server:1.0.0-amd64

docker build --platform linux/arm64 -t honua-server:1.0.0-arm64 .
docker push honua-server:1.0.0-arm64

# Create manifest
docker manifest create honua-server:1.0.0 \
  honua-server:1.0.0-amd64 \
  honua-server:1.0.0-arm64

# Inspect manifest
docker manifest inspect honua-server:1.0.0

# Push manifest
docker manifest push honua-server:1.0.0
```

---

## Registry Operations

### GitHub Container Registry (GHCR)

```bash
# Login
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Tag
docker tag honua-server:latest ghcr.io/honuaio/honua-server:latest

# Push
docker push ghcr.io/honuaio/honua-server:latest

# Pull
docker pull ghcr.io/honuaio/honua-server:latest
```

### AWS ECR Public

```bash
# Login (requires AWS CLI)
aws ecr-public get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin public.ecr.aws

# Tag
docker tag honua-server:latest public.ecr.aws/honuaio/honua-server:latest

# Push
docker push public.ecr.aws/honuaio/honua-server:latest

# Pull
docker pull public.ecr.aws/honuaio/honua-server:latest
```

### Google Artifact Registry

```bash
# Configure auth (requires gcloud CLI)
gcloud auth configure-docker us-central1-docker.pkg.dev

# Tag
docker tag honua-server:latest \
  us-central1-docker.pkg.dev/project-id/honua-server/honua-server:latest

# Push
docker push us-central1-docker.pkg.dev/project-id/honua-server/honua-server:latest

# Pull
docker pull us-central1-docker.pkg.dev/project-id/honua-server/honua-server:latest
```

### Azure Container Registry

```bash
# Login (requires Azure CLI)
az acr login --name honuaacr

# Tag
docker tag honua-server:latest honuaacr.azurecr.io/honua-server:latest

# Push
docker push honuaacr.azurecr.io/honua-server:latest

# Pull
docker pull honuaacr.azurecr.io/honua-server:latest
```

---

## Troubleshooting

### View Container Logs

```bash
# View logs
docker logs honua-server

# Follow logs
docker logs -f honua-server

# Last 100 lines
docker logs --tail 100 honua-server

# With timestamps
docker logs -t honua-server
```

### Inspect Container

```bash
# View container details
docker inspect honua-server

# View environment variables
docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' honua-server

# View mounts
docker inspect -f '{{range .Mounts}}{{println .Source .Destination}}{{end}}' honua-server

# View network settings
docker inspect -f '{{json .NetworkSettings}}' honua-server | jq
```

### Execute Commands in Container

```bash
# Open shell (for standard images)
docker exec -it honua-server /bin/bash

# Run command
docker exec honua-server ps aux

# Check .NET runtime info
docker exec honua-server dotnet --info

# Check environment variables
docker exec honua-server env
```

### Debug Build Issues

```bash
# Build with no cache
docker build --no-cache -t honua-server:latest .

# Build with progress output
docker build --progress=plain -t honua-server:latest .

# View build history
docker history honua-server:latest

# Analyze image layers (use dive)
dive honua-server:latest
```

### Container Not Starting

```bash
# Check container status
docker ps -a --filter "name=honua-server"

# View exit code
docker inspect -f '{{.State.ExitCode}}' honua-server

# View error message
docker inspect -f '{{.State.Error}}' honua-server

# Try running interactively
docker run -it --rm \
  -e ConnectionStrings__DefaultConnection="..." \
  honua-server:latest
```

### Performance Analysis

```bash
# View container stats
docker stats honua-server

# View container top processes
docker top honua-server

# Export logs for analysis
docker logs honua-server > honua-server.log 2>&1
```

### Cleanup

```bash
# Remove stopped containers
docker container prune

# Remove unused images
docker image prune

# Remove all unused resources
docker system prune

# Remove everything (including volumes)
docker system prune -a --volumes

# Remove specific image
docker rmi honua-server:latest

# Force remove
docker rmi -f honua-server:latest
```

---

## Common Environment Variables

```bash
# Database
ConnectionStrings__DefaultConnection="Host=postgres;Database=honua;Username=honua;Password=..."

# Metadata
HONUA__METADATA__PROVIDER=postgres
HONUA__METADATA__AutoApplyMigrations=true

# Cache
HONUA__CACHE__PROVIDER=redis
HONUA__CACHE__REDIS__CONNECTIONSTRING=redis:6379

# Authentication
HONUA__AUTHENTICATION__MODE=Local
HONUA__AUTHENTICATION__JWT__SECRET=your-secret-key

# Logging
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Honua=Debug

# ASP.NET Core
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:8080

# .NET Runtime
DOTNET_TieredPGO=1
DOTNET_ReadyToRun=1
DOTNET_EnableDiagnostics=0
```

---

## Quick Links

- [Detailed Docker Deployment Guide](DOCKER_DEPLOYMENT.md)
- [Main README](../README.md)
- [Configuration Documentation](configuration/)
- [Deployment Guides](deployment/)
