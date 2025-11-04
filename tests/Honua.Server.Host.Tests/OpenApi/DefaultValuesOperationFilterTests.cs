using Honua.Server.Host.OpenApi.Filters;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Reflection;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="DefaultValuesOperationFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class DefaultValuesOperationFilterTests
{
    private readonly DefaultValuesOperationFilter _filter;

    public DefaultValuesOperationFilterTests()
    {
        _filter = new DefaultValuesOperationFilter();
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
    public void Apply_WithDefaultValueFromMetadata_SetsDefaultValue()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "limit",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var parameterDescription = new ApiParameterDescription
        {
            Name = "limit",
            DefaultValue = 10,
            ModelMetadata = CreateModelMetadata(typeof(int))
        };

        var context = CreateOperationFilterContext("TestMethod", parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters[0].Schema.Default);
    }

    [Fact]
    public void Apply_WithDefaultValueAttribute_SetsDefaultValue()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "pageSize",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithDefaultValue))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "pageSize"
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters[0].Schema.Default);
    }

    [Fact]
    public void Apply_WithOptionalParameter_SetsDefaultValue()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "optional",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithOptionalParameter))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "optional"
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters[0].Schema.Default);
    }

    [Fact]
    public void Apply_WithExistingDefaultValue_DoesNotOverride()
    {
        // Arrange
        var existingDefault = new Microsoft.OpenApi.Any.OpenApiInteger(999);
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "limit",
                    Schema = new OpenApiSchema
                    {
                        Default = existingDefault
                    }
                }
            }
        };

        var parameterDescription = new ApiParameterDescription
        {
            Name = "limit",
            DefaultValue = 10,
            ModelMetadata = CreateModelMetadata(typeof(int))
        };

        var context = CreateOperationFilterContext("TestMethod", parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Same(existingDefault, operation.Parameters[0].Schema.Default);
    }

    [Fact]
    public void Apply_WithNullDefaultValue_DoesNotSetDefault()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "optional",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var parameterDescription = new ApiParameterDescription
        {
            Name = "optional",
            DefaultValue = null
        };

        var context = CreateOperationFilterContext("TestMethod", parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Null(operation.Parameters[0].Schema.Default);
    }

    [Fact]
    public void Apply_WithDbNullDefaultValue_DoesNotSetDefault()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "optional",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var parameterDescription = new ApiParameterDescription
        {
            Name = "optional",
            DefaultValue = DBNull.Value
        };

        var context = CreateOperationFilterContext("TestMethod", parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Null(operation.Parameters[0].Schema.Default);
    }

    [Fact]
    public void Apply_WithStringDefaultValue_SetsDefaultCorrectly()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "format",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var parameterDescription = new ApiParameterDescription
        {
            Name = "format",
            DefaultValue = "json",
            ModelMetadata = CreateModelMetadata(typeof(string))
        };

        var context = CreateOperationFilterContext("TestMethod", parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters[0].Schema.Default);
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

    private static ModelMetadata CreateModelMetadata(Type type)
    {
        var provider = new EmptyModelMetadataProvider();
        return provider.GetMetadataForType(type);
    }

    // Test controller with various parameter scenarios
    public class TestController
    {
        public void TestMethod() { }

        public void MethodWithDefaultValue([DefaultValue(25)] int pageSize) { }

        public void MethodWithOptionalParameter(int optional = 100) { }

        public void MethodWithStringDefault(string format = "json") { }
    }
}
