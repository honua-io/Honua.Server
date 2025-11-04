# Honua.Server.Gateway - YARP API Gateway
# Multi-stage Dockerfile optimized for YARP reverse proxy
# Supports both x64 and ARM64 architectures

ARG DOTNET_VERSION=9.0
ARG RUNTIME_ARCH=linux-x64

# ============================================
# Stage 1: Base runtime image
# ============================================
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-alpine AS base
ARG RUNTIME_ARCH
WORKDIR /app

# Install required runtime dependencies
RUN apk add --no-cache \
    icu-libs \
    tzdata \
    curl \
    ca-certificates \
    && rm -rf /var/cache/apk/*

# Create non-root user for security
RUN addgroup -g 1000 honua \
    && adduser -D -u 1000 -G honua honua \
    && mkdir -p /app/logs /app/config \
    && chown -R honua:honua /app

# ============================================
# Stage 2: Build
# ============================================
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION}-alpine AS build
ARG RUNTIME_ARCH
WORKDIR /src

# Copy project files for dependency caching
COPY ["src/Honua.Server.Gateway/Honua.Server.Gateway.csproj", "src/Honua.Server.Gateway/"]

# Restore dependencies - layer cached until project files change
RUN dotnet restore "src/Honua.Server.Gateway/Honua.Server.Gateway.csproj" \
    -r ${RUNTIME_ARCH}

# Copy source code
COPY ["src/Honua.Server.Gateway/", "src/Honua.Server.Gateway/"]

# Build
WORKDIR "/src/src/Honua.Server.Gateway"
RUN dotnet build "Honua.Server.Gateway.csproj" \
    -c Release \
    -o /app/build \
    -r ${RUNTIME_ARCH} \
    --no-restore

# ============================================
# Stage 3: Publish
# ============================================
FROM build AS publish
ARG RUNTIME_ARCH
RUN dotnet publish "Honua.Server.Gateway.csproj" \
    -c Release \
    -o /app/publish \
    -r ${RUNTIME_ARCH} \
    --no-restore \
    --no-build \
    --self-contained false \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false

# ============================================
# Stage 4: Final runtime image
# ============================================
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish --chown=honua:honua /app/publish .

# Switch to non-root user
USER honua

# Expose ports
EXPOSE 8080 8443
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

# Environment variables
ENV ASPNETCORE_URLS=http://+:8080;https://+:8443 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_HTTPS_PORTS=8443 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    DOTNET_RUNNING_IN_CONTAINER=true

# Entrypoint
ENTRYPOINT ["dotnet", "Honua.Server.Gateway.dll"]
