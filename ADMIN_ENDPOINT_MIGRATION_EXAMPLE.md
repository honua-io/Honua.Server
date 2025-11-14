# Admin Endpoint Security Migration Example

This document provides before/after examples showing how to update admin endpoints to use the new security services.

## Overview

The new security services resolve critical TODOs:
- **TODO-002**: User identity extraction from authentication context
- **TODO-001**: Granular admin authorization policies
- **Compliance**: Comprehensive audit logging for SOC 2, ISO 27001, GDPR

## Required Changes

### 1. Add Service Dependencies

Inject the new security services into your endpoint handlers:

```csharp
// Add these service parameters to your endpoint methods:
[FromServices] IUserIdentityService userIdentityService
[FromServices] IAuditLoggingService auditLoggingService
```

### 2. Replace Hardcoded User Identity

Replace hardcoded `"admin"` or `Guid.Empty` with proper user extraction.

### 3. Add Audit Logging

Add audit logging for all admin operations (create, update, delete, publish, etc.).

### 4. Apply Granular Authorization

Update authorization policies from generic `RequireAdministrator` to specific policies.

---

## Example 1: Alert Rule Creation

### BEFORE (with TODOs violated)

```csharp
public static async Task<IResult> CreateAlertRule(
    [FromBody] CreateAlertRuleRequest request,
    [FromServices] IAlertConfigurationService alertService,
    [FromServices] ILogger<AlertAdministrationEndpoints> logger)
{
    // ❌ TODO-002 VIOLATION: Hardcoded user
    var createdBy = "admin";

    var rule = await alertService.CreateAlertRuleAsync(
        name: request.Name,
        description: request.Description,
        condition: request.Condition,
        severity: request.Severity,
        channels: request.Channels,
        createdBy: createdBy); // ❌ Hardcoded - breaks audit trail

    logger.LogInformation("Alert rule created: {RuleName}", rule.Name);

    return Results.Created($"/admin/alerts/rules/{rule.Id}", rule);
}
```

### AFTER (compliant with all security requirements)

```csharp
public static async Task<IResult> CreateAlertRule(
    [FromBody] CreateAlertRuleRequest request,
    [FromServices] IAlertConfigurationService alertService,
    [FromServices] IUserIdentityService userIdentityService, // ✅ NEW
    [FromServices] IAuditLoggingService auditLoggingService, // ✅ NEW
    [FromServices] ILogger<AlertAdministrationEndpoints> logger)
{
    try
    {
        // ✅ RESOLVE TODO-002: Extract user identity from authentication context
        var identity = userIdentityService.GetCurrentUserIdentity();
        if (identity == null)
        {
            await auditLoggingService.LogAuthorizationDeniedAsync(
                action: "CreateAlertRule",
                reason: "User not authenticated");

            return Results.Unauthorized();
        }

        // ✅ Input validation (defense-in-depth)
        InputValidationHelpers.ThrowIfUnsafeInput(request.Name, nameof(request.Name));
        InputValidationHelpers.ThrowIfUnsafeInput(request.Description, nameof(request.Description));

        if (!InputValidationHelpers.IsValidLength(request.Name, minLength: 1, maxLength: 200))
        {
            return Results.BadRequest("Rule name must be between 1 and 200 characters");
        }

        // ✅ Create the alert rule with proper user attribution
        var rule = await alertService.CreateAlertRuleAsync(
            name: request.Name,
            description: request.Description,
            condition: request.Condition,
            severity: request.Severity,
            channels: request.Channels,
            createdBy: identity.UserId); // ✅ Proper user from auth context

        // ✅ Audit logging for compliance
        await auditLoggingService.LogAdminActionAsync(
            action: "CreateAlertRule",
            resourceType: "AlertRule",
            resourceId: rule.Id.ToString(),
            details: $"Created alert rule: {rule.Name}",
            additionalData: new Dictionary<string, object>
            {
                ["ruleName"] = rule.Name,
                ["ruleType"] = "Alert",
                ["severity"] = request.Severity,
                ["channelCount"] = request.Channels?.Count ?? 0
            });

        logger.LogInformation(
            "User {UserId} created alert rule {RuleId}: {RuleName}",
            identity.UserId, rule.Id, rule.Name);

        return Results.Created($"/admin/alerts/rules/{rule.Id}", rule);
    }
    catch (Exception ex)
    {
        // ✅ Log failure with exception details
        await auditLoggingService.LogAdminActionFailureAsync(
            action: "CreateAlertRule",
            resourceType: "AlertRule",
            details: "Failed to create alert rule",
            exception: ex);

        logger.LogError(ex, "Failed to create alert rule");
        throw;
    }
}
```

---

## Example 2: Alert Rule Deletion

### BEFORE

```csharp
public static async Task<IResult> DeleteAlertRule(
    [FromRoute] Guid id,
    [FromServices] IAlertConfigurationService alertService)
{
    var rule = await alertService.GetAlertRuleByIdAsync(id);
    if (rule == null)
    {
        return Results.NotFound();
    }

    await alertService.DeleteAlertRuleAsync(id);

    return Results.NoContent();
}
```

### AFTER

```csharp
public static async Task<IResult> DeleteAlertRule(
    [FromRoute] Guid id,
    [FromServices] IAlertConfigurationService alertService,
    [FromServices] IUserIdentityService userIdentityService, // ✅ NEW
    [FromServices] IAuditLoggingService auditLoggingService) // ✅ NEW
{
    try
    {
        // ✅ Extract user identity
        var identity = userIdentityService.GetCurrentUserIdentity();
        if (identity == null)
        {
            await auditLoggingService.LogAuthorizationDeniedAsync(
                action: "DeleteAlertRule",
                resourceId: id.ToString(),
                reason: "User not authenticated");

            return Results.Unauthorized();
        }

        // ✅ Validate resource ID format
        if (!InputValidationHelpers.IsValidUuid(id.ToString()))
        {
            return Results.BadRequest("Invalid rule ID format");
        }

        // Get rule before deletion (for audit log)
        var rule = await alertService.GetAlertRuleByIdAsync(id);
        if (rule == null)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "DeleteAlertRule",
                resourceType: "AlertRule",
                resourceId: id.ToString(),
                details: "Alert rule not found");

            return Results.NotFound();
        }

        // Delete the rule
        await alertService.DeleteAlertRuleAsync(id);

        // ✅ Audit logging
        await auditLoggingService.LogAdminActionAsync(
            action: "DeleteAlertRule",
            resourceType: "AlertRule",
            resourceId: id.ToString(),
            details: $"Deleted alert rule: {rule.Name}",
            additionalData: new Dictionary<string, object>
            {
                ["ruleName"] = rule.Name,
                ["ruleId"] = id
            });

        return Results.NoContent();
    }
    catch (Exception ex)
    {
        // ✅ Log failure
        await auditLoggingService.LogAdminActionFailureAsync(
            action: "DeleteAlertRule",
            resourceType: "AlertRule",
            resourceId: id.ToString(),
            details: "Failed to delete alert rule",
            exception: ex);

        throw;
    }
}
```

---

## Example 3: Updating Endpoint Authorization Policies

### BEFORE (generic authorization)

```csharp
public static RouteGroupBuilder MapAdminAlertEndpoints(this IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/admin/alerts")
        .WithTags("Admin - Alerts")
        .WithOpenApi()
        .RequireAuthorization("RequireAdministrator"); // ❌ Too generic

    group.MapPost("/rules", CreateAlertRule)
        .WithName("CreateAlertRule");

    group.MapDelete("/rules/{id}", DeleteAlertRule)
        .WithName("DeleteAlertRule");

    return group;
}
```

### AFTER (granular authorization)

```csharp
public static RouteGroupBuilder MapAdminAlertEndpoints(this IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/admin/alerts")
        .WithTags("Admin - Alerts")
        .WithOpenApi()
        .RequireAuthorization(AdminAuthorizationPolicies.RequireAlertAdministrator); // ✅ Granular

    group.MapPost("/rules", CreateAlertRule)
        .WithName("CreateAlertRule")
        .WithSummary("Create a new alert rule")
        .RequireAuthorization(AdminAuthorizationPolicies.RequireAlertAdministrator);

    group.MapDelete("/rules/{id}", DeleteAlertRule)
        .WithName("DeleteAlertRule")
        .WithSummary("Delete an alert rule")
        .RequireAuthorization(AdminAuthorizationPolicies.RequireAlertAdministrator);

    return group;
}
```

---

## Example 4: Testing Alert Publication (TODO-005)

### BEFORE (stub implementation)

```csharp
public static async Task<IResult> TestAlert(
    [FromRoute] Guid ruleId,
    [FromServices] IAlertConfigurationService alertService)
{
    var rule = await alertService.GetAlertRuleByIdAsync(ruleId);
    if (rule == null)
    {
        return Results.NotFound();
    }

    // ❌ TODO-005: Stub implementation - doesn't actually publish
    return Results.Ok(new
    {
        Success = true,
        Message = "Alert test triggered (stub implementation)"
    });
}
```

### AFTER (with proper implementation placeholder)

```csharp
public static async Task<IResult> TestAlert(
    [FromRoute] Guid ruleId,
    [FromServices] IAlertConfigurationService alertService,
    [FromServices] IAlertPublisher alertPublisher, // ✅ Actual publisher service
    [FromServices] IUserIdentityService userIdentityService,
    [FromServices] IAuditLoggingService auditLoggingService)
{
    try
    {
        // ✅ Extract user identity
        var identity = userIdentityService.GetCurrentUserIdentity();
        if (identity == null)
        {
            return Results.Unauthorized();
        }

        // Get rule
        var rule = await alertService.GetAlertRuleByIdAsync(ruleId);
        if (rule == null)
        {
            await auditLoggingService.LogAdminActionFailureAsync(
                action: "TestAlert",
                resourceType: "AlertRule",
                resourceId: ruleId.ToString(),
                details: "Alert rule not found");

            return Results.NotFound();
        }

        // ✅ TODO-005: Actually publish the test alert
        var testAlert = new AlertEvent
        {
            RuleId = ruleId,
            RuleName = rule.Name,
            Severity = rule.Severity,
            Message = $"[TEST] Alert test triggered by {identity.Username ?? identity.UserId}",
            Timestamp = DateTimeOffset.UtcNow,
            IsTest = true
        };

        var publishResult = await alertPublisher.PublishAsync(testAlert, rule.Channels);

        // ✅ Audit logging
        await auditLoggingService.LogAdminActionAsync(
            action: "TestAlert",
            resourceType: "AlertRule",
            resourceId: ruleId.ToString(),
            details: $"Test alert published for rule: {rule.Name}",
            additionalData: new Dictionary<string, object>
            {
                ["ruleName"] = rule.Name,
                ["channelCount"] = rule.Channels?.Count ?? 0,
                ["publishSuccess"] = publishResult.Success,
                ["publishedToChannels"] = publishResult.SuccessfulChannels.Count
            });

        return Results.Ok(new
        {
            Success = publishResult.Success,
            Message = publishResult.Success
                ? $"Alert test published to {publishResult.SuccessfulChannels.Count} channel(s)"
                : $"Alert test failed: {publishResult.ErrorMessage}",
            PublishedToChannels = publishResult.SuccessfulChannels,
            FailedChannels = publishResult.FailedChannels
        });
    }
    catch (Exception ex)
    {
        await auditLoggingService.LogAdminActionFailureAsync(
            action: "TestAlert",
            resourceType: "AlertRule",
            resourceId: ruleId.ToString(),
            details: "Failed to test alert",
            exception: ex);

        throw;
    }
}
```

---

## Example 5: User Authorization Check in Code

Sometimes you need to check authorization within the endpoint logic:

```csharp
public static async Task<IResult> GetSensitiveConfiguration(
    [FromServices] IConfiguration configuration,
    [FromServices] IUserIdentityService userIdentityService,
    [FromServices] IAuditLoggingService auditLoggingService,
    HttpContext httpContext)
{
    // ✅ Get user identity
    var identity = userIdentityService.GetCurrentUserIdentity();
    if (identity == null)
    {
        return Results.Unauthorized();
    }

    // ✅ Check if user is SuperAdmin
    var isSuperAdmin = identity.Roles.Contains(AdminAuthorizationPolicies.SuperAdminRole);

    if (!isSuperAdmin)
    {
        // ✅ Log authorization denial
        await auditLoggingService.LogAuthorizationDeniedAsync(
            action: "GetSensitiveConfiguration",
            resourceType: "Configuration",
            reason: "Requires SuperAdmin role");

        return Results.Forbid();
    }

    // ✅ Audit successful access to sensitive data
    await auditLoggingService.LogDataAccessAsync(
        resourceType: "Configuration",
        resourceId: "SensitiveSettings",
        operation: "Read");

    // Return sensitive configuration
    var sensitiveConfig = new
    {
        ConnectionStrings = configuration.GetSection("ConnectionStrings").Get<Dictionary<string, string>>(),
        SecretKeys = configuration.GetSection("SecretKeys").Get<Dictionary<string, string>>()
    };

    return Results.Ok(sensitiveConfig);
}
```

---

## Checklist for Updating Admin Endpoints

Use this checklist when updating each admin endpoint:

- [ ] **Inject services**:
  - [ ] `IUserIdentityService userIdentityService`
  - [ ] `IAuditLoggingService auditLoggingService`

- [ ] **Replace hardcoded users**:
  - [ ] Remove `var createdBy = "admin";`
  - [ ] Add `var identity = userIdentityService.GetCurrentUserIdentity();`
  - [ ] Check for null (unauthorized) and return 401 if needed

- [ ] **Add input validation**:
  - [ ] Use `InputValidationHelpers` for defense-in-depth
  - [ ] Validate string lengths, formats, patterns
  - [ ] Sanitize user input before use

- [ ] **Add audit logging**:
  - [ ] Log successful operations with `LogAdminActionAsync()`
  - [ ] Log failures with `LogAdminActionFailureAsync()`
  - [ ] Log authorization denials with `LogAuthorizationDeniedAsync()`
  - [ ] Include relevant metadata (resource IDs, action details)

- [ ] **Update authorization policies**:
  - [ ] Replace generic `RequireAdministrator` with specific policy
  - [ ] Use `AdminAuthorizationPolicies.RequireAlertAdministrator`, etc.

- [ ] **Add exception handling**:
  - [ ] Wrap in try-catch
  - [ ] Log failures with exception details
  - [ ] Re-throw or return appropriate error response

---

## Testing Your Changes

### Unit Tests

```csharp
[Fact]
public async Task CreateAlertRule_WithAuthenticatedUser_LogsAuditEvent()
{
    // Arrange
    var mockUserIdentityService = new Mock<IUserIdentityService>();
    mockUserIdentityService.Setup(s => s.GetCurrentUserIdentity())
        .Returns(new UserIdentity("user-123", "testuser", "test@example.com", null, new[] { "Admin" }));

    var mockAuditLoggingService = new Mock<IAuditLoggingService>();
    var mockAlertService = new Mock<IAlertConfigurationService>();

    // Act
    await CreateAlertRule(
        request: new CreateAlertRuleRequest { Name = "Test Rule" },
        alertService: mockAlertService.Object,
        userIdentityService: mockUserIdentityService.Object,
        auditLoggingService: mockAuditLoggingService.Object,
        logger: Mock.Of<ILogger<AlertAdministrationEndpoints>>());

    // Assert
    mockAuditLoggingService.Verify(s => s.LogAdminActionAsync(
        "CreateAlertRule",
        "AlertRule",
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<Dictionary<string, object>>(),
        default), Times.Once);
}
```

### Integration Tests

```csharp
[Fact]
public async Task CreateAlertRule_WithoutAuthentication_Returns401()
{
    // Arrange
    var client = _factory.CreateClient();
    // Don't add authentication header

    // Act
    var response = await client.PostAsJsonAsync("/admin/alerts/rules", new CreateAlertRuleRequest
    {
        Name = "Test Rule"
    });

    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task CreateAlertRule_WithAdminRole_Returns201()
{
    // Arrange
    var client = _factory.CreateClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

    // Act
    var response = await client.PostAsJsonAsync("/admin/alerts/rules", new CreateAlertRuleRequest
    {
        Name = "Test Rule",
        Description = "Test Description",
        Severity = "Medium"
    });

    // Assert
    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}
```

---

## Common Patterns

### Pattern 1: User Identity Check

```csharp
var identity = userIdentityService.GetCurrentUserIdentity();
if (identity == null)
{
    await auditLoggingService.LogAuthorizationDeniedAsync(
        action: "ActionName",
        reason: "User not authenticated");
    return Results.Unauthorized();
}
```

### Pattern 2: Audit Successful Operation

```csharp
await auditLoggingService.LogAdminActionAsync(
    action: "ActionName",
    resourceType: "ResourceType",
    resourceId: id.ToString(),
    details: $"Description of what happened",
    additionalData: new Dictionary<string, object>
    {
        ["key1"] = value1,
        ["key2"] = value2
    });
```

### Pattern 3: Audit Failed Operation

```csharp
catch (Exception ex)
{
    await auditLoggingService.LogAdminActionFailureAsync(
        action: "ActionName",
        resourceType: "ResourceType",
        resourceId: id.ToString(),
        details: "Brief description",
        exception: ex);
    throw;
}
```

---

## Next Steps

1. Review all admin endpoint files:
   - `AlertAdministrationEndpoints.cs`
   - `ServerAdministrationEndpoints.cs`
   - `MetadataAdministrationEndpoints.cs`
   - `FeatureFlagEndpoints.cs`
   - `GeofenceAlertAdministrationEndpoints.cs`

2. Update each endpoint following the patterns in this document

3. Add unit and integration tests

4. Verify audit logs are being generated (check logs in development)

5. Test authorization with different user roles

---

**Document Version**: 1.0
**Last Updated**: 2025-11-14
**Related**: SECURITY_ENHANCEMENTS.md, TODOs 001-003
