// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Attribute to specify custom OpenAPI extensions for parameters or schemas.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
public sealed class OpenApiExtensionAttribute : Attribute
{
    /// <summary>
    /// Gets the extension key (must start with "x-").
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the extension value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenApiExtensionAttribute"/> class.
    /// </summary>
    /// <param name="key">The extension key (must start with "x-").</param>
    /// <param name="value">The extension value.</param>
    public OpenApiExtensionAttribute(string key, string value)
    {
        if (!key.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("OpenAPI extension keys must start with 'x-'", nameof(key));
        }

        Key = key;
        Value = value;
    }
}

/// <summary>
/// Operation filter that adds custom schema extensions to parameters and schemas
/// in the OpenAPI specification. This allows for vendor-specific extensions
/// and additional metadata to be included in the API documentation.
/// </summary>
public sealed class SchemaExtensionsOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies schema extension logic to the operation's parameters.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being processed.</param>
    /// <param name="context">Context containing API metadata and parameter information.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters != null && operation.Parameters.Any())
        {
            ApplyParameterExtensions(operation, context);
        }

        // Add validation information to parameter descriptions
        if (operation.Parameters != null)
        {
            ApplyValidationMetadata(operation, context);
        }
    }

    private static void ApplyParameterExtensions(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters)
        {
            var parameterDescription = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(p => p.Name == parameter.Name);

            if (parameterDescription == null)
            {
                continue;
            }

            var methodParameter = context.MethodInfo.GetParameters()
                .FirstOrDefault(p => p.Name == parameterDescription.Name);

            if (methodParameter == null)
            {
                continue;
            }

            // Apply custom extensions from attributes
            var extensionAttributes = methodParameter.GetCustomAttributes<OpenApiExtensionAttribute>();
            foreach (var attr in extensionAttributes)
            {
                parameter.Extensions[attr.Key] = new OpenApiString(attr.Value);
            }
        }
    }

    private static void ApplyValidationMetadata(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var parameter in operation.Parameters)
        {
            var parameterDescription = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(p => p.Name == parameter.Name);

            if (parameterDescription?.Type == null)
            {
                continue;
            }

            var methodParameter = context.MethodInfo.GetParameters()
                .FirstOrDefault(p => p.Name == parameterDescription.Name);

            if (methodParameter == null)
            {
                continue;
            }

            var validationInfo = new List<string>();

            // Add range validation info
            var rangeAttribute = methodParameter.GetCustomAttribute<RangeAttribute>();
            if (rangeAttribute != null)
            {
                if (parameter.Schema != null)
                {
                    if (double.TryParse(rangeAttribute.Minimum?.ToString(), out var min))
                    {
                        parameter.Schema.Minimum = (decimal)min;
                    }
                    if (double.TryParse(rangeAttribute.Maximum?.ToString(), out var max))
                    {
                        parameter.Schema.Maximum = (decimal)max;
                    }
                }
                validationInfo.Add($"Must be between {rangeAttribute.Minimum} and {rangeAttribute.Maximum}");
            }

            // Add string length validation info
            var stringLengthAttribute = methodParameter.GetCustomAttribute<StringLengthAttribute>();
            if (stringLengthAttribute != null)
            {
                if (parameter.Schema != null)
                {
                    parameter.Schema.MaxLength = stringLengthAttribute.MaximumLength;
                    if (stringLengthAttribute.MinimumLength > 0)
                    {
                        parameter.Schema.MinLength = stringLengthAttribute.MinimumLength;
                    }
                }
                validationInfo.Add($"Length must be between {stringLengthAttribute.MinimumLength} and {stringLengthAttribute.MaximumLength}");
            }

            // Add min/max length validation info
            var minLengthAttribute = methodParameter.GetCustomAttribute<MinLengthAttribute>();
            if (minLengthAttribute != null && parameter.Schema != null)
            {
                parameter.Schema.MinLength = minLengthAttribute.Length;
                validationInfo.Add($"Minimum length: {minLengthAttribute.Length}");
            }

            var maxLengthAttribute = methodParameter.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLengthAttribute != null && parameter.Schema != null)
            {
                parameter.Schema.MaxLength = maxLengthAttribute.Length;
                validationInfo.Add($"Maximum length: {maxLengthAttribute.Length}");
            }

            // Add required validation info
            var requiredAttribute = methodParameter.GetCustomAttribute<RequiredAttribute>();
            if (requiredAttribute != null)
            {
                parameter.Required = true;
                validationInfo.Add("Required");
            }

            // Append validation info to description
            if (validationInfo.Any())
            {
                var validationText = $"**Validation:** {string.Join(", ", validationInfo)}";
                parameter.Description = parameter.Description.IsNullOrEmpty()
                    ? validationText
                    : $"{parameter.Description}\n\n{validationText}";
            }
        }
    }
}
