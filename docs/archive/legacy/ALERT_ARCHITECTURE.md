# Alert Architecture

## Overview

Honua provides a **decoupled, multi-source alert architecture** that is **not tied to Prometheus**. Alerts can come from multiple sources and are routed through a central Alert Receiver service to various notification providers.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Alert Sources                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  Prometheus  │  │ Application  │  │   Serilog    │          │
│  │ AlertManager │  │  Code (API)  │  │  Log Sink    │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                  │                  │                   │
│         │ Webhook          │ HTTP POST        │ HTTP POST        │
│         │                  │                  │                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ Health Check │  │  CloudWatch  │  │    Custom    │          │
│  │   Failures   │  │     Logs     │  │   Scripts    │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                  │                  │                   │
└─────────┼──────────────────┼──────────────────┼───────────────────┘
          │                  │                  │
          │                  │                  │
          ▼                  ▼                  ▼
    ┌─────────────────────────────────────────────────┐
    │         Alert Receiver Service                   │
    │                                                   │
    │  ┌────────────────┐    ┌──────────────────┐    │
    │  │  AlertManager  │    │  Generic Alert   │    │
    │  │    Webhook     │    │       API        │    │
    │  │   /alert/*     │    │   /api/alerts    │    │
    │  └────────┬───────┘    └────────┬─────────┘    │
    │           │                      │               │
    │           └──────────┬───────────┘               │
    │                      │                           │
    │              ┌───────▼────────┐                  │
    │              │ Alert Adapter  │                  │
    │              └───────┬────────┘                  │
    │                      │                           │
    │              ┌───────▼────────┐                  │
    │              │   Composite    │                  │
    │              │   Publisher    │                  │
    │              └───────┬────────┘                  │
    └──────────────────────┼───────────────────────────┘
                           │
         ┌─────────────────┼─────────────────┐
         │                 │                 │
         ▼                 ▼                 ▼
   ┌──────────┐      ┌──────────┐      ┌──────────┐
   │ AWS SNS  │      │  Azure   │      │PagerDuty │
   └──────────┘      │  Events  │      └──────────┘
                     └──────────┘
         ▼                 ▼                 ▼
   ┌──────────┐      ┌──────────┐      ┌──────────┐
   │  Slack   │      │  Teams   │      │ Opsgenie │
   └──────────┘      └──────────┘      └──────────┘
```

## Alert Sources

### 1. Prometheus/AlertManager (Optional)
Traditional metrics-based alerting.

**Flow:**
- Prometheus evaluates alert rules
- AlertManager groups and routes alerts
- Sends webhook to `/alert/{severity}`

**Use Cases:**
- Resource-based alerts (CPU, memory, disk)
- Query-based thresholds (error rate > 5%)
- Time-series anomalies

### 2. Application Code (Direct)
Send alerts directly from your application code.

**Flow:**
- Application calls `IAlertClient.SendAlertAsync()`
- Alert sent via HTTP POST to `/api/alerts`
- Immediate delivery to all providers

**Use Cases:**
- Business logic errors
- Payment failures
- Data integrity issues
- Authentication failures

**Example:**
```csharp
public class PaymentService
{
    private readonly IAlertClient _alertClient;

    public async Task ProcessPayment(Payment payment)
    {
        try
        {
            await _paymentGateway.Charge(payment);
        }
        catch (PaymentDeclinedException ex)
        {
            // Send alert immediately
            await _alertClient.SendCriticalAlertAsync(
                name: "PaymentDeclined",
                description: $"Payment declined for order {payment.OrderId}: {ex.Message}",
                labels: new Dictionary<string, string>
                {
                    ["order_id"] = payment.OrderId,
                    ["amount"] = payment.Amount.ToString(),
                    ["customer_id"] = payment.CustomerId
                });

            throw;
        }
    }
}
```

### 3. Serilog Sink (Automatic)
Automatically send Error/Fatal logs as alerts.

**Flow:**
- Application logs error/fatal message
- Serilog sink intercepts log event
- Converts to alert and sends to `/api/alerts`

**Use Cases:**
- Unhandled exceptions
- Database connection failures
- Configuration errors
- External API failures

**Configuration:**
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.AlertReceiver(
        alertReceiverUrl: "http://alert-receiver:8080",
        alertReceiverToken: "your-token",
        environment: "production",
        serviceName: "honua-api",
        minimumLevel: LogEventLevel.Error)
    .CreateLogger();
```

Now any error log automatically triggers an alert:
```csharp
_logger.LogError(ex, "Failed to connect to database");
// → Automatically sends alert to all providers
```

### 4. Health Check Failures
Monitor service health and alert on failures.

**Flow:**
- Health check endpoint fails
- Monitoring system POSTs to `/api/alerts`
- Alert sent to providers

**Example:**
```bash
# From monitoring script
curl -X POST http://alert-receiver:8080/api/alerts \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "HealthCheckFailed",
    "severity": "critical",
    "description": "Database health check failed",
    "source": "health-monitor",
    "service": "honua-api"
  }'
```

### 5. CloudWatch Logs
Alert based on log patterns in CloudWatch.

**Flow:**
- CloudWatch Logs filter matches pattern
- Triggers SNS → Lambda
- Lambda POSTs to `/api/alerts`

**Example Lambda:**
```javascript
export const handler = async (event) => {
  const alert = {
    name: "HighErrorRate",
    severity: "critical",
    description: "Error rate exceeded threshold",
    source: "cloudwatch-logs"
  };

  await fetch('http://alert-receiver:8080/api/alerts', {
    method: 'POST',
    headers: {
      'Authorization': 'Bearer ' + process.env.ALERT_TOKEN,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(alert)
  });
};
```

### 6. Custom Scripts
Send alerts from any script or tool.

**Examples:**

**Backup failure:**
```bash
#!/bin/bash
if ! /usr/local/bin/backup.sh; then
  curl -X POST http://alert-receiver:8080/api/alerts \
    -H "Authorization: Bearer $ALERT_TOKEN" \
    -d '{
      "name": "BackupFailed",
      "severity": "high",
      "description": "Database backup failed",
      "source": "backup-script"
    }'
fi
```

**Deployment notification:**
```bash
#!/bin/bash
curl -X POST http://alert-receiver:8080/api/alerts \
  -H "Authorization: Bearer $ALERT_TOKEN" \
  -d '{
    "name": "DeploymentStarted",
    "severity": "info",
    "description": "Deploying version v1.2.3 to production",
    "source": "deployment-pipeline",
    "labels": {"version": "v1.2.3"}
  }'
```

## Alert Formats

### Generic Alert Format
Simple, source-agnostic format for any alert.

```json
{
  "name": "DatabaseConnectionFailed",
  "severity": "critical",
  "status": "firing",
  "summary": "Cannot connect to PostgreSQL",
  "description": "Database connection pool exhausted after 30s timeout",
  "source": "honua-api",
  "service": "database-layer",
  "environment": "production",
  "labels": {
    "database": "postgresql",
    "host": "db.example.com"
  },
  "context": {
    "connection_pool_size": 100,
    "active_connections": 100,
    "timeout_seconds": 30
  },
  "timestamp": "2024-01-01T12:00:00Z",
  "fingerprint": "abc123..."
}
```

**Fields:**
- `name` (required): Alert name
- `severity` (required): critical | high | medium | low | info
- `status`: firing | resolved
- `summary`: Short description
- `description`: Detailed description
- `source`: Where the alert came from
- `service`: Service/component identifier
- `environment`: production | staging | dev
- `labels`: Key-value tags
- `context`: Additional structured data
- `timestamp`: When alert occurred
- `fingerprint`: For deduplication (auto-generated if not provided)

### AlertManager Webhook Format
Traditional Prometheus format (still supported).

```json
{
  "version": "4",
  "status": "firing",
  "alerts": [{
    "labels": {
      "alertname": "HonuaServiceDown",
      "severity": "critical"
    },
    "annotations": {
      "description": "Service down for 1 minute"
    },
    "startsAt": "2024-01-01T12:00:00Z"
  }]
}
```

## Decoupling Benefits

### 1. No Prometheus Required
- Don't need to run Prometheus/AlertManager for alerting
- Can use lightweight, immediate alerting from application code
- Reduces infrastructure complexity for small deployments

### 2. Multiple Alert Sources
- Combine metrics-based (Prometheus) + event-based (application) alerts
- Single pipeline for all alert types
- Consistent notification delivery

### 3. Immediate Alerting
- Application errors → instant notification (no Prometheus scrape interval)
- Business logic failures → real-time alerts
- No dependency on metrics collection

### 4. Flexibility
- Use Prometheus for resource/performance monitoring
- Use direct API for business/application alerts
- Use Serilog sink for automatic error alerting
- Mix and match based on needs

## Deployment Strategies

### Minimal (No Prometheus)
```yaml
services:
  honua-api:
    environment:
      - Alerts:ReceiverUrl=http://alert-receiver:8080
      - Alerts:ReceiverToken=your-token

  alert-receiver:
    environment:
      - Alerts:Slack:CriticalWebhookUrl=https://hooks.slack.com/...
```

**Alerts via:**
- Application code (IAlertClient)
- Serilog sink (automatic)
- Health checks

### Standard (Prometheus + Direct)
```yaml
services:
  prometheus:
    # Metrics collection + rule evaluation

  alertmanager:
    # Prometheus alerts → webhook

  honua-api:
    # Direct alerts from code

  alert-receiver:
    # Receives from both sources
```

**Alerts via:**
- Prometheus (resource/performance)
- Application code (business logic)
- Serilog sink (errors)

### Enterprise (All Sources)
```yaml
services:
  prometheus:
    # Metrics-based alerting

  alertmanager:
    # Prometheus routing

  honua-api:
    # Direct application alerts

  alert-receiver:
    # Multi-provider delivery

  # Plus:
  # - CloudWatch integration
  # - Custom scripts
  # - Health check monitors
```

## Configuration Examples

### Application Configuration
```json
{
  "Alerts": {
    "ReceiverUrl": "http://alert-receiver:8080",
    "ReceiverToken": "your-secure-token"
  },
  "ServiceName": "honua-api",
  "Environment": "production"
}
```

### Dependency Injection
```csharp
// In Startup.cs / Program.cs
builder.Services.AddHttpClient("AlertReceiver");
builder.Services.AddSingleton<IAlertClient, AlertClient>();

// In your services
public class MyService
{
    private readonly IAlertClient _alertClient;

    public MyService(IAlertClient alertClient)
    {
        _alertClient = alertClient;
    }

    public async Task DoSomething()
    {
        try
        {
            // Your code
        }
        catch (CriticalException ex)
        {
            await _alertClient.SendCriticalAlertAsync(
                "CriticalOperationFailed",
                ex.Message);
            throw;
        }
    }
}
```

### Serilog Configuration
```csharp
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .WriteTo.Console()
    .WriteTo.AlertReceiver(
        alertReceiverUrl: configuration["Alerts:ReceiverUrl"],
        alertReceiverToken: configuration["Alerts:ReceiverToken"],
        environment: configuration["Environment"],
        serviceName: configuration["ServiceName"],
        minimumLevel: LogEventLevel.Error)
    .CreateLogger();
```

## Best Practices

### 1. Source Selection
- **Prometheus**: Resource utilization, performance trends
- **Direct API**: Business logic errors, critical operations
- **Serilog Sink**: Unhandled exceptions, infrastructure failures
- **Health Checks**: Service availability monitoring

### 2. Severity Mapping
- **critical**: Service down, data loss, security breach
- **high**: Major functionality broken, payment failures
- **medium**: Degraded performance, non-critical errors
- **low**: Warnings, informational events

### 3. Deduplication
- Use consistent `fingerprint` for related alerts
- Alert Receiver automatically deduplicates by fingerprint
- PagerDuty and Opsgenie also deduplicate by fingerprint

### 4. Performance
- Alert sending is fire-and-forget with short timeout
- Won't block application threads
- Failed alerts are logged but don't fail operations

## Migration Path

### From Prometheus-Only
```
Before: App → Prometheus → AlertManager → SNS
After:  App → Alert Receiver → Multiple Providers
        Prometheus → AlertManager → Alert Receiver → Multiple Providers
```

### Hybrid Approach
Keep Prometheus for infrastructure monitoring, add direct alerts for application-specific issues:

```csharp
// Infrastructure: Let Prometheus handle
// - CPU/memory/disk usage
// - Request rates and latencies
// - Database connection pools

// Application: Send direct alerts
await _alertClient.SendCriticalAlertAsync(
    "PaymentProcessingFailed",
    $"Failed to process ${amount} payment");
```

## Summary

The decoupled alert architecture provides:

✅ **Flexibility**: Not tied to Prometheus
✅ **Multiple Sources**: Metrics, logs, code, scripts
✅ **Immediate Delivery**: No scrape interval delays
✅ **Multi-Provider**: AWS, Azure, PagerDuty, Slack, Teams, Opsgenie
✅ **Simple API**: POST JSON to send alert
✅ **Automatic Logging**: Serilog sink for hands-free alerting
✅ **Scalable**: From minimal to enterprise deployments
