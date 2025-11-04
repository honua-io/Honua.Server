// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text;
using System.Xml.Linq;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Cloud.Security;

/// <summary>
/// Encrypts XML keys using Google Cloud Key Management Service (KMS).
/// This allows Data Protection keys to be encrypted at rest using GCP KMS.
/// </summary>
public sealed class GcpKmsXmlEncryptor : IXmlEncryptor
{
    private readonly KeyManagementServiceClient _kmsClient;
    private readonly CryptoKeyName _keyName;

    /// <summary>
    /// Creates a new GCP KMS XML encryptor.
    /// </summary>
    /// <param name="keyResourceName">
    /// The full resource name of the KMS crypto key.
    /// Format: projects/{project}/locations/{location}/keyRings/{keyRing}/cryptoKeys/{cryptoKey}
    /// </param>
    public GcpKmsXmlEncryptor(string keyResourceName)
    {
        if (keyResourceName.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("KMS key resource name cannot be null or empty.", nameof(keyResourceName));
        }

        try
        {
            _keyName = CryptoKeyName.Parse(keyResourceName);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Invalid GCP KMS key resource name: '{keyResourceName}'. " +
                "Expected format: projects/{{project}}/locations/{{location}}/keyRings/{{keyRing}}/cryptoKeys/{{cryptoKey}}",
                nameof(keyResourceName),
                ex);
        }

        // Use default credentials (service account, application default credentials, etc.)
        _kmsClient = KeyManagementServiceClient.Create();
    }

    /// <summary>
    /// Encrypts the specified XML element using GCP KMS.
    /// </summary>
    /// <param name="plaintextElement">The plaintext XML element to encrypt.</param>
    /// <returns>An encrypted representation of the XML element.</returns>
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        if (plaintextElement == null)
        {
            throw new ArgumentNullException(nameof(plaintextElement));
        }

        // Convert XML to bytes
        var plaintext = plaintextElement.ToString(SaveOptions.DisableFormatting);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        // Encrypt using KMS
        EncryptResponse response;
        try
        {
            response = _kmsClient.Encrypt(_keyName, ByteString.CopyFrom(plaintextBytes));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to encrypt data using GCP KMS key '{_keyName}'. " +
                $"Ensure the key exists, is enabled, and the service account has cloudkms.cryptoKeyVersions.useToEncrypt permissions.",
                ex);
        }

        // Convert encrypted data to base64
        var ciphertextBase64 = Convert.ToBase64String(response.Ciphertext.ToByteArray());

        // Create encrypted XML element
        var element = new XElement("encryptedKey",
            new XComment(" This key is encrypted with GCP KMS. "),
            new XElement("value", ciphertextBase64),
            new XElement("keyResourceName", _keyName.ToString()));

        return new EncryptedXmlInfo(element, typeof(GcpKmsXmlDecryptor));
    }
}

/// <summary>
/// Decrypts XML keys that were encrypted using GCP KMS.
/// </summary>
public sealed class GcpKmsXmlDecryptor : IXmlDecryptor
{
    private readonly KeyManagementServiceClient _kmsClient;

    /// <summary>
    /// Creates a new GCP KMS XML decryptor.
    /// </summary>
    public GcpKmsXmlDecryptor()
    {
        // Use default credentials (service account, application default credentials, etc.)
        _kmsClient = KeyManagementServiceClient.Create();
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

        // Extract ciphertext and key name from XML
        var ciphertextBase64 = encryptedElement.Element("value")?.Value;
        if (ciphertextBase64.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Encrypted element does not contain a 'value' element.");
        }

        var keyResourceName = encryptedElement.Element("keyResourceName")?.Value;
        if (keyResourceName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Encrypted element does not contain a 'keyResourceName' element.");
        }

        CryptoKeyName cryptoKeyName;
        try
        {
            cryptoKeyName = CryptoKeyName.Parse(keyResourceName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Invalid GCP KMS key resource name in encrypted data: '{keyResourceName}'",
                ex);
        }

        var ciphertextBytes = Convert.FromBase64String(ciphertextBase64);

        // Decrypt using KMS
        DecryptResponse response;
        try
        {
            response = _kmsClient.Decrypt(cryptoKeyName, ByteString.CopyFrom(ciphertextBytes));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to decrypt data using GCP KMS key '{cryptoKeyName}'. " +
                "Ensure the service account has cloudkms.cryptoKeyVersions.useToDecrypt permissions.",
                ex);
        }

        // Convert decrypted bytes back to XML
        var plaintextXml = Encoding.UTF8.GetString(response.Plaintext.ToByteArray());
        return XElement.Parse(plaintextXml);
    }
}
