// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Operation filter that automatically adds security requirements to operations
/// based on authorization attributes. This ensures that endpoints requiring authentication
/// are properly documented with security schemes in the OpenAPI specification.
/// </summary>
public sealed class SecurityRequirementsOperationFilter : IOperationFilter
{
    /// <summary>
    /// Applies security requirement logic to the operation based on authorization attributes.
    /// </summary>
    /// <param name="operation">The OpenAPI operation being processed.</param>
    /// <param name="context">Context containing API metadata and method information.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Check if the endpoint allows anonymous access
        var allowAnonymous = context.MethodInfo.GetCustomAttributes<AllowAnonymousAttribute>().Any()
            || context.MethodInfo.DeclaringType?.GetCustomAttributes<AllowAnonymousAttribute>().Any() == true;

        if (allowAnonymous)
        {
            // Remove any security requirements for anonymous endpoints
            operation.Security?.Clear();
            return;
        }

        // Check if authorization is required at method or controller level
        var authorizeAttributes = GetAuthorizeAttributes(context);

        if (!authorizeAttributes.Any())
        {
            return; // No authorization required
        }

        // Add Bearer token security requirement
        var bearerScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        };

        operation.Security ??= new List<OpenApiSecurityRequirement>();

        // Extract required roles and policies
        var roles = authorizeAttributes
            .Where(a => !a.Roles.IsNullOrEmpty())
            .SelectMany(a => a.Roles!.Split(','))
            .Select(r => r.Trim())
            .Distinct()
            .ToList();

        var policies = authorizeAttributes
            .Where(a => !a.Policy.IsNullOrEmpty())
            .Select(a => a.Policy!)
            .Distinct()
            .ToList();

        // Combine roles and policies as scopes
        var scopes = new List<string>();
        scopes.AddRange(roles);
        scopes.AddRange(policies);

        var securityRequirement = new OpenApiSecurityRequirement
        {
            { bearerScheme, scopes }
        };

        operation.Security.Add(securityRequirement);

        // Add description about required authorization
        if (roles.Any() || policies.Any())
        {
            var authDescription = new List<string>();

            if (roles.Any())
            {
                authDescription.Add($"Required roles: {string.Join(", ", roles)}");
            }

            if (policies.Any())
            {
                authDescription.Add($"Required policies: {string.Join(", ", policies)}");
            }

            operation.Description = operation.Description.IsNullOrEmpty()
                ? string.Join(". ", authDescription)
                : $"{operation.Description}\n\n**Authorization:** {string.Join(". ", authDescription)}.";
        }
    }

    private static IEnumerable<AuthorizeAttribute> GetAuthorizeAttributes(OperationFilterContext context)
    {
        // Get authorize attributes from method
        var methodAttributes = context.MethodInfo.GetCustomAttributes<AuthorizeAttribute>();

        // Get authorize attributes from controller
        var controllerAttributes = context.MethodInfo.DeclaringType?
            .GetCustomAttributes<AuthorizeAttribute>() ?? Enumerable.Empty<AuthorizeAttribute>();

        return methodAttributes.Concat(controllerAttributes);
    }
}
