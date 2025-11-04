// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿// using Honua.Server.Core.GitOps; // TODO: GitOps feature not yet implemented
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Server.Host.Hosting;

/// <summary>
/// Extensions for configuring Honua Server hosting and services.
/// </summary>
internal static class HonuaHostConfigurationExtensions
{
    /// <summary>
    /// Configures all Honua services including core services, authentication, observability, and features.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The configured web application builder.</returns>
    public static WebApplicationBuilder ConfigureHonuaServices(this WebApplicationBuilder builder)
    {
        // Core services and infrastructure
        builder.Services.AddHonuaCoreServices(builder.Configuration, builder.Environment.ContentRootPath);

        // Localization support for multiple languages
        builder.Services.AddHonuaLocalization();

        // Performance optimizations
        builder.Services.AddHonuaPerformanceOptimizations(builder.Configuration);
        // Rate limiting - Handled by YARP gateway (not at application level)
        builder.ConfigureRequestLimits();

        // Authentication and authorization
        builder.Services.AddHonuaAuthentication(builder.Configuration);
        builder.Services.AddHonuaAuthorization(builder.Configuration);

        // CORS
        builder.Services.AddHonuaCors();

        // Feature-specific services
        builder.Services.AddHonuaWfsServices(builder.Configuration);
        builder.Services.AddHonuaRasterServices();
        builder.Services.AddHonuaStacServices();
        builder.Services.AddHonuaCartoServices();

        // Health checks
        builder.Services.AddHonuaHealthChecks(builder.Configuration);

        // Observability
        builder.AddHonuaLogging(builder.Configuration);
        builder.Services.AddHonuaObservability(builder.Configuration);

        // MVC and API documentation
        var mvcBuilder = builder.Services.AddHonuaMvcServices();
        builder.Services.AddHonuaApiDocumentation(builder.Environment);

        // OData (conditional)
        var odataEnabled = builder.Configuration.GetValue<bool?>("honua:services:odata:enabled") ?? true;
        if (odataEnabled)
        {
            builder.Services.AddHonuaODataServices(builder.Configuration, mvcBuilder);
        }

        // Security and schema validation
        builder.Services.AddHonuaSecurityValidation();
        builder.Services.AddHonuaSchemaValidation(builder.Configuration);

        // CSRF protection for browser clients
        builder.Services.AddHonuaCsrfProtection(builder.Environment);

        // GitOps (conditional) - TODO: Not yet implemented
        // var gitOpsEnabled = builder.Configuration.GetValue<bool?>("GitOps:Enabled") ?? false;
        // if (gitOpsEnabled)
        // {
        //     builder.Services.AddGitOps(builder.Configuration);
        // }

        return builder;
    }
    /// <summary>
    /// Configures the Honua request pipeline with middleware and endpoint routing.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The configured web application.</returns>
    public static WebApplication ConfigureHonuaRequestPipeline(this WebApplication app)
    {
        // Configure middleware pipeline in the correct order
        app.UseHonuaMiddlewarePipeline();

        // Map all endpoints
        app.MapHonuaEndpoints();

        // Configure metrics endpoint if enabled
        app.UseHonuaMetricsEndpoint();

        return app;
    }
}
