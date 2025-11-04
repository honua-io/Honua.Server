using Honua.Server.Host.OpenApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="SecurityRequirementsOperationFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class SecurityRequirementsOperationFilterTests
{
    private readonly SecurityRequirementsOperationFilter _filter;

    public SecurityRequirementsOperationFilterTests()
    {
        _filter = new SecurityRequirementsOperationFilter();
    }

    [Fact]
    public void Apply_WithoutAuthorization_DoesNotAddSecurity()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.PublicMethod))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Null(operation.Security);
    }

    [Fact]
    public void Apply_WithAuthorizeAttribute_AddsBearerSecurity()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.AuthorizedMethod))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
        var securityRequirement = operation.Security[0];
        Assert.Contains(securityRequirement.Keys, k => k.Reference.Id == "Bearer");
    }

    [Fact]
    public void Apply_WithAllowAnonymous_ClearsSecurity()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement()
            }
        };
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.AnonymousMethod))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Empty(operation.Security);
    }

    [Fact]
    public void Apply_WithRoles_AddsRolesToScopes()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithRoles))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
        var scopes = operation.Security[0].Values.First();
        Assert.Contains("Admin", scopes);
        Assert.Contains("User", scopes);
    }

    [Fact]
    public void Apply_WithPolicy_AddsPolicyToScopes()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithPolicy))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
        var scopes = operation.Security[0].Values.First();
        Assert.Contains("CanEditData", scopes);
    }

    [Fact]
    public void Apply_WithRolesAndPolicy_AddsBothToScopes()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithRolesAndPolicy))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
        var scopes = operation.Security[0].Values.First();
        Assert.Contains("SuperAdmin", scopes);
        Assert.Contains("RequiresMFA", scopes);
    }

    [Fact]
    public void Apply_WithRoles_AddsDescriptionToOperation()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Description = "Original description"
        };
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithRoles))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Contains("Required roles:", operation.Description);
        Assert.Contains("Admin", operation.Description);
        Assert.Contains("User", operation.Description);
    }

    [Fact]
    public void Apply_WithPolicy_AddsDescriptionToOperation()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithPolicy))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Contains("Required policies:", operation.Description);
        Assert.Contains("CanEditData", operation.Description);
    }

    [Fact]
    public void Apply_WithControllerLevelAuthorization_AddsSecurity()
    {
        // Arrange
        var operation = new OpenApiOperation();
        var methodInfo = typeof(AuthorizedController).GetMethod(nameof(AuthorizedController.ControllerLevelMethod))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Security);
        Assert.Single(operation.Security);
    }

    [Fact]
    public void Apply_WithControllerAuthAndMethodAllowAnonymous_ClearsSecurity()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Security = new List<OpenApiSecurityRequirement>
            {
                new OpenApiSecurityRequirement()
            }
        };
        var methodInfo = typeof(AuthorizedController).GetMethod(nameof(AuthorizedController.OverrideWithAnonymous))!;
        var context = CreateOperationFilterContext(methodInfo);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Empty(operation.Security);
    }

    // Helper methods
    private static OperationFilterContext CreateOperationFilterContext(MethodInfo methodInfo)
    {
        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
            {
                ControllerName = methodInfo.DeclaringType?.Name ?? "Test",
                ActionName = methodInfo.Name,
                MethodInfo = methodInfo
            }
        };

        var schemaRepository = new SchemaRepository();
        var schemaGenerator = new SchemaGenerator(new SchemaGeneratorOptions(), new Moq.Mock<ISerializerDataContractResolver>().Object);

        return new OperationFilterContext(
            apiDescription,
            schemaGenerator,
            schemaRepository,
            methodInfo);
    }

    // Test controllers with various authorization scenarios
    public class TestController
    {
        public void PublicMethod() { }

        [Authorize]
        public void AuthorizedMethod() { }

        [AllowAnonymous]
        public void AnonymousMethod() { }

        [Authorize(Roles = "Admin,User")]
        public void MethodWithRoles() { }

        [Authorize(Policy = "CanEditData")]
        public void MethodWithPolicy() { }

        [Authorize(Roles = "SuperAdmin", Policy = "RequiresMFA")]
        public void MethodWithRolesAndPolicy() { }
    }

    [Authorize]
    public class AuthorizedController
    {
        public void ControllerLevelMethod() { }

        [AllowAnonymous]
        public void OverrideWithAnonymous() { }
    }
}
