// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Validation;

/// <summary>
/// Extension methods for configuring validation in the application.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Adds validation services to the service collection.
    /// </summary>
    public static IServiceCollection AddHonuaValidation(this IServiceCollection services)
    {
        // Configure API behavior for automatic model validation
        services.Configure<ApiBehaviorOptions>(options =>
        {
            // Suppress the default model state validation filter
            // We'll use our custom ValidationMiddleware instead
            options.SuppressModelStateInvalidFilter = false;

            // Customize the automatic response
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(e => e.Value?.Errors.Count > 0)
                    .ToDictionary(
                        e => ToCamelCase(e.Key),
                        e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
                    );

                var problemDetails = new ValidationProblemDetails(errors)
                {
                    Type = "https://tools.ietf.org/html/rfc7807",
                    Title = "One or more validation errors occurred.",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = context.HttpContext.Request.Path
                };

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        // Add model validation
        services.AddControllers(options =>
        {
            // Add the model state validation filter globally
            options.Filters.Add<ValidateModelStateAttribute>();
        });

        return services;
    }

    /// <summary>
    /// Adds validation middleware to the application pipeline.
    /// </summary>
    public static IApplicationBuilder UseHonuaValidation(this IApplicationBuilder app)
    {
        app.UseMiddleware<ValidationMiddleware>();
        return app;
    }

    private static string ToCamelCase(string value)
    {
        if (value.IsNullOrEmpty() || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }
}
