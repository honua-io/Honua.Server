// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.Observability;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Provides API endpoints for runtime logging configuration.
/// Allows administrators to change log levels for specific categories at runtime.
/// </summary>
internal static class LoggingEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all logging configuration management endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The route group builder for additional configuration.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Viewing available log levels
    /// - Getting current log level configuration for all categories
    /// - Setting log levels for specific categories at runtime
    /// - Removing runtime overrides to revert to appsettings.json
    /// - Writing test log messages
    /// - Clearing all runtime overrides
    /// </remarks>
    /// <example>
    /// Example request to set log level:
    /// <code>
    /// PATCH /admin/logging/categories/Honua.Server.Core.Data
    /// {
    ///   "level": "Trace"
    /// }
    /// </code>
    /// </example>
    public static RouteGroupBuilder MapLoggingConfiguration(this WebApplication app)
    {
        Guard.NotNull(app);

        var group = app.MapGroup("/admin/logging");
        return MapLoggingConfigurationCore(group);
    }

    public static RouteGroupBuilder MapLoggingConfiguration(this RouteGroupBuilder group)
    {
        Guard.NotNull(group);

        return MapLoggingConfigurationCore(group.MapGroup("/admin/logging"));
    }

    private static RouteGroupBuilder MapLoggingConfigurationCore(RouteGroupBuilder group)
    {
        group.RequireAuthorization("RequireAdministrator");

        // GET /admin/logging/levels - Get available log levels
        group.MapGet("/levels", () =>
        {
            return Results.Ok(new
            {
                levels = new[]
                {
                    new { value = 0, name = "Trace", description = "Most verbose - includes sensitive data" },
                    new { value = 1, name = "Debug", description = "Debugging diagnostics" },
                    new { value = 2, name = "Information", description = "General flow of application" },
                    new { value = 3, name = "Warning", description = "Abnormal or unexpected events" },
                    new { value = 4, name = "Error", description = "Errors and exceptions" },
                    new { value = 5, name = "Critical", description = "Critical failures" },
                    new { value = 6, name = "None", description = "Disable logging" }
                },
                note = "Set logging levels at runtime using PATCH /admin/logging/categories/{category}"
            });
        });

        // GET /admin/logging/categories - Get current log level configuration
        group.MapGet("/categories", ([FromServices] RuntimeLoggingConfigurationService loggingConfig) =>
        {
            var currentLevels = loggingConfig.GetAllLevels();

            var recommendedCategories = new
            {
                Default = new { description = "Default log level for all categories", recommended = "Information" },
                Microsoft_AspNetCore = new { description = "ASP.NET Core framework logs", recommended = "Warning" },
                Microsoft_EntityFrameworkCore = new { description = "Entity Framework Core", recommended = "Warning" },
                Honua_Server = new { description = "All Honua server components", recommended = "Information" },
                Honua_Server_Core = new { description = "Core business logic", recommended = "Debug" },
                Honua_Server_Host = new { description = "HTTP hosting layer", recommended = "Information" },
                Honua_Server_Core_Data = new { description = "Database operations", recommended = "Debug" },
                Honua_Server_Core_Query = new { description = "Query translation and execution", recommended = "Debug" },
                Honua_Server_Core_Raster = new { description = "Raster tile operations", recommended = "Information" },
                Honua_Server_Core_Authentication = new { description = "Authentication and authorization", recommended = "Information" },
                Honua_Server_Core_Metadata = new { description = "Metadata loading and validation", recommended = "Information" }
            };

            return Results.Ok(new
            {
                current = currentLevels.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new { level = kvp.Value.ToString(), value = (int)kvp.Value }),
                recommended = recommendedCategories,
                note = "Use PATCH /admin/logging/categories/{category} to set log levels. Use DELETE to remove overrides. Category names use '.' as separator (e.g. 'Honua.Server.Core')",
                example = new
                {
                    method = "PATCH",
                    url = "/admin/logging/categories/Honua.Server.Core.Data",
                    body = new { level = "Trace" }
                }
            });
        });

        // PATCH /admin/logging/categories/{category} - Set log level for a category
        group.MapPatch("/categories/{*category}", (
            string category,
            SetLogLevelRequest request,
            [FromServices] RuntimeLoggingConfigurationService loggingConfig,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Results.BadRequest(new { error = "Category cannot be empty" });
            }

            // Parse log level
            if (!Enum.TryParse<LogLevel>(request.Level, ignoreCase: true, out var logLevel))
            {
                return Results.BadRequest(new
                {
                    error = $"Invalid log level: {request.Level}",
                    validLevels = Enum.GetNames<LogLevel>()
                });
            }

            // Set the runtime override
            loggingConfig.SetLevel(category, logLevel);

            // Create a test logger to verify behavior
            var testLogger = loggerFactory.CreateLogger(category);

            return Results.Ok(new
            {
                status = "updated",
                category,
                level = logLevel.ToString(),
                levelValue = (int)logLevel,
                message = $"Log level for '{category}' set to {logLevel}",
                note = "This change is in-memory only and applies immediately. To persist, update appsettings.json Logging:LogLevel section.",
                effective = new
                {
                    trace = testLogger.IsEnabled(LogLevel.Trace),
                    debug = testLogger.IsEnabled(LogLevel.Debug),
                    information = testLogger.IsEnabled(LogLevel.Information),
                    warning = testLogger.IsEnabled(LogLevel.Warning),
                    error = testLogger.IsEnabled(LogLevel.Error),
                    critical = testLogger.IsEnabled(LogLevel.Critical)
                }
            });
        });

        // DELETE /admin/logging/categories/{category} - Remove runtime override
        group.MapDelete("/categories/{*category}", (
            string category,
            [FromServices] RuntimeLoggingConfigurationService loggingConfig) =>
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return Results.BadRequest(new { error = "Category cannot be empty" });
            }

            var removed = loggingConfig.RemoveLevel(category);

            if (!removed)
            {
                return GeoservicesRESTErrorHelper.NotFoundWithMessage($"No runtime override found for category '{category}'");
            }

            return Results.Ok(new
            {
                status = "removed",
                category,
                message = $"Runtime log level override removed for '{category}'. Reverted to appsettings.json configuration."
            });
        });

        // POST /admin/logging/test - Write test log messages
        group.MapPost("/test", (
            TestLogRequest request,
            [FromServices] ILoggerFactory loggerFactory) =>
        {
            var category = request.Category ?? "Honua.Server.Test";
            var logger = loggerFactory.CreateLogger(category);

            if (!Enum.TryParse<LogLevel>(request.Level, ignoreCase: true, out var logLevel))
            {
                return Results.BadRequest(new
                {
                    error = $"Invalid log level: {request.Level}",
                    validLevels = Enum.GetNames<LogLevel>()
                });
            }

            var message = request.Message ?? $"Test log message at {logLevel} level - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}Z";

            logger.Log(logLevel, message);

            return Results.Ok(new
            {
                status = "logged",
                category,
                level = logLevel.ToString(),
                message,
                enabled = logger.IsEnabled(logLevel),
                note = logger.IsEnabled(logLevel)
                    ? "Message was logged to console (check logs)"
                    : $"Message was NOT logged - {logLevel} is disabled for category '{category}'"
            });
        });

        // DELETE /admin/logging/categories - Clear all runtime overrides
        group.MapDelete("/categories", ([FromServices] RuntimeLoggingConfigurationService loggingConfig) =>
        {
            var currentCount = loggingConfig.GetAllLevels().Count;
            loggingConfig.Clear();

            return Results.Ok(new
            {
                status = "cleared",
                count = currentCount,
                message = $"Cleared {currentCount} runtime log level override(s). All categories reverted to appsettings.json configuration."
            });
        });

        return group;
    }

    /// <summary>
    /// Request model for setting a log level.
    /// </summary>
    /// <param name="Level">The log level name (Trace, Debug, Information, Warning, Error, Critical, None).</param>
    private sealed record SetLogLevelRequest(string Level);

    /// <summary>
    /// Request model for writing a test log message.
    /// </summary>
    /// <param name="Category">The logging category (optional, defaults to "Honua.Server.Test").</param>
    /// <param name="Level">The log level for the test message.</param>
    /// <param name="Message">The message to log (optional, auto-generated if not provided).</param>
    private sealed record TestLogRequest(string? Category, string Level, string? Message);
}
