# Configuration 2.0 - Best Practices

This guide provides best practices for organizing, securing, and managing Honua Configuration 2.0 files.

---

## Table of Contents

1. [File Organization](#file-organization)
2. [Security](#security)
3. [Version Control](#version-control)
4. [Environment Management](#environment-management)
5. [Validation](#validation)
6. [Performance](#performance)
7. [Documentation](#documentation)
8. [Testing](#testing)

---

## File Organization

### Recommended Structure

```
project/
├── config/
│   ├── honua.config.hcl          # Base configuration
│   ├── honua.development.hcl     # Development overrides
│   ├── honua.staging.hcl         # Staging overrides
│   ├── honua.production.hcl      # Production overrides
│   └── honua.test.hcl            # Test configuration
├── config/templates/             # Reusable templates
│   ├── data-sources.hcl
│   ├── services.hcl
│   └── common-layers.hcl
└── config/generated/             # Auto-generated configs
    └── database-layers.hcl
```

### Naming Conventions

**✅ Good**:
```hcl
data_source "postgres_primary"     # Clear, descriptive
layer "roads_primary"              # Matches table name
service "odata"                    # Service type
```

**❌ Bad**:
```hcl
data_source "db1"                  # Unclear purpose
layer "layer1"                     # Not descriptive
service "s1"                       # Ambiguous
```

### Modular Configuration

Break large configurations into modules:

```hcl
# honua.config.hcl (main file)
honua {
  version     = "1.0"
  environment = "production"
}

# Import common configurations
# (Future feature - not yet implemented)
# import "data-sources.hcl"
# import "services.hcl"
```

For now, use shell concatenation:

```bash
cat config/base.hcl \
    config/data-sources.hcl \
    config/services.hcl \
    config/layers.hcl \
    > honua.config.hcl
```

---

## Security

### 1. Never Hardcode Secrets

**❌ Bad**:
```hcl
data_source "db" {
  provider   = "postgresql"
  connection = "Host=localhost;Password=MySecretPassword123"
}
```

**✅ Good**:
```hcl
data_source "db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
}
```

### 2. Use Environment Variables for Sensitive Data

```bash
# .env (never commit this!)
DATABASE_URL="Host=prod.db.com;User=app;Password=***"
REDIS_URL="redis://prod.cache.com:6379"
API_KEY="sk-***"
```

Load before running:

```bash
export $(cat .env | xargs)
dotnet run
```

### 3. Different Secrets Per Environment

```bash
# Development
export DATABASE_URL="Host=localhost;Database=dev;..."

# Production (via CI/CD)
export DATABASE_URL="Host=prod.db.com;Database=prod;..."
```

### 4. Restrict CORS in Production

**❌ Bad** (Development only!):
```hcl
honua {
  cors {
    allow_any_origin = true  # ⚠️ Security risk!
  }
}
```

**✅ Good**:
```hcl
honua {
  cors {
    allow_any_origin = false
    allowed_origins  = [
      "https://app.example.com",
      "https://admin.example.com"
    ]
  }
}
```

### 5. Read-Only Services in Production

```hcl
service "odata" {
  enabled       = true
  allow_writes  = false  # ← Read-only in production
  max_page_size = 500    # ← Limit page size
}

rate_limit {
  enabled = true         # ← Enable rate limiting
  rules {
    default = {
      requests = 1000
      window   = "1m"
    }
  }
}
```

---

## Version Control

### 1. Always Commit Configuration

```gitignore
# .gitignore

# ✅ Commit these
honua.config.hcl
honua.*.hcl

# ❌ Never commit these
.env
.env.*
secrets.hcl
*.secrets.hcl
```

### 2. Use Meaningful Commit Messages

```bash
# Good
git commit -m "config: Add Redis caching for production"
git commit -m "config: Enable WFS service for parcels layer"
git commit -m "config: Update connection pool settings"

# Bad
git commit -m "update config"
git commit -m "changes"
```

### 3. Tag Configuration Versions

```bash
# Tag when deploying to production
git tag -a config-v1.2.0 -m "Production config: Added caching, rate limiting"
git push origin config-v1.2.0
```

### 4. Review Configuration Changes

Use pull requests for configuration changes:

```bash
# Validate before committing
honua config validate honua.config.hcl

# Preview changes
honua config plan honua.config.hcl --show-endpoints

# Create PR with validation results
git checkout -b config/add-wms-service
git add honua.config.hcl
git commit -m "config: Enable WMS service"
git push origin config/add-wms-service
```

---

## Environment Management

### 1. Use Environment-Specific Files

```hcl
# honua.config.hcl (base)
honua {
  version = "1.0"
  log_level = "information"
}

data_source "db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
  pool {
    min_size = 5
    max_size = 20
  }
}
```

```hcl
# honua.production.hcl (overrides)
honua {
  log_level = "warning"
}

data_source "db" {
  pool {
    min_size = 10
    max_size = 50
  }
}

cache "redis" {
  enabled = true
  connection = env("REDIS_URL")
}
```

### 2. Load Configuration Based on Environment

```bash
# Development
export HONUA_CONFIG=config/honua.development.hcl
dotnet run

# Production
export HONUA_CONFIG=config/honua.production.hcl
dotnet run
```

Or use environment variable to choose:

```csharp
var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var configPath = $"config/honua.{env.ToLower()}.hcl";
var config = await HonuaConfigLoader.LoadAsync(configPath);
```

### 3. Validate Per Environment

```bash
# Validate development config
honua config validate config/honua.development.hcl

# Validate production config (with database checks)
honua config validate config/honua.production.hcl --full
```

---

## Validation

### 1. Always Validate Before Deployment

```bash
# CI/CD Pipeline
honua config validate honua.production.hcl --full || exit 1
```

### 2. Validate on Every Change

```bash
# Git pre-commit hook
#!/bin/bash
if git diff --cached --name-only | grep -q "\.hcl$\|\.honua$"; then
    echo "Validating configuration..."
    honua config validate honua.config.hcl || exit 1
fi
```

### 3. Use Verbose Output for Debugging

```bash
honua config validate honua.config.hcl --verbose
```

### 4. Preview Before Applying

```bash
# See what will be configured
honua config plan honua.config.hcl --show-endpoints
```

---

## Performance

### 1. Use Field Introspection

**✅ Recommended**:
```hcl
layer "my_layer" {
  introspect_fields = true  # Automatic, always in sync
  # ...
}
```

**❌ Avoid** (unless necessary):
```hcl
layer "my_layer" {
  introspect_fields = false
  fields {
    # Manually list 50+ fields... 😰
  }
}
```

### 2. Optimize Connection Pools

```hcl
data_source "db" {
  pool {
    min_size = 10   # Keep connections warm
    max_size = 50   # Prevent connection exhaustion
    timeout  = 30   # Fail fast
  }
}
```

**Guidelines**:
- **Development**: `min_size = 2`, `max_size = 10`
- **Production**: `min_size = 10`, `max_size = 50`
- **High-traffic**: `min_size = 20`, `max_size = 100`

### 3. Enable Caching in Production

```hcl
cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")
  prefix     = "honua:"
  ttl        = 3600  # 1 hour
}
```

### 4. Configure Rate Limiting

```hcl
rate_limit {
  enabled = true
  store   = "redis"  # Use Redis for distributed scenarios

  rules {
    default = {
      requests = 1000
      window   = "1m"
    }

    authenticated = {
      requests = 10000
      window   = "1m"
    }
  }
}
```

### 5. Limit Page Sizes

```hcl
service "odata" {
  max_page_size     = 500   # Prevent huge queries
  default_page_size = 100   # Reasonable default
}

service "ogc_api" {
  item_limit = 1000         # Max items per request
}
```

---

## Documentation

### 1. Add Comments to Configuration

```hcl
# Production database - Primary PostgreSQL cluster
data_source "postgres_primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 10  # Maintain warm connections
    max_size = 50  # Prevent exhaustion under load
  }

  # Verify connection on startup
  health_check = "SELECT 1"
}

# Roads layer - Updated nightly from DOT data feed
layer "roads_primary" {
  title         = "Primary Roads"
  data_source   = data_source.postgres_primary
  table         = "public.roads_primary"
  # ... rest of config
}
```

### 2. Document Environment Variables

Create `config/README.md`:

```markdown
# Configuration Environment Variables

Required for all environments:
- `DATABASE_URL` - PostgreSQL connection string

Required for production:
- `REDIS_URL` - Redis connection string
- `API_KEY` - External API authentication

Optional:
- `HONUA_CONFIG` - Path to config file (default: honua.config.hcl)
- `LOG_LEVEL` - Override log level
```

### 3. Keep a Changelog

```markdown
# Configuration Changelog

## [1.2.0] - 2025-11-11
### Added
- Redis caching for production
- WMS service support
- Rate limiting rules

### Changed
- Increased connection pool size to 50
- Changed default page size to 100

### Fixed
- Corrected SRID for parcels layer (was 4326, now 3857)
```

---

## Testing

### 1. Use Test-Specific Configuration

```hcl
# honua.test.hcl
honua {
  version     = "1.0"
  environment = "test"
  log_level   = "debug"  # More verbose in tests
}

data_source "test_db" {
  provider   = "sqlite"
  connection = "Data Source=:memory:"  # In-memory for tests
}

service "odata" {
  enabled = true
  allow_writes = true  # Tests need write access
}
```

### 2. Test Configuration Changes

```bash
# Run tests with test configuration
export HONUA_CONFIG=config/honua.test.hcl
dotnet test

# Run integration tests
dotnet test --filter Category=Integration
```

### 3. Validate in CI/CD

```yaml
# .github/workflows/ci.yml
- name: Validate Configuration
  run: |
    honua config validate config/honua.config.hcl --syntax-only
    honua config validate config/honua.production.hcl --syntax-only
```

### 4. Test Configuration Loading

```csharp
[Fact]
public async Task Configuration_LoadsSuccessfully()
{
    // Arrange
    var configPath = "config/honua.test.hcl";

    // Act
    var config = await HonuaConfigLoader.LoadAsync(configPath);

    // Assert
    Assert.NotNull(config);
    Assert.Equal("test", config.Honua.Environment);
    Assert.True(config.Services.ContainsKey("odata"));
}
```

---

## Anti-Patterns to Avoid

### ❌ Duplicating Configuration

Don't copy-paste entire configurations:

```hcl
# Bad: Duplicated configuration
# honua.development.hcl
honua { ... entire config ... }
data_source "db" { ... }
service "odata" { ... }
# ... 500 lines

# honua.production.hcl
honua { ... entire config copy ... }
data_source "db" { ... }
service "odata" { ... }
# ... 500 lines (mostly duplicated)
```

Use base + overrides instead:

```hcl
# Good: honua.config.hcl (base)
# ... all common configuration

# Good: honua.production.hcl (overrides only)
honua {
  log_level = "warning"
}
cache "redis" {
  enabled = true
}
```

### ❌ Overly Complex Configurations

Keep it simple:

```hcl
# Bad: Too complex
layer "roads" {
  # ... 500 lines of explicit field definitions
  # ... complex conditional logic
  # ... dozens of settings
}

# Good: Simple and maintainable
layer "roads" {
  title            = "Roads"
  data_source      = data_source.db
  table            = "roads"
  id_field         = "id"
  introspect_fields = true  # ← Let the system handle it
  # ...
}
```

### ❌ Not Validating

Always validate before deploying:

```bash
# Bad: Deploy without validation
git push origin main

# Good: Validate first
honua config validate honua.config.hcl --full && \
git push origin main
```

### ❌ Hardcoding Environment-Specific Values

```hcl
# Bad: Hardcoded production URL
data_source "db" {
  connection = "Host=prod.db.com;Database=prod;..."  # ❌
}

# Good: Use environment variables
data_source "db" {
  connection = env("DATABASE_URL")  # ✅
}
```

---

## Checklist

### Development

- [ ] Use `introspect_fields = true` for layers
- [ ] Enable verbose logging (`log_level = "debug"`)
- [ ] Allow any CORS origin for local dev
- [ ] Use local database (SQLite or local PostgreSQL)
- [ ] Validate configuration on every change

### Staging

- [ ] Use production-like database
- [ ] Enable moderate logging (`log_level = "information"`)
- [ ] Restrict CORS to staging domains
- [ ] Test with Redis caching
- [ ] Run full validation including database checks

### Production

- [ ] Secrets via environment variables only
- [ ] Minimal logging (`log_level = "warning"`)
- [ ] Strict CORS configuration
- [ ] Enable Redis caching
- [ ] Enable rate limiting
- [ ] Read-only services where appropriate
- [ ] Optimized connection pools
- [ ] Full validation before deployment
- [ ] Monitor configuration changes

---

## Quick Reference Card

```hcl
# Minimal Best-Practice Configuration Template

honua {
  version     = "1.0"
  environment = env("ENVIRONMENT")
  log_level   = env("LOG_LEVEL")

  cors {
    allow_any_origin = false
    allowed_origins  = [env("ALLOWED_ORIGIN")]
  }
}

data_source "db" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")
  pool {
    min_size = 10
    max_size = 50
  }
  health_check = "SELECT 1"
}

cache "redis" {
  enabled    = true
  connection = env("REDIS_URL")
}

service "odata" {
  enabled       = true
  allow_writes  = false
  max_page_size = 500
}

layer "my_layer" {
  title            = "My Layer"
  data_source      = data_source.db
  table            = "schema.table"
  id_field         = "id"
  introspect_fields = true  # ← Best practice!

  geometry {
    column = "geom"
    type   = "Point"
    srid   = 4326
  }

  services = [service.odata]
}

rate_limit {
  enabled = true
  store   = "redis"
  rules {
    default = { requests = 1000, window = "1m" }
  }
}
```

---

## Further Reading

- [Complete Reference](./configuration-v2-reference.md)
- [Quick Start](./configuration-v2-quickstart.md)
- [Migration Guide](./configuration-v2-migration.md)
- [CLI Reference](./configuration-v2-cli.md)
