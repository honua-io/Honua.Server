// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Text.RegularExpressions;
using Honua.Server.AlertReceiver.Configuration;
using Honua.Server.AlertReceiver.Security;
using Honua.Server.AlertReceiver.Services;
using Microsoft.Extensions.Options;

namespace Honua.Server.AlertReceiver.Middleware;

/// <summary>
/// Middleware that validates webhook signatures for incoming requests.
/// Applies to webhook endpoints to ensure authenticity.
/// </summary>
public sealed class WebhookSignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<WebhookSignatureMiddleware> _logger;

    /// <summary>
    /// Compiled regex patterns for detecting sensitive headers.
    /// SECURITY: These patterns identify headers that should NEVER be logged as they may contain:
    /// - Authentication credentials (tokens, API keys)
    /// - Webhook signatures (used for validation)
    /// - Session identifiers
    /// - Secrets or passwords
    /// </summary>
    private static readonly Regex[] SensitiveHeaderPatterns = new[]
    {
        new Regex(@"-Key$", RegexOptions.IgnoreCase | RegexOptions.Compiled),           // *-Key (X-Api-Key, Auth-Key)
        new Regex(@"-Token$", RegexOptions.IgnoreCase | RegexOptions.Compiled),         // *-Token (X-Auth-Token, Bearer-Token)
        new Regex(@"-Secret$", RegexOptions.IgnoreCase | RegexOptions.Compiled),        // *-Secret (X-Webhook-Secret)
        new Regex(@"-Signature$", RegexOptions.IgnoreCase | RegexOptions.Compiled),     // *-Signature (X-Hub-Signature-256)
        new Regex(@"^Authorization$", RegexOptions.IgnoreCase | RegexOptions.Compiled), // Authorization header
        new Regex(@"^Cookie$", RegexOptions.IgnoreCase | RegexOptions.Compiled),        // Cookie header
        new Regex(@"^Session", RegexOptions.IgnoreCase | RegexOptions.Compiled),        // Session* headers
        new Regex(@"^X-Session", RegexOptions.IgnoreCase | RegexOptions.Compiled),      // X-Session* headers
        new Regex(@"^Proxy-Authorization$", RegexOptions.IgnoreCase | RegexOptions.Compiled), // Proxy-Authorization
        new Regex(@"-Password$", RegexOptions.IgnoreCase | RegexOptions.Compiled),      // *-Password
        new Regex(@"^WWW-Authenticate$", RegexOptions.IgnoreCase | RegexOptions.Compiled) // WWW-Authenticate
    };

    public WebhookSignatureMiddleware(
        RequestDelegate next,
        ILogger<WebhookSignatureMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(
        HttpContext context,
        IWebhookSignatureValidator validator,
        IOptions<WebhookSecurityOptions> options,
        IWebhookSecurityMetrics? metrics = null)
    {
        var securityOptions = options.Value;

        // Skip validation if not required
        if (!securityOptions.RequireSignature)
        {
            _logger.LogDebug("Webhook signature validation disabled - proceeding without validation");
            await _next(context);
            return;
        }

        // SECURITY FIX: Validate ALL HTTP methods when signature validation is required
        // CRITICAL: This prevents HTTP method tampering bypass attacks where attackers
        // could use GET, HEAD, or other methods to bypass signature validation.
        //
        // Why validate all methods:
        // 1. Prevent method tampering attacks (attacker switches POST to GET)
        // 2. Prevent misconfigured endpoints that accept unintended methods
        // 3. Defense in depth - fail closed rather than open
        // 4. GET requests can be triggered by browsers (prefetch, link previews, crawlers)
        // 5. Even "safe" HTTP methods can trigger side effects if endpoints are misconfigured
        //
        // The AllowedHttpMethods configuration provides a strict allowlist that is checked
        // AFTER signature validation passes.

        // Check if method is in the allowed list (fail closed by default)
        var allowedMethods = securityOptions.AllowedHttpMethods ?? new List<string> { "POST" };
        var methodIsAllowed = allowedMethods.Any(m =>
            string.Equals(m, context.Request.Method, StringComparison.OrdinalIgnoreCase));

        if (securityOptions.RejectUnknownMethods && !methodIsAllowed)
        {
            _logger.LogWarning(
                "Webhook rejected: HTTP method {Method} not allowed from {RemoteIp}. Allowed methods: {AllowedMethods}",
                context.Request.Method,
                context.Connection.RemoteIpAddress,
                string.Join(", ", allowedMethods));

            // Record rejected method for security monitoring
            RecordRejectedMethod(context, allowedMethods);
            metrics?.RecordMethodRejection(context.Request.Method, "not_in_allowlist");

            await WriteErrorResponse(
                context,
                StatusCodes.Status405MethodNotAllowed,
                $"HTTP method {context.Request.Method} not allowed for webhook endpoints");
            return;
        }

        // Check HTTPS requirement
        if (!securityOptions.AllowInsecureHttp && !context.Request.IsHttps)
        {
            _logger.LogWarning(
                "Webhook rejected: HTTPS required but request is HTTP from {RemoteIp}",
                context.Connection.RemoteIpAddress);

            metrics?.RecordHttpsViolation();

            await WriteErrorResponse(
                context,
                StatusCodes.Status403Forbidden,
                "HTTPS is required for webhook endpoints");
            return;
        }

        // Validate timestamp (replay attack protection)
        if (securityOptions.MaxWebhookAge > 0)
        {
            if (!ValidateTimestamp(context.Request, securityOptions, out var timestampError))
            {
                _logger.LogWarning(
                    "Webhook rejected: {Error} from {RemoteIp}",
                    timestampError,
                    context.Connection.RemoteIpAddress);

                metrics?.RecordTimestampValidationFailure(timestampError);

                await WriteErrorResponse(
                    context,
                    StatusCodes.Status401Unauthorized,
                    timestampError);
                return;
            }
        }

        // Get all secrets for validation
        var secrets = securityOptions.GetAllSecrets().ToList();

        if (secrets.Count == 0)
        {
            _logger.LogError("Webhook signature validation required but no secrets configured");
            await WriteErrorResponse(
                context,
                StatusCodes.Status500InternalServerError,
                "Server configuration error");
            return;
        }

        // Record number of active secrets (for monitoring secret rotation)
        metrics?.RecordSecretRotation(secrets.Count);

        // Try validating with each secret (supports secret rotation)
        var isValid = false;
        foreach (var secret in secrets)
        {
            try
            {
                isValid = await validator.ValidateSignatureAsync(
                    context.Request,
                    secret,
                    context.RequestAborted);

                if (isValid)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during webhook signature validation");
            }
        }

        if (!isValid)
        {
            // Record failed validation for security monitoring
            RecordFailedValidation(context);
            metrics?.RecordValidationAttempt(context.Request.Method, success: false);

            await WriteErrorResponse(
                context,
                StatusCodes.Status401Unauthorized,
                "Invalid webhook signature");
            return;
        }

        // Signature valid - proceed with request
        _logger.LogDebug(
            "Webhook signature validated successfully from {RemoteIp}, Method: {Method}",
            context.Connection.RemoteIpAddress,
            context.Request.Method);

        metrics?.RecordValidationAttempt(context.Request.Method, success: true);

        await _next(context);
    }

    /// <summary>
    /// Validates the webhook timestamp to prevent replay attacks.
    /// </summary>
    private bool ValidateTimestamp(
        HttpRequest request,
        WebhookSecurityOptions options,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!request.Headers.TryGetValue(options.TimestampHeaderName, out var timestampHeader))
        {
            errorMessage = $"Missing timestamp header: {options.TimestampHeaderName}";
            return false;
        }

        var timestampStr = timestampHeader.ToString();

        // Try parsing as Unix timestamp (seconds)
        if (long.TryParse(timestampStr, out var unixTimestamp))
        {
            var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
            var age = DateTimeOffset.UtcNow - requestTime;

            if (age.TotalSeconds > options.MaxWebhookAge)
            {
                errorMessage = $"Webhook timestamp too old: {age.TotalSeconds:F0}s (max: {options.MaxWebhookAge}s)";
                return false;
            }

            if (age.TotalSeconds < -60) // Allow 60 seconds clock skew
            {
                errorMessage = "Webhook timestamp is in the future";
                return false;
            }

            return true;
        }

        // Try parsing as ISO 8601 timestamp
        if (DateTimeOffset.TryParse(timestampStr, out var parsedTime))
        {
            var age = DateTimeOffset.UtcNow - parsedTime;

            if (age.TotalSeconds > options.MaxWebhookAge)
            {
                errorMessage = $"Webhook timestamp too old: {age.TotalSeconds:F0}s (max: {options.MaxWebhookAge}s)";
                return false;
            }

            if (age.TotalSeconds < -60)
            {
                errorMessage = "Webhook timestamp is in the future";
                return false;
            }

            return true;
        }

        errorMessage = $"Invalid timestamp format: {timestampStr}";
        return false;
    }

    /// <summary>
    /// Records failed validation attempts for security monitoring.
    /// SECURITY: Uses structured logging with individual fields to prevent sensitive data exposure.
    /// Only logs headers from the allowlist to prevent leaking credentials, tokens, or PII.
    /// </summary>
    private void RecordFailedValidation(HttpContext context)
    {
        // Get security options from DI container
        var securityOptions = context.RequestServices
            .GetService<IOptions<WebhookSecurityOptions>>()?.Value;

        // Redact sensitive headers using allowlist
        var safeHeaders = RedactSensitiveHeaders(
            context.Request.Headers,
            securityOptions?.AllowedLogHeaders);

        // SECURITY: Use structured logging with individual fields instead of serializing objects
        // This prevents accidental logging of sensitive data that might be added to the object later
        _logger.LogWarning(
            "SECURITY: Failed webhook validation - EventType: {EventType}, Timestamp: {Timestamp}, " +
            "RemoteIp: {RemoteIp}, Path: {Path}, Method: {Method}, UserAgent: {UserAgent}, " +
            "ContentType: {ContentType}, ContentLength: {ContentLength}, SafeHeaders: {SafeHeaders}",
            "WebhookValidationFailure",
            DateTimeOffset.UtcNow,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            context.Request.Path.ToString(),
            context.Request.Method,
            context.Request.Headers.UserAgent.ToString(),
            context.Request.ContentType ?? "none",
            context.Request.ContentLength?.ToString() ?? "unknown",
            JsonSerializer.Serialize(safeHeaders));
    }

    /// <summary>
    /// Records rejected HTTP method attempts for security monitoring.
    /// SECURITY: Uses structured logging with individual fields to prevent sensitive data exposure.
    /// </summary>
    private void RecordRejectedMethod(HttpContext context, List<string> allowedMethods)
    {
        // Check if this looks like a browser request (potential GET attack)
        var isPotentialBrowserRequest = !string.IsNullOrEmpty(context.Request.Headers.Accept) &&
                                       context.Request.Headers.Accept.ToString().Contains("text/html");

        // SECURITY: Use structured logging with individual fields
        _logger.LogWarning(
            "SECURITY: Rejected HTTP method for webhook - EventType: {EventType}, Timestamp: {Timestamp}, " +
            "RemoteIp: {RemoteIp}, Path: {Path}, Method: {Method}, AllowedMethods: {AllowedMethods}, " +
            "UserAgent: {UserAgent}, Referer: {Referer}, IsPotentialBrowserRequest: {IsPotentialBrowserRequest}",
            "WebhookMethodRejected",
            DateTimeOffset.UtcNow,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            context.Request.Path.ToString(),
            context.Request.Method,
            string.Join(", ", allowedMethods),
            context.Request.Headers.UserAgent.ToString(),
            context.Request.Headers.Referer.ToString(),
            isPotentialBrowserRequest);
    }

    /// <summary>
    /// Redacts sensitive headers from logging using a strict allowlist approach.
    /// SECURITY: This is a critical security control that prevents exposure of:
    /// - Authentication credentials (API keys, bearer tokens, basic auth)
    /// - Webhook signatures (used for validation)
    /// - Session identifiers (cookies, session tokens)
    /// - Other secrets or PII
    /// </summary>
    /// <param name="headers">The full header collection from the request</param>
    /// <param name="allowedHeaders">Optional list of headers that are safe to log (default: User-Agent, Content-Type, Content-Length)</param>
    /// <returns>Dictionary containing only non-sensitive headers</returns>
    /// <remarks>
    /// Uses a defense-in-depth approach:
    /// 1. Allowlist: Only explicitly safe headers are included
    /// 2. Pattern matching: Headers matching sensitive patterns are always excluded
    /// 3. Fail closed: If allowlist is empty, uses minimal safe defaults
    ///
    /// This prevents common security issues:
    /// - Accidental logging of Authorization headers
    /// - Exposure of X-Api-Key, X-Auth-Token, etc.
    /// - Leaking webhook signatures (X-Hub-Signature-256, X-Webhook-Signature)
    /// - Cookie/session data exposure
    /// </remarks>
    private static Dictionary<string, string> RedactSensitiveHeaders(
        IHeaderDictionary headers,
        List<string>? allowedHeaders = null)
    {
        // Default to minimal safe headers if none specified
        var safeHeaderNames = allowedHeaders?.Count > 0
            ? new HashSet<string>(allowedHeaders, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "User-Agent",
                "Content-Type",
                "Content-Length"
            };

        var safeHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            // Skip if not in allowlist
            if (!safeHeaderNames.Contains(header.Key))
            {
                continue;
            }

            // Double-check: Skip if matches any sensitive pattern (defense in depth)
            var isSensitive = false;
            foreach (var pattern in SensitiveHeaderPatterns)
            {
                if (pattern.IsMatch(header.Key))
                {
                    isSensitive = true;
                    break;
                }
            }

            if (!isSensitive)
            {
                safeHeaders[header.Key] = header.Value.ToString();
            }
        }

        return safeHeaders;
    }

    /// <summary>
    /// Writes a standardized error response.
    /// </summary>
    private static async Task WriteErrorResponse(
        HttpContext context,
        int statusCode,
        string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = message,
            statusCode,
            timestamp = DateTimeOffset.UtcNow
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Extension methods for registering webhook signature middleware.
/// </summary>
public static class WebhookSignatureMiddlewareExtensions
{
    /// <summary>
    /// Adds webhook signature validation middleware to the pipeline.
    /// Should be added before authorization and controller mapping.
    /// </summary>
    public static IApplicationBuilder UseWebhookSignatureValidation(
        this IApplicationBuilder app,
        PathString pathPrefix = default)
    {
        // If a path prefix is specified, only apply to those paths
        if (pathPrefix.HasValue)
        {
            return app.MapWhen(
                context => context.Request.Path.StartsWithSegments(pathPrefix),
                branch => branch.UseMiddleware<WebhookSignatureMiddleware>());
        }

        return app.UseMiddleware<WebhookSignatureMiddleware>();
    }
}
