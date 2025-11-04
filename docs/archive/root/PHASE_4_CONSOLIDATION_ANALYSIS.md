# Phase 4: Infrastructure & Cross-Cutting Consolidation Analysis
**HonuaIO Codebase**
**Analysis Date: October 25, 2025**

## Executive Summary

Phase 4 identifies consolidation opportunities across infrastructure, utilities, and middleware—areas not covered in Phases 1-3 which focused on protocol implementations. This analysis reveals significant duplication in health checks, HTTP client patterns, configuration validation, caching, and alert publishing.

### Key Findings

**Total Opportunities Identified:** 52 distinct patterns
**Estimated Consolidation Potential:** 950-1,400 additional lines could be eliminated
**High-Impact Quick Wins:** 10 patterns with minimal coupling and immediate benefit
**Strategic Refactorings:** 8 patterns requiring careful API design but substantial long-term value

### Phases Overview
- **Phase 1-2 (Completed):** ~200 lines eliminated through GeoServices/OGC consolidation
- **Phase 3 (Completed):** ~1,200 lines eliminated across protocol implementations
- **Phase 4 (Current):** Cross-cutting infrastructure analysis
- **Expected Phase 4 Impact:** 950-1,400 additional lines elimination

---

## Part 1: Top 10 Highest-Impact Opportunities

### 1. CLOUD STORAGE HEALTH CHECKS - NEARLY IDENTICAL IMPLEMENTATIONS
**Status:** 3 duplicate implementations
**Files Affected:** 3 files
**Duplication Type:** 85-90% similar code with only client SDK differences

#### Problem Analysis

**Current Implementations:**

1. **AzureBlobHealthCheck** (142 lines)
   - `/src/Honua.Server.Host/Health/AzureBlobHealthCheck.cs`
   - Pattern: Check client configured → Get account info → Check test container

2. **S3HealthCheck** (129 lines)
   - `/src/Honua.Server.Host/Health/S3HealthCheck.cs`
   - Pattern: Check client configured → List buckets → Check test bucket

3. **GcsHealthCheck** (136 lines)
   - `/src/Honua.Server.Host/Health/GcsHealthCheck.cs`
   - Pattern: Check client configured → Get bucket → Check test bucket

**Core Pattern Duplication:**
```csharp
// Pattern repeated in all 3 files (90%+ identical):
protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(...)
{
    // 1. Check if client is null
    if (_client == null)
    {
        data["provider.configured"] = false;
        return HealthCheckResult.Healthy("Not configured (optional)");
    }

    data["provider.configured"] = true;

    // 2. Test basic connectivity
    await _client.TestConnectivity(); // SDK-specific

    // 3. If test bucket/container specified, verify access
    if (_testBucket.HasValue())
    {
        try
        {
            var exists = await _client.CheckBucketExists(); // SDK-specific
            data["provider.test_bucket_accessible"] = exists;
            if (!exists) return Degraded(...);
        }
        catch (ForbiddenException) { return Degraded("Access denied"); }
        catch (NotFoundException) { return Degraded("Not found"); }
    }

    return HealthCheckResult.Healthy(...);
}
```

**Consolidation Approach:**

```csharp
// Create abstract base: CloudStorageHealthCheckBase<TClient>
public abstract class CloudStorageHealthCheckBase<TClient> : HealthCheckBase
    where TClient : class
{
    private readonly TClient? _client;
    private readonly string? _testBucket;
    private readonly string _providerName;

    protected abstract Task TestConnectivityAsync(TClient client, CancellationToken ct);
    protected abstract Task<bool> BucketExistsAsync(TClient client, string bucket, CancellationToken ct);
    protected abstract Task<object> GetBucketMetadataAsync(TClient client, string bucket, CancellationToken ct);

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(...)
    {
        // Shared logic for all providers (90 lines)
        // Calls abstract methods for SDK-specific operations (10 lines)
    }
}

// Implementations become minimal:
public sealed class AzureBlobHealthCheck : CloudStorageHealthCheckBase<BlobServiceClient>
{
    protected override Task TestConnectivityAsync(...) =>
        client.GetAccountInfoAsync(ct);

    protected override async Task<bool> BucketExistsAsync(...) =>
        (await client.GetBlobContainerClient(bucket).ExistsAsync(ct)).Value;

    // ~30 lines total (vs 142 lines)
}
```

**Estimated Impact:**
- Lines to Consolidate: 250+ lines (eliminating ~80% duplication across 3 files)
- Files to Update: 3 files + 1 new base class
- Complexity: LOW (straightforward Template Method pattern)
- Priority: HIGH (high ROI, low risk)

**Benefits:**
- Consistent health check behavior across all cloud providers
- Easy to add new cloud storage providers (just 30 lines each)
- Centralized error handling and logging
- Single source of truth for health check metadata format

---

### 2. CONTROL PLANE API CLIENTS - HTTP OPERATION DUPLICATION
**Status:** Base class exists but individual clients still duplicate patterns
**Files Affected:** 8+ API client implementations
**Duplication Type:** Similar error handling, envelope unwrapping, endpoint building

#### Problem Analysis

**Current State:**
- `ControlPlaneApiClientBase` provides GET/POST/PATCH/DELETE primitives (323 lines)
- Individual clients (`DataIngestionApiClient`, `MigrationApiClient`, etc.) duplicate:
  - Envelope unwrapping logic (e.g., `JobEnvelope`, `JobsEnvelope`)
  - Error message building
  - Multi-part form data construction
  - Content-type resolution

**Duplication Examples:**

```csharp
// Pattern repeated in 5+ API clients:
// 1. Envelope unwrapping (repeated ~8 times)
var envelope = await GetAsync<JobEnvelope>(connection, endpoint, ct);
if (envelope?.Job is null)
{
    throw new InvalidOperationException("Control plane response did not include payload.");
}
return envelope.Job;

// 2. Multi-part form building (repeated 3 times)
private static MultipartFormDataContent BuildMultipartContent(...)
{
    var content = new MultipartFormDataContent();
    content.Add(new StringContent(serviceId), "serviceId");
    content.Add(new StringContent(layerId), "layerId");

    var stream = File.OpenRead(filePath);
    var fileContent = new StreamContent(stream);
    fileContent.Headers.ContentType = new MediaTypeHeaderValue(ResolveContentType(fileName));
    content.Add(fileContent, "file", fileName);

    return content;
}

// 3. Content-type resolution (repeated 3 times)
private static string ResolveContentType(string fileName) =>
    Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".geojson" => "application/geo+json",
        ".gpkg" => "application/geopackage+sqlite3",
        ...
    };
```

**Consolidation Opportunity:**

```csharp
// Extend ControlPlaneApiClientBase with helper methods:
public abstract class ControlPlaneApiClientBase
{
    // Existing GET/POST/PATCH/DELETE methods...

    // Add envelope unwrapping helper
    protected async Task<T> GetAndUnwrapAsync<TEnvelope, T>(
        ControlPlaneConnection connection,
        string endpoint,
        Func<TEnvelope, T?> extractor,
        CancellationToken ct)
    {
        var envelope = await GetAsync<TEnvelope>(connection, endpoint, ct);
        return extractor(envelope) ?? throw new InvalidOperationException("Missing payload");
    }

    // Add multi-part form helper
    protected static MultipartFormDataContent BuildFileUploadContent(
        string filePath,
        Dictionary<string, string> fields)
    {
        var content = new MultipartFormDataContent();
        foreach (var field in fields)
            content.Add(new StringContent(field.Value), field.Key);

        var fileName = Path.GetFileName(filePath);
        var stream = File.OpenRead(filePath);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            GeoDataContentTypeResolver.Resolve(fileName));
        content.Add(fileContent, "file", fileName);

        return content;
    }
}

// Create shared content-type resolver
public static class GeoDataContentTypeResolver
{
    private static readonly Dictionary<string, string> ExtensionMap = new()
    {
        [".json"] = "application/json",
        [".geojson"] = "application/geo+json",
        [".gpkg"] = "application/geopackage+sqlite3",
        [".zip"] = "application/zip",
        [".csv"] = "text/csv",
        [".shp"] = "application/x-shapefile",
        [".fgb"] = "application/flatgeobuf"
    };

    public static string Resolve(string fileName) =>
        ExtensionMap.GetValueOrDefault(
            Path.GetExtension(fileName).ToLowerInvariant(),
            "application/octet-stream");
}
```

**Estimated Impact:**
- Lines to Consolidate: 180+ lines across 8 files
- Files to Update: 8 API client files + extend base class
- Complexity: LOW
- Priority: HIGH

---

### 3. ALERT PUBLISHER WEBHOOK BASE CLASS - ALREADY GOOD, CAN BE IMPROVED
**Status:** Base class exists with excellent design, minor opportunities remain
**Files Affected:** 5 alert publishers
**Duplication Type:** Severity mapping, routing key resolution, per-alert iteration

#### Problem Analysis

**Current State:**
- `WebhookAlertPublisherBase` already consolidates 90% of webhook logic (185 lines) ✅
- Good use of Template Method pattern ✅
- Minimal coupling between publishers ✅

**Remaining Duplication:**

```csharp
// Pattern 1: Severity mapping (repeated in 4 publishers)
private static string MapSeverity(string severity) =>
    severity.ToLowerInvariant() switch
    {
        "critical" => "critical", // or "P1", "error", etc.
        "warning" => "warning",   // or "P3", "warning", etc.
        "database" => "error",    // or "P2", "error", etc.
        _ => "info"              // or "P4", "info", etc.
    };

// Pattern 2: Configuration key resolution (repeated in 3 publishers)
private string GetRoutingKey(string severity) =>
    Configuration[severity.ToLowerInvariant() switch
    {
        "critical" => "Alerts:Service:CriticalRoutingKey",
        "warning" => "Alerts:Service:WarningRoutingKey",
        _ => "Alerts:Service:DefaultRoutingKey"
    }] ?? string.Empty;

// Pattern 3: Per-alert iteration (repeated in 2 publishers)
public override async Task PublishAsync(...)
{
    foreach (var alert in webhook.Alerts)
    {
        if (alert.Status == "firing")
            await CreateAlert(alert, severity, ...);
        else if (alert.Status == "resolved")
            await CloseAlert(alert, ...);
    }
}
```

**Consolidation Opportunity:**

```csharp
// Add to WebhookAlertPublisherBase:
public abstract class WebhookAlertPublisherBase : IAlertPublisher
{
    // Existing methods...

    // Add severity mapper factory method
    protected virtual ISeverityMapper CreateSeverityMapper() =>
        new StandardSeverityMapper();

    // Add configuration resolver helper
    protected string GetConfigurationBySeverity(
        string configKeyPrefix,
        string severity,
        string? fallbackKey = null)
    {
        var key = $"{configKeyPrefix}:{severity.ToLowerInvariant() switch
        {
            "critical" => "Critical",
            "warning" => "Warning",
            "database" => "Database",
            _ => "Default"
        }}";

        return Configuration[key]
            ?? (fallbackKey != null ? Configuration[fallbackKey] : null)
            ?? string.Empty;
    }

    // Add per-alert iteration helper
    protected async Task PublishPerAlertAsync(
        AlertManagerWebhook webhook,
        string severity,
        Func<Alert, string, CancellationToken, Task> publishFiring,
        Func<Alert, string, CancellationToken, Task>? publishResolved = null,
        CancellationToken ct = default)
    {
        foreach (var alert in webhook.Alerts)
        {
            if (alert.Status == "firing")
                await publishFiring(alert, severity, ct);
            else if (alert.Status == "resolved" && publishResolved != null)
                await publishResolved(alert, severity, ct);
        }
    }
}

// Severity mapper interface
public interface ISeverityMapper
{
    string Map(string severity, string targetFormat);
}

// Standard implementation
public sealed class StandardSeverityMapper : ISeverityMapper
{
    private static readonly Dictionary<(string, string), string> Mappings = new()
    {
        [("critical", "pagerduty")] = "critical",
        [("critical", "opsgenie")] = "P1",
        [("warning", "pagerduty")] = "warning",
        [("warning", "opsgenie")] = "P3",
        // ... etc
    };

    public string Map(string severity, string targetFormat) =>
        Mappings.GetValueOrDefault(
            (severity.ToLowerInvariant(), targetFormat.ToLowerInvariant()),
            "info");
}
```

**Estimated Impact:**
- Lines to Consolidate: 80+ lines across 5 publishers
- Files to Update: 5 publishers + extend base class
- Complexity: LOW
- Priority: MEDIUM

**Note:** This is already one of the best-designed parts of the codebase. Only minor improvements needed.

---

### 4. EMBEDDING PROVIDER INITIALIZATION - HTTPCLIENT PATTERNS
**Status:** 6 providers with similar HttpClient setup
**Files Affected:** 6 embedding provider implementations
**Duplication Type:** HttpClient creation, timeout configuration, header setup

#### Problem Analysis

**Duplication Pattern:**

```csharp
// Repeated in 6 embedding providers:
public SomeEmbeddingProvider(LlmProviderOptions options, IHttpClientFactory? httpClientFactory = null)
{
    _options = options.Provider;

    // Pattern 1: HttpClient creation (repeated 6 times)
    if (httpClientFactory != null)
    {
        _httpClient = httpClientFactory.CreateClient("ProviderName");
        _httpClient.BaseAddress = new Uri("https://api.provider.com/");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }
    else
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.provider.com/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    // Pattern 2: API key validation and header setup (repeated 6 times)
    if (_options.ApiKey.IsNullOrWhiteSpace())
    {
        throw new InvalidOperationException(
            "API key is required. Set it in configuration or environment variable.");
    }

    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ApiKey}");
}
```

**Consolidation Opportunity:**

```csharp
// Create base class for embedding providers:
public abstract class EmbeddingProviderBase : IEmbeddingProvider
{
    protected readonly HttpClient HttpClient;
    protected readonly string ProviderName;

    protected EmbeddingProviderBase(
        string providerName,
        string baseUrl,
        string apiKey,
        IHttpClientFactory? httpClientFactory = null,
        TimeSpan? timeout = null)
    {
        ProviderName = providerName;

        HttpClient = CreateConfiguredHttpClient(
            providerName,
            baseUrl,
            apiKey,
            httpClientFactory,
            timeout ?? TimeSpan.FromSeconds(30));
    }

    private static HttpClient CreateConfiguredHttpClient(
        string providerName,
        string baseUrl,
        string apiKey,
        IHttpClientFactory? factory,
        TimeSpan timeout)
    {
        if (apiKey.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException(
                $"{providerName} API key is required. " +
                $"Set it in configuration (LlmProvider:{providerName}:ApiKey) " +
                $"or environment variable {providerName.ToUpperInvariant()}_API_KEY.");
        }

        var client = factory?.CreateClient(providerName) ?? new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = timeout;
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        return client;
    }

    // Shared availability check
    public virtual async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await GetEmbeddingAsync("test", ct);
            return response.Success;
        }
        catch
        {
            return false;
        }
    }

    // Abstract methods for providers to implement
    public abstract Task<EmbeddingResponse> GetEmbeddingAsync(
        string text,
        CancellationToken ct = default);
}

// Implementations become simpler:
public sealed class OpenAIEmbeddingProvider : EmbeddingProviderBase
{
    public OpenAIEmbeddingProvider(
        LlmProviderOptions options,
        IHttpClientFactory? httpClientFactory = null)
        : base(
            "OpenAI",
            "https://api.openai.com/v1/",
            options.OpenAI.ApiKey,
            httpClientFactory)
    {
        // Provider-specific initialization only
    }

    // Only implement embedding logic, not initialization boilerplate
}
```

**Estimated Impact:**
- Lines to Consolidate: 120+ lines across 6 providers
- Files to Update: 6 providers + 1 new base class
- Complexity: MEDIUM
- Priority: MEDIUM-HIGH

---

### 5. CONFIGURATION VALIDATORS - DUPLICATE VALIDATION PATTERNS
**Status:** 15+ validators with similar validation logic
**Files Affected:** 15+ validator files
**Duplication Type:** Range checks, string validation, cross-field validation

#### Problem Analysis

**Common Validation Patterns (Repeated 15+ times):**

```csharp
// Pattern 1: Required string validation (repeated 20+ times)
if (options.SomeString.IsNullOrWhiteSpace())
{
    failures.Add("SomeString is required. Set 'ConfigPath:SomeString'.");
}

// Pattern 2: Range validation (repeated 15+ times)
if (options.PageSize <= 0)
{
    failures.Add($"PageSize must be > 0. Current: {options.PageSize}.");
}

if (options.PageSize > 5000)
{
    failures.Add($"PageSize ({options.PageSize}) exceeds limit of 5000.");
}

// Pattern 3: Cross-field validation (repeated 10+ times)
if (options.MinValue > options.MaxValue)
{
    failures.Add($"MinValue ({options.MinValue}) cannot exceed MaxValue ({options.MaxValue}).");
}

// Pattern 4: Enum validation (repeated 8+ times)
var validValues = new[] { "option1", "option2", "option3" };
if (!validValues.Contains(options.SomeEnum, StringComparer.OrdinalIgnoreCase))
{
    failures.Add($"SomeEnum '{options.SomeEnum}' is invalid. Valid: {string.Join(", ", validValues)}.");
}

// Pattern 5: Connection string validation (repeated 5+ times)
if (!IsValidConnectionString(options.ConnectionString))
{
    failures.Add($"ConnectionString '{options.ConnectionString}' is invalid. Expected format: ...");
}
```

**Consolidation Opportunity:**

```csharp
// Create fluent validation builder:
public sealed class ValidationBuilder
{
    private readonly List<string> _failures = new();
    private readonly string _configPath;

    public ValidationBuilder(string configPath)
    {
        _configPath = configPath;
    }

    public ValidationBuilder Required(string? value, string propertyName)
    {
        if (value.IsNullOrWhiteSpace())
            _failures.Add($"{propertyName} is required. Set '{_configPath}:{propertyName}'.");
        return this;
    }

    public ValidationBuilder Range(
        int value,
        string propertyName,
        int? min = null,
        int? max = null)
    {
        if (min.HasValue && value < min.Value)
            _failures.Add($"{propertyName} must be >= {min}. Current: {value}.");
        if (max.HasValue && value > max.Value)
            _failures.Add($"{propertyName} ({value}) exceeds limit of {max}.");
        return this;
    }

    public ValidationBuilder Enum(
        string value,
        string propertyName,
        params string[] validValues)
    {
        if (!validValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            _failures.Add(
                $"{propertyName} '{value}' is invalid. " +
                $"Valid: {string.Join(", ", validValues)}.");
        }
        return this;
    }

    public ValidationBuilder CrossField<T>(
        T value1,
        T value2,
        string prop1,
        string prop2,
        Func<T, T, bool> comparison,
        string message) where T : IComparable<T>
    {
        if (!comparison(value1, value2))
            _failures.Add(message);
        return this;
    }

    public ValidationBuilder ConnectionString(
        string value,
        string propertyName,
        ConnectionStringType type)
    {
        if (!ConnectionStringValidator.Validate(value, type, out var error))
            _failures.Add($"{propertyName} is invalid: {error}");
        return this;
    }

    public ValidateOptionsResult Build() =>
        _failures.Count > 0
            ? ValidateOptionsResult.Fail(_failures)
            : ValidateOptionsResult.Success;
}

// Usage in validators:
public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        return new ValidationBuilder("Redis")
            .Required(options.ConnectionString, "ConnectionString")
            .ConnectionString(options.ConnectionString, "ConnectionString", ConnectionStringType.Redis)
            .Required(options.KeyPrefix, "KeyPrefix")
            .Range(options.TtlSeconds, "TtlSeconds", min: 1, max: 2_592_000)
            .Range(options.ConnectTimeoutMs, "ConnectTimeoutMs", min: 1, max: 60_000)
            .Range(options.SyncTimeoutMs, "SyncTimeoutMs", min: 1, max: 30_000)
            .Build();
    }
}
```

**Estimated Impact:**
- Lines to Consolidate: 300+ lines across 15+ validators
- Files to Update: 15+ validators + 1 new ValidationBuilder
- Complexity: LOW
- Priority: HIGH (improves consistency and reduces errors)

---

### 6. HTTP CLIENT JSON SERIALIZATION OPTIONS - REPEATED 50+ TIMES
**Status:** Default JsonSerializerOptions recreated everywhere
**Files Affected:** 50+ files with JsonSerializer.Serialize/Deserialize calls
**Duplication Type:** Same serializer options configuration repeated

#### Problem Analysis

**Pattern (Repeated 50+ times across codebase):**

```csharp
// Pattern 1: Creating default options (repeated 50+ times)
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
var json = JsonSerializer.Serialize(payload, options);

// Pattern 2: Creating options with custom settings (repeated 30+ times)
var options = Honua.Server.Core.Utilities.JsonHelper.CreateOptions(
    writeIndented: false,
    camelCase: true,
    caseInsensitive: true,
    ignoreNullValues: true,
    maxDepth: 64
);

// Pattern 3: Creating minimal options (repeated 20+ times)
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```

**Current Partial Solution:**
- `JsonHelper.CreateOptions()` exists but not consistently used
- `ControlPlaneApiClientBase` has `DefaultSerializerOptions` static field
- Many files still create options inline

**Consolidation Opportunity:**

```csharp
// Centralize in JsonHelper (already exists, extend it):
public static class JsonHelper
{
    // Add pre-configured static options
    public static readonly JsonSerializerOptions WebDefaults = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions StrictDefaults = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        MaxDepth = 64
    };

    public static readonly JsonSerializerOptions CaseInsensitiveDefaults = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Existing CreateOptions method remains for custom scenarios
}

// Usage becomes one-liner:
var json = JsonSerializer.Serialize(payload, JsonHelper.WebDefaults);
```

**Estimated Impact:**
- Lines to Consolidate: 150+ lines across 50+ files (3 lines → 1 line each)
- Files to Update: 50+ files
- Complexity: VERY LOW
- Priority: MEDIUM (high volume, low individual impact)

---

### 7. LOGGING EXTENSION METHODS - DUPLICATE STRUCTURED LOGGING
**Status:** 370+ files with ILogger, many duplicate log patterns
**Files Affected:** 370+ files
**Duplication Type:** Similar structured logging patterns

#### Problem Analysis

Based on grep count: 2,802 `ILogger<>` or `_logger.` references across 370 files.

**Common Patterns:**

```csharp
// Pattern 1: Operation failure logging (repeated 100+ times)
_logger.LogError(ex, "Failed to {Operation} for {Resource}", operation, resource);

// Pattern 2: External service failure (repeated 50+ times)
_logger.LogError(ex, "{Service} service error: {ErrorCode}", serviceName, errorCode);

// Pattern 3: Resource not found (repeated 40+ times)
_logger.LogWarning("{ResourceType} not found: {ResourceId}", type, id);

// Pattern 4: Security events (repeated 30+ times)
_logger.LogWarning("Access denied to {Resource} for user {User}", resource, userId);
```

**Current State:**
- Some structured logging extensions exist in `Honua.Server.Core.Logging`
- `LogOperationFailure`, `LogExternalServiceFailure`, `LogResourceNotFound` already exist
- Not consistently used across codebase

**Consolidation Opportunity:**

Extend existing logging extensions and promote their usage:

```csharp
// Extend Honua.Server.Core/Logging/StructuredLoggingExtensions.cs
public static class StructuredLoggingExtensions
{
    // Existing methods (already in codebase):
    // - LogOperationFailure
    // - LogExternalServiceFailure
    // - LogResourceNotFound

    // Add missing common patterns:
    public static void LogSecurityDenied(
        this ILogger logger,
        string resource,
        string? userId,
        string? reason = null)
    {
        logger.LogWarning(
            "Access denied to {Resource} for user {UserId}. Reason: {Reason}",
            resource,
            userId ?? "anonymous",
            reason ?? "Unauthorized");
    }

    public static void LogConfigurationWarning(
        this ILogger logger,
        string configKey,
        string issue,
        string? recommendation = null)
    {
        logger.LogWarning(
            "Configuration issue with {ConfigKey}: {Issue}. {Recommendation}",
            configKey,
            issue,
            recommendation ?? "Check documentation");
    }

    // Add performance logging
    public static IDisposable LogOperationTiming(
        this ILogger logger,
        string operationName,
        params object[] contextValues)
    {
        return new OperationTimer(logger, operationName, contextValues);
    }
}

private sealed class OperationTimer : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operation;
    private readonly object[] _context;
    private readonly Stopwatch _stopwatch;

    public OperationTimer(ILogger logger, string operation, object[] context)
    {
        _logger = logger;
        _operation = operation;
        _context = context;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _logger.LogInformation(
            "{Operation} completed in {ElapsedMs}ms. Context: {@Context}",
            _operation,
            _stopwatch.ElapsedMilliseconds,
            _context);
    }
}
```

**Estimated Impact:**
- Lines to Consolidate: 200+ lines (improve consistency, minor line savings)
- Files to Update: 100+ files (gradual adoption)
- Complexity: VERY LOW
- Priority: LOW (architectural improvement, not line reduction)

**Recommendation:** Document and promote existing extensions rather than major refactoring.

---

### 8. RESILIENCE POLICIES - RETRY/TIMEOUT CONFIGURATION DUPLICATION
**Status:** Polly policies configured in multiple places
**Files Affected:** 10+ services with retry/timeout logic
**Duplication Type:** Similar Polly policy configurations

#### Problem Analysis

**Duplication Pattern:**

```csharp
// Pattern repeated in 10+ files:
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timespan, retryCount, context) =>
        {
            _logger.LogWarning(
                exception,
                "Retry {RetryCount} after {Delay}s for {Operation}",
                retryCount,
                timespan.TotalSeconds,
                operationName);
        });

var timeoutPolicy = Policy.TimeoutAsync(
    TimeSpan.FromSeconds(30),
    onTimeoutAsync: (context, timeout, task) =>
    {
        _logger.LogError("Operation timed out after {Timeout}s", timeout.TotalSeconds);
        return Task.CompletedTask;
    });

var circuitBreakerPolicy = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        exceptionsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromMinutes(1));
```

**Current State:**
- `ResiliencePolicies` class exists in `Honua.Server.Core/Resilience/`
- Some policies already centralized
- Not consistently used across services

**Consolidation Opportunity:**

Extend and promote existing `ResiliencePolicies`:

```csharp
// Extend Honua.Server.Core/Resilience/ResiliencePolicies.cs
public static class ResiliencePolicies
{
    // Add policy factory methods
    public static IAsyncPolicy<HttpResponseMessage> CreateHttpRetryPolicy(
        ILogger logger,
        int maxRetries = 3,
        string? operationName = null)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: maxRetries,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetryAsync: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning(
                        outcome.Exception,
                        "Retry {RetryCount}/{MaxRetries} after {Delay}s for {Operation}. StatusCode: {StatusCode}",
                        retryCount,
                        maxRetries,
                        timespan.TotalSeconds,
                        operationName ?? "HTTP request",
                        outcome.Result?.StatusCode);
                    return Task.CompletedTask;
                });
    }

    public static IAsyncPolicy CreateCircuitBreakerPolicy(
        ILogger logger,
        int exceptionsBeforeBreaking = 5,
        TimeSpan? breakDuration = null,
        string? serviceName = null)
    {
        return Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: exceptionsBeforeBreaking,
                durationOfBreak: breakDuration ?? TimeSpan.FromMinutes(1),
                onBreak: (exception, duration) =>
                {
                    logger.LogError(
                        exception,
                        "Circuit breaker opened for {Service}. Duration: {Duration}s",
                        serviceName ?? "service",
                        duration.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation(
                        "Circuit breaker reset for {Service}",
                        serviceName ?? "service");
                });
    }

    // Add combined policy builder
    public static IAsyncPolicy<HttpResponseMessage> CreateStandardHttpPolicy(
        ILogger logger,
        string serviceName)
    {
        var retry = CreateHttpRetryPolicy(logger, operationName: serviceName);
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
        var circuitBreaker = CreateCircuitBreakerPolicy(logger, serviceName: serviceName)
            .AsAsyncPolicy<HttpResponseMessage>();

        return Policy.WrapAsync(retry, circuitBreaker, timeout);
    }
}
```

**Estimated Impact:**
- Lines to Consolidate: 150+ lines across 10+ files
- Files to Update: 10+ files
- Complexity: MEDIUM
- Priority: MEDIUM-HIGH (improves resilience consistency)

---

### 9. TELEMETRY/METRICS RECORDING PATTERNS
**Status:** Activity/Span creation repeated across many files
**Files Affected:** 50+ files with OpenTelemetry usage
**Duplication Type:** Similar activity creation and tagging patterns

#### Problem Analysis

**Common Pattern:**

```csharp
// Repeated 50+ times:
using var activity = ActivitySource.StartActivity("OperationName");
activity?.SetTag("resource.id", resourceId);
activity?.SetTag("operation.type", operationType);

try
{
    // ... operation ...
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

**Consolidation Opportunity:**

```csharp
// Create telemetry helper:
public static class TelemetryHelper
{
    public static async Task<T> ExecuteWithTelemetryAsync<T>(
        ActivitySource activitySource,
        string operationName,
        Func<Activity?, Task<T>> operation,
        Action<Activity>? setTags = null)
    {
        using var activity = activitySource.StartActivity(operationName);
        setTags?.Invoke(activity);

        try
        {
            var result = await operation(activity);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }

    // Synchronous version
    public static T ExecuteWithTelemetry<T>(
        ActivitySource activitySource,
        string operationName,
        Func<Activity?, T> operation,
        Action<Activity>? setTags = null)
    {
        using var activity = activitySource.StartActivity(operationName);
        setTags?.Invoke(activity);

        try
        {
            var result = operation(activity);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}

// Usage:
var result = await TelemetryHelper.ExecuteWithTelemetryAsync(
    _activitySource,
    "GetFeatures",
    async activity =>
    {
        activity?.SetTag("layer.id", layerId);
        activity?.SetTag("bbox", bbox.ToString());
        return await _repository.QueryAsync(...);
    });
```

**Estimated Impact:**
- Lines to Consolidate: 200+ lines (5 lines → 2 lines per usage)
- Files to Update: 50+ files
- Complexity: LOW
- Priority: MEDIUM

---

### 10. DATABASE CONNECTION STRING PARSING
**Status:** Connection string parsing duplicated in multiple data providers
**Files Affected:** 8+ data store providers
**Duplication Type:** Similar parsing and validation logic

#### Problem Analysis

**Duplication Pattern (Repeated 8+ times):**

```csharp
// Pattern in PostgreSQL, MySQL, SQL Server, SQLite, Oracle, etc.:
private static (string Host, int Port, string Database, string User) ParseConnectionString(string cs)
{
    var builder = new NpgsqlConnectionStringBuilder(cs); // or SqlConnectionStringBuilder, etc.

    if (builder.Host.IsNullOrWhiteSpace())
        throw new ArgumentException("Host is required in connection string");

    if (builder.Database.IsNullOrWhiteSpace())
        throw new ArgumentException("Database is required in connection string");

    return (builder.Host, builder.Port, builder.Database, builder.Username);
}

// Validation repeated:
private static void ValidateConnectionString(string connectionString)
{
    if (connectionString.IsNullOrWhiteSpace())
        throw new ArgumentException("Connection string cannot be empty");

    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        // Validation logic...
    }
    catch (ArgumentException ex)
    {
        throw new InvalidOperationException($"Invalid connection string: {ex.Message}", ex);
    }
}
```

**Consolidation Opportunity:**

```csharp
// Create unified connection string helper:
public static class ConnectionStringHelper
{
    public static ConnectionStringInfo Parse(
        string connectionString,
        DatabaseProvider provider)
    {
        if (connectionString.IsNullOrWhiteSpace())
            throw new ArgumentException("Connection string cannot be empty");

        return provider switch
        {
            DatabaseProvider.PostgreSQL => ParsePostgreSQL(connectionString),
            DatabaseProvider.MySQL => ParseMySQL(connectionString),
            DatabaseProvider.SqlServer => ParseSqlServer(connectionString),
            DatabaseProvider.SQLite => ParseSQLite(connectionString),
            DatabaseProvider.Oracle => ParseOracle(connectionString),
            _ => throw new NotSupportedException($"Provider {provider} not supported")
        };
    }

    public static bool TryParse(
        string connectionString,
        DatabaseProvider provider,
        out ConnectionStringInfo? info,
        out string? error)
    {
        try
        {
            info = Parse(connectionString, provider);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            info = null;
            error = ex.Message;
            return false;
        }
    }

    public static void Validate(
        string connectionString,
        DatabaseProvider provider,
        bool requireDatabase = true,
        bool requireCredentials = false)
    {
        var info = Parse(connectionString, provider);

        if (requireDatabase && info.Database.IsNullOrWhiteSpace())
            throw new ArgumentException("Database name is required");

        if (requireCredentials && info.Username.IsNullOrWhiteSpace())
            throw new ArgumentException("Username is required");
    }

    private static ConnectionStringInfo ParsePostgreSQL(string cs)
    {
        var builder = new NpgsqlConnectionStringBuilder(cs);
        return new ConnectionStringInfo(
            Provider: DatabaseProvider.PostgreSQL,
            Host: builder.Host ?? "localhost",
            Port: builder.Port != 0 ? builder.Port : 5432,
            Database: builder.Database ?? string.Empty,
            Username: builder.Username ?? string.Empty);
    }

    // Similar methods for other providers...
}

public record ConnectionStringInfo(
    DatabaseProvider Provider,
    string Host,
    int Port,
    string Database,
    string Username);

public enum DatabaseProvider
{
    PostgreSQL,
    MySQL,
    SqlServer,
    SQLite,
    Oracle,
    Snowflake,
    BigQuery,
    Redshift
}
```

**Estimated Impact:**
- Lines to Consolidate: 200+ lines across 8+ providers
- Files to Update: 8+ data store providers
- Complexity: MEDIUM
- Priority: MEDIUM-HIGH

---

## Part 2: Category Breakdown

### Infrastructure (8 opportunities, 450+ lines)
**Primary Focus: Caching, HTTP, Resilience**

1. **Cloud Storage Health Checks** (250 lines) - HIGH PRIORITY
   - 3 nearly identical implementations
   - Template Method pattern ideal fit
   - Files: AzureBlobHealthCheck, S3HealthCheck, GcsHealthCheck

2. **Control Plane API Clients** (180 lines) - HIGH PRIORITY
   - Envelope unwrapping duplication
   - Multi-part form content building
   - Content-type resolution
   - Files: 8+ API client files

3. **Resilience Policies** (150 lines) - MEDIUM-HIGH PRIORITY
   - Polly policy configuration repeated
   - Extend existing ResiliencePolicies
   - Files: 10+ service files

4. **Database Connection String Parsing** (200 lines) - MEDIUM-HIGH PRIORITY
   - Repeated across 8+ data providers
   - Validation logic duplicated
   - Files: PostgreSQL, MySQL, SQL Server, SQLite, Oracle, Snowflake, BigQuery, Redshift providers

5. **HTTP Client Initialization** (120 lines) - MEDIUM PRIORITY
   - Repeated in embedding providers
   - Similar in alert publishers
   - Files: 6+ providers

### Configuration (4 opportunities, 350+ lines)
**Primary Focus: Validation, Options Patterns**

1. **Configuration Validators** (300 lines) - HIGH PRIORITY
   - 15+ validators with duplicate patterns
   - Range checks, required fields, cross-field validation
   - Create fluent ValidationBuilder
   - Files: 15+ validator files

2. **JSON Serialization Options** (150 lines) - MEDIUM PRIORITY
   - Recreated 50+ times
   - Use existing JsonHelper more consistently
   - Files: 50+ files

3. **Connection String Validators** (included in #4 above)

### Observability (3 opportunities, 400+ lines)
**Primary Focus: Logging, Telemetry, Metrics**

1. **Telemetry/Metrics Recording** (200 lines) - MEDIUM PRIORITY
   - Activity creation repeated 50+ times
   - Create TelemetryHelper wrapper
   - Files: 50+ files with OpenTelemetry

2. **Structured Logging** (200 lines) - LOW PRIORITY (Architectural)
   - Extend existing logging extensions
   - Promote consistent usage
   - Files: 100+ files (gradual adoption)

3. **Health Check Base Pattern** (minimal duplication)
   - HealthCheckBase already exists and used well

### Alert/Notification (2 opportunities, 100+ lines)
**Primary Focus: Webhook Patterns**

1. **Alert Publisher Improvements** (80 lines) - MEDIUM PRIORITY
   - WebhookAlertPublisherBase already excellent
   - Minor opportunities: severity mapping, config resolution
   - Files: 5 publisher implementations

2. **Notification Service Patterns** (minimal duplication)
   - Email, Slack already well-abstracted

### AI/ML (2 opportunities, 150+ lines)
**Primary Focus: Provider Patterns**

1. **Embedding Provider Initialization** (120 lines) - MEDIUM-HIGH PRIORITY
   - HttpClient setup repeated 6 times
   - Create EmbeddingProviderBase
   - Files: 6 embedding providers

2. **LLM Provider Patterns** (assessed separately)
   - LlmProviderBase already exists

---

## Part 3: Quick Wins (High Impact, Low Effort)

### Quick Win 1: Cloud Storage Health Check Consolidation
**Effort:** 3-4 hours
**Impact:** 250+ lines consolidated
**Risk:** LOW

**Steps:**
1. Create `CloudStorageHealthCheckBase<TClient>` abstract class (1 hour)
2. Refactor `AzureBlobHealthCheck` to inherit from base (45 min)
3. Refactor `S3HealthCheck` to inherit from base (45 min)
4. Refactor `GcsHealthCheck` to inherit from base (45 min)
5. Test all three implementations (30 min)

**Files:**
- Create: `/src/Honua.Server.Host/Health/CloudStorageHealthCheckBase.cs`
- Modify: `AzureBlobHealthCheck.cs`, `S3HealthCheck.cs`, `GcsHealthCheck.cs`

---

### Quick Win 2: Control Plane API Client Helpers
**Effort:** 2-3 hours
**Impact:** 180+ lines consolidated
**Risk:** LOW

**Steps:**
1. Extend `ControlPlaneApiClientBase` with envelope unwrapping (45 min)
2. Create `GeoDataContentTypeResolver` utility (30 min)
3. Add multi-part form helper method (30 min)
4. Refactor 3 API clients to use new helpers (1 hour)
5. Test all API clients (30 min)

**Files:**
- Modify: `/src/Honua.Cli/Services/ControlPlane/ControlPlaneApiClientBase.cs`
- Create: `/src/Honua.Cli/Services/ControlPlane/GeoDataContentTypeResolver.cs`
- Update: `DataIngestionApiClient.cs`, `MigrationApiClient.cs`, `RasterTileCacheApiClient.cs`

---

### Quick Win 3: Configuration Validation Builder
**Effort:** 4-5 hours
**Impact:** 300+ lines consolidated
**Risk:** LOW

**Steps:**
1. Create `ValidationBuilder` fluent API (2 hours)
2. Add `ConnectionStringValidator` helper (1 hour)
3. Refactor 5 validators to use builder (1.5 hours)
4. Test validators with invalid configurations (30 min)

**Files:**
- Create: `/src/Honua.Server.Core/Configuration/ValidationBuilder.cs`
- Create: `/src/Honua.Server.Core/Configuration/ConnectionStringValidator.cs`
- Modify: 5+ validator files

---

### Quick Win 4: JSON Serialization Options Standardization
**Effort:** 2 hours
**Impact:** 150+ lines (consistency improvement)
**Risk:** VERY LOW

**Steps:**
1. Add static options properties to existing `JsonHelper` (30 min)
2. Document usage patterns (30 min)
3. Refactor 10 high-traffic files as examples (1 hour)

**Files:**
- Modify: `/src/Honua.Server.Core/Utilities/JsonHelper.cs`
- Update: 10 example files

---

### Quick Win 5: Resilience Policy Factory Methods
**Effort:** 2-3 hours
**Impact:** 150+ lines consolidated
**Risk:** LOW

**Steps:**
1. Extend existing `ResiliencePolicies` with factory methods (1 hour)
2. Refactor 3 services to use factories (1 hour)
3. Test retry/circuit breaker scenarios (1 hour)

**Files:**
- Modify: `/src/Honua.Server.Core/Resilience/ResiliencePolicies.cs`
- Update: 3 service files

---

## Part 4: Strategic Refactorings

### Strategic Refactoring 1: Embedding Provider Base Class
**Effort:** 6-8 hours
**Impact:** 150+ lines consolidated
**Files:** 6 embedding providers
**Risk:** MEDIUM

**Deliverables:**
1. `EmbeddingProviderBase` abstract class
2. Refactor all 6 providers to inherit from base
3. Add comprehensive unit tests
4. Update documentation

**Benefits:**
- Consistent provider initialization
- Easier to add new providers
- Centralized error handling
- Better testability

---

### Strategic Refactoring 2: Database Connection String Helper
**Effort:** 8-10 hours
**Impact:** 200+ lines consolidated
**Files:** 8+ data providers
**Risk:** MEDIUM-HIGH (database connectivity critical)

**Deliverables:**
1. `ConnectionStringHelper` utility class
2. `ConnectionStringInfo` record
3. Provider-specific parsing methods
4. Comprehensive validation
5. Extensive testing with various connection string formats

**Benefits:**
- Single source of truth for connection string parsing
- Consistent validation across all providers
- Better error messages
- Easier to add new database providers

---

### Strategic Refactoring 3: Telemetry Helper Wrapper
**Effort:** 4-6 hours
**Impact:** 200+ lines consolidated
**Files:** 50+ files
**Risk:** LOW-MEDIUM

**Deliverables:**
1. `TelemetryHelper` utility class
2. Async/sync overloads
3. Refactor 10 high-traffic services as examples
4. Document patterns

**Benefits:**
- Consistent telemetry recording
- Automatic error status setting
- Less boilerplate in service code
- Improved trace quality

---

## Part 5: Implementation Roadmap

### Phase 4a: Quick Wins (Weeks 1-2)
**Target:** 700+ lines consolidated
**Effort:** 15-20 hours

Week 1:
- Cloud Storage Health Check Consolidation (3-4 hours)
- Control Plane API Client Helpers (2-3 hours)
- JSON Serialization Options Standardization (2 hours)

Week 2:
- Configuration Validation Builder (4-5 hours)
- Resilience Policy Factory Methods (2-3 hours)
- Testing & Integration (2-3 hours)

### Phase 4b: Strategic Refactorings (Weeks 3-4)
**Target:** 550+ lines consolidated
**Effort:** 18-24 hours

Week 3:
- Embedding Provider Base Class (6-8 hours)
- Telemetry Helper Wrapper (4-6 hours)

Week 4:
- Database Connection String Helper (8-10 hours)
- Comprehensive testing and documentation (4-6 hours)

### Phase 4c: Documentation & Promotion (Week 5)
**Target:** Improve adoption of existing patterns
**Effort:** 6-8 hours

- Document all consolidation patterns
- Create usage examples
- Update architecture diagrams
- Code review guidelines
- Training materials

---

## Part 6: Risk Analysis

### Low Risk Changes
- JSON serialization options (read-only static fields)
- Configuration validation builder (isolated utility)
- Resilience policy factories (wrapper methods)
- Cloud storage health checks (isolated health check system)

### Medium Risk Changes
- Control plane API client changes (affects CLI tool)
- Embedding provider refactoring (affects AI features)
- Telemetry helper (affects observability)
- Alert publisher improvements (affects monitoring)

### High Risk Changes
- Database connection string parsing (critical for data access)
- HTTP client initialization changes (external dependencies)

**Mitigation Strategies:**
- Comprehensive unit tests for all changes
- Integration tests for high-risk areas
- Canary deployments for database changes
- Feature flags for new patterns
- Extensive logging during migration
- Backward compatibility where possible

---

## Part 7: Expected Outcomes

### Total Consolidation Potential

| Phase | Lines Eliminated | Effort (Hours) | Priority |
|-------|------------------|----------------|----------|
| Quick Wins (Phase 4a) | 700+ lines | 15-20 | HIGH |
| Strategic (Phase 4b) | 550+ lines | 18-24 | MEDIUM-HIGH |
| Documentation (Phase 4c) | 0 lines (quality) | 6-8 | MEDIUM |
| **Total Phase 4** | **1,250+ lines** | **39-52 hours** | **HIGH** |

### Cumulative Impact

| Phase | Lines Eliminated | Status |
|-------|------------------|--------|
| Phase 1-2 | ~200 lines | ✅ Completed |
| Phase 3 | ~1,200 lines | ✅ Completed |
| Phase 4 | ~1,250 lines | 📋 Planned |
| **Grand Total** | **~2,650 lines** | **Target** |

### Quality Improvements

Beyond line count reduction:

1. **Consistency**
   - Standardized health checks across all cloud providers
   - Uniform configuration validation patterns
   - Consistent error handling and logging

2. **Maintainability**
   - Single source of truth for common patterns
   - Easier to add new implementations (providers, validators, etc.)
   - Reduced cognitive load for developers

3. **Testability**
   - Centralized logic easier to test
   - Fewer test duplicates needed
   - Better test coverage possible

4. **Reliability**
   - Consistent resilience patterns
   - Standardized retry/timeout behavior
   - Better error handling

5. **Observability**
   - Consistent telemetry recording
   - Structured logging adoption
   - Better debugging experience

---

## Part 8: Alternative Approaches Considered

### Approach 1: Aggressive Consolidation (Not Recommended)
Create mega-base classes for everything.

**Pros:** Maximum line reduction
**Cons:** High coupling, reduced flexibility, harder to test
**Decision:** REJECTED - Violates SOLID principles

### Approach 2: No Consolidation (Status Quo)
Leave duplicates as-is for maximum independence.

**Pros:** Zero risk, no effort
**Cons:** Continued maintenance burden, inconsistency
**Decision:** REJECTED - Technical debt accumulates

### Approach 3: Incremental Consolidation (Recommended) ✅
Focus on high-value, low-risk consolidations first.

**Pros:** Balance risk/reward, iterative improvement
**Cons:** Takes longer to complete
**Decision:** SELECTED - Best balance of safety and impact

### Approach 4: Code Generation
Use T4 templates or source generators for repeated patterns.

**Pros:** Zero runtime overhead
**Cons:** Complexity, harder debugging, tooling requirements
**Decision:** DEFERRED - Consider for Phase 5 if needed

---

## Part 9: Success Metrics

### Quantitative Metrics

1. **Lines of Code**
   - Target: Eliminate 1,250+ duplicate lines
   - Measure: Git diff analysis

2. **File Count**
   - Target: Reduce files with duplicate patterns from 100+ to 50
   - Measure: Pattern analysis

3. **Test Coverage**
   - Target: Maintain or increase coverage despite fewer lines
   - Measure: Code coverage reports

4. **Build Time**
   - Target: No increase (ideally slight decrease)
   - Measure: CI/CD pipeline metrics

### Qualitative Metrics

1. **Developer Experience**
   - Faster onboarding for new developers
   - Easier to add new implementations (providers, validators)
   - Less confusion about which pattern to use

2. **Code Review Quality**
   - Fewer "this looks like..." comments
   - Faster reviews due to consistent patterns
   - Higher quality feedback on business logic

3. **Bug Reduction**
   - Fewer copy-paste errors
   - Consistent error handling
   - Better test coverage

4. **Documentation Quality**
   - Clear usage examples
   - Reduced documentation duplication
   - Better architecture diagrams

---

## Part 10: Recommendations

### Immediate Actions (This Sprint)

1. **Quick Win 1: Cloud Storage Health Checks** (3-4 hours)
   - Highest ROI (250 lines / 4 hours = 62 lines/hour)
   - Low risk, clear benefits
   - Start immediately

2. **Quick Win 2: Control Plane API Client Helpers** (2-3 hours)
   - High value for CLI tool
   - Affects developer productivity
   - Start after Quick Win 1

3. **Quick Win 4: JSON Serialization Options** (2 hours)
   - Very low risk
   - Can be done in parallel
   - Quick documentation win

### Short-Term Actions (Next 2 Sprints)

1. Complete remaining Quick Wins (Weeks 1-2)
2. Begin Strategic Refactoring 1: Embedding Providers (Week 3)
3. Complete Strategic Refactoring 3: Telemetry Helper (Week 3-4)

### Long-Term Actions (Months 2-3)

1. Strategic Refactoring 2: Database Connection String Helper
   - Requires extensive testing
   - High risk, high reward
   - Plan carefully

2. Documentation & Promotion Phase
   - Ensure team adoption
   - Update coding standards
   - Training materials

### Areas to Avoid

1. **Protocol Implementation Logic**
   - Already covered in Phase 3
   - Protocol-specific logic should remain separate
   - Don't over-consolidate

2. **Business Logic**
   - Don't consolidate unique domain logic
   - Duplication here may be acceptable
   - Focus on infrastructure/utilities only

3. **Performance-Critical Paths**
   - Don't add abstraction overhead to hot paths
   - Profile before consolidating
   - Keep optimizations separate

---

## Conclusion

Phase 4 analysis identifies significant consolidation opportunities in infrastructure, configuration, and observability—complementing the protocol-focused work of Phases 1-3. The recommended incremental approach balances:

- **High Impact:** 1,250+ lines of duplication eliminated
- **Low Risk:** Focus on utilities and infrastructure, not business logic
- **Quick Wins:** 700+ lines in first 2 weeks
- **Strategic Value:** Improved consistency, testability, and maintainability

**Recommendation:** Proceed with Quick Wins immediately, then evaluate strategic refactorings based on team capacity and priorities.

**Estimated ROI:**
- Total effort: 39-52 hours
- Lines eliminated: 1,250+
- Lines per hour: ~25 lines/hour
- Long-term maintenance reduction: 15-20% fewer defects in consolidated areas

The consolidation work improves code quality while maintaining the excellent architectural patterns already established (e.g., `WebhookAlertPublisherBase`, `HealthCheckBase`, `ControlPlaneApiClientBase`). Phase 4 extends these patterns and promotes their consistent adoption across the codebase.

---

## Appendix A: File Inventory

### High-Priority Files for Consolidation

**Health Checks (250 lines):**
- `/src/Honua.Server.Host/Health/AzureBlobHealthCheck.cs` (142 lines)
- `/src/Honua.Server.Host/Health/S3HealthCheck.cs` (129 lines)
- `/src/Honua.Server.Host/Health/GcsHealthCheck.cs` (136 lines)

**Control Plane API Clients (180 lines):**
- `/src/Honua.Cli/Services/ControlPlane/ControlPlaneApiClientBase.cs` (323 lines - extend)
- `/src/Honua.Cli/Services/ControlPlane/DataIngestionApiClient.cs` (127 lines)
- `/src/Honua.Cli/Services/ControlPlane/MigrationApiClient.cs` (~120 lines)
- `/src/Honua.Cli/Services/ControlPlane/RasterTileCacheApiClient.cs` (~100 lines)
- Plus 5 more API clients

**Configuration Validators (300 lines):**
- `/src/Honua.Cli.AI/Configuration/RedisOptionsValidator.cs` (95 lines)
- `/src/Honua.Server.Core/Configuration/HonuaConfigurationValidator.cs` (100+ lines)
- Plus 13 more validators

**Embedding Providers (120 lines):**
- `/src/Honua.Cli.AI/Services/AI/Providers/AnthropicEmbeddingProvider.cs` (~150 lines)
- `/src/Honua.Cli.AI/Services/AI/Providers/OpenAIEmbeddingProvider.cs` (~120 lines)
- `/src/Honua.Cli.AI/Services/AI/Providers/AzureOpenAIEmbeddingProvider.cs` (~130 lines)
- `/src/Honua.Cli.AI/Services/AI/Providers/LocalAIEmbeddingProvider.cs` (~110 lines)
- Plus 2 more providers

**Alert Publishers (80 lines):**
- `/src/Honua.Server.AlertReceiver/Services/WebhookAlertPublisherBase.cs` (185 lines - extend)
- `/src/Honua.Server.AlertReceiver/Services/OpsgenieAlertPublisher.cs` (240 lines)
- `/src/Honua.Server.AlertReceiver/Services/PagerDutyAlertPublisher.cs` (170 lines)
- Plus 3 more publishers

### Total Files Affected: 60+ files
### Total Consolidation Potential: 1,250+ lines

---

## Appendix B: Code Examples

### Example 1: Cloud Storage Health Check Before/After

**Before (142 lines in AzureBlobHealthCheck.cs):**
```csharp
public sealed class AzureBlobHealthCheck : HealthCheckBase
{
    private readonly BlobServiceClient? _blobServiceClient;
    private readonly string? _testContainer;

    public AzureBlobHealthCheck(
        ILogger<AzureBlobHealthCheck> logger,
        BlobServiceClient? blobServiceClient = null,
        string? testContainer = null)
        : base(logger)
    {
        _blobServiceClient = blobServiceClient;
        _testContainer = testContainer;
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_blobServiceClient == null)
            {
                data["azure_blob.configured"] = false;
                Logger.LogDebug("Azure Blob Storage client not configured");
                return HealthCheckResult.Healthy(
                    "Azure Blob Storage not configured (optional dependency)",
                    data);
            }

            data["azure_blob.configured"] = true;
            data["azure_blob.account_name"] = _blobServiceClient.AccountName;

            var accountInfo = await _blobServiceClient.GetAccountInfoAsync(cancellationToken);
            data["azure_blob.account_kind"] = accountInfo.Value.AccountKind.ToString();
            data["azure_blob.can_access_account"] = true;

            if (_testContainer.HasValue())
            {
                try
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(_testContainer);
                    var exists = await containerClient.ExistsAsync(cancellationToken);

                    data["azure_blob.test_container"] = _testContainer;
                    data["azure_blob.test_container_exists"] = exists.Value;

                    if (exists.Value)
                    {
                        var properties = await containerClient.GetPropertiesAsync(
                            cancellationToken: cancellationToken);
                        data["azure_blob.test_container_accessible"] = true;

                        Logger.LogDebug(
                            "Azure Blob health check passed. Account: {AccountName}",
                            _blobServiceClient.AccountName);
                    }
                    else
                    {
                        data["azure_blob.test_container_accessible"] = false;
                        Logger.LogWarning("Test container does not exist: {TestContainer}",
                            _testContainer);

                        return HealthCheckResult.Degraded(
                            $"Test container '{_testContainer}' does not exist",
                            data: data);
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 403)
                {
                    data["azure_blob.test_container_accessible"] = false;
                    Logger.LogWarning(ex, "Test container access denied: {TestContainer}",
                        _testContainer);

                    return HealthCheckResult.Degraded(
                        $"Access denied to test container '{_testContainer}'",
                        data: data);
                }
            }

            return HealthCheckResult.Healthy("Azure Blob Storage accessible", data);
        }
        catch (RequestFailedException ex)
        {
            data["azure_blob.error"] = ex.Message;
            Logger.LogError(ex, "Azure Blob service error");
            return HealthCheckResult.Unhealthy("Azure Blob service error", ex, data);
        }
    }

    // ... (CreateUnhealthyResult method - 10 more lines)
}
```

**After (30 lines in AzureBlobHealthCheck.cs):**
```csharp
public sealed class AzureBlobHealthCheck
    : CloudStorageHealthCheckBase<BlobServiceClient>
{
    public AzureBlobHealthCheck(
        ILogger<AzureBlobHealthCheck> logger,
        BlobServiceClient? blobServiceClient = null,
        string? testContainer = null)
        : base(logger, blobServiceClient, testContainer, "azure_blob")
    {
    }

    protected override async Task TestConnectivityAsync(
        BlobServiceClient client,
        CancellationToken ct)
    {
        var accountInfo = await client.GetAccountInfoAsync(ct);
        Metadata["account_kind"] = accountInfo.Value.AccountKind.ToString();
        Metadata["account_name"] = client.AccountName;
    }

    protected override async Task<bool> BucketExistsAsync(
        BlobServiceClient client,
        string bucket,
        CancellationToken ct)
    {
        var containerClient = client.GetBlobContainerClient(bucket);
        var exists = await containerClient.ExistsAsync(ct);
        return exists.Value;
    }

    protected override async Task<object> GetBucketMetadataAsync(
        BlobServiceClient client,
        string bucket,
        CancellationToken ct)
    {
        var containerClient = client.GetBlobContainerClient(bucket);
        var properties = await containerClient.GetPropertiesAsync(
            cancellationToken: ct);
        return new { PublicAccess = properties.Value.PublicAccess.ToString() };
    }
}
```

**Reduction: 142 lines → 30 lines (78% reduction)**

### Example 2: Configuration Validator Before/After

**Before (95 lines in RedisOptionsValidator.cs):**
```csharp
public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        var failures = new List<string>();

        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        if (options.ConnectionString.IsNullOrWhiteSpace())
        {
            failures.Add("Redis ConnectionString is required when enabled.");
        }
        else if (!IsValidRedisConnectionString(options.ConnectionString))
        {
            failures.Add($"Redis ConnectionString '{options.ConnectionString}' is invalid.");
        }

        if (options.KeyPrefix.IsNullOrWhiteSpace())
        {
            failures.Add("Redis KeyPrefix cannot be empty.");
        }

        if (options.TtlSeconds <= 0)
        {
            failures.Add($"Redis TtlSeconds must be > 0. Current: {options.TtlSeconds}.");
        }

        if (options.TtlSeconds > 2_592_000)
        {
            failures.Add($"Redis TtlSeconds ({options.TtlSeconds}) exceeds limit.");
        }

        // ... 50 more lines of similar validation ...

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidRedisConnectionString(string connectionString)
    {
        // 20 lines of validation logic
    }
}
```

**After (25 lines in RedisOptionsValidator.cs):**
```csharp
public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        return new ValidationBuilder("Redis")
            .Required(options.ConnectionString, "ConnectionString")
            .ConnectionString(
                options.ConnectionString,
                "ConnectionString",
                ConnectionStringType.Redis)
            .Required(options.KeyPrefix, "KeyPrefix")
            .Range(options.TtlSeconds, "TtlSeconds", min: 1, max: 2_592_000)
            .Range(options.ConnectTimeoutMs, "ConnectTimeoutMs", min: 1, max: 60_000)
            .Range(options.SyncTimeoutMs, "SyncTimeoutMs", min: 1, max: 30_000)
            .Build();
    }
}
```

**Reduction: 95 lines → 25 lines (74% reduction)**

---

**End of Phase 4 Analysis**
