# Honua Dev Container

This dev container provides a complete development environment for Honua with all dependencies pre-configured.

## What's Included

### Development Tools
- **.NET 9.0 SDK** - Latest .NET runtime and SDK
- **Git** - Version control
- **GitHub CLI** - GitHub integration
- **GDAL** - Geospatial data processing libraries
- **PROJ** - Cartographic projections library

### Database Services
- **PostgreSQL 16 with PostGIS 3.4** - Spatial database (port 5432)
- **SQL Server 2022 Express** - Microsoft SQL Server (port 1433)
- **MySQL 8.0** - MySQL database (port 3306)
- **Redis 7** - In-memory cache (port 6379)

### Cloud Storage Emulators (for Integration Tests)
- **LocalStack 3.0** - AWS S3 emulator (port 4566)
- **Azurite** - Azure Blob Storage emulator (port 10000)
- **fake-gcs-server** - Google Cloud Storage emulator (port 4443)

## Quick Start

### VS Code

1. Install the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
2. Open this project in VS Code
3. Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
4. Select **"Dev Containers: Reopen in Container"**
5. Wait for the container to build (first time only, ~5-10 minutes)

### GitHub Codespaces

1. Go to the repository on GitHub
2. Click the green **Code** button
3. Select **Codespaces** tab
4. Click **Create codespace on [branch]**
5. Wait for the environment to start

## Running Tests

### All Tests (Unit + Integration)
```bash
dotnet test
```

### Unit Tests Only
```bash
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

### Integration Tests Only
```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

### Specific Test Suite
```bash
# GCS integration tests
dotnet test --filter "FullyQualifiedName~GcsRasterTileCacheProviderIntegrationTests"

# S3 integration tests
dotnet test --filter "FullyQualifiedName~S3.*IntegrationTests"

# Azure integration tests
dotnet test --filter "FullyQualifiedName~Azure.*IntegrationTests"
```

## Services and Ports

| Service | Port | Purpose |
|---------|------|---------|
| PostgreSQL/PostGIS | 5432 | Primary spatial database |
| SQL Server | 1433 | Alternative database option |
| MySQL | 3306 | Alternative database option |
| Redis | 6379 | Caching |
| LocalStack (S3) | 4566 | AWS S3 emulator for tests |
| Azurite (Azure Blob) | 10000 | Azure Blob emulator for tests |
| GCS Emulator | 4443 | Google Cloud Storage emulator for tests |

## Environment Variables

The following environment variables are automatically configured:

### GDAL/PROJ
- `GDAL_DATA=/usr/share/gdal`
- `PROJ_LIB=/usr/share/proj`

### Database Connection Strings
- `ConnectionStrings__SqlServer=Server=sqlserver;Database=Honua;User Id=sa;Password=Honua123!;TrustServerCertificate=true;`
- `ConnectionStrings__PostgreSQL=Host=postgres;Database=honua;Username=postgres;Password=Honua123!`
- `ConnectionStrings__MySQL=Server=mysql;Database=honua;Uid=root;Pwd=Honua123!;`

### Cloud Storage Emulators
- `LOCALSTACK_ENDPOINT=http://localstack:4566`
- `AZURITE_BLOB_ENDPOINT=http://azurite:10000`
- `GCS_EMULATOR_ENDPOINT=http://gcs-emulator:4443`
- `AWS_ACCESS_KEY_ID=test`
- `AWS_SECRET_ACCESS_KEY=test`
- `AWS_DEFAULT_REGION=us-east-1`

## Health Checks

All emulators have health checks configured to ensure they're ready before tests run:

```bash
# LocalStack health check
curl http://localstack:4566/_localstack/health

# Azurite health check
curl http://azurite:10000/devstoreaccount1?comp=list

# GCS emulator health check
curl http://gcs-emulator:4443/storage/v1/b
```

## Customization

### Modifying Services

Edit `.devcontainer/docker-compose.yml` to:
- Add new services
- Change ports
- Modify environment variables
- Add volumes

### Adding VS Code Extensions

Edit `.devcontainer/devcontainer.json` under `customizations.vscode.extensions`:

```json
{
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csharp",
        "your-new-extension-id"
      ]
    }
  }
}
```

### Post-Create Commands

The `post-create.sh` script runs after the container is created. Edit `.devcontainer/post-create.sh` to add setup steps.

## Troubleshooting

### Container Won't Start

```bash
# Rebuild container from scratch
# In VS Code: Ctrl+Shift+P -> "Dev Containers: Rebuild Container"

# Or from command line:
docker-compose -f .devcontainer/docker-compose.yml down -v
docker-compose -f .devcontainer/docker-compose.yml build --no-cache
```

### Services Not Responding

```bash
# Check service health
docker-compose -f .devcontainer/docker-compose.yml ps

# View logs
docker-compose -f .devcontainer/docker-compose.yml logs localstack
docker-compose -f .devcontainer/docker-compose.yml logs azurite
docker-compose -f .devcontainer/docker-compose.yml logs gcs-emulator
```

### Integration Tests Failing

1. Verify emulators are healthy (see Health Checks above)
2. Check environment variables are set: `env | grep -E "(LOCALSTACK|AZURITE|GCS)"`
3. Restart services: `docker-compose -f .devcontainer/docker-compose.yml restart`

## Performance Tips

### Storage Volumes

The dev container uses named volumes for better performance:
- `honua-cache` - VS Code cache
- `honua-nuget` - NuGet packages cache
- `postgres-data` - PostgreSQL data
- `sqlserver-data` - SQL Server data
- `mysql-data` - MySQL data
- `localstack-data` - LocalStack data
- `azurite-data` - Azurite data
- `gcs-data` - GCS emulator data

### Resource Allocation

Increase Docker resources if services are slow:
- **Memory**: 8GB minimum, 16GB recommended
- **CPUs**: 4 cores minimum, 8 cores recommended
- **Disk**: 50GB minimum

Configure in Docker Desktop → Settings → Resources

## Learn More

- [Dev Containers Documentation](https://containers.dev/)
- [VS Code Dev Containers](https://code.visualstudio.com/docs/devcontainers/containers)
- [GitHub Codespaces](https://github.com/features/codespaces)
- [Testing Guide](../docs/TESTING.md)
