---
name: 'TODO-003: Extract Tenant ID from Claims for Multi-Tenancy'
about: Implement proper tenant isolation by extracting tenant ID from authentication claims
title: '[P0] Extract Tenant ID from Claims for Multi-Tenancy'
labels: ['priority: critical', 'security', 'multi-tenancy', 'todo-cleanup']
assignees: []
---

## Summary

Multiple controllers have TODO comments indicating they need to extract tenant ID from claims or context, but currently use hardcoded values or are missing tenant isolation entirely. This is a critical security issue for multi-tenant deployments.

**Priority:** P0 - Critical (Security & Data Isolation)
**Effort:** Medium (2-3 days)
**Sprint Target:** Sprint 1

## Context

### Files Affected

| File | Line | Current Issue |
|------|------|---------------|
| `GeoEventController.cs` | 345 | `TODO: Extract tenant ID from claims or context` |
| `AzureStreamAnalyticsController.cs` | 291 | `TODO: Extract tenant ID from claims or context` |
| `GeofencesController.cs` | 266 | `TODO: Extract tenant ID from claims or context` |

### Current Implementation

```csharp
// GeoEventController.cs:345
public async Task<IActionResult> GetGeoEvents([FromQuery] GeoEventQuery query)
{
    var tenantId = "default"; // TODO: Extract tenant ID from claims or context

    var events = await _geoEventStore.GetGeoEventsAsync(tenantId, query);
    return Ok(events);
}
```

### Problem

1. **Data leakage** - Tenant A could access Tenant B's data
2. **Security vulnerability** - No tenant isolation enforcement
3. **Compliance violation** - Violates data privacy regulations (GDPR, HIPAA, SOC 2)
4. **Incorrect behavior** - All tenants see the same "default" tenant's data

## Expected Behavior

### 1. Add Tenant ID to Claims

**During Authentication (JWT Token Issuer):**

```csharp
var claims = new List<Claim>
{
    new Claim("sub", user.Id),
    new Claim("name", user.Name),
    new Claim("email", user.Email),
    new Claim("tenant_id", user.TenantId), // ✅ Add tenant ID
    new Claim("role", user.Role)
};

var token = new JwtSecurityToken(
    issuer: _configuration["Jwt:Issuer"],
    audience: _configuration["Jwt:Audience"],
    claims: claims,
    expires: DateTime.UtcNow.AddHours(8),
    signingCredentials: credentials
);
```

### 2. Extend IUserIdentityService

```csharp
public interface IUserIdentityService
{
    string GetCurrentUserId();
    string GetCurrentUserName();
    string? GetCurrentUserEmail();

    /// <summary>
    /// Gets the current authenticated user's tenant ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when tenant_id claim is missing.</exception>
    string GetCurrentTenantId();

    /// <summary>
    /// Tries to get the current tenant ID. Returns false if not found.
    /// </summary>
    bool TryGetCurrentTenantId([NotNullWhen(true)] out string? tenantId);

    /// <summary>
    /// Checks if the current user is a super admin (can access all tenants).
    /// </summary>
    bool IsSuperAdmin();
}

public class HttpContextUserIdentityService : IUserIdentityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUserIdentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available");

        return httpContext.User.FindFirst("tenant_id")?.Value
            ?? throw new InvalidOperationException(
                "Tenant ID not found in claims. Ensure the user is authenticated with a tenant-scoped token.");
    }

    public bool TryGetCurrentTenantId([NotNullWhen(true)] out string? tenantId)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        tenantId = httpContext?.User.FindFirst("tenant_id")?.Value;
        return tenantId != null;
    }

    public bool IsSuperAdmin()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.User.IsInRole("SuperAdmin") ?? false;
    }

    // ... other methods
}
```

### 3. Update Controllers

```csharp
// GeoEventController.cs
public class GeoEventController : ControllerBase
{
    private readonly IGeoEventStore _geoEventStore;
    private readonly IUserIdentityService _userIdentity;
    private readonly ILogger<GeoEventController> _logger;

    public GeoEventController(
        IGeoEventStore geoEventStore,
        IUserIdentityService userIdentity,
        ILogger<GeoEventController> logger)
    {
        _geoEventStore = geoEventStore;
        _userIdentity = userIdentity;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetGeoEvents([FromQuery] GeoEventQuery query)
    {
        // ✅ Extract tenant ID from claims
        var tenantId = _userIdentity.GetCurrentTenantId();

        _logger.LogInformation(
            "Fetching geo events for tenant {TenantId} by user {UserId}",
            tenantId,
            _userIdentity.GetCurrentUserId());

        var events = await _geoEventStore.GetGeoEventsAsync(tenantId, query);
        return Ok(events);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateGeoEvent([FromBody] CreateGeoEventRequest request)
    {
        var tenantId = _userIdentity.GetCurrentTenantId();
        var userId = _userIdentity.GetCurrentUserId();

        var geoEvent = new GeoEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId, // ✅ Set tenant ID
            Type = request.Type,
            Geometry = request.Geometry,
            Properties = request.Properties,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _geoEventStore.CreateGeoEventAsync(geoEvent);

        return Created($"/api/geoevents/{geoEvent.Id}", geoEvent);
    }
}
```

### 4. Add Tenant Isolation Filter (Optional but Recommended)

Create a global filter to enforce tenant isolation:

```csharp
public class TenantIsolationFilter : IAsyncActionFilter
{
    private readonly IUserIdentityService _userIdentity;
    private readonly ILogger<TenantIsolationFilter> _logger;

    public TenantIsolationFilter(
        IUserIdentityService userIdentity,
        ILogger<TenantIsolationFilter> logger)
    {
        _userIdentity = userIdentity;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        // Skip for super admins
        if (_userIdentity.IsSuperAdmin())
        {
            await next();
            return;
        }

        // Ensure tenant ID is present for authenticated users
        if (context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            if (!_userIdentity.TryGetCurrentTenantId(out var tenantId))
            {
                _logger.LogWarning(
                    "Authenticated user {UserId} is missing tenant_id claim",
                    _userIdentity.GetCurrentUserId());

                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "tenant_missing",
                    message = "Your account is not associated with a tenant. Please contact support."
                });
                return;
            }

            // Add tenant ID to HttpContext.Items for easy access
            context.HttpContext.Items["TenantId"] = tenantId;
        }

        await next();
    }
}
```

Register the filter:

```csharp
services.AddControllers(options =>
{
    options.Filters.Add<TenantIsolationFilter>();
});
```

## Acceptance Criteria

- [ ] `IUserIdentityService` updated with `GetCurrentTenantId()` and `IsSuperAdmin()` methods
- [ ] All controllers extract tenant ID from claims (no hardcoded "default" values)
- [ ] Tenant ID is validated and present in claims for all authenticated requests
- [ ] Super admins can bypass tenant isolation (access all tenants)
- [ ] Unit tests verify tenant isolation in all multi-tenant endpoints
- [ ] Integration tests verify Tenant A cannot access Tenant B's data
- [ ] Audit log includes tenant ID for all operations

## Testing Checklist

### Unit Tests

```csharp
[Fact]
public void GetCurrentTenantId_WithTenantClaim_ReturnsTenantId()
{
    // Arrange
    var claims = new[]
    {
        new Claim("sub", "user-123"),
        new Claim("tenant_id", "tenant-abc")
    };
    var identity = new ClaimsIdentity(claims, "Test");
    var principal = new ClaimsPrincipal(identity);

    var httpContext = new DefaultHttpContext { User = principal };
    var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

    var service = new HttpContextUserIdentityService(httpContextAccessor);

    // Act
    var tenantId = service.GetCurrentTenantId();

    // Assert
    Assert.Equal("tenant-abc", tenantId);
}

[Fact]
public void GetCurrentTenantId_WithoutTenantClaim_ThrowsException()
{
    // Arrange
    var claims = new[] { new Claim("sub", "user-123") };
    var identity = new ClaimsIdentity(claims, "Test");
    var principal = new ClaimsPrincipal(identity);

    var httpContext = new DefaultHttpContext { User = principal };
    var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

    var service = new HttpContextUserIdentityService(httpContextAccessor);

    // Act & Assert
    var exception = Assert.Throws<InvalidOperationException>(() => service.GetCurrentTenantId());
    Assert.Contains("Tenant ID not found", exception.Message);
}
```

### Integration Tests

```csharp
[Fact]
public async Task GetGeoEvents_ReturnOnlyCurrentTenantEvents()
{
    // Arrange
    var client1 = _factory.CreateClientWithUser("user1", tenantId: "tenant-1");
    var client2 = _factory.CreateClientWithUser("user2", tenantId: "tenant-2");

    // Create events for both tenants
    await SeedGeoEvents("tenant-1", count: 5);
    await SeedGeoEvents("tenant-2", count: 3);

    // Act
    var response1 = await client1.GetAsync("/api/geoevents");
    var response2 = await client2.GetAsync("/api/geoevents");

    // Assert
    var events1 = await response1.Content.ReadFromJsonAsync<List<GeoEvent>>();
    var events2 = await response2.Content.ReadFromJsonAsync<List<GeoEvent>>();

    Assert.Equal(5, events1.Count);
    Assert.All(events1, e => Assert.Equal("tenant-1", e.TenantId));

    Assert.Equal(3, events2.Count);
    Assert.All(events2, e => Assert.Equal("tenant-2", e.TenantId));
}

[Fact]
public async Task CreateGeoEvent_SetsTenantIdFromClaims()
{
    // Arrange
    var client = _factory.CreateClientWithUser("user1", tenantId: "tenant-1");
    var request = new CreateGeoEventRequest
    {
        Type = "geofence_entry",
        Geometry = new Point(new Position(10, 20))
    };

    // Act
    var response = await client.PostAsJsonAsync("/api/geoevents", request);

    // Assert
    response.EnsureSuccessStatusCode();
    var geoEvent = await response.Content.ReadFromJsonAsync<GeoEvent>();
    Assert.Equal("tenant-1", geoEvent.TenantId); // ✅ Set from claims, not request body
}
```

## Related Files

- `/home/user/Honua.Server/src/Honua.Server.Host/GeoEvent/GeoEventController.cs:345`
- `/home/user/Honua.Server/src/Honua.Server.Host/GeoEvent/AzureStreamAnalyticsController.cs:291`
- `/home/user/Honua.Server/src/Honua.Server.Host/GeoEvent/GeofencesController.cs:266`

## Related Issues

- #TBD-001 - Add Authorization to Admin Endpoints
- #TBD-002 - Extract User Identity from Authentication Context

## References

- [Multi-tenancy in ASP.NET Core](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models)
- [Claims-based Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims)
- [Data Isolation Patterns](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/overview)

## Security Considerations

⚠️ **CRITICAL:** Missing tenant isolation is a severe security vulnerability.

**Risk Assessment:**
- **Impact:** Critical - Data leakage between tenants
- **Likelihood:** High - Current code does not enforce tenant isolation
- **Overall Risk:** Critical

**Compliance Impact:**
- GDPR - Requires strict data segregation
- HIPAA - Requires isolation of protected health information (PHI)
- SOC 2 - Requires logical access controls to prevent data leakage
- ISO 27001 - Requires multi-tenancy security controls

**Recommended Actions:**
1. Implement this fix immediately
2. Audit all existing data for potential cross-tenant leakage
3. Add automated tests for tenant isolation to CI/CD
4. Perform security audit after implementation
