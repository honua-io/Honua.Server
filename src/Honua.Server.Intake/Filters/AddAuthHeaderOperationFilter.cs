// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Filters;

/// <summary>
/// Operation filter to add authentication information to Swagger documentation.
/// </summary>
public class AddAuthHeaderOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies the filter to add authorization requirements to operations.
    /// </summary>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check if the endpoint requires authorization
        var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AuthorizeAttribute>()
            .Any() ?? false;

        var allowAnonymous = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
            .Union(context.MethodInfo.GetCustomAttributes(true))
            .OfType<AllowAnonymousAttribute>()
            .Any() ?? false;

        if (!hasAuthorize || allowAnonymous)
        {
            // Endpoint is public - no auth required
            operation.Security?.Clear();

            // Add note to description
            if (operation.Description == null)
            {
                operation.Description = "**Authentication:** None required (public endpoint)\n\n";
            }
            else
            {
                operation.Description = "**Authentication:** None required (public endpoint)\n\n" + operation.Description;
            }

            return;
        }

        // Endpoint requires authentication
        operation.Responses.TryAdd("401", new OpenApiResponse
        {
            Description = "Unauthorized - Missing or invalid authentication token"
        });

        operation.Responses.TryAdd("403", new OpenApiResponse
        {
            Description = "Forbidden - Insufficient permissions for this operation"
        });

        // Add authentication note to description
        if (operation.Description == null)
        {
            operation.Description = "**Authentication:** Required (Bearer token or API key)\n\n";
        }
        else
        {
            operation.Description = "**Authentication:** Required (Bearer token or API key)\n\n" + operation.Description;
        }

        // Ensure security requirement is present
        if (operation.Security == null || !operation.Security.Any())
        {
            operation.Security = new[]
            {
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] { }
                    }
                }
            }.ToList();
        }
    }
}
