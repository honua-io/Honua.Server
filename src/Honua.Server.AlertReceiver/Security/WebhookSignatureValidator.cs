// <copyright file="WebhookSignatureValidator.cs" company="HonuaIO">
// Copyright (c) 2025 HonuaIO.
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Honua.Server.AlertReceiver.Configuration;
using Microsoft.Extensions.Options;

namespace Honua.Server.AlertReceiver.Security;

/// <summary>
/// Interface for webhook signature validation.
/// </summary>
public interface IWebhookSignatureValidator
{
    /// <summary>
    /// Validates the signature of an incoming webhook request.
    /// </summary>
    /// <param name="request">The HTTP request to validate.</param>
    /// <param name="secret">The shared secret used to sign the webhook.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
    Task<bool> ValidateSignatureAsync(HttpRequest request, string secret, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a signature for a given payload.
    /// </summary>
    /// <param name="payload">The payload bytes to sign.</param>
    /// <param name="secret">The shared secret to use for signing.</param>
    /// <returns>The signature in the format "sha256=&lt;hex_signature&gt;".</returns>
    string GenerateSignature(byte[] payload, string secret);
}

/// <summary>
/// Validates webhook signatures using HMAC-SHA256.
/// Implements constant-time comparison to prevent timing attacks.
/// </summary>
public sealed class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly WebhookSecurityOptions options;
    private readonly ILogger<WebhookSignatureValidator> logger;

    public WebhookSignatureValidator(
        IOptions<WebhookSecurityOptions> options,
        ILogger<WebhookSignatureValidator> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates the signature of an incoming webhook request.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public async Task<bool> ValidateSignatureAsync(
        HttpRequest request,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            this.logger.LogWarning("Webhook signature validation failed: secret is null or empty");
            return false;
        }

        // Extract signature from header
        if (!request.Headers.TryGetValue(this.options.SignatureHeaderName, out var signatureHeader))
        {
            this.logger.LogWarning(
                "Webhook signature validation failed: {HeaderName} header not found",
                this.options.SignatureHeaderName);
            return false;
        }

        var providedSignature = signatureHeader.ToString();
        if (string.IsNullOrWhiteSpace(providedSignature))
        {
            this.logger.LogWarning("Webhook signature validation failed: signature header is empty");
            return false;
        }

        // Read request body
        request.EnableBuffering();
        byte[] payload;

        try
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, cancellationToken);
            payload = ms.ToArray();

            // Reset stream position for subsequent reads
            request.Body.Position = 0;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to read request body for signature validation");
            return false;
        }

        // Check payload size
        if (payload.Length > this.options.MaxPayloadSize)
        {
            this.logger.LogWarning(
                "Webhook signature validation failed: payload size {Size} exceeds maximum {MaxSize}",
                payload.Length,
                this.options.MaxPayloadSize);
            return false;
        }

        // Generate expected signature
        var expectedSignature = this.GenerateSignature(payload, secret);

        // Perform constant-time comparison to prevent timing attacks
        var isValid = CompareSignaturesConstantTime(expectedSignature, providedSignature);

        if (!isValid)
        {
            this.logger.LogWarning(
                "Webhook signature validation failed: signature mismatch from {RemoteIp}",
                request.HttpContext.Connection.RemoteIpAddress);
        }

        return isValid;
    }

    /// <summary>
    /// Generates a signature for a given payload using HMAC-SHA256.
    /// </summary>
    /// <param name="payload">The payload bytes to sign.</param>
    /// <param name="secret">The shared secret to use for signing.</param>
    /// <returns>The signature in the format "sha256=&lt;hex_signature&gt;".</returns>
    public string GenerateSignature(byte[] payload, string secret)
    {
        if (payload == null)
        {
            throw new ArgumentNullException(nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret cannot be null or empty", nameof(secret));
        }

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(payload);
        var hexHash = Convert.ToHexString(hash).ToLowerInvariant();

        return $"sha256={hexHash}";
    }

    /// <summary>
    /// Compares two signatures in constant time to prevent timing attacks.
    /// Supports multiple signature formats (with/without algorithm prefix).
    /// </summary>
    /// <remarks>
    /// SECURITY: Regular string comparison (==, Equals) can leak information about the signature
    /// through timing side-channels. An attacker could measure response times to determine
    /// which characters in their guess are correct, gradually revealing the full signature.
    ///
    /// CryptographicOperations.FixedTimeEquals compares signatures in constant time regardless
    /// of where differences occur, preventing this attack vector.
    /// </remarks>
    private static bool CompareSignaturesConstantTime(string expected, string provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        // Normalize signatures - extract the hash part if prefixed with algorithm
        // Also normalize to lowercase for case-insensitive comparison
        var expectedHash = ExtractHash(expected).ToLowerInvariant();
        var providedHash = ExtractHash(provided).ToLowerInvariant();

        // Convert to byte arrays for constant-time comparison
        byte[] expectedBytes;
        byte[] providedBytes;

        try
        {
            expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
            providedBytes = Encoding.UTF8.GetBytes(providedHash);
        }
        catch
        {
            return false;
        }

        // FixedTimeEquals requires equal length arrays
        if (expectedBytes.Length != providedBytes.Length)
        {
            // Still do a fixed-time comparison to avoid leaking length information
            // by comparing against a dummy value of the same length as provided
            var dummy = new byte[providedBytes.Length];
            CryptographicOperations.FixedTimeEquals(dummy, providedBytes);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    /// <summary>
    /// Extracts the hash portion from a signature, handling various formats.
    /// Supports: "sha256=abc123", "sha256:abc123", or just "abc123"
    /// </summary>
    private static string ExtractHash(string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return string.Empty;
        }

        // Check for common prefixes
        var prefixes = new[] { "sha256=", "sha256:", "sha-256=", "sha-256:" };

        foreach (var prefix in prefixes)
        {
            if (signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return signature.Substring(prefix.Length);
            }
        }

        // No prefix found, return as-is
        return signature;
    }
}
