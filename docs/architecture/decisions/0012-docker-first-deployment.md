# 12. Docker-First Deployment Strategy

Date: 2025-10-17

Status: Accepted

## Context

Honua needs to support diverse deployment targets:
- Local development (Windows, macOS, Linux)
- On-premises servers
- Cloud platforms (AWS, Azure, GCP)
- Kubernetes clusters
- Edge deployments

**Requirements:**
- Consistent runtime across environments
- Simple deployment process
- Platform independence
- Efficient resource usage
- Production-grade container images

**Existing Evidence:**
- Dockerfile in repository root
- Docker Compose configurations: `/docker/docker-compose.yml`
- Multi-stage builds for optimized images
- Base image: `mcr.microsoft.com/dotnet/aspnet:9.0`

## Decision

Adopt **Docker as the primary deployment method** with support for native deployments where needed.

**Container Strategy:**
- Multi-stage Dockerfile for optimal image size
- .NET 9.0 runtime base images
- Health checks in container
- Non-root user for security
- Layer caching for fast builds

**Supported Deployments:**
1. Docker / Docker Compose (primary)
2. Kubernetes (via Helm charts)
3. AWS ECS / Fargate
4. Azure Container Apps
5. Google Cloud Run
6. Native binary (fallback)

## Consequences

### Positive

- **Consistency**: Same image runs everywhere
- **Isolation**: Dependencies bundled, no conflicts
- **Portability**: Works on any container platform
- **Orchestration**: Easy Kubernetes integration
- **Rollback**: Image tags enable easy rollback
- **CI/CD**: Natural fit for modern pipelines

### Negative

- **Image Size**: 150-300 MB per image
- **Build Time**: Multi-stage builds take time
- **Learning Curve**: Requires Docker knowledge
- **Runtime Overhead**: Small performance penalty vs native

### Neutral

- Container registries needed for distribution
- Must maintain Dockerfile alongside code

## Alternatives Considered

**Native Binaries Only**: Rejected - inconsistent environments
**VM Images**: Rejected - too heavyweight
**Snap/Flatpak**: Rejected - Linux-only

## Implementation

**Multi-stage Dockerfile:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 5000
USER $APP_UID
ENTRYPOINT ["dotnet", "Honua.Server.Host.dll"]
```

## Code Reference

- Dockerfile: `/Dockerfile`
- Docker Compose: `/docker/docker-compose.yml`

## References

- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [.NET Docker Images](https://hub.docker.com/_/microsoft-dotnet)

## Notes

Docker-first deployment aligns with cloud-native practices and simplifies multi-platform support.
