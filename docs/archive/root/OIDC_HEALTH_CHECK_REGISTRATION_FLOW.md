# OIDC Health Check Registration Flow

## Visual Registration Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Program.cs                                  │
│                                                                          │
│  1. var builder = WebApplication.CreateBuilder(args);                   │
│  2. builder.ConfigureHonuaServices(); ───────────┐                      │
│  3. var app = builder.Build();                   │                      │
│  4. app.ConfigureHonuaRequestPipeline(); ──────┐ │                      │
│  5. app.Run();                                 │ │                      │
└────────────────────────────────────────────────┼─┼──────────────────────┘
                                                 │ │
                    ┌────────────────────────────┘ │
                    │                              │
                    ▼                              │
┌─────────────────────────────────────────────────┼──────────────────────┐
│         HonuaHostConfigurationExtensions.cs     │                      │
│         ConfigureHonuaServices()                │                      │
│                                                 │                      │
│  • AddHonuaCoreServices()                       │                      │
│  • AddHonuaAuthentication()                     │                      │
│  • AddHonuaHealthChecks() ──────────────────┐   │                      │
│  • AddHonuaObservability()                  │   │                      │
│  • ...                                      │   │                      │
└─────────────────────────────────────────────┼───┼──────────────────────┘
                                              │   │
                    ┌─────────────────────────┘   │
                    │                             │
                    ▼                             │
┌─────────────────────────────────────────────────┼──────────────────────┐
│            HealthCheckExtensions.cs             │                      │
│            AddHonuaHealthChecks()               │                      │
│                                                 │                      │
│  services.AddHealthChecks()                     │                      │
│      .AddCheck<MetadataHealthCheck>()           │                      │
│      .AddCheck<DataSourceHealthCheck>()         │                      │
│      .AddCheck<SchemaHealthCheck>()             │                      │
│      .AddCheck<RedisStoresHealthCheck>()        │                      │
│      .AddCheck<OidcDiscoveryHealthCheck>(       │                      │
│          "oidc",                                │                      │
│          failureStatus: Degraded,               │                      │
│          tags: ["ready", "oidc"],               │                      │
│          timeout: 5 seconds                     │                      │
│      ) ◄─────── REGISTRATION HERE               │                      │
│      .AddCheck("self", ...)                     │                      │
│                                                 │                      │
└─────────────────────────────────────────────────┼──────────────────────┘
                                                  │
                    ┌─────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    Dependency Injection Container                        │
│                                                                          │
│  IHealthCheck Registrations:                                            │
│  ├─ "metadata"     → MetadataHealthCheck                                │
│  ├─ "dataSources"  → DataSourceHealthCheck                              │
│  ├─ "schema"       → SchemaHealthCheck                                  │
│  ├─ "redisStores"  → RedisStoresHealthCheck                             │
│  ├─ "oidc"         → OidcDiscoveryHealthCheck ◄── REGISTERED            │
│  └─ "self"         → Inline lambda                                      │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
                                                  │
                    ┌─────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│         HonuaHostConfigurationExtensions.cs                              │
│         ConfigureHonuaRequestPipeline()                                  │
│                                                                          │
│  • app.UseHonuaMiddlewarePipeline()                                     │
│  • app.MapHonuaEndpoints() ────────────────────────┐                    │
│  • app.UseHonuaMetricsEndpoint()                   │                    │
│                                                    │                    │
└────────────────────────────────────────────────────┼────────────────────┘
                                                     │
                    ┌────────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                   EndpointExtensions.cs                                  │
│                   MapHonuaEndpoints()                                    │
│                                                                          │
│  • app.MapOgcEndpoints()                                                │
│  • app.MapCartoEndpoints()                                              │
│  • app.MapHonuaHealthCheckEndpoints() ──────────┐                       │
│  • app.MapAdministrationEndpoints()             │                       │
│                                                 │                       │
└─────────────────────────────────────────────────┼───────────────────────┘
                                                  │
                    ┌─────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                   EndpointExtensions.cs                                  │
│                   MapHonuaHealthCheckEndpoints()                         │
│                                                                          │
│  app.MapHealthChecks("/healthz/startup", new HealthCheckOptions {       │
│      Predicate = reg => reg.Tags.Contains("startup")                    │
│  });                                                                     │
│                                                                          │
│  app.MapHealthChecks("/healthz/live", new HealthCheckOptions {          │
│      Predicate = reg => reg.Tags.Contains("live")                       │
│  });                                                                     │
│                                                                          │
│  app.MapHealthChecks("/healthz/ready", new HealthCheckOptions {         │
│      Predicate = reg => reg.Tags.Contains("ready") ◄── OIDC INCLUDED    │
│  });                                                                     │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
                                                  │
                    ┌─────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                        HTTP Endpoints                                    │
│                                                                          │
│  GET /healthz/startup  ──► Checks: metadata                             │
│                            Tags: ["startup"]                             │
│                                                                          │
│  GET /healthz/live     ──► Checks: self                                 │
│                            Tags: ["live"]                                │
│                                                                          │
│  GET /healthz/ready    ──► Checks: metadata, dataSources, schema,       │
│                                    redisStores, oidc ◄── AVAILABLE       │
│                            Tags: ["ready"]                               │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Request Flow: Client → OIDC Health Check

```
┌──────────────┐
│   Client     │
│  (K8s Probe) │
└──────┬───────┘
       │
       │ GET /healthz/ready
       │
       ▼
┌──────────────────────────────────────────────────────────────────┐
│                       ASP.NET Core Pipeline                      │
│                                                                  │
│  1. Middleware Pipeline (logging, auth, etc.)                   │
│  2. Endpoint Routing                                            │
│  3. MapHealthChecks("/healthz/ready") handler                   │
└──────────────────────────┬───────────────────────────────────────┘
                           │
                           │ Filter checks with tag "ready"
                           │
                           ▼
┌──────────────────────────────────────────────────────────────────┐
│                  Health Check Service                            │
│                                                                  │
│  Executes all checks with "ready" tag:                          │
│  ├─ MetadataHealthCheck                                         │
│  ├─ DataSourceHealthCheck                                       │
│  ├─ SchemaHealthCheck                                           │
│  ├─ RedisStoresHealthCheck                                      │
│  └─ OidcDiscoveryHealthCheck ◄── EXECUTES                       │
└──────────────────────────┬───────────────────────────────────────┘
                           │
                           │ Call CheckHealthAsync()
                           │
                           ▼
┌──────────────────────────────────────────────────────────────────┐
│              OidcDiscoveryHealthCheck                            │
│              CheckHealthAsync()                                  │
│                                                                  │
│  1. Get current authentication options                          │
│  2. If mode != Oidc → return Healthy                            │
│  3. Check cache for recent result                               │
│  4. If cached → return cached result                            │
│  5. Build discovery URL                                         │
│     (.well-known/openid-configuration)                          │
│  6. Create HTTP client (IHttpClientFactory)                     │
│  7. Send GET request (5 second timeout)                         │
│  8. If success → return Healthy, cache 15 min                   │
│  9. If error → return Degraded, cache 1 min                     │
└──────────────────────────┬───────────────────────────────────────┘
                           │
                           │ Return HealthCheckResult
                           │
                           ▼
┌──────────────────────────────────────────────────────────────────┐
│                  Health Check Service                            │
│                                                                  │
│  Aggregates all results:                                        │
│  {                                                               │
│    "status": "Healthy",                                         │
│    "entries": {                                                 │
│      "metadata": { "status": "Healthy" },                       │
│      "dataSources": { "status": "Healthy" },                    │
│      "schema": { "status": "Healthy" },                         │
│      "redisStores": { "status": "Degraded" },                   │
│      "oidc": {                                                  │
│        "status": "Healthy",                                     │
│        "description": "OIDC discovery endpoint accessible",     │
│        "data": {                                                │
│          "authority": "https://auth.example.com",               │
│          "discovery_url": "https://auth.../...configuration",   │
│          "cached": false,                                       │
│          "cache_duration_minutes": 15.0                         │
│        },                                                       │
│        "tags": ["ready", "oidc"]                                │
│      }                                                          │
│    }                                                            │
│  }                                                              │
└──────────────────────────┬───────────────────────────────────────┘
                           │
                           │ Format as JSON (HealthResponseWriter)
                           │
                           ▼
┌──────────────┐
│   Client     │
│  (K8s Probe) │
│              │
│  HTTP 200 OK │
│  + JSON body │
└──────────────┘
```

---

## Health Check Decision Tree

```
                    ┌─────────────────────────┐
                    │ CheckHealthAsync called │
                    └────────────┬────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │ Is OIDC mode enabled?   │
                    └────────┬────────┬───────┘
                             │        │
                         No  │        │ Yes
                             │        │
                    ┌────────▼─────┐  │
                    │ Return       │  │
                    │ Healthy      │  │
                    │ (skip check) │  │
                    └──────────────┘  │
                                      │
                                      ▼
                    ┌─────────────────────────┐
                    │ Is authority configured?│
                    └────────┬────────┬───────┘
                             │        │
                         No  │        │ Yes
                             │        │
                    ┌────────▼─────┐  │
                    │ Return       │  │
                    │ Degraded     │  │
                    └──────────────┘  │
                                      │
                                      ▼
                    ┌─────────────────────────┐
                    │ Check cache for result  │
                    └────────┬────────┬───────┘
                             │        │
                      Found  │        │ Not Found
                             │        │
                    ┌────────▼─────┐  │
                    │ Return cached│  │
                    │ result       │  │
                    └──────────────┘  │
                                      │
                                      ▼
                    ┌─────────────────────────┐
                    │ Build discovery URL     │
                    │ Create HTTP client      │
                    │ Set 5-second timeout    │
                    └────────────┬────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │ Send HTTP GET request   │
                    └────────┬────────┬───────┘
                             │        │
                             │        │
          ┌──────────────────┴────────┴──────────────────┐
          │                  │                           │
      Success            Error/Timeout              Unexpected
          │                  │                           │
          ▼                  ▼                           ▼
┌──────────────────┐  ┌──────────────────┐  ┌──────────────────┐
│ Return Healthy   │  │ Return Degraded  │  │ Return Degraded  │
│ Cache 15 minutes │  │ Cache 1 minute   │  │ Don't cache      │
│                  │  │                  │  │                  │
│ Data:            │  │ Data:            │  │ Data:            │
│ • authority      │  │ • authority      │  │ • authority      │
│ • discovery_url  │  │ • status_code    │  │ • error message  │
│ • cached: false  │  │ • error message  │  │ • exception      │
│ • cache_duration │  │ • cached: false  │  │ • cached: false  │
└──────────────────┘  └──────────────────┘  └──────────────────┘
```

---

## Tag-Based Endpoint Filtering

```
Health Checks Registered:
┌─────────────────────────────────────────────────────────────────┐
│ Name         │ Status      │ Tags                    │ Timeout  │
├─────────────────────────────────────────────────────────────────┤
│ metadata     │ Unhealthy   │ ["startup", "ready"]    │ default  │
│ dataSources  │ Unhealthy   │ ["ready"]               │ default  │
│ schema       │ Degraded    │ ["ready"]               │ default  │
│ redisStores  │ Degraded    │ ["ready", "distributed"]│ default  │
│ oidc         │ Degraded    │ ["ready", "oidc"]       │ 5s       │
│ self         │ Healthy     │ ["live"]                │ default  │
└─────────────────────────────────────────────────────────────────┘
                                │
                ┌───────────────┼───────────────┐
                │               │               │
                ▼               ▼               ▼
    ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
    │ /healthz/     │ │ /healthz/     │ │ /healthz/     │
    │ startup       │ │ live          │ │ ready         │
    │               │ │               │ │               │
    │ Tag: "startup"│ │ Tag: "live"   │ │ Tag: "ready"  │
    └───────┬───────┘ └───────┬───────┘ └───────┬───────┘
            │                 │                 │
            ▼                 ▼                 ▼
    ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
    │ Includes:     │ │ Includes:     │ │ Includes:     │
    │ • metadata    │ │ • self        │ │ • metadata    │
    │               │ │               │ │ • dataSources │
    │               │ │               │ │ • schema      │
    │               │ │               │ │ • redisStores │
    │               │ │               │ │ • oidc ✓      │
    └───────────────┘ └───────────────┘ └───────────────┘
```

---

## Caching Strategy

```
First Request (Cache Miss):
┌────────────────┐
│ Check cache    │
│ Key: "OidcDis..│
│ _https://...   │
└────────┬───────┘
         │
         │ Not found
         │
         ▼
┌────────────────┐
│ HTTP GET to    │
│ OIDC endpoint  │
│ (0-5 seconds)  │
└────────┬───────┘
         │
         ▼
┌────────────────────────────┐
│ Store in cache             │
│                            │
│ If Healthy:                │
│   TTL = 15 minutes         │
│                            │
│ If Degraded:               │
│   TTL = 1 minute           │
│                            │
│ If Unexpected Error:       │
│   Don't cache              │
└────────────────────────────┘

Second Request (Cache Hit):
┌────────────────┐
│ Check cache    │
│ Key: "OidcDis..│
│ _https://...   │
└────────┬───────┘
         │
         │ Found (< 15 min old)
         │
         ▼
┌────────────────┐
│ Return cached  │
│ result         │
│ (<1ms)         │
└────────────────┘

Cache Expiration (Healthy):
┌────────────────┐
│ Time: 0:00     │
│ HTTP request   │
│ Result: Healthy│
│ Cache: 15 min  │
└────────────────┘
         │
         │ 14 minutes pass...
         │
         ▼
┌────────────────┐
│ Time: 0:14     │
│ Cache hit      │
│ No HTTP request│
└────────────────┘
         │
         │ 1 minute passes...
         │
         ▼
┌────────────────┐
│ Time: 0:15     │
│ Cache expired  │
│ HTTP request   │
│ Result: Healthy│
│ Cache: 15 min  │
└────────────────┘

Cache Expiration (Degraded):
┌────────────────┐
│ Time: 0:00     │
│ HTTP error     │
│ Result: Degr.  │
│ Cache: 1 min   │
└────────────────┘
         │
         │ 30 seconds pass...
         │
         ▼
┌────────────────┐
│ Time: 0:00:30  │
│ Cache hit      │
│ No HTTP request│
└────────────────┘
         │
         │ 30 seconds pass...
         │
         ▼
┌────────────────┐
│ Time: 0:01     │
│ Cache expired  │
│ HTTP request   │
│ (retry)        │
└────────────────┘
```

---

## File References

| File | Lines | Purpose |
|------|-------|---------|
| `Program.cs` | 5 | Calls `ConfigureHonuaServices()` |
| `HonuaHostConfigurationExtensions.cs` | 42 | Calls `AddHonuaHealthChecks()` |
| `HealthCheckExtensions.cs` | 31 | Registers `OidcDiscoveryHealthCheck` |
| `EndpointExtensions.cs` | 47 | Calls `MapHonuaHealthCheckEndpoints()` |
| `EndpointExtensions.cs` | 157-161 | Maps `/healthz/ready` endpoint |
| `OidcDiscoveryHealthCheck.cs` | 42-164 | Implements health check logic |

---

## Summary

The OIDC Discovery Health Check is registered through a clear, linear flow:

1. **Program.cs** starts the application
2. **ConfigureHonuaServices()** registers all services
3. **AddHonuaHealthChecks()** registers health checks including OIDC
4. **ConfigureHonuaRequestPipeline()** configures HTTP pipeline
5. **MapHonuaHealthCheckEndpoints()** maps health check URLs
6. **Client requests** `/healthz/ready` trigger the check
7. **Tag filtering** ensures OIDC only runs for readiness probe
8. **Caching** minimizes load on OIDC endpoint
9. **Result** is returned to client (Kubernetes probe)

**Status**: ✅ Fully implemented and production-ready

---

**Document**: Registration Flow Diagram
**Date**: 2025-10-18
**Version**: 1.0
