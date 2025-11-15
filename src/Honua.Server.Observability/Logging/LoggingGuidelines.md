# Honua.Server Logging Guidelines

This document provides guidelines for structured logging across the Honua.Server codebase following OpenTelemetry and industry best practices.

## Table of Contents
- [Structured Logging Principles](#structured-logging-principles)
- [Log Levels](#log-levels)
- [Correlation IDs](#correlation-ids)
- [Sensitive Data Protection](#sensitive-data-protection)
- [Performance Considerations](#performance-considerations)
- [Examples](#examples)

## Structured Logging Principles

### Use ILogger<T> with Structured Logging

**DO** use structured logging with named parameters:

```csharp
logger.LogInformation("User {UserId} performed {Action} on resource {ResourceId}",
    userId, action, resourceId);
```

**DON'T** use string interpolation or concatenation:

```csharp
// ❌ Bad - not structured
logger.LogInformation($"User {userId} performed {action}");
```

### Include Context in Log Messages

Always include relevant context that helps with debugging and troubleshooting:

```csharp
logger.LogWarning(
    "Query execution exceeded threshold - Duration: {DurationMs}ms, Query: {QueryType}, Layer: {LayerId}",
    durationMs, queryType, layerId);
```

## Log Levels

Use appropriate log levels following these guidelines:

### Trace
- Very detailed information for diagnosing issues in development
- May contain sensitive information (disable in production)
- Example: SQL query parameters, request/response bodies

```csharp
logger.LogTrace("Executing query with parameters: {Parameters}", parameters);
```

### Debug
- Detailed information useful during development
- Can be enabled in production for troubleshooting
- Example: Cache hits/misses, algorithm decisions

```csharp
logger.LogDebug("Cache {CacheResult} for key {CacheKey}", hit ? "hit" : "miss", cacheKey);
```

### Information
- General informational messages about application flow
- Example: Service start/stop, important state changes

```csharp
logger.LogInformation("Service {ServiceName} started successfully on port {Port}",
    serviceName, port);
```

### Warning
- Unexpected situations that don't prevent operation
- Degraded performance or temporary failures
- Example: Slow queries, retry attempts, deprecated API usage

```csharp
logger.LogWarning(
    "Slow query detected - Duration: {DurationMs}ms exceeds threshold {ThresholdMs}ms - Layer: {LayerId}",
    durationMs, thresholdMs, layerId);
```

### Error
- Errors that prevent operation but not the entire application
- Caught exceptions that can be recovered
- Example: Failed database queries, invalid user input

```csharp
logger.LogError(exception,
    "Failed to process request for layer {LayerId} - Error: {ErrorMessage}",
    layerId, exception.Message);
```

### Critical
- Catastrophic failures requiring immediate attention
- Application crashes or data loss scenarios
- Example: Database unavailable, configuration errors

```csharp
logger.LogCritical(exception,
    "Database connection failed - Application cannot start");
```

## Correlation IDs

### Automatic Correlation ID Enrichment

All logs are automatically enriched with correlation IDs via the `CorrelationIdMiddleware`. The correlation ID is:

1. Extracted from `X-Correlation-ID` header
2. Falls back to W3C Trace Context `traceparent` header
3. Generated if not present
4. Added to all logs via `LogContext.PushProperty("CorrelationId", correlationId)`

### Manual Correlation ID Usage

For background tasks or async operations outside HTTP context:

```csharp
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    logger.LogInformation("Processing background task {TaskId}", taskId);
}
```

### Cross-Service Correlation

When making HTTP calls to other services, propagate the correlation ID:

```csharp
httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
```

## Sensitive Data Protection

### Never Log These Items

**NEVER** log the following sensitive information:

- Passwords, password hashes, or password reset tokens
- API keys, secrets, or access tokens
- JWT tokens (full token - logging metadata is OK)
- Credit card numbers or payment information
- Social Security Numbers or national identifiers
- Personally Identifiable Information (PII) without explicit consent
- Database connection strings with credentials
- Encryption keys or certificates
- Session IDs or authentication cookies

### Sensitive Data Patterns to Avoid

```csharp
// ❌ Bad - logs password
logger.LogInformation("User login with username {Username} and password {Password}",
    username, password);

// ✅ Good - no sensitive data
logger.LogInformation("User login attempt for {Username}", username);
```

```csharp
// ❌ Bad - logs full token
logger.LogInformation("Authenticated with token {Token}", jwtToken);

// ✅ Good - logs only metadata
logger.LogInformation("Authenticated with token type {TokenType} expiring at {Expiry}",
    tokenType, expiryTime);
```

```csharp
// ❌ Bad - logs connection string with credentials
logger.LogError(exception, "Database connection failed: {ConnectionString}", connectionString);

// ✅ Good - logs only server/database name
logger.LogError(exception, "Database connection failed to {Server}/{Database}",
    serverName, databaseName);
```

### Redacting Sensitive Data

When you must log data that might contain sensitive information, redact it first:

```csharp
// Example redaction helper
public static string RedactEmail(string email)
{
    if (string.IsNullOrEmpty(email)) return email;
    var parts = email.Split('@');
    if (parts.Length != 2) return "***";
    return $"{parts[0].Substring(0, Math.Min(2, parts[0].Length))}***@{parts[1]}";
}

logger.LogInformation("Password reset sent to {Email}", RedactEmail(userEmail));
// Output: "Password reset sent to jo***@example.com"
```

### Automatic Redaction

The `RequestResponseLoggingMiddleware` automatically redacts:

- Headers: `Authorization`, `Cookie`, `Set-Cookie`, `X-API-Key`, `X-Auth-Token`
- JSON body fields: `password`, `secret`, `token`, `apiKey`, `api_key`
- Query parameters: `password`, `secret`, `token`, `api_key`

## Performance Considerations

### Use LoggerMessage for High-Frequency Logging

For frequently called code paths, use source-generated logging for better performance:

```csharp
public static partial class LoggerExtensions
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Cache {CacheResult} for key {CacheKey} in {DurationMs}ms")]
    public static partial void LogCacheResult(
        this ILogger logger,
        string cacheResult,
        string cacheKey,
        long durationMs);
}

// Usage
logger.LogCacheResult("hit", cacheKey, durationMs);
```

### Avoid Expensive Operations in Log Statements

```csharp
// ❌ Bad - serializes object even if logging is disabled
logger.LogDebug("Request body: {Body}", JsonSerializer.Serialize(requestBody));

// ✅ Good - only serializes if debug logging is enabled
if (logger.IsEnabled(LogLevel.Debug))
{
    logger.LogDebug("Request body: {Body}", JsonSerializer.Serialize(requestBody));
}
```

### Use Sampling for High-Volume Logs

For very high-frequency operations (e.g., tile requests), use sampling:

```csharp
// Log only 10% of tile requests
if (Random.Shared.Next(100) < 10)
{
    logger.LogInformation("Tile request: {Z}/{X}/{Y}", z, x, y);
}
```

## Examples

### Example: Successful Operation

```csharp
logger.LogInformation(
    "Feature collection retrieved successfully - Layer: {LayerId}, Count: {FeatureCount}, Duration: {DurationMs}ms",
    layerId, featureCount, durationMs);
```

### Example: Warning with Context

```csharp
logger.LogWarning(
    "Cache invalidation retry {Attempt}/{MaxAttempts} for key {CacheKey} - Error: {ErrorMessage}",
    attempt, maxAttempts, cacheKey, errorMessage);
```

### Example: Error with Exception

```csharp
try
{
    await ProcessGeoprocessingJob(jobId);
}
catch (Exception ex)
{
    logger.LogError(ex,
        "Geoprocessing job {JobId} failed - JobType: {JobType}, Attempt: {Attempt}",
        jobId, jobType, attemptCount);
    throw;
}
```

### Example: Using Scopes for Context

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["LayerId"] = layerId,
    ["UserId"] = userId,
    ["TenantId"] = tenantId
}))
{
    logger.LogInformation("Starting feature export");
    // All logs in this scope will include LayerId, UserId, TenantId
    await ExportFeatures();
    logger.LogInformation("Feature export completed");
}
```

### Example: Custom Log Extensions

Create domain-specific logging extensions for consistency:

```csharp
public static class GeoprocessingLogExtensions
{
    public static void LogGeoprocessingJobStarted(
        this ILogger logger,
        string jobId,
        string jobType,
        string userId)
    {
        logger.LogInformation(
            "Geoprocessing job started - JobId: {JobId}, JobType: {JobType}, User: {UserId}",
            jobId, jobType, userId);
    }

    public static void LogGeoprocessingJobCompleted(
        this ILogger logger,
        string jobId,
        long durationMs,
        long processedRecords)
    {
        logger.LogInformation(
            "Geoprocessing job completed - JobId: {JobId}, Duration: {DurationMs}ms, Records: {RecordCount}",
            jobId, durationMs, processedRecords);
    }
}
```

## Integration with OpenTelemetry

All structured logs are automatically exported to OpenTelemetry when configured. Log fields become span attributes in traces, enabling correlation between logs, metrics, and traces.

### Configuring Log Export

In `appsettings.json`:

```json
{
  "observability": {
    "logging": {
      "exporter": "otlp",
      "otlpEndpoint": "http://otel-collector:4317",
      "otlpHeaders": "x-api-key=your-api-key"
    }
  }
}
```

## Checklist

Before committing code with logging:

- [ ] Used structured logging with named parameters
- [ ] Selected appropriate log level
- [ ] Included sufficient context for debugging
- [ ] Did NOT log sensitive data (passwords, tokens, PII)
- [ ] Redacted any potentially sensitive information
- [ ] Used correlation IDs for cross-service tracking
- [ ] Considered performance impact for high-frequency logs
- [ ] Verified logs work well with OpenTelemetry exporters
