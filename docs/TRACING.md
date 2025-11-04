# Distributed Tracing with OpenTelemetry

Honua Server supports distributed tracing using OpenTelemetry, allowing you to observe request flows, identify performance bottlenecks, and troubleshoot issues across the entire system.

## Overview

Tracing is configured via the `observability:tracing` section in `appsettings.json`. Honua provides Activity Sources for all major subsystems, making it easy to trace requests through different layers of the application.

## Configuration

### Enable Console Tracing (Development)

For local development, use the console exporter to see traces in your terminal:

```json
{
  "observability": {
    "metrics": {
      "enabled": true
    },
    "tracing": {
      "exporter": "console"
    }
  }
}
```

### Enable OTLP Tracing (Production - Jaeger)

For production or advanced debugging, export traces to Jaeger or any OTLP-compatible backend:

```json
{
  "observability": {
    "metrics": {
      "enabled": true
    },
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://localhost:4317"
    }
  }
}
```

### Disable Tracing

```json
{
  "observability": {
    "metrics": {
      "enabled": true
    },
    "tracing": {
      "exporter": "none"
    }
  }
}
```

## Activity Sources

Honua defines the following Activity Sources for distributed tracing:

| Activity Source | Scope |
|----------------|-------|
| `Honua.Server.OgcProtocols` | WMS, WFS, WMTS, WCS, CSW operations |
| `Honua.Server.OData` | OData query processing |
| `Honua.Server.Stac` | STAC catalog operations |
| `Honua.Server.Database` | Database query execution |
| `Honua.Server.RasterTiles` | Raster tile rendering and caching |
| `Honua.Server.Metadata` | Metadata loading and validation |
| `Honua.Server.Authentication` | Authentication and authorization |
| `Honua.Server.Export` | Data export operations (GeoPackage, Shapefile, CSV) |
| `Honua.Server.Import` | Data ingestion and migration |

### Built-in Instrumentation

In addition to Honua-specific traces, OpenTelemetry automatically instruments:

- **ASP.NET Core** - HTTP request/response
- **HTTP Client** - Outbound HTTP requests

Health check endpoints (`/healthz/*`) are automatically excluded from tracing to reduce noise.

## Using Tracing in Code

To add tracing to your code, use the `HonuaTelemetry` activity sources:

```csharp
using System.Diagnostics;
using Honua.Server.Core.Observability;

// Start an activity
using var activity = HonuaTelemetry.Database.StartActivity("ExecuteQuery");
activity?.SetTag("query.type", "SELECT");
activity?.SetTag("table.name", tableName);

try
{
    // Your code here
    var results = await ExecuteQueryAsync(query);

    activity?.SetTag("results.count", results.Count);
}
catch (Exception ex)
{
    activity?.RecordException(ex);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    throw;
}
```

### Example: Tracing OGC WMS GetMap Request

```csharp
using var activity = HonuaTelemetry.OgcProtocols.StartActivity("WMS.GetMap");
activity?.SetTag("service", "WMS");
activity?.SetTag("operation", "GetMap");
activity?.SetTag("layer", layerId);
activity?.SetTag("bbox", bbox.ToString());
activity?.SetTag("width", width);
activity?.SetTag("height", height);

// Nested database query will appear as child span
using var dbActivity = HonuaTelemetry.Database.StartActivity("FetchFeatures");
dbActivity?.SetTag("layer.id", layerId);
var features = await featureRepo.GetFeaturesAsync(layerId, bbox);

// Render operation as another child span
using var renderActivity = HonuaTelemetry.RasterTiles.StartActivity("RenderTile");
renderActivity?.SetTag("renderer", "SkiaSharp");
var image = await renderer.RenderAsync(features, width, height);
```

## Running Jaeger for Local Development

### Using Docker

```bash
docker run -d --name jaeger \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4317:4317 \
  -p 4318:4318 \
  jaegertracing/all-in-one:latest
```

Then configure Honua to export to Jaeger:

```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://localhost:4317"
    }
  }
}
```

Access Jaeger UI at: http://localhost:16686

## Trace Analysis Examples

### Finding Slow Queries

1. Open Jaeger UI
2. Select service "Honua.Server"
3. Filter by operation "WFS.GetFeature"
4. Sort by duration (descending)
5. Click on a slow trace to see breakdown
6. Look for `Honua.Server.Database` spans with high duration

### Identifying Bottlenecks

Trace timeline shows:
- HTTP request ingress
- Authentication check
- Metadata lookup
- Database query execution
- Response serialization
- HTTP response egress

Identify which spans consume the most time.

### Tracking Request Flows

Follow a single trace ID through:
1. WMS GetMap request
2. Feature query from database
3. Raster tile cache lookup
4. Tile rendering
5. Response compression
6. HTTP response

## Best Practices

### Do's ✓

- **Tag important dimensions**: Add tags for layer ID, service type, operation
- **Record exceptions**: Use `activity?.RecordException(ex)` for error tracing
- **Use child spans**: Create nested activities for sub-operations
- **Set meaningful names**: Use clear activity names like "WMS.GetMap" not "Operation"

### Don'ts ✗

- **Don't trace health checks**: Already filtered out automatically
- **Don't add PII to tags**: Avoid user IDs, emails, or sensitive data
- **Don't create too many spans**: Keep spans at meaningful operation boundaries
- **Don't forget to dispose**: Always use `using` statements for activities

## Performance Impact

Tracing has minimal overhead:
- Console exporter: ~1-2% CPU overhead
- OTLP exporter: ~2-5% CPU overhead (depends on network)
- No exporter: ~0.5% overhead (instrumentation still active)

For production:
- Use sampling (configure at Jaeger/OTLP collector level)
- Use OTLP exporter, not console
- Monitor trace export performance

## Integration with Other Tools

### Zipkin

```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://localhost:9411"
    }
  }
}
```

### Honeycomb, Datadog, New Relic

Use their OTLP-compatible endpoints:

```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "https://api.honeycomb.io",
      "otlpHeaders": {
        "x-honeycomb-team": "your-api-key"
      }
    }
  }
}
```

Note: Header configuration requires additional code changes (not yet implemented).

## Troubleshooting

### Traces Not Appearing

1. Check configuration is valid JSON
2. Verify `observability:tracing:exporter` is set to "console" or "otlp"
3. Check Jaeger/collector is running (if using OTLP)
4. Verify endpoint is reachable: `curl http://localhost:4317`

### Console Exporter Shows Nothing

Console exporter outputs to STDOUT. Ensure:
- Application is running in console (not as service)
- Log level allows console output
- Tracing is actually happening (make requests to trigger traces)

### Performance Degradation

- Reduce sampling rate at collector level
- Use OTLP instead of console exporter
- Verify network latency to collector is acceptable
- Consider local OTLP collector with batching

## Next Steps

- [Metrics Documentation](./METRICS.md)
- [Logging Documentation](./LOGGING.md)
- [Deployment Guide](./DEPLOYMENT.md)
