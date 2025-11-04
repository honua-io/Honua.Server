# Alert Receiver API Reference

## Overview

The Alert Receiver API provides endpoints for ingesting alerts from various sources (application code, health checks, monitoring tools, etc.) with built-in deduplication, silencing, and routing capabilities.

## Base URL

```
/api/alerts
```

## Authentication

- **JWT Bearer Token**: Required for `/api/alerts` and `/api/alerts/batch` endpoints
- **Webhook Signature**: Required for `/api/alerts/webhook` endpoint (validated by middleware)

## Endpoints

### POST /api/alerts

Send a single alert (authenticated via JWT).

**Rate Limit**: `alert-ingestion` policy

**Request Body**:

```json
{
  "name": "string (required, 1-256 characters)",
  "severity": "string (required, max 50 characters)",
  "status": "string (required, max 50 characters, default: 'firing')",
  "summary": "string (optional, max 500 characters)",
  "description": "string (optional, max 4000 characters)",
  "source": "string (required, max 256 characters)",
  "service": "string (optional, max 256 characters)",
  "environment": "string (optional, max 100 characters)",
  "labels": {
    "key": "value (max 50 labels, keys max 256 chars, values max 1000 chars)"
  },
  "timestamp": "ISO8601 datetime (optional, defaults to now)",
  "fingerprint": "string (optional, max 256 characters)",
  "context": {
    "key": "value (max 100 entries)"
  }
}
```

**Severity Values**: `critical`, `high`, `medium`, `low`, `info`, `crit`, `fatal`, `error`, `err`, `warning`, `warn`, `information`

**Status Values**: `firing`, `resolved`

**Fingerprint Validation**:
- **Maximum Length**: 256 characters (strictly enforced)
- **Auto-Generation**: If not provided, automatically generated from `source:name:service`
- **Custom Fingerprints**: Must be 256 characters or less
- **Recommendation**: Use hashed identifiers (e.g., SHA256) for long custom identifiers
- **Truncation Policy**: Requests with fingerprints exceeding 256 characters are **rejected** with HTTP 400

> **CRITICAL**: Fingerprints longer than 256 characters are rejected to prevent hash collisions and incorrect deduplication. Silent truncation would lead to alert storms where different alerts share the same truncated fingerprint.

**Response**: `200 OK`

```json
{
  "status": "sent|deduplicated|silenced|acknowledged|failed",
  "alertName": "string",
  "fingerprint": "string",
  "publishedTo": ["provider1", "provider2"],
  "error": "string (only if status=failed)"
}
```

**Error Response**: `400 Bad Request`

```json
{
  "error": "Fingerprint exceeds maximum length of 256 characters",
  "fingerprintLength": 300,
  "maxLength": 256,
  "details": "Alert fingerprints must be 256 characters or less to ensure proper deduplication. If using a custom fingerprint, consider using a hash (e.g., SHA256) of your identifier. Auto-generated fingerprints are always within the limit."
}
```

**Other Error Responses**:
- `400 Bad Request`: Validation errors (labels > 50, context > 100, label key/value length exceeded)
- `503 Service Unavailable`: Alert persistence unavailable
- `500 Internal Server Error`: Processing failure

---

### POST /api/alerts/webhook

Send a single alert via webhook (signature-validated, no JWT required).

**Rate Limit**: `webhook-ingestion` policy

**Authentication**: Webhook signature validation via `X-Hub-Signature-256` header (handled by middleware)

**Request/Response**: Same as `POST /api/alerts`

---

### POST /api/alerts/batch

Send multiple alerts in a single request (authenticated via JWT).

**Rate Limit**: `alert-batch-ingestion` policy

**Request Body**:

```json
{
  "alerts": [
    {
      // Same structure as single alert
    }
  ]
}
```

**Constraints**:
- Minimum: 1 alert per batch
- Maximum: 100 alerts per batch
- All alerts must pass validation (labels, context, fingerprint length)

**Response**: `200 OK`

```json
{
  "status": "sent|partial_success|failed",
  "alertCount": 100,
  "publishedGroups": 3,
  "totalGroups": 3,
  "errors": ["severity: error message"] // only if failures occurred
}
```

---

### GET /api/alerts/health

Health check endpoint (unauthenticated).

**Response**: `200 OK`

```json
{
  "status": "healthy",
  "service": "generic-alerts"
}
```

---

## Validation Rules

### Field Length Limits

| Field | Min | Max | Notes |
|-------|-----|-----|-------|
| name | 1 | 256 | Required |
| severity | 1 | 50 | Required |
| status | 1 | 50 | Required, default: "firing" |
| summary | 0 | 500 | Optional |
| description | 0 | 4000 | Optional |
| source | 1 | 256 | Required |
| service | 0 | 256 | Optional |
| environment | 0 | 100 | Optional |
| fingerprint | 0 | 256 | **Strictly enforced** - requests rejected if exceeded |
| label key | 1 | 256 | Per label |
| label value | 1 | 1000 | Per label |

### Collection Size Limits

| Collection | Max Size |
|------------|----------|
| labels | 50 entries |
| context | 100 entries |
| batch alerts | 100 alerts |

### Fingerprint Best Practices

1. **Auto-Generated (Recommended)**: Omit `fingerprint` field - system generates SHA256-based hash
2. **Custom Short IDs**: Use identifiers up to 256 characters directly
3. **Custom Long IDs**: Hash your identifier:
   ```csharp
   using var sha = System.Security.Cryptography.SHA256.Create();
   var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(yourLongId));
   var fingerprint = Convert.ToHexString(hash).ToLowerInvariant(); // 64 chars
   ```

---

## Metrics

The alert receiver emits OpenTelemetry metrics for monitoring:

### Fingerprint Metrics

- **honua.alerts.fingerprint_length** (histogram)
  - Unit: `{character}`
  - Description: Distribution of alert fingerprint lengths
  - Use Case: Identify alerts approaching the 256-character limit

### Other Metrics

- **honua.alerts.received** (counter): Alerts received by source and severity
- **honua.alerts.sent** (counter): Alerts sent to providers
- **honua.alerts.suppressed** (counter): Alerts suppressed by reason
  - Reasons: `deduplication`, `silenced`, `acknowledged`, `fingerprint_too_long`, `publish_failure`
- **honua.alerts.latency** (histogram): Alert delivery latency by provider
- **honua.alerts.circuit_breaker_state** (gauge): Circuit breaker state by provider

---

## Status Codes

| Status | Meaning |
|--------|---------|
| `sent` | Alert published successfully to all configured providers |
| `deduplicated` | Alert suppressed due to recent identical alert (within deduplication window) |
| `silenced` | Alert matches silencing rules |
| `acknowledged` | Alert has been acknowledged and is suppressed |
| `failed` | Alert publishing failed (persisted but not delivered) |
| `partial_success` | Some alerts in batch succeeded, others failed |

---

## Error Handling

All errors return structured JSON responses:

```json
{
  "error": "Human-readable error message",
  "details": "Additional context (optional)",
  "field": "Specific field that failed validation (optional)"
}
```

### Common Errors

| Error | Status | Solution |
|-------|--------|----------|
| Fingerprint too long | 400 | Use hashed identifier or omit for auto-generation |
| Too many labels | 400 | Reduce labels to 50 or fewer |
| Too many context entries | 400 | Reduce context to 100 or fewer |
| Label key too long | 400 | Limit label keys to 256 characters |
| Label value too long | 400 | Limit label values to 1000 characters |
| Persistence unavailable | 503 | Retry with exponential backoff |

---

## Examples

### Example 1: Basic Alert with Auto-Generated Fingerprint

```bash
curl -X POST https://api.example.com/api/alerts \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "High CPU Usage",
    "severity": "warning",
    "status": "firing",
    "summary": "CPU usage exceeded 80%",
    "source": "monitoring-agent",
    "service": "web-api",
    "environment": "production",
    "labels": {
      "host": "web-01",
      "region": "us-east-1"
    }
  }'
```

### Example 2: Alert with Custom Hashed Fingerprint

```bash
# Generate fingerprint (64-character SHA256 hex)
FINGERPRINT=$(echo -n "my-unique-long-identifier" | sha256sum | cut -d' ' -f1)

curl -X POST https://api.example.com/api/alerts \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{
    \"name\": \"Database Connection Pool Exhausted\",
    \"severity\": \"critical\",
    \"status\": \"firing\",
    \"source\": \"app-server\",
    \"service\": \"order-processing\",
    \"fingerprint\": \"$FINGERPRINT\"
  }"
```

### Example 3: Batch Alert Submission

```bash
curl -X POST https://api.example.com/api/alerts/batch \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "alerts": [
      {
        "name": "Disk Space Low",
        "severity": "warning",
        "status": "firing",
        "source": "monitoring",
        "service": "database-01"
      },
      {
        "name": "Memory Usage High",
        "severity": "warning",
        "status": "firing",
        "source": "monitoring",
        "service": "database-01"
      }
    ]
  }'
```

---

## Migration Guide: Fingerprint Truncation Fix

**Breaking Change**: As of version 2.x, fingerprints exceeding 256 characters are rejected instead of silently truncated.

### Before (Version 1.x)

```json
{
  "fingerprint": "very-long-identifier-that-exceeds-256-characters..." // Silently truncated
}
// Result: Accepted, truncated to 256 chars (could cause collisions)
```

### After (Version 2.x)

```json
{
  "fingerprint": "very-long-identifier-that-exceeds-256-characters..." // Rejected
}
// Result: HTTP 400 Bad Request
```

### Migration Steps

1. **Audit Existing Fingerprints**: Query your alerts for fingerprint lengths
   ```sql
   SELECT fingerprint, LENGTH(fingerprint) as len
   FROM alerts
   WHERE LENGTH(fingerprint) > 256;
   ```

2. **Update Client Code**: Hash long identifiers before sending
   ```csharp
   // Old (will be rejected)
   var fingerprint = $"{longValue1}-{longValue2}-{longValue3}";

   // New (works)
   var key = $"{longValue1}-{longValue2}-{longValue3}";
   using var sha = System.Security.Cryptography.SHA256.Create();
   var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
   var fingerprint = Convert.ToHexString(hash).ToLowerInvariant();
   ```

3. **Monitor Metrics**: Watch `honua.alerts.suppressed` with reason `fingerprint_too_long`

4. **Test Endpoints**: Verify all alert-sending clients comply with the 256-character limit

---

## Support

For issues or questions:
- GitHub Issues: [https://github.com/honua/honua](https://github.com/honua/honua)
- Documentation: [https://docs.honua.io](https://docs.honua.io)
