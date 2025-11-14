// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using FluentAssertions;
using Honua.Server.Core.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security;

public class UserIdentityServiceTests
{
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly UserIdentityService _service;

    public UserIdentityServiceTests()
    {
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _service = new UserIdentityService(_mockHttpContextAccessor.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullHttpContextAccessor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new UserIdentityService(null!));
    }

    #endregion

    #region GetCurrentUserId Tests

    [Fact]
    public void GetCurrentUserId_WithJwtSubClaim_ReturnsUserId()
    {
        // Arrange
        var userId = "user-123";
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WithNameIdentifierClaim_ReturnsUserId()
    {
        // Arrange
        var userId = "user-456";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WithSubStringClaim_ReturnsUserId()
    {
        // Arrange
        var userId = "user-789";
        var claims = new List<Claim>
        {
            new Claim("sub", userId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WithNameIdClaim_ReturnsUserId()
    {
        // Arrange
        var userId = "user-abc";
        var claims = new List<Claim>
        {
            new Claim("nameid", userId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().Be(userId);
    }

    [Fact]
    public void GetCurrentUserId_WithMultipleClaims_ReturnsFirstInPriority()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "jwt-user"),
            new Claim(ClaimTypes.NameIdentifier, "nameidentifier-user"),
            new Claim("sub", "sub-user"),
            new Claim("nameid", "nameid-user")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().Be("jwt-user");
    }

    [Fact]
    public void GetCurrentUserId_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WithNullHttpContext_ReturnsNull()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserId_WithMissingClaims_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserId();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCurrentUsername Tests

    [Fact]
    public void GetCurrentUsername_WithClaimTypesName_ReturnsUsername()
    {
        // Arrange
        var username = "john.doe";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().Be(username);
    }

    [Fact]
    public void GetCurrentUsername_WithNameClaim_ReturnsUsername()
    {
        // Arrange
        var username = "jane.smith";
        var claims = new List<Claim>
        {
            new Claim("name", username)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().Be(username);
    }

    [Fact]
    public void GetCurrentUsername_WithPreferredUsernameClaim_ReturnsUsername()
    {
        // Arrange
        var username = "preferred_user";
        var claims = new List<Claim>
        {
            new Claim("preferred_username", username)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().Be(username);
    }

    [Fact]
    public void GetCurrentUsername_WithJwtNameClaim_ReturnsUsername()
    {
        // Arrange
        var username = "jwt_user";
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Name, username)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().Be(username);
    }

    [Fact]
    public void GetCurrentUsername_WithIdentityName_ReturnsUsername()
    {
        // Arrange
        var username = "identity_user";
        var claims = new List<Claim>();
        var identity = new ClaimsIdentity(claims, "TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, username));
        var principal = new ClaimsPrincipal(identity);
        SetupHttpContext(principal);

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().Be(username);
    }

    [Fact]
    public void GetCurrentUsername_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUsername_WithMissingClaims_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUsername();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCurrentUserEmail Tests

    [Fact]
    public void GetCurrentUserEmail_WithClaimTypesEmail_ReturnsEmail()
    {
        // Arrange
        var email = "user@example.com";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, email)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserEmail();

        // Assert
        result.Should().Be(email);
    }

    [Fact]
    public void GetCurrentUserEmail_WithEmailClaim_ReturnsEmail()
    {
        // Arrange
        var email = "test@test.com";
        var claims = new List<Claim>
        {
            new Claim("email", email)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserEmail();

        // Assert
        result.Should().Be(email);
    }

    [Fact]
    public void GetCurrentUserEmail_WithJwtEmailClaim_ReturnsEmail()
    {
        // Arrange
        var email = "jwt@example.com";
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Email, email)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserEmail();

        // Assert
        result.Should().Be(email);
    }

    [Fact]
    public void GetCurrentUserEmail_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.GetCurrentUserEmail();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserEmail_WithMissingClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserEmail();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCurrentTenantId Tests

    [Fact]
    public void GetCurrentTenantId_WithTenantIdClaim_ReturnsTenantId()
    {
        // Arrange
        var tenantId = "tenant-123";
        var claims = new List<Claim>
        {
            new Claim("tenant_id", tenantId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_WithTidClaim_ReturnsTenantId()
    {
        // Arrange
        var tenantId = "tenant-456";
        var claims = new List<Claim>
        {
            new Claim("tid", tenantId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_WithTenantIdCamelCaseClaim_ReturnsTenantId()
    {
        // Arrange
        var tenantId = "tenant-789";
        var claims = new List<Claim>
        {
            new Claim("tenantId", tenantId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_WithOrganizationIdClaim_ReturnsTenantId()
    {
        // Arrange
        var tenantId = "org-abc";
        var claims = new List<Claim>
        {
            new Claim("organization_id", tenantId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_WithOrgIdClaim_ReturnsTenantId()
    {
        // Arrange
        var tenantId = "org-xyz";
        var claims = new List<Claim>
        {
            new Claim("org_id", tenantId)
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().Be(tenantId);
    }

    [Fact]
    public void GetCurrentTenantId_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentTenantId_WithMissingClaim_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentTenantId();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetCurrentUserRoles Tests

    [Fact]
    public void GetCurrentUserRoles_WithClaimTypesRole_ReturnsRoles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Admin");
        result.Should().Contain("User");
    }

    [Fact]
    public void GetCurrentUserRoles_WithRoleClaim_ReturnsRoles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("role", "Editor"),
            new Claim("role", "Viewer")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("Editor");
        result.Should().Contain("Viewer");
    }

    [Fact]
    public void GetCurrentUserRoles_WithHonuaRoleClaim_ReturnsRoles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("honua_role", "DataAdmin")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("DataAdmin");
    }

    [Fact]
    public void GetCurrentUserRoles_WithRolesClaim_ReturnsRoles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim("roles", "SuperAdmin")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("SuperAdmin");
    }

    [Fact]
    public void GetCurrentUserRoles_WithMixedRoleClaims_ReturnsAllRoles()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("role", "Editor"),
            new Claim("honua_role", "Analyst"),
            new Claim("roles", "Guest")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain(new[] { "Admin", "Editor", "Analyst", "Guest" });
    }

    [Fact]
    public void GetCurrentUserRoles_WithUnauthenticatedUser_ReturnsEmptyArray()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCurrentUserRoles_WithNoRoleClaims_ReturnsEmptyArray()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCurrentUserRoles_WithEmptyRoleValues_FiltersOutEmptyValues()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, ""),
            new Claim(ClaimTypes.Role, "   "),
            new Claim(ClaimTypes.Role, "User")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserRoles();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { "Admin", "User" });
    }

    #endregion

    #region GetCurrentUserIdentity Tests

    [Fact]
    public void GetCurrentUserIdentity_WithFullClaims_ReturnsCompleteIdentity()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "user-123"),
            new Claim(ClaimTypes.Name, "john.doe"),
            new Claim(ClaimTypes.Email, "john@example.com"),
            new Claim("tenant_id", "tenant-456"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserIdentity();

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be("user-123");
        result.Username.Should().Be("john.doe");
        result.Email.Should().Be("john@example.com");
        result.TenantId.Should().Be("tenant-456");
        result.Roles.Should().HaveCount(2);
        result.Roles.Should().Contain(new[] { "Admin", "User" });
    }

    [Fact]
    public void GetCurrentUserIdentity_WithMinimalClaims_ReturnsPartialIdentity()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "user-789")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserIdentity();

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be("user-789");
        result.Username.Should().BeNull();
        result.Email.Should().BeNull();
        result.TenantId.Should().BeNull();
        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public void GetCurrentUserIdentity_WithUnauthenticatedUser_ReturnsNull()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.GetCurrentUserIdentity();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserIdentity_WithoutUserId_ReturnsNull()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.Email, "test@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserIdentity();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCurrentUserIdentity_UsernamePreference_UsesUsernameOverEmail()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "user-123"),
            new Claim(ClaimTypes.Name, "john.doe"),
            new Claim(ClaimTypes.Email, "john@example.com")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.GetCurrentUserIdentity();

        // Assert
        result.Should().NotBeNull();
        result!.Username.Should().Be("john.doe");
        result.Email.Should().Be("john@example.com");
    }

    #endregion

    #region IsAuthenticated Tests

    [Fact]
    public void IsAuthenticated_WithAuthenticatedUser_ReturnsTrue()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "testuser")
        };
        SetupAuthenticatedUser(claims);

        // Act
        var result = _service.IsAuthenticated();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_WithUnauthenticatedUser_ReturnsFalse()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = _service.IsAuthenticated();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_WithNullHttpContext_ReturnsFalse()
    {
        // Arrange
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        // Act
        var result = _service.IsAuthenticated();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAuthenticated_WithNullUser_ReturnsFalse()
    {
        // Arrange
        var httpContext = new DefaultHttpContext
        {
            User = null!
        };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        // Act
        var result = _service.IsAuthenticated();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private void SetupAuthenticatedUser(List<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        SetupHttpContext(principal);
    }

    private void SetupUnauthenticatedUser()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        SetupHttpContext(principal);
    }

    private void SetupHttpContext(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext
        {
            User = principal
        };
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);
    }

    #endregion
}
