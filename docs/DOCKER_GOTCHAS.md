# Docker Deployment Gotchas and Important Notes

Critical information and common pitfalls when deploying Honua Server with Docker.

## Table of Contents

- [GDAL and Base Image Compatibility](#gdal-and-base-image-compatibility)
- [ReadyToRun Limitations](#readytorun-limitations)
- [Multi-Architecture Build Issues](#multi-architecture-build-issues)
- [Chiseled Images Constraints](#chiseled-images-constraints)
- [Performance Considerations](#performance-considerations)
- [Security Considerations](#security-considerations)
- [Cloud Platform Specifics](#cloud-platform-specifics)

---

## GDAL and Base Image Compatibility

### Critical: Alpine Linux is NOT Compatible with GDAL

**Problem:**
GDAL native libraries are compiled against glibc, but Alpine Linux uses musl libc. They are binary incompatible.

**Symptoms:**
```
System.DllNotFoundException: Unable to load shared library 'gdal' or one of its dependencies
```

**Solution:**
- Use Ubuntu-based images (Noble, Jammy) for Full variant
- Use Alpine ONLY for Lite variant (no GDAL dependencies)

**Current Configuration:**
```dockerfile
# Full (Dockerfile) - Ubuntu Noble Chiseled ✓
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime

# Lite (Dockerfile.lite) - Alpine ✓
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
```

### GDAL Dependencies in Chiseled Images

**Important:** Chiseled Ubuntu images are minimal but still glibc-based, making them compatible with GDAL.

**What's included:**
- glibc and essential runtime libraries
- No package manager (apt-get, dpkg)
- No shell utilities (bash, sh)
- GDAL native libraries from MaxRev.Gdal work correctly

**What's NOT included:**
- System utilities (curl, wget, tar, etc.)
- Debugging tools
- Package manager

**Workaround:** Copy utilities from other images:
```dockerfile
# Copy wget from busybox for health checks
COPY --from=busybox:stable-glibc /bin/wget /bin/wget
```

### GDAL Version Compatibility

**Current:** MaxRev.Gdal.Core 3.11.3.339

**Important notes:**
- MaxRev.Gdal packages include prebuilt native libraries
- Native libraries are platform-specific (linux-x64, linux-arm64)
- ReadyToRun compilation doesn't affect GDAL native code
- GDAL initialization happens at runtime, not build time

**Testing GDAL:**
```bash
# Run container and check GDAL
docker run -it honua-server:latest dotnet exec Honua.Server.Host.dll --version

# Check GDAL drivers
docker run -it honua-server:latest gdalinfo --formats
```

---

## ReadyToRun Limitations

### Architecture-Specific Compilation

**Critical:** ReadyToRun code is compiled for a specific architecture during build.

**What this means:**
- An amd64 R2R image will NOT run on arm64 (and vice versa)
- Cross-platform builds require building separately for each architecture
- Multi-arch manifests solve this by storing both architectures

**Current behavior:**
```bash
# Building with --platform linux/amd64 produces x64 R2R code
docker build --platform linux/amd64 -t image:amd64 .

# Building with --platform linux/arm64 produces ARM64 R2R code
docker build --platform linux/arm64 -t image:arm64 .
```

### Build Time Impact

**Expect:** 2-5 minutes longer build times with ReadyToRun enabled

**Why:**
- Additional ahead-of-time native code generation
- Platform-specific optimizations
- Larger intermediate build artifacts

**Trade-off:**
- Longer build times → Faster startup times
- Worth it for production and serverless deployments
- Consider disabling for local development builds

### R2R Warnings

**Common warning:**
```
Warning: Method '<method>' does not have R2R image
```

**Explanation:**
- Some methods can't be ahead-of-time compiled (reflection, dynamic code)
- These methods fall back to JIT compilation at runtime
- Usually harmless, but can be investigated

**Suppress warnings:**
```dockerfile
/p:PublishReadyToRunShowWarnings=false
```

### Trimming Compatibility

**Important:** ReadyToRun is compatible with trimming, but be careful:

```dockerfile
# Safe configuration
/p:PublishReadyToRun=true
/p:PublishTrimmed=true
/p:TrimMode=partial
```

**Avoid:**
- `TrimMode=full` with OData (breaks reflection-heavy code)
- Trimming GDAL or NetTopologySuite

**Current Lite configuration:**
```dockerfile
/p:PublishReadyToRun=true
/p:PublishTrimmed=true
/p:TrimMode=partial
/p:InvariantGlobalization=false  # Keep globalization support
```

---

## Multi-Architecture Build Issues

### Cross-Platform Build Performance

**Problem:** Building ARM64 images on x64 machines is SLOW (5-10x slower)

**Why:**
- QEMU emulation for cross-architecture builds
- Full .NET SDK compilation under emulation
- R2R compilation is CPU-intensive

**Solutions:**

1. **Use native ARM64 runners (recommended for CI/CD):**
   ```yaml
   # GitHub Actions
   - arch: arm64
     runner: ubuntu-latest-arm64
   ```

2. **Build on local ARM64 hardware (Apple Silicon):**
   ```bash
   docker build --platform linux/arm64 -t image:arm64 .
   ```

3. **Use Docker Buildx with remote builders:**
   ```bash
   docker buildx create --name remote-arm \
     --driver remote \
     --platform linux/arm64 \
     tcp://arm-build-server:1234
   ```

### Buildx Cache Issues

**Problem:** Cache may not work correctly across architectures

**Symptom:**
```
=> ERROR [internal] load metadata for mcr.microsoft.com/dotnet/sdk:9.0
```

**Solutions:**

1. **Use separate cache scopes:**
   ```yaml
   cache-from: type=gha,scope=build-${{ matrix.arch }}
   cache-to: type=gha,mode=max,scope=build-${{ matrix.arch }}
   ```

2. **Clear cache if builds fail:**
   ```bash
   docker buildx prune -f
   ```

### Manifest Creation Errors

**Problem:** Manifest creation fails if images aren't pushed yet

**Error:**
```
manifest list references non-existent manifest
```

**Solution:** Ensure individual arch images are pushed before creating manifest:
```bash
# Push individual images first
docker push honua-server:1.0.0-amd64
docker push honua-server:1.0.0-arm64

# Then create manifest
docker manifest create honua-server:1.0.0 \
  honua-server:1.0.0-amd64 \
  honua-server:1.0.0-arm64
```

---

## Chiseled Images Constraints

### No Package Manager

**Problem:** Can't install additional packages at runtime

**Workaround:** Copy from other images during build:
```dockerfile
# Copy debugging tools from another image
COPY --from=busybox:stable-glibc /bin/sh /bin/sh
COPY --from=busybox:stable-glibc /bin/ls /bin/ls
```

### Limited Debugging

**Problem:** No shell, no debugging tools

**Solutions:**

1. **Use standard Ubuntu base for debugging:**
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime
   ```

2. **Copy debugging tools:**
   ```dockerfile
   COPY --from=busybox:stable-glibc /bin/sh /bin/sh
   ```

3. **Use docker exec with dotnet CLI:**
   ```bash
   docker exec honua-server dotnet --info
   ```

### Health Check Limitations

**Problem:** Default health checks require curl/wget

**Current solution:**
```dockerfile
# Copy wget from busybox
COPY --from=busybox:stable-glibc /bin/wget /bin/wget

HEALTHCHECK CMD wget --no-verbose --tries=1 --spider \
  http://localhost:8080/healthz/live || exit 1
```

**Alternative:** Use .NET health check implementation (no external tools needed)

---

## Performance Considerations

### Memory Configuration

**Serverless (Lite) - Minimize memory footprint:**
```dockerfile
ENV DOTNET_GCServer=0 \
    DOTNET_GCRetainVM=0
```

**Long-running (Full) - Maximize throughput:**
```dockerfile
ENV DOTNET_GCServer=1 \
    DOTNET_GCRetainVM=1
```

**Why:**
- Server GC uses more threads, better for high-throughput
- Workstation GC uses less memory, better for bursty serverless

### Cold Start Optimization

**Critical for serverless:**

1. **Use Lite variant** (60MB vs 150MB)
2. **Enable ReadyToRun** (30-50% faster startup)
3. **Disable diagnostics:**
   ```dockerfile
   ENV DOTNET_EnableDiagnostics=0
   ```
4. **Use ARM64 on Graviton** (10-20% faster cold starts)

### Layer Caching

**Problem:** Changing source code invalidates all subsequent layers

**Solution:** Optimize layer order:
```dockerfile
# 1. Copy project files (rarely change)
COPY *.csproj ./

# 2. Restore dependencies (changes when deps change)
RUN dotnet restore

# 3. Copy source code (changes frequently)
COPY . ./

# 4. Build
RUN dotnet publish
```

---

## Security Considerations

### Running as Non-Root

**Important:** Both Dockerfiles run as non-root by default

**Full (chiseled):**
```dockerfile
USER app  # Built into chiseled images
```

**Lite (Alpine):**
```dockerfile
RUN adduser -D -u 1000 honua
USER honua
```

**Verify:**
```bash
docker exec honua-server id
# uid=1000(honua) gid=1000(honua) groups=1000(honua)
```

### Secrets Management

**Problem:** Don't bake secrets into images

**Bad:**
```dockerfile
ENV ConnectionStrings__DefaultConnection="Server=...;Password=secret123"
```

**Good:**
```bash
# Pass at runtime
docker run \
  -e ConnectionStrings__DefaultConnection="..." \
  honua-server:latest

# Or use secrets management
docker run \
  --env-file secrets.env \
  honua-server:latest
```

### Image Scanning

**Recommended:** Scan all images before deployment

```bash
# Trivy
trivy image --severity HIGH,CRITICAL honua-server:latest

# Grype
grype honua-server:latest

# Snyk
snyk container test honua-server:latest
```

---

## Cloud Platform Specifics

### AWS Lambda

**Critical Gotchas:**

1. **Lambda Web Adapter required:**
   ```dockerfile
   COPY --from=public.ecr.aws/awsguru/aws-lambda-adapter:0.8.4 \
     /lambda-adapter /opt/extensions/lambda-adapter
   ```

2. **Port must be 8080** (Lambda Web Adapter default)

3. **Startup time matters:**
   - Lambda bills for cold starts
   - Use Lite variant for cost savings
   - Consider Lambda SnapStart (Java-only, not for .NET)

4. **Graviton2/3 (ARM64) recommended:**
   - 34% price-performance improvement
   - Use `--architectures arm64`

### Google Cloud Run

**Critical Gotchas:**

1. **Must listen on $PORT:**
   ```dockerfile
   ENV PORT=8080
   ENV ASPNETCORE_URLS=http://+:${PORT}
   ```

2. **Request timeout:**
   - Max 60 minutes
   - Set appropriately for long-running operations

3. **ARM64 availability:**
   - Only in select regions (us-central1, europe-west1, asia-northeast1)
   - Check before deploying

### Azure Container Apps

**Critical Gotchas:**

1. **Ingress configuration:**
   - Must expose port 8080
   - External ingress for public access

2. **Min/max replicas:**
   - Min=0 for serverless (scale to zero)
   - Min=1 for production (avoid cold starts)

3. **Managed identity:**
   - Use for ACR authentication (no passwords)
   - Requires proper RBAC setup

### Kubernetes

**Critical Gotchas:**

1. **Resource requests/limits:**
   ```yaml
   resources:
     requests:
       memory: "512Mi"
       cpu: "500m"
     limits:
       memory: "1Gi"
       cpu: "1000m"
   ```

2. **Liveness vs Readiness:**
   - Liveness: `/healthz/live` (is app running?)
   - Readiness: `/healthz/ready` (is app ready for traffic?)

3. **ImagePullPolicy:**
   - Use `Always` for `latest` tag
   - Use `IfNotPresent` for versioned tags

---

## Summary of Critical Gotchas

1. **Never use Alpine for Full variant** (GDAL incompatibility)
2. **ReadyToRun is architecture-specific** (build separately for amd64/arm64)
3. **Chiseled images have no package manager** (copy tools during build)
4. **Cross-platform builds are slow** (use native runners for CI/CD)
5. **Use Lite variant for serverless** (60MB, faster cold starts)
6. **ARM64 provides cost savings** (Graviton, Azure ARM)
7. **Don't bake secrets into images** (pass at runtime)
8. **Layer caching order matters** (dependencies before source code)
9. **Health checks need tools in chiseled images** (copy wget/curl)
10. **Server GC vs Workstation GC** (choose based on workload)

---

## Additional Resources

- [Docker Deployment Guide](DOCKER_DEPLOYMENT.md)
- [Docker Quick Reference](DOCKER_QUICK_REFERENCE.md)
- [.NET Docker Best Practices](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/docker-application-development-process/docker-app-development-workflow)
- [GDAL Docker Images](https://gdal.org/download.html#docker-images)
- [Chiseled Ubuntu Images](https://devblogs.microsoft.com/dotnet/announcing-dotnet-chiseled-containers/)
