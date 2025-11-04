// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Reflection;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Operation filter that adds default values to operation parameters in the OpenAPI specification.
/// This filter examines parameter metadata including DefaultValueAttribute and uses it to populate
/// the schema default value, improving API documentation clarity.
/// </summary>
public sealed class DefaultValuesOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies default value population logic to the operation's parameters.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being processed.</param>
    /// <param name="context">Context containing API metadata and parameter information.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters.IsNullOrEmpty())
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            var parameterDescription = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(p => p.Name == parameter.Name);

            if (parameterDescription == null)
            {
                continue;
            }

            // Apply default value from parameter metadata
            ApplyDefaultValue(parameter, parameterDescription);

            // Apply default value from method parameter attributes
            ApplyDefaultValueFromMethodParameter(parameter, context, parameterDescription);
        }
    }

    private static void ApplyDefaultValue(OpenApiParameter parameter, Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription parameterDescription)
    {
        if (parameter.Schema.Default != null)
        {
            return; // Already has a default value
        }

        if (parameterDescription.DefaultValue != null &&
            parameterDescription.DefaultValue is not DBNull &&
            parameterDescription.ModelMetadata is { } modelMetadata)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                parameterDescription.DefaultValue,
                modelMetadata.ModelType);
            parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
        }
    }

    private static void ApplyDefaultValueFromMethodParameter(
        OpenApiParameter parameter,
        OperationFilterContext context,
        Microsoft.AspNetCore.Mvc.ApiExplorer.ApiParameterDescription parameterDescription)
    {
        if (parameter.Schema.Default != null)
        {
            return; // Already has a default value
        }

        var methodInfo = context.MethodInfo;
        var parameterInfo = methodInfo.GetParameters()
            .FirstOrDefault(p => p.Name == parameterDescription.Name);

        if (parameterInfo == null)
        {
            return;
        }

        // Check for DefaultValueAttribute
        var defaultValueAttribute = parameterInfo.GetCustomAttribute<DefaultValueAttribute>();
        if (defaultValueAttribute?.Value != null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                defaultValueAttribute.Value,
                parameterInfo.ParameterType);
            parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
        }
        // Check for optional parameters with default values
        else if (parameterInfo.HasDefaultValue && parameterInfo.DefaultValue != null)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(
                parameterInfo.DefaultValue,
                parameterInfo.ParameterType);
            parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
        }
    }
}
