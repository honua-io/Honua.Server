# OpenRosa Distributed Tracing Implementation

This document describes the distributed tracing implementation for the OpenRosa subsystem using System.Diagnostics.ActivitySource API.

## Summary

Distributed tracing has been added to the OpenRosa implementation to enable end-to-end observability of submission flows from HTTP request through processing. The implementation uses `System.Diagnostics.ActivitySource` (available in .NET 6+) and follows OpenTelemetry semantic conventions.

## Files Created

### 1. OpenRosaActivitySource.cs

**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/OpenRosaActivitySource.cs`

**Status:** ✅ Created

```csharp
using System.Diagnostics;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// ActivitySource for distributed tracing of OpenRosa operations.
/// Enables end-to-end observability of submission flows from HTTP request through processing.
/// </summary>
public static class OpenRosaActivitySource
{
    /// <summary>
    /// ActivitySource for Honua.OpenRosa operations.
    /// Use this to create activities for tracing submission processing, form generation, and repository operations.
    /// </summary>
    public static readonly ActivitySource Source = new("Honua.OpenRosa", "1.0.0");
}
```

## Files to Instrument

###  2. SubmissionProcessor.cs

**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/SubmissionProcessor.cs`

**Changes Required:**

#### Add using directive:
```csharp
using System.Diagnostics;
```

#### ProcessAsync Method:
```csharp
public async Task<SubmissionResult> ProcessAsync(SubmissionRequest request, CancellationToken ct = default)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("ProcessSubmission");
    var stopwatch = Stopwatch.StartNew();

    try
    {
        // ... existing code ...

        // After extracting metadata, add tags:
        activity?.SetTag("submission.instance_id", instanceId);
        activity?.SetTag("submission.submitted_by", request.SubmittedBy);
        activity?.SetTag("submission.device_id", request.DeviceId);
        activity?.SetTag("submission.attachment_count", request.Attachments.Count);

        // After validation, add more tags:
        activity?.SetTag("submission.layer_id", layerId);
        activity?.SetTag("submission.service_id", serviceId);
        activity?.SetTag("layer.geometry_type", layer.GeometryType);
        activity?.SetTag("submission.mode", mode);

        // On success:
        activity?.SetTag("submission.result", mode == "direct" ? "direct_published" : "staged_for_review");

        // On validation errors:
        activity?.SetStatus(ActivityStatusCode.Error, "Error message here");
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        // ... existing error handling ...
    }
}
```

**Tags Added:**
- `submission.instance_id` - Unique submission identifier
- `submission.submitted_by` - Username who submitted
- `submission.device_id` - Device ID from ODK
- `submission.attachment_count` - Number of attachments
- `submission.layer_id` - Target layer
- `submission.service_id` - Target service
- `layer.geometry_type` - Geometry type (Point, LineString, Polygon)
- `submission.mode` - Processing mode (direct/staged)
- `submission.result` - Result type (direct_published/staged_for_review)

#### ParseXFormInstance Method:
```csharp
private (Geometry? geometry, IReadOnlyDictionary<string, object?> attributes) ParseXFormInstance(
    XElement root,
    LayerDefinition layer)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("ParseXFormInstance");
    activity?.SetTag("layer.id", layer.Id);
    activity?.SetTag("layer.geometry_field", layer.GeometryField);

    // ... parsing logic ...

    activity?.SetTag("parsing.attribute_count", attributes.Count);
    activity?.SetTag("parsing.has_geometry", geometry is not null);

    return (geometry, attributes);
}
```

**Tags Added:**
- `layer.id` - Layer identifier
- `layer.geometry_field` - Name of geometry field
- `parsing.attribute_count` - Number of attributes parsed
- `parsing.has_geometry` - Whether geometry was found

#### ParseGeometry Method:
```csharp
private Geometry? ParseGeometry(string odkGeometry, string geometryType)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("ParseGeometry");
    activity?.SetTag("geometry.type", geometryType);

    if (string.IsNullOrWhiteSpace(odkGeometry))
    {
        activity?.SetTag("geometry.empty", true);
        return null;
    }

    try
    {
        // ... parsing logic ...

        // On successful parse:
        activity?.SetTag("geometry.parsed", "point"); // or "linestring", "polygon"
        activity?.SetTag("geometry.coordinate_count", coords.Length); // for linestring/polygon
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Geometry parsing failed");
        activity?.RecordException(ex);
    }

    return null;
}
```

**Tags Added:**
- `geometry.type` - Expected geometry type
- `geometry.empty` - Whether input was empty
- `geometry.parsed` - Actual parsed type (point/linestring/polygon)
- `geometry.coordinate_count` - Number of coordinates (for linestring/polygon)

#### PublishDirectlyAsync Method:
```csharp
private async Task<FeatureEditBatchResult> PublishDirectlyAsync(
    string serviceId,
    string layerId,
    Geometry? geometry,
    IReadOnlyDictionary<string, object?> attributes,
    CancellationToken ct)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("PublishDirectly");
    activity?.SetTag("publish.service_id", serviceId);
    activity?.SetTag("publish.layer_id", layerId);
    activity?.SetTag("publish.has_geometry", geometry is not null);
    activity?.SetTag("publish.attribute_count", attributes.Count);

    // ... publishing logic ...

    var result = await _editOrchestrator.ExecuteAsync(batch, ct);

    activity?.SetTag("publish.success", result.Success);
    if (!result.Success)
    {
        activity?.SetStatus(ActivityStatusCode.Error, "Feature publishing failed");
    }

    return result;
}
```

**Tags Added:**
- `publish.service_id` - Target service
- `publish.layer_id` - Target layer
- `publish.has_geometry` - Whether geometry included
- `publish.attribute_count` - Number of attributes
- `publish.success` - Whether publish succeeded

### 3. XFormGenerator.cs

**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/XFormGenerator.cs`

**Changes Required:**

#### Add using directive:
```csharp
using System.Diagnostics;
```

#### Generate Method:
```csharp
public XForm Generate(LayerDefinition layer, string baseUrl)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("GenerateXForm");
    activity?.SetTag("layer.id", layer.Id);
    activity?.SetTag("layer.service_id", layer.ServiceId);
    activity?.SetTag("layer.geometry_type", layer.GeometryType);

    if (layer.OpenRosa is not { Enabled: true })
    {
        activity?.SetStatus(ActivityStatusCode.Error, $"Layer '{layer.Id}' does not have OpenRosa enabled");
        throw new InvalidOperationException($"Layer '{layer.Id}' does not have OpenRosa enabled.");
    }

    var openrosa = layer.OpenRosa;
    var formId = openrosa.FormId ?? $"{layer.ServiceId}_{layer.Id}";
    var formTitle = openrosa.FormTitle ?? layer.Title;
    var version = openrosa.FormVersion;

    activity?.SetTag("form.id", formId);
    activity?.SetTag("form.title", formTitle);
    activity?.SetTag("form.version", version);
    activity?.SetTag("form.field_count", layer.Fields.Count);

    // ... form generation logic ...

    return new XForm { /* ... */ };
}
```

**Tags Added:**
- `layer.id` - Layer identifier
- `layer.service_id` - Service identifier
- `layer.geometry_type` - Geometry type
- `form.id` - Generated form ID
- `form.title` - Form title
- `form.version` - Form version
- `form.field_count` - Number of fields in form

### 4. SqliteSubmissionRepository.cs

**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Core/OpenRosa/SqliteSubmissionRepository.cs`

**Changes Required:**

#### Add using directive:
```csharp
using System.Diagnostics;
```

#### CreateAsync Method:
```csharp
public async Task CreateAsync(Submission submission, CancellationToken ct = default)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("StoreSubmission");
    activity?.SetTag("db.operation", "insert");
    activity?.SetTag("db.table", "openrosa_submissions");
    activity?.SetTag("submission.id", submission.Id);
    activity?.SetTag("submission.layer_id", submission.LayerId);

    await EnsureInitializedAsync(ct);

    using var connection = new SqliteConnection(_connectionString);
    await connection.OpenAsync(ct).ConfigureAwait(false);

    // ... insert logic ...

    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

    activity?.SetTag("db.success", true);
}
```

**Tags Added:**
- `db.operation` - Database operation type
- `db.table` - Table name
- `submission.id` - Submission ID
- `submission.layer_id` - Layer ID
- `db.success` - Operation success status

#### GetAsync Method:
```csharp
public async Task<Submission?> GetAsync(string id, CancellationToken ct = default)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("GetSubmission");
    activity?.SetTag("db.operation", "select");
    activity?.SetTag("db.table", "openrosa_submissions");
    activity?.SetTag("submission.id", id);

    // ... query logic ...

    if (await reader.ReadAsync(ct))
    {
        activity?.SetTag("db.found", true);
        return MapSubmission(reader);
    }

    activity?.SetTag("db.found", false);
    return null;
}
```

#### GetPendingAsync Method:
```csharp
public async Task<IReadOnlyList<Submission>> GetPendingAsync(string? layerId = null, CancellationToken ct = default)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("GetPendingSubmissions");
    activity?.SetTag("db.operation", "select");
    activity?.SetTag("db.table", "openrosa_submissions");
    activity?.SetTag("query.layer_id", layerId);

    // ... query logic ...

    activity?.SetTag("db.result_count", results.Count);

    return results;
}
```

**Tags Added:**
- `query.layer_id` - Optional layer filter
- `db.result_count` - Number of results returned
- `db.found` - Whether record was found

#### UpdateAsync Method:
```csharp
public async Task UpdateAsync(Submission submission, CancellationToken ct = default)
{
    using var activity = OpenRosaActivitySource.Source.StartActivity("UpdateSubmission");
    activity?.SetTag("db.operation", "update");
    activity?.SetTag("db.table", "openrosa_submissions");
    activity?.SetTag("submission.id", submission.Id);
    activity?.SetTag("submission.status", submission.Status.ToString());

    // ... update logic ...

    await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

    activity?.SetTag("db.success", true);
}
```

### 5. OpenRosaEndpoints.cs

**Location:** `/home/mike/projects/HonuaIO/src/Honua.Server.Host/OpenRosa/OpenRosaEndpoints.cs`

**Changes Required:**

#### Add using directive:
```csharp
using System.Diagnostics;
```

**Note:** ASP.NET Core automatically creates activities for HTTP requests. We add custom tags to the existing activity using `Activity.Current`.

#### FormList Endpoint:
```csharp
group.MapGet("/formList", async (
    IMetadataRegistry metadata,
    IXFormGenerator xformGenerator,
    HttpContext context) =>
{
    Activity.Current?.SetTag("openrosa.endpoint", "formList");

    var snapshot = await metadata.GetSnapshotAsync();

    var formCount = snapshot.Services
        .SelectMany(s => s.Layers)
        .Count(l => l.OpenRosa?.Enabled == true);

    Activity.Current?.SetTag("openrosa.form_count", formCount);

    // ... generate XML ...
});
```

#### GetForm Endpoint:
```csharp
group.MapGet("/forms/{formId}", async (
    string formId,
    IMetadataRegistry metadata,
    IXFormGenerator xformGenerator,
    HttpContext context) =>
{
    Activity.Current?.SetTag("openrosa.endpoint", "getForm");
    Activity.Current?.SetTag("openrosa.form_id", formId);
    Activity.Current?.SetBaggage("form_id", formId);

    // ... find and generate form ...

    if (targetLayer is null)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, "Form not found");
        return Results.NotFound(new { error = $"Form '{formId}' not found" });
    }

    // ... return form ...
});
```

#### Submission Endpoint:
```csharp
group.MapPost("/submission", async (
    HttpRequest request,
    ISubmissionProcessor processor,
    IOptions<OpenRosaOptions> openRosaOptions,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    Activity.Current?.SetTag("openrosa.endpoint", "submission");
    Activity.Current?.SetTag("http.request.method", "POST");

    var username = context.User.Identity?.Name ?? "anonymous";
    Activity.Current?.SetTag("user.name", username);

    if (!request.HasFormContentType)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, "Invalid content type");
        return Results.BadRequest(new { error = "Expected multipart/form-data" });
    }

    try
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var totalBytes = form.Files.Sum(f => f.Length);

        Activity.Current?.SetTag("openrosa.file_count", form.Files.Count);
        Activity.Current?.SetTag("openrosa.total_bytes", totalBytes);

        // ... process submission ...

        var result = await processor.ProcessAsync(submissionRequest, cancellationToken);

        Activity.Current?.SetTag("openrosa.submission_result", result.ResultType.ToString());

        if (result.Success)
        {
            Activity.Current?.SetBaggage("instance_id", result.InstanceId);
            return Results.Content(responseXml.ToString(), "text/xml; charset=utf-8", statusCode: 201);
        }
        else
        {
            Activity.Current?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
            return Results.Content(errorXml.ToString(), "text/xml; charset=utf-8", statusCode: 400);
        }
    }
    catch (Exception ex)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Activity.Current?.RecordException(ex);
        // ... error response ...
    }
});
```

**Tags Added:**
- `openrosa.endpoint` - Endpoint name (formList/getForm/submission)
- `openrosa.form_id` - Form identifier
- `openrosa.form_count` - Number of available forms
- `user.name` - Authenticated username
- `http.request.method` - HTTP method
- `openrosa.file_count` - Number of files in submission
- `openrosa.total_bytes` - Total submission size
- `openrosa.submission_result` - Submission result type

**Baggage Added:**
- `form_id` - Propagated to child spans
- `instance_id` - Propagated to child spans

## Example Trace Hierarchy

When a user submits a form, the following trace hierarchy is created:

```
HTTP POST /openrosa/submission
├─ ProcessSubmission
│  ├─ ParseXFormInstance
│  │  └─ ParseGeometry
│  └─ PublishDirectly
│     └─ (FeatureEditOrchestrator activities - from existing instrumentation)
```

When a form is downloaded:

```
HTTP GET /openrosa/forms/{formId}
└─ GenerateXForm
```

When staged submissions are stored:

```
HTTP POST /openrosa/submission (mode=staged)
├─ ProcessSubmission
│  ├─ ParseXFormInstance
│  │  └─ ParseGeometry
│  └─ StoreSubmission
```

## Semantic Conventions

The implementation follows OpenTelemetry semantic conventions:

- **Span Names:** Use verb-object format (ProcessSubmission, ParseGeometry, StoreSubmission)
- **Attributes:**
  - Use `.` separator for namespacing (e.g., `submission.instance_id`, `db.operation`)
  - Follow standard conventions where applicable (e.g., `http.request.method`, `user.name`)
- **Status:** Set to `Error` on failures with descriptive messages
- **Exceptions:** Record using `RecordException()` for full stack traces

## Benefits

1. **End-to-end visibility:** Track submissions from HTTP request through database storage
2. **Performance monitoring:** Identify slow operations (geometry parsing, database writes)
3. **Error diagnosis:** See exactly where failures occur in the processing pipeline
4. **Capacity planning:** Monitor submission volumes, file sizes, and processing times
5. **Integration with observability platforms:** Works with Jaeger, Zipkin, Application Insights, etc.

## Configuration

No additional configuration is required. The `ActivitySource` is automatically discovered by OpenTelemetry SDKs when configured in the application startup:

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Honua.OpenRosa");
        // ... other sources ...
    });
```

## Testing

To verify tracing is working:

1. **Console Exporter (Development):**
   ```csharp
   tracing.AddConsoleExporter();
   ```

2. **Jaeger (Production):**
   ```csharp
   tracing.AddJaegerExporter(options =>
   {
       options.AgentHost = "jaeger";
       options.AgentPort = 6831;
   });
   ```

3. **Check for activities:**
   - Submit a form via ODK Collect
   - Check logs/traces for "ProcessSubmission" spans
   - Verify all tags are present

## Performance Impact

- **Minimal overhead:** Activities are lightweight (< 1μs per span creation)
- **Conditional execution:** Tags are only set if tracing is enabled
- **Sampling:** Production deployments should use sampling (e.g., 10% of requests)
