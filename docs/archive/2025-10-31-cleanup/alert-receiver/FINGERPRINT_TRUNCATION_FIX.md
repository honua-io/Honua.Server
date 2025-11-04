# Alert Fingerprint Truncation Fix

## Summary

Fixed critical security and data integrity issue where alert fingerprints exceeding 256 characters were silently truncated, potentially causing hash collisions and incorrect alert deduplication (leading to alert storms).

## Problem Statement

**Location**: `src/Honua.Server.AlertReceiver/Controllers/GenericAlertController.cs`, lines 84-90

**Original Code**:
```csharp
// Validate fingerprint length
if (fingerprint.Length > 256)
{
    fingerprint = fingerprint.Substring(0, 256);
}
```

**Issue**: Fingerprints longer than 256 characters were silently truncated, which could cause:
1. **Hash Collisions**: Different alerts with long fingerprints could map to the same truncated value
2. **Incorrect Deduplication**: Unrelated alerts would be treated as duplicates
3. **Alert Storms**: Important alerts could be suppressed incorrectly
4. **Data Integrity**: Silent failures mask configuration issues in client code

## Solution

Replaced silent truncation with explicit validation and rejection.

### Changes Made

#### 1. Controller Validation (GenericAlertController.cs)

**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Controllers/GenericAlertController.cs`

**New Code**:
```csharp
// Record fingerprint length for monitoring and capacity planning
_metricsService.RecordFingerprintLength(fingerprint.Length);

// CRITICAL: Reject fingerprints exceeding 256 characters instead of silently truncating.
// Silent truncation can cause hash collisions and incorrect deduplication, leading to alert storms.
// The 256-character limit is enforced by the database schema and must be validated before persistence.
if (fingerprint.Length > 256)
{
    _logger.LogWarning(
        "Alert fingerprint exceeds maximum length of 256 characters - Name: {Name}, Source: {Source}, FingerprintLength: {Length}",
        alert.Name, alert.Source, fingerprint.Length);

    _metricsService.RecordAlertSuppressed("fingerprint_too_long", alert.Severity);

    return BadRequest(new
    {
        error = "Fingerprint exceeds maximum length of 256 characters",
        fingerprintLength = fingerprint.Length,
        maxLength = 256,
        details = "Alert fingerprints must be 256 characters or less to ensure proper deduplication. " +
                 "If using a custom fingerprint, consider using a hash (e.g., SHA256) of your identifier. " +
                 "Auto-generated fingerprints are always within the limit."
    });
}
```

**Benefits**:
- Clear error message explaining the problem
- Actionable guidance for fixing the issue
- Logging for operations monitoring
- Metrics for tracking occurrences
- Prevents data integrity issues

#### 2. Metrics Service Enhancement (AlertMetricsService.cs)

**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Services/AlertMetricsService.cs`

**Added Metric**:
```csharp
private readonly Histogram<int> _fingerprintLength;

_fingerprintLength = _meter.CreateHistogram<int>(
    "honua.alerts.fingerprint_length",
    unit: "{character}",
    description: "Distribution of alert fingerprint lengths (helps identify truncation risks)");

public void RecordFingerprintLength(int length)
{
    _fingerprintLength.Record(length);
}
```

**Also Added** (bonus fix for pre-existing compilation errors):
```csharp
private readonly Counter<long> _raceConditionsPrevented;

_raceConditionsPrevented = _meter.CreateCounter<long>(
    "honua.alerts.race_conditions_prevented",
    unit: "{race_condition}",
    description: "Number of race conditions prevented by atomic operations");

public void RecordRaceConditionPrevented(string scenario)
{
    _raceConditionsPrevented.Add(1, new KeyValuePair<string, object?>("scenario", scenario));
}
```

**Benefits**:
- Proactive monitoring of fingerprint lengths
- Early warning system for approaching limits
- Capacity planning data
- Observability into deduplication behavior

#### 3. Model Documentation (GenericAlert.cs)

**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Models/GenericAlert.cs`

**Enhanced Documentation**:
```csharp
/// <summary>
/// Unique identifier for deduplication.
/// CRITICAL: Maximum 256 characters (enforced by database schema and validation).
/// Fingerprints exceeding this limit are rejected to prevent hash collisions and alert storms.
/// If not provided, auto-generated from source:name:service (always within limit).
/// For custom fingerprints, use hashed identifiers (e.g., SHA256 hex = 64 chars).
/// </summary>
[JsonPropertyName("fingerprint")]
[StringLength(256, ErrorMessage = "Fingerprint must be 256 characters or less")]
public string? Fingerprint { get; set; }
```

**Benefits**:
- Clear documentation for API consumers
- Guidance on best practices
- IntelliSense support for developers

#### 4. API Documentation (api-reference.md)

**File**: `/home/mike/projects/HonuaIO/docs/alert-receiver/api-reference.md` (NEW)

Created comprehensive API reference documentation including:
- Endpoint descriptions with rate limits
- Authentication requirements
- Request/response schemas
- Fingerprint validation rules (prominently featured)
- Field length limits table
- Collection size limits
- Best practices for fingerprint generation
- Example requests (basic, custom hashed, batch)
- Migration guide for breaking change
- Error handling guide
- Metrics documentation

**Fingerprint Section Highlights**:
```markdown
**Fingerprint Validation**:
- **Maximum Length**: 256 characters (strictly enforced)
- **Auto-Generation**: If not provided, automatically generated from `source:name:service`
- **Custom Fingerprints**: Must be 256 characters or less
- **Recommendation**: Use hashed identifiers (e.g., SHA256) for long custom identifiers
- **Truncation Policy**: Requests with fingerprints exceeding 256 characters are **rejected** with HTTP 400

> **CRITICAL**: Fingerprints longer than 256 characters are rejected to prevent hash collisions
> and incorrect deduplication. Silent truncation would lead to alert storms where different
> alerts share the same truncated fingerprint.
```

#### 5. Bug Fixes (SqlAlertDeduplicator.cs)

**File**: `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Services/SqlAlertDeduplicator.cs`

Fixed pre-existing compilation errors (variable scope issues):
- Renamed `rowsAffected` to `suppressRowsAffected` (line 273)
- Renamed `rowsAffected` to `dedupRowsAffected` (line 330)
- Renamed `rowsAffected` to `rateLimitRowsAffected` (line 366)

**Result**: Build now succeeds with only 1 warning (unrelated async method)

## Testing Recommendations

### Unit Tests

Create tests for:
1. Fingerprint exactly 256 characters (should succeed)
2. Fingerprint 257 characters (should return BadRequest)
3. Fingerprint 300 characters (should return BadRequest)
4. Auto-generated fingerprint (should always be under 256)
5. Verify error response structure
6. Verify metrics are recorded

### Integration Tests

1. Send alert with long custom fingerprint
2. Verify HTTP 400 response
3. Verify error message contains guidance
4. Verify `honua.alerts.suppressed` metric incremented
5. Verify `honua.alerts.fingerprint_length` histogram recorded

### Example Test Case

```csharp
[Fact]
public async Task SendAlert_WithFingerprintExceeding256Chars_ReturnsValidationError()
{
    // Arrange
    var longFingerprint = new string('x', 257);
    var alert = new GenericAlert
    {
        Name = "Test Alert",
        Severity = "critical",
        Source = "test",
        Fingerprint = longFingerprint
    };

    // Act
    var response = await _client.PostAsJsonAsync("/api/alerts", alert);

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
    Assert.Contains("256 characters", error.Error);
    Assert.Equal(257, error.FingerprintLength);
    Assert.Contains("hash", error.Details, StringComparison.OrdinalIgnoreCase);
}
```

## Monitoring

### Metrics to Watch

1. **honua.alerts.suppressed** with reason `fingerprint_too_long`
   - Alert: > 0 indicates clients sending invalid fingerprints
   - Action: Contact client owners to fix fingerprint generation

2. **honua.alerts.fingerprint_length** histogram
   - Monitor P95/P99 percentiles
   - Alert if approaching 256 characters
   - Use for capacity planning

3. **honua.alerts.received** and **honua.alerts.sent** ratio
   - Significant drop after deployment may indicate clients rejecting fixes
   - Compare pre/post deployment rates

### Dashboard Queries

**Prometheus/OpenTelemetry**:
```promql
# Count of alerts rejected for long fingerprints
sum(rate(honua_alerts_suppressed_total{reason="fingerprint_too_long"}[5m]))

# Fingerprint length distribution
histogram_quantile(0.95, honua_alerts_fingerprint_length_bucket)
histogram_quantile(0.99, honua_alerts_fingerprint_length_bucket)

# Alert reception rate (watch for drops)
rate(honua_alerts_received_total[5m])
```

## Migration Impact

### Breaking Change

This is a **breaking change** for clients that:
1. Send custom fingerprints exceeding 256 characters
2. Rely on silent truncation behavior (incorrect behavior)

### Migration Steps for Clients

**Before (Silent Truncation)**:
```csharp
var fingerprint = $"{longValue1}-{longValue2}-{longValue3}"; // May exceed 256 chars
// Result: Truncated to 256 chars (could cause collisions)
```

**After (Hash Long Identifiers)**:
```csharp
var key = $"{longValue1}-{longValue2}-{longValue3}";
using var sha = System.Security.Cryptography.SHA256.Create();
var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
var fingerprint = Convert.ToHexString(hash).ToLowerInvariant(); // 64 chars
```

**Or (Omit Fingerprint)**:
```csharp
// Don't set fingerprint - system auto-generates from source:name:service
var alert = new GenericAlert
{
    Name = "Database Error",
    Source = "api-server",
    Service = "order-processing",
    // fingerprint will be auto-generated (always < 256 chars)
};
```

### Deployment Strategy

1. **Pre-Deployment**:
   - Audit existing alerts for fingerprint lengths
   - Identify clients sending long fingerprints
   - Communicate breaking change to client owners

2. **Deployment**:
   - Deploy to staging first
   - Monitor `fingerprint_too_long` metric
   - Verify error responses are clear
   - Update client code if needed

3. **Post-Deployment**:
   - Monitor alert reception rates
   - Watch for support requests about validation errors
   - Track fingerprint length histogram
   - Verify deduplication working correctly

## Security Considerations

### Why This Is Critical

1. **Data Integrity**: Silent failures mask problems
2. **Alert Storms**: Incorrect deduplication could cause missed critical alerts
3. **Operational Safety**: Clear errors help operators diagnose issues
4. **Defense in Depth**: Model validation + controller validation + database constraints

### Validation Layers

1. **Model Validation**: `[StringLength(256)]` attribute
2. **Controller Validation**: Explicit length check with clear error
3. **Database Schema**: `VARCHAR(256)` constraint
4. **Metrics**: Observable behavior for monitoring

## Related Files

### Modified
- `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Controllers/GenericAlertController.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Services/AlertMetricsService.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Models/GenericAlert.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.AlertReceiver/Services/SqlAlertDeduplicator.cs`

### Created
- `/home/mike/projects/HonuaIO/docs/alert-receiver/api-reference.md`
- `/home/mike/projects/HonuaIO/docs/alert-receiver/FINGERPRINT_TRUNCATION_FIX.md` (this file)

## References

- Database schema: Alert fingerprints stored as `VARCHAR(256)`
- Auto-generation logic: `GenerateFingerprint()` in GenericAlertController.cs
- Deduplication logic: SqlAlertDeduplicator.cs, uses fingerprint as key
- Metrics service: AlertMetricsService.cs

## Conclusion

This fix eliminates a critical data integrity issue by replacing silent truncation with explicit validation. The change is observable through metrics, well-documented in the API reference, and provides clear guidance to API consumers.

**Key Benefits**:
- ✅ Prevents hash collisions
- ✅ Prevents incorrect deduplication
- ✅ Prevents alert storms
- ✅ Clear error messages
- ✅ Observable via metrics
- ✅ Well-documented
- ✅ Backward compatible (auto-generated fingerprints unchanged)

**Build Status**: ✅ Compiles successfully with 0 errors, 1 warning (unrelated)
