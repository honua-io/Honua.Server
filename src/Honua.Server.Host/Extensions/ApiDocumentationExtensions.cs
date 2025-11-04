// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Reflection;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Honua.Server.Host.OpenApi.Filters;

namespace Honua.Server.Host.Extensions;

/// <summary>
/// Extension methods for configuring API documentation with Swagger/OpenAPI.
/// </summary>
internal static class ApiDocumentationExtensions
{
    /// <summary>
    /// Adds Swagger/OpenAPI documentation generation services.
    /// Configures API metadata and JWT Bearer authentication support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The hosting environment (optional, for version info).</param>
    /// <param name="configureContactInfo">Optional action to configure contact information.</param>
    /// <param name="configureDeprecationInfo">Optional action to configure deprecation information.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IServiceCollection AddHonuaApiDocumentation(
        this IServiceCollection services,
        IHostEnvironment? environment = null,
        Action<ContactInfoOptions>? configureContactInfo = null,
        Action<DeprecationInfoOptions>? configureDeprecationInfo = null)
    {
        services.AddEndpointsApiExplorer();

        // Temporarily disable Swagger to bypass filter issues
        return services;

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Honua Server API",
                Version = "v1.0",
                Description = "OGC-compliant geospatial server with WMS, WFS, WMTS, WCS, CSW, OData, and STAC support. " +
                             "API versioning is supported via URL path (e.g., /v1/collections) or without version for default behavior.",
                Contact = new OpenApiContact
                {
                    Name = "Honua Team",
                    Url = new Uri("https://github.com/mikemcdougall/HonuaIO")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License"
                }
            });

            // Add security definition for Bearer tokens
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                    Array.Empty<string>()
                }
            });

            // Register enhanced OpenAPI filters

            // Operation Filters - applied to individual operations
            options.OperationFilter<SwaggerDefaultValues>();
            options.OperationFilter<DefaultValuesOperationFilter>();
            options.OperationFilter<ExampleValuesOperationFilter>();
            options.OperationFilter<SecurityRequirementsOperationFilter>();
            options.OperationFilter<SchemaExtensionsOperationFilter>();

            // Document Filters - applied to the entire document
            options.DocumentFilter<VersionInfoDocumentFilter>();

            var contactInfoOptions = new ContactInfoOptions();
            configureContactInfo?.Invoke(contactInfoOptions);
            options.DocumentFilter<ContactInfoDocumentFilter>(contactInfoOptions);

            var deprecationInfoOptions = new DeprecationInfoOptions();
            configureDeprecationInfo?.Invoke(deprecationInfoOptions);
            options.DocumentFilter<DeprecationInfoDocumentFilter>(deprecationInfoOptions);

            // Include XML comments for better documentation
            var xmlFiles = new[]
            {
                "Honua.Server.Host.xml",
                "Honua.Server.Core.xml"
            };

            foreach (var xmlFile in xmlFiles)
            {
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }
            }
        });

        return services;
    }

    /// <summary>
    /// Configures the Swagger UI middleware in the request pipeline.
    /// Makes API documentation available at /swagger endpoint in Development environment only.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for method chaining.</returns>
    public static WebApplication UseHonuaApiDocumentation(this WebApplication app)
    {
        // Temporarily disabled to bypass Swagger filter issues
        return app;

        // Only enable Swagger in Development to prevent information disclosure in production
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Honua Server API v1");
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "Honua Server API Documentation";
                options.DisplayRequestDuration();
            });
        }

        return app;
    }
}

/// <summary>
/// Swagger operation filter that adds default values and handles API versioning display in Swagger UI.
/// This ensures versioned endpoints display correctly in Swagger documentation.
/// </summary>
internal sealed class SwaggerDefaultValues : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Mark deprecated operations based on API description metadata
        if (context.ApiDescription.CustomAttributes().OfType<ObsoleteAttribute>().Any())
        {
            operation.Deprecated = true;
        }

        if (operation.Parameters == null)
        {
            return;
        }

        // Set default values and descriptions for parameters from API description metadata
        foreach (var parameter in operation.Parameters)
        {
            var description = context.ApiDescription.ParameterDescriptions
                .FirstOrDefault(p => p.Name == parameter.Name);

            if (description == null)
            {
                continue;
            }

            // Add parameter description from metadata if not already set
            parameter.Description ??= description.ModelMetadata?.Description;

            // Set default value for parameter if available
            if (parameter.Schema.Default == null &&
                description.DefaultValue != null &&
                description.DefaultValue is not DBNull &&
                description.ModelMetadata is { } modelMetadata)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(
                    description.DefaultValue,
                    modelMetadata.ModelType);
                parameter.Schema.Default = OpenApiAnyFactory.CreateFromJson(json);
            }

            // Mark required parameters
            parameter.Required |= description.IsRequired;
        }
    }
}
