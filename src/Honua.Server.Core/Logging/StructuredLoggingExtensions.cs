// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Logging;

/// <summary>
/// Extension methods for structured logging to standardize common logging patterns.
/// Provides consistent, structured logging across the application with predefined templates
/// for common scenarios like operation failures, validation errors, and resource access issues.
/// </summary>
public static class StructuredLoggingExtensions
{
    /// <summary>
    /// Logs an operation failure with exception details and context.
    /// Use for operations that fail with exceptions (e.g., geometry operations, data processing).
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ex">The exception that caused the failure.</param>
    /// <param name="operation">The name of the operation that failed.</param>
    /// <param name="context">Optional context (e.g., entity ID, operation type).</param>
    public static void LogOperationFailure(
        this ILogger logger,
        Exception ex,
        string operation,
        string? context = null)
    {
        if (context != null)
        {
            logger.LogError(ex, "{Operation} failed for {Context}", operation, context);
        }
        else
        {
            logger.LogError(ex, "{Operation} failed", operation);
        }
    }

    /// <summary>
    /// Logs a validation failure warning with field and reason.
    /// Use for parameter validation, data validation, or business rule violations.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="field">The field or parameter that failed validation.</param>
    /// <param name="reason">The reason for validation failure.</param>
    /// <param name="value">Optional value that failed validation.</param>
    public static void LogValidationFailure(
        this ILogger logger,
        string field,
        string reason,
        object? value = null)
    {
        if (value != null)
        {
            logger.LogWarning("Validation failed for {Field} with value '{Value}': {Reason}",
                field, value, reason);
        }
        else
        {
            logger.LogWarning("Validation failed for {Field}: {Reason}",
                field, reason);
        }
    }

    /// <summary>
    /// Logs a resource not found warning.
    /// Use when a requested resource (collection, item, dataset, etc.) cannot be found.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="resourceType">The type of resource (e.g., "Collection", "Item", "Dataset").</param>
    /// <param name="resourceId">The ID of the resource that was not found.</param>
    public static void LogResourceNotFound(
        this ILogger logger,
        string resourceType,
        string resourceId)
    {
        logger.LogWarning("{ResourceType} not found: {ResourceId}", resourceType, resourceId);
    }

    /// <summary>
    /// Logs an operation timeout warning.
    /// Use when an operation exceeds its allowed time limit.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="operation">The name of the operation that timed out.</param>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    public static void LogOperationTimeout(
        this ILogger logger,
        string operation,
        int timeoutSeconds)
    {
        logger.LogWarning("{Operation} timed out after {Timeout} seconds",
            operation, timeoutSeconds);
    }

    /// <summary>
    /// Logs a configuration or feature disabled warning.
    /// Use when a feature is accessed but not enabled in configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="feature">The feature or service name.</param>
    /// <param name="reason">Optional reason or additional context.</param>
    public static void LogFeatureDisabled(
        this ILogger logger,
        string feature,
        string? reason = null)
    {
        if (reason != null)
        {
            logger.LogWarning("{Feature} is not enabled: {Reason}", feature, reason);
        }
        else
        {
            logger.LogWarning("{Feature} is not enabled", feature);
        }
    }

    /// <summary>
    /// Logs an external service failure with exception details.
    /// Use for failures when communicating with external services (S3, GCS, databases, APIs).
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="service">The external service name (e.g., "S3", "GCS", "PostgreSQL").</param>
    /// <param name="operation">The operation being performed (e.g., "read", "write", "delete").</param>
    /// <param name="resource">Optional resource identifier.</param>
    public static void LogExternalServiceFailure(
        this ILogger logger,
        Exception ex,
        string service,
        string operation,
        string? resource = null)
    {
        if (resource != null)
        {
            logger.LogError(ex, "{Service} {Operation} failed for {Resource}",
                service, operation, resource);
        }
        else
        {
            logger.LogError(ex, "{Service} {Operation} failed", service, operation);
        }
    }

    /// <summary>
    /// Logs a request rejection warning.
    /// Use when a request is rejected due to policy, security, or configuration reasons.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="requestType">The type of request (e.g., "STAC search", "Collection creation").</param>
    /// <param name="reason">The reason for rejection.</param>
    public static void LogRequestRejected(
        this ILogger logger,
        string requestType,
        string reason)
    {
        logger.LogWarning("{RequestType} rejected: {Reason}", requestType, reason);
    }
}
