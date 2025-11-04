# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY NuGet.Config ./
COPY src/Honua.Server.Core/Honua.Server.Core.csproj src/Honua.Server.Core/
COPY src/Honua.Server.Host/Honua.Server.Host.csproj src/Honua.Server.Host/

COPY . ./

RUN dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
    --configuration Release \
    --output /app/publish \
    /p:UseAppHost=false

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
