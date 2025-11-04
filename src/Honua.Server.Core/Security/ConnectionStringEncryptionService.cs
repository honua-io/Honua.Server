// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Security;

/// <summary>
/// Implementation of connection string encryption using ASP.NET Core Data Protection API.
/// Supports backward compatibility with unencrypted connection strings.
/// </summary>
public sealed class ConnectionStringEncryptionService : IConnectionStringEncryptionService
{
    private const string EncryptionPrefix = "ENC:";
    private const string Purpose = "ConnectionString.Protection.v1";

    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ConnectionStringEncryptionOptions _options;
    private readonly ILogger<ConnectionStringEncryptionService> _logger;
    private readonly IDataProtector _protector;

    public ConnectionStringEncryptionService(
        IDataProtectionProvider dataProtectionProvider,
        IOptions<ConnectionStringEncryptionOptions> options,
        ILogger<ConnectionStringEncryptionService> logger)
    {
        _dataProtectionProvider = dataProtectionProvider ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _options.Validate();

        // Create a purpose-specific protector for connection strings
        _protector = _dataProtectionProvider.CreateProtector(Purpose);
    }

    /// <inheritdoc />
    public Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
    {
        if (plainText.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(plainText));
        }

        if (!_options.Enabled)
        {
            _logger.LogWarning("Connection string encryption is disabled. Returning plain text connection string.");
            return Task.FromResult(plainText);
        }

        try
        {
            // If already encrypted, return as-is
            if (IsEncrypted(plainText))
            {
                _logger.LogDebug("Connection string is already encrypted.");
                return Task.FromResult(plainText);
            }

            // Encrypt using Data Protection API
            var protectedData = _protector.Protect(plainText);

            // Convert to Base64 for safe storage
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(protectedData));

            // Add prefix to identify encrypted strings
            var encrypted = $"{EncryptionPrefix}{base64}";

            _logger.LogInformation("Successfully encrypted connection string.");
            return Task.FromResult(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to encrypt connection string. This is a critical failure that will prevent secure storage of connection strings.");
            throw new InvalidOperationException("Failed to encrypt connection string.", ex);
        }
    }

    /// <inheritdoc />
    public Task<string> DecryptAsync(string value, CancellationToken cancellationToken = default)
    {
        if (value.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(value));
        }

        // Backward compatibility: if not encrypted, return as-is
        if (!IsEncrypted(value))
        {
            _logger.LogDebug("Connection string is not encrypted. Returning as-is for backward compatibility.");
            return Task.FromResult(value);
        }

        if (!_options.Enabled)
        {
            _logger.LogWarning("Connection string encryption is disabled but encrypted connection string detected. Cannot decrypt.");
            throw new InvalidOperationException("Encrypted connection string detected but encryption is disabled in configuration.");
        }

        try
        {
            // Remove prefix
            var base64 = value.Substring(EncryptionPrefix.Length);

            // Decode from Base64
            var protectedData = Encoding.UTF8.GetString(Convert.FromBase64String(base64));

            // Decrypt using Data Protection API
            var plainText = _protector.Unprotect(protectedData);

            _logger.LogDebug("Successfully decrypted connection string.");
            return Task.FromResult(plainText);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to decrypt connection string. Ensure the correct encryption keys are available. This is a critical failure that will prevent database connectivity.");
            throw new InvalidOperationException("Failed to decrypt connection string. Ensure the correct encryption keys are available.", ex);
        }
    }

    /// <inheritdoc />
    public bool IsEncrypted(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        return value.StartsWith(EncryptionPrefix, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public async Task<string> RotateKeyAsync(string encryptedValue, CancellationToken cancellationToken = default)
    {
        if (encryptedValue.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Encrypted connection string cannot be null or empty.", nameof(encryptedValue));
        }

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Connection string encryption is disabled. Cannot rotate keys.");
        }

        if (!IsEncrypted(encryptedValue))
        {
            throw new ArgumentException("Value is not encrypted. Use EncryptAsync instead.", nameof(encryptedValue));
        }

        try
        {
            _logger.LogInformation("Starting key rotation for connection string.");

            // Decrypt with old key
            var plainText = await DecryptAsync(encryptedValue, cancellationToken).ConfigureAwait(false);

            // Re-encrypt with new key (Data Protection API handles key rotation automatically)
            // The CreateProtector with same purpose will use the latest key
            var newProtector = _dataProtectionProvider.CreateProtector(Purpose);
            var protectedData = newProtector.Protect(plainText);
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(protectedData));
            var reencrypted = $"{EncryptionPrefix}{base64}";

            _logger.LogInformation("Successfully rotated encryption key for connection string.");
            return reencrypted;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to rotate encryption key for connection string. This is a critical failure that may leave the connection string encrypted with an old or invalid key.");
            throw new InvalidOperationException("Failed to rotate encryption key.", ex);
        }
    }
}
