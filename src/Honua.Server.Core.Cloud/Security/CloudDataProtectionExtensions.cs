// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Core.Security;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Cloud.Security;

/// <summary>
/// Extension methods for configuring Data Protection with cloud KMS providers.
/// </summary>
public static class CloudDataProtectionExtensions
{
    /// <summary>
    /// Configures Azure Key Vault for Data Protection key encryption.
    /// </summary>
    public static IDataProtectionBuilder ConfigureAzureKeyVault(
        this IDataProtectionBuilder builder,
        ConnectionStringEncryptionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.AzureKeyVaultUri.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("AzureKeyVaultUri is required for Azure Key Vault storage.");
        }

        if (options.AzureKeyVaultKeyName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("AzureKeyVaultKeyName is required for Azure Key Vault storage.");
        }

        // Azure Key Vault integration requires:
        // 1. Azure.Extensions.AspNetCore.DataProtection.Keys
        // 2. Azure.Identity for authentication
        // The keys will be stored in Azure Blob Storage and encrypted with Key Vault

        var keyVaultUri = new Uri(options.AzureKeyVaultUri);

        // Use DefaultAzureCredential for authentication (supports managed identity, etc.)
        // This will use environment variables, managed identity, Azure CLI, etc.
        // Configure credential options to restrict authentication methods for security
        var credentialOptions = new Azure.Identity.DefaultAzureCredentialOptions
        {
            // Exclude developer tools credentials in production for security
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeAzureCliCredential = false,        // Allow Azure CLI for deployment scenarios
            ExcludeManagedIdentityCredential = false  // Allow managed identity (production)
        };

        var credential = new Azure.Identity.DefaultAzureCredential(credentialOptions);

        // Configure Key Vault for key encryption
        builder.ProtectKeysWithAzureKeyVault(keyVaultUri, credential);

        // Note: For production, you should also persist keys to Azure Blob Storage
        // This requires additional configuration with connection string or blob URI
        // Example:
        // builder.PersistKeysToAzureBlobStorage(blobUri, credential);

        // For now, we'll use ephemeral storage with Key Vault encryption
        // In production, consider adding blob storage persistence

        return builder;
    }

    /// <summary>
    /// Configures AWS KMS for Data Protection key encryption.
    /// </summary>
    public static IDataProtectionBuilder ConfigureAwsKms(
        this IDataProtectionBuilder builder,
        ConnectionStringEncryptionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.AwsKmsKeyId.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("AwsKmsKeyId is required for AWS KMS storage.");
        }

        if (options.AwsRegion.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("AwsRegion is required for AWS KMS storage.");
        }

        // Configure AWS KMS encryption for Data Protection keys
        // Uses AWS credentials from environment (IAM role, environment variables, AWS CLI, etc.)
        // Required IAM permissions: kms:Encrypt, kms:Decrypt on the specified KMS key
        builder.Services.AddSingleton<IXmlEncryptor>(sp =>
            new AwsKmsXmlEncryptor(options.AwsKmsKeyId, options.AwsRegion));
        builder.Services.AddSingleton<IXmlDecryptor>(sp =>
            new AwsKmsXmlDecryptor(options.AwsRegion));

        // For production, consider also persisting keys to S3 for durability
        // This prevents data loss if the application's local storage is wiped
        // Example: builder.PersistKeysToAWSSystemsManager(...);

        return builder;
    }

    /// <summary>
    /// Configures GCP KMS for Data Protection key encryption.
    /// </summary>
    public static IDataProtectionBuilder ConfigureGcpKms(
        this IDataProtectionBuilder builder,
        ConnectionStringEncryptionOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.GcpKmsKeyResourceName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("GcpKmsKeyResourceName is required for GCP KMS storage.");
        }

        // Configure GCP KMS encryption for Data Protection keys
        // Uses default credentials (service account, application default credentials, etc.)
        // Required permissions: cloudkms.cryptoKeyVersions.useToEncrypt, cloudkms.cryptoKeyVersions.useToDecrypt
        builder.Services.AddSingleton<IXmlEncryptor>(sp =>
            new GcpKmsXmlEncryptor(options.GcpKmsKeyResourceName));
        builder.Services.AddSingleton<IXmlDecryptor>(sp =>
            new GcpKmsXmlDecryptor());

        // For production, consider also persisting keys to Google Cloud Storage for durability
        // This prevents data loss if the application's local storage is wiped
        // Example: builder.PersistKeysToGoogleCloudStorage(...);

        return builder;
    }

    /// <summary>
    /// Internal configurator that routes to the appropriate cloud provider.
    /// This is registered with Core's DataProtectionConfiguration at module initialization.
    /// </summary>
    internal static void ConfigureCloudKeyStorage(IDataProtectionBuilder builder, ConnectionStringEncryptionOptions options)
    {
        var provider = options.KeyStorageProvider.ToLowerInvariant();

        switch (provider)
        {
            case "azurekeyvault":
                ConfigureAzureKeyVault(builder, options);
                break;

            case "awskms":
                ConfigureAwsKms(builder, options);
                break;

            case "gcpkms":
                ConfigureGcpKms(builder, options);
                break;

            default:
                throw new InvalidOperationException(
                    $"Cloud key storage provider '{options.KeyStorageProvider}' is not supported by Core.Cloud module.");
        }
    }
}

/// <summary>
/// Module initializer that registers cloud KMS support with Core's DataProtectionConfiguration.
/// This runs automatically when the Core.Cloud assembly is loaded.
/// </summary>
internal static class CloudDataProtectionModuleInitializer
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        // Register the cloud configurator with Core
        DataProtectionConfiguration.CloudConfigurator = CloudDataProtectionExtensions.ConfigureCloudKeyStorage;
    }
}
