---
name: 'TODO-002: Extract User Identity from Authentication Context'
about: Replace hardcoded user identities with values extracted from authentication context
title: '[P0] Extract User Identity from Authentication Context'
labels: ['priority: critical', 'security', 'authentication', 'todo-cleanup']
assignees: []
---

## Summary

Multiple endpoints use hardcoded user identities (e.g., `"admin"`, `Guid.Empty`) instead of extracting the actual authenticated user from the authentication context. This breaks audit trails, accountability, and security.

**Priority:** P0 - Critical (Security & Audit Compliance)
**Effort:** Small (1-2 days)
**Sprint Target:** Sprint 1

## Context

### Files Affected

| File | Lines | Hardcoded Value | Field |
|------|-------|-----------------|-------|
| `AlertAdministrationEndpoints.cs` | 218, 289, 488, 549, 856 | `"admin"` | CreatedBy, ModifiedBy |
| `SamlEndpoints.cs` | 186 | `Guid.Empty` | Session ID |

### Current Implementation

```csharp
// AlertAdministrationEndpoints.cs:218
var alertRule = new AlertRule
{
    // ... other properties
    CreatedBy = "admin" // TODO: Get from authentication context
};

// SamlEndpoints.cs:186
var samlResponse = new SamlResponse
{
    SessionId = Guid.Empty, // TODO: Get from session
    // ... other properties
};
```

### Problem

1. **Broken audit trail** - Cannot track who created/modified alerts
2. **Security compliance** - Violates audit requirements (SOC 2, ISO 27001, GDPR)
3. **No accountability** - All actions appear to be performed by "admin"
4. **Testing issues** - Cannot verify multi-user scenarios

## Expected Behavior

### 1. Create User Identity Service

```csharp
public interface IUserIdentityService
{
    /// <summary>
    /// Gets the current authenticated user's ID.
    /// </summary>
    /// <returns>User ID from claims (e.g., "sub" claim).</returns>
    string GetCurrentUserId();

    /// <summary>
    /// Gets the current authenticated user's name.
    /// </summary>
    /// <returns>User name from claims (e.g., "name" or "preferred_username").</returns>
    string GetCurrentUserName();

    /// <summary>
    /// Gets the current authenticated user's email.
    /// </summary>
    /// <returns>Email from claims (e.g., "email").</returns>
    string? GetCurrentUserEmail();

    /// <summary>
    /// Gets the current authenticated user's tenant ID.
    /// </summary>
    /// <returns>Tenant ID from claims (e.g., "tenant_id").</returns>
    string GetCurrentTenantId();

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}

public class HttpContextUserIdentityService : IUserIdentityService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextUserIdentityService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available");

        return httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
    }

    public string GetCurrentUserName()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available");

        return httpContext.User.FindFirst("name")?.Value
            ?? httpContext.User.FindFirst("preferred_username")?.Value
            ?? httpContext.User.Identity?.Name
            ?? throw new InvalidOperationException("User name not found in claims");
    }

    public string? GetCurrentUserEmail()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.User.FindFirst("email")?.Value
            ?? httpContext?.User.FindFirst(ClaimTypes.Email)?.Value;
    }

    public string GetCurrentTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext is not available");

        return httpContext.User.FindFirst("tenant_id")?.Value
            ?? throw new InvalidOperationException("Tenant ID not found in claims");
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
```

### 2. Register Service

```csharp
// ServiceCollectionExtensions.cs
services.AddHttpContextAccessor();
services.AddScoped<IUserIdentityService, HttpContextUserIdentityService>();
```

### 3. Update Endpoints

```csharp
public static async Task<IResult> CreateAlertRule(
    CreateAlertRuleRequest request,
    IAlertRuleStore alertRuleStore,
    IUserIdentityService userIdentity, // Inject user identity service
    ILogger<AlertAdministrationEndpoints> logger)
{
    try
    {
        var alertRule = new AlertRule
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Severity = request.Severity,
            Matchers = request.Matchers,
            NotificationChannelIds = request.NotificationChannelIds,
            IsEnabled = request.IsEnabled ?? true,
            CreatedBy = userIdentity.GetCurrentUserName(), // ✅ From auth context
            CreatedAt = DateTime.UtcNow
        };

        await alertRuleStore.CreateAlertRuleAsync(alertRule);

        logger.LogInformation(
            "Alert rule created: {RuleName} by {User}",
            alertRule.Name,
            alertRule.CreatedBy);

        return Results.Created($"/admin/alerts/rules/{alertRule.Id}", alertRule);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to create alert rule");
        return Results.Problem(
            title: "Failed to create alert rule",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
```

### 4. Update SAML Session

```csharp
// SamlEndpoints.cs
public static IResult ProcessSamlResponse(
    [FromBody] SamlResponseRequest request,
    ISamlService samlService,
    ISessionStore sessionStore, // Add session store
    ILogger<SamlEndpoints> logger)
{
    try
    {
        var validationResult = samlService.ValidateResponse(request.SamlResponse);

        if (!validationResult.IsValid)
        {
            return Results.BadRequest(new { error = validationResult.Error });
        }

        // Create session
        var sessionId = Guid.NewGuid();
        var session = new UserSession
        {
            SessionId = sessionId,
            UserId = validationResult.UserId,
            UserName = validationResult.UserName,
            Email = validationResult.Email,
            TenantId = validationResult.TenantId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(8)
        };

        await sessionStore.CreateSessionAsync(session);

        return Results.Ok(new SamlAuthenticationResponse
        {
            Success = true,
            SessionId = sessionId, // ✅ From newly created session
            UserId = validationResult.UserId,
            UserName = validationResult.UserName,
            RedirectUrl = validationResult.RedirectUrl ?? "/"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "SAML response processing failed");
        return Results.Problem("SAML authentication failed", statusCode: 500);
    }
}
```

## Acceptance Criteria

- [ ] `IUserIdentityService` interface created with methods to extract user info from claims
- [ ] `HttpContextUserIdentityService` implementation created
- [ ] Service registered in DI container
- [ ] All hardcoded `"admin"` strings replaced with `userIdentity.GetCurrentUserName()`
- [ ] All hardcoded `Guid.Empty` session IDs replaced with actual session creation
- [ ] Unit tests verify service extracts claims correctly
- [ ] Integration tests verify CreatedBy/ModifiedBy fields are populated from auth context
- [ ] Audit log shows actual user names, not "admin"

## Testing Checklist

### Unit Tests

```csharp
[Fact]
public void GetCurrentUserId_WithSubClaim_ReturnsUserId()
{
    // Arrange
    var claims = new[]
    {
        new Claim("sub", "user-123"),
        new Claim("name", "John Doe")
    };
    var identity = new ClaimsIdentity(claims, "Test");
    var principal = new ClaimsPrincipal(identity);

    var httpContext = new DefaultHttpContext { User = principal };
    var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };

    var service = new HttpContextUserIdentityService(httpContextAccessor);

    // Act
    var userId = service.GetCurrentUserId();

    // Assert
    Assert.Equal("user-123", userId);
}

[Fact]
public void GetCurrentUserId_WithoutClaims_ThrowsException()
{
    // Arrange
    var httpContext = new DefaultHttpContext();
    var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
    var service = new HttpContextUserIdentityService(httpContextAccessor);

    // Act & Assert
    Assert.Throws<InvalidOperationException>(() => service.GetCurrentUserId());
}
```

### Integration Tests

```csharp
[Fact]
public async Task CreateAlertRule_SetsCreatedByFromAuthContext()
{
    // Arrange
    var client = _factory.CreateClientWithUser("john.doe", userId: "user-123");
    var request = new CreateAlertRuleRequest
    {
        Name = "Test Alert",
        Severity = "Critical"
    };

    // Act
    var response = await client.PostAsJsonAsync("/admin/alerts/rules", request);

    // Assert
    response.EnsureSuccessStatusCode();
    var alertRule = await response.Content.ReadFromJsonAsync<AlertRule>();
    Assert.Equal("john.doe", alertRule.CreatedBy); // Not "admin"
}
```

## Related Files

- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/AlertAdministrationEndpoints.cs:218,289,488,549,856`
- `/home/user/Honua.Server/src/Honua.Server.Host/Authentication/SamlEndpoints.cs:186`
- `/home/user/Honua.Server/src/Honua.Server.Host/Extensions/ServiceCollectionExtensions.cs` (DI registration)

## Related Issues

- #TBD-001 - Add Authorization to Admin Endpoints
- #TBD-003 - Extract Tenant ID from Claims for Multi-Tenancy

## References

- [ClaimsPrincipal in ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/api/system.security.claims.claimsprincipal)
- [IHttpContextAccessor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.ihttpcontextaccessor)
- [Claims-based Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/claims)

## Security Considerations

⚠️ **CRITICAL:** Hardcoded user identities break audit trails and accountability.

**Risk Assessment:**
- **Impact:** High - Cannot trace who performed admin actions
- **Likelihood:** High - Currently all actions show "admin"
- **Overall Risk:** High

**Compliance Impact:**
- SOC 2 Type II - Requires audit trails with user attribution
- ISO 27001 - Requires accountability for privileged actions
- GDPR - Requires tracking of data access and modifications
