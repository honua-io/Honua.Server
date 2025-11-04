# Honua Kubernetes - Allowed Traffic Flows

This document provides a comprehensive visual reference for all allowed network traffic flows in the Honua Kubernetes deployment.

## Visual Traffic Flow Diagram

```
═══════════════════════════════════════════════════════════════════════════
                         EXTERNAL INTERNET
                         (Unrestricted)
                              │
                              │ Port 443 (HTTPS)
                              │ Port 80 (HTTP)
                              │
═══════════════════════════════════════════════════════════════════════════
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    INGRESS CONTROLLER NAMESPACE                           │
│                         (ingress-nginx)                                   │
│                                                                           │
│                      ┌──────────────────┐                                │
│                      │ Ingress Controller│                                │
│                      │  (NGINX/Traefik) │                                │
│                      │  • TLS Termination│                                │
│                      │  • Rate Limiting │                                │
│                      └────────┬──────────┘                                │
│                              │                                            │
└──────────────────────────────┼────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    │   Port 8080       │
                    │   (HTTP)          │
                    └─────────┬─────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                         HONUA NAMESPACE                                   │
│                                                                           │
│   ┌──────────────────────────────────────────────────────────────────┐   │
│   │                     Honua Server Pods                             │   │
│   │                   (app: honua-server)                             │   │
│   │                   Replicas: 2-10 (HPA)                            │   │
│   │                                                                   │   │
│   │   INGRESS ALLOWED FROM:                                           │   │
│   │   ✓ Ingress Controller (ns: ingress-nginx) → Port 8080           │   │
│   │   ✓ Same pods (app: honua-server) → Port 8080                    │   │
│   │   ✓ Monitoring (ns: monitoring) → Port 8080                       │   │
│   │                                                                   │   │
│   │   EGRESS ALLOWED TO:                                              │   │
│   │   ✓ PostgreSQL (port 5432)                                        │   │
│   │   ✓ Redis (port 6379)                                             │   │
│   │   ✓ DNS (port 53 UDP/TCP)                                         │   │
│   │   ✓ External HTTPS (port 443)                                     │   │
│   │   ✓ External HTTP (port 80)                                       │   │
│   │   ✓ OTLP Collector (ports 4317, 4318)                             │   │
│   │   ✓ Same pods (port 8080)                                         │   │
│   └─────────┬──────────────────────────┬────────────────────────────────┘   │
│             │                          │                                │
│    ┌────────▼──────┐          ┌────────▼────────┐                      │
│    │Port 5432      │          │Port 6379        │                      │
│    │               │          │                 │                      │
│   ┌▼───────────────▼┐        ┌▼─────────────────▼┐                     │
│   │  PostgreSQL/     │        │    Redis Cache    │                     │
│   │  PostGIS         │        │                   │                     │
│   │  StatefulSet     │        │    Deployment     │                     │
│   │  (app: postgis)  │        │    (app: redis)   │                     │
│   │                  │        │                   │                     │
│   │ INGRESS FROM:    │        │  INGRESS FROM:    │                     │
│   │ ✓ honua-server   │        │  ✓ honua-server   │                     │
│   │   → 5432         │        │    → 6379         │                     │
│   │ ✓ backup pods    │        │  ✓ monitoring     │                     │
│   │   → 5432         │        │    → 6379         │                     │
│   │ ✓ migration jobs │        │  ✓ same pods      │                     │
│   │   → 5432         │        │    → 6379/16379/  │                     │
│   │ ✓ monitoring     │        │      26379        │                     │
│   │   → 5432         │        │                   │                     │
│   │ ✓ same pods      │        │  EGRESS TO:       │                     │
│   │   → 5432         │        │  ✓ DNS (port 53)  │                     │
│   │                  │        │  ✓ same pods      │                     │
│   │ EGRESS TO:       │        │    → 6379/16379/  │                     │
│   │ ✓ DNS (port 53)  │        │      26379        │                     │
│   │ ✓ same pods      │        │                   │                     │
│   │   → 5432         │        │  ✗ NO INTERNET    │                     │
│   │                  │        │                   │                     │
│   │ ✗ NO INTERNET    │        └───────────────────┘                     │
│   │                  │                                                  │
│   └──────────────────┘                                                  │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
                              │
                    ┌─────────┴─────────┐
                    │   Port 8080       │
                    │   (Metrics/Health)│
                    └─────────┬─────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    MONITORING NAMESPACE                                   │
│                      (name: monitoring)                                   │
│                                                                           │
│   ┌──────────────────────────────────────────────────────────────────┐   │
│   │  • Prometheus (metrics scraping)                                 │   │
│   │  • Grafana (dashboards)                                          │   │
│   │  • postgres-exporter (database metrics)                          │   │
│   │  • redis-exporter (cache metrics)                                │   │
│   │                                                                  │   │
│   │  EGRESS ALLOWED TO:                                              │   │
│   │  ✓ Honua Server (port 8080) in honua namespace                  │   │
│   │  ✓ PostgreSQL (port 5432) in honua namespace                    │   │
│   │  ✓ Redis (port 6379) in honua namespace                         │   │
│   └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                  OBSERVABILITY NAMESPACE                                  │
│                  (name: observability)                                    │
│                                                                           │
│   ┌──────────────────────────────────────────────────────────────────┐   │
│   │  • OpenTelemetry Collector (OTLP 4317/4318)                      │   │
│   │  • Jaeger (distributed tracing)                                  │   │
│   │  • Loki (log aggregation)                                        │   │
│   │                                                                  │   │
│   │  INGRESS ALLOWED FROM:                                           │   │
│   │  ✓ Honua Server (ports 4317, 4318)                               │   │
│   └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                      KUBE-SYSTEM NAMESPACE                                │
│                                                                           │
│   ┌──────────────────────────────────────────────────────────────────┐   │
│   │  • CoreDNS / kube-dns                                            │   │
│   │                                                                  │   │
│   │  INGRESS ALLOWED FROM:                                           │   │
│   │  ✓ ALL PODS in ALL NAMESPACES → Port 53 (UDP/TCP)               │   │
│   └──────────────────────────────────────────────────────────────────┘   │
│                                                                           │
└───────────────────────────────────────────────────────────────────────────┘
```

## Traffic Flow Table

### Honua Server Pod Traffic

| Direction | Source/Destination | Port | Protocol | NetworkPolicy | Status |
|-----------|-------------------|------|----------|---------------|--------|
| **INGRESS** | Ingress Controller | 8080 | TCP | honua-server | ✓ Allowed |
| **INGRESS** | Other Honua Servers | 8080 | TCP | honua-server | ✓ Allowed |
| **INGRESS** | Monitoring namespace | 8080 | TCP | honua-server | ✓ Allowed |
| **INGRESS** | Generic pods in honua ns | 8080 | TCP | honua-server | ✗ Denied |
| **INGRESS** | Other namespaces | 8080 | TCP | namespace-isolation | ✗ Denied |
| **EGRESS** | PostgreSQL service | 5432 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | Redis service | 6379 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | kube-dns | 53 | UDP/TCP | honua-server | ✓ Allowed |
| **EGRESS** | Internet (HTTPS) | 443 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | Internet (HTTP) | 80 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | OTLP Collector | 4317 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | OTLP Collector | 4318 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | Other Honua Servers | 8080 | TCP | honua-server | ✓ Allowed |
| **EGRESS** | Random services | Any | TCP | default-deny-all | ✗ Denied |

### PostgreSQL Pod Traffic

| Direction | Source/Destination | Port | Protocol | NetworkPolicy | Status |
|-----------|-------------------|------|----------|---------------|--------|
| **INGRESS** | Honua Server pods | 5432 | TCP | postgis-database | ✓ Allowed |
| **INGRESS** | Backup pods | 5432 | TCP | postgis-database | ✓ Allowed |
| **INGRESS** | Migration jobs | 5432 | TCP | postgis-database | ✓ Allowed |
| **INGRESS** | Monitoring namespace | 5432 | TCP | postgis-database | ✓ Allowed |
| **INGRESS** | Other PostgreSQL pods | 5432 | TCP | postgis-database | ✓ Allowed |
| **INGRESS** | Generic pods | 5432 | TCP | postgis-database | ✗ Denied |
| **INGRESS** | Other namespaces | 5432 | TCP | namespace-isolation | ✗ Denied |
| **EGRESS** | kube-dns | 53 | UDP/TCP | postgis-database | ✓ Allowed |
| **EGRESS** | Other PostgreSQL pods | 5432 | TCP | postgis-database | ✓ Allowed |
| **EGRESS** | Internet | Any | TCP | postgis-database | ✗ Denied |

### Redis Pod Traffic

| Direction | Source/Destination | Port | Protocol | NetworkPolicy | Status |
|-----------|-------------------|------|----------|---------------|--------|
| **INGRESS** | Honua Server pods | 6379 | TCP | redis-cache | ✓ Allowed |
| **INGRESS** | Monitoring namespace | 6379 | TCP | redis-cache | ✓ Allowed |
| **INGRESS** | Other Redis pods | 6379 | TCP | redis-cache | ✓ Allowed |
| **INGRESS** | Other Redis pods | 16379 | TCP | redis-cache | ✓ Allowed |
| **INGRESS** | Generic pods | 6379 | TCP | redis-cache | ✗ Denied |
| **INGRESS** | Other namespaces | 6379 | TCP | namespace-isolation | ✗ Denied |
| **EGRESS** | kube-dns | 53 | UDP/TCP | redis-cache | ✓ Allowed |
| **EGRESS** | Other Redis pods | 6379 | TCP | redis-cache | ✓ Allowed |
| **EGRESS** | Other Redis pods | 16379 | TCP | redis-cache | ✓ Allowed |
| **EGRESS** | Other Redis pods | 26379 | TCP | redis-cache | ✓ Allowed |
| **EGRESS** | Internet | Any | TCP | redis-cache | ✗ Denied |

### Generic Pod Traffic (No app label)

| Direction | Source/Destination | Port | Protocol | NetworkPolicy | Status |
|-----------|-------------------|------|----------|---------------|--------|
| **INGRESS** | Any | Any | Any | default-deny-all | ✗ Denied |
| **EGRESS** | kube-dns | 53 | UDP/TCP | allow-dns-access | ✓ Allowed |
| **EGRESS** | Any other | Any | Any | default-deny-all | ✗ Denied |

### Cross-Namespace Traffic

| Source Namespace | Destination Namespace | Allowed? | Condition |
|-----------------|----------------------|----------|-----------|
| ingress-nginx | honua | ✓ Yes | To pods on port 8080 |
| monitoring | honua | ✓ Yes | To pods for metrics |
| observability | honua | ✓ Yes | For tracing/logging |
| honua | honua | ✓ Yes | Within same namespace |
| kube-system | Any | ✓ Yes | DNS (port 53) |
| Any other | honua | ✗ No | Blocked by namespace-isolation |
| honua | Any other | ✗ No | Blocked by default-deny (except DNS) |

## External Access Patterns

### From Honua Server to Internet

```
Honua Server Pod
    │
    ├─► AWS S3 (HTTPS:443)
    │   ✓ Allowed - Cloud storage for raster tiles
    │
    ├─► Azure Blob Storage (HTTPS:443)
    │   ✓ Allowed - Cloud storage for raster tiles
    │
    ├─► Google Cloud Storage (HTTPS:443)
    │   ✓ Allowed - Cloud storage for raster tiles
    │
    ├─► Weather APIs (HTTP:80, HTTPS:443)
    │   ✓ Allowed - External meteorological data
    │
    ├─► Raster Data Sources (HTTP:80, HTTPS:443)
    │   ✓ Allowed - Remote COG/Zarr datasets
    │
    ├─► OIDC Providers (HTTPS:443)
    │   ✓ Allowed - Azure AD, Auth0, Google OAuth
    │
    ├─► OpenAI API (HTTPS:443)
    │   ✓ Allowed - AI/LLM services
    │
    ├─► Azure OpenAI (HTTPS:443)
    │   ✓ Allowed - AI/LLM services
    │
    └─► Anthropic API (HTTPS:443)
        ✓ Allowed - AI/LLM services
```

### From Internet to Honua

```
External Client (Browser/API Client)
    │
    ├─► HTTPS (443) → Load Balancer
    │       │
    │       └─► Ingress Controller (ingress-nginx namespace)
    │               │
    │               └─► HTTP (8080) → Honua Server Pod
    │                       │
    │                       ├─► /api/* (OGC APIs, STAC, etc.)
    │                       ├─► /health (Health checks)
    │                       ├─► /ready (Readiness checks)
    │                       └─► /metrics (Prometheus metrics)
    │
    └─► Blocked - All other traffic denied
```

## Security Boundaries

### Trust Zones

```
┌────────────────────────────────────────────────────────────┐
│  ZONE 1: Internet (Untrusted)                              │
│  • External users                                          │
│  • External APIs and services                              │
│  • Cloud storage providers                                 │
└─────────────────────────┬──────────────────────────────────┘
                          │
                          │ TLS, Rate Limiting
                          │
┌─────────────────────────▼──────────────────────────────────┐
│  ZONE 2: Edge (DMZ)                                        │
│  • Ingress controller                                      │
│  • TLS termination                                         │
│  • Request routing                                         │
└─────────────────────────┬──────────────────────────────────┘
                          │
                          │ NetworkPolicy enforcement
                          │
┌─────────────────────────▼──────────────────────────────────┐
│  ZONE 3: Application Layer (Semi-Trusted)                  │
│  • Honua Server pods                                       │
│  • Health checks                                           │
│  • Business logic                                          │
│  • External API calls                                      │
└─────────────────────────┬──────────────────────────────────┘
                          │
                          │ Strict NetworkPolicy rules
                          │
┌─────────────────────────▼──────────────────────────────────┐
│  ZONE 4: Data Layer (Trusted, Isolated)                    │
│  • PostgreSQL/PostGIS                                      │
│  • Redis cache                                             │
│  • No internet access                                      │
│  • No lateral movement                                     │
└────────────────────────────────────────────────────────────┘
```

## Blocked Traffic Examples

### Common Blocked Scenarios

1. **Generic Pod → Database**
   ```
   Generic Pod (no app label)
       │
       X  Port 5432 (BLOCKED by postgis-database policy)
       │
   PostgreSQL Service
   ```

2. **Database → Internet**
   ```
   PostgreSQL Pod
       │
       X  Port 443 (BLOCKED by default-deny-all)
       │
   Internet (AWS, Azure, etc.)
   ```

3. **External Namespace → Honua**
   ```
   Pod in random-namespace
       │
       X  Port 8080 (BLOCKED by namespace-isolation)
       │
   Honua Server
   ```

4. **Generic Pod → Redis**
   ```
   Generic Pod (no app label)
       │
       X  Port 6379 (BLOCKED by redis-cache policy)
       │
   Redis Service
   ```

5. **Generic Pod → Internet**
   ```
   Generic Pod (no app label)
       │
       X  Port 443 (BLOCKED by default-deny-all)
       │
   Internet
   ```

## Policy Enforcement Order

NetworkPolicies are additive. The following order shows how policies build upon each other:

```
1. default-deny-all
   └─► DENY ALL ingress and egress
       │
       ├─► 2. allow-dns-access
       │   └─► ALLOW egress to kube-dns:53 (all pods)
       │
       ├─► 3. namespace-isolation
       │   └─► ALLOW ingress from same namespace
       │   └─► ALLOW ingress from ingress-nginx namespace
       │   └─► ALLOW ingress from monitoring namespace
       │   └─► ALLOW ingress from observability namespace
       │
       ├─► 4. honua-server
       │   └─► ALLOW specific ingress (ingress controller, monitoring)
       │   └─► ALLOW specific egress (postgres, redis, internet)
       │
       ├─► 5. postgis-database
       │   └─► ALLOW specific ingress (honua-server, backup, monitoring)
       │   └─► ALLOW specific egress (DNS, replication)
       │
       └─► 6. redis-cache
           └─► ALLOW specific ingress (honua-server, monitoring, cluster)
           └─► ALLOW specific egress (DNS, cluster)
```

**Result**: Only explicitly allowed traffic flows; all else is denied.

## Verification Tests

### Test Matrix

| Test Case | Expected Result | NetworkPolicy Verified |
|-----------|----------------|------------------------|
| Honua Server → PostgreSQL:5432 | ✓ Success | honua-server, postgis-database |
| Honua Server → Redis:6379 | ✓ Success | honua-server, redis-cache |
| Honua Server → Internet:443 | ✓ Success | honua-server |
| Honua Server → DNS:53 | ✓ Success | allow-dns-access |
| Generic Pod → PostgreSQL:5432 | ✗ Blocked | default-deny-all, postgis-database |
| Generic Pod → Redis:6379 | ✗ Blocked | default-deny-all, redis-cache |
| Generic Pod → Internet:443 | ✗ Blocked | default-deny-all |
| Generic Pod → DNS:53 | ✓ Success | allow-dns-access |
| External NS → Honua Server:8080 | ✗ Blocked | namespace-isolation |
| Ingress NS → Honua Server:8080 | ✓ Success | namespace-isolation, honua-server |
| Monitoring → Honua Server:8080 | ✓ Success | namespace-isolation, honua-server |
| Monitoring → PostgreSQL:5432 | ✓ Success | namespace-isolation, postgis-database |
| Monitoring → Redis:6379 | ✓ Success | namespace-isolation, redis-cache |
| PostgreSQL → Internet:443 | ✗ Blocked | default-deny-all |
| Redis → Internet:443 | ✗ Blocked | default-deny-all |

**Total Tests**: 15
**Expected Pass**: 15
**Expected Fail**: 0

## Summary

This NetworkPolicy implementation provides:

✓ **Zero-Trust Security**: Default deny all traffic
✓ **Explicit Allow Rules**: Only required traffic permitted
✓ **Namespace Isolation**: Cross-namespace traffic restricted
✓ **Data Layer Protection**: Databases/caches have no internet access
✓ **Least Privilege**: Minimal network access per component
✓ **Defense in Depth**: Multiple layers of network security
✓ **Comprehensive Coverage**: All pods and services covered
✓ **Production Ready**: Tested and documented

**Status**: ✓ Complete and Ready for Deployment

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Honua Version**: Latest
