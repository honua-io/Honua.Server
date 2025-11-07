# GeoETL Error Handling and Retry Logic

This guide explains the comprehensive error handling and retry mechanisms built into the GeoETL workflow engine to make workflows production-ready and resilient.

## Overview

GeoETL includes several layers of error handling:

1. **Automatic Retry Logic** - Node-level and workflow-level retries with exponential backoff
2. **Circuit Breaker Pattern** - Prevents cascading failures by temporarily blocking failing nodes
3. **Dead Letter Queue** - Failed workflows are captured for manual review and retry
4. **Error Categorization** - Intelligent classification of errors for appropriate handling
5. **Monitoring & Analytics** - Track failure patterns and success rates

## Error Categories

Errors are automatically categorized to determine retry behavior:

| Category | Description | Retry? | Examples |
|----------|-------------|--------|----------|
| **Transient** | Temporary failures likely to succeed on retry | ✅ Yes | Network timeout, temporary unavailability |
| **Data** | Invalid or corrupt data | ❌ No | Invalid geometry, missing required field |
| **Resource** | System resource constraints | ✅ Yes (with backoff) | Out of memory, rate limiting |
| **Configuration** | Setup or credential issues | ❌ No | Missing API key, invalid credentials |
| **External** | Third-party service failures | ✅ Yes | External API down, service unavailable |
| **Logic** | Code bugs or unexpected errors | ❌ No | Null reference, index out of range |

## Retry Policies

### Default Retry Policy

```csharp
var policy = RetryPolicy.Default;
// MaxAttempts: 3
// BackoffStrategy: Exponential
// InitialDelaySeconds: 5
// MaxDelaySeconds: 300 (5 minutes)
// RetryableErrors: Transient, Resource, External
```

### Predefined Policies

```csharp
// For transient errors (more aggressive)
var policy = RetryPolicy.ForTransientErrors;
// MaxAttempts: 5, InitialDelay: 2s

// For external service calls
var policy = RetryPolicy.ForExternalServices;
// MaxAttempts: 3, InitialDelay: 10s

// Disable retries
var policy = RetryPolicy.NoRetry;
```

### Custom Retry Policy

```csharp
var customPolicy = new RetryPolicy
{
    MaxAttempts = 5,
    BackoffStrategy = BackoffStrategy.Exponential,
    InitialDelaySeconds = 10,
    MaxDelaySeconds = 600,
    UseJitter = true,
    JitterFactor = 0.3,
    RetryableErrors = new HashSet<ErrorCategory>
    {
        ErrorCategory.Transient,
        ErrorCategory.External
    }
};
```

## Backoff Strategies

### Exponential Backoff (Recommended)

Doubles the delay with each retry to avoid overwhelming failing services:

```
Attempt 1: Wait 5 seconds
Attempt 2: Wait 10 seconds
Attempt 3: Wait 20 seconds
Attempt 4: Wait 40 seconds
Attempt 5: Wait 80 seconds (capped at MaxDelaySeconds)
```

### Linear Backoff

Increases delay linearly:

```
Attempt 1: Wait 5 seconds
Attempt 2: Wait 10 seconds
Attempt 3: Wait 15 seconds
Attempt 4: Wait 20 seconds
```

### Constant Backoff

Same delay between all retries:

```
All attempts: Wait 5 seconds
```

### Jitter

Adding jitter (random variation) prevents the "thundering herd" problem when multiple workflows retry simultaneously:

```csharp
policy.UseJitter = true;
policy.JitterFactor = 0.2; // ±20% randomness

// Attempt 1 base delay: 10s
// With jitter: 8-12s (randomly chosen)
```

## Node-Level Retry

### Configure per Node

```json
{
  "nodes": [
    {
      "id": "fetch-data",
      "type": "data_source.http",
      "parameters": {
        "url": "https://api.example.com/data"
      },
      "execution": {
        "maxRetries": 5,
        "timeoutSeconds": 30
      }
    }
  ]
}
```

### Custom Retry Policy in Node Implementation

```csharp
public class MyDataSourceNode : WorkflowNodeBase
{
    protected override RetryPolicy GetRetryPolicy()
    {
        // Use aggressive retry for this node type
        return RetryPolicy.ForTransientErrors;
    }

    protected override async Task<NodeExecutionResult> ExecuteInternalAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Implementation
        // Retry logic is automatically applied by base class
    }
}
```

## Workflow-Level Error Handling

### Continue on Error

```json
{
  "workflow": {
    "id": "data-pipeline",
    "continueOnError": true
  }
}
```

When enabled, the workflow continues executing subsequent nodes even if one fails. Failed nodes are marked but don't stop the entire workflow.

### Stop on First Error (Default)

```json
{
  "workflow": {
    "id": "data-pipeline",
    "continueOnError": false
  }
}
```

Workflow stops at the first node failure and is added to the dead letter queue.

## Circuit Breaker

The circuit breaker prevents cascading failures by temporarily blocking nodes that are repeatedly failing.

### How It Works

1. **Closed State** (Normal Operation)
   - All requests flow through normally
   - Failures are counted

2. **Open State** (Blocking)
   - After `FailureThreshold` consecutive failures
   - All requests are immediately blocked
   - Enters this state for `TimeoutSeconds`

3. **Half-Open State** (Testing)
   - After timeout expires
   - Allows one test request through
   - Success → Returns to Closed
   - Failure → Returns to Open

### Configuration

```json
{
  "GeoETL": {
    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5,
      "TimeoutSeconds": 60
    }
  }
}
```

### Monitoring Circuit Breakers

```bash
# Get all circuit breaker states
GET /admin/api/geoetl/circuit-breakers

# Response
{
  "enabled": true,
  "circuitBreakers": [
    {
      "nodeType": "data_source.http",
      "state": "Open",
      "consecutiveFailures": 5,
      "totalFailures": 23,
      "totalSuccesses": 145,
      "failureRate": 0.137,
      "lastFailureAt": "2025-01-07T10:30:00Z",
      "openedAt": "2025-01-07T10:30:00Z",
      "halfOpenAt": "2025-01-07T10:31:00Z"
    }
  ]
}
```

### Resetting a Circuit Breaker

```bash
# Manually reset if you've fixed the underlying issue
POST /admin/api/geoetl/circuit-breakers/data_source.http/reset
```

## Dead Letter Queue

Failed workflows are automatically added to the dead letter queue for review and retry.

### Viewing Failed Workflows

```bash
# List all failed workflows
GET /admin/api/geoetl/failed-workflows?status=Pending

# Get specific failure
GET /admin/api/geoetl/failed-workflows/{id}

# Filter by error category
GET /admin/api/geoetl/failed-workflows?errorCategory=Transient

# Search by error message
GET /admin/api/geoetl/failed-workflows?searchText=timeout
```

### Retry Options

#### Retry from Failed Node (Recommended)

```bash
POST /admin/api/geoetl/failed-workflows/{id}/retry
{
  "fromPoint": "FailedNode",
  "forceRetry": false
}
```

Continues execution from the node that failed, preserving results from successful upstream nodes.

#### Retry from Beginning

```bash
POST /admin/api/geoetl/failed-workflows/{id}/retry
{
  "fromPoint": "Beginning",
  "forceRetry": false
}
```

Restarts the entire workflow from scratch.

#### Retry with Parameter Overrides

```bash
POST /admin/api/geoetl/failed-workflows/{id}/retry
{
  "fromPoint": "FailedNode",
  "parameterOverrides": {
    "timeout": 60,
    "batchSize": 100
  },
  "forceRetry": true
}
```

Retry with different parameters and force retry even if circuit breaker is open.

### Bulk Operations

```bash
# Retry multiple workflows
POST /admin/api/geoetl/failed-workflows/bulk-retry
{
  "failedWorkflowIds": [
    "uuid-1",
    "uuid-2",
    "uuid-3"
  ],
  "options": {
    "fromPoint": "FailedNode"
  }
}

# Response
{
  "totalRequested": 3,
  "succeeded": 2,
  "failed": 1,
  "results": [...]
}
```

### Abandon Workflows

```bash
POST /admin/api/geoetl/failed-workflows/{id}/abandon
{
  "reason": "Data source permanently unavailable"
}
```

Mark a workflow as abandoned if it cannot or should not be retried.

## Blazor UI

Navigate to `/geoetl/failed-workflows` to access the web interface:

### Features

- **Dashboard** - View failure statistics and trends
- **Filter & Search** - Find failures by status, category, date, or error message
- **Detailed View** - Full error information, stack traces, and context
- **Retry Controls** - One-click retry with options
- **Bulk Operations** - Select and retry multiple workflows
- **Assignment** - Assign failures to team members for investigation

### Statistics Dashboard

Shows:
- Total failures in period
- Pending vs. resolved
- Average retry count
- Failure rate by category
- Most common errors
- Failure trends over time

## Error Statistics API

```bash
# Get error statistics
GET /admin/api/geoetl/error-stats?from=2025-01-01&to=2025-01-31

# Response
{
  "fromDate": "2025-01-01T00:00:00Z",
  "toDate": "2025-01-31T23:59:59Z",
  "totalFailures": 156,
  "pendingFailures": 23,
  "resolvedFailures": 120,
  "abandonedFailures": 13,
  "failuresByCategory": {
    "Transient": 78,
    "External": 45,
    "Data": 23,
    "Configuration": 10
  },
  "failuresByNodeType": {
    "data_source.http": 45,
    "geoprocessing.buffer": 23,
    "data_sink.postgis": 12
  },
  "topErrors": [
    {
      "errorMessage": "Connection timeout",
      "category": "Transient",
      "count": 34,
      "lastOccurred": "2025-01-30T15:23:00Z"
    }
  ],
  "averageRetryCount": 2.3,
  "successfulRetryRate": 0.76
}
```

## Configuration Reference

### appsettings.json

```json
{
  "GeoETL": {
    "Retry": {
      "DefaultMaxAttempts": 3,
      "DefaultBackoff": "Exponential",
      "DefaultInitialDelaySeconds": 5,
      "DefaultMaxDelaySeconds": 300,
      "TransientErrorRetryAttempts": 5,
      "UseJitter": true,
      "JitterFactor": 0.2
    },
    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5,
      "TimeoutSeconds": 60
    },
    "DeadLetterQueue": {
      "Enabled": true,
      "RetentionDays": 30,
      "MaxRetries": 3,
      "AutoRetryTransientErrors": false
    }
  }
}
```

## Best Practices

### 1. Use Appropriate Retry Policies

```csharp
// Network operations - aggressive retry
protected override RetryPolicy GetRetryPolicy()
{
    return RetryPolicy.ForTransientErrors;
}

// Data validation - no retry
protected override RetryPolicy GetRetryPolicy()
{
    return RetryPolicy.NoRetry;
}
```

### 2. Set Realistic Timeouts

```json
{
  "execution": {
    "timeoutSeconds": 300,  // 5 minutes
    "maxRetries": 3
  }
}
```

### 3. Use Idempotent Operations

Ensure nodes can be safely retried without causing duplicate data or inconsistent state.

### 4. Monitor Circuit Breakers

Set up alerts when circuit breakers open frequently - it indicates a systemic problem.

### 5. Review Failed Workflows Regularly

Don't let the dead letter queue grow indefinitely. Review and resolve or abandon old failures.

### 6. Use Specific Error Messages

```csharp
// Good
throw new InvalidOperationException("Failed to parse GeoJSON: missing 'coordinates' property");

// Bad
throw new Exception("Error");
```

Specific messages enable better error categorization and debugging.

## Troubleshooting

### High Failure Rate

1. Check circuit breaker status
2. Review error statistics by category
3. Look for patterns in failed workflows
4. Check external service status
5. Review system resources (memory, CPU)

### Workflows Stuck in Retrying

1. Check if circuit breaker is open
2. Verify retry policy configuration
3. Review error category classification
4. Consider if error should be retryable

### Circuit Breaker Opens Frequently

1. Investigate underlying cause
2. Fix root issue before resetting
3. Consider adjusting threshold
4. Check if timeouts are too aggressive

## Example: Complete Resilient Workflow

```json
{
  "id": "resilient-data-pipeline",
  "version": 1,
  "metadata": {
    "name": "Resilient Data Pipeline",
    "description": "Production-ready pipeline with comprehensive error handling"
  },
  "parameters": {
    "apiUrl": {
      "type": "string",
      "required": true
    }
  },
  "nodes": [
    {
      "id": "fetch-data",
      "type": "data_source.http",
      "parameters": {
        "url": "{{parameters.apiUrl}}",
        "method": "GET"
      },
      "execution": {
        "maxRetries": 5,
        "timeoutSeconds": 30
      }
    },
    {
      "id": "process-geometries",
      "type": "geoprocessing.buffer",
      "parameters": {
        "distance": 100
      },
      "execution": {
        "maxRetries": 2,
        "timeoutSeconds": 300
      }
    },
    {
      "id": "save-results",
      "type": "data_sink.postgis",
      "parameters": {
        "table": "processed_data"
      },
      "execution": {
        "maxRetries": 3,
        "timeoutSeconds": 60
      }
    }
  ],
  "edges": [
    {"from": "fetch-data", "to": "process-geometries"},
    {"from": "process-geometries", "to": "save-results"}
  ]
}
```

## Maintenance

### Cleanup Old Failures

```bash
# Clean up resolved/abandoned failures older than 30 days
POST /admin/api/geoetl/error-stats/cleanup
{
  "retentionDays": 30
}
```

### Database Maintenance

The system stores failure history in PostgreSQL:

```sql
-- View recent failures
SELECT * FROM geoetl_failed_workflows
WHERE failed_at > NOW() - INTERVAL '7 days'
ORDER BY failed_at DESC;

-- Get failure statistics
SELECT * FROM geoetl_error_stats
WHERE failure_date > NOW() - INTERVAL '30 days';

-- Clean up old data
DELETE FROM geoetl_failed_workflows
WHERE status IN ('Resolved', 'Abandoned')
  AND resolved_at < NOW() - INTERVAL '90 days';
```

## Support

For questions or issues:
- Review error logs in `/logs/honua-*.log`
- Check the Blazor UI at `/geoetl/failed-workflows`
- Examine circuit breaker status via API
- Contact support with workflow run ID and error details
