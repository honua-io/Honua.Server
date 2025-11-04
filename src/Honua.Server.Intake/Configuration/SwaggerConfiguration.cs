// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Reflection;
using Honua.Server.Intake.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Honua.Server.Intake.Configuration;

/// <summary>
/// Swagger/OpenAPI configuration for Honua Build Orchestrator API.
/// </summary>
public static class SwaggerConfiguration
{
    /// <summary>
    /// Adds Swagger documentation generation with comprehensive configuration.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // API Information
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Honua Build Orchestrator API",
                Version = "v1",
                Description = @"
# Honua Build Orchestrator API

The Honua Build Orchestrator API provides a comprehensive platform for building, deploying, and managing custom geospatial server containers.

## Key Features

### 🤖 AI-Guided Configuration (Intake API)
- Natural language conversation interface
- Intelligent requirement extraction
- Automatic cost estimation
- Cloud-optimized recommendations

### 🏗️ Custom Container Builds (Build API)
- Protocol selection (ESRI REST, WFS, WMS, WMTS, OGC API, etc.)
- Database integration (PostgreSQL, BigQuery, Snowflake, etc.)
- Multi-cloud support (AWS, Azure, GCP)
- Architecture optimization (ARM64 for cost savings, x64 for compatibility)
- Intelligent build caching for instant deployment

### 📜 License Management (License API)
- Tier-based licensing (Core, Pro, Enterprise, Enterprise ASP)
- Automated license generation and validation
- Credential management and rotation
- Usage tracking and compliance

### 🔐 Registry Management (Registry API)
- Multi-cloud registry provisioning (ECR, ACR, Artifact Registry, GHCR)
- Automated credential management
- Secure image distribution
- Cross-registry synchronization

### ⚙️ Administration (Admin API)
- Build queue monitoring
- System health checks
- Configuration management
- Analytics and reporting

## Getting Started

1. **Start a Conversation**: Use `/api/intake/start` to begin configuring your deployment
2. **Answer Questions**: The AI will guide you through requirements
3. **Trigger Build**: Once complete, use `/api/intake/build` to start the build process
4. **Monitor Progress**: Track your build with `/api/intake/builds/{buildId}/status`
5. **Deploy**: Receive container image with pull credentials

## Authentication

Most endpoints require JWT bearer token authentication. Include your token in the Authorization header:

```
Authorization: Bearer {your-jwt-token}
```

Obtain tokens from your Honua account dashboard or via the authentication API.

## Rate Limits

- **Free Tier**: 100 requests/hour
- **Pro Tier**: 1,000 requests/hour
- **Enterprise**: 10,000 requests/hour
- **Enterprise ASP**: Unlimited

## Support

- Documentation: https://docs.honua.io
- Support: support@honua.io
- Status: https://status.honua.io
",
                Contact = new OpenApiContact
                {
                    Name = "Honua Support",
                    Email = "support@honua.io",
                    Url = new Uri("https://honua.io/support")
                },
                License = new OpenApiLicense
                {
                    Name = "Commercial License",
                    Url = new Uri("https://honua.io/license")
                },
                TermsOfService = new Uri("https://honua.io/terms")
            });

            // JWT Bearer Authentication
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = @"
JWT Authorization header using the Bearer scheme.

Enter your JWT token in the text input below.

Example: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...'

You do not need to add 'Bearer' prefix - it will be added automatically.
"
            });

            // API Key Authentication (alternative)
            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "X-API-Key",
                Description = "API Key authentication. Provide your API key in the X-API-Key header."
            });

            // Apply security globally
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

            // XML Documentation
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            // Operation Filters
            options.OperationFilter<AddAuthHeaderOperationFilter>();
            options.OperationFilter<ExampleValuesOperationFilter>();
            options.OperationFilter<AddResponseHeadersOperationFilter>();

            // Schema Filters
            options.SchemaFilter<RequiredNotNullableSchemaFilter>();
            options.SchemaFilter<EnumSchemaFilter>();

            // Document Filters
            options.DocumentFilter<TagDescriptionDocumentFilter>();

            // Custom operation ID
            options.CustomOperationIds(apiDesc =>
            {
                return apiDesc.TryGetMethodInfo(out var methodInfo)
                    ? methodInfo.Name
                    : null;
            });

            // Order actions by relative path
            options.OrderActionsBy(apiDesc => apiDesc.RelativePath);

            // Enable annotations
            options.EnableAnnotations();

            // Tag all endpoints by controller
            options.TagActionsBy(api =>
            {
                if (api.GroupName != null)
                {
                    return new[] { api.GroupName };
                }

                if (api.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName))
                {
                    return new[] { controllerName };
                }

                throw new InvalidOperationException("Unable to determine tag for endpoint.");
            });
        });

        return services;
    }

    /// <summary>
    /// Configures Swagger UI with ReDoc alternative.
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseHonuaSwagger(this IApplicationBuilder app)
    {
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "api-docs/{documentName}/openapi.json";
            options.PreSerializeFilters.Add((swagger, httpReq) =>
            {
                swagger.Servers = new[]
                {
                    new OpenApiServer
                    {
                        Url = $"{httpReq.Scheme}://{httpReq.Host.Value}",
                        Description = "Current server"
                    },
                    new OpenApiServer
                    {
                        Url = "https://api.honua.io",
                        Description = "Production API"
                    },
                    new OpenApiServer
                    {
                        Url = "https://api-staging.honua.io",
                        Description = "Staging API"
                    }
                };
            });
        });

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/api-docs/v1/openapi.json", "Honua Build Orchestrator API v1");
            options.RoutePrefix = "api-docs";
            options.DocumentTitle = "Honua Build Orchestrator API Documentation";

            // Enhanced UI settings
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.EnableValidator();
            options.DefaultModelsExpandDepth(2);
            options.DefaultModelExpandDepth(2);
            options.DisplayOperationId();
            options.EnableTryItOutByDefault();

            // Custom CSS for branding
            options.InjectStylesheet("/swagger-ui/custom.css");

            // Custom JavaScript
            options.InjectJavascript("/swagger-ui/custom.js");
        });

        // ReDoc alternative documentation
        app.UseReDoc(options =>
        {
            options.SpecUrl = "/api-docs/v1/openapi.json";
            options.RoutePrefix = "docs";
            options.DocumentTitle = "Honua Build Orchestrator API Reference";

            // ReDoc options
            options.EnableUntrustedSpec();
            options.ScrollYOffset(10);
            options.HideHostname();
            options.HideDownloadButton();
            options.ExpandResponses("200,201");
            options.RequiredPropsFirst();
            options.NoAutoAuth();
            options.PathInMiddlePanel();
            // options.HideSingleRequestSampleTab(); // Property removed in newer version
            // options.MenuToggle(); // Property removed in newer version
        });

        return app;
    }
}
