# OpenRosa Distributed Tracing - Quick Reference

## Quick Start

```bash
# Verify what's been done
./verify_openrosa_tracing.sh

# Read detailed implementation guide
cat OpenRosa_Tracing_Implementation.md

# Read complete summary
cat OpenRosa_Tracing_Summary.md
```

## Key Files

| File | Purpose | Status |
|------|---------|--------|
| `OpenRosaActivitySource.cs` | ActivitySource definition | ✅ Done |
| `SubmissionProcessor.cs` | Main processing logic | ⏳ Todo |
| `XFormGenerator.cs` | Form generation | ⏳ Todo |
| `SqliteSubmissionRepository.cs` | Database operations | ⏳ Todo |
| `OpenRosaEndpoints.cs` | HTTP endpoints | ⏳ Todo |

## Common Patterns

### Creating an Activity
```csharp
using var activity = OpenRosaActivitySource.Source.StartActivity("OperationName");
```

### Adding Tags
```csharp
activity?.SetTag("key.name", value);
activity?.SetTag("submission.instance_id", instanceId);
```

### Recording Success
```csharp
activity?.SetTag("operation.success", true);
```

### Recording Errors
```csharp
activity?.SetStatus(ActivityStatusCode.Error, "Error message");
activity?.RecordException(exception);
```

### Using Current Activity (HTTP Endpoints)
```csharp
Activity.Current?.SetTag("openrosa.endpoint", "submission");
Activity.Current?.SetBaggage("instance_id", id);
```

## Tag Naming Conventions

- Use dot notation: `submission.instance_id` not `submission_instance_id`
- Start with domain: `submission.*`, `db.*`, `form.*`, `geometry.*`
- Be consistent: `has_geometry` not `geometry_present`
- Follow OpenTelemetry conventions where applicable

## Activity Hierarchy

```
HTTP Request (automatic)
├─ ProcessSubmission
│  ├─ ParseXFormInstance
│  │  └─ ParseGeometry
│  └─ PublishDirectly OR StoreSubmission
└─ GenerateXForm
```

## Must-Have Tags

### ProcessSubmission
- `submission.instance_id`
- `submission.layer_id`
- `submission.mode`
- `submission.result`

### ParseGeometry
- `geometry.type`
- `geometry.parsed`

### StoreSubmission
- `db.operation`
- `db.table`
- `db.success`

### GenerateXForm
- `form.id`
- `form.version`

## Testing

1. **Add OpenTelemetry to startup:**
   ```csharp
   services.AddOpenTelemetry()
       .WithTracing(t => t.AddSource("Honua.OpenRosa"));
   ```

2. **Submit a test form via ODK Collect**

3. **Check traces in Jaeger/console**

## Checklist

- [ ] Add `using System.Diagnostics;`
- [ ] Create activity with `StartActivity()`
- [ ] Add key tags with `SetTag()`
- [ ] Handle errors with `SetStatus()` and `RecordException()`
- [ ] Dispose activity with `using var`
- [ ] Verify with `./verify_openrosa_tracing.sh`

## Common Mistakes

❌ **Wrong:** `Activity.Current?.StartActivity()` in controller
✅ **Right:** `Activity.Current?.SetTag()` in controller

❌ **Wrong:** Creating activity without `using var`
✅ **Right:** `using var activity = ...`

❌ **Wrong:** String formatting before null check
✅ **Right:** `activity?.SetTag("key", value)` (null-conditional)

❌ **Wrong:** Not recording exceptions
✅ **Right:** `activity?.RecordException(ex)` in catch blocks

## Performance Tips

- Use sampling in production (10-20%)
- Conditional tag setting (`?.`) is efficient
- No overhead when tracing disabled
- Minimal heap allocation per span

## Debugging

**Problem:** Traces not appearing in Jaeger
- Check OpenTelemetry configuration
- Verify ActivitySource name matches
- Check sampling rate (not too low)
- Verify exporter is configured

**Problem:** Missing tags on spans
- Check for null activities
- Verify SetTag() before span ends
- Check for early returns/exceptions

**Problem:** High overhead
- Reduce sampling rate
- Remove expensive tag values
- Check for infinite loops creating spans

## Additional Resources

- Implementation guide: `OpenRosa_Tracing_Implementation.md`
- Complete summary: `OpenRosa_Tracing_Summary.md`
- Verification script: `./verify_openrosa_tracing.sh`
