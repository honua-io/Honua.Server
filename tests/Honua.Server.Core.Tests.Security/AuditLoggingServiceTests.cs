// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security;

public class AuditLoggingServiceTests
{
    private readonly Mock<IUserIdentityService> _mockUserIdentityService;
    private readonly Mock<ILogger<AuditLoggingService>> _mockLogger;
    private readonly AuditLoggingService _service;

    public AuditLoggingServiceTests()
    {
        _mockUserIdentityService = new Mock<IUserIdentityService>();
        _mockLogger = new Mock<ILogger<AuditLoggingService>>();
        _service = new AuditLoggingService(_mockUserIdentityService.Object, _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullUserIdentityService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AuditLoggingService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new AuditLoggingService(_mockUserIdentityService.Object, null!));
    }

    #endregion

    #region LogAdminActionAsync Tests

    [Fact]
    public async Task LogAdminActionAsync_WithBasicAction_LogsSuccessfully()
    {
        // Arrange
        var action = "CreateDataSource";
        var userIdentity = new UserIdentity(
            UserId: "user-123",
            Username: "admin.user",
            Email: "admin@example.com",
            TenantId: "tenant-456",
            Roles: new[] { "Admin" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync(action);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAdminActionAsync_WithFullParameters_LogsAllDetails()
    {
        // Arrange
        var action = "UpdateDataSource";
        var resourceType = "DataSource";
        var resourceId = "ds-789";
        var details = "Updated connection string";
        var additionalData = new Dictionary<string, object>
        {
            ["previousValue"] = "old-connection",
            ["newValue"] = "new-connection"
        };

        var userIdentity = new UserIdentity(
            UserId: "user-123",
            Username: "admin.user",
            Email: "admin@example.com",
            TenantId: "tenant-456",
            Roles: new[] { "Admin" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync(action, resourceType, resourceId, details, additionalData);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockUserIdentityService.Verify(x => x.GetCurrentUserIdentity(), Times.Once);
    }

    [Fact]
    public async Task LogAdminActionAsync_WithAnonymousUser_LogsWithoutUserInfo()
    {
        // Arrange
        var action = "ViewSystemStatus";

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns((UserIdentity?)null);

        // Act
        await _service.LogAdminActionAsync(action);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAdminActionAsync_GeneratesCorrectLogEntry()
    {
        // Arrange
        var action = "DeleteDataSource";
        var resourceType = "DataSource";
        var resourceId = "ds-123";

        var userIdentity = new UserIdentity(
            UserId: "user-789",
            Username: "test.admin",
            Email: null,
            TenantId: null,
            Roles: Array.Empty<string>());

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync(action, resourceType, resourceId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Administration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region LogAdminActionFailureAsync Tests

    [Fact]
    public async Task LogAdminActionFailureAsync_WithException_IncludesExceptionDetails()
    {
        // Arrange
        var action = "CreateDataSource";
        var resourceType = "DataSource";
        var resourceId = "ds-fail";
        var details = "Failed to create datasource";
        var exception = new InvalidOperationException("Connection failed");

        var userIdentity = new UserIdentity(
            UserId: "user-123",
            Username: "admin.user",
            Email: "admin@example.com",
            TenantId: null,
            Roles: new[] { "Admin" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionFailureAsync(action, resourceType, resourceId, details, exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockUserIdentityService.Verify(x => x.GetCurrentUserIdentity(), Times.Once);
    }

    [Fact]
    public async Task LogAdminActionFailureAsync_WithoutException_LogsFailure()
    {
        // Arrange
        var action = "UpdateDataSource";
        var details = "Validation failed";

        var userIdentity = new UserIdentity(
            UserId: "user-456",
            Username: "editor.user",
            Email: "editor@example.com",
            TenantId: "tenant-123",
            Roles: new[] { "Editor" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionFailureAsync(action, details: details);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAdminActionFailureAsync_WithExceptionAndDetails_CombinesMessages()
    {
        // Arrange
        var action = "DeleteDataSource";
        var details = "Permission denied";
        var exception = new UnauthorizedAccessException("User lacks delete permission");

        var userIdentity = new UserIdentity(
            UserId: "user-789",
            Username: "user.test",
            Email: null,
            TenantId: null,
            Roles: new[] { "User" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionFailureAsync(action, details: details, exception: exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAdminActionFailureAsync_WithExceptionOnly_UsesExceptionMessage()
    {
        // Arrange
        var action = "UpdateConfiguration";
        var exception = new ArgumentException("Invalid configuration value");

        var userIdentity = new UserIdentity(
            UserId: "user-abc",
            Username: "config.admin",
            Email: "config@example.com",
            TenantId: null,
            Roles: new[] { "Admin" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionFailureAsync(action, exception: exception);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region LogAuthorizationDeniedAsync Tests

    [Fact]
    public async Task LogAuthorizationDeniedAsync_WithBasicDenial_LogsAsWarning()
    {
        // Arrange
        var action = "AccessSecretData";
        var reason = "Insufficient permissions";

        var userIdentity = new UserIdentity(
            UserId: "user-123",
            Username: "regular.user",
            Email: "user@example.com",
            TenantId: "tenant-456",
            Roles: new[] { "User" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAuthorizationDeniedAsync(action, reason: reason);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAuthorizationDeniedAsync_WithResourceDetails_IncludesResourceInfo()
    {
        // Arrange
        var action = "DeleteDataSource";
        var resourceType = "DataSource";
        var resourceId = "ds-789";
        var reason = "Not owner of resource";

        var userIdentity = new UserIdentity(
            UserId: "user-456",
            Username: "other.user",
            Email: "other@example.com",
            TenantId: "tenant-789",
            Roles: new[] { "Editor" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAuthorizationDeniedAsync(action, resourceType, resourceId, reason);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockUserIdentityService.Verify(x => x.GetCurrentUserIdentity(), Times.Once);
    }

    [Fact]
    public async Task LogAuthorizationDeniedAsync_WithoutReason_UsesDefaultMessage()
    {
        // Arrange
        var action = "AccessAdminPanel";

        var userIdentity = new UserIdentity(
            UserId: "user-789",
            Username: "guest.user",
            Email: null,
            TenantId: null,
            Roles: new[] { "Guest" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAuthorizationDeniedAsync(action);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAuthorizationDeniedAsync_WithAnonymousUser_LogsAnonymousAccess()
    {
        // Arrange
        var action = "AccessProtectedResource";
        var reason = "Not authenticated";

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns((UserIdentity?)null);

        // Act
        await _service.LogAuthorizationDeniedAsync(action, reason: reason);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region LogSecurityViolationAsync Tests

    [Fact]
    public async Task LogSecurityViolationAsync_WithBasicViolation_LogsAsWarning()
    {
        // Arrange
        var violationType = "SqlInjectionAttempt";
        var details = "Detected SQL injection pattern in query parameter";

        var userIdentity = new UserIdentity(
            UserId: "user-suspect",
            Username: "suspicious.user",
            Email: "suspicious@example.com",
            TenantId: null,
            Roles: Array.Empty<string>());

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogSecurityViolationAsync(violationType, details);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogSecurityViolationAsync_WithAdditionalData_IncludesContextInfo()
    {
        // Arrange
        var violationType = "XSSAttempt";
        var details = "Detected XSS pattern in user input";
        var additionalData = new Dictionary<string, object>
        {
            ["inputField"] = "comment",
            ["suspiciousPattern"] = "<script>alert('xss')</script>"
        };

        var userIdentity = new UserIdentity(
            UserId: "user-attacker",
            Username: "attacker",
            Email: null,
            TenantId: null,
            Roles: Array.Empty<string>());

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogSecurityViolationAsync(violationType, details, additionalData);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockUserIdentityService.Verify(x => x.GetCurrentUserIdentity(), Times.Once);
    }

    #endregion

    #region LogDataAccessAsync Tests

    [Fact]
    public async Task LogDataAccessAsync_WithBasicAccess_LogsSuccessfully()
    {
        // Arrange
        var resourceType = "DataSource";
        var resourceId = "ds-123";
        var operation = "Query";

        var userIdentity = new UserIdentity(
            UserId: "user-123",
            Username: "data.analyst",
            Email: "analyst@example.com",
            TenantId: "tenant-456",
            Roles: new[] { "Analyst" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogDataAccessAsync(resourceType, resourceId, operation);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogDataAccessAsync_WithAdditionalData_IncludesMetadata()
    {
        // Arrange
        var resourceType = "Layer";
        var resourceId = "layer-789";
        var operation = "Export";
        var additionalData = new Dictionary<string, object>
        {
            ["format"] = "GeoJSON",
            ["recordCount"] = 1500
        };

        var userIdentity = new UserIdentity(
            UserId: "user-456",
            Username: "gis.user",
            Email: "gis@example.com",
            TenantId: null,
            Roles: new[] { "User" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogDataAccessAsync(resourceType, resourceId, operation, additionalData);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region LogEventAsync Tests

    [Fact]
    public async Task LogEventAsync_WithAllParameters_CreatesCompleteAuditLog()
    {
        // Arrange
        var category = AuditEventCategory.Configuration;
        var action = "UpdateSystemSettings";
        var result = AuditEventResult.Success;
        var resourceType = "Configuration";
        var resourceId = "system-config";
        var details = "Updated maximum file upload size";
        var additionalData = new Dictionary<string, object>
        {
            ["oldValue"] = 10485760,
            ["newValue"] = 52428800
        };

        var userIdentity = new UserIdentity(
            UserId: "user-admin",
            Username: "system.admin",
            Email: "admin@honua.io",
            TenantId: null,
            Roles: new[] { "SystemAdmin" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogEventAsync(
            category,
            action,
            result,
            resourceType,
            resourceId,
            details,
            additionalData);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);

        _mockUserIdentityService.Verify(x => x.GetCurrentUserIdentity(), Times.Once);
    }

    [Fact]
    public async Task LogEventAsync_WithSecurityCategory_LogsAsWarning()
    {
        // Arrange
        var category = AuditEventCategory.Security;
        var action = "PasswordResetAttempt";
        var result = AuditEventResult.Failure;

        var userIdentity = new UserIdentity(
            UserId: "user-test",
            Username: "test.user",
            Email: "test@example.com",
            TenantId: null,
            Roles: Array.Empty<string>());

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogEventAsync(category, action, result);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogEventAsync_WithDeniedResult_LogsAsWarning()
    {
        // Arrange
        var category = AuditEventCategory.Authorization;
        var action = "AccessRestrictedArea";
        var result = AuditEventResult.Denied;

        var userIdentity = new UserIdentity(
            UserId: "user-limited",
            Username: "limited.user",
            Email: null,
            TenantId: "tenant-123",
            Roles: new[] { "Limited" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogEventAsync(category, action, result);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogEventAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var action = "TestAction";
        var cancellationToken = new CancellationToken();

        var userIdentity = new UserIdentity(
            UserId: "user-test",
            Username: "test.user",
            Email: "test@example.com",
            TenantId: null,
            Roles: Array.Empty<string>());

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogEventAsync(
            AuditEventCategory.DataAccess,
            action,
            AuditEventResult.Success,
            cancellationToken: cancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region User Attribution Tests

    [Fact]
    public async Task LogEventAsync_CallsUserIdentityService_ForUserAttribution()
    {
        // Arrange
        var userIdentity = new UserIdentity(
            UserId: "user-123",
            Username: "test.user",
            Email: "test@example.com",
            TenantId: "tenant-456",
            Roles: new[] { "User" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync("TestAction");

        // Assert
        _mockUserIdentityService.Verify(x => x.GetCurrentUserIdentity(), Times.Once);
    }

    [Fact]
    public async Task LogEventAsync_WithEmailOnlyIdentity_UsesEmailAsUsername()
    {
        // Arrange
        var userIdentity = new UserIdentity(
            UserId: "user-email-only",
            Username: null,
            Email: "emailonly@example.com",
            TenantId: null,
            Roles: Array.Empty<string>());

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync("TestAction");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region Structured Logging Tests

    [Fact]
    public async Task LogEventAsync_CreatesStructuredLogWithScope()
    {
        // Arrange
        var userIdentity = new UserIdentity(
            UserId: "user-structured",
            Username: "structured.user",
            Email: "structured@example.com",
            TenantId: "tenant-structured",
            Roles: new[] { "Admin" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync(
            "StructuredLogTest",
            "TestResource",
            "resource-123",
            "Testing structured logging");

        // Assert
        _mockLogger.Verify(
            x => x.BeginScope(It.IsAny<Dictionary<string, object?>>()),
            Times.AtLeastOnce);

        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogEventAsync_LogsJsonFormattedAuditEntry()
    {
        // Arrange
        var userIdentity = new UserIdentity(
            UserId: "user-json",
            Username: "json.user",
            Email: "json@example.com",
            TenantId: null,
            Roles: new[] { "User" });

        _mockUserIdentityService.Setup(x => x.GetCurrentUserIdentity())
            .Returns(userIdentity);

        // Act
        await _service.LogAdminActionAsync("JsonLogTest");

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("[AUDIT]")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    #endregion
}
