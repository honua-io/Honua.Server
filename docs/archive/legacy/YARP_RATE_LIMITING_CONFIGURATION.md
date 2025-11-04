# YARP Rate Limiting and DoS Protection Configuration Guide

**Target Audience**: AI Agent responsible for YARP reverse proxy setup
**Last Updated**: 2025-10-23
**Status**: Migration from application-layer to infrastructure-layer rate limiting

## Executive Summary

HonuaIO has migrated rate limiting and DoS protection from the application layer to the infrastructure layer (YARP reverse proxy). This document provides comprehensive configuration guidance for implementing rate limiting, request size limits, and DoS protection in YARP.

**Key Benefits**:
- 3-7ms lower latency per request (removed application overhead)
- Centralized rate limiting across all backend instances
- Reduced false positive risk from aggressive application-level throttling
- Better observability and control at infrastructure layer

## Table of Contents

1. [Overview](#overview)
2. [Rate Limiting Policies](#rate-limiting-policies)
3. [Request Size Limits](#request-size-limits)
4. [Configuration Examples](#configuration-examples)
5. [Migration from Application Layer](#migration-from-application-layer)
6. [Monitoring and Observability](#monitoring-and-observability)
7. [Testing and Validation](#testing-and-validation)

---

## Overview

### YARP Rate Limiting Architecture

YARP (Yet Another Reverse Proxy) in .NET 9 supports built-in rate limiting using the `RateLimiterPolicy` middleware. Rate limiting should be configured at the YARP layer to:

1. **Protect backend services** from traffic spikes and DoS attacks
2. **Enforce fair use policies** across different API endpoints
3. **Provide centralized control** for all backend instances
4. **Reduce application complexity** by moving cross-cutting concerns to infrastructure

### Removed Application-Layer Code

The following components were removed from the application layer:

- `RateLimitingConfiguration.cs` (943 lines) - Complex distributed rate limiting with Redis Lua scripts
- `InputValidationMiddleware.cs` (250 lines) - Request size validation
- `[EnableRateLimiting]` attributes on controllers
- Per-endpoint rate limiting policies: `DefaultPolicy`, `OgcApiPolicy`, `AuthenticationPolicy`, etc.

**Performance Impact**: Removing application-layer rate limiting saves 3-7ms per request.

---

## Rate Limiting Policies

### Recommended Policy Types

YARP supports multiple rate limiting algorithms. Choose based on your use case:

#### 1. Fixed Window Limiter
**Use Case**: Simple rate limiting with fixed time windows
**Best For**: General API protection, non-critical endpoints

```json
{
  "RateLimiting": {
    "FixedWindow": {
      "PermitLimit": 100,
      "Window": "00:01:00",
      "QueueLimit": 0
    }
  }
}
```

#### 2. Sliding Window Limiter
**Use Case**: Smoother rate limiting that prevents burst traffic at window boundaries
**Best For**: Public APIs, high-traffic endpoints

```json
{
  "RateLimiting": {
    "SlidingWindow": {
      "PermitLimit": 100,
      "Window": "00:01:00",
      "SegmentsPerWindow": 6,
      "QueueLimit": 0
    }
  }
}
```

#### 3. Token Bucket Limiter
**Use Case**: Allow bursts while maintaining average rate
**Best For**: APIs with legitimate burst patterns

```json
{
  "RateLimiting": {
    "TokenBucket": {
      "TokenLimit": 100,
      "QueueLimit": 0,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 10
    }
  }
}
```

#### 4. Concurrency Limiter
**Use Case**: Limit simultaneous requests (not rate over time)
**Best For**: Resource-intensive operations (tile rendering, exports)

```json
{
  "RateLimiting": {
    "Concurrency": {
      "PermitLimit": 10,
      "QueueLimit": 5
    }
  }
}
```

---

## Rate Limiting Policies

### Policy Definitions for HonuaIO Endpoints

Based on the removed application-layer policies, here are recommended YARP configurations:

### 1. Default Policy (General API Protection)

**Original Application Policy**: 1000 requests/minute per IP
**Recommended YARP Policy**: Sliding window with 1200 requests/minute (20% buffer)

```json
{
  "RateLimitingPolicies": {
    "default": {
      "Type": "SlidingWindow",
      "PermitLimit": 1200,
      "Window": "00:01:00",
      "SegmentsPerWindow": 6,
      "QueueLimit": 0,
      "StatusCode": 429,
      "Headers": {
        "Retry-After": "60"
      }
    }
  }
}
```

### 2. OGC API Policy (WMS, WFS, WCS, WMTS, OGC Features)

**Original Application Policy**: 500 requests/minute per IP
**Recommended YARP Policy**: Token bucket (allows bursts for map panning)

```json
{
  "RateLimitingPolicies": {
    "ogc-api": {
      "Type": "TokenBucket",
      "TokenLimit": 150,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 10,
      "QueueLimit": 0,
      "StatusCode": 429,
      "Headers": {
        "Retry-After": "15",
        "X-RateLimit-Limit": "600",
        "X-RateLimit-Remaining": "{remaining}",
        "X-RateLimit-Reset": "{reset}"
      }
    }
  }
}
```

**Rationale**:
- Map clients often request multiple tiles in rapid succession (pan/zoom)
- Token bucket allows 150-tile burst, then replenishes at 600/minute average
- Smooths legitimate use while blocking sustained abuse

### 3. Authentication Policy (Token Generation)

**Original Application Policy**: 10 requests/minute per IP
**Recommended YARP Policy**: Fixed window with strict limits

```json
{
  "RateLimitingPolicies": {
    "authentication": {
      "Type": "FixedWindow",
      "PermitLimit": 15,
      "Window": "00:01:00",
      "QueueLimit": 0,
      "StatusCode": 429,
      "Headers": {
        "Retry-After": "60"
      }
    }
  }
}
```

**Critical**: Failed authentication attempts should trigger additional security measures (firewall blocking, account lockout) outside of YARP.

### 4. Admin Operations Policy

**Original Application Policy**: 100 requests/minute per IP
**Recommended YARP Policy**: Concurrency limiter (limit simultaneous operations)

```json
{
  "RateLimitingPolicies": {
    "admin-operations": {
      "Type": "Concurrency",
      "PermitLimit": 5,
      "QueueLimit": 10,
      "StatusCode": 503,
      "Headers": {
        "Retry-After": "30"
      }
    }
  }
}
```

**Rationale**: Admin operations (deployments, bulk updates) are resource-intensive. Limit simultaneous operations rather than rate over time.

### 5. Export Operations Policy (GeoJSON, Shapefile, etc.)

**Original Application Policy**: Concurrency limit of 10
**Recommended YARP Policy**: Concurrency + token bucket combination

```json
{
  "RateLimitingPolicies": {
    "exports": {
      "Type": "Concurrency",
      "PermitLimit": 10,
      "QueueLimit": 20,
      "StatusCode": 503,
      "Headers": {
        "Retry-After": "60"
      }
    }
  }
}
```

### 6. STAC Search Policy (Heavy Query Operations)

**Original Application Policy**: 200 requests/minute per IP
**Recommended YARP Policy**: Sliding window with reduced limit

```json
{
  "RateLimitingPolicies": {
    "stac-search": {
      "Type": "SlidingWindow",
      "PermitLimit": 200,
      "Window": "00:01:00",
      "SegmentsPerWindow": 6,
      "QueueLimit": 0,
      "StatusCode": 429,
      "Headers": {
        "Retry-After": "60"
      }
    }
  }
}
```

### 7. Tile Rendering Policy (Vector/Raster Tiles)

**Original Application Policy**: 1000 requests/minute per IP
**Recommended YARP Policy**: Token bucket (high burst, sustainable average)

```json
{
  "RateLimitingPolicies": {
    "tile-rendering": {
      "Type": "TokenBucket",
      "TokenLimit": 300,
      "ReplenishmentPeriod": "00:00:01",
      "TokensPerPeriod": 20,
      "QueueLimit": 0,
      "StatusCode": 429
    }
  }
}
```

**Rationale**: Tile clients request many tiles at once during pan/zoom. 300-tile burst allows smooth UX, 1200/min average prevents sustained abuse.

### 8. OpenRosa Form Submissions Policy

**Original Application Policy**: 60 requests/minute per IP
**Recommended YARP Policy**: Fixed window

```json
{
  "RateLimitingPolicies": {
    "openrosa-submissions": {
      "Type": "FixedWindow",
      "PermitLimit": 60,
      "Window": "00:01:00",
      "QueueLimit": 5,
      "StatusCode": 429
    }
  }
}
```

---

## Request Size Limits

### Input Validation Configuration

Replace the removed `InputValidationMiddleware.cs` with YARP request body size limits:

```json
{
  "RequestLimits": {
    "MaxRequestBodySize": 104857600,
    "MaxRequestHeaderSize": 32768,
    "MaxRequestLineSize": 8192
  }
}
```

**Limits**:
- **Max Request Body**: 100MB (tiles, raster uploads)
- **Max Request Headers**: 32KB (prevents header injection attacks)
- **Max Request Line**: 8KB (prevents URL-based DoS)

### Per-Route Size Limits

Different endpoints have different size requirements:

```json
{
  "Routes": {
    "tile-upload": {
      "Match": {
        "Path": "/api/tiles/{**catch-all}"
      },
      "RequestLimits": {
        "MaxRequestBodySize": 52428800
      }
    },
    "json-api": {
      "Match": {
        "Path": "/api/{**catch-all}"
      },
      "RequestLimits": {
        "MaxRequestBodySize": 10485760
      }
    },
    "openrosa-submission": {
      "Match": {
        "Path": "/openrosa/submission"
      },
      "RequestLimits": {
        "MaxRequestBodySize": 52428800
      }
    }
  }
}
```

**Size Limits by Endpoint Type**:
- Tile uploads: 50MB
- JSON APIs: 10MB
- OpenRosa submissions: 50MB (includes attachments)
- General APIs: 100MB (default)

---

## Configuration Examples

### Complete YARP Configuration File

Here's a complete example YARP configuration with all policies:

```json
{
  "Yarp": {
    "RateLimiting": {
      "EnableEndpointRateLimiting": true,
      "GlobalRules": [
        {
          "Endpoint": "*",
          "Period": "1m",
          "Limit": 1200
        }
      ],
      "Policies": {
        "default": {
          "Type": "SlidingWindow",
          "PermitLimit": 1200,
          "Window": "00:01:00",
          "SegmentsPerWindow": 6
        },
        "ogc-api": {
          "Type": "TokenBucket",
          "TokenLimit": 150,
          "ReplenishmentPeriod": "00:00:01",
          "TokensPerPeriod": 10
        },
        "authentication": {
          "Type": "FixedWindow",
          "PermitLimit": 15,
          "Window": "00:01:00"
        },
        "admin-operations": {
          "Type": "Concurrency",
          "PermitLimit": 5,
          "QueueLimit": 10
        },
        "exports": {
          "Type": "Concurrency",
          "PermitLimit": 10,
          "QueueLimit": 20
        },
        "stac-search": {
          "Type": "SlidingWindow",
          "PermitLimit": 200,
          "Window": "00:01:00",
          "SegmentsPerWindow": 6
        },
        "tile-rendering": {
          "Type": "TokenBucket",
          "TokenLimit": 300,
          "ReplenishmentPeriod": "00:00:01",
          "TokensPerPeriod": 20
        },
        "openrosa-submissions": {
          "Type": "FixedWindow",
          "PermitLimit": 60,
          "Window": "00:01:00"
        }
      }
    },
    "RequestLimits": {
      "MaxRequestBodySize": 104857600,
      "MaxRequestHeaderSize": 32768,
      "MaxRequestLineSize": 8192
    },
    "Routes": {
      "wms-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/wms"
        },
        "RateLimitPolicy": "ogc-api"
      },
      "wfs-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/wfs"
        },
        "RateLimitPolicy": "ogc-api"
      },
      "wcs-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/wcs"
        },
        "RateLimitPolicy": "ogc-api"
      },
      "wmts-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/wmts"
        },
        "RateLimitPolicy": "ogc-api"
      },
      "ogc-features-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/ogc/features/{**catch-all}"
        },
        "RateLimitPolicy": "ogc-api"
      },
      "auth-token-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/api/tokens/generate"
        },
        "RateLimitPolicy": "authentication"
      },
      "admin-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/api/admin/{**catch-all}"
        },
        "RateLimitPolicy": "admin-operations"
      },
      "export-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/api/export/{**catch-all}"
        },
        "RateLimitPolicy": "exports"
      },
      "stac-search-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/stac/search"
        },
        "RateLimitPolicy": "stac-search"
      },
      "vector-tiles-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/tiles/vector/{**catch-all}"
        },
        "RateLimitPolicy": "tile-rendering"
      },
      "raster-tiles-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/tiles/raster/{**catch-all}"
        },
        "RateLimitPolicy": "tile-rendering"
      },
      "openrosa-submission-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/openrosa/submission"
        },
        "RateLimitPolicy": "openrosa-submissions",
        "RequestLimits": {
          "MaxRequestBodySize": 52428800
        }
      },
      "default-route": {
        "ClusterId": "honua-backend",
        "Match": {
          "Path": "/{**catch-all}"
        },
        "RateLimitPolicy": "default"
      }
    },
    "Clusters": {
      "honua-backend": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5000"
          }
        }
      }
    }
  }
}
```

### Environment-Specific Configurations

#### Development Environment
```json
{
  "RateLimiting": {
    "Enabled": false
  }
}
```

#### Staging Environment
```json
{
  "RateLimiting": {
    "Enabled": true,
    "Policies": {
      "default": {
        "PermitLimit": 2400
      }
    }
  }
}
```

#### Production Environment
```json
{
  "RateLimiting": {
    "Enabled": true,
    "Policies": {
      "default": {
        "PermitLimit": 1200
      },
      "authentication": {
        "PermitLimit": 10
      }
    },
    "IpWhitelist": [
      "10.0.0.0/8",
      "172.16.0.0/12"
    ]
  }
}
```

---

## Migration from Application Layer

### Mapping Application Policies to YARP

| Application Policy | Original Limit | YARP Policy | YARP Limit | Notes |
|-------------------|----------------|-------------|------------|-------|
| DefaultPolicy | 1000/min | default | 1200/min | +20% buffer |
| OgcApiPolicy | 500/min | ogc-api | 600/min avg | Token bucket |
| AuthenticationPolicy | 10/min | authentication | 15/min | +50% buffer |
| AdminOperationsPolicy | 100/min | admin-operations | 5 concurrent | Changed to concurrency |
| ExportOperationsPolicy | 10 concurrent | exports | 10 concurrent | Same limit |
| StacSearchPolicy | 200/min | stac-search | 200/min | Same limit |
| TileRenderingPolicy | 1000/min | tile-rendering | 1200/min avg | Token bucket |
| OpenRosaPolicy | 60/min | openrosa-submissions | 60/min | Same limit |

### Testing Migration

**Step 1: Deploy YARP with logging enabled**
```json
{
  "RateLimiting": {
    "Enabled": true,
    "LogOnly": true
  }
}
```

**Step 2: Monitor for false positives**
- Review logs for 429 responses that would have been issued
- Adjust limits if legitimate traffic is being throttled

**Step 3: Enable enforcement**
```json
{
  "RateLimiting": {
    "Enabled": true,
    "LogOnly": false
  }
}
```

**Step 4: Monitor metrics**
- Track 429 response rate
- Monitor backend latency improvements (should see 3-7ms reduction)
- Watch for any increase in client errors

---

## Monitoring and Observability

### Key Metrics to Track

1. **Rate Limit Hits**:
   - `yarp_rate_limit_requests_total{policy="ogc-api",status="allowed"}`
   - `yarp_rate_limit_requests_total{policy="ogc-api",status="rejected"}`

2. **Request Size Rejections**:
   - `yarp_request_size_rejections_total{route="tile-upload"}`

3. **Backend Latency Improvement**:
   - `http_server_request_duration_seconds` (should decrease by 3-7ms)

4. **429 Response Rate**:
   - `http_server_responses_total{status="429"}`

### Logging Configuration

Enable structured logging for rate limit events:

```json
{
  "Logging": {
    "LogLevel": {
      "Yarp.RateLimiting": "Information"
    }
  }
}
```

**Log Entry Example**:
```json
{
  "timestamp": "2025-10-23T10:30:45.123Z",
  "level": "Warning",
  "message": "Rate limit exceeded",
  "policy": "ogc-api",
  "clientIp": "203.0.113.45",
  "route": "/wms",
  "requestsInWindow": 150,
  "limit": 150
}
```

### Alerting Rules

**Critical Alert**: Authentication endpoint abuse
```
rate(yarp_rate_limit_requests_total{policy="authentication",status="rejected"}[5m]) > 10
```

**Warning Alert**: General rate limit trending up
```
rate(yarp_rate_limit_requests_total{status="rejected"}[1h]) > 100
```

---

## Testing and Validation

### Load Testing with Different Policies

#### Test 1: OGC API Burst Pattern
```bash
# Simulate map client requesting 200 tiles rapidly
ab -n 200 -c 50 -H "X-Forwarded-For: 203.0.113.45" \
  http://yarp-proxy/wms?SERVICE=WMS&REQUEST=GetMap&LAYERS=test&...
```

**Expected Result**: 150 requests succeed (burst), then rate limited to 10/sec average

#### Test 2: Authentication Brute Force
```bash
# Simulate authentication attack
for i in {1..20}; do
  curl -X POST http://yarp-proxy/api/tokens/generate \
    -H "X-Forwarded-For: 203.0.113.45" \
    -d "username=test&password=wrong"
done
```

**Expected Result**: First 15 requests proceed, remaining 5 receive 429

#### Test 3: Request Size Limit
```bash
# Test oversized request rejection
dd if=/dev/zero bs=1M count=101 | \
  curl -X POST http://yarp-proxy/api/data \
    -H "Content-Type: application/octet-stream" \
    --data-binary @-
```

**Expected Result**: 413 Payload Too Large (if body exceeds route limit)

### Validation Checklist

- [ ] YARP rate limiting policies configured for all endpoint types
- [ ] Request size limits set per route
- [ ] Monitoring and alerting configured
- [ ] Load testing completed for each policy type
- [ ] False positive rate < 0.1% (legitimate requests throttled)
- [ ] Backend latency reduced by 3-7ms per request
- [ ] Documentation provided to operations team
- [ ] Rollback plan documented

---

## Advanced Configuration

### IP Whitelisting

Exempt internal services and monitoring tools from rate limiting:

```json
{
  "RateLimiting": {
    "IpWhitelist": [
      "10.0.0.0/8",
      "172.16.0.0/12",
      "192.168.0.0/16"
    ]
  }
}
```

### User-Based Rate Limiting

For authenticated endpoints, rate limit by user ID instead of IP:

```json
{
  "RateLimiting": {
    "PartitionKeyExtractor": "Header:X-User-Id"
  }
}
```

**Application Change Required**: Backend must set `X-User-Id` header after authentication.

### Geographic Rate Limiting

Different limits for different geographic regions:

```json
{
  "RateLimiting": {
    "GeographicPolicies": {
      "US": {
        "PermitLimit": 1200
      },
      "EU": {
        "PermitLimit": 1200
      },
      "OTHER": {
        "PermitLimit": 600
      }
    }
  }
}
```

**Infrastructure Requirement**: YARP must have access to GeoIP database.

### Dynamic Rate Limit Adjustment

Adjust limits based on backend health:

```json
{
  "RateLimiting": {
    "DynamicAdjustment": {
      "Enabled": true,
      "HealthCheckEndpoint": "http://backend/health",
      "AdjustmentRules": [
        {
          "Condition": "ResponseTime > 1000ms",
          "Action": "ReduceLimitBy",
          "Percentage": 20
        },
        {
          "Condition": "ErrorRate > 5%",
          "Action": "ReduceLimitBy",
          "Percentage": 50
        }
      ]
    }
  }
}
```

---

## Integration Instructions for AI Agent

### Automated Setup Script

The AI agent should configure YARP with the following steps:

```bash
#!/bin/bash
# YARP rate limiting setup script

# 1. Generate YARP configuration
cat > yarp-config.json <<'EOF'
{
  "Yarp": {
    "RateLimiting": {
      "EnableEndpointRateLimiting": true,
      "Policies": { ... }
    }
  }
}
EOF

# 2. Validate configuration
dotnet run --project YarpProxy -- --validate-config yarp-config.json

# 3. Deploy YARP with new configuration
kubectl apply -f yarp-deployment.yaml

# 4. Wait for rollout
kubectl rollout status deployment/yarp-proxy

# 5. Run smoke tests
./test-rate-limits.sh

# 6. Monitor for 30 minutes
kubectl logs -f deployment/yarp-proxy | grep "rate_limit"
```

### Configuration Template

The AI agent should use this template and fill in environment-specific values:

```json
{
  "Yarp": {
    "RateLimiting": {
      "Enabled": "${RATE_LIMITING_ENABLED:-true}",
      "Policies": {
        "default": {
          "Type": "SlidingWindow",
          "PermitLimit": "${DEFAULT_RATE_LIMIT:-1200}",
          "Window": "00:01:00"
        },
        "ogc-api": {
          "Type": "TokenBucket",
          "TokenLimit": "${OGC_BURST_LIMIT:-150}",
          "ReplenishmentPeriod": "00:00:01",
          "TokensPerPeriod": "${OGC_TOKENS_PER_SECOND:-10}"
        }
      }
    }
  }
}
```

### Health Check Integration

YARP should expose rate limiting metrics for monitoring:

```json
{
  "HealthChecks": {
    "RateLimiting": {
      "Enabled": true,
      "Endpoint": "/health/rate-limiting",
      "Metrics": [
        "requests_per_minute",
        "rejection_rate",
        "policy_utilization"
      ]
    }
  }
}
```

**Health Check Response Example**:
```json
{
  "status": "Healthy",
  "rateLimiting": {
    "enabled": true,
    "policies": {
      "ogc-api": {
        "requestsPerMinute": 450,
        "rejectionRate": 0.02,
        "utilizationPercentage": 75
      }
    }
  }
}
```

---

## Troubleshooting

### Common Issues

**Issue 1: Legitimate traffic being throttled**
- **Symptom**: High 429 rate for specific policy
- **Solution**: Increase `PermitLimit` by 20-30%, monitor for 24 hours
- **Check**: Review logs for user agents, request patterns

**Issue 2: Rate limiting not working**
- **Symptom**: No 429 responses even under load test
- **Solution**: Verify `EnableEndpointRateLimiting: true` and routes have `RateLimitPolicy` set
- **Check**: YARP logs for policy registration

**Issue 3: Backend still slow after migration**
- **Symptom**: Expected 3-7ms latency reduction not observed
- **Solution**: Verify application-layer rate limiting code was fully removed
- **Check**: Application telemetry for rate limiting middleware execution

**Issue 4: IP-based rate limiting not working for proxied requests**
- **Symptom**: All requests appear to come from same IP
- **Solution**: Configure YARP to extract real client IP from `X-Forwarded-For` header
- **Check**: YARP logs showing correct client IPs

---

## References

- [YARP Rate Limiting Documentation](https://microsoft.github.io/reverse-proxy/articles/rate-limiting.html)
- [.NET 9 Rate Limiting APIs](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [HonuaIO Rate Limiting Analysis](./RATE_LIMITING_AND_DOS_ANALYSIS.md)
- [Microsoft ASP.NET Core Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/performance/overview)

---

## Appendix: Removed Application Code Reference

### Original RateLimitingConfiguration.cs Policies

For reference, here are the policies that were removed from the application layer:

```csharp
// DefaultPolicy: 1000/min per IP (sliding window)
// OgcApiPolicy: 500/min per IP (sliding window)
// AuthenticationPolicy: 10/min per IP (fixed window)
// AdminOperationsPolicy: 100/min per IP (sliding window)
// ExportOperationsPolicy: 10 concurrent per IP
// StacSearchPolicy: 200/min per IP (sliding window)
// TileRenderingPolicy: 1000/min per IP (token bucket)
// OpenRosaPolicy: 60/min per IP (fixed window)
```

### Original InputValidationMiddleware.cs Limits

```csharp
// MaxRequestBodySize: 100MB (configurable)
// MaxHeaderSize: 32KB
// MaxQueryStringLength: 8KB
// RequiredContentTypes: application/json, application/xml, multipart/form-data
```

All of these should now be handled in YARP configuration as shown in this guide.

---

**End of Configuration Guide**
