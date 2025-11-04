# OpenRosa Distributed Tracing - Implementation Summary

## Overview

Distributed tracing has been designed and partially implemented for the OpenRosa subsystem using `System.Diagnostics.ActivitySource` API. This enables end-to-end observability of submission flows from HTTP request through processing.

## Status: Partially Complete

### ✅ Completed

1. **OpenRosaActivitySource.cs** - Core ActivitySource definition created
   - Location: `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/OpenRosaActivitySource.cs`
   - Defines `ActivitySource` with name "Honua.OpenRosa" version "1.0.0"
   - Ready for use by instrumentation code

2. **Implementation Documentation** - Comprehensive guide created
   - Location: `/home/mike/projects/HonuaIO/OpenRosa_Tracing_Implementation.md`
   - Contains detailed code snippets for each file
   - Includes semantic conventions and best practices

3. **Verification Script** - Automated checking tool created
   - Location: `/home/mike/projects/HonuaIO/verify_openrosa_tracing.sh`
   - Checks for presence of tracing code in all files
   - Run with: `./verify_openrosa_tracing.sh`

### ⏳ Pending Implementation

The following files need manual instrumentation (due to active file watchers/formatters):

1. **SubmissionProcessor.cs**
2. **XFormGenerator.cs**
3. **SqliteSubmissionRepository.cs**
4. **OpenRosaEndpoints.cs**

## Activities/Spans Created

The implementation creates the following hierarchical activities:

### 1. ProcessSubmission
- **Parent:** HTTP Request (auto-created by ASP.NET Core)
- **Purpose:** Main submission processing workflow
- **Tags:**
  - `submission.instance_id` - Unique submission ID
  - `submission.submitted_by` - Username
  - `submission.device_id` - ODK device ID
  - `submission.attachment_count` - Number of files
  - `submission.layer_id` - Target layer
  - `submission.service_id` - Target service
  - `layer.geometry_type` - Geometry type (Point/LineString/Polygon)
  - `submission.mode` - Processing mode (direct/staged)
  - `submission.result` - Result type

### 2. ParseXFormInstance
- **Parent:** ProcessSubmission
- **Purpose:** Parse XML form data into attributes and geometry
- **Tags:**
  - `layer.id` - Layer identifier
  - `layer.geometry_field` - Name of geometry field
  - `parsing.attribute_count` - Number of attributes parsed
  - `parsing.has_geometry` - Whether geometry was found

### 3. ParseGeometry
- **Parent:** ParseXFormInstance
- **Purpose:** Parse ODK geometry string into NTS Geometry
- **Tags:**
  - `geometry.type` - Expected geometry type
  - `geometry.empty` - Whether input was empty
  - `geometry.parsed` - Actual parsed type
  - `geometry.coordinate_count` - Number of coordinates

### 4. PublishDirectly
- **Parent:** ProcessSubmission
- **Purpose:** Publish submission directly to production layer
- **Tags:**
  - `publish.service_id` - Target service
  - `publish.layer_id` - Target layer
  - `publish.has_geometry` - Whether geometry included
  - `publish.attribute_count` - Number of attributes
  - `publish.success` - Whether publish succeeded

### 5. GenerateXForm
- **Parent:** HTTP Request
- **Purpose:** Generate XForm XML from layer metadata
- **Tags:**
  - `layer.id` - Layer identifier
  - `layer.service_id` - Service identifier
  - `layer.geometry_type` - Geometry type
  - `form.id` - Generated form ID
  - `form.title` - Form title
  - `form.version` - Form version
  - `form.field_count` - Number of fields

### 6. StoreSubmission
- **Parent:** ProcessSubmission (staged mode only)
- **Purpose:** Store submission in SQLite for review
- **Tags:**
  - `db.operation` - Database operation (insert)
  - `db.table` - Table name
  - `submission.id` - Submission ID
  - `submission.layer_id` - Layer ID
  - `db.success` - Operation success

### 7. GetSubmission
- **Parent:** HTTP Request
- **Purpose:** Retrieve submission by ID
- **Tags:**
  - `db.operation` - Database operation (select)
  - `db.table` - Table name
  - `submission.id` - Submission ID
  - `db.found` - Whether record was found

### 8. GetPendingSubmissions
- **Parent:** HTTP Request
- **Purpose:** Query pending submissions
- **Tags:**
  - `db.operation` - Database operation (select)
  - `db.table` - Table name
  - `query.layer_id` - Optional layer filter
  - `db.result_count` - Number of results

### 9. UpdateSubmission
- **Parent:** HTTP Request
- **Purpose:** Update submission status
- **Tags:**
  - `db.operation` - Database operation (update)
  - `db.table` - Table name
  - `submission.id` - Submission ID
  - `submission.status` - New status
  - `db.success` - Operation success

## Example Trace Hierarchy

### Scenario 1: Direct Submission
```
HTTP POST /openrosa/submission [200ms]
├─ ProcessSubmission [185ms]
│  ├─ ParseXFormInstance [25ms]
│  │  └─ ParseGeometry [5ms]
│  └─ PublishDirectly [150ms]
│     └─ ExecuteFeatureEditBatch [145ms]
│        ├─ ValidateFeatureEdit [10ms]
│        └─ InsertFeature [130ms]
│           └─ PostgreSQL INSERT [125ms]
```

### Scenario 2: Staged Submission
```
HTTP POST /openrosa/submission [80ms]
├─ ProcessSubmission [75ms]
│  ├─ ParseXFormInstance [20ms]
│  │  └─ ParseGeometry [4ms]
│  └─ StoreSubmission [45ms]
│     └─ SQLite INSERT [40ms]
```

### Scenario 3: Form Generation
```
HTTP GET /openrosa/forms/water_points [50ms]
└─ GenerateXForm [45ms]
```

## Tags/Attributes Summary

### Submission Processing Tags
- **Identifiers:** `instance_id`, `layer_id`, `service_id`, `device_id`
- **User context:** `submitted_by`, `user.name`
- **Content:** `attachment_count`, `total_bytes`, `file_count`
- **Processing:** `mode`, `result`, `geometry_type`
- **Outcomes:** `success`, `error`, `status`

### Database Operation Tags
- **Operations:** `db.operation` (insert/select/update/delete)
- **Targets:** `db.table`, `db.collection`
- **Results:** `db.success`, `db.found`, `db.result_count`
- **Queries:** `query.layer_id`, `query.status`

### Form Generation Tags
- **Layer metadata:** `layer.id`, `service_id`, `geometry_type`
- **Form metadata:** `form.id`, `form.title`, `form.version`
- **Content:** `field_count`, `binding_count`

### HTTP Endpoint Tags
- **Endpoints:** `openrosa.endpoint` (formList/getForm/submission)
- **HTTP details:** `http.request.method`, `http.status_code`
- **OpenRosa specifics:** `form_id`, `form_count`

### Baggage (Propagated Context)
- `form_id` - Propagates form identifier to child spans
- `instance_id` - Propagates submission ID to child spans

## Files Summary

### Created Files

| File | Status | Description |
|------|--------|-------------|
| `OpenRosaActivitySource.cs` | ✅ Complete | ActivitySource definition |
| `OpenRosa_Tracing_Implementation.md` | ✅ Complete | Detailed implementation guide |
| `OpenRosa_Tracing_Summary.md` | ✅ Complete | This document |
| `apply_openrosa_tracing.sh` | ✅ Complete | Helper script for backups |
| `verify_openrosa_tracing.sh` | ✅ Complete | Verification script |

### Files Needing Instrumentation

| File | Lines to Add | Activities | Status |
|------|--------------|------------|--------|
| `SubmissionProcessor.cs` | ~30 | 4 | ⏳ Pending |
| `XFormGenerator.cs` | ~10 | 1 | ⏳ Pending |
| `SqliteSubmissionRepository.cs` | ~20 | 4 | ⏳ Pending |
| `OpenRosaEndpoints.cs` | ~15 | 0* | ⏳ Pending |

*OpenRosaEndpoints uses existing HTTP request activities

## Implementation Approach

### Design Decisions

1. **System.Diagnostics.ActivitySource** - Chosen over external libraries
   - Built into .NET 6+
   - No additional dependencies
   - Native OpenTelemetry support
   - Lightweight and performant

2. **Semantic Conventions** - Following OpenTelemetry standards
   - Verb-object span names (ProcessSubmission, ParseGeometry)
   - Dotted attribute names (submission.instance_id, db.operation)
   - Standard HTTP attributes (http.request.method, user.name)

3. **Error Handling** - Comprehensive error tracking
   - `SetStatus(ActivityStatusCode.Error)` on failures
   - `RecordException()` for stack traces
   - Descriptive error messages as status descriptions

4. **Performance** - Minimal overhead
   - Conditional tag setting (`activity?.SetTag`)
   - No string formatting unless tracing enabled
   - Sampling-friendly design

## Benefits

### Observability
- **End-to-end visibility:** Track submissions from HTTP through database
- **Distributed tracing:** See interactions across services
- **Dependency mapping:** Understand call chains automatically

### Performance
- **Bottleneck identification:** Find slow operations (geometry parsing, DB writes)
- **Latency analysis:** P50/P95/P99 metrics per operation
- **Resource usage:** Monitor spans/second, memory allocation

### Debugging
- **Error diagnosis:** Pinpoint exact failure location
- **Context preservation:** See all tags/attributes at error time
- **Stack traces:** Full exception details in traces

### Operations
- **Capacity planning:** Monitor submission volumes and sizes
- **SLA monitoring:** Track processing times against targets
- **Alerting:** Set up alerts on error rates, latencies

## Integration

### OpenTelemetry Configuration

Add to your startup code:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("Honua.OpenRosa")  // ← Add this line
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddNpgsql()
            .AddJaegerExporter(/* ... */);
    });
```

### Jaeger UI Example

When viewing traces in Jaeger, you'll see:

1. **Service:** `honua-server`
2. **Operation:** `POST /openrosa/submission`
3. **Child Spans:**
   - ProcessSubmission
   - ParseXFormInstance
   - ParseGeometry
   - PublishDirectly

Each span includes all tags/attributes for filtering and analysis.

### Sampling Strategy

For production, use head-based sampling:

```csharp
tracing.SetSampler(new TraceIdRatioBasedSampler(0.1)); // 10% sampling
```

Or tail-based sampling for errors:

```csharp
// Sample all errors, 1% of success
tracing.SetSampler(new AlwaysOnSampler()); // Collect all
tracing.AddProcessor(new FilteringSpanProcessor(
    span => span.Status == Status.Error || Random.Shared.NextDouble() < 0.01
));
```

## Next Steps

To complete the implementation:

1. **Review the implementation guide:**
   ```bash
   cat OpenRosa_Tracing_Implementation.md
   ```

2. **Apply instrumentation to each file:**
   - Follow code snippets in the guide
   - Add using statements
   - Create activities with proper tags
   - Handle errors with SetStatus/RecordException

3. **Verify instrumentation:**
   ```bash
   ./verify_openrosa_tracing.sh
   ```

4. **Test end-to-end:**
   - Configure OpenTelemetry exporter
   - Submit a form via ODK Collect
   - View traces in Jaeger/Zipkin
   - Verify all spans appear with correct tags

5. **Monitor and tune:**
   - Adjust sampling rates based on volume
   - Add custom attributes as needed
   - Set up alerting on error rates

## Testing Checklist

- [ ] OpenRosaActivitySource.cs exists and compiles
- [ ] SubmissionProcessor.cs instrumented
- [ ] XFormGenerator.cs instrumented
- [ ] SqliteSubmissionRepository.cs instrumented
- [ ] OpenRosaEndpoints.cs instrumented
- [ ] Verification script passes (no errors)
- [ ] Code compiles without warnings
- [ ] OpenTelemetry configured in startup
- [ ] Test submission via ODK Collect
- [ ] Verify traces appear in Jaeger/console
- [ ] All tags present on spans
- [ ] Error traces include exceptions
- [ ] Performance overhead < 1% (load test)

## References

- **OpenTelemetry Semantic Conventions:** https://opentelemetry.io/docs/specs/semconv/
- **System.Diagnostics.Activity:** https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity
- **OpenRosa Specification:** https://docs.getodk.org/openrosa/
- **Honua OpenRosa Implementation:** `/src/Honua.Server.Core/OpenRosa/`

## Support

For questions or issues:

1. Review `OpenRosa_Tracing_Implementation.md` for detailed code examples
2. Run `./verify_openrosa_tracing.sh` to check instrumentation status
3. Check OpenTelemetry logs for export errors
4. Verify ActivitySource is registered in startup configuration
