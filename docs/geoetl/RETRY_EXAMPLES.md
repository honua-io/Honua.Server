# GeoETL Retry Logic - Practical Examples

This document provides real-world examples of how the retry logic works in practice.

## Example 1: Transient Network Error - Automatic Recovery

### Scenario
A workflow fetches data from an external API that occasionally times out.

### Workflow Definition
```json
{
  "id": "api-data-pipeline",
  "nodes": [
    {
      "id": "fetch-earthquake-data",
      "type": "data_source.http",
      "parameters": {
        "url": "https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/all_week.geojson"
      },
      "execution": {
        "maxRetries": 5,
        "timeoutSeconds": 30
      }
    }
  ]
}
```

### Execution Timeline

```
10:15:00 - Workflow starts, node "fetch-earthquake-data" begins
10:15:25 - Request times out (network congestion)
10:15:25 - Error categorized as: Transient
10:15:25 - Retry policy: 5 max attempts, exponential backoff
10:15:30 - Retry attempt 1 (wait 5 seconds)
10:15:30 - Request times out again
10:15:35 - Retry attempt 2 (wait 10 seconds, exponential)
10:15:45 - Request succeeds! ✓
10:15:45 - Node completes successfully
10:15:45 - Workflow continues to next node
```

### Logs
```
[10:15:00 INF] Starting workflow execution api-data-pipeline
[10:15:25 WRN] Node fetch-earthquake-data attempt 0 failed with Transient error: Request timeout
[10:15:30 INF] Retrying node fetch-earthquake-data, attempt 1/5
[10:15:30 INF] Waiting 5000ms before retry attempt 1/5
[10:15:35 WRN] Node fetch-earthquake-data attempt 1 failed with Transient error
[10:15:35 INF] Waiting 10000ms before retry attempt 2/5
[10:15:45 INF] Node fetch-earthquake-data succeeded after 2 retries
[10:15:45 INF] Workflow execution api-data-pipeline completed successfully
```

### Result
- **Workflow Status**: Success
- **Total Duration**: 45 seconds
- **Retries**: 2
- **User Impact**: None (automatic recovery)

---

## Example 2: Data Quality Error - No Retry

### Scenario
A workflow processes GeoJSON data with invalid geometry.

### Workflow Definition
```json
{
  "id": "geometry-validation",
  "nodes": [
    {
      "id": "load-parcels",
      "type": "data_source.file",
      "parameters": {
        "path": "/data/parcels.geojson"
      }
    },
    {
      "id": "validate-geometries",
      "type": "geoprocessing.buffer",
      "parameters": {
        "distance": 0
      }
    }
  ]
}
```

### Execution Timeline

```
11:00:00 - Workflow starts
11:00:01 - load-parcels completes successfully (1000 features)
11:00:01 - validate-geometries begins processing
11:00:05 - Error: Invalid geometry at feature 342 (self-intersecting polygon)
11:00:05 - Error categorized as: Data
11:00:05 - Retry policy: Data errors are not retryable
11:00:05 - Workflow fails immediately
11:00:05 - Added to dead letter queue for manual review
```

### Error Details
```json
{
  "workflowRunId": "abc-123",
  "nodeId": "validate-geometries",
  "category": "Data",
  "message": "Invalid geometry: Self-intersecting polygon at coordinates [...]",
  "suggestion": "This appears to be a data quality issue. Please check your input data for validity.",
  "inputData": {
    "featureIndex": 342,
    "featureId": "parcel-8472"
  }
}
```

### Result
- **Workflow Status**: Failed
- **Retries**: 0 (not retryable)
- **Next Steps**: Fix data quality issue in source file

---

## Example 3: Circuit Breaker Opens - Prevents Cascading Failures

### Scenario
An external geocoding API goes down. Multiple workflows are failing.

### Timeline

```
14:00:00 - Workflow A starts, geocode node fails
14:00:05 - Workflow B starts, geocode node fails
14:00:10 - Workflow C starts, geocode node fails
14:00:15 - Workflow D starts, geocode node fails
14:00:20 - Workflow E starts, geocode node fails

Circuit breaker trips (5 consecutive failures)

14:00:25 - Workflow F starts
14:00:25 - geocode node immediately blocked (circuit open)
14:00:25 - Error: "Circuit breaker is open for node type 'data_source.geocoding'"
14:00:25 - Workflow F fails fast (no API call made)

Wait 60 seconds (circuit breaker timeout)

14:01:25 - Circuit transitions to Half-Open
14:01:30 - Workflow G starts
14:01:30 - geocode node allowed through (test request)
14:01:35 - geocode succeeds! API is back online
14:01:35 - Circuit transitions to Closed
14:01:40 - All subsequent workflows succeed normally
```

### Circuit Breaker Stats
```json
{
  "nodeType": "data_source.geocoding",
  "state": "Open",
  "consecutiveFailures": 5,
  "totalFailures": 5,
  "totalSuccesses": 0,
  "failureRate": 1.0,
  "openedAt": "2025-01-07T14:00:20Z",
  "halfOpenAt": "2025-01-07T14:01:20Z"
}
```

### Benefits
- Prevented 10+ additional API calls to failing service
- Fast-fail for affected workflows (no 30-second timeouts)
- Automatic recovery when service restored
- Protected external service from additional load

---

## Example 4: Rate Limiting - Retry with Backoff

### Scenario
A workflow hits rate limits on a third-party API.

### Execution Timeline

```
15:30:00 - Workflow starts
15:30:01 - API call #1 succeeds
15:30:02 - API call #2 succeeds
15:30:03 - API call #3 succeeds
15:30:04 - API call #4 returns: HTTP 429 Too Many Requests
15:30:04 - Error categorized as: Resource
15:30:04 - Retry with exponential backoff

15:30:09 - Retry #1 (wait 5s)
15:30:09 - Still rate limited, HTTP 429
15:30:19 - Retry #2 (wait 10s)
15:30:19 - Still rate limited, HTTP 429
15:30:39 - Retry #3 (wait 20s)
15:30:39 - Success! Rate limit window reset

15:30:40 - Continue processing
```

### Configuration
```json
{
  "execution": {
    "maxRetries": 5,
    "timeoutSeconds": 60
  }
}
```

Custom retry policy in node:
```csharp
protected override RetryPolicy GetRetryPolicy()
{
    return new RetryPolicy
    {
        MaxAttempts = 5,
        BackoffStrategy = BackoffStrategy.Exponential,
        InitialDelaySeconds = 5,
        RetryableErrors = new HashSet<ErrorCategory>
        {
            ErrorCategory.Resource,
            ErrorCategory.Transient
        }
    };
}
```

---

## Example 5: Dead Letter Queue - Manual Retry

### Scenario
A workflow fails due to external service downtime. Operator retries after service is restored.

### Initial Failure
```
16:00:00 - Workflow starts
16:00:05 - External service unavailable (HTTP 503)
16:00:05 - Error categorized as: External
16:00:10 - Retry #1 fails
16:00:20 - Retry #2 fails
16:00:40 - Retry #3 fails (max retries exhausted)
16:00:40 - Workflow marked as Failed
16:00:40 - Added to dead letter queue
```

### Dead Letter Queue Entry
```json
{
  "id": "dlq-456",
  "workflowRunId": "run-789",
  "workflowId": "workflow-123",
  "status": "Pending",
  "errorCategory": "External",
  "errorMessage": "Service unavailable: geocoding.example.com",
  "failedNodeId": "geocode-addresses",
  "failedNodeType": "data_source.geocoding",
  "retryCount": 0,
  "failedAt": "2025-01-07T16:00:40Z",
  "priority": "Low"
}
```

### Manual Retry (After Service Restored)
```bash
# Operator checks service status
curl https://geocoding.example.com/health
# Status: OK

# Retry the failed workflow
POST /admin/api/geoetl/failed-workflows/dlq-456/retry
{
  "fromPoint": "FailedNode",
  "forceRetry": false
}
```

### Retry Timeline
```
17:30:00 - Operator initiates retry
17:30:00 - Workflow resumes from "geocode-addresses" node
17:30:00 - Previous successful nodes (load-data) are NOT re-executed
17:30:05 - geocode-addresses succeeds
17:30:10 - Workflow completes successfully
17:30:10 - Dead letter queue entry marked as Resolved
```

---

## Example 6: Bulk Retry - Mass Recovery

### Scenario
Database was down for 1 hour. 47 workflows failed. After database is restored, retry all at once.

### Failed Workflows
```sql
SELECT id, workflow_name, failed_at, error_category
FROM geoetl_failed_workflows
WHERE failed_at BETWEEN '2025-01-07 18:00' AND '2025-01-07 19:00'
  AND error_category = 'External'
  AND status = 'Pending';

-- Results: 47 workflows
```

### Bulk Retry Request
```bash
POST /admin/api/geoetl/failed-workflows/bulk-retry
{
  "failedWorkflowIds": [
    "dlq-001", "dlq-002", "dlq-003", ..., "dlq-047"
  ],
  "options": {
    "fromPoint": "FailedNode",
    "forceRetry": false
  }
}
```

### Bulk Retry Results
```json
{
  "totalRequested": 47,
  "succeeded": 45,
  "failed": 2,
  "results": [
    {
      "failedWorkflowId": "dlq-001",
      "success": true,
      "newRunId": "run-1001"
    },
    // ...
    {
      "failedWorkflowId": "dlq-023",
      "success": false,
      "errorMessage": "Data source no longer exists"
    }
  ]
}
```

### Statistics
- **Success Rate**: 95.7% (45/47)
- **Total Retry Time**: 3 minutes
- **Manual Effort**: 1 API call
- **Failed Workflows Still Pending**: 2 (require investigation)

---

## Example 7: Custom Retry Strategy - Image Processing

### Scenario
Heavy image processing nodes that need longer backoff times.

### Custom Node Implementation
```csharp
public class ImageProcessingNode : WorkflowNodeBase
{
    protected override RetryPolicy GetRetryPolicy()
    {
        return new RetryPolicy
        {
            MaxAttempts = 3,
            BackoffStrategy = BackoffStrategy.Exponential,
            InitialDelaySeconds = 30,  // Longer initial delay
            MaxDelaySeconds = 600,      // Up to 10 minutes
            UseJitter = true,
            JitterFactor = 0.3,
            RetryableErrors = new HashSet<ErrorCategory>
            {
                ErrorCategory.Resource,  // Out of memory
                ErrorCategory.Transient  // Temporary file access issues
            }
        };
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Process large image files
            var imagePath = context.Parameters["imagePath"].ToString();
            var processedImage = await ProcessImageAsync(imagePath);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                ["processedPath"] = processedImage.Path,
                ["width"] = processedImage.Width,
                ["height"] = processedImage.Height
            });
        }
        catch (OutOfMemoryException ex)
        {
            // Will be categorized as Resource and retried
            throw;
        }
    }
}
```

### Execution with Memory Pressure
```
20:00:00 - Start image processing (100 MB image)
20:00:45 - OutOfMemoryException (categorized as Resource)
20:01:15 - Retry #1 (wait 30s, system may have freed memory)
20:01:45 - OutOfMemoryException again
20:02:45 - Retry #2 (wait 60s, exponential backoff)
20:03:30 - Success! (memory available after GC)
```

---

## Example 8: Configuration Override - Production vs Development

### Development Environment
```json
{
  "GeoETL": {
    "Retry": {
      "DefaultMaxAttempts": 1,
      "DefaultBackoff": "None"
    },
    "CircuitBreaker": {
      "Enabled": false
    },
    "DeadLetterQueue": {
      "Enabled": false
    }
  }
}
```

**Result**: Fast failures for debugging, no automatic retries.

### Production Environment
```json
{
  "GeoETL": {
    "Retry": {
      "DefaultMaxAttempts": 5,
      "DefaultBackoff": "Exponential",
      "DefaultInitialDelaySeconds": 5,
      "UseJitter": true
    },
    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5,
      "TimeoutSeconds": 60
    },
    "DeadLetterQueue": {
      "Enabled": true,
      "RetentionDays": 30
    }
  }
}
```

**Result**: Maximum resilience, automatic recovery from transient failures.

---

## Monitoring Dashboard Examples

### Error Statistics (Last 7 Days)
```json
{
  "totalFailures": 234,
  "pendingFailures": 12,
  "resolvedFailures": 210,
  "abandonedFailures": 12,
  "failuresByCategory": {
    "Transient": 145,    // 62% - auto-recovered
    "External": 45,       // 19% - external service issues
    "Data": 23,          // 10% - data quality
    "Configuration": 12, // 5%  - setup issues
    "Resource": 9        // 4%  - resource constraints
  },
  "averageRetryCount": 2.1,
  "successfulRetryRate": 0.89  // 89% of retries succeed
}
```

### Top Errors
```json
{
  "topErrors": [
    {
      "errorMessage": "Connection timeout to geocoding.example.com",
      "category": "Transient",
      "count": 67,
      "lastOccurred": "2025-01-07T15:23:00Z"
    },
    {
      "errorMessage": "Invalid geometry: Self-intersecting polygon",
      "category": "Data",
      "count": 23,
      "lastOccurred": "2025-01-06T11:45:00Z"
    },
    {
      "errorMessage": "Rate limit exceeded",
      "category": "Resource",
      "count": 9,
      "lastOccurred": "2025-01-05T09:12:00Z"
    }
  ]
}
```

---

## Summary of Examples

| Example | Error Type | Retry? | Outcome | Key Takeaway |
|---------|------------|--------|---------|--------------|
| 1. Network Timeout | Transient | ✅ Yes (2 retries) | Success | Automatic recovery from temporary issues |
| 2. Invalid Data | Data | ❌ No | Failed (DLQ) | Data issues require manual fix |
| 3. Circuit Breaker | External | ❌ Blocked | Fast-fail | Prevents cascading failures |
| 4. Rate Limiting | Resource | ✅ Yes (3 retries) | Success | Backoff respects rate limits |
| 5. Manual Retry | External | ✅ Yes (manual) | Success | DLQ enables recovery after fix |
| 6. Bulk Retry | External | ✅ Yes (bulk) | 95% success | Mass recovery after incident |
| 7. Custom Policy | Resource | ✅ Yes (custom) | Success | Tailored retry for node type |
| 8. Config Override | Various | Varies | Various | Environment-specific behavior |

## Best Practices Demonstrated

1. **Automatic retry for transient errors** - No manual intervention needed
2. **Fast-fail for permanent errors** - Don't waste time retrying data issues
3. **Circuit breaker protection** - Prevent overwhelming failing services
4. **Exponential backoff** - Give systems time to recover
5. **Jitter** - Avoid thundering herd when retrying
6. **Dead letter queue** - Capture failures for analysis and manual retry
7. **Bulk operations** - Recover from mass failures efficiently
8. **Environment-specific config** - Different behavior for dev vs prod
9. **Monitoring and analytics** - Track patterns and success rates
10. **Custom policies** - Tailor retry behavior per node type
