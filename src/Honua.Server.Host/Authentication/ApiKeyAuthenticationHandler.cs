// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Authentication;

/// <summary>
/// Authentication handler for API key-based authentication.
/// Supports header-based (X-API-Key) authentication only for security.
/// </summary>
/// <remarks>
/// SECURITY: Query parameter API keys are NOT supported as they are insecure:
/// - They appear in server logs, proxy logs, and browser history
/// - They can be cached by CDNs and intermediaries
/// - They are visible in URLs that may be shared or bookmarked
/// - They may be leaked via Referer headers to third-party sites
///
/// Always use the X-API-Key header for API authentication.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-API-Key";

    private readonly IOptionsMonitor<HonuaAuthenticationOptions> authOptions;
    private readonly ISecurityAuditLogger auditLogger;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        ISecurityAuditLogger auditLogger)
        : base(options, logger, encoder)
    {
        this.authOptions = Guard.NotNull(authOptions);
        this.auditLogger = Guard.NotNull(auditLogger);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // API keys only work in QuickStart mode or when explicitly enabled
        var currentAuthOptions = this.authOptions.CurrentValue;
        if (currentAuthOptions.Mode != HonuaAuthenticationOptions.AuthenticationMode.QuickStart &&
            !Options.AllowInProductionMode)
        {
            Logger.LogDebug("API key authentication skipped: current mode is {Mode}", currentAuthOptions.Mode);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // SECURITY: Extract API key from header only (query parameters are insecure)
        // API keys in URLs can be logged in web server logs, proxy logs, and browser history
        var apiKey = this.Request.Headers[ApiKeyHeaderName].FirstOrDefault();

        if (apiKey.IsNullOrWhiteSpace())
        {
            Logger.LogDebug("No API key provided in {HeaderName} header", ApiKeyHeaderName);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Logger.LogDebug("API key validation attempt from {RemoteIp}", Context.Connection.RemoteIpAddress);

        // SECURITY: Validate API key using constant-time comparison to prevent timing attacks
        // Timing attacks could allow attackers to determine valid API key prefixes by measuring response times
        ApiKeyDefinition? validApiKey = null;

        foreach (var configuredKey in Options.ApiKeys)
        {
            bool isMatch;

            // Support both hashed and legacy plain-text keys
            if (!string.IsNullOrEmpty(configuredKey.KeyHash) && !string.IsNullOrEmpty(configuredKey.KeySalt))
            {
                // New hashed key validation using PBKDF2
                var providedKeyHash = HashApiKey(apiKey, configuredKey.KeySalt);
                isMatch = string.Equals(providedKeyHash, configuredKey.KeyHash, StringComparison.Ordinal);

                Logger.LogDebug("Validating hashed API key '{KeyName}'", configuredKey.Name);
            }
            else if (!string.IsNullOrEmpty(configuredKey.Key))
            {
                // Legacy plain-text key validation (deprecated)
                isMatch = IsApiKeyMatch(configuredKey.Key, apiKey);

                Logger.LogWarning(
                    "API key '{KeyName}' is using deprecated plain-text storage. " +
                    "Consider migrating to hashed keys for better security.",
                    configuredKey.Name);
            }
            else
            {
                Logger.LogWarning(
                    "API key '{KeyName}' has invalid configuration (missing Key or KeyHash/KeySalt)",
                    configuredKey.Name);
                continue; // Invalid key configuration
            }

            if (isMatch)
            {
                validApiKey = configuredKey;
                break;
            }
        }

        if (validApiKey == null)
        {
            // SECURITY: Hash API key for logging instead of logging partial key
            var keyHash = ComputeKeyHash(apiKey);
            this.auditLogger.LogApiKeyValidationFailure(keyHash, Context.Connection.RemoteIpAddress?.ToString());

            Logger.LogWarning(
                "Invalid API key attempted from {RemoteIp}, KeyHash={KeyHash}",
                Context.Connection.RemoteIpAddress,
                keyHash);

            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));
        }

        // Check if key is expired
        if (validApiKey.ExpiresAt.HasValue && validApiKey.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            Logger.LogWarning(
                "Expired API key '{KeyName}' (expired {ExpiryDate}) attempted from {RemoteIp}",
                validApiKey.Name,
                validApiKey.ExpiresAt.Value,
                Context.Connection.RemoteIpAddress);

            return Task.FromResult(AuthenticateResult.Fail("API key expired"));
        }

        // Create claims for the API key
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, validApiKey.Name),
            new(ClaimTypes.NameIdentifier, validApiKey.Name),
            new("api_key_name", validApiKey.Name)
        };

        // Add roles
        foreach (var role in validApiKey.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        this.auditLogger.LogApiKeyAuthentication(
            validApiKey.Name,
            Context.Connection.RemoteIpAddress?.ToString());

        Logger.LogInformation(
            "API key authentication successful for '{KeyName}' from {RemoteIp} with roles: {Roles}",
            validApiKey.Name,
            Context.Connection.RemoteIpAddress,
            string.Join(",", validApiKey.Roles));

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        this.Response.StatusCode = 401;
        this.Response.Headers.Append("WWW-Authenticate", $"{Scheme.Name} realm=\"Honua API\"");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Computes a SHA256 hash of the API key for secure logging.
    /// Returns the first 16 characters of the hex-encoded hash.
    /// </summary>
    private static string ComputeKeyHash(string apiKey)
    {
        if (apiKey.IsNullOrEmpty())
            return "null";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(apiKey));
        var hashHex = Convert.ToHexString(hashBytes);
        return hashHex[..Math.Min(16, hashHex.Length)];
    }

    /// <summary>
    /// Performs constant-time comparison of two API keys to prevent timing attacks.
    /// Uses CryptographicOperations.FixedTimeEquals which is resistant to timing analysis.
    /// </summary>
    /// <param name="configuredKey">The API key from configuration.</param>
    /// <param name="providedKey">The API key provided in the request.</param>
    /// <returns>True if keys match, false otherwise.</returns>
    /// <remarks>
    /// SECURITY: Regular string comparison (==, Equals) can leak information about the key
    /// through timing side-channels. An attacker could measure response times to determine
    /// which characters in their guess are correct, gradually revealing the full key.
    ///
    /// FixedTimeEquals compares keys in constant time regardless of where differences occur,
    /// preventing this attack vector.
    /// </remarks>
    private static bool IsApiKeyMatch(string configuredKey, string providedKey)
    {
        if (configuredKey.IsNullOrEmpty() || providedKey.IsNullOrEmpty())
            return false;

        // Convert both keys to byte arrays for constant-time comparison
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        // FixedTimeEquals throws if the spans differ, so guard on length first.
        if (configuredBytes.Length != providedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
    }

    /// <summary>
    /// Hashes an API key with a salt using PBKDF2.
    /// </summary>
    /// <param name="apiKey">The API key to hash.</param>
    /// <param name="salt">The base64-encoded salt to use for hashing.</param>
    /// <returns>Base64-encoded hash of the API key.</returns>
    /// <remarks>
    /// SECURITY: Uses PBKDF2 (Password-Based Key Derivation Function 2) with:
    /// - SHA256 as the hash algorithm
    /// - 100,000 iterations (OWASP recommended minimum as of 2023)
    /// - 32-byte (256-bit) output hash size
    ///
    /// This makes brute-force attacks computationally expensive even if the configuration
    /// file is compromised.
    /// </remarks>
    private static string HashApiKey(string apiKey, string salt)
    {
        const int iterations = 100000;
        const int hashSize = 32;

        var saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            apiKey,
            saltBytes,
            iterations,
            HashAlgorithmName.SHA256);

        var hash = pbkdf2.GetBytes(hashSize);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Generates a random salt for API key hashing.
    /// </summary>
    /// <returns>Base64-encoded random salt (32 bytes).</returns>
    /// <remarks>
    /// SECURITY: Uses cryptographically secure random number generation.
    /// Each API key should have its own unique salt.
    /// </remarks>
    public static string GenerateSalt()
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Generates a hash for a given API key and salt.
    /// Useful for generating configuration values.
    /// </summary>
    /// <param name="apiKey">The API key to hash.</param>
    /// <returns>A tuple containing the base64-encoded hash and salt.</returns>
    /// <example>
    /// <code>
    /// var (hash, salt) = ApiKeyAuthenticationHandler.GenerateApiKeyHash("my-secret-key");
    /// Console.WriteLine($"KeyHash: {hash}");
    /// Console.WriteLine($"KeySalt: {salt}");
    /// </code>
    /// </example>
    public static (string Hash, string Salt) GenerateApiKeyHash(string apiKey)
    {
        var salt = GenerateSalt();
        var hash = HashApiKey(apiKey, salt);
        return (hash, salt);
    }
}

/// <summary>
/// Options for API key authentication.
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";

    /// <summary>
    /// List of valid API keys.
    /// </summary>
    public List<ApiKeyDefinition> ApiKeys { get; set; } = new();

    /// <summary>
    /// Allow API key authentication even when not in QuickStart mode.
    /// WARNING: Should only be enabled for specific service-to-service scenarios.
    /// </summary>
    public bool AllowInProductionMode { get; set; } = false;
}

/// <summary>
/// Defines an API key with associated metadata.
/// Supports both legacy plain-text keys and secure hashed keys.
/// </summary>
/// <remarks>
/// SECURITY: For new API keys, use KeyHash and KeySalt instead of Key for better security.
///
/// Example configuration with hashed keys:
/// <code>
/// "ApiKeys": [
///   {
///     "Name": "service-account",
///     "KeyHash": "base64-encoded-hash",
///     "KeySalt": "base64-encoded-salt",
///     "Roles": ["User", "Admin"],
///     "Description": "Service account for automated tasks"
///   }
/// ]
/// </code>
///
/// To generate a hash for a new key, use the GenerateApiKeyHash static method in ApiKeyAuthenticationHandler.
/// </remarks>
public sealed class ApiKeyDefinition
{
    /// <summary>
    /// Human-readable name for this API key.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The actual API key value (legacy - deprecated).
    /// For new keys, use KeyHash and KeySalt instead.
    /// </summary>
    [Obsolete("Use KeyHash and KeySalt instead for better security")]
    public string? Key { get; init; }

    /// <summary>
    /// SHA256 hash of the API key (recommended).
    /// Use this in combination with KeySalt for secure storage.
    /// Generated using PBKDF2 with 100,000 iterations.
    /// </summary>
    public string? KeyHash { get; init; }

    /// <summary>
    /// Salt used for hashing the API key.
    /// Required when using KeyHash. Should be a unique random value per key.
    /// </summary>
    public string? KeySalt { get; init; }

    /// <summary>
    /// Roles assigned to this API key.
    /// </summary>
    public List<string> Roles { get; init; } = new();

    /// <summary>
    /// Optional expiration date for this key.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Description of what this key is used for.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Extension methods for API key authentication.
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public static AuthenticationBuilder AddApiKey(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationOptions.DefaultScheme,
            configureOptions);
    }
}
