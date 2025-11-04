// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Configures ASP.NET Core Data Protection for connection string encryption.
/// Supports multiple key storage providers: FileSystem, Azure Key Vault, AWS KMS, GCP KMS.
/// </summary>
public static class DataProtectionConfiguration
{
    /// <summary>
    /// Adds Data Protection services configured for connection string encryption.
    /// </summary>
    public static IServiceCollection AddConnectionStringEncryption(
        this IServiceCollection services,
        ConnectionStringEncryptionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate();

        if (!options.Enabled)
        {
            // Register a no-op service when encryption is disabled
            services.AddSingleton<IConnectionStringEncryptionService, NoOpConnectionStringEncryptionService>();
            return services;
        }

        // Configure Data Protection based on key storage provider
        var dataProtection = services.AddDataProtection()
            .SetApplicationName(options.ApplicationName);

        ConfigureKeyStorage(dataProtection, options);

        // Register the encryption service
        services.AddSingleton<IConnectionStringEncryptionService, ConnectionStringEncryptionService>();

        return services;
    }

    /// <summary>
    /// Delegate for configuring cloud-specific key storage providers.
    /// Used by Core.Cloud module to plug in Azure Key Vault, AWS KMS, and GCP KMS support.
    /// </summary>
    public delegate void CloudKeyStorageConfigurator(IDataProtectionBuilder builder, ConnectionStringEncryptionOptions options);

    /// <summary>
    /// Optional configurator for cloud key storage providers.
    /// Set this from Core.Cloud module to enable cloud KMS support.
    /// </summary>
    public static CloudKeyStorageConfigurator? CloudConfigurator { get; set; }

    private static void ConfigureKeyStorage(
        IDataProtectionBuilder builder,
        ConnectionStringEncryptionOptions options)
    {
        var provider = options.KeyStorageProvider.ToLowerInvariant();

        switch (provider)
        {
            case "filesystem":
                ConfigureFileSystemStorage(builder, options);
                break;

            case "azurekeyvault":
            case "awskms":
            case "gcpkms":
                // Cloud providers require Core.Cloud module
                if (CloudConfigurator == null)
                {
                    throw new InvalidOperationException(
                        $"Key storage provider '{options.KeyStorageProvider}' requires Honua.Server.Core.Cloud module. " +
                        "Ensure Core.Cloud is referenced and its configuration methods are called during startup.");
                }
                CloudConfigurator(builder, options);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported key storage provider: {options.KeyStorageProvider}. " +
                    "Supported providers: filesystem, azurekeyvault (requires Core.Cloud), awskms (requires Core.Cloud), gcpkms (requires Core.Cloud).");
        }

        // Set key lifetime
        builder.SetDefaultKeyLifetime(TimeSpan.FromDays(options.KeyLifetimeDays));
    }

    private static void ConfigureFileSystemStorage(
        IDataProtectionBuilder builder,
        ConnectionStringEncryptionOptions options)
    {
        // Use specified directory or default to app data directory
        var keyDirectory = options.KeyStorageDirectory;
        if (keyDirectory.IsNullOrWhiteSpace())
        {
            // Default to a secure location within the application
            var appData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.DoNotVerify);

            keyDirectory = Path.Combine(appData, options.ApplicationName, "DataProtection-Keys");
        }

        var directoryInfo = new DirectoryInfo(keyDirectory);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();

            // Set restrictive permissions on Unix-based systems
            if (!OperatingSystem.IsWindows())
            {
                try
                {
                    // chmod 700
                    File.SetUnixFileMode(keyDirectory,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                catch
                {
                    // Ignore if we can't set permissions
                }
            }
        }

        builder.PersistKeysToFileSystem(directoryInfo);

        // On Windows, use DPAPI for at-rest encryption
        if (OperatingSystem.IsWindows())
        {
            builder.ProtectKeysWithDpapi();
        }
    }

    // Note: Cloud KMS configuration methods (Azure Key Vault, AWS KMS, GCP KMS) have been moved to
    // Honua.Server.Core.Cloud.Security.CloudDataProtectionExtensions for modular architecture.

    /// <summary>
    /// No-op implementation used when encryption is disabled.
    /// </summary>
    private sealed class NoOpConnectionStringEncryptionService : IConnectionStringEncryptionService
    {
        private readonly ILogger<NoOpConnectionStringEncryptionService> _logger;

        public NoOpConnectionStringEncryptionService(ILogger<NoOpConnectionStringEncryptionService> logger)
        {
            _logger = logger;
        }

        public Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Connection string encryption is disabled. Returning unencrypted value.");
            return Task.FromResult(plainText);
        }

        public Task<string> DecryptAsync(string value, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(value);
        }

        public bool IsEncrypted(string value)
        {
            return false;
        }

        public Task<string> RotateKeyAsync(string encryptedValue, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Key rotation is not supported when encryption is disabled.");
        }
    }
}
