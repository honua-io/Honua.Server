---
name: 'TODO-001: Add Authorization to Admin Endpoints'
about: Implement proper authorization checks for all admin endpoints
title: '[P0] Add Authorization to Admin Endpoints'
labels: ['priority: critical', 'security', 'authentication', 'todo-cleanup']
assignees: []
---

## Summary

Multiple admin endpoints are missing proper authorization checks. Currently, they rely on the `.RequireAuthorization("RequireAdministrator")` policy but have TODO comments indicating incomplete auth integration.

**Priority:** P0 - Critical (Security)
**Effort:** Medium (3-5 days)
**Sprint Target:** Sprint 1

## Context

The following admin endpoint files have placeholder authorization:

### Files Affected

1. **AlertAdministrationEndpoints.cs** (Line 34)
2. **ServerAdministrationEndpoints.cs** (Line 30)
3. **MetadataAdministrationEndpoints.cs** (Line 31)
4. **FeatureFlagEndpoints.cs** (Line 28)

### Current Implementation

```csharp
public static class AlertAdministrationEndpoints
{
    public static void MapAlertAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/alerts")
            .WithTags("Admin - Alerts")
            .WithOpenApi()
            .RequireAuthorization("RequireAdministrator"); // TODO: Add authorization after auth integration

        // ... endpoint mappings
    }
}
```

### Problem

1. **Authorization policy may not be fully configured** - The "RequireAdministrator" policy needs to be verified in `Program.cs` or authorization configuration
2. **No role-based access control (RBAC)** - Different admin functions should have different permission levels
3. **No audit logging** - Admin actions are not being logged for compliance
4. **No tenant isolation** - Admin endpoints may leak data across tenants

## Expected Behavior

### 1. Define Authorization Policies

In `Program.cs` or `ServiceCollectionExtensions.cs`:

```csharp
services.AddAuthorization(options =>
{
    // Super admin - full system access
    options.AddPolicy("RequireSuperAdministrator", policy =>
        policy.RequireRole("SuperAdmin")
              .RequireClaim("scope", "admin:full"));

    // Alert admin - manage alerts and notifications
    options.AddPolicy("RequireAlertAdministrator", policy =>
        policy.RequireRole("Admin", "AlertAdmin")
              .RequireClaim("scope", "admin:alerts"));

    // Metadata admin - manage data sources and services
    options.AddPolicy("RequireMetadataAdministrator", policy =>
        policy.RequireRole("Admin", "MetadataAdmin")
              .RequireClaim("scope", "admin:metadata"));

    // Feature flag admin - manage feature flags
    options.AddPolicy("RequireFeatureFlagAdministrator", policy =>
        policy.RequireRole("Admin")
              .RequireClaim("scope", "admin:features"));
});
```

### 2. Update Endpoint Authorization

```csharp
public static class AlertAdministrationEndpoints
{
    public static void MapAlertAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/admin/alerts")
            .WithTags("Admin - Alerts")
            .WithOpenApi()
            .RequireAuthorization("RequireAlertAdministrator"); // Specific policy

        // ... endpoint mappings
    }
}
```

### 3. Add Audit Logging

Create an audit logging filter or middleware:

```csharp
public class AdminAuditLoggingFilter : IEndpointFilter
{
    private readonly IAuditLogger _auditLogger;

    public AdminAuditLoggingFilter(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;
        var endpoint = httpContext.GetEndpoint()?.DisplayName;

        await _auditLogger.LogAdminActionAsync(new AuditLogEntry
        {
            UserId = user.FindFirst("sub")?.Value,
            UserName = user.Identity?.Name,
            Action = httpContext.Request.Method,
            Resource = endpoint,
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext.Request.Headers["User-Agent"].ToString()
        });

        return await next(context);
    }
}
```

Apply to admin endpoints:

```csharp
var group = endpoints.MapGroup("/admin/alerts")
    .WithTags("Admin - Alerts")
    .WithOpenApi()
    .RequireAuthorization("RequireAlertAdministrator")
    .AddEndpointFilter<AdminAuditLoggingFilter>(); // Add audit logging
```

### 4. Add Tenant Isolation

Ensure admin operations are scoped to the user's tenant (unless super admin):

```csharp
public static async Task<IResult> GetAlertRules(
    HttpContext context,
    IAlertRuleStore alertRuleStore,
    ILogger<AlertAdministrationEndpoints> logger)
{
    var tenantId = context.User.FindFirst("tenant_id")?.Value;

    // Super admins can see all tenants
    var isSuperAdmin = context.User.IsInRole("SuperAdmin");

    var rules = isSuperAdmin
        ? await alertRuleStore.GetAllAlertRulesAsync() // All tenants
        : await alertRuleStore.GetAlertRulesByTenantAsync(tenantId); // Scoped to tenant

    return Results.Ok(rules);
}
```

## Acceptance Criteria

- [ ] All admin endpoint groups use specific authorization policies (not generic "RequireAdministrator")
- [ ] Authorization policies are defined with appropriate roles and claims
- [ ] Admin actions are logged to audit log (user, action, timestamp, IP, result)
- [ ] Tenant isolation is enforced (admins can only manage their tenant's resources, unless super admin)
- [ ] Unit tests verify authorization policies are applied correctly
- [ ] Integration tests verify unauthorized users receive 403 Forbidden
- [ ] Documentation updated with admin role definitions and permissions matrix

## Testing Checklist

### Unit Tests

```csharp
[Fact]
public async Task GetAlertRules_WithoutAdminRole_Returns403()
{
    // Arrange
    var user = CreateUserWithRole("User"); // Not admin

    // Act
    var result = await GetAlertRules(user, ...);

    // Assert
    Assert.IsType<ForbidResult>(result);
}

[Fact]
public async Task GetAlertRules_WithAdminRole_ReturnsOnlyTenantRules()
{
    // Arrange
    var user = CreateUserWithRole("Admin", tenantId: "tenant-1");

    // Act
    var result = await GetAlertRules(user, ...);

    // Assert
    var rules = Assert.IsType<OkObjectResult>(result).Value as List<AlertRule>;
    Assert.All(rules, rule => Assert.Equal("tenant-1", rule.TenantId));
}
```

### Integration Tests

```csharp
[Fact]
public async Task AdminEndpoints_RequireAuthentication()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act - No auth header
    var response = await client.GetAsync("/admin/alerts/rules");

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task AdminEndpoints_RequireAdminRole()
{
    // Arrange
    var client = _factory.CreateClientWithUser("user", roles: new[] { "User" });

    // Act
    var response = await client.GetAsync("/admin/alerts/rules");

    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

## Related Files

- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/AlertAdministrationEndpoints.cs:34`
- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/ServerAdministrationEndpoints.cs:30`
- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/MetadataAdministrationEndpoints.cs:31`
- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/FeatureFlagEndpoints.cs:28`
- `/home/user/Honua.Server/src/Honua.Server.Host/Program.cs` (authorization configuration)
- `/home/user/Honua.Server/src/Honua.Server.Host/Extensions/ServiceCollectionExtensions.cs` (DI registration)

## Related Issues

- #TBD-002 - Extract User Identity from Authentication Context
- #TBD-003 - Extract Tenant ID from Claims for Multi-Tenancy

## References

- [ASP.NET Core Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction)
- [Policy-based Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies)
- [Role-based Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles)

## Security Considerations

⚠️ **CRITICAL:** This is a security issue. Admin endpoints currently may not be properly protected.

**Risk Assessment:**
- **Impact:** High - Unauthorized access to admin functions could compromise system
- **Likelihood:** Medium - Depends on whether RequireAdministrator policy is configured
- **Overall Risk:** High

**Mitigation:**
- Verify authorization is enforced in integration tests
- Perform security audit after implementation
- Document admin role requirements in security documentation
