using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin;
using Honua.Server.Host.Admin.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Admin;

/// <summary>
/// Tests for RbacEndpoints to ensure proper authorization and functionality
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class RbacEndpointsTests
{
    #region Authorization Tests

    [Fact]
    public async Task GetRoles_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetRole_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/roles/administrator");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateRole_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        var request = new CreateRoleRequest
        {
            Name = "test-role",
            DisplayName = "Test Role",
            Permissions = new List<string> { "read" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateRole_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        var request = new UpdateRoleRequest
        {
            DisplayName = "Updated Role"
        };

        // Act
        var response = await client.PutAsJsonAsync("/admin/metadata/rbac/roles/test-role", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteRole_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/admin/metadata/rbac/roles/test-role");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPermissions_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/permissions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePermission_RequiresAuthorization()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: true);
        using var client = factory.CreateClient();

        var request = new CreatePermissionRequest
        {
            Name = "custom.permission",
            DisplayName = "Custom Permission",
            Category = "Custom"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/permissions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Role Management Tests

    [Fact]
    public async Task GetRoles_WithAdminAuth_ReturnsAllRoles()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/roles");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoleListResponse>();
        result.Should().NotBeNull();
        result!.Roles.Should().HaveCount(3); // administrator, datapublisher, viewer
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GetRole_WithAdminAuth_ReturnsRole()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/roles/administrator");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoleDefinitionDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("administrator");
        result.DisplayName.Should().Be("Administrator");
        result.IsSystem.Should().BeTrue();
        result.Permissions.Should().Contain("all");
    }

    [Fact]
    public async Task GetRole_NotFound_ReturnsNotFound()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/roles/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateRole_WithValidData_CreatesRole()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new CreateRoleRequest
        {
            Name = "editor",
            DisplayName = "Editor",
            Description = "Can edit content",
            Permissions = new List<string> { "read", "write" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoleDefinitionDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("editor");
        result.DisplayName.Should().Be("Editor");
        result.Description.Should().Be("Can edit content");
        result.Permissions.Should().BeEquivalentTo(new[] { "read", "write" });
        result.IsSystem.Should().BeFalse();

        // Verify UpdateSnapshotAsync was called
        mockRegistry.Verify(
            m => m.UpdateSnapshotAsync(It.IsAny<MetadataSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRole_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new CreateRoleRequest
        {
            Name = "administrator", // Duplicate system role
            DisplayName = "Another Admin",
            Permissions = new List<string> { "read" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateRole_InvalidPermissions_ReturnsBadRequest()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new CreateRoleRequest
        {
            Name = "invalid-role",
            DisplayName = "Invalid Role",
            Permissions = new List<string> { "nonexistent.permission" }
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/roles", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Invalid permissions");
    }

    [Fact]
    public async Task UpdateRole_WithValidData_UpdatesRole()
    {
        // Arrange
        var mockRegistry = CreateMockRegistryWithCustomRole();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new UpdateRoleRequest
        {
            DisplayName = "Updated Editor",
            Description = "Updated description",
            Permissions = new List<string> { "read", "write", "delete" }
        };

        // Act
        var response = await client.PutAsJsonAsync("/admin/metadata/rbac/roles/editor", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<RoleDefinitionDto>();
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Updated Editor");
        result.Description.Should().Be("Updated description");
        result.Permissions.Should().BeEquivalentTo(new[] { "read", "write", "delete" });
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateRole_SystemRole_ReturnsBadRequest()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new UpdateRoleRequest
        {
            DisplayName = "Modified Administrator"
        };

        // Act
        var response = await client.PutAsJsonAsync("/admin/metadata/rbac/roles/administrator", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Cannot modify system roles");
    }

    [Fact]
    public async Task DeleteRole_CustomRole_DeletesRole()
    {
        // Arrange
        var mockRegistry = CreateMockRegistryWithCustomRole();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/admin/metadata/rbac/roles/editor");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify UpdateSnapshotAsync was called
        mockRegistry.Verify(
            m => m.UpdateSnapshotAsync(It.IsAny<MetadataSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteRole_SystemRole_ReturnsBadRequest()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/admin/metadata/rbac/roles/administrator");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("Cannot delete system roles");
    }

    [Fact]
    public async Task DeleteRole_NotFound_ReturnsNotFound()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.DeleteAsync("/admin/metadata/rbac/roles/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Permission Management Tests

    [Fact]
    public async Task GetPermissions_WithAdminAuth_ReturnsAllPermissions()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/admin/metadata/rbac/permissions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PermissionListResponse>();
        result.Should().NotBeNull();
        result!.Permissions.Should().NotBeEmpty();
        result.Categories.Should().NotBeEmpty();
        result.Categories.Should().Contain("System");
        result.Categories.Should().Contain("Data");
        result.Categories.Should().Contain("Administration");
    }

    [Fact]
    public async Task CreatePermission_WithValidData_CreatesPermission()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new CreatePermissionRequest
        {
            Name = "reports.export",
            DisplayName = "Export Reports",
            Description = "Can export reports to various formats",
            Category = "Reports"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/permissions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PermissionDefinitionDto>();
        result.Should().NotBeNull();
        result!.Name.Should().Be("reports.export");
        result.DisplayName.Should().Be("Export Reports");
        result.Category.Should().Be("Reports");
        result.IsSystem.Should().BeFalse();

        // Verify UpdateSnapshotAsync was called
        mockRegistry.Verify(
            m => m.UpdateSnapshotAsync(It.IsAny<MetadataSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePermission_DuplicateName_ReturnsBadRequest()
    {
        // Arrange
        var mockRegistry = CreateMockRegistry();
        await using var factory = CreateTestFactory(mockRegistry.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new CreatePermissionRequest
        {
            Name = "read", // Duplicate system permission
            DisplayName = "Another Read",
            Category = "Custom"
        };

        // Act
        var response = await client.PostAsJsonAsync("/admin/metadata/rbac/permissions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadAsStringAsync();
        error.Should().Contain("already exists");
    }

    #endregion

    #region Helper Methods

    private static Mock<IMetadataRegistry> CreateMockRegistry()
    {
        var mockRegistry = new Mock<IMetadataRegistry>();
        var snapshot = CreateDefaultSnapshot();

        mockRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        mockRegistry
            .Setup(m => m.UpdateSnapshotAsync(It.IsAny<MetadataSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mockRegistry;
    }

    private static Mock<IMetadataRegistry> CreateMockRegistryWithCustomRole()
    {
        var mockRegistry = new Mock<IMetadataRegistry>();
        var snapshot = CreateSnapshotWithCustomRole();

        mockRegistry
            .Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        mockRegistry
            .Setup(m => m.UpdateSnapshotAsync(It.IsAny<MetadataSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mockRegistry;
    }

    private static MetadataSnapshot CreateDefaultSnapshot()
    {
        var rbac = RbacDefinition.Default;
        var server = ServerDefinition.Default with { Rbac = rbac };

        return new MetadataSnapshot
        {
            Version = "1.0.0",
            Server = server,
            DataSources = new Dictionary<string, DataSourceDefinition>(),
            Folders = new Dictionary<string, FolderDefinition>(),
            Services = new Dictionary<string, ServiceDefinition>()
        };
    }

    private static MetadataSnapshot CreateSnapshotWithCustomRole()
    {
        var rbac = RbacDefinition.Default;
        var customRole = new RoleDefinition
        {
            Id = "editor",
            Name = "editor",
            DisplayName = "Editor",
            Description = "Can edit content",
            Permissions = new List<string> { "read", "write" },
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var updatedRbac = rbac with
        {
            Roles = rbac.Roles.Append(customRole).ToList()
        };

        var server = ServerDefinition.Default with { Rbac = updatedRbac };

        return new MetadataSnapshot
        {
            Version = "1.0.0",
            Server = server,
            DataSources = new Dictionary<string, DataSourceDefinition>(),
            Folders = new Dictionary<string, FolderDefinition>(),
            Services = new Dictionary<string, ServiceDefinition>()
        };
    }

    private static WebApplicationFactory<RbacTestStartup> CreateTestFactory(
        IMetadataRegistry registry,
        bool requireAuth)
    {
        return new WebApplicationFactory<RbacTestStartup>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(registry);
                    services.AddSingleton<ILogger<Program>>(NullLogger<Program>.Instance);

                    if (requireAuth)
                    {
                        // Configure to require authentication
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("RequireAdministrator", policy =>
                                policy.RequireAuthenticatedUser()
                                      .RequireRole("administrator"));
                        });

                        services.AddAuthentication("Test")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                    }
                    else
                    {
                        // Configure to allow all (simulate admin user)
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("RequireAdministrator", policy =>
                                policy.RequireAssertion(_ => true));
                        });
                    }
                });
            });
    }

    #endregion
}

/// <summary>
/// Test authentication handler that always rejects authentication
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Always fail authentication
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

/// <summary>
/// Minimal test startup for RbacEndpoints testing
/// </summary>
internal sealed class RbacTestStartup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            var group = endpoints.MapGroup("/admin/metadata");
            group.MapAdminRbacEndpoints();
        });
    }
}
