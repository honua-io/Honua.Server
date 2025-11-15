# Honua.Server Deployment Tiers

## Overview

Honua.Server is designed to scale from a single-server development setup to a multi-region highly-available deployment. Choose the tier that matches your operational capabilities and requirements.

---

## Tier 1: Simple (Development / Small Teams)

**Use Case**: Development, small teams (<10 users), personal projects, proof-of-concepts

**Infrastructure**:
- Single server (VM or container)
- PostgreSQL with PostGIS (local or managed)
- No external dependencies

**Configuration** (`honua.config.hcl`):
```hcl
honua {
  version     = "1.0"
  environment = "development"
  log_level   = "information"
}

# Single data source - that's it!
data_source "primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")  // or direct: "postgresql://localhost/honua"

  pool {
    min_size = 2
    max_size = 50    # Default for single instance
    timeout  = 30
  }
}

# Enable services you need
service "ogc_api" {
  enabled   = true
  base_path = "/ogc"
}

service "wfs" {
  enabled = true
}

# Optional: In-memory cache (no Redis needed)
cache "memory" {
  type    = "memory"
  enabled = true
}

# Note: WFS locks are in-memory by default
# WARNING: Multi-instance deployments require Tier 2+
```

**Pros**:
- ✅ Minimal operational complexity
- ✅ No external service dependencies
- ✅ Easy to understand and debug
- ✅ Low cost

**Cons**:
- ⚠️ Cannot run multiple instances (no distributed locks)
- ⚠️ Limited scalability (vertical scaling only)
- ⚠️ No cache sharing between restarts

**Performance**: 100-500 requests/second (single instance)

---

## Tier 2: Standard (Production / Medium Teams)

**Use Case**: Production deployments, 10-100 users, departmental systems, multi-instance HA

**Infrastructure**:
- 2-5 application servers (load balanced)
- PostgreSQL with PostGIS (managed service recommended)
- Redis (managed service or containerized)

**Configuration** (`honua.config.hcl`):
```hcl
honua {
  version     = "1.0"
  environment = "production"
  log_level   = "warning"

  allowed_hosts = ["honua.example.com", "*.honua.example.com"]

  cors {
    allowed_origins  = ["https://app.example.com"]
    allow_credentials = true
  }
}

# Primary database
data_source "primary" {
  provider   = "postgresql"
  connection = env("DATABASE_URL")

  pool {
    min_size = 2
    max_size = 120    # Higher for multi-instance (auto-scale: CPU cores * 15)
    timeout  = 30
  }

  health_check = "SELECT 1"
}

# Redis for distributed caching and coordination
cache "redis" {
  type       = "redis"
  enabled    = true
  connection = env("REDIS_URL")  // "redis-host:6379"

  # Require Redis in production
  required_in = ["production", "staging"]
}

# Distributed WFS locks (requires Redis)
service "wfs" {
  enabled = true

  settings = {
    lock_manager = "redis"           # Changed from "InMemory"
    lock_timeout = "00:05:00"        # 5 minutes
  }
}

service "ogc_api" {
  enabled   = true
  base_path = "/ogc"
}

# Background jobs with leader election
# Note: Still uses PostgreSQL polling (simpler than message queue)
# but adds leader election so only one instance processes jobs
```

**Additional Configuration** (via `appsettings.json` or environment variables):
```hcl
# In honua.config.hcl, you can't yet configure everything
# Some settings still need appsettings.json:

# Background Jobs (PostgreSQL polling with leader election)
variable "background_jobs" {
  mode             = "Polling"         # Simple, no message broker
  polling_interval = "00:00:02"        # 2 seconds (faster for HA)

  leader_election = {
    enabled                  = true
    provider                 = "Redis"
    resource_name            = "honua-geoprocessing"
    lease_duration_seconds   = 30
    renewal_interval_seconds = 10
  }
}
```

**Pros**:
- ✅ High availability (2+ instances)
- ✅ Horizontal scaling
- ✅ Shared cache across instances
- ✅ Still relatively simple (just add Redis)

**Cons**:
- Redis dependency (one more service to manage)
- Higher operational complexity

**Performance**: 1,000-5,000 requests/second (2-5 instances)

---

## Tier 3: Enterprise (High Traffic / Large Organizations)

**Use Case**: High-traffic production, 100+ users, SLA-backed services, enterprise deployments

**Infrastructure**:
- 10+ application servers (auto-scaling)
- PostgreSQL primary + read replicas
- Redis cluster (HA)
- Message queue (AWS SQS / Azure Service Bus / RabbitMQ)
- CDN (CloudFront / Fastly)

**Configuration** (`honua.production.hcl`):
```hcl
honua {
  version     = "1.0"
  environment = "production"
  log_level   = "warning"

  allowed_hosts = ["*.honua.example.com"]

  cors {
    allowed_origins   = ["https://app.example.com", "https://admin.example.com"]
    allow_credentials = true
  }

  # High availability configuration
  high_availability {
    enabled = true

    leader_election {
      enabled                  = true
      resource_name            = "honua-server"
      lease_duration_seconds   = 30
      renewal_interval_seconds = 10
      key_prefix               = "honua:leader:"
    }
  }
}

# Primary database (writes)
data_source "primary" {
  provider   = "postgresql"
  connection = env("DATABASE_PRIMARY_URL")

  pool {
    min_size = 5
    max_size = 240    # 15 per CPU core (16-core server)
    timeout  = 30
  }

  health_check = "SELECT 1"
}

# Read replica 1 (reads only)
data_source "replica_1" {
  provider   = "postgresql"
  connection = env("DATABASE_REPLICA_1_URL")

  pool {
    min_size = 2
    max_size = 120
    timeout  = 30
  }

  health_check = "SELECT 1"

  # Mark as read-only
  settings = {
    read_only = true
  }
}

# Read replica 2 (reads only)
data_source "replica_2" {
  provider   = "postgresql"
  connection = env("DATABASE_REPLICA_2_URL")

  pool {
    min_size = 2
    max_size = 120
    timeout  = 30
  }

  settings = {
    read_only = true
  }
}

# Redis cluster for caching and coordination
cache "redis" {
  type       = "redis"
  enabled    = true
  connection = env("REDIS_CLUSTER_URL")  // Points to Redis Sentinel or Cluster

  required_in = ["production"]

  settings = {
    cluster_mode          = true
    ssl                   = true
    connect_timeout       = 5000
    sync_timeout          = 5000
    abort_on_connect_fail = false
  }
}

# CDN configuration for tile caching
cache "cdn" {
  type    = "cdn"
  enabled = true

  settings = {
    provider      = "cloudfront"
    distribution  = env("CDN_DISTRIBUTION_ID")
    max_age       = 86400      # 1 day
    s_max_age     = 2592000    # 30 days (edge cache)
  }
}

# Services
service "wfs" {
  enabled = true

  settings = {
    lock_manager = "redis"
    lock_timeout = "00:05:00"
  }
}

service "ogc_api" {
  enabled   = true
  base_path = "/ogc"

  settings = {
    max_page_size  = 1000
    default_page_size = 100
    enable_caching = true
    cache_ttl      = 300  # 5 minutes
  }
}

service "stac" {
  enabled = true

  settings = {
    enable_caching = true
    cache_ttl      = 600  # 10 minutes
  }
}

# Rate limiting
rate_limit {
  enabled = true
  store   = "redis"  # Distributed rate limiting

  rules = {
    default = {
      requests = 1000
      window   = "1m"
    }

    authenticated = {
      requests = 5000
      window   = "1m"
    }

    tile_generation = {
      requests = 10000
      window   = "1m"
    }
  }
}

# Layers using read replicas
layer "features" {
  title        = "Feature Collection"
  data_source  = data_source.replica_1  # Route reads to replica
  table        = "features"
  id_field     = "id"

  geometry {
    column = "geom"
    type   = "Geometry"
    srid   = 4326
  }

  services = ["ogc_api", "wfs"]
}
```

**Additional Enterprise Configuration** (environment variables or separate config):

```bash
# Background Jobs - Message Queue Mode
BACKGROUND_JOBS__MODE=MessageQueue
BACKGROUND_JOBS__PROVIDER=AwsSqs
BACKGROUND_JOBS__QUEUE__URL=https://sqs.us-east-1.amazonaws.com/123456/honua-jobs
BACKGROUND_JOBS__QUEUE__MAX_CONCURRENCY=10
BACKGROUND_JOBS__QUEUE__VISIBILITY_TIMEOUT=00:05:00
BACKGROUND_JOBS__QUEUE__DEAD_LETTER_QUEUE=honua-jobs-dlq
BACKGROUND_JOBS__ENABLE_IDEMPOTENCY=true

# Database Read Replica Routing
DATABASE__ENABLE_READ_REPLICA_ROUTING=true
DATABASE__READ_REPLICA_OPERATIONS=Features,Observations,Tiles
DATABASE__FALLBACK_TO_PRIMARY=true

# Connection Pool Auto-Scaling
DATABASE__CONNECTION_POOL__AUTO_SCALE=true
DATABASE__CONNECTION_POOL__SCALE_FACTOR=15  # 15 connections per CPU core

# Load Shedding
RESILIENCE__LOAD_SHEDDING__ENABLED=true
RESILIENCE__LOAD_SHEDDING__CPU_THRESHOLD=0.90
RESILIENCE__LOAD_SHEDDING__QUEUE_THRESHOLD=1000

# SRE Features (Optional)
SRE__ENABLED=true
SRE__SLOS__LATENCY_SLO__ENABLED=true
SRE__SLOS__LATENCY_SLO__TARGET=0.99
SRE__SLOS__LATENCY_SLO__THRESHOLD_MS=500
SRE__ERROR_BUDGET__ENABLED=true
SRE__ERROR_BUDGET__DEPLOYMENT_GATING=true
SRE__ERROR_BUDGET__MINIMUM_BUDGET_REMAINING=0.25
```

**Pros**:
- ✅ Maximum performance and reliability
- ✅ Auto-scaling for traffic spikes
- ✅ SLA-ready with error budget tracking
- ✅ Geographic distribution

**Cons**:
- ⚠️ High operational complexity
- ⚠️ Requires dedicated DevOps/SRE team
- ⚠️ Higher infrastructure costs

**Performance**: 10,000-50,000 requests/second (auto-scaling)

---

## Feature Comparison Matrix

| Feature | Tier 1: Simple | Tier 2: Standard | Tier 3: Enterprise |
|---------|----------------|------------------|-------------------|
| **Instances** | 1 | 2-5 | 10+ (auto-scale) |
| **Distributed Cache** | ❌ Memory only | ✅ Redis | ✅ Redis Cluster |
| **WFS Locks** | ⚠️ In-memory | ✅ Redis | ✅ Redis |
| **Background Jobs** | Polling (PostgreSQL) | Polling + Leader Election | Message Queue (SQS/etc) |
| **Read Replicas** | ❌ | Optional | ✅ Required |
| **CDN** | ❌ | Optional | ✅ Required |
| **Load Shedding** | ❌ | Optional | ✅ Enabled |
| **SLO Tracking** | ❌ | Optional | ✅ Enabled |
| **Error Budgets** | ❌ | Optional | ✅ Enabled |
| **Max RPS** | 100-500 | 1,000-5,000 | 10,000-50,000 |
| **Operational Complexity** | Low | Medium | High |
| **Recommended For** | Dev/PoC | Production | Enterprise |

---

## Configuration Validation

Honua.Server validates your configuration at startup and warns about potential issues:

**Tier 1 with multiple instances (misconfigured)**:
```
[WARN] WFS lock_manager='InMemory' (default) with multiple instances detected.
       WFS-T transactions may encounter race conditions.
       Recommendation: Set service.wfs.settings.lock_manager='redis'
       and configure cache "redis" block.
```

**Tier 2 without Redis (misconfigured)**:
```
[ERROR] Cache 'redis' is required in environment 'production' but connection not configured.
        Set REDIS_URL environment variable or cache.redis.connection in config.
```

**Tier 3 properly configured**:
```
[INFO] Honua.Server starting in Tier 3 (Enterprise) mode
[INFO] Configuration V2 loaded: honua.production.hcl
[INFO] - Data sources: 3 (1 primary, 2 replicas)
[INFO] - Services: 4 enabled (ogc_api, wfs, stac, wmts)
[INFO] - Layers: 12
[INFO] - Cache: Redis cluster (HA mode)
[INFO] - WFS: Distributed locks (Redis)
[INFO] - Background jobs: Message queue (AWS SQS)
[INFO] - Leader election: Enabled (Redis)
[INFO] - SRE: Enabled (3 SLOs configured)
[INFO] All configuration validated successfully.
```

---

## Migration Path

### From Tier 1 → Tier 2

1. **Add Redis**:
   ```hcl
   cache "redis" {
     type       = "redis"
     enabled    = true
     connection = env("REDIS_URL")
   }
   ```

2. **Enable distributed WFS locks**:
   ```hcl
   service "wfs" {
     enabled = true
     settings = {
       lock_manager = "redis"  # Changed from default "InMemory"
     }
   }
   ```

3. **Scale to 2+ instances** behind load balancer

**Downtime**: Zero (backward compatible)

### From Tier 2 → Tier 3

1. **Add read replicas**:
   ```hcl
   data_source "replica_1" {
     provider   = "postgresql"
     connection = env("DATABASE_REPLICA_1_URL")
     settings = { read_only = true }
   }
   ```

2. **Enable read replica routing** (via environment variables)

3. **Add message queue** (AWS SQS / Azure Service Bus)

4. **Configure CDN** (CloudFront / Fastly)

5. **Enable auto-scaling** (Kubernetes HPA)

6. **Enable SLO tracking** (optional)

**Downtime**: Zero (gradual rollout)

---

## Environment-Specific Configs

Use file naming convention for different environments:

```
honua.development.hcl   # Tier 1: Single instance, in-memory cache
honua.staging.hcl       # Tier 2: Multi-instance, Redis
honua.production.hcl    # Tier 3: Full enterprise
```

**Automatic selection**:
```bash
# Development
ASPNETCORE_ENVIRONMENT=Development dotnet run
# Loads: honua.development.hcl

# Production
ASPNETCORE_ENVIRONMENT=Production dotnet run
# Loads: honua.production.hcl
```

---

## Philosophy

> **"Make the simple case simple, and the complex case possible."**

- **80% of users** should get great performance with Tier 1 (no extra dependencies)
- **15% of users** need Tier 2 (just add Redis)
- **5% of users** need Tier 3 (full enterprise features)

Honua.Server scales **with your operational maturity**, not against it.
