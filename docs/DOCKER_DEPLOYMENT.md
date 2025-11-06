# Docker Deployment Strategy

Comprehensive guide for building, deploying, and publishing Honua Server container images across multiple architectures and cloud platforms.

## Table of Contents

- [Overview](#overview)
- [Image Variants](#image-variants)
- [Image Naming Convention](#image-naming-convention)
- [Base Image Selection](#base-image-selection)
- [Multi-Architecture Builds](#multi-architecture-builds)
- [Platform-Specific Deployments](#platform-specific-deployments)
- [Registry Publishing Strategy](#registry-publishing-strategy)
- [Security and Signing](#security-and-signing)
- [Performance Tuning](#performance-tuning)
- [Troubleshooting](#troubleshooting)

---

## Overview

Honua Server provides two optimized Docker image variants:

1. **Full** - Complete feature set with raster processing (GDAL), cloud SDKs, and all data providers
2. **Lite** - Vector-only, serverless-optimized build with minimal dependencies

Both variants support:
- Multi-architecture builds (linux/amd64, linux/arm64)
- ReadyToRun (R2R) ahead-of-time compilation for 30-50% faster cold starts
- Platform-specific optimizations for AWS Graviton, Azure ARM, and Google Cloud
- Security-hardened base images (non-root user, minimal attack surface)

---

## Image Variants

### Full Image (Dockerfile)

**Best for:** Production deployments, Kubernetes, traditional hosting, raster workloads

**Features:**
- Vector and raster data processing
- GDAL 3.11 for GeoTIFF, COG, and 150+ raster formats
- Cloud storage SDKs (AWS S3, Azure Blob, Google Cloud Storage)
- SkiaSharp for map rendering
- All database providers (PostgreSQL, MySQL, SQL Server, Oracle, Snowflake, BigQuery, etc.)
- Full OGC WCS 2.0 coverage support
- Enterprise geoprocessing capabilities

**Size:** ~150-180MB (compressed)

**Base Image:** `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled`
- Ubuntu 24.04 (Noble) chiseled image
- Ultra-minimal, container-optimized
- Non-root by default
- No package manager (security hardened)

**GDAL Compatibility:**
- Requires standard base images (Noble, Jammy) - NOT Alpine
- GDAL native libraries require glibc (not available in Alpine's musl libc)
- Chiseled images include glibc and work with GDAL

### Lite Image (Dockerfile.lite)

**Best for:** Serverless platforms, fast cold starts, vector-only workloads

**Features:**
- Vector geometry processing only (NetTopologySuite)
- PostgreSQL, MySQL, SQLite support
- OGC API Features, WFS 2.0/3.0, STAC 1.0
- Geoservices REST a.k.a. Geoservices REST a.k.a. Esri REST API vector services
- No raster processing (no GDAL)
- No cloud SDKs (smaller dependency tree)
- Optional trimming for even smaller size

**Size:** ~60-80MB (compressed)

**Base Image:** `mcr.microsoft.com/dotnet/aspnet:9.0-alpine`
- Alpine Linux 3.20 (ultra-lightweight)
- Suitable for vector-only workloads
- No GDAL dependencies required

**Cold Start Performance:**
- AWS Lambda: 1.5-2.5 seconds
- Google Cloud Run: 1.5-2.0 seconds
- Azure Container Apps: 2.0-2.5 seconds

---

## Image Naming Convention

All Honua images follow a consistent naming scheme across registries:

```
{registry}/{namespace}/honua-server:{tag}
```

### Tag Format

```
{version}[-{variant}][-{arch}]
```

**Examples:**

```bash
# Version-specific tags
honuaio/honua-server:1.0.0              # Multi-arch manifest (latest stable)
honuaio/honua-server:1.0.0-amd64        # x64 architecture
honuaio/honua-server:1.0.0-arm64        # ARM64 architecture

# Variant tags
honuaio/honua-server:1.0.0-lite         # Lite variant (multi-arch)
honuaio/honua-server:1.0.0-lite-amd64   # Lite variant (x64)
honuaio/honua-server:1.0.0-lite-arm64   # Lite variant (ARM64)

# Rolling tags
honuaio/honua-server:latest             # Latest stable release (full)
honuaio/honua-server:lite               # Latest stable release (lite)
honuaio/honua-server:dev                # Development branch
honuaio/honua-server:stable             # Alias for latest
```

### Registry-Specific Examples

```bash
# GitHub Container Registry (GHCR)
ghcr.io/honuaio/honua-server:1.0.0
ghcr.io/honuaio/honua-server:1.0.0-lite

# AWS Elastic Container Registry (ECR) Public
public.ecr.aws/honuaio/honua-server:1.0.0
public.ecr.aws/honuaio/honua-server:1.0.0-lite

# Google Container Registry (GCR)
gcr.io/honuaio/honua-server:1.0.0
gcr.io/honuaio/honua-server:1.0.0-lite

# Azure Container Registry (ACR)
honuaio.azurecr.io/honua-server:1.0.0
honuaio.azurecr.io/honua-server:1.0.0-lite

# Docker Hub (if used)
docker.io/honuaio/honua-server:1.0.0
docker.io/honuaio/honua-server:1.0.0-lite
```

---

## Base Image Selection

### Full Image: Ubuntu Noble Chiseled

**Dockerfile base:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime
```

**Why Chiseled?**
- 60% smaller than standard Ubuntu images (~110MB vs ~220MB)
- Contains only essential runtime libraries
- No package manager, shell, or utilities (reduced attack surface)
- Still compatible with GDAL native libraries (glibc-based)
- Regular security updates from Canonical

**Considerations:**
- Debugging is harder (no shell, no package manager)
- Use busybox or wget for health checks (copied in Dockerfile)
- GDAL native libraries work fine (verified)
- Cannot install additional packages at runtime

**Alternatives:**
```dockerfile
# Standard Ubuntu (easier debugging, slightly larger)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble

# Jammy (Ubuntu 22.04, LTS)
FROM mcr.microsoft.com/dotnet/aspnet:9.0-jammy
FROM mcr.microsoft.com/dotnet/aspnet:9.0-jammy-chiseled
```

### Lite Image: Alpine Linux

**Dockerfile.lite base:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
```

**Why Alpine?**
- Smallest possible image size (~60MB base)
- Perfect for serverless and vector-only workloads
- musl libc (not glibc) - incompatible with GDAL
- Built-in package manager (apk) for runtime tools
- Excellent for containers that don't need native dependencies

**Important:** Alpine is NOT compatible with GDAL. Use only for Lite builds.

---

## Multi-Architecture Builds

### Supported Architectures

| Architecture | Platform        | Use Case                                    |
|--------------|-----------------|---------------------------------------------|
| linux/amd64  | x86_64 (Intel/AMD) | Traditional servers, most cloud platforms |
| linux/arm64  | aarch64 (ARM)   | AWS Graviton, Azure ARM VMs, Apple Silicon |

### Building for Specific Architecture

**Manual build (local):**
```bash
# Build for x64
docker build --platform linux/amd64 \
  -t honuaio/honua-server:1.0.0-amd64 \
  -f Dockerfile .

# Build for ARM64
docker build --platform linux/arm64 \
  -t honuaio/honua-server:1.0.0-arm64 \
  -f Dockerfile .

# Build Lite variant for ARM64 (serverless)
docker build --platform linux/arm64 \
  -t honuaio/honua-server:1.0.0-lite-arm64 \
  -f Dockerfile.lite .
```

**Important:** Cross-platform builds require:
- Docker Desktop with containerd image store enabled, OR
- Docker Buildx with QEMU emulation, OR
- Native ARM64 build machines (recommended for production)

### Multi-Arch Manifest with Docker Buildx

**Setup Buildx (one-time):**
```bash
# Create new builder instance
docker buildx create --name multiarch \
  --driver docker-container \
  --bootstrap

# Use the builder
docker buildx use multiarch

# Verify platforms
docker buildx inspect --bootstrap
```

**Build and push multi-arch images:**
```bash
# Build Full image for both architectures
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t honuaio/honua-server:1.0.0 \
  -t honuaio/honua-server:latest \
  --push \
  -f Dockerfile .

# Build Lite image for both architectures
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  -t honuaio/honua-server:1.0.0-lite \
  -t honuaio/honua-server:lite \
  --push \
  -f Dockerfile.lite .
```

**What happens:**
1. Builds both amd64 and arm64 images in parallel
2. Pushes individual architecture images to registry
3. Creates a manifest list that references both
4. When users pull, Docker automatically selects the correct architecture

### Manual Manifest Creation

If you build architecture-specific images separately:

```bash
# Build and push individual images
docker build --platform linux/amd64 -t honuaio/honua-server:1.0.0-amd64 .
docker push honuaio/honua-server:1.0.0-amd64

docker build --platform linux/arm64 -t honuaio/honua-server:1.0.0-arm64 .
docker push honuaio/honua-server:1.0.0-arm64

# Create and push manifest
docker manifest create honuaio/honua-server:1.0.0 \
  honuaio/honua-server:1.0.0-amd64 \
  honuaio/honua-server:1.0.0-arm64

docker manifest push honuaio/honua-server:1.0.0

# Create latest tag
docker manifest create honuaio/honua-server:latest \
  honuaio/honua-server:1.0.0-amd64 \
  honuaio/honua-server:1.0.0-arm64

docker manifest push honuaio/honua-server:latest
```

### ReadyToRun and Multi-Arch

**Current configuration (Dockerfile):**
```dockerfile
RUN dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true \
    /p:PublishReadyToRunShowWarnings=true
```

**How it works:**
- ReadyToRun compilation is architecture-specific
- Building with `--platform linux/amd64` produces x64 R2R code
- Building with `--platform linux/arm64` produces ARM64 R2R code
- Each image contains optimized native code for its target platform

**Performance impact:**
- 30-50% faster cold starts (critical for serverless)
- 10-20% better steady-state performance
- Slightly larger image size (~15-20MB per architecture)

---

## Platform-Specific Deployments

### AWS Lambda (Container Image)

**Architecture:** ARM64 (Graviton2/3) recommended for cost savings

**Requirements:**
- Image must implement the Lambda Runtime API OR use AWS Lambda Web Adapter
- Maximum uncompressed size: 10GB
- Recommended: Use Lite variant for faster cold starts

**Option 1: AWS Lambda Web Adapter (Recommended)**

Install the Lambda Web Adapter as a Lambda layer or copy into the image:

```dockerfile
# Add to Dockerfile
COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:0.8.4 /lambda-adapter /opt/extensions/lambda-adapter

ENV AWS_LAMBDA_EXEC_WRAPPER=/opt/bootstrap
```

**Option 2: Custom Lambda Handler**

Implement `Amazon.Lambda.AspNetCoreServer` in your application.

**ECR Repository Setup:**
```bash
# Authenticate to ECR
aws ecr-public get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin public.ecr.aws

# Create repository
aws ecr-public create-repository \
  --repository-name honua-server \
  --region us-east-1

# Tag and push
docker tag honuaio/honua-server:1.0.0-lite-arm64 \
  public.ecr.aws/your-alias/honua-server:1.0.0-lite-arm64

docker push public.ecr.aws/your-alias/honua-server:1.0.0-lite-arm64
```

**Lambda Function Configuration:**
```bash
aws lambda create-function \
  --function-name honua-server \
  --package-type Image \
  --code ImageUri=public.ecr.aws/your-alias/honua-server:1.0.0-lite-arm64 \
  --role arn:aws:iam::123456789012:role/lambda-execution-role \
  --architectures arm64 \
  --memory-size 1024 \
  --timeout 30 \
  --environment Variables="{
    ConnectionStrings__DefaultConnection=Server=...,
    HONUA__METADATA__PROVIDER=postgres
  }"
```

**Graviton Benefits:**
- Up to 34% better price-performance vs x64
- Lower latency (typically 10-20% faster)
- Same or better performance

**Recommended configuration:**
- Memory: 1024-2048 MB
- Timeout: 30-60 seconds
- Ephemeral storage: 512 MB (default)
- Architecture: arm64 (Graviton)

### Google Cloud Run

**Architecture:** Both amd64 and arm64 supported (region-dependent)

**Requirements:**
- Image must listen on $PORT (default: 8080)
- Container must be stateless
- Maximum request timeout: 60 minutes

**Artifact Registry Setup:**
```bash
# Create repository
gcloud artifacts repositories create honua-server \
  --repository-format=docker \
  --location=us-central1 \
  --description="Honua Server container images"

# Configure Docker auth
gcloud auth configure-docker us-central1-docker.pkg.dev

# Tag and push
docker tag honuaio/honua-server:1.0.0-lite \
  us-central1-docker.pkg.dev/your-project/honua-server/honua-server:1.0.0-lite

docker push us-central1-docker.pkg.dev/your-project/honua-server/honua-server:1.0.0-lite
```

**Deploy to Cloud Run:**
```bash
gcloud run deploy honua-server \
  --image us-central1-docker.pkg.dev/your-project/honua-server/honua-server:1.0.0-lite \
  --platform managed \
  --region us-central1 \
  --allow-unauthenticated \
  --memory 512Mi \
  --cpu 1 \
  --min-instances 0 \
  --max-instances 10 \
  --concurrency 80 \
  --timeout 300s \
  --set-env-vars "ConnectionStrings__DefaultConnection=Server=..." \
  --set-env-vars "HONUA__METADATA__PROVIDER=postgres"
```

**ARM64 Support:**
- Available in select regions (us-central1, europe-west1, asia-northeast1)
- Use `--platform managed` with ARM64 images
- 15-20% cost savings vs x64

**Best Practices:**
- Use Lite variant for faster cold starts (<2s)
- Enable CPU throttling only when serving requests
- Use Cloud SQL Proxy for database connections
- Set `--min-instances 1` for production (avoid cold starts)

### Azure Container Apps

**Architecture:** Both amd64 and arm64 supported

**Requirements:**
- Image must be in ACR or public registry
- Port must be exposed (8080)

**ACR Setup:**
```bash
# Create registry
az acr create \
  --resource-group honua-rg \
  --name honuaacr \
  --sku Basic

# Login to ACR
az acr login --name honuaacr

# Tag and push
docker tag honuaio/honua-server:1.0.0-lite \
  honuaacr.azurecr.io/honua-server:1.0.0-lite

docker push honuaacr.azurecr.io/honua-server:1.0.0-lite
```

**Deploy Container App:**
```bash
# Create Container Apps environment
az containerapp env create \
  --name honua-env \
  --resource-group honua-rg \
  --location eastus

# Create container app
az containerapp create \
  --name honua-server \
  --resource-group honua-rg \
  --environment honua-env \
  --image honuaacr.azurecr.io/honua-server:1.0.0-lite \
  --target-port 8080 \
  --ingress external \
  --min-replicas 0 \
  --max-replicas 10 \
  --cpu 1.0 \
  --memory 2Gi \
  --env-vars \
    "ConnectionStrings__DefaultConnection=Server=..." \
    "HONUA__METADATA__PROVIDER=postgres"
```

**ARM64 Support:**
- Azure Container Apps supports ARM64 with D-series VMs
- Use `--workload-profile-name` to select ARM-based compute
- Cost savings similar to AWS Graviton

### Kubernetes (General)

**Deployment YAML:**
```yaml
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
      - name: honua-server
        image: honuaio/honua-server:1.0.0  # Multi-arch manifest
        ports:
        - containerPort: 8080
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: connection-string
        - name: HONUA__METADATA__PROVIDER
          value: "postgres"
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /healthz/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /healthz/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
---
apiVersion: v1
kind: Service
metadata:
  name: honua-server
spec:
  selector:
    app: honua-server
  ports:
  - port: 80
    targetPort: 8080
  type: LoadBalancer
```

**Multi-Arch Considerations:**
- Use manifest lists (no need to specify architecture)
- Kubernetes automatically pulls correct arch for each node
- Mixed-arch clusters work seamlessly (amd64 and arm64 nodes)

**Recommended:**
- Use HorizontalPodAutoscaler for scaling
- Use PodDisruptionBudgets for high availability
- Use ResourceQuotas and LimitRanges
- Enable NetworkPolicies for security

---

## Registry Publishing Strategy

### Recommended Multi-Registry Approach

**Primary Registry:** GitHub Container Registry (GHCR)
- Free for public repositories
- Integrated with GitHub Actions
- Excellent for open-source and development

**Cloud-Specific Registries:**

1. **AWS ECR Public** - For AWS Lambda and ECS deployments
2. **Google Artifact Registry** - For GCP Cloud Run and GKE
3. **Azure ACR** - For Azure Container Apps and AKS
4. **Docker Hub** (optional) - For broad community distribution

### GitHub Container Registry (GHCR)

**Authentication:**
```bash
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin
```

**Push images:**
```bash
docker tag honuaio/honua-server:1.0.0 \
  ghcr.io/honuaio/honua-server:1.0.0

docker push ghcr.io/honuaio/honua-server:1.0.0
```

**Benefits:**
- Free unlimited storage for public images
- Integrated with GitHub Actions (automatic authentication)
- Package visibility tied to repository visibility
- Supports image signing with Cosign

### AWS ECR Public

**Authentication:**
```bash
aws ecr-public get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin public.ecr.aws
```

**Create and push:**
```bash
aws ecr-public create-repository \
  --repository-name honua-server \
  --region us-east-1

docker tag honuaio/honua-server:1.0.0 \
  public.ecr.aws/honuaio/honua-server:1.0.0

docker push public.ecr.aws/honuaio/honua-server:1.0.0
```

**Benefits:**
- High bandwidth (no throttling for public images)
- Low latency for AWS services (Lambda, ECS, EKS)
- Free for public repositories
- Automatic replication across regions

### Google Artifact Registry

**Authentication:**
```bash
gcloud auth configure-docker us-central1-docker.pkg.dev
```

**Create and push:**
```bash
gcloud artifacts repositories create honua-server \
  --repository-format=docker \
  --location=us-central1

docker tag honuaio/honua-server:1.0.0 \
  us-central1-docker.pkg.dev/project-id/honua-server/honua-server:1.0.0

docker push us-central1-docker.pkg.dev/project-id/honua-server/honua-server:1.0.0
```

**Benefits:**
- Integrated with Cloud Run, GKE
- Low latency for GCP services
- Vulnerability scanning included
- Regional and multi-regional replication

### Azure Container Registry

**Authentication:**
```bash
az acr login --name honuaacr
```

**Create and push:**
```bash
az acr create --resource-group honua-rg --name honuaacr --sku Basic

docker tag honuaio/honua-server:1.0.0 \
  honuaacr.azurecr.io/honua-server:1.0.0

docker push honuaacr.azurecr.io/honua-server:1.0.0
```

**Benefits:**
- Integrated with AKS, Container Apps
- Geo-replication for global deployments
- Azure Defender security scanning
- Managed identities for authentication

### Docker Hub (Optional)

**Authentication:**
```bash
docker login docker.io -u USERNAME
```

**Push:**
```bash
docker tag honuaio/honua-server:1.0.0 \
  docker.io/honuaio/honua-server:1.0.0

docker push docker.io/honuaio/honua-server:1.0.0
```

**Considerations:**
- Pull rate limits (100 pulls/6h for anonymous, 200 pulls/6h for authenticated)
- Good for public community distribution
- Familiar to most users

---

## Security and Signing

### Image Signing with Cosign

**Install Cosign:**
```bash
# Linux
curl -LO https://github.com/sigstore/cosign/releases/latest/download/cosign-linux-amd64
sudo install cosign-linux-amd64 /usr/local/bin/cosign

# Verify installation
cosign version
```

**Generate key pair:**
```bash
cosign generate-key-pair
# Creates cosign.key (private) and cosign.pub (public)
```

**Sign images:**
```bash
# Sign with private key
cosign sign --key cosign.key ghcr.io/honuaio/honua-server:1.0.0

# Keyless signing (using OIDC)
cosign sign ghcr.io/honuaio/honua-server:1.0.0
```

**Verify signatures:**
```bash
# Verify with public key
cosign verify --key cosign.pub ghcr.io/honuaio/honua-server:1.0.0

# Verify keyless signature
cosign verify ghcr.io/honuaio/honua-server:1.0.0
```

### Vulnerability Scanning

**Trivy (recommended):**
```bash
# Scan local image
trivy image honuaio/honua-server:1.0.0

# Scan remote image
trivy image ghcr.io/honuaio/honua-server:1.0.0

# Scan and fail on high/critical
trivy image --severity HIGH,CRITICAL --exit-code 1 \
  honuaio/honua-server:1.0.0
```

**Grype:**
```bash
grype honuaio/honua-server:1.0.0
```

**Snyk:**
```bash
snyk container test honuaio/honua-server:1.0.0
```

### SBOM (Software Bill of Materials)

**Generate with Syft:**
```bash
# Generate SPDX format
syft honuaio/honua-server:1.0.0 -o spdx-json > sbom.spdx.json

# Generate CycloneDX format
syft honuaio/honua-server:1.0.0 -o cyclonedx-json > sbom.cdx.json
```

**Attach SBOM to image:**
```bash
cosign attach sbom --sbom sbom.spdx.json \
  ghcr.io/honuaio/honua-server:1.0.0
```

---

## Performance Tuning

### ReadyToRun Optimization

**Current configuration (recommended):**
```dockerfile
RUN dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true \
    /p:PublishReadyToRunShowWarnings=true
```

**Benefits:**
- 30-50% faster cold starts
- 10-20% better steady-state performance
- Platform-specific native code

**Trade-offs:**
- Larger image size (~15-20MB increase)
- Longer build times (additional compilation step)

### Runtime Environment Variables

**Performance tuning:**
```dockerfile
ENV DOTNET_TieredPGO=1 \
    DOTNET_ReadyToRun=1 \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_TieredCompilation=1
```

**Explanation:**
- `DOTNET_TieredPGO=1` - Enable Profile-Guided Optimization
- `DOTNET_ReadyToRun=1` - Use R2R compiled code
- `DOTNET_EnableDiagnostics=0` - Disable diagnostics (faster startup)
- `DOTNET_TieredCompilation=1` - Enable tiered JIT (better steady-state perf)

### Memory Configuration

**For serverless (Lite):**
```dockerfile
ENV DOTNET_GCServer=0 \
    DOTNET_GCRetainVM=0
```

**For long-running (Full):**
```dockerfile
ENV DOTNET_GCServer=1 \
    DOTNET_GCRetainVM=1
```

### Layer Caching Optimization

**Best practices:**
1. Copy dependency files first (least frequently changed)
2. Restore dependencies (cached if no changes)
3. Copy source code last (most frequently changed)
4. Use multi-stage builds to minimize final image size

**Example:**
```dockerfile
# Copy only project files for dependency restore
COPY src/Honua.Server.Core/Honua.Server.Core.csproj src/Honua.Server.Core/
COPY src/Honua.Server.Host/Honua.Server.Host.csproj src/Honua.Server.Host/
RUN dotnet restore src/Honua.Server.Host/Honua.Server.Host.csproj

# Copy source code after dependencies are restored
COPY . ./
RUN dotnet publish ...
```

---

## Troubleshooting

### GDAL Library Not Found

**Symptom:**
```
System.DllNotFoundException: Unable to load shared library 'gdal' or one of its dependencies
```

**Solution:**
- Ensure you're using Full image (not Lite)
- Verify base image is glibc-based (Noble/Jammy, NOT Alpine)
- Check GDAL runtime packages are installed:

```dockerfile
# For Ubuntu-based images
RUN apt-get update && apt-get install -y \
    gdal-bin \
    libgdal-dev \
    && rm -rf /var/lib/apt/lists/*
```

### Alpine + GDAL Incompatibility

**Problem:** GDAL native libraries are built for glibc, but Alpine uses musl libc

**Solution:** Use Ubuntu-based images for Full variant, Alpine for Lite

### Cross-Platform Build Issues

**Problem:** Building ARM64 images on x64 machines is slow or fails

**Solutions:**
1. Use native ARM64 runners (GitHub Actions: `ubuntu-latest-arm64`)
2. Enable QEMU emulation:
   ```bash
   docker run --rm --privileged multiarch/qemu-user-static --reset -p yes
   ```
3. Use Docker Buildx with proper driver configuration

### Container Health Check Failures

**Problem:** Health check fails in chiseled images

**Solution:** Chiseled images don't include curl/wget by default. Current Dockerfile copies wget from busybox:

```dockerfile
COPY --from=busybox:stable-glibc /bin/wget /bin/wget

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/healthz/live || exit 1
```

### ReadyToRun Build Warnings

**Problem:** R2R compilation shows warnings about uncompilable methods

**Solution:** These are usually harmless. To suppress:
```dockerfile
/p:PublishReadyToRunShowWarnings=false
```

To investigate specific warnings:
```dockerfile
/p:PublishReadyToRunShowWarnings=true
```

### Image Pull Rate Limits

**Problem:** Docker Hub rate limits exceeded

**Solutions:**
1. Authenticate to Docker Hub (increases limit)
2. Use alternative registries (GHCR, ECR Public)
3. Use image pull secrets in Kubernetes
4. Set up local registry cache/mirror

### Large Image Size

**Problem:** Image is larger than expected

**Diagnostics:**
```bash
# Analyze layers
docker history honuaio/honua-server:1.0.0

# Use dive for detailed analysis
dive honuaio/honua-server:1.0.0
```

**Solutions:**
1. Use Lite variant for vector-only workloads
2. Enable trimming (may break reflection-heavy code):
   ```dockerfile
   /p:PublishTrimmed=true \
   /p:TrimMode=partial
   ```
3. Remove unnecessary dependencies
4. Use chiseled base images

---

## Additional Resources

- [.NET Container Images](https://hub.docker.com/_/microsoft-dotnet-aspnet/)
- [Docker Buildx Documentation](https://docs.docker.com/buildx/)
- [Cosign Documentation](https://docs.sigstore.dev/cosign/overview/)
- [GDAL Docker Best Practices](https://gdal.org/download.html#docker-images)
- [AWS Lambda Container Images](https://docs.aws.amazon.com/lambda/latest/dg/images-create.html)
- [Google Cloud Run Documentation](https://cloud.google.com/run/docs)
- [Azure Container Apps Documentation](https://learn.microsoft.com/en-us/azure/container-apps/)

---

## Summary

**Key Takeaways:**

1. **Two Variants:** Full (raster + vector) and Lite (vector-only)
2. **Multi-Arch:** Support both amd64 and arm64 for cloud flexibility
3. **Base Images:** Ubuntu Noble Chiseled (Full), Alpine (Lite)
4. **GDAL:** Requires glibc-based images (NOT Alpine)
5. **ReadyToRun:** Essential for serverless cold start performance
6. **Registries:** Use GHCR for development, cloud-specific for production
7. **Security:** Sign images with Cosign, scan with Trivy
8. **Performance:** R2R + PGO + proper environment variables

**Recommended Workflow:**

1. Build both variants with multi-arch support
2. Push to GHCR for general use
3. Push to cloud-specific registries for production
4. Sign images for supply chain security
5. Generate and publish SBOMs
6. Automate with GitHub Actions (see `.github/workflows/build-and-push.yml`)
