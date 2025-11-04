// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Filters;

/// <summary>
/// Schema filter to mark required properties as not nullable in OpenAPI schema.
/// </summary>
public class RequiredNotNullableSchemaFilter : ISchemaFilter
{
    /// <summary>
    /// Applies the filter to mark required properties as not nullable.
    /// </summary>
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties == null || schema.Required == null)
            return;

        // For each required property, ensure it's marked as not nullable
        foreach (var requiredPropertyName in schema.Required)
        {
            var property = schema.Properties.FirstOrDefault(p =>
                string.Equals(p.Key, requiredPropertyName, StringComparison.OrdinalIgnoreCase));

            if (property.Value != null)
            {
                property.Value.Nullable = false;
            }
        }

        // Remove properties from required if they're marked as nullable
        var nullableProperties = schema.Properties
            .Where(p => p.Value.Nullable == true)
            .Select(p => p.Key)
            .ToList();

        foreach (var nullableProp in nullableProperties)
        {
            schema.Required.Remove(nullableProp);
        }
    }
}
