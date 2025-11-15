# Configuration Strategy: Performance vs. Simplicity

## Design Principles

### 1. **Sensible Defaults** (Zero-Config Startup)
Honua.Server should work out-of-the-box with just a database connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "postgresql://localhost/honua"
  }
}
```

Everything else is optional.

### 2. **Progressive Enhancement**
Add complexity only when needed:
- **Tier 1**: No configuration needed (in-memory everything)
- **Tier 2**: Add Redis connection string (distributed caching + locks)
- **Tier 3**: Add message queue, replicas, CDN (enterprise features)

### 3. **Explicit Tradeoffs**
Configuration should clearly document tradeoffs:

```json
{
  "BackgroundJobs": {
    "Mode": "Polling",  // Simple: No dependencies
                        // Tradeoff: Less reliable, wastes CPU polling

    // "Mode": "MessageQueue",  // Complex: Requires SQS/ServiceBus/RabbitMQ
                                 // Benefit: Reliable, efficient, auto-retry
  }
}
```

---

## Implementation: Configuration Options

### **Example: WFS Lock Manager**

```csharp
// File: /src/Honua.Server.Core/Configuration/WfsOptions.cs
public class WfsOptions
{
    /// <summary>
    /// Lock manager implementation.
    /// - "InMemory": Simple, no dependencies. ⚠️ Single instance only.
    /// - "Redis": Distributed locks for multi-instance deployments.
    /// Default: "InMemory"
    /// </summary>
    public WfsLockManagerType LockManager { get; set; } = WfsLockManagerType.InMemory;

    /// <summary>
    /// Redis connection string (required if LockManager = Redis)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Maximum lock duration before automatic release.
    /// Default: 5 minutes
    /// </summary>
    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

public enum WfsLockManagerType
{
    /// <summary>
    /// In-memory locks (default). Fast, no dependencies.
    /// ⚠️ WARNING: Only works with single instance deployments.
    /// Multi-instance deployments will have race conditions.
    /// </summary>
    InMemory,

    /// <summary>
    /// Redis-based distributed locks. Required for multi-instance HA.
    /// Requires: Redis 6.0+ connection string in WfsOptions.RedisConnectionString
    /// </summary>
    Redis
}

// Service registration:
public static IServiceCollection AddWfsServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var options = configuration.GetSection("WFS").Get<WfsOptions>() ?? new WfsOptions();

    services.AddSingleton(options);

    // Register appropriate implementation based on configuration
    services.AddSingleton<IWfsLockManager>(sp =>
    {
        return options.LockManager switch
        {
            WfsLockManagerType.InMemory => new InMemoryWfsLockManager(),

            WfsLockManagerType.Redis => new RedisWfsLockManager(
                options.RedisConnectionString
                    ?? throw new InvalidOperationException(
                        "WFS.RedisConnectionString is required when LockManager = Redis")),

            _ => throw new NotSupportedException($"Unsupported lock manager: {options.LockManager}")
        };
    });

    return services;
}

// Startup validation:
public class WfsConfigurationValidator : IStartupValidator
{
    public ValidationResult Validate(WfsOptions options, DeploymentInfo deployment)
    {
        if (options.LockManager == WfsLockManagerType.InMemory
            && deployment.InstanceCount > 1)
        {
            return ValidationResult.Warning(
                "WFS.LockManager='InMemory' with multiple instances detected. " +
                "WFS-T transactions may encounter race conditions. " +
                "Recommendation: Set WFS.LockManager='Redis' for multi-instance deployments.");
        }

        if (options.LockManager == WfsLockManagerType.Redis
            && string.IsNullOrEmpty(options.RedisConnectionString))
        {
            return ValidationResult.Error(
                "WFS.LockManager='Redis' requires WFS.RedisConnectionString to be set.");
        }

        return ValidationResult.Success();
    }
}
```

**Configuration:**

```json
// Tier 1: Simple (default)
{
  "WFS": {
    "LockManager": "InMemory"
    // No other config needed
  }
}

// Tier 2+: Multi-instance
{
  "WFS": {
    "LockManager": "Redis",
    "RedisConnectionString": "redis:6379",
    "LockTimeout": "00:05:00"
  }
}
```

---

### **Example: Background Jobs**

```csharp
// File: /src/Honua.Server.Core/Configuration/BackgroundJobOptions.cs
public class BackgroundJobOptions
{
    /// <summary>
    /// Background job execution mode.
    /// - "Polling": Simple, uses PostgreSQL queue (default)
    ///   Pros: No dependencies, easy to debug
    ///   Cons: Wastes CPU polling, less reliable on failures
    ///
    /// - "MessageQueue": Enterprise, uses AWS SQS / Azure Service Bus / RabbitMQ
    ///   Pros: Reliable, efficient, auto-retry, dead-letter queue
    ///   Cons: Requires external message broker
    ///
    /// Default: "Polling"
    /// </summary>
    public BackgroundJobMode Mode { get; set; } = BackgroundJobMode.Polling;

    /// <summary>
    /// Polling mode: How often to check for new jobs (default: 5 seconds)
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Message queue provider (required if Mode = MessageQueue)
    /// </summary>
    public MessageQueueProvider? Provider { get; set; }

    /// <summary>
    /// Message queue configuration (provider-specific)
    /// </summary>
    public MessageQueueConfig? Queue { get; set; }

    /// <summary>
    /// Enable idempotency protection (recommended for MessageQueue mode)
    /// Prevents duplicate job execution if worker crashes
    /// Default: false (Polling mode), true (MessageQueue mode)
    /// </summary>
    public bool EnableIdempotency { get; set; }
}

public enum BackgroundJobMode
{
    /// <summary>
    /// PostgreSQL-based job queue with polling (default).
    /// Simple, no external dependencies.
    /// </summary>
    Polling,

    /// <summary>
    /// Message queue (AWS SQS, Azure Service Bus, RabbitMQ).
    /// Reliable, efficient, recommended for production.
    /// </summary>
    MessageQueue
}

public enum MessageQueueProvider
{
    AwsSqs,
    AzureServiceBus,
    RabbitMq
}

public class MessageQueueConfig
{
    public string QueueUrl { get; set; } = string.Empty;
    public int MaxConcurrency { get; set; } = 5;
    public TimeSpan VisibilityTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public string? DeadLetterQueue { get; set; }
}
```

**Configuration:**

```json
// Tier 1: Simple (default)
{
  "BackgroundJobs": {
    "Mode": "Polling",
    "PollingInterval": "00:00:05"
  }
}

// Tier 3: Enterprise
{
  "BackgroundJobs": {
    "Mode": "MessageQueue",
    "Provider": "AwsSqs",
    "Queue": {
      "QueueUrl": "https://sqs.us-east-1.amazonaws.com/123456/honua-jobs",
      "MaxConcurrency": 10,
      "VisibilityTimeout": "00:05:00",
      "DeadLetterQueue": "honua-jobs-dlq"
    },
    "EnableIdempotency": true
  }
}
```

---

### **Example: Database Read Replicas**

```csharp
// File: /src/Honua.Server.Core/Configuration/DatabaseOptions.cs
public class DatabaseOptions
{
    /// <summary>
    /// Primary database connection (required)
    /// </summary>
    public string PrimaryConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Read replica connection strings (optional)
    /// If configured, read-heavy operations will be routed to replicas.
    /// </summary>
    public List<string> ReadReplicaConnectionStrings { get; set; } = new();

    /// <summary>
    /// Enable automatic read replica routing.
    /// Default: false (disabled unless replicas configured)
    /// </summary>
    public bool EnableReadReplicaRouting { get; set; } = false;

    /// <summary>
    /// Operations to route to read replicas (when enabled)
    /// Default: Features, Observations, Tiles
    /// </summary>
    public List<string> ReadReplicaOperations { get; set; } = new()
    {
        "Features",      // OGC Features queries
        "Observations",  // SensorThings observations
        "Tiles"          // MVT tile generation
    };

    /// <summary>
    /// Fallback to primary if all replicas unavailable.
    /// Default: true (prefer availability over perfect load distribution)
    /// </summary>
    public bool FallbackToPrimary { get; set; } = true;

    /// <summary>
    /// Connection pool configuration
    /// </summary>
    public ConnectionPoolOptions ConnectionPool { get; set; } = new();
}

public class ConnectionPoolOptions
{
    /// <summary>
    /// Maximum pool size.
    /// Default: 50 (Tier 1), auto-scales based on CPU cores if AutoScale=true
    /// Recommended: 10-20 per CPU core for production
    /// </summary>
    public int MaxSize { get; set; } = 50;

    /// <summary>
    /// Automatically scale MaxSize based on CPU cores.
    /// Formula: MaxSize = Environment.ProcessorCount * ScaleFactor
    /// Default: false
    /// </summary>
    public bool AutoScale { get; set; } = false;

    /// <summary>
    /// Scale factor when AutoScale is enabled.
    /// Default: 15 (15 connections per CPU core)
    /// </summary>
    public int ScaleFactor { get; set; } = 15;
}

// Service registration:
public static IServiceCollection AddDatabaseServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var options = configuration.GetSection("Database").Get<DatabaseOptions>()
        ?? new DatabaseOptions();

    // Auto-scale pool size if enabled
    if (options.ConnectionPool.AutoScale)
    {
        options.ConnectionPool.MaxSize =
            Environment.ProcessorCount * options.ConnectionPool.ScaleFactor;
    }

    services.AddSingleton(options);

    // Register data source provider
    if (options.EnableReadReplicaRouting && options.ReadReplicaConnectionStrings.Any())
    {
        services.AddSingleton<IDataStoreProvider, ReplicaAwareDataStoreProvider>();
    }
    else
    {
        services.AddSingleton<IDataStoreProvider, PostgresDataStoreProvider>();
    }

    return services;
}
```

**Configuration:**

```json
// Tier 1: Simple (default)
{
  "ConnectionStrings": {
    "DefaultConnection": "postgresql://localhost/honua"
  }
  // Database options use defaults
}

// Tier 2: Standard with auto-scaling
{
  "Database": {
    "PrimaryConnectionString": "postgresql://primary:5432/honua",
    "ConnectionPool": {
      "AutoScale": true,        // 120 connections on 8-core server
      "ScaleFactor": 15
    }
  }
}

// Tier 3: Enterprise with read replicas
{
  "Database": {
    "PrimaryConnectionString": "postgresql://primary:5432/honua",
    "ReadReplicaConnectionStrings": [
      "postgresql://replica1:5432/honua",
      "postgresql://replica2:5432/honua"
    ],
    "EnableReadReplicaRouting": true,
    "ReadReplicaOperations": ["Features", "Observations", "Tiles"],
    "FallbackToPrimary": true,
    "ConnectionPool": {
      "AutoScale": true,
      "ScaleFactor": 15
    }
  }
}
```

---

### **Example: SLI/SLO Configuration**

```csharp
// File: /src/Honua.Server.Core/Configuration/SreOptions.cs
public class SreOptions
{
    /// <summary>
    /// Enable SRE features (SLIs, SLOs, error budgets).
    /// Default: false
    ///
    /// Recommended for:
    /// - Tier 3 (Enterprise) deployments
    /// - SLA-backed services
    /// - Organizations with SRE practices
    ///
    /// Not needed for:
    /// - Development environments
    /// - Small deployments
    /// - Teams without SRE expertise
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Service Level Objectives (user-defined targets)
    /// </summary>
    public Dictionary<string, SloConfig> SLOs { get; set; } = new();

    /// <summary>
    /// Error budget configuration
    /// </summary>
    public ErrorBudgetOptions ErrorBudget { get; set; } = new();
}

public class SloConfig
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// SLO target (0.0 - 1.0)
    /// Examples:
    /// - 0.90 = 90% (relaxed)
    /// - 0.95 = 95% (standard)
    /// - 0.99 = 99% (high)
    /// - 0.999 = 99.9% (very high)
    /// - 0.9999 = 99.99% (enterprise)
    /// </summary>
    public double Target { get; set; }

    /// <summary>
    /// Measurement window (rolling window for compliance calculation)
    /// Default: 28 days (Google SRE recommendation)
    /// </summary>
    public TimeSpan MeasurementWindow { get; set; } = TimeSpan.FromDays(28);

    /// <summary>
    /// SLI-specific thresholds (vary by SLI type)
    /// </summary>
    public Dictionary<string, object> Thresholds { get; set; } = new();
}

public class ErrorBudgetOptions
{
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Webhook URL to notify when error budget is low/exhausted
    /// </summary>
    public string? AlertWebhook { get; set; }

    /// <summary>
    /// Enable deployment gating based on error budget.
    /// If true, deployments are blocked when budget exhausted.
    /// Default: false
    /// </summary>
    public bool DeploymentGating { get; set; } = false;

    /// <summary>
    /// Minimum error budget remaining to allow deployments (0.0 - 1.0)
    /// Default: 0.25 (25% budget remaining)
    /// </summary>
    public double MinimumBudgetRemaining { get; set; } = 0.25;
}
```

**Configuration:**

```json
// Tier 1 & 2: Disabled (default)
{
  "SRE": {
    "Enabled": false
  }
}

// Tier 3: Enterprise with SLOs
{
  "SRE": {
    "Enabled": true,

    "SLOs": {
      "LatencySLO": {
        "Enabled": true,
        "Target": 0.99,              // 99% of requests < 500ms
        "MeasurementWindow": "28.00:00:00",
        "Thresholds": {
          "ResponseTimeMs": 500
        }
      },

      "AvailabilitySLO": {
        "Enabled": true,
        "Target": 0.999,             // 99.9% uptime (operators define based on their needs)
        "MeasurementWindow": "28.00:00:00"
      },

      "ErrorRateSLO": {
        "Enabled": true,
        "Target": 0.999,             // 99.9% success rate (5xx errors only)
        "MeasurementWindow": "28.00:00:00"
      }
    },

    "ErrorBudget": {
      "Enabled": true,
      "AlertWebhook": "https://alerts.example.com/honua-error-budget",
      "DeploymentGating": true,
      "MinimumBudgetRemaining": 0.25   // Block deploys if <25% budget remains
    }
  }
}
```

---

## Startup Validation & Warnings

Honua.Server validates configuration at startup and provides actionable warnings:

```csharp
// File: /src/Honua.Server.Host/Validation/ConfigurationValidator.cs
public class ConfigurationValidator : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        var deployment = DetermineDeploymentTier();

        ValidateWfsConfiguration(deployment);
        ValidateBackgroundJobConfiguration(deployment);
        ValidateDatabaseConfiguration(deployment);
        ValidateSreConfiguration(deployment);

        return Task.CompletedTask;
    }

    private void ValidateWfsConfiguration(DeploymentInfo deployment)
    {
        if (_wfsOptions.LockManager == WfsLockManagerType.InMemory
            && deployment.InstanceCount > 1)
        {
            _logger.LogWarning(
                "⚠️  WFS.LockManager='InMemory' with {InstanceCount} instances detected.\n" +
                "    WFS-T transactions may encounter race conditions.\n" +
                "    Recommendation: Set WFS.LockManager='Redis' or reduce to single instance.\n" +
                "    See: https://docs.honua.io/wfs-locking",
                deployment.InstanceCount);
        }
    }

    private void ValidateBackgroundJobConfiguration(DeploymentInfo deployment)
    {
        if (_jobOptions.Mode == BackgroundJobMode.Polling
            && deployment.Tier >= DeploymentTier.Enterprise)
        {
            _logger.LogInformation(
                "ℹ️  BackgroundJobs.Mode='Polling' in Tier 3 deployment.\n" +
                "    Consider upgrading to MessageQueue for better reliability and efficiency.\n" +
                "    Benefit: Auto-retry, dead-letter queue, no polling overhead.\n" +
                "    See: https://docs.honua.io/background-jobs");
        }
    }

    private void ValidateSreConfiguration(DeploymentInfo deployment)
    {
        if (!_sreOptions.Enabled)
        {
            _logger.LogInformation(
                "ℹ️  SRE.Enabled=false. SLO tracking and error budgets disabled.\n" +
                "    This is normal for Tier 1-2 deployments.\n" +
                "    Enable for SLA-backed services or enterprise deployments.\n" +
                "    See: https://docs.honua.io/sre-configuration");
        }
    }
}
```

**Example Startup Output:**

```
[INFO] Honua.Server starting in Tier 2 (Standard) mode
[INFO] Configuration:
       - WFS.LockManager: Redis ✓
       - BackgroundJobs.Mode: Polling ✓
       - Database.ReadReplicas: 0 (optional)
       - SRE.Enabled: false (optional)

[INFO] ℹ️  SRE.Enabled=false. SLO tracking and error budgets disabled.
       This is normal for Tier 1-2 deployments.

[INFO] All configuration validated successfully.
[INFO] Listening on http://localhost:5000
```

---

## Documentation Strategy

Each configuration option includes inline documentation with:

1. **What it does** (simple explanation)
2. **Tradeoffs** (pros/cons)
3. **Defaults** (sensible for 80% of users)
4. **When to use** (which deployment tier)
5. **Example** (copy-paste ready)

**Example:**

```csharp
/// <summary>
/// Background job execution mode.
///
/// OPTION 1: "Polling" (default, simple)
///   How it works: Checks PostgreSQL queue every 5 seconds for new jobs
///   Pros: No external dependencies, easy to debug
///   Cons: Wastes CPU polling, less reliable on worker crashes
///   Use when: Tier 1-2, job volume <1000/day, simplicity valued
///
/// OPTION 2: "MessageQueue" (enterprise, complex)
///   How it works: Uses AWS SQS / Azure Service Bus / RabbitMQ for job queue
///   Pros: Push-based (no polling), auto-retry, dead-letter queue, idempotency
///   Cons: Requires external message broker, more moving parts
///   Use when: Tier 3, job volume >1000/day, reliability critical
///
/// Default: "Polling"
/// </summary>
public BackgroundJobMode Mode { get; set; } = BackgroundJobMode.Polling;
```

---

## Philosophy Summary

### ✅ **DO:**
- Provide sensible defaults that work without configuration
- Make advanced features opt-in via configuration
- Clearly document tradeoffs in comments and docs
- Validate configuration and warn about potential issues
- Scale complexity with operational maturity

### ❌ **DON'T:**
- Require external dependencies for basic functionality
- Hide performance characteristics behind abstractions
- Assume all users have DevOps expertise
- Make configuration overly complex
- Force users into "one true architecture"

---

## Key Insight

> **"The best architecture is the one you can actually operate."**

A research lab running Honua.Server on a single VM shouldn't need to understand:
- Redis cluster operations
- Message queue semantics
- Read replica lag monitoring
- Error budget calculations

But an enterprise team with dedicated SRE should have access to all those features when they need them.

**Configuration makes this possible.**
