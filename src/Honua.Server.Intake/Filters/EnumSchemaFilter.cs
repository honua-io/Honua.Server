// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Filters;

/// <summary>
/// Schema filter to provide better enum documentation with descriptions.
/// </summary>
public class EnumSchemaFilter : ISchemaFilter
{
    /// <summary>
    /// Applies the filter to enhance enum documentation.
    /// </summary>
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Enum.Clear();
            var enumValues = Enum.GetValues(context.Type);
            var enumNames = Enum.GetNames(context.Type);

            for (int i = 0; i < enumNames.Length; i++)
            {
                schema.Enum.Add(new OpenApiString(enumNames[i]));
            }

            // Add enum descriptions
            var descriptions = enumNames.Select(name =>
            {
                var memberInfo = context.Type.GetField(name);
                var attr = memberInfo?.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                    .FirstOrDefault() as System.ComponentModel.DescriptionAttribute;
                return $"- `{name}`: {attr?.Description ?? name}";
            });

            schema.Description = string.IsNullOrEmpty(schema.Description)
                ? string.Join("\n", descriptions)
                : schema.Description + "\n\n**Values:**\n" + string.Join("\n", descriptions);

            schema.Type = "string";
            schema.Format = null;
        }
    }
}
