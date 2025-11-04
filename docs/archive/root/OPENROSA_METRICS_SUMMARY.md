# OpenRosa Metrics Implementation Summary

## Overview
Comprehensive metrics and counters have been added to the OpenRosa implementation using the System.Diagnostics.Metrics API (OpenTelemetry-compatible).

## 1. Metrics Defined

### File: `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/OpenRosaMetrics.cs`

#### Submission Counters
- **submissions_received** - Total submissions received
  - Tags: layer_id, service_id, submission_mode, device_id
  
- **submissions_processed** - Total submissions processed successfully  
  - Tags: layer_id, service_id, submission_mode, result_type

- **submissions_failed** - Total submissions that failed
  - Tags: layer_id, service_id, submission_mode, error_type

#### Performance Histograms
- **submission_processing_time** (ms) - Time to process a submission
  - Tags: layer_id, service_id, submission_mode, result_type

- **attachment_size** (bytes) - Individual attachment file sizes
  - Tags: layer_id, service_id, content_type

- **total_submission_size** (bytes) - Total size of all attachments
  - Tags: layer_id, service_id, attachment_count

#### XForm Generation Metrics
- **xforms_generated** - Total XForms generated
  - Tags: layer_id, service_id, form_id

- **xform_generation_time** (ms) - Time to generate an XForm
  - Tags: layer_id, service_id, form_id

#### Endpoint Metrics
- **formlist_requests** - FormList endpoint requests
  - Tags: status_code, form_count

- **form_download_requests** - Form download requests
  - Tags: form_id, layer_id, service_id, status_code

#### Geometry Metrics
- **geometry_type_distribution** - Distribution of geometry types
  - Tags: layer_id, service_id, geometry_type

- **missing_geometries** - Submissions with missing geometry
  - Tags: layer_id, service_id

#### Field Processing
- **field_parsing_errors** - Field parsing errors
  - Tags: layer_id, field_name, data_type

#### Attachment Metrics
- **attachments_processed** - Total attachments processed
  - Tags: layer_id, service_id, content_type

#### Active Gauges
- **active_submissions** - Current number of submissions being processed
  - Type: UpDownCounter

## 2. Files Instrumented

### SubmissionProcessor.cs (To Be Completed)
**Location**: `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/SubmissionProcessor.cs`

**Instrumentation needed**:
- Track `active_submissions` gauge (increment at start, decrement in finally block)
- Record `submissions_received` when submission starts processing
- Record `submissions_processed` or `submissions_failed` based on outcome
- Measure `submission_processing_time` with Stopwatch
- Record `attachment_size` for each attachment
- Record `total_submission_size` for all attachments
- Track `geometry_type_distribution` or `missing_geometries`
- All metrics tagged with layer_id, service_id, submission_mode, etc.

### XFormGenerator.cs ✅ COMPLETED
**Location**: `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/XFormGenerator.cs`

**Instrumentation added**:
- Added `using System.Diagnostics;`
- Added Stopwatch to measure generation time
- Record `xforms_generated` counter
- Record `xform_generation_time` histogram
- Tags: layer_id, service_id, form_id

### OpenRosaEndpoints.cs ✅ COMPLETED  
**Location**: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/OpenRosa/OpenRosaEndpoints.cs`

**Instrumentation added**:
- FormList endpoint: Record `formlist_requests` with form_count
- Form download endpoint: Record `form_download_requests` with form_id, layer_id, service_id

## 3. Example Metric Output Format

### OpenTelemetry OTLP Format (JSON)
```json
{
  "resourceMetrics": [{
    "resource": {
      "attributes": [
        {"key": "service.name", "value": {"stringValue": "Honua.Server"}}
      ]
    },
    "scopeMetrics": [{
      "scope": {
        "name": "Honua.OpenRosa",
        "version": "1.0.0"
      },
      "metrics": [
        {
          "name": "submissions_processed",
          "unit": "submissions",
          "description": "Total submissions processed successfully",
          "sum": {
            "dataPoints": [{
              "attributes": [
                {"key": "layer_id", "value": {"stringValue": "parcels"}},
                {"key": "service_id", "value": {"stringValue": "cadastre"}},
                {"key": "submission_mode", "value": {"stringValue": "direct"}},
                {"key": "result_type", "value": {"stringValue": "direct_published"}}
              ],
              "asInt": "42",
              "timeUnixNano": "1698012345000000000"
            }]
          }
        },
        {
          "name": "submission_processing_time",
          "unit": "ms",
          "description": "Submission processing time in milliseconds",
          "histogram": {
            "dataPoints": [{
              "attributes": [
                {"key": "layer_id", "value": {"stringValue": "parcels"}},
                {"key": "submission_mode", "value": {"stringValue": "direct"}}
              ],
              "count": "42",
              "sum": 12543.5,
              "bucketCounts": ["5", "15", "12", "8", "2"],
              "explicitBounds": [10, 50, 100, 500, 1000]
            }]
          }
        }
      ]
    }]
  }]
}
```

### Prometheus Format
```prometheus
# HELP submissions_received Total submissions received
# TYPE submissions_received counter
submissions_received{layer_id="parcels",service_id="cadastre",device_id="odk123"} 156

# HELP submissions_processed Total submissions processed successfully
# TYPE submissions_processed counter
submissions_processed{layer_id="parcels",service_id="cadastre",submission_mode="direct",result_type="direct_published"} 148

# HELP submissions_failed Total submissions failed
# TYPE submissions_failed counter
submissions_failed{layer_id="parcels",service_id="cadastre",error_type="layer_not_found"} 8

# HELP submission_processing_time Submission processing time in milliseconds
# TYPE submission_processing_time histogram
submission_processing_time_bucket{layer_id="parcels",submission_mode="direct",le="10"} 5
submission_processing_time_bucket{layer_id="parcels",submission_mode="direct",le="50"} 20
submission_processing_time_bucket{layer_id="parcels",submission_mode="direct",le="100"} 32
submission_processing_time_bucket{layer_id="parcels",submission_mode="direct",le="500"} 40
submission_processing_time_bucket{layer_id="parcels",submission_mode="direct",le="+Inf"} 42
submission_processing_time_sum{layer_id="parcels",submission_mode="direct"} 12543.5
submission_processing_time_count{layer_id="parcels",submission_mode="direct"} 42

# HELP xforms_generated XForms generated from layer metadata
# TYPE xforms_generated counter
xforms_generated{layer_id="parcels",service_id="cadastre",form_id="cadastre_parcels"} 23

# HELP attachment_size Attachment file sizes in bytes
# TYPE attachment_size histogram
attachment_size_bucket{layer_id="parcels",content_type="image/jpeg",le="1024"} 5
attachment_size_bucket{layer_id="parcels",content_type="image/jpeg",le="10240"} 15
attachment_size_bucket{layer_id="parcels",content_type="image/jpeg",le="102400"} 42
attachment_size_bucket{layer_id="parcels",content_type="image/jpeg",le="+Inf"} 45
attachment_size_sum{layer_id="parcels",content_type="image/jpeg"} 2458624
attachment_size_count{layer_id="parcels",content_type="image/jpeg"} 45

# HELP active_submissions Current number of submissions being processed
# TYPE active_submissions gauge
active_submissions 3
```

## 4. Integration with OpenTelemetry

### Required Configuration (appsettings.json)
```json
{
  "OpenTelemetry": {
    "Metrics": {
      "Exporters": {
        "Prometheus": {
          "Enabled": true,
          "ScrapeEndpointPath": "/metrics"
        },
        "OTLP": {
          "Enabled": false,
          "Endpoint": "http://localhost:4317"
        }
      },
      "Meters": [
        "Honua.OpenRosa"
      ]
    }
  }
}
```

### Startup Configuration (Program.cs or Startup.cs)
```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("Honua.OpenRosa")  // Add our custom meter
            .AddPrometheusExporter()     // or .AddOtlpExporter()
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();
    });

// If using Prometheus exporter
app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

## 5. Usage Examples

### Query Examples (PromQL for Prometheus)

**Submission rate (submissions/second)**:
```promql
rate(submissions_received[5m])
```

**Success rate percentage**:
```promql
100 * sum(rate(submissions_processed[5m])) / sum(rate(submissions_received[5m]))
```

**95th percentile processing time**:
```promql
histogram_quantile(0.95, rate(submission_processing_time_bucket[5m]))
```

**Average attachment size by content type**:
```promql
sum(rate(attachment_size_sum[5m])) by (content_type) / sum(rate(attachment_size_count[5m])) by (content_type)
```

**Most common error types**:
```promql
topk(5, sum(rate(submissions_failed[5m])) by (error_type))
```

## 6. Benefits

1. **Performance Monitoring**: Track submission processing times to identify bottlenecks
2. **Resource Usage**: Monitor attachment sizes and submission volumes
3. **Error Tracking**: Identify common failure modes and error patterns
4. **Capacity Planning**: Use active_submissions gauge to understand concurrent load
5. **SLA Compliance**: Track success rates and processing times against SLAs
6. **Trend Analysis**: Historical data on geometry types, submission modes, etc.
7. **Alerting**: Set up alerts on high error rates or slow processing times
8. **OpenTelemetry Compatible**: Works with Prometheus, Grafana, Datadog, New Relic, etc.

## 7. Next Steps

1. ✅ Create OpenRosaMetrics.cs with all metric definitions
2. ⚠️  Complete instrumentation of SubmissionProcessor.cs
3. ✅ Instrument XFormGenerator.cs  
4. ✅ Instrument OpenRosaEndpoints.cs
5. Configure OpenTelemetry in startup configuration
6. Set up Prometheus/Grafana dashboards for visualization
7. Configure alerting rules for critical metrics
8. Document metrics in API documentation
