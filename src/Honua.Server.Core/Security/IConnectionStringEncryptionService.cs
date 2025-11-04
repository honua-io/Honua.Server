// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Security;

/// <summary>
/// Service for encrypting and decrypting connection strings using ASP.NET Core Data Protection API.
/// </summary>
public interface IConnectionStringEncryptionService
{
    /// <summary>
    /// Encrypts a connection string.
    /// </summary>
    /// <param name="plainText">The plain text connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The encrypted connection string with encryption marker prefix.</returns>
    Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypts a connection string if it is encrypted, otherwise returns the original value.
    /// </summary>
    /// <param name="value">The connection string (encrypted or plain text).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decrypted connection string.</returns>
    Task<string> DecryptAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if a connection string is encrypted.
    /// </summary>
    /// <param name="value">The connection string to check.</param>
    /// <returns>True if the connection string is encrypted, false otherwise.</returns>
    bool IsEncrypted(string value);

    /// <summary>
    /// Re-encrypts an already encrypted connection string with a new key.
    /// This is used for key rotation scenarios.
    /// </summary>
    /// <param name="encryptedValue">The currently encrypted connection string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection string encrypted with the new key.</returns>
    Task<string> RotateKeyAsync(string encryptedValue, CancellationToken cancellationToken = default);
}
