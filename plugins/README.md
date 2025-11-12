# Honua Server Plugins

This directory contains plugins for Honua Server. Plugins provide extensibility for database providers, cloud storage, services (WFS, WMS, etc.), and more.

## Plugin Types

Honua Server supports several types of plugins:

| Plugin Type | Description | Example Plugins |
|------------|-------------|-----------------|
| **Service** | OGC and API services | WFS, WMS, WMTS, OGC API Features, STAC |
| **Database** | Database providers for storing and querying geospatial data | PostgreSQL, SQL Server, MySQL, MongoDB, BigQuery |
| **Cloud Storage** | Cloud storage providers for attachments and rasters | AWS S3, Azure Blob, Google Cloud Storage |
| **Exporter** | Export formats for geospatial data | Shapefile, GeoPackage, KML |
| **Auth Provider** | Authentication and authorization providers | OAuth, SAML, LDAP |
| **Extension** | Custom extensions and utilities | Any custom functionality |

## Directory Structure

Each plugin is contained in its own directory with the following structure:

```
plugins/
├── Honua.Server.Plugins.Database.PostgreSQL/
│   ├── Honua.Server.Plugins.Database.PostgreSQL.csproj
│   ├── PostgreSQLDatabasePlugin.cs
│   ├── plugin.json
│   └── README.md (optional)
├── Honua.Server.Plugins.Storage.S3/
│   ├── Honua.Server.Plugins.Storage.S3.csproj
│   ├── S3CloudStoragePlugin.cs
│   ├── plugin.json
│   └── README.md (optional)
└── ...
```

## Plugin Manifest (plugin.json)

Every plugin must include a `plugin.json` manifest file:

```json
{
  "id": "honua.plugins.database.postgresql",
  "name": "PostgreSQL/PostGIS Database Plugin",
  "version": "1.0.0",
  "description": "Full-featured PostgreSQL/PostGIS database provider",
  "author": "HonuaIO",
  "pluginType": "database",
  "assembly": "Honua.Server.Plugins.Database.PostgreSQL.dll",
  "entryPoint": "Honua.Server.Plugins.Database.PostgreSQL.PostgreSQLDatabasePlugin",
  "dependencies": [],
  "minimumHonuaVersion": "1.0.0"
}
```

## Implementing a Plugin

### 1. Database Plugin

Implement the `IDatabasePlugin` interface:

```csharp
public class PostgreSQLDatabasePlugin : IDatabasePlugin
{
    // IHonuaPlugin properties
    public string Id => "honua.plugins.database.postgresql";
    public string Name => "PostgreSQL/PostGIS Database Plugin";
    public string Version => "1.0.0";
    // ... other properties

    // IDatabasePlugin properties
    public string ProviderKey => "postgis";
    public DatabaseProviderType ProviderType => DatabaseProviderType.Relational;
    public IDataStoreCapabilities Capabilities => new PostgresDataStoreCapabilities();

    // Lifecycle methods
    public Task OnLoadAsync(PluginContext context, CancellationToken cancellationToken = default)
    {
        // Initialize plugin
        return Task.CompletedTask;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context)
    {
        // Register database provider
        services.AddKeyedSingleton<IDataStoreProvider>(ProviderKey, CreateProvider());
    }

    public PluginValidationResult ValidateConfiguration(IConfiguration configuration)
    {
        // Validate configuration
        var result = new PluginValidationResult();
        // Add validation logic
        return result;
    }

    public IDataStoreProvider CreateProvider()
    {
        return new PostgresDataStoreProvider();
    }
}
```

### 2. Cloud Storage Plugin

Implement the `ICloudStoragePlugin` interface:

```csharp
public class S3CloudStoragePlugin : ICloudStoragePlugin
{
    // IHonuaPlugin properties
    public string Id => "honua.plugins.storage.s3";
    public string Name => "AWS S3 Cloud Storage Plugin";
    public string Version => "1.0.0";
    // ... other properties

    // ICloudStoragePlugin properties
    public string ProviderKey => "s3";
    public CloudProviderType CloudProvider => CloudProviderType.AWS;
    public CloudStorageCapabilities Capabilities => new()
    {
        SupportsPresignedUrls = true,
        SupportsEncryption = true,
        SupportsVersioning = true,
        // ... other capabilities
    };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration, PluginContext context)
    {
        // Register cloud storage provider
        services.AddKeyedSingleton<IAttachmentStoreProvider>(ProviderKey, CreateProvider(configuration));
    }

    public IAttachmentStoreProvider CreateProvider(IConfiguration configuration)
    {
        return new S3AttachmentStoreProvider(/* config */);
    }
}
```

## Plugin Configuration

### Configuration V2 (HCL Format)

Plugins are configured using Honua's Configuration V2 (`.honua` files):

```hcl
# honua.config.hcl

honua {
  plugins {
    # Plugin discovery paths
    paths = ["./plugins", "/usr/local/lib/honua/plugins"]

    # Explicitly load only these plugins (optional)
    load = [
      "honua.plugins.database.postgresql",
      "honua.plugins.storage.s3"
    ]

    # Exclude these plugins (optional)
    exclude = ["honua.plugins.storage.azure"]

    # Enable hot reload in development
    enable_hot_reload = true
  }
}

# Database configuration
data_source "gis_db" {
  provider   = "postgresql"  # Matches ProviderKey from plugin
  connection = env("DATABASE_URL")

  pool {
    min_size = 10
    max_size = 50
  }
}

# Cloud storage configuration (future - not yet implemented in Config V2)
# Currently configured via appsettings.json
```

### Legacy Configuration (appsettings.json)

```json
{
  "Plugins": {
    "Paths": ["./plugins"],
    "Load": ["honua.plugins.database.postgresql"],
    "Exclude": []
  },
  "Honua": {
    "Attachments": {
      "Profiles": {
        "default": {
          "Provider": "s3",
          "S3": {
            "BucketName": "my-bucket",
            "Region": "us-east-1"
          }
        }
      }
    }
  }
}
```

## Plugin Discovery and Loading

Plugins are automatically discovered and loaded at application startup:

1. **Discovery**: The `PluginLoader` scans configured plugin paths
2. **Manifest Parsing**: Each plugin's `plugin.json` is parsed
3. **Filtering**: Plugins are filtered based on `load` and `exclude` configuration
4. **Loading**: Plugin assemblies are loaded using isolated `AssemblyLoadContext`
5. **Validation**: `ValidateConfiguration()` is called to check prerequisites
6. **Service Registration**: `ConfigureServices()` registers services in DI container
7. **Initialization**: `OnLoadAsync()` initializes the plugin

## Plugin Isolation

Plugins are loaded in isolated `AssemblyLoadContext` instances, which:

- Prevents version conflicts between plugins
- Enables hot-reload in development mode
- Allows plugins to use different versions of dependencies
- Shares core Honua assemblies (Honua.Server.Core) for efficiency

## Available Plugins

### Database Plugins

| Plugin | Provider Key | Description |
|--------|-------------|-------------|
| **PostgreSQL** | `postgis` | Full-featured PostgreSQL/PostGIS with native geometry support |
| SQL Server | `sqlserver` | Microsoft SQL Server with spatial types |
| MySQL | `mysql` | MySQL with spatial extensions |
| SQLite | `sqlite` | Lightweight file-based database with SpatiaLite |
| DuckDB | `duckdb` | Analytics-focused OLAP database |

### Cloud Storage Plugins

| Plugin | Provider Key | Description |
|--------|-------------|-------------|
| **AWS S3** | `s3` | Amazon S3 object storage |
| **Azure Blob** | `azureblob` | Microsoft Azure Blob Storage |
| **GCP Cloud Storage** | `gcs` | Google Cloud Storage |

### Service Plugins

| Plugin | Service ID | Description |
|--------|-----------|-------------|
| WFS | `wfs` | OGC Web Feature Service |
| WMS | `wms` | OGC Web Map Service |
| WMTS | `wmts` | OGC Web Map Tile Service |
| OGC API Features | `ogcapi` | Modern OGC API for Features |
| STAC | `stac` | SpatioTemporal Asset Catalog |

## Building Plugins

### Build a Single Plugin

```bash
dotnet build plugins/Honua.Server.Plugins.Database.PostgreSQL/
```

### Build All Plugins

```bash
for dir in plugins/*/; do
  dotnet build "$dir"
done
```

### Deploy Plugins

Copy the built plugin directory (including `plugin.json` and dependencies) to the Honua Server's plugin directory:

```bash
cp -r plugins/Honua.Server.Plugins.Database.PostgreSQL/bin/Release/net8.0/* \
  /path/to/honua-server/plugins/Honua.Server.Plugins.Database.PostgreSQL/
```

## Hot Reload

In development mode (`ASPNETCORE_ENVIRONMENT=Development`), plugins support hot reload:

1. Update plugin code
2. Rebuild the plugin
3. Call the reload API endpoint: `POST /api/plugins/{pluginId}/reload`
4. The plugin will be unloaded and reloaded without restarting the server

## Plugin Development Best Practices

1. **Isolation**: Don't share mutable state between plugins
2. **Validation**: Always validate configuration in `ValidateConfiguration()`
3. **Error Handling**: Use proper try-catch blocks and logging
4. **Dependencies**: Minimize external dependencies to reduce conflicts
5. **Documentation**: Include a README.md explaining configuration and usage
6. **Versioning**: Follow semantic versioning for plugin versions
7. **Testing**: Write unit tests for plugin functionality
8. **Logging**: Use the provided `ILogger` for all log messages

## Troubleshooting

### Plugin Not Loading

1. Check plugin manifest (`plugin.json`) is valid JSON
2. Verify `assembly` and `entryPoint` are correct
3. Check plugin is not excluded in configuration
4. Review logs for validation errors

### Assembly Load Errors

1. Ensure all dependencies are included in the plugin directory
2. Check for version conflicts with core assemblies
3. Verify target framework matches Honua Server (net8.0 or net9.0)

### Configuration Errors

1. Validate `ValidateConfiguration()` implementation
2. Check configuration paths match expected structure
3. Verify provider keys match between plugin and configuration

## API Reference

See the following files for detailed API documentation:

- `src/Honua.Server.Core/Plugins/IHonuaPlugin.cs` - Base plugin interface
- `src/Honua.Server.Core/Plugins/IServicePlugin.cs` - Service plugin interface
- `src/Honua.Server.Core/Plugins/IDatabasePlugin.cs` - Database plugin interface
- `src/Honua.Server.Core/Plugins/ICloudStoragePlugin.cs` - Cloud storage plugin interface
- `src/Honua.Server.Core/Plugins/PluginLoader.cs` - Plugin loading infrastructure

## Contributing

To contribute a new plugin:

1. Create a new directory under `plugins/`
2. Implement the appropriate plugin interface
3. Add a `plugin.json` manifest
4. Write tests for your plugin
5. Document configuration and usage
6. Submit a pull request

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
