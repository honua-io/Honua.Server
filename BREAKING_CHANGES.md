# Breaking Changes

This document tracks breaking changes in Honua Server releases.

---

## Current Version (Configuration V2 Mandatory)

### Legacy Configuration System Removed

**Date:** 2025-11-11
**Impact:** BREAKING CHANGE - Legacy configuration no longer supported
**Severity:** HIGH

#### What Changed

Legacy metadata.json configuration and the ConfigurationLoader system have been completely removed from Honua Server. Configuration V2 (HCL-based configuration) is now the only supported configuration method.

#### Migration Required

**ALL users must migrate to Configuration V2**. Applications using legacy metadata.json configuration will no longer start.

#### What Was Removed

1. **metadata.json configuration files** - No longer loaded or recognized
2. **ConfigurationLoader class** - Removed from codebase
3. **Legacy metadata providers** - JSON, PostgreSQL, and S3 metadata providers removed
4. **Environment variables for metadata** - `HONUA__METADATA__PROVIDER` and `HONUA__METADATA__PATH` no longer used

#### What You Need to Do

**1. Create HCL Configuration File**

Convert your existing metadata.json to HCL format:

```hcl
honua {
  version     = "1.0"
  environment = "production"
}

data_source "main_db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
}

service "ogc_api" {
  enabled = true
}

layer "features" {
  title       = "Features"
  data_source = data_source.main_db
  table       = "features"
  id_field    = "id"

  geometry {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }

  services = [service.ogc_api]
}
```

**2. Set HONUA_CONFIG Environment Variable**

Point to your new HCL configuration file:

```bash
export HONUA_CONFIG="./honua.config.hcl"
```

**3. Remove Legacy Environment Variables**

These are no longer needed:
```bash
# REMOVE THESE
unset HONUA__METADATA__PROVIDER
unset HONUA__METADATA__PATH
```

**4. Update Deployment Scripts**

Update your Docker, Kubernetes, or other deployment configurations to:
- Mount HCL configuration files instead of metadata.json
- Set `HONUA_CONFIG` environment variable
- Remove legacy metadata-related environment variables

#### Migration Tools

**Automated Migration:**
```bash
# Convert metadata.json to HCL
honua config convert metadata.json honua.config.hcl

# Validate new configuration
honua config validate honua.config.hcl
```

**Database Introspection:**
```bash
# Generate configuration from existing database
honua config introspect "$DATABASE_URL" \
  --output honua.config.hcl \
  --services ogc_api,wfs
```

#### Documentation

- **[Configuration V2 Reference](docs/configuration-v2-reference.md)** - Complete HCL configuration guide
- **[Migration Guide](docs/configuration-v2-migration.md)** - Detailed migration instructions
- **[Test Migration Guide](tests/CONFIGURATION_V2_MIGRATION_GUIDE.md)** - Migrating tests to Configuration V2

#### Benefits of Configuration V2

1. **Single Source of Truth** - One file instead of multiple configuration sources
2. **Validation Before Deployment** - Catch errors in seconds
3. **Type Safety** - Strong typing with schema validation
4. **Better IDE Support** - Syntax highlighting and validation in modern editors
5. **Database Introspection** - Auto-generate configuration from database schema
6. **No Manual Service Registration** - Services automatically registered from configuration

#### Examples

**Before (metadata.json):**
```json
{
  "dataSources": [
    {
      "id": "postgres_main",
      "provider": "PostgreSQL",
      "connectionString": "Host=localhost;Database=gis;..."
    }
  ],
  "services": [
    {
      "id": "odata_service",
      "type": "OData",
      "layers": [...]
    }
  ]
}
```

**After (honua.config.hcl):**
```hcl
data_source "postgres_main" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
}

service "odata" {
  enabled = true
}

layer "features" {
  data_source = data_source.postgres_main
  table       = "features"
  services    = [service.odata]
  # ... layer configuration
}
```

#### Support

If you encounter issues during migration:
- Review the [Migration Guide](docs/configuration-v2-migration.md)
- Check [Configuration V2 Reference](docs/configuration-v2-reference.md)
- Open an issue on GitHub: https://github.com/honua-io/Honua.Server/issues
- Contact support: support@honua.io

---

## Version History

### Previous Versions

No breaking changes were documented in previous versions.

---

**Note:** This file will be updated with each breaking change. Subscribe to the repository to receive notifications of updates.
