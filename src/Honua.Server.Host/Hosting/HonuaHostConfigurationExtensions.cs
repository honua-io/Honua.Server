// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿// using Honua.Server.Core.GitOps; // TODO: GitOps feature not yet implemented
using Honua.Server.Core.Extensions;

using Honua.Server.Enterprise.ETL;
using Honua.Server.Enterprise.Events;
using Honua.Server.Enterprise.Sensors.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.GeoEvent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        builder.Services.AddResourceAuthorization(builder.Configuration);

        // CORS
        builder.Services.AddHonuaCors();

        // Feature-specific services
        builder.Services.AddHonuaWfsServices(builder.Configuration);
        builder.Services.AddHonuaRasterServices();
        builder.Services.AddHonuaStacServices();
        builder.Services.AddHonuaCartoServices();
        builder.Services.AddSensorThings(builder.Configuration); // OGC SensorThings API v1.1

        // GeoEvent services (conditional - requires Postgres connection)
        var connectionString = builder.Configuration.GetConnectionString("Postgres")
            ?? builder.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddGeoEventServices(connectionString, builder.Configuration);

            // Register SignalR hub for real-time event streaming
            builder.Services.AddSingleton<IGeoEventBroadcaster, SignalRGeoEventBroadcaster>();

            // Register GeoETL services (Enterprise feature)
            builder.Services.AddGeoEtl(connectionString, usePostgresStore: true);

            // Register AI-powered workflow generation (optional, requires OpenAI configuration)
            builder.Services.AddGeoEtlAi(builder.Configuration);

            // Register GeoETL progress broadcaster for real-time workflow execution tracking
            builder.Services.AddSingleton<Honua.Server.Enterprise.ETL.Progress.IWorkflowProgressBroadcaster,
                Honua.Server.Enterprise.ETL.Progress.SignalRWorkflowProgressBroadcaster>();
        }
        else
        {
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
            logger.LogWarning("GeoEvent services not registered - PostgreSQL connection string not configured");
            logger.LogWarning("GeoETL services not registered - PostgreSQL connection string not configured");
        }

        // Health checks
        builder.Services.AddHonuaHealthChecks(builder.Configuration);

        // Observability
        builder.AddHonuaLogging(builder.Configuration);
        builder.Services.AddHonuaObservability(builder.Configuration);

        // MVC and API documentation
        var mvcBuilder = builder.Services.AddHonuaMvcServices();
        builder.Services.AddHonuaApiDocumentation(builder.Environment);

        // Admin UI SignalR for real-time updates
        builder.Services.AddAdminSignalR();

        // Alert management services
        builder.Services.AddAlertManagementServices(builder.Configuration);

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
