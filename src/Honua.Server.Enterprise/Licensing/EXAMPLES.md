# Honua License Management - Usage Examples

Complete examples for using the Honua License Management system.

## Setup

### 1. Configure Services

In your `Startup.cs` or `Program.cs`:

```csharp
using Honua.Server.Core.Licensing;

var builder = WebApplication.CreateBuilder(args);

// Add licensing services
builder.Services.AddHonuaLicensing(builder.Configuration);

var app = builder.Build();
```

### 2. Create Database Tables

```bash
# PostgreSQL
psql -U postgres -d honua_licenses < src/Honua.Server.Core/Licensing/Storage/migrations-postgres.sql

# Or from code (one-time migration)
dotnet run --migrate-licenses
```

## License Generation

### Generate a Free Tier License

```csharp
[ApiController]
[Route("api/admin/licenses")]
public class LicenseAdminController : ControllerBase
{
    private readonly ILicenseManager _licenseManager;
    private readonly ILogger<LicenseAdminController> _logger;

    public LicenseAdminController(
        ILicenseManager licenseManager,
        ILogger<LicenseAdminController> logger)
    {
        _licenseManager = licenseManager;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateLicense([FromBody] CreateLicenseRequest request)
    {
        try
        {
            var licenseRequest = new LicenseGenerationRequest
            {
                CustomerId = request.CustomerId,
                Email = request.Email,
                Tier = request.Tier,
                DurationDays = request.DurationDays
            };

            var license = await _licenseManager.GenerateLicenseAsync(licenseRequest);

            return Ok(new
            {
                licenseKey = license.LicenseKey,
                customerId = license.CustomerId,
                tier = license.Tier.ToString(),
                expiresAt = license.ExpiresAt,
                features = license.Features
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate license for customer {CustomerId}", request.CustomerId);
            return StatusCode(500, new { error = "Failed to generate license" });
        }
    }
}

public record CreateLicenseRequest(
    string CustomerId,
    string Email,
    LicenseTier Tier,
    int DurationDays);
```

### Generate with Custom Features

```csharp
[HttpPost("custom")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> GenerateCustomLicense([FromBody] CustomLicenseRequest request)
{
    var customFeatures = new LicenseFeatures
    {
        MaxUsers = request.MaxUsers,
        MaxCollections = request.MaxCollections,
        AdvancedAnalytics = request.EnableAnalytics,
        CloudIntegrations = request.EnableCloud,
        StacCatalog = request.EnableStac,
        RasterProcessing = request.EnableRaster,
        VectorTiles = true,
        PrioritySupport = request.Tier == LicenseTier.Enterprise,
        MaxApiRequestsPerDay = request.ApiQuota,
        MaxStorageGb = request.StorageQuotaGb
    };

    var licenseRequest = new LicenseGenerationRequest
    {
        CustomerId = request.CustomerId,
        Email = request.Email,
        Tier = request.Tier,
        DurationDays = request.DurationDays,
        CustomFeatures = customFeatures,
        Metadata = new Dictionary<string, string>
        {
            ["plan_name"] = request.PlanName,
            ["sales_rep"] = request.SalesRep
        }
    };

    var license = await _licenseManager.GenerateLicenseAsync(licenseRequest);

    return Ok(new { licenseKey = license.LicenseKey });
}
```

## License Validation

### API Endpoint Protection

```csharp
[ApiController]
[Route("api/data")]
public class DataController : ControllerBase
{
    private readonly ILicenseValidator _licenseValidator;

    public DataController(ILicenseValidator licenseValidator)
    {
        _licenseValidator = licenseValidator;
    }

    [HttpGet("collections")]
    public async Task<IActionResult> GetCollections(
        [FromHeader(Name = "X-License-Key")] string licenseKey)
    {
        // Validate license
        var validationResult = await _licenseValidator.ValidateAsync(licenseKey);

        if (!validationResult.IsValid)
        {
            return Unauthorized(new
            {
                error = validationResult.ErrorMessage,
                errorCode = validationResult.ErrorCode.ToString()
            });
        }

        var license = validationResult.License!;

        // Check tier requirement
        if (license.Tier < LicenseTier.Professional)
        {
            return StatusCode(403, new
            {
                error = "This feature requires Professional tier or higher",
                currentTier = license.Tier.ToString(),
                requiredTier = nameof(LicenseTier.Professional)
            });
        }

        // Check feature flag
        if (!license.Features.StacCatalog)
        {
            return StatusCode(403, new { error = "STAC catalog feature is not enabled" });
        }

        // Return data
        return Ok(new { collections = GetCollectionsForCustomer(license.CustomerId) });
    }

    private object GetCollectionsForCustomer(string customerId)
    {
        // Your implementation
        return new { /* collections */ };
    }
}
```

### Middleware-based License Validation

```csharp
public class LicenseValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILicenseValidator _licenseValidator;
    private readonly ILogger<LicenseValidationMiddleware> _logger;

    public LicenseValidationMiddleware(
        RequestDelegate next,
        ILicenseValidator licenseValidator,
        ILogger<LicenseValidationMiddleware> logger)
    {
        _next = next;
        _licenseValidator = licenseValidator;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for health checks, etc.
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Extract license key from header
        if (!context.Request.Headers.TryGetValue("X-License-Key", out var licenseKey) ||
            string.IsNullOrWhiteSpace(licenseKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "License key required" });
            return;
        }

        // Validate license
        var result = await _licenseValidator.ValidateAsync(licenseKey!);

        if (!result.IsValid)
        {
            _logger.LogWarning(
                "License validation failed: {ErrorCode} - {ErrorMessage}",
                result.ErrorCode,
                result.ErrorMessage);

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                error = result.ErrorMessage,
                errorCode = result.ErrorCode.ToString()
            });
            return;
        }

        // Add license info to HttpContext for downstream use
        context.Items["License"] = result.License;

        await _next(context);
    }
}

// Register in Startup/Program.cs
app.UseMiddleware<LicenseValidationMiddleware>();
```

## License Management Operations

### Upgrade a License

```csharp
[HttpPost("{customerId}/upgrade")]
[Authorize(Roles = "Admin,Sales")]
public async Task<IActionResult> UpgradeLicense(
    string customerId,
    [FromBody] UpgradeRequest request)
{
    try
    {
        var upgraded = await _licenseManager.UpgradeLicenseAsync(
            customerId,
            request.NewTier);

        _logger.LogInformation(
            "Upgraded customer {CustomerId} to {NewTier}",
            customerId,
            request.NewTier);

        // Optionally send email notification
        await SendUpgradeNotificationEmail(upgraded);

        return Ok(new
        {
            message = "License upgraded successfully",
            licenseKey = upgraded.LicenseKey,
            tier = upgraded.Tier.ToString(),
            features = upgraded.Features
        });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

public record UpgradeRequest(LicenseTier NewTier);
```

### Downgrade a License

```csharp
[HttpPost("{customerId}/downgrade")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> DowngradeLicense(
    string customerId,
    [FromBody] DowngradeRequest request)
{
    try
    {
        var downgraded = await _licenseManager.DowngradeLicenseAsync(
            customerId,
            request.NewTier);

        _logger.LogWarning(
            "Downgraded customer {CustomerId} to {NewTier}",
            customerId,
            request.NewTier);

        return Ok(new
        {
            message = "License downgraded",
            licenseKey = downgraded.LicenseKey,
            tier = downgraded.Tier.ToString()
        });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

public record DowngradeRequest(LicenseTier NewTier);
```

### Renew a License

```csharp
[HttpPost("{customerId}/renew")]
[Authorize(Roles = "Admin,Billing")]
public async Task<IActionResult> RenewLicense(
    string customerId,
    [FromBody] RenewRequest request)
{
    try
    {
        var renewed = await _licenseManager.RenewLicenseAsync(
            customerId,
            request.ExtensionDays);

        return Ok(new
        {
            message = "License renewed successfully",
            licenseKey = renewed.LicenseKey,
            expiresAt = renewed.ExpiresAt,
            daysAdded = request.ExtensionDays
        });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

public record RenewRequest(int ExtensionDays);
```

### Revoke a License

```csharp
[HttpPost("{customerId}/revoke")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> RevokeLicense(
    string customerId,
    [FromBody] RevokeRequest request)
{
    try
    {
        var revokedBy = User.Identity?.Name ?? "System";

        await _licenseManager.RevokeLicenseAsync(
            customerId,
            request.Reason,
            revokedBy);

        _logger.LogWarning(
            "Revoked license for customer {CustomerId}. Reason: {Reason}, By: {RevokedBy}",
            customerId,
            request.Reason,
            revokedBy);

        return Ok(new
        {
            message = "License revoked successfully",
            credentialsRevoked = true
        });
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}

public record RevokeRequest(string Reason);
```

## Customer Self-Service Portal

### Check License Status

```csharp
[ApiController]
[Route("api/my/license")]
[Authorize]
public class CustomerLicenseController : ControllerBase
{
    private readonly ILicenseStore _licenseStore;

    [HttpGet]
    public async Task<IActionResult> GetMyLicense()
    {
        var customerId = User.FindFirst("customer_id")?.Value;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return BadRequest(new { error = "Customer ID not found in token" });
        }

        var license = await _licenseStore.GetByCustomerIdAsync(customerId);
        if (license == null)
        {
            return NotFound(new { error = "License not found" });
        }

        return Ok(new
        {
            tier = license.Tier.ToString(),
            status = license.Status.ToString(),
            issuedAt = license.IssuedAt,
            expiresAt = license.ExpiresAt,
            daysUntilExpiration = license.DaysUntilExpiration(),
            features = license.Features,
            isValid = license.IsValid()
        });
    }
}
```

### Request License Renewal

```csharp
[HttpPost("renew")]
public async Task<IActionResult> RequestRenewal([FromBody] RenewalRequest request)
{
    var customerId = User.FindFirst("customer_id")?.Value;

    // Create renewal ticket in billing system
    var ticket = await CreateRenewalTicket(customerId, request.DurationDays);

    return Ok(new
    {
        message = "Renewal request submitted",
        ticketId = ticket.Id,
        estimatedCost = CalculateRenewalCost(request.DurationDays)
    });
}
```

## Background Service Integration

### Manual Trigger for Testing

```csharp
[ApiController]
[Route("api/admin/licensing/jobs")]
[Authorize(Roles = "Admin")]
public class LicensingJobsController : ControllerBase
{
    private readonly ICredentialRevocationService _revocationService;
    private readonly ILicenseStore _licenseStore;

    [HttpPost("check-expirations")]
    public async Task<IActionResult> CheckExpirations()
    {
        var expiredLicenses = await _licenseStore.GetExpiredLicensesAsync();

        return Ok(new
        {
            expiredCount = expiredLicenses.Length,
            customers = expiredLicenses.Select(l => new
            {
                customerId = l.CustomerId,
                expiresAt = l.ExpiresAt,
                tier = l.Tier.ToString()
            })
        });
    }

    [HttpPost("revoke-expired")]
    public async Task<IActionResult> RevokeExpiredCredentials()
    {
        await _revocationService.RevokeExpiredCredentialsAsync();

        return Ok(new { message = "Credential revocation job completed" });
    }
}
```

## Testing

### Unit Test Examples

```csharp
public class LicenseManagerTests
{
    [Fact]
    public async Task GenerateLicense_CreatesValidLicense()
    {
        // Arrange
        var request = new LicenseGenerationRequest
        {
            CustomerId = "test-customer",
            Email = "test@example.com",
            Tier = LicenseTier.Professional,
            DurationDays = 365
        };

        // Act
        var license = await _licenseManager.GenerateLicenseAsync(request);

        // Assert
        Assert.NotNull(license);
        Assert.Equal("test-customer", license.CustomerId);
        Assert.Equal(LicenseTier.Professional, license.Tier);
        Assert.True(license.IsValid());
    }

    [Fact]
    public async Task UpgradeLicense_UpdatesTierAndFeatures()
    {
        // Arrange
        var customerId = "test-customer";
        await CreateTestLicense(customerId, LicenseTier.Free);

        // Act
        var upgraded = await _licenseManager.UpgradeLicenseAsync(
            customerId,
            LicenseTier.Professional);

        // Assert
        Assert.Equal(LicenseTier.Professional, upgraded.Tier);
        Assert.True(upgraded.Features.CloudIntegrations);
    }
}
```

### Integration Test Example

```csharp
public class LicenseValidationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public LicenseValidationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidLicense_AllowsAccess()
    {
        // Arrange
        var licenseKey = await GenerateTestLicense();

        // Act
        _client.DefaultRequestHeaders.Add("X-License-Key", licenseKey);
        var response = await _client.GetAsync("/api/data/collections");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredLicense_ReturnsUnauthorized()
    {
        // Arrange
        var expiredLicenseKey = await GenerateExpiredTestLicense();

        // Act
        _client.DefaultRequestHeaders.Add("X-License-Key", expiredLicenseKey);
        var response = await _client.GetAsync("/api/data/collections");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

## CLI Tools

### Generate License (CLI)

```csharp
// Program.cs
if (args.Length > 0 && args[0] == "generate-license")
{
    var customerId = args[1];
    var email = args[2];
    var tier = Enum.Parse<LicenseTier>(args[3]);
    var days = int.Parse(args[4]);

    var licenseManager = app.Services.GetRequiredService<ILicenseManager>();

    var license = await licenseManager.GenerateLicenseAsync(new LicenseGenerationRequest
    {
        CustomerId = customerId,
        Email = email,
        Tier = tier,
        DurationDays = days
    });

    Console.WriteLine($"License Key: {license.LicenseKey}");
    Console.WriteLine($"Expires: {license.ExpiresAt}");

    return 0;
}
```

Usage:
```bash
dotnet run -- generate-license customer-123 customer@example.com Professional 365
```

## Monitoring and Alerts

### Health Check

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<LicensingHealthCheck>("licensing");

public class LicensingHealthCheck : IHealthCheck
{
    private readonly ILicenseStore _licenseStore;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Test database connectivity
            var expiring = await _licenseStore.GetExpiringLicensesAsync(7, cancellationToken);

            return HealthCheckResult.Healthy($"Found {expiring.Length} expiring licenses");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("License database unavailable", ex);
        }
    }
}
```

### Metrics

```csharp
public class LicenseMetrics
{
    private readonly ILicenseStore _licenseStore;

    public async Task<LicenseStatistics> GetStatisticsAsync()
    {
        var allLicenses = await _licenseStore.GetAllLicensesAsync();

        return new LicenseStatistics
        {
            TotalActive = allLicenses.Count(l => l.Status == LicenseStatus.Active),
            TotalExpired = allLicenses.Count(l => l.Status == LicenseStatus.Expired),
            TotalRevoked = allLicenses.Count(l => l.Status == LicenseStatus.Revoked),
            ByTier = allLicenses
                .GroupBy(l => l.Tier)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
    }
}
```
