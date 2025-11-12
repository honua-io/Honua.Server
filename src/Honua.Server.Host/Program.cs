// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// VALIDATE CRITICAL CONFIGURATION BEFORE BUILDING
// Skip validation in test environments where WebApplicationFactory manages configuration
var isTestEnvironment = builder.Environment.EnvironmentName == "Test" ||
                        builder.Environment.EnvironmentName == "Testing" ||
                        Environment.GetEnvironmentVariable("ASPNETCORE_TEST_RUNNER") == "true";

var config = builder.Configuration;
var validationErrors = new List<string>();

// Check Redis connection - required in Production for distributed rate limiting
var redisConnection = config.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnection))
{
    if (builder.Environment.IsProduction())
    {
        validationErrors.Add("ConnectionStrings:Redis is required in Production for distributed rate limiting");
    }
    else
    {
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("Startup");
        logger.LogWarning(
            "Redis not configured. Using in-memory rate limiting. " +
            "This is acceptable for Development/Testing but NOT suitable for multi-instance Production deployments.");
    }
}
else if (redisConnection.Contains("localhost") && builder.Environment.IsProduction())
{
    validationErrors.Add("ConnectionStrings:Redis points to localhost in Production environment");
}

// Check metadata configuration
var metadataProvider = config.GetValue<string>("honua:metadata:provider");
var metadataPath = config.GetValue<string>("honua:metadata:path");

if (string.IsNullOrWhiteSpace(metadataProvider))
{
    validationErrors.Add("honua:metadata:provider is required");
}

if (string.IsNullOrWhiteSpace(metadataPath))
{
    validationErrors.Add("honua:metadata:path is required");
}

// Check allowed hosts in production
if (builder.Environment.IsProduction())
{
    var allowedHosts = config.GetValue<string>("AllowedHosts");
    if (allowedHosts == "*")
    {
        validationErrors.Add("AllowedHosts must not be '*' in Production. Specify actual domains.");
    }
    else if (string.IsNullOrWhiteSpace(allowedHosts))
    {
        validationErrors.Add("AllowedHosts must be configured in Production. Empty or null values accept any host header, enabling host-header injection attacks.");
    }
}

// Check CORS configuration
var corsAllowAnyOrigin = config.GetValue<bool>("honua:cors:allowAnyOrigin");
if (corsAllowAnyOrigin && builder.Environment.IsProduction())
{
    validationErrors.Add("CORS allowAnyOrigin must be false in Production");
}

// If validation failed, log and exit (unless in test environment)
if (validationErrors.Any() && !isTestEnvironment)
{
    var logger = LoggerFactory.Create(b => b.AddConsole())
        .CreateLogger("Startup");

    logger.LogCritical(
        "CONFIGURATION VALIDATION FAILED:{NewLine}{Errors}{NewLine}" +
        "Application cannot start. Fix configuration and try again.",
        Environment.NewLine,
        string.Join(Environment.NewLine, validationErrors.Select(e => $"  - {e}")),
        Environment.NewLine);

    throw new InvalidOperationException(
        $"Configuration validation failed with {validationErrors.Count} error(s). " +
        "See log output for details.");
}
else if (validationErrors.Any() && isTestEnvironment)
{
    // In test environment, log warnings but don't fail
    var logger = LoggerFactory.Create(b => b.AddConsole())
        .CreateLogger("Startup");

    logger.LogWarning(
        "Configuration validation warnings in test environment (not enforced):{NewLine}{Warnings}",
        Environment.NewLine,
        string.Join(Environment.NewLine, validationErrors.Select(e => $"  - {e}")));
}

builder.ConfigureHonuaServices();

var app = builder.Build();

// Validate required services were registered
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Check critical services
    var metadataRegistry = services.GetService<IMetadataRegistry>();
    if (metadataRegistry == null)
    {
        throw new InvalidOperationException(
            "IMetadataRegistry not registered. Core services initialization failed.");
    }

    var dataStoreFactory = services.GetService<IDataStoreProviderFactory>();
    if (dataStoreFactory == null)
    {
        throw new InvalidOperationException(
            "IDataStoreProviderFactory not registered. Data access initialization failed.");
    }
}

app.ConfigureHonuaRequestPipeline();

// Register conditional service endpoints (OData, OpenRosa, etc.)
app.MapConditionalServiceEndpoints();

app.Run();

// Make Program class visible to tests
public partial class Program { }
