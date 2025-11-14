# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy NuGet config and all project files only (for caching)
COPY NuGet.Config Directory.Build.props ./
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj 2>/dev/null || true); do \
      mkdir -p src/$(basename $file .csproj)/ && \
      mv $file src/$(basename $file .csproj)/; \
    done

# Restore with BuildKit cache mount for NuGet packages
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Honua.Server.Host/Honua.Server.Host.csproj

# Copy source code (this invalidates cache when code changes, but restore is cached)
COPY src/ ./src/

# Build and publish with cache mount (cache mount persists NuGet packages)
# Disable code analysis to avoid hitting Docker's 2MB log limit with warnings
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:RunAnalyzers=false \
    /p:EnforceCodeStyleInBuild=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_TieredPGO=1 \
    DOTNET_ReadyToRun=1

COPY --from=build /app/publish ./

EXPOSE 8080

# Health check using ASP.NET Core health endpoint
# Uses /healthz/live for liveness probe (checks if app is running)
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/healthz/live || exit 1
ENTRYPOINT ["dotnet", "Honua.Server.Host.dll"]
