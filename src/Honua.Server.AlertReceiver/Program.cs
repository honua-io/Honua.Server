// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Amazon.SimpleNotificationService;
using Honua.Server.AlertReceiver.Configuration;
using Honua.Server.AlertReceiver.Middleware;
using Honua.Server.AlertReceiver.Security;
using Honua.Server.AlertReceiver.Services;
using Honua.Server.Core.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Linq;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add database
var connectionString = builder.Configuration.GetConnectionString("AlertHistory");
if (connectionString.IsNullOrWhiteSpace())
{
    Log.Fatal(
        "CONFIGURATION ERROR: ConnectionStrings:AlertHistory is required for alert persistence. {NewLine}" +
        "Set via appsettings.json or environment variable: ConnectionStrings__AlertHistory{NewLine}" +
        "Example: \"Host=localhost;Database=alerts;Username=user;Password=pass\"",
        Environment.NewLine, Environment.NewLine);
    throw new InvalidOperationException("AlertHistory connection string not configured");
}
// Configure webhook security
builder.Services.Configure<WebhookSecurityOptions>(
    builder.Configuration.GetSection(WebhookSecurityOptions.SectionName));

// Validate webhook security configuration
var webhookSecurityOptions = builder.Configuration
    .GetSection(WebhookSecurityOptions.SectionName)
    .Get<WebhookSecurityOptions>() ?? new WebhookSecurityOptions();

if (!webhookSecurityOptions.IsValid(out var validationErrors))
{
    foreach (var error in validationErrors)
    {
        Log.Warning("Webhook security configuration issue: {Error}", error);
    }

    // Only fail startup if signature is required but secrets are missing
    if (webhookSecurityOptions.RequireSignature &&
        !webhookSecurityOptions.GetAllSecrets().Any())
    {
        Log.Fatal(
            "CONFIGURATION ERROR: Webhook:Security:RequireSignature is true but no secrets configured. {NewLine}" +
            "Set via environment variable: Webhook__Security__SharedSecret{NewLine}" +
            "Generate a secure secret: openssl rand -base64 32{NewLine}" +
            "Or set Webhook:Security:RequireSignature to false (NOT recommended for production)",
            Environment.NewLine, Environment.NewLine, Environment.NewLine);
    }
}

Log.Information(
    "Webhook security configured - RequireSignature: {RequireSignature}, HTTPS Required: {HttpsRequired}",
    webhookSecurityOptions.RequireSignature,
    !webhookSecurityOptions.AllowInsecureHttp);

// Add webhook signature validator
builder.Services.AddScoped<IWebhookSignatureValidator, WebhookSignatureValidator>();

// Configure alert deduplication cache
builder.Services.Configure<AlertDeduplicationCacheOptions>(
    builder.Configuration.GetSection(AlertDeduplicationCacheOptions.SectionName));

var cacheOptions = builder.Configuration
    .GetSection(AlertDeduplicationCacheOptions.SectionName)
    .Get<AlertDeduplicationCacheOptions>() ?? new AlertDeduplicationCacheOptions();

// MEMORY LEAK FIX: Add MemoryCache with bounded size limit for alert deduplication
builder.Services.AddMemoryCache(options =>
{
    // Set size limit to prevent unbounded memory growth
    // Each cache entry has size=1, so this limits total entries
    options.SizeLimit = cacheOptions.MaxEntries;

    // Compact cache when memory pressure is high
    options.CompactionPercentage = 0.25; // Remove 25% of entries when limit is reached

    // Check for expired entries every 5 minutes
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
});

Log.Information(
    "Alert deduplication cache configured with size limit: {SizeLimit} entries",
    cacheOptions.MaxEntries);

// Add alert services
builder.Services.AddSingleton<IAlertReceiverDbConnectionFactory>(_ =>
    new NpgsqlAlertReceiverDbConnectionFactory(connectionString));
builder.Services.AddSingleton<IAlertHistoryStore, AlertHistoryStore>();
builder.Services.AddScoped<IAlertDeduplicator, SqlAlertDeduplicator>();
builder.Services.AddScoped<IAlertPersistenceService, AlertPersistenceService>();
builder.Services.AddScoped<IAlertSilencingService, AlertSilencingService>();
builder.Services.AddSingleton<IAlertMetricsService, AlertMetricsService>();
builder.Services.AddHostedService<AlertHistoryStartupInitializer>();

// Configure HTTP clients
builder.Services.AddHttpClient("PagerDuty");
builder.Services.AddHttpClient("Slack");
builder.Services.AddHttpClient("Teams");
builder.Services.AddHttpClient("Opsgenie");

// Configure AWS SNS
builder.Services.AddAWSService<IAmazonSimpleNotificationService>();

// Register individual alert publishers
builder.Services.AddSingleton<SnsAlertPublisher>();
builder.Services.AddSingleton<AzureEventGridAlertPublisher>();
builder.Services.AddSingleton<PagerDutyAlertPublisher>();
builder.Services.AddSingleton<SlackWebhookAlertPublisher>();
builder.Services.AddSingleton<TeamsWebhookAlertPublisher>();
builder.Services.AddSingleton<OpsgenieAlertPublisher>();

// Register composite publisher with retry and circuit breaker
builder.Services.AddSingleton<IAlertPublisher>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var compositeLogger = sp.GetRequiredService<ILogger<CompositeAlertPublisher>>();
    var retryLogger = sp.GetRequiredService<ILogger<RetryAlertPublisher>>();
    var cbLogger = sp.GetRequiredService<ILogger<CircuitBreakerAlertPublisher>>();
    var publishers = new List<IAlertPublisher>();

    // Helper to wrap publisher with retry + circuit breaker
    IAlertPublisher WrapPublisher(IAlertPublisher publisher)
    {
        // Apply retry first (inner)
        var withRetry = new RetryAlertPublisher(publisher, config, retryLogger);
        // Then circuit breaker (outer)
        return new CircuitBreakerAlertPublisher(withRetry, config, cbLogger);
    }

    // Add enabled publishers based on configuration
    if (config["Alerts:SNS:CriticalTopicArn"].HasValue())
    {
        publishers.Add(WrapPublisher(sp.GetRequiredService<SnsAlertPublisher>()));
    }

    if (config["Alerts:Azure:EventGridEndpoint"].HasValue())
    {
        publishers.Add(WrapPublisher(sp.GetRequiredService<AzureEventGridAlertPublisher>()));
    }

    if (config["Alerts:PagerDuty:CriticalRoutingKey"].HasValue() ||
        config["Alerts:PagerDuty:DefaultRoutingKey"].HasValue())
    {
        publishers.Add(WrapPublisher(sp.GetRequiredService<PagerDutyAlertPublisher>()));
    }

    if (config["Alerts:Slack:CriticalWebhookUrl"].HasValue() ||
        config["Alerts:Slack:DefaultWebhookUrl"].HasValue())
    {
        publishers.Add(WrapPublisher(sp.GetRequiredService<SlackWebhookAlertPublisher>()));
    }

    if (config["Alerts:Teams:CriticalWebhookUrl"].HasValue() ||
        config["Alerts:Teams:DefaultWebhookUrl"].HasValue())
    {
        publishers.Add(WrapPublisher(sp.GetRequiredService<TeamsWebhookAlertPublisher>()));
    }

    if (config["Alerts:Opsgenie:ApiKey"].HasValue())
    {
        publishers.Add(WrapPublisher(sp.GetRequiredService<OpsgenieAlertPublisher>()));
    }

    compositeLogger.LogInformation("Registered {Count} alert publishers with retry and circuit breaker", publishers.Count);
    return new CompositeAlertPublisher(publishers, compositeLogger);
});

// Configure authentication
var jwtIssuer = builder.Configuration["Authentication:JwtIssuer"] ?? "honua-alert-receiver";
var jwtAudience = builder.Configuration["Authentication:JwtAudience"] ?? "honua-alert-api";

var signingKeyOptions = builder.Configuration
    .GetSection("Authentication:JwtSigningKeys")
    .Get<JwtSigningKeyOption[]>() ?? Array.Empty<JwtSigningKeyOption>();

SymmetricSecurityKey[] signingKeys;

if (signingKeyOptions.Length > 0)
{
    signingKeys = signingKeyOptions
        .Select((option, index) => CreateSymmetricKey(option, index))
        .ToArray();

    var duplicateKeyIds = signingKeys
        .GroupBy(key => key.KeyId, StringComparer.Ordinal)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key ?? "(null)")
        .ToArray();

    if (duplicateKeyIds.Length > 0)
    {
        throw new InvalidOperationException(
            "Authentication:JwtSigningKeys contains duplicate keyId values: " +
            string.Join(", ", duplicateKeyIds));
    }

    var activeOption = signingKeyOptions.FirstOrDefault(option => option.Active);
    string activeKeyId;

    if (activeOption is null)
    {
        activeKeyId = signingKeys.First().KeyId ?? "key-1";
        Log.Warning(
            "Authentication:JwtSigningKeys does not specify an active key. Defaulting to {ActiveKeyId}.",
            activeKeyId);
    }
    else
    {
        activeKeyId = string.IsNullOrWhiteSpace(activeOption.KeyId)
            ? $"key-{Array.IndexOf(signingKeyOptions, activeOption) + 1}"
            : activeOption.KeyId!;
    }

    Log.Information(
        "JWT signing keys configured. Active key: {ActiveKeyId}. Total keys: {Count}",
        activeKeyId,
        signingKeys.Length);
}
else
{
    var jwtSecret = builder.Configuration["Authentication:JwtSecret"];

    if (jwtSecret.IsNullOrWhiteSpace())
    {
        Log.Fatal(
            "CONFIGURATION ERROR: Authentication:JwtSecret is required when JwtSigningKeys are not configured. {NewLine}" +
            "Set via appsettings.json or environment variable: Authentication__JwtSecret{NewLine}" +
            "Generate a secure secret: openssl rand -base64 32",
            Environment.NewLine,
            Environment.NewLine);
        throw new InvalidOperationException("JWT secret not configured");
    }

    // NIST SP 800-107 recommends key length equal to hash output for HMAC
    // For HMAC-SHA256: 256 bits = 32 bytes = 43 base64 characters minimum
    // We require 64 characters (512 bits) for future-proofing and enhanced security
    if (jwtSecret.Length < 64)
    {
        Log.Fatal(
            "CONFIGURATION ERROR: Authentication:JwtSecret must be at least 64 characters for HMAC-SHA256 security. {NewLine}" +
            "Current length: {Length}. NIST SP 800-107 requires key length >= hash output (256 bits). {NewLine}" +
            "Generate a secure 512-bit secret: openssl rand -base64 64{NewLine}" +
            "Or use hex encoding (128 hex chars): openssl rand -hex 64{NewLine}" +
            "NOTE: For key rotation, use Authentication:JwtSigningKeys array instead.",
            Environment.NewLine, jwtSecret.Length, Environment.NewLine, Environment.NewLine, Environment.NewLine);
        throw new InvalidOperationException("JWT secret too short (minimum 64 characters required for HMAC-SHA256 security)");
    }

    // Validate entropy to prevent weak keys (e.g., repeated characters, patterns)
    var entropyValidation = ValidateKeyEntropy(jwtSecret);
    if (!entropyValidation.IsValid)
    {
        Log.Fatal(
            "CONFIGURATION ERROR: Authentication:JwtSecret has insufficient entropy. {NewLine}" +
            "Issue: {Reason}{NewLine}" +
            "Generate a cryptographically secure secret: openssl rand -base64 64",
            Environment.NewLine, entropyValidation.Reason, Environment.NewLine);
        throw new InvalidOperationException($"JWT secret has weak entropy: {entropyValidation.Reason}");
    }

    // Warn if using legacy single-key configuration (should migrate to key rotation)
    Log.Warning(
        "Using legacy Authentication:JwtSecret configuration. Consider migrating to Authentication:JwtSigningKeys " +
        "for key rotation support. See documentation: docs/api/authentication.md");

    var defaultKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    {
        KeyId = "default"
    };

    signingKeys = new[] { defaultKey };

    Log.Information("JWT signing keys configured using legacy JwtSecret value.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKeys = signingKeys,
            ClockSkew = TimeSpan.FromMinutes(5),
            IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
            {
                if (!string.IsNullOrWhiteSpace(kid))
                {
                    var matches = signingKeys
                        .Where(key => string.Equals(key.KeyId, kid, StringComparison.Ordinal))
                        .Cast<SecurityKey>()
                        .ToArray();

                    if (matches.Length > 0)
                    {
                        return matches;
                    }
                }

                return signingKeys;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure health checks
builder.Services.AddHealthChecks()
    .AddCheck<AlertHistoryHealthCheck>("alert-history")
    .AddCheck("sns", () =>
    {
        // Simple health check
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SNS service registered");
    });

var app = builder.Build();

app.UseSerilogRequestLogging();

// Apply webhook signature validation to webhook endpoints only
app.UseWebhookSignatureValidation(new PathString("/api/alerts/webhook"));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

static SymmetricSecurityKey CreateSymmetricKey(JwtSigningKeyOption option, int index)
{
    if (option == null)
    {
        throw new InvalidOperationException($"Authentication:JwtSigningKeys[{index}] is not configured.");
    }

    if (string.IsNullOrWhiteSpace(option.Key))
    {
        throw new InvalidOperationException($"Authentication:JwtSigningKeys[{index}].key is required.");
    }

    // NIST SP 800-107 recommends key length equal to hash output for HMAC
    // For HMAC-SHA256: 256 bits = 32 bytes = 43 base64 characters minimum
    // We require 64 characters (512 bits) for future-proofing and enhanced security
    if (option.Key.Length < 64)
    {
        throw new InvalidOperationException(
            $"Authentication:JwtSigningKeys[{index}].key must be at least 64 characters for HMAC-SHA256 security. " +
            $"Current length: {option.Key.Length}. NIST SP 800-107 requires key length >= hash output (256 bits). " +
            "Generate a secure 512-bit secret: openssl rand -base64 64");
    }

    // Validate entropy to prevent weak keys
    var entropyValidation = ValidateKeyEntropy(option.Key);
    if (!entropyValidation.IsValid)
    {
        throw new InvalidOperationException(
            $"Authentication:JwtSigningKeys[{index}].key has insufficient entropy: {entropyValidation.Reason}. " +
            "Generate a cryptographically secure secret with: openssl rand -base64 64");
    }

    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(option.Key))
    {
        KeyId = string.IsNullOrWhiteSpace(option.KeyId) ? $"key-{index + 1}" : option.KeyId
    };

    return securityKey;
}

/// <summary>
/// Validates the entropy of a JWT signing key to prevent weak keys.
/// Checks for common weak patterns like repeated characters, sequential patterns, etc.
/// </summary>
/// <param name="key">The key to validate</param>
/// <returns>Validation result with IsValid flag and Reason if invalid</returns>
static (bool IsValid, string? Reason) ValidateKeyEntropy(string key)
{
    if (string.IsNullOrWhiteSpace(key))
    {
        return (false, "Key is empty or whitespace");
    }

    // Check for excessive repeated characters (more than 30% of the key)
    var charGroups = key.GroupBy(c => c).OrderByDescending(g => g.Count()).ToArray();
    var mostCommonChar = charGroups.FirstOrDefault();
    if (mostCommonChar != null && mostCommonChar.Count() > key.Length * 0.3)
    {
        return (false, $"Key has excessive repeated characters ('{mostCommonChar.Key}' appears {mostCommonChar.Count()} times)");
    }

    // Check for sequential patterns (at least 8 sequential characters)
    for (int i = 0; i < key.Length - 7; i++)
    {
        bool isSequential = true;
        for (int j = 1; j < 8; j++)
        {
            if (key[i + j] != key[i + j - 1] + 1)
            {
                isSequential = false;
                break;
            }
        }
        if (isSequential)
        {
            return (false, $"Key contains sequential pattern at position {i}");
        }
    }

    // Check unique character count (require at least 16 unique characters)
    var uniqueChars = key.Distinct().Count();
    if (uniqueChars < 16)
    {
        return (false, $"Key has insufficient character diversity (only {uniqueChars} unique characters)");
    }

    // Check for common weak patterns
    var lowerKey = key.ToLowerInvariant();
    string[] weakPatterns =
    [
        "0000000000", "1111111111", "aaaaaaaaaa", "password", "secret", "admin",
        "test", "example", "change", "default", "xxxxxxxx"
    ];

    foreach (var pattern in weakPatterns)
    {
        if (lowerKey.Contains(pattern))
        {
            return (false, $"Key contains weak pattern: '{pattern}'");
        }
    }

    // All entropy checks passed
    return (true, null);
}

internal sealed record JwtSigningKeyOption
{
    public string Key { get; init; } = string.Empty;
    public string? KeyId { get; init; }
    public bool Active { get; init; }
}
