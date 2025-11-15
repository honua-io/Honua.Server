// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Validates application configuration on startup to ensure data integrity.
/// Fails fast with clear error messages when configuration is invalid.
/// </summary>
public static class ConfigurationValidator
{
    /// <summary>
    /// Validates critical configuration sections on startup.
    /// DATA INTEGRITY: Ensures configuration meets minimum requirements before server starts.
    /// </summary>
    /// <param name="configuration">The application configuration to validate.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    public static void ValidateOnStartup(IConfiguration configuration, ILogger? logger = null)
    {
        Guard.NotNull(configuration);

        var errors = new List<string>();

        // Validate connection strings
        ValidateConnectionStrings(configuration, errors);

        // Validate rate limiting configuration
        ValidateRateLimiting(configuration, errors);

        // Validate cache configuration
        ValidateCaching(configuration, errors);

        // Validate request limits
        ValidateRequestLimits(configuration, errors);

        // Validate metadata paths
        ValidateMetadataPaths(configuration, errors, logger);

        // Validate security settings
        ValidateSecuritySettings(configuration, errors);

        // Throw if any errors found
        if (errors.Count > 0)
        {
            var errorMessage = string.Join(Environment.NewLine, errors);
            throw new InvalidOperationException(
                $"Configuration validation failed with {errors.Count} error(s):{Environment.NewLine}{errorMessage}");
        }
    }

    private static void ValidateConnectionStrings(IConfiguration configuration, List<string> errors)
    {
        var connectionStrings = configuration.GetSection("ConnectionStrings");
        if (!connectionStrings.Exists())
        {
            // Connection strings may be in metadata, so this is just a warning
            return;
        }

        // Validate each connection string is not empty
        foreach (var child in connectionStrings.GetChildren())
        {
            var value = child.Value;
            if (value.IsNullOrWhiteSpace())
            {
                errors.Add($"Connection string '{child.Key}' is empty or whitespace.");
            }
        }
    }

    private static void ValidateRateLimiting(IConfiguration configuration, List<string> errors)
    {
        var rateLimiting = configuration.GetSection("RateLimiting");
        if (!rateLimiting.Exists())
        {
            return;
        }

        // Validate global rate limit if specified
        if (rateLimiting["GlobalLimit"] is string globalLimit)
        {
            if (!int.TryParse(globalLimit, out var limit) || limit <= 0)
            {
                errors.Add($"RateLimiting:GlobalLimit must be a positive integer, got: '{globalLimit}'.");
            }
        }

        // Validate window size
        if (rateLimiting["WindowSeconds"] is string windowSeconds)
        {
            if (!int.TryParse(windowSeconds, out var seconds) || seconds <= 0)
            {
                errors.Add($"RateLimiting:WindowSeconds must be a positive integer, got: '{windowSeconds}'.");
            }
        }

        // Validate queue limit
        if (rateLimiting["QueueLimit"] is string queueLimit)
        {
            if (!int.TryParse(queueLimit, out var limit) || limit < 0)
            {
                errors.Add($"RateLimiting:QueueLimit must be a non-negative integer, got: '{queueLimit}'.");
            }
        }
    }

    private static void ValidateCaching(IConfiguration configuration, List<string> errors)
    {
        var caching = configuration.GetSection("Caching");
        if (!caching.Exists())
        {
            return;
        }

        // Validate Redis connection if caching is enabled
        if (caching["Enabled"] is string enabled && bool.Parse(enabled))
        {
            var redisConnection = configuration.GetConnectionString("Redis");
            if (redisConnection.IsNullOrWhiteSpace())
            {
                errors.Add("Caching is enabled but Redis connection string is not configured.");
            }
        }

        // Validate cache TTL
        if (caching["DefaultTtlMinutes"] is string ttlMinutes)
        {
            if (!int.TryParse(ttlMinutes, out var minutes) || minutes <= 0)
            {
                errors.Add($"Caching:DefaultTtlMinutes must be a positive integer, got: '{ttlMinutes}'.");
            }
        }

        // Validate cache size limits
        if (caching["MaxSizeMB"] is string maxSizeMB)
        {
            if (!int.TryParse(maxSizeMB, out var sizeMB) || sizeMB <= 0)
            {
                errors.Add($"Caching:MaxSizeMB must be a positive integer, got: '{maxSizeMB}'.");
            }
        }
    }

    private static void ValidateRequestLimits(IConfiguration configuration, List<string> errors)
    {
        var requestLimits = configuration.GetSection("RequestLimits");
        if (!requestLimits.Exists())
        {
            return;
        }

        // Validate max body size
        if (requestLimits["MaxBodySize"] is string maxBodySize)
        {
            if (!long.TryParse(maxBodySize, out var size) || size <= 0)
            {
                errors.Add($"RequestLimits:MaxBodySize must be a positive integer, got: '{maxBodySize}'.");
            }
            else if (size > 10L * 1024 * 1024 * 1024) // 10 GB
            {
                // Warn about very large sizes (potential DoS risk)
                errors.Add($"RequestLimits:MaxBodySize is very large ({size} bytes). Consider reducing for security.");
            }
        }

        // Validate max query string length
        if (requestLimits["MaxQueryStringLength"] is string maxQueryStringLength)
        {
            if (!int.TryParse(maxQueryStringLength, out var length) || length <= 0)
            {
                errors.Add($"RequestLimits:MaxQueryStringLength must be a positive integer, got: '{maxQueryStringLength}'.");
            }
        }

        // Validate max concurrent connections
        if (requestLimits["MaxConcurrentConnections"] is string maxConnections)
        {
            if (!int.TryParse(maxConnections, out var connections) || connections <= 0)
            {
                errors.Add($"RequestLimits:MaxConcurrentConnections must be a positive integer, got: '{maxConnections}'.");
            }
        }
    }

    private static void ValidateMetadataPaths(IConfiguration configuration, List<string> errors, ILogger? logger)
    {
        var metadataPath = configuration["Honua:MetadataPath"] ?? configuration["MetadataPath"];
        if (metadataPath.IsNullOrWhiteSpace())
        {
            errors.Add("Metadata path (Honua:MetadataPath) is not configured. This is required for server startup.");
        }
        else
        {
            // Validate path exists (but allow relative paths)
            if (!System.IO.Directory.Exists(metadataPath) && !System.IO.File.Exists(metadataPath))
            {
                // Don't fail - it might be a relative path that will be resolved later
                logger?.LogWarning(
                    "Configured metadata path '{MetadataPath}' does not exist at startup. " +
                    "It will be resolved relative to content root",
                    metadataPath);
            }
        }
    }

    private static void ValidateSecuritySettings(IConfiguration configuration, List<string> errors)
    {
        var security = configuration.GetSection("Security");
        if (!security.Exists())
        {
            return;
        }

        // Validate HTTPS redirect in production
        var environment = configuration["ASPNETCORE_ENVIRONMENT"];
        var useHttpsRedirection = security["UseHttpsRedirection"];
        if (environment is "Production" or "Staging")
        {
            if (useHttpsRedirection is null or "false" or "False")
            {
                errors.Add(
                    "Security:UseHttpsRedirection is disabled in production/staging environment. " +
                    "This is a security risk. Enable HTTPS redirection for production deployments.");
            }
        }

        // Validate allowed hosts in production
        var allowedHosts = configuration["AllowedHosts"];
        if (environment is "Production")
        {
            if (allowedHosts.IsNullOrWhiteSpace() || allowedHosts == "*")
            {
                errors.Add(
                    "AllowedHosts is not restricted in production. " +
                    "Configure specific allowed hosts to prevent host header injection attacks.");
            }
        }

        // Validate CORS settings
        var cors = configuration.GetSection("Honua:Server:Cors");
        if (cors.Exists())
        {
            var allowAnyOrigin = cors["AllowAnyOrigin"];
            var allowCredentials = cors["AllowCredentials"];

            if (allowAnyOrigin is "true" or "True" && allowCredentials is "true" or "True")
            {
                errors.Add(
                    "CORS configuration error: Cannot allow credentials (AllowCredentials=true) " +
                    "when allowing any origin (AllowAnyOrigin=true). This violates CORS specification.");
            }
        }

        // Validate encryption key for sensitive data
        var encryptionKey = configuration["Security:EncryptionKey"];
        if (encryptionKey.HasValue())
        {
            if (encryptionKey.Length < 32)
            {
                errors.Add(
                    "Security:EncryptionKey is too short. " +
                    "Encryption keys must be at least 32 characters for adequate security.");
            }
        }
    }

    /// <summary>
    /// Validates that required configuration values are present.
    /// </summary>
    public static void ValidateRequiredSettings(IConfiguration configuration, params string[] requiredKeys)
    {
        Guard.NotNull(configuration);

        var missingKeys = new List<string>();

        foreach (var key in requiredKeys)
        {
            var value = configuration[key];
            if (value.IsNullOrWhiteSpace())
            {
                missingKeys.Add(key);
            }
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                $"Required configuration keys are missing: {string.Join(", ", missingKeys)}");
        }
    }

    /// <summary>
    /// Validates numeric configuration values are within acceptable ranges.
    /// </summary>
    public static void ValidateNumericRange(
        IConfiguration configuration,
        string key,
        long minValue,
        long maxValue)
    {
        Guard.NotNull(configuration);

        var value = configuration[key];
        if (value.IsNullOrWhiteSpace())
        {
            return; // Optional setting
        }

        if (!long.TryParse(value, out var numericValue))
        {
            throw new InvalidOperationException(
                $"Configuration key '{key}' must be a numeric value, got: '{value}'.");
        }

        if (numericValue < minValue || numericValue > maxValue)
        {
            throw new InvalidOperationException(
                $"Configuration key '{key}' value {numericValue} is out of range. " +
                $"Must be between {minValue} and {maxValue}.");
        }
    }
}
