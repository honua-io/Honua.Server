// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Security.Claims;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Provides structured security event logging for GeoservicesREST operations.
/// Logs security-relevant events without exposing sensitive information.
/// </summary>
internal static class GeoservicesRESTSecurityLogger
{
    /// <summary>
    /// Logs a failed authorization attempt.
    /// </summary>
    public static void LogAuthorizationFailure(
        ILogger logger,
        HttpContext httpContext,
        string operation,
        string resource)
    {
        var userId = httpContext.User?.Identity?.Name ?? "anonymous";
        var endpoint = httpContext.Request.Path.ToString();
        var method = httpContext.Request.Method;
        var ipAddress = GetClientIpAddress(httpContext);

        logger.LogWarning(
            "Authorization failed: UserId={UserId}, Operation={Operation}, Resource={Resource}, Endpoint={Endpoint}, Method={Method}, IpAddress={IpAddress}",
            userId, operation, resource, endpoint, method, ipAddress);
    }

    /// <summary>
    /// Logs an unusually large request that exceeds normal parameters.
    /// </summary>
    public static void LogLargeRequest(
        ILogger logger,
        HttpContext httpContext,
        string requestType,
        int itemCount,
        int maxAllowed)
    {
        var userId = httpContext.User?.Identity?.Name ?? "anonymous";
        var endpoint = httpContext.Request.Path.ToString();
        var ipAddress = GetClientIpAddress(httpContext);

        logger.LogWarning(
            "Large request detected: UserId={UserId}, RequestType={RequestType}, ItemCount={ItemCount}, MaxAllowed={MaxAllowed}, Endpoint={Endpoint}, IpAddress={IpAddress}",
            userId, requestType, itemCount, maxAllowed, endpoint, ipAddress);
    }

    /// <summary>
    /// Logs a validation failure that may indicate malicious input.
    /// </summary>
    public static void LogValidationFailure(
        ILogger logger,
        HttpContext httpContext,
        string validationType,
        string details)
    {
        var userId = httpContext.User?.Identity?.Name ?? "anonymous";
        var endpoint = httpContext.Request.Path.ToString();
        var ipAddress = GetClientIpAddress(httpContext);

        logger.LogWarning(
            "Validation failure: UserId={UserId}, ValidationType={ValidationType}, Details={Details}, Endpoint={Endpoint}, IpAddress={IpAddress}",
            userId, validationType, details, endpoint, ipAddress);
    }

    /// <summary>
    /// Logs a repeated failure from the same user/IP combination.
    /// This helps detect potential brute force or abuse attempts.
    /// </summary>
    public static void LogRepeatedFailure(
        ILogger logger,
        HttpContext httpContext,
        string failureType,
        int failureCount,
        TimeSpan timeWindow)
    {
        var userId = httpContext.User?.Identity?.Name ?? "anonymous";
        var endpoint = httpContext.Request.Path.ToString();
        var ipAddress = GetClientIpAddress(httpContext);

        logger.LogWarning(
            "Repeated failures detected: UserId={UserId}, FailureType={FailureType}, FailureCount={FailureCount}, TimeWindow={TimeWindow}, Endpoint={Endpoint}, IpAddress={IpAddress}",
            userId, failureType, failureCount, timeWindow, endpoint, ipAddress);
    }

    /// <summary>
    /// Logs a suspicious pattern that may indicate an attack or abuse.
    /// </summary>
    public static void LogSuspiciousActivity(
        ILogger logger,
        HttpContext httpContext,
        string activityType,
        string description)
    {
        var userId = httpContext.User?.Identity?.Name ?? "anonymous";
        var endpoint = httpContext.Request.Path.ToString();
        var ipAddress = GetClientIpAddress(httpContext);

        logger.LogWarning(
            "Suspicious activity detected: UserId={UserId}, ActivityType={ActivityType}, Description={Description}, Endpoint={Endpoint}, IpAddress={IpAddress}",
            userId, activityType, description, endpoint, ipAddress);
    }

    /// <summary>
    /// Gets the client IP address from the HTTP context, checking proxy headers.
    /// </summary>
    private static string GetClientIpAddress(HttpContext httpContext)
    {
        return httpContext.GetClientIpAddress();
    }
}
