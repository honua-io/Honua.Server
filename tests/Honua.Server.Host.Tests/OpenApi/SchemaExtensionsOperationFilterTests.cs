using Honua.Server.Host.OpenApi.Filters;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="SchemaExtensionsOperationFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class SchemaExtensionsOperationFilterTests
{
    private readonly SchemaExtensionsOperationFilter _filter;

    public SchemaExtensionsOperationFilterTests()
    {
        _filter = new SchemaExtensionsOperationFilter();
    }

    [Fact]
    public void Apply_WithNoParameters_DoesNotThrow()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>()
        };
        var context = CreateOperationFilterContext("TestMethod");

        // Act & Assert
        var exception = Record.Exception(() => _filter.Apply(operation, context));
        Assert.Null(exception);
    }

    [Fact]
    public void Apply_WithOpenApiExtensionAttribute_AddsExtension()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "customParam",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithExtension))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "customParam"
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.True(operation.Parameters[0].Extensions.ContainsKey("x-custom-property"));
    }

    [Fact]
    public void Apply_WithRangeAttribute_SetsMinMaxValues()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "score",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithRange))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "score",
            Type = typeof(int)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Equal(0, operation.Parameters[0].Schema.Minimum);
        Assert.Equal(100, operation.Parameters[0].Schema.Maximum);
        Assert.Contains("Must be between", operation.Parameters[0].Description);
    }

    [Fact]
    public void Apply_WithStringLengthAttribute_SetsMinMaxLength()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "username",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithStringLength))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "username",
            Type = typeof(string)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Equal(3, operation.Parameters[0].Schema.MinLength);
        Assert.Equal(50, operation.Parameters[0].Schema.MaxLength);
        Assert.Contains("Length must be between", operation.Parameters[0].Description);
    }

    [Fact]
    public void Apply_WithMinLengthAttribute_SetsMinLength()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "description",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithMinLength))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "description",
            Type = typeof(string)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Equal(10, operation.Parameters[0].Schema.MinLength);
        Assert.Contains("Minimum length", operation.Parameters[0].Description);
    }

    [Fact]
    public void Apply_WithMaxLengthAttribute_SetsMaxLength()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "title",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithMaxLength))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "title",
            Type = typeof(string)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Equal(100, operation.Parameters[0].Schema.MaxLength);
        Assert.Contains("Maximum length", operation.Parameters[0].Description);
    }

    [Fact]
    public void Apply_WithRequiredAttribute_MarksParameterRequired()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "id",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithRequired))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "id",
            Type = typeof(string)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.True(operation.Parameters[0].Required);
        Assert.Contains("Required", operation.Parameters[0].Description);
    }

    [Fact]
    public void Apply_WithMultipleValidationAttributes_AddsAllValidation()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "email",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithMultipleValidations))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "email",
            Type = typeof(string)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.True(operation.Parameters[0].Required);
        Assert.NotNull(operation.Parameters[0].Schema.MinLength);
        Assert.NotNull(operation.Parameters[0].Schema.MaxLength);
        Assert.Contains("Validation:", operation.Parameters[0].Description);
    }

    [Fact]
    public void OpenApiExtensionAttribute_Constructor_ValidatesKey()
    {
        // Act & Assert - valid key
        var validAttribute = new OpenApiExtensionAttribute("x-valid", "value");
        Assert.Equal("x-valid", validAttribute.Key);
        Assert.Equal("value", validAttribute.Value);

        // Act & Assert - invalid key
        Assert.Throws<ArgumentException>(() => new OpenApiExtensionAttribute("invalid", "value"));
    }

    [Fact]
    public void OpenApiExtensionAttribute_Constructor_CaseInsensitiveKeyValidation()
    {
        // Act & Assert - uppercase X is valid
        var attribute = new OpenApiExtensionAttribute("X-CUSTOM", "value");
        Assert.Equal("X-CUSTOM", attribute.Key);
    }

    // Helper methods
    private static OperationFilterContext CreateOperationFilterContext(
        string methodName,
        params ApiParameterDescription[] parameterDescriptions)
    {
        var methodInfo = typeof(TestController).GetMethod(methodName)
            ?? typeof(TestController).GetMethods().First();

        return CreateOperationFilterContext(methodInfo, parameterDescriptions);
    }

    private static OperationFilterContext CreateOperationFilterContext(
        MethodInfo methodInfo,
        params ApiParameterDescription[] parameterDescriptions)
    {
        var apiDescription = new ApiDescription
        {
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor
            {
                ControllerName = "Test",
                ActionName = methodInfo.Name,
                MethodInfo = methodInfo
            }
        };

        foreach (var param in parameterDescriptions)
        {
            apiDescription.ParameterDescriptions.Add(param);
        }

        var schemaRepository = new SchemaRepository();
        var schemaGenerator = new SchemaGenerator(new SchemaGeneratorOptions(), new Moq.Mock<ISerializerDataContractResolver>().Object);

        return new OperationFilterContext(
            apiDescription,
            schemaGenerator,
            schemaRepository,
            methodInfo);
    }

    // Test controller with various validation scenarios
    public class TestController
    {
        public void TestMethod() { }

        public void MethodWithExtension(
            [OpenApiExtension("x-custom-property", "custom-value")] string customParam) { }

        public void MethodWithRange([Range(0, 100)] int score) { }

        public void MethodWithStringLength([StringLength(50, MinimumLength = 3)] string username) { }

        public void MethodWithMinLength([MinLength(10)] string description) { }

        public void MethodWithMaxLength([MaxLength(100)] string title) { }

        public void MethodWithRequired([Required] string id) { }

        public void MethodWithMultipleValidations(
            [Required]
            [StringLength(100, MinimumLength = 5)]
            string email) { }
    }
}
