// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.AlertReceiver.Configuration;

/// <summary>
/// Configuration options for webhook security and signature validation.
/// </summary>
public sealed class WebhookSecurityOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Webhook:Security";

    /// <summary>
    /// Whether signature validation is required for webhook endpoints.
    /// Default: true (recommended for production).
    /// </summary>
    /// <remarks>
    /// SECURITY: Set to false only for development/testing or during migration.
    /// When false, webhooks are not validated and can be spoofed.
    /// </remarks>
    public bool RequireSignature { get; set; } = true;

    /// <summary>
    /// The HTTP header name to check for the signature.
    /// Default: "X-Hub-Signature-256" (GitHub-style).
    /// </summary>
    /// <remarks>
    /// Common values:
    /// - "X-Hub-Signature-256" (GitHub)
    /// - "X-Signature" (generic)
    /// - "X-Webhook-Signature" (custom)
    /// </remarks>
    public string SignatureHeaderName { get; set; } = "X-Hub-Signature-256";

    /// <summary>
    /// The shared secret used to validate webhook signatures.
    /// Should be configured via environment variables or secure configuration.
    /// </summary>
    /// <remarks>
    /// SECURITY: NEVER commit this value to source control.
    /// Use environment variable: Webhook__Security__SharedSecret
    /// Minimum length: 64 characters (512 bits) recommended for HMAC-SHA256
    /// Generate a strong secret: openssl rand -base64 64
    /// </remarks>
    public string? SharedSecret { get; set; }

    /// <summary>
    /// Maximum allowed payload size in bytes.
    /// Default: 1 MB (1,048,576 bytes).
    /// </summary>
    /// <remarks>
    /// Prevents DoS attacks via large payloads.
    /// Adjust based on expected webhook payload sizes.
    /// </remarks>
    public int MaxPayloadSize { get; set; } = 1_048_576; // 1MB

    /// <summary>
    /// Allow HTTP (non-HTTPS) connections.
    /// Default: false (HTTPS required).
    /// </summary>
    /// <remarks>
    /// SECURITY: Should only be true in development environments.
    /// Production webhooks should always use HTTPS to prevent:
    /// - Man-in-the-middle attacks
    /// - Eavesdropping on webhook payloads
    /// - Secret exposure through network sniffing
    /// </remarks>
    public bool AllowInsecureHttp { get; set; } = false;

    /// <summary>
    /// List of additional shared secrets for validation.
    /// Useful for secret rotation without downtime.
    /// </summary>
    /// <remarks>
    /// SECURITY: During secret rotation:
    /// 1. Add new secret to this list
    /// 2. Update webhook senders to use new secret
    /// 3. Remove old secret after all senders are updated
    /// </remarks>
    public List<string> AdditionalSecrets { get; set; } = new();

    /// <summary>
    /// Maximum age (in seconds) for webhook requests (replay attack protection).
    /// Default: 300 seconds (5 minutes).
    /// Set to 0 to disable timestamp validation.
    /// </summary>
    /// <remarks>
    /// SECURITY: Prevents replay attacks where an attacker intercepts and resends
    /// a valid webhook. Requires the webhook to include a timestamp header.
    /// </remarks>
    public int MaxWebhookAge { get; set; } = 300;

    /// <summary>
    /// The HTTP header name containing the webhook timestamp.
    /// Default: "X-Webhook-Timestamp".
    /// </summary>
    public string TimestampHeaderName { get; set; } = "X-Webhook-Timestamp";

    /// <summary>
    /// Allowed HTTP methods for webhook requests.
    /// Default: POST only (recommended for webhooks).
    /// </summary>
    /// <remarks>
    /// SECURITY: Webhooks should typically only accept POST requests.
    /// Adding other methods (PUT, PATCH, DELETE) may be necessary for specific integrations
    /// but should be done with caution. GET requests should never be used for webhooks
    /// as they can be easily triggered via browser prefetch, link previews, etc.
    ///
    /// When RequireSignature is true, ALL methods are validated regardless of this list.
    /// This setting controls which methods are allowed after successful validation.
    ///
    /// If empty or null, defaults to POST only for security.
    /// </remarks>
    public List<string> AllowedHttpMethods { get; set; } = new() { "POST" };

    /// <summary>
    /// Whether to reject unknown HTTP methods not in the AllowedHttpMethods list.
    /// Default: true (fail closed - reject unknown methods).
    /// </summary>
    /// <remarks>
    /// SECURITY: Recommended to keep as true (fail closed).
    /// When true, only methods in AllowedHttpMethods are accepted.
    /// When false, any method is allowed if signature validation passes.
    /// </remarks>
    public bool RejectUnknownMethods { get; set; } = true;

    /// <summary>
    /// List of HTTP headers that are safe to log in security events.
    /// Default: User-Agent, Content-Type, Content-Length only.
    /// </summary>
    /// <remarks>
    /// SECURITY: Only headers that do NOT contain sensitive data should be included.
    /// Headers containing tokens, keys, secrets, signatures, or PII should NEVER be logged.
    /// Use a strict allowlist approach - only explicitly safe headers are included.
    ///
    /// Safe headers typically include:
    /// - User-Agent: Identifies the client software
    /// - Content-Type: MIME type of the payload
    /// - Content-Length: Size of the payload
    /// - Accept: Content types the client accepts
    ///
    /// DO NOT include:
    /// - Authorization headers (Bearer tokens, Basic auth)
    /// - API keys (X-Api-Key, etc.)
    /// - Webhook signatures (X-Hub-Signature-256, X-Webhook-Signature)
    /// - Session cookies
    /// - Custom authentication headers
    /// </remarks>
    public List<string> AllowedLogHeaders { get; set; } = new()
    {
        "User-Agent",
        "Content-Type",
        "Content-Length"
    };

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (RequireSignature)
        {
            if (string.IsNullOrWhiteSpace(SharedSecret) && AdditionalSecrets.Count == 0)
            {
                errors.Add($"{SectionName}:SharedSecret is required when RequireSignature is true");
            }

            // NIST SP 800-107 recommends key length >= hash output for HMAC
            // For HMAC-SHA256: 256 bits minimum, 512 bits recommended
            // 64 characters = 512 bits for future-proofing
            if (!string.IsNullOrWhiteSpace(SharedSecret) && SharedSecret.Length < 64)
            {
                errors.Add($"{SectionName}:SharedSecret must be at least 64 characters for HMAC-SHA256 security (current: {SharedSecret.Length}). Generate: openssl rand -base64 64");
            }

            foreach (var secret in AdditionalSecrets.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                if (secret.Length < 64)
                {
                    errors.Add($"{SectionName}:AdditionalSecrets contains a secret shorter than 64 characters (HMAC-SHA256 requires 512 bits). Generate: openssl rand -base64 64");
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(SignatureHeaderName))
        {
            errors.Add($"{SectionName}:SignatureHeaderName cannot be empty");
        }

        if (MaxPayloadSize <= 0)
        {
            errors.Add($"{SectionName}:MaxPayloadSize must be greater than 0");
        }

        if (MaxPayloadSize > 10_485_760) // 10 MB
        {
            errors.Add($"{SectionName}:MaxPayloadSize should not exceed 10 MB (10,485,760 bytes)");
        }

        if (MaxWebhookAge < 0)
        {
            errors.Add($"{SectionName}:MaxWebhookAge cannot be negative");
        }

        // Validate AllowedHttpMethods
        if (AllowedHttpMethods == null || AllowedHttpMethods.Count == 0)
        {
            errors.Add($"{SectionName}:AllowedHttpMethods cannot be empty (defaults to POST)");
        }
        else
        {
            var validMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"
            };

            foreach (var method in AllowedHttpMethods)
            {
                if (string.IsNullOrWhiteSpace(method))
                {
                    errors.Add($"{SectionName}:AllowedHttpMethods contains empty or null values");
                    break;
                }

                if (!validMethods.Contains(method))
                {
                    errors.Add($"{SectionName}:AllowedHttpMethods contains invalid method: {method}");
                    break;
                }
            }

            // Security warning: GET should not be allowed for webhooks
            if (AllowedHttpMethods.Any(m => string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"{SectionName}:AllowedHttpMethods should not include GET (security risk: GET requests can be triggered by browsers, crawlers, link previews)");
            }
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Gets all valid secrets (primary + additional) for validation.
    /// </summary>
    public IEnumerable<string> GetAllSecrets()
    {
        if (!string.IsNullOrWhiteSpace(SharedSecret))
        {
            yield return SharedSecret;
        }

        foreach (var secret in AdditionalSecrets.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            yield return secret;
        }
    }
}
