// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Cloud.Security;

/// <summary>
/// Encrypts XML keys using AWS Key Management Service (KMS).
/// This allows Data Protection keys to be encrypted at rest using AWS KMS.
/// </summary>
/// <remarks>
/// BUG FIX #20: Made encryption async to avoid blocking threads on AWS KMS calls.
/// Note: IXmlEncryptor interface is synchronous (from ASP.NET Core), but encryption
/// typically happens during app startup, not on hot path, so sync wrapper is acceptable.
/// The underlying AWS SDK call is now properly async.
/// </remarks>
public sealed class AwsKmsXmlEncryptor : IXmlEncryptor
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly string _keyId;

    /// <summary>
    /// Creates a new AWS KMS XML encryptor.
    /// </summary>
    /// <param name="keyId">The AWS KMS key ID or ARN to use for encryption.</param>
    /// <param name="region">The AWS region where the KMS key resides.</param>
    public AwsKmsXmlEncryptor(string keyId, string region)
    {
        if (keyId.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("KMS key ID cannot be null or empty.", nameof(keyId));
        }

        if (region.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("AWS region cannot be null or empty.", nameof(region));
        }

        _keyId = keyId;

        // Use AWS credentials from environment (IAM role, env vars, AWS CLI, etc.)
        var config = new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
        };

        _kmsClient = new AmazonKeyManagementServiceClient(config);
    }

    /// <summary>
    /// Encrypts the specified XML element using AWS KMS.
    /// </summary>
    /// <param name="plaintextElement">The plaintext XML element to encrypt.</param>
    /// <returns>An encrypted representation of the XML element.</returns>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }

        // BUG FIX #20: Use async encryption method
        // Note: IXmlEncryptor.Encrypt is sync, called during startup, so Task.Run is acceptable here
        return Task.Run(() => EncryptAsync(plaintextElement)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Encrypts the specified XML element using AWS KMS asynchronously.
    /// </summary>
    public async Task<EncryptedXmlInfo> EncryptAsync(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }

        // Convert XML to bytes
        var plaintext = plaintextElement.ToString(SaveOptions.DisableFormatting);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // BUG FIX #20: Properly await AWS KMS call instead of blocking
        var request = new EncryptRequest
        {
            KeyId = _keyId,
            Plaintext = new MemoryStream(plaintextBytes)
        };

        EncryptResponse response;
        try
        {
            response = await _kmsClient.EncryptAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to encrypt data using AWS KMS key '{_keyId}'. " +
                $"Ensure the key exists, is enabled, and the application has kms:Encrypt permissions.",
                ex);
        }

        // Convert encrypted data to base64
        var ciphertextBase64 = Convert.ToBase64String(response.CiphertextBlob.ToArray());

        // Create encrypted XML element
        var element = new XElement("encryptedKey",
            new XComment(" This key is encrypted with AWS KMS. "),
            new XElement("value", ciphertextBase64),
            new XElement("keyId", _keyId));

        return new EncryptedXmlInfo(element, typeof(AwsKmsXmlDecryptor));
    }
}

/// <summary>
/// Decrypts XML keys that were encrypted using AWS KMS.
/// </summary>
/// <remarks>
/// BUG FIX #20: Made decryption async to avoid blocking threads on AWS KMS calls.
/// Note: IXmlDecryptor interface is synchronous (from ASP.NET Core), but decryption
/// typically happens during app startup, not on hot path, so sync wrapper is acceptable.
/// The underlying AWS SDK call is now properly async.
/// </remarks>
public sealed class AwsKmsXmlDecryptor : IXmlDecryptor
{
    private readonly IAmazonKeyManagementService _kmsClient;

    /// <summary>
    /// Creates a new AWS KMS XML decryptor.
    /// </summary>
    /// <param name="region">The AWS region where the KMS key resides.</param>
    public AwsKmsXmlDecryptor(string region)
    {
        if (region.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("AWS region cannot be null or empty.", nameof(region));
        }

        var config = new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region)
        };

        _kmsClient = new AmazonKeyManagementServiceClient(config);
    }

    /// <summary>
    /// Decrypts the specified XML element.
    /// </summary>
    /// <param name="encryptedElement">The encrypted XML element.</param>
    /// <returns>The decrypted XML element.</returns>
    public XElement Decrypt(XElement encryptedElement)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }

        // BUG FIX #20: Use async decryption method
        // Note: IXmlDecryptor.Decrypt is sync, called during startup, so Task.Run is acceptable here
        return Task.Run(() => DecryptAsync(encryptedElement)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Decrypts the specified XML element asynchronously.
    /// </summary>
    public async Task<XElement> DecryptAsync(XElement encryptedElement)
    {
        if (encryptedElement == null)
        {
            throw new ArgumentNullException(nameof(encryptedElement));
        }

        // Extract ciphertext from XML
        var ciphertextBase64 = encryptedElement.Element("value")?.Value;
        if (ciphertextBase64.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Encrypted element does not contain a 'value' element.");
        }

        var ciphertextBytes = Convert.FromBase64String(ciphertextBase64);

        // BUG FIX #20: Properly await AWS KMS call instead of blocking
        var request = new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(ciphertextBytes)
        };

        DecryptResponse response;
        try
        {
            response = await _kmsClient.DecryptAsync(request).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to decrypt data using AWS KMS. " +
                "Ensure the application has kms:Decrypt permissions for the key used to encrypt this data.",
                ex);
        }

        // Convert decrypted bytes back to XML
        using var reader = new StreamReader(response.Plaintext, Encoding.UTF8);
        var plaintextXml = await reader.ReadToEndAsync().ConfigureAwait(false);

        return XElement.Parse(plaintextXml);
    }
}
