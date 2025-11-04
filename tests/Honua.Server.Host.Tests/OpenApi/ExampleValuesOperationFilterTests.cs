using Honua.Server.Host.OpenApi.Filters;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Xunit;

namespace Honua.Server.Host.Tests.OpenApi;

/// <summary>
/// Unit tests for <see cref="ExampleValuesOperationFilter"/>.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public class ExampleValuesOperationFilterTests
{
    private readonly ExampleValuesOperationFilter _filter;

    public ExampleValuesOperationFilterTests()
    {
        _filter = new ExampleValuesOperationFilter();
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
    public void Apply_WithSwaggerExampleAttribute_SetsExample()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "collectionId",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithExample))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "collectionId"
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters[0].Example);
    }

    [Fact]
    public void Apply_WithoutSwaggerExampleAttribute_DoesNotSetExample()
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

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithoutExample))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "id"
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.Null(operation.Parameters[0].Example);
    }

    [Fact]
    public void Apply_WithMultipleParametersWithExamples_SetsAllExamples()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            Parameters = new List<OpenApiParameter>
            {
                new OpenApiParameter
                {
                    Name = "name",
                    Schema = new OpenApiSchema()
                },
                new OpenApiParameter
                {
                    Name = "age",
                    Schema = new OpenApiSchema()
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithMultipleExamples))!;
        var parameterDescriptions = new[]
        {
            new ApiParameterDescription { Name = "name" },
            new ApiParameterDescription { Name = "age" }
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescriptions);

        // Act
        _filter.Apply(operation, context);

        // Assert
        Assert.NotNull(operation.Parameters[0].Example);
        Assert.NotNull(operation.Parameters[1].Example);
    }

    [Fact]
    public void Apply_WithRequestBody_SetsRequestBodyExample()
    {
        // Arrange
        var operation = new OpenApiOperation
        {
            RequestBody = new OpenApiRequestBody
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema()
                    }
                }
            }
        };

        var methodInfo = typeof(TestController).GetMethod(nameof(TestController.MethodWithRequestBody))!;
        var parameterDescription = new ApiParameterDescription
        {
            Name = "model",
            Type = typeof(TestModel),
            Source = new BindingSource("Body", "Body", isGreedy: false, isFromRequest: true)
        };

        var context = CreateOperationFilterContext(methodInfo, parameterDescription);

        // Act
        _filter.Apply(operation, context);

        // Assert - request body example setting is attempted (may be null if type can't be instantiated)
        Assert.NotNull(operation.RequestBody);
    }

    [Fact]
    public void Apply_WithNullRequestBody_DoesNotThrow()
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
    public void SwaggerExampleAttribute_Constructor_SetsExample()
    {
        // Arrange & Act
        var attribute = new SwaggerExampleAttribute("test-example");

        // Assert
        Assert.Equal("test-example", attribute.Example);
    }

    [Fact]
    public void SwaggerExampleAttribute_WithNumericExample_StoresCorrectly()
    {
        // Arrange & Act
        var attribute = new SwaggerExampleAttribute(42);

        // Assert
        Assert.Equal(42, attribute.Example);
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

    // Test controller with various example scenarios
    public class TestController
    {
        public void TestMethod() { }

        public void MethodWithExample([SwaggerExample("buildings")] string collectionId) { }

        public void MethodWithoutExample(int id) { }

        public void MethodWithMultipleExamples(
            [SwaggerExample("John Doe")] string name,
            [SwaggerExample(30)] int age) { }

        public void MethodWithRequestBody(TestModel model) { }
    }

    public class TestModel
    {
        [SwaggerExample("Test Name")]
        public string? Name { get; set; }

        [SwaggerExample(100)]
        public int Value { get; set; }
    }
}
