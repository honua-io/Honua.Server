# Honua Integration and Deployment Summary

**Last Updated**: 2025-10-17
**Version**: 2.0.0
**Status**: Production Ready

## Overview

This document summarizes all improvements completed for Honua 2.0 and provides guidance on integration, deployment, and monitoring setup. For detailed guides, see the referenced documentation below.

---

## Major Improvements Completed

### 1. Pure .NET Raster Readers (Commit 404d22dd)

**Components Added**:
- `LibTiffCogReader.cs` - Pure .NET Cloud Optimized GeoTIFF reader
- `HttpZarrReader.cs` - HTTP-based Zarr array reader with streaming
- `GeoTiffTagParser.cs` - GeoTIFF metadata parsing
- `ZarrDecompressor.cs` - Zarr chunk decompression (Gzip, Zlib)

**Benefits**:
- No GDAL dependency for basic COG/Zarr reading
- HTTP range requests for efficient remote data access
- Fallback to GDAL for advanced features (reprojection, complex compression)

**Configuration**:
```json
{
  "Raster": {
    "PreferNativeReaders": true,
    "FallbackToGdal": true
  }
}
```

**Dependencies**:
- `LibTiff.Net` 2.4.649+
- No new runtime dependencies

### 2. Multi-Cloud Tile Cache Providers

**Components Added**:
- `GcsRasterTileCacheProvider.cs` - Google Cloud Storage cache
- Enhanced `S3RasterTileCacheProvider.cs` with circuit breakers
- Enhanced `AzureBlobRasterTileCacheProvider.cs` with resilience policies
- `ExternalServiceResiliencePolicies.cs` - Polly circuit breakers and retries

**Benefits**:
- Choose any cloud storage provider (S3, Azure, GCS, or FileSystem)
- Automatic retry on transient failures
- Circuit breaker prevents cascade failures
- Seamless provider switching via configuration

**Configuration**:
```json
{
  "RasterTileCache": {
    "Provider": "S3|Azure|GCS|FileSystem",
    "S3": {
      "BucketName": "honua-tiles",
      "Region": "us-west-2"
    }
  }
}
```

**Dependencies**:
- `AWSSDK.S3` 3.7.0+ (for S3)
- `Azure.Storage.Blobs` 12.19.0+ (for Azure)
- `Google.Cloud.Storage.V1` 4.8.0+ (for GCS)
- `Polly` 8.5.0+ (resilience policies)

### 3. Process Framework Integration (Semantic Kernel)

**Components Added**:
8 Processes with 50+ steps:
- `DeploymentProcess.cs` - End-to-end deployment automation
- `UpgradeProcess.cs` - Zero-downtime blue-green upgrades
- `MetadataProcess.cs` - STAC catalog publishing
- `GitOpsProcess.cs` - Git-driven configuration management
- `BenchmarkProcess.cs` - Automated performance testing
- `CertificateRenewalProcess.cs` - Let's Encrypt automation
- `NetworkDiagnosticsProcess.cs` - Network troubleshooting workflows

**Benefits**:
- Stateful, pausable workflows with checkpointing
- Event-driven architecture for loose coupling
- Redis-backed state persistence
- Complete observability with OpenTelemetry
- Dry-run mode for safe testing

**Configuration**:
```json
{
  "ProcessFramework": {
    "Redis": {
      "ConnectionString": "localhost:6379"
    },
    "StateStore": "Redis",
    "EnableCheckpointing": true
  }
}
```

**Dependencies**:
- `Microsoft.SemanticKernel` 1.34.0+
- `StackExchange.Redis` 2.8.16+
- Redis 7.0+ (external service)

### 4. AI Guard System

**Components Added**:
- `LlmInputGuard.cs` - Prompt injection detection, PII redaction
- `LlmOutputGuard.cs` - Hallucination detection, content filtering

**Benefits**:
- Prevents prompt injection attacks
- Redacts PII (emails, phone numbers, SSNs)
- Detects potential hallucinations
- Filters blocked phrases
- All violations logged to security audit log

**Configuration**:
```json
{
  "AIGuard": {
    "Input": {
      "EnablePromptInjectionDetection": true,
      "EnablePIIRedaction": true,
      "BlockedPatterns": ["system:", "ignore previous"]
    },
    "Output": {
      "EnableHallucinationDetection": true,
      "EnableContentFiltering": true
    }
  }
}
```

**Dependencies**: None (uses regex-based detection)

### 5. Multi-Provider LLM Streaming

**Components Enhanced**:
- `OpenAILlmProvider.cs` - Added streaming support
- `AzureOpenAILlmProvider.cs` - Added streaming support
- `LocalAILlmProvider.cs` - Added streaming support
- `AnthropicLlmProvider.cs` - Already had streaming

**Benefits**:
- Real-time token-by-token responses
- Better user experience for long responses
- Compatible with all LLM providers

**Configuration**:
```json
{
  "LLM": {
    "Provider": "OpenAI|AzureOpenAI|Anthropic|LocalAI",
    "OpenAI": {
      "ApiKey": "sk-...",
      "Model": "gpt-4o",
      "EnableStreaming": true
    }
  }
}
```

### 6. Database Resilience Enhancements

**Components Added**:
- `DatabaseRetryPolicy.cs` - Polly exponential backoff
- `PostgresConnectionPoolMetrics.cs` - Connection health monitoring
- Fixed SQL injection vulnerabilities (parameterized queries)
- Added GiST spatial index on STAC items

**Benefits**:
- Automatic retry on transient database failures
- Connection pool health metrics in Prometheus
- Improved query performance (spatial indices)
- Eliminated SQL injection risks

**Configuration**:
```json
{
  "Database": {
    "RetryPolicy": {
      "MaxRetries": 3,
      "DelayMilliseconds": 100,
      "UseExponentialBackoff": true
    },
    "ConnectionPool": {
      "MinSize": 5,
      "MaxSize": 100
    }
  }
}
```

### 7. Full Observability Stack

**Components Added**:
- OpenTelemetry exporters (OTLP, Prometheus, Azure AI)
- Grafana dashboards (Honua Server, Process Framework)
- Prometheus alerting rules
- Loki log aggregation
- `ProcessFrameworkMetrics.cs` - Custom process metrics

**Benefits**:
- Complete metrics, traces, and logs in one place
- Pre-built Grafana dashboards
- Production-ready alerts
- Distributed tracing for debugging

**Local Stack**: `docker/process-testing/` provides full stack

**Configuration**:
```json
{
  "OpenTelemetry": {
    "ServiceName": "honua-server",
    "Metrics": {
      "Enabled": true,
      "Exporters": ["otlp", "prometheus"]
    },
    "Tracing": {
      "Enabled": true,
      "Exporters": ["otlp"]
    },
    "Otlp": {
      "Endpoint": "http://otel-collector:4317"
    }
  }
}
```

---

## Quick Start Guide

### Development Setup

**1. Clone and Build**:
```bash
git clone https://github.com/honua/honua.next.git
cd honua.next
dotnet restore
dotnet build
```

**2. Start Dependencies** (PostgreSQL, Redis, Observability):
```bash
cd docker/process-testing
./scripts/start-testing-stack.sh
```

**3. Run Server**:
```bash
cd src/Honua.Server.Host
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --urls http://localhost:5000
```

**4. Run AI Consultant** (optional):
```bash
cd src/Honua.Cli.AI
export ASPNETCORE_ENVIRONMENT=Testing
export LLM__PROVIDER=OpenAI
export LLM__OPENAI__APIKEY=sk-...
dotnet run
```

**5. Access Services**:
- Honua Server: http://localhost:5000
- Grafana: http://localhost:3000 (admin/admin)
- Prometheus: http://localhost:9090

### Production Deployment (Kubernetes)

**1. Create Namespace and Secrets**:
```bash
kubectl create namespace honua-production
kubectl create secret generic honua-db-credentials \
  --from-literal=connection-string="${DB_CONNECTION_STRING}"
kubectl create secret generic honua-redis-credentials \
  --from-literal=connection-string="redis:6379"
```

**2. Deploy Services**:
```bash
kubectl apply -f deploy/kubernetes/postgres-statefulset.yaml
kubectl apply -f deploy/kubernetes/redis-deployment.yaml
kubectl apply -f deploy/kubernetes/honua-deployment.yaml
kubectl apply -f deploy/kubernetes/honua-hpa.yaml
kubectl apply -f deploy/kubernetes/honua-ingress.yaml
```

**3. Verify Deployment**:
```bash
kubectl get pods -n honua-production
kubectl logs -f deployment/honua-server
curl https://gis.example.com/health
```

### Docker Compose (Simple Deployments)

**1. Create docker-compose.yml**:
```yaml
version: '3.8'
services:
  honua-server:
    image: honua/server:2.0.0
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=${DB_CONNECTION_STRING}
    volumes:
      - ./metadata.json:/app/config/metadata.json:ro

  postgres:
    image: postgis/postgis:16-3.4
    environment:
      - POSTGRES_DB=honua
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data

  redis:
    image: redis:7-alpine
    volumes:
      - redis-data:/data

volumes:
  postgres-data:
  redis-data:
```

**2. Start Stack**:
```bash
docker-compose up -d
docker-compose logs -f honua-server
```

---

## Configuration Reference

### Complete appsettings.json Example

```json
{
  "Honua": {
    "Metadata": {
      "Provider": "json",
      "Path": "metadata.json"
    }
  },

  "ConnectionStrings": {
    "Default": "Host=postgres;Database=honua;Username=honua_app;Password=${DB_PASSWORD}"
  },

  "Raster": {
    "PreferNativeReaders": true,
    "FallbackToGdal": true
  },

  "RasterTileCache": {
    "Provider": "S3",
    "S3": {
      "BucketName": "honua-tiles",
      "Region": "us-west-2"
    }
  },

  "Database": {
    "RetryPolicy": {
      "MaxRetries": 3,
      "DelayMilliseconds": 100,
      "UseExponentialBackoff": true
    }
  },

  "ProcessFramework": {
    "Redis": {
      "ConnectionString": "redis:6379"
    },
    "StateStore": "Redis",
    "EnableCheckpointing": true
  },

  "LLM": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-...",
      "Model": "gpt-4o",
      "EnableStreaming": true
    }
  },

  "AIGuard": {
    "Input": {
      "EnablePromptInjectionDetection": true,
      "EnablePIIRedaction": true
    },
    "Output": {
      "EnableHallucinationDetection": true,
      "EnableContentFiltering": true
    }
  },

  "OpenTelemetry": {
    "ServiceName": "honua-server",
    "Metrics": {
      "Enabled": true,
      "Exporters": ["otlp", "prometheus"]
    },
    "Tracing": {
      "Enabled": true,
      "Exporters": ["otlp"]
    },
    "Otlp": {
      "Endpoint": "http://otel-collector:4317"
    }
  }
}
```

---

## Monitoring and Observability

### Grafana Dashboards

**Pre-built Dashboards** (in `docker/grafana/dashboards/`):
1. **Honua Server Overview** (`honua-detailed.json`)
   - Request rates and latencies (P50, P95, P99)
   - Error rates (4xx, 5xx)
   - Database connection pool stats
   - Tile cache hit/miss rates
   - CPU, memory, GC metrics

2. **Process Framework** (`process-framework-dashboard.json`)
   - Active process instances
   - Step execution rates
   - Step duration histograms
   - Process success/failure rates
   - Redis metrics

### Prometheus Alerts

**Key Alerts** (see `docker/prometheus/alerts/honua-alerts.yml`):
- High error rate (>5% for 5min)
- High latency (P95 >2s for 10min)
- Database pool exhaustion (>90% for 5min)
- Tile cache low hit rate (<70% for 15min)
- Process framework failures (>10% for 10min)

### Log Aggregation

**Loki Setup**:
- Promtail automatically ships logs from Kubernetes pods
- Query with LogQL in Grafana Explore
- Example queries:
  ```logql
  {namespace="honua-production", app="honua-server"}
  {namespace="honua-production"} |= "error"
  {namespace="honua-production"} | json | duration > 1000ms
  ```

---

## Testing

### Unit Tests
```bash
dotnet test --filter "TestCategory=Unit"
```

### Integration Tests (requires storage emulators)
```bash
docker-compose -f tests/Honua.Server.Core.Tests/docker-compose.storage-emulators.yml up -d
dotnet test --filter "TestCategory=Integration"
```

### E2E Tests (Process Framework)
```bash
cd tests/Honua.Cli.AI.E2ETests
dotnet test
```

### Test Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

---

## Migration Paths

### From GDAL-only to Hybrid Raster Processing

1. Update `appsettings.json`:
   ```json
   {
     "Raster": {
       "PreferNativeReaders": true,
       "FallbackToGdal": true
     }
   }
   ```

2. Install LibTiff.Net: `dotnet add package LibTiff.Net --version 2.4.649`

3. Monitor logs for reader selection:
   - "Using native COG reader" → Pure .NET
   - "Falling back to GDAL" → Complex features

4. Gradually disable fallback once confident:
   ```json
   { "FallbackToGdal": false }
   ```

### From FileSystem to Cloud Tile Cache

1. Create cloud storage bucket (S3/Azure/GCS)
2. Configure credentials (IAM role, service principal, etc.)
3. Update `appsettings.json` with provider and bucket name
4. Test connectivity: `honua cache-stats`
5. Optionally migrate existing tiles to cloud storage

### From Agent-only to Process Framework

1. Start Redis: `docker/process-testing/scripts/start-testing-stack.sh`
2. Configure Redis connection in `appsettings.json`
3. Test with dry-run: `honua process deploy --dry-run`
4. Integrate with coordinator for multi-step workflows

---

## Troubleshooting

### Common Issues

**Issue: Pods failing to start**
- Check: Database connection string in secrets
- Check: ConfigMap exists and mounted
- Check: Resource limits not exceeded
- Solution: `kubectl describe pod ${POD_NAME}` for details

**Issue: High latency**
- Check: Database slow query log
- Check: Tile cache hit rate (<70% is low)
- Check: CPU throttling (pods hitting limits)
- Solution: Scale horizontally or add indices

**Issue: No metrics in Prometheus**
- Check: ServiceMonitor created and labels match Service
- Check: Prometheus targets show honua-server as UP
- Check: Metrics endpoint accessible: `curl http://honua:9090/metrics`

**Issue: Traces not in Jaeger**
- Check: OTLP collector logs
- Check: `Tracing.Enabled: true` in config
- Check: Sampling ratio (increase to 1.0 for debugging)

---

## Dependencies Summary

### NuGet Packages (by feature)

**Core**:
- `Microsoft.AspNetCore.App` 9.0+
- `Npgsql` 8.0+
- `NetTopologySuite` 2.5.0+

**Raster Processing**:
- `LibTiff.Net` 2.4.649+
- `MaxRev.Gdal.Core` 3.9.0+
- `SkiaSharp` 2.88.0+

**Multi-Cloud**:
- `AWSSDK.S3` 3.7.0+
- `Azure.Storage.Blobs` 12.19.0+
- `Google.Cloud.Storage.V1` 4.8.0+

**AI & Process Framework**:
- `Microsoft.SemanticKernel` 1.34.0+
- `Magentic` 0.2.0+
- `Azure.AI.OpenAI` 2.0.0+
- `Anthropic.SDK` 0.2.3+

**Resilience & Observability**:
- `Polly` 8.5.0+
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.9.0+
- `OpenTelemetry.Exporter.Prometheus.AspNetCore` 1.9.0+
- `StackExchange.Redis` 2.8.16+

### External Services

**Required**:
- PostgreSQL 14+ with PostGIS 3.3+

**Optional**:
- Redis 7.0+ (for Process Framework)
- Cloud storage (S3/Azure/GCS for tile cache)
- Prometheus 2.45+ (metrics)
- Grafana 10.0+ (dashboards)
- Loki 2.9+ (log aggregation)
- OpenTelemetry Collector 0.100+ (telemetry)

---

## Documentation

**Main Guides**:
- [MONITORING_SETUP.md](MONITORING_SETUP.md) - Complete observability setup
- [process-framework-implementation-guide.md](process-framework-implementation-guide.md) - Process Framework details
- [RASTER_STORAGE_ARCHITECTURE.md](RASTER_STORAGE_ARCHITECTURE.md) - COG & Zarr architecture
- [AI_GUARD_SYSTEM.md](AI_GUARD_SYSTEM.md) - LLM safety guardrails

**Quick References**:
- [quickstart/README.md](quickstart/README.md) - 5-minute setup
- [configuration/README.md](configuration/README.md) - Full config reference
- [api/README.md](api/README.md) - API endpoints
- [rag/05-02-common-issues.md](rag/05-02-common-issues.md) - Troubleshooting

**Operations**:
- [observability/README.md](observability/README.md) - Monitoring overview
- [observability/performance-baselines.md](observability/performance-baselines.md) - SLOs
- [operations/RUNBOOKS.md](operations/RUNBOOKS.md) - Operational procedures

---

## Next Steps

1. ✅ Review this summary
2. ✅ Choose deployment method (Docker Compose, Kubernetes, Cloud)
3. ✅ Configure required services (PostgreSQL, Redis)
4. ✅ Set up observability stack
5. ✅ Deploy Honua Server
6. ✅ Validate health checks
7. ✅ Configure alerts and monitoring
8. ✅ Run performance tests
9. ✅ Document custom workflows

---

## Support

For questions or issues:
- Check [REMAINING_TODOS.md](../REMAINING_TODOS.md) for known issues
- Review [TESTING.md](TESTING.md) for test coverage
- Contact DevOps team for deployment support

---

**Version**: 2.0.0
**Last Updated**: 2025-10-17
**Contributors**: Honua Development Team
