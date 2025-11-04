// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Attribute to specify example values for parameters in OpenAPI documentation.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
public sealed class SwaggerExampleAttribute : Attribute
{
    /// <summary>
    /// Gets the example value for the parameter.
    /// </summary>
    public object Example { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwaggerExampleAttribute"/> class.
    /// </summary>
    /// <param name="example">The example value to display in API documentation.</param>
    public SwaggerExampleAttribute(object example)
    {
        Example = example;
    }
}

/// <summary>
/// Operation filter that adds example values to operation parameters and request bodies
/// in the OpenAPI specification. This improves API documentation by providing concrete
/// examples of valid inputs.
/// </summary>
public sealed class ExampleValuesOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies example value population logic to the operation's parameters and request body.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being processed.</param>
    /// <param name="context">Context containing API metadata and parameter information.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add examples to parameters
        if (operation.Parameters != null && operation.Parameters.Any())
        {
            ApplyParameterExamples(operation, context);
        }

        // Add examples to request body
        if (operation.RequestBody?.Content != null)
        {
            ApplyRequestBodyExamples(operation.RequestBody, context);
        }
    }

    private static void ApplyParameterExamples(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters)
        {
            var parameterDescription = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(p => p.Name == parameter.Name);

            if (parameterDescription == null)
            {
                continue;
            }

            // Try to get example from method parameter attribute
            var methodParameter = context.MethodInfo.GetParameters()
                .FirstOrDefault(p => p.Name == parameterDescription.Name);

            if (methodParameter != null)
            {
                var exampleAttribute = methodParameter.GetCustomAttribute<SwaggerExampleAttribute>();
                if (exampleAttribute?.Example != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(
                        exampleAttribute.Example,
                        methodParameter.ParameterType);
                    parameter.Example = OpenApiAnyFactory.CreateFromJson(json);
                }
            }
        }
    }

    private static void ApplyRequestBodyExamples(OpenApiRequestBody requestBody, OperationFilterContext context)
    {
        // Get the request body parameter type
        var requestBodyParameter = context.ApiDescription.ParameterDescriptions
            .FirstOrDefault(p => p.Source.Id == "Body");

        if (requestBodyParameter?.Type == null)
        {
            return;
        }

        var exampleObject = GenerateExampleFromType(requestBodyParameter.Type);
        if (exampleObject != null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                exampleObject,
                requestBodyParameter.Type);

            foreach (var content in requestBody.Content.Values)
            {
                content.Example = OpenApiAnyFactory.CreateFromJson(json);
            }
        }
    }

    private static object? GenerateExampleFromType(Type type)
    {
        // Try to find a property with SwaggerExampleAttribute
        var exampleProperty = type.GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<SwaggerExampleAttribute>() != null);

        if (exampleProperty != null)
        {
            // Create instance and populate with examples
            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance != null)
                {
                    foreach (var prop in type.GetProperties())
                    {
                        var attr = prop.GetCustomAttribute<SwaggerExampleAttribute>();
                        if (attr != null && prop.CanWrite)
                        {
                            prop.SetValue(instance, attr.Example);
                        }
                    }
                    return instance;
                }
            }
            catch
            {
                // If we can't create an example, return null
                return null;
            }
        }

        return null;
    }
}
