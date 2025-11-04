// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Configuration options for connection string encryption.
/// </summary>
public sealed class ConnectionStringEncryptionOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Honua:Security:ConnectionStringEncryption";

    /// <summary>
    /// Gets or sets whether connection string encryption is enabled.
    /// Default is true for security by default.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the key storage provider type.
    /// Supported values: "FileSystem", "AzureKeyVault", "AwsKms", "GcpKms".
    /// Default is "FileSystem" for ease of use.
    /// </summary>
    public string KeyStorageProvider { get; set; } = "FileSystem";

    /// <summary>
    /// Gets or sets the directory path for file system key storage.
    /// Only used when KeyStorageProvider is "FileSystem".
    /// </summary>
    public string? KeyStorageDirectory { get; set; }

    /// <summary>
    /// Gets or sets the Azure Key Vault URI.
    /// Only used when KeyStorageProvider is "AzureKeyVault".
    /// Example: "https://myvault.vault.azure.net/"
    /// </summary>
    public string? AzureKeyVaultUri { get; set; }

    /// <summary>
    /// Gets or sets the Azure Key Vault key name.
    /// Only used when KeyStorageProvider is "AzureKeyVault".
    /// </summary>
    public string? AzureKeyVaultKeyName { get; set; }

    /// <summary>
    /// Gets or sets the AWS KMS key ID or ARN.
    /// Only used when KeyStorageProvider is "AwsKms".
    /// Example: "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012"
    /// </summary>
    public string? AwsKmsKeyId { get; set; }

    /// <summary>
    /// Gets or sets the AWS region for KMS.
    /// Only used when KeyStorageProvider is "AwsKms".
    /// </summary>
    public string? AwsRegion { get; set; }

    /// <summary>
    /// Gets or sets the GCP KMS key resource name.
    /// Only used when KeyStorageProvider is "GcpKms".
    /// Example: "projects/PROJECT_ID/locations/LOCATION/keyRings/KEY_RING/cryptoKeys/KEY"
    /// </summary>
    public string? GcpKmsKeyResourceName { get; set; }

    /// <summary>
    /// Gets or sets the application name used for Data Protection.
    /// This ensures keys are isolated per application.
    /// </summary>
    public string ApplicationName { get; set; } = "Honua.Server";

    /// <summary>
    /// Gets or sets the key lifetime in days before rotation is recommended.
    /// Default is 90 days.
    /// </summary>
    public int KeyLifetimeDays { get; set; } = 90;

    /// <summary>
    /// Validates the configuration options.
    /// </summary>
    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (KeyStorageProvider.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("KeyStorageProvider must be specified when encryption is enabled.");
        }

        switch (KeyStorageProvider.ToLowerInvariant())
        {
            case "filesystem":
                // KeyStorageDirectory is optional - will use default if not specified
                break;

            case "azurekeyvault":
                if (AzureKeyVaultUri.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("AzureKeyVaultUri must be specified when using AzureKeyVault key storage.");
                }
                if (AzureKeyVaultKeyName.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("AzureKeyVaultKeyName must be specified when using AzureKeyVault key storage.");
                }
                break;

            case "awskms":
                if (AwsKmsKeyId.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("AwsKmsKeyId must be specified when using AwsKms key storage.");
                }
                if (AwsRegion.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("AwsRegion must be specified when using AwsKms key storage.");
                }
                break;

            case "gcpkms":
                if (GcpKmsKeyResourceName.IsNullOrWhiteSpace())
                {
                    throw new InvalidOperationException("GcpKmsKeyResourceName must be specified when using GcpKms key storage.");
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported KeyStorageProvider: {KeyStorageProvider}. Supported values are: FileSystem, AzureKeyVault, AwsKms, GcpKms.");
        }

        if (KeyLifetimeDays <= 0)
        {
            throw new InvalidOperationException("KeyLifetimeDays must be greater than 0.");
        }

        if (ApplicationName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("ApplicationName must be specified.");
        }
    }
}
