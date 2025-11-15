// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using Honua.Server.Core.Domain.Common;

namespace Honua.Server.Core.Domain.Sharing;

/// <summary>
/// Value object representing a hashed password for share protection.
/// Encapsulates password hashing and validation logic.
/// </summary>
public sealed class SharePassword : ValueObject
{
    private const int SaltSize = 16; // 128 bits
    private const int HashSize = 32; // 256 bits
    private const int Iterations = 100000; // PBKDF2 iterations

    /// <summary>
    /// Gets the password hash (salt + hash combined)
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// Private constructor for creating a SharePassword
    /// </summary>
    private SharePassword(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Password hash cannot be empty", nameof(hash));

        Hash = hash;
    }

    /// <summary>
    /// Creates a SharePassword from a plain text password by hashing it
    /// </summary>
    /// <param name="plainTextPassword">The plain text password to hash</param>
    /// <returns>A new SharePassword instance</returns>
    /// <exception cref="ArgumentException">Thrown when password is invalid</exception>
    public static SharePassword Create(string plainTextPassword)
    {
        if (string.IsNullOrWhiteSpace(plainTextPassword))
            throw new ArgumentException("Password cannot be empty", nameof(plainTextPassword));

        if (plainTextPassword.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters long", nameof(plainTextPassword));

        if (plainTextPassword.Length > 100)
            throw new ArgumentException("Password must not exceed 100 characters", nameof(plainTextPassword));

        var hash = HashPassword(plainTextPassword);
        return new SharePassword(hash);
    }

    /// <summary>
    /// Creates a SharePassword from an existing hash (for reconstruction from storage)
    /// </summary>
    /// <param name="existingHash">The existing password hash</param>
    /// <returns>A new SharePassword instance</returns>
    public static SharePassword FromHash(string existingHash)
    {
        return new SharePassword(existingHash);
    }

    /// <summary>
    /// Validates a plain text password against this hashed password
    /// </summary>
    /// <param name="plainTextPassword">The plain text password to validate</param>
    /// <returns>True if the password matches, false otherwise</returns>
    public bool Validate(string plainTextPassword)
    {
        if (string.IsNullOrWhiteSpace(plainTextPassword))
            return false;

        try
        {
            return VerifyPassword(plainTextPassword, Hash);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Hashes a password using PBKDF2
    /// </summary>
    private static string HashPassword(string password)
    {
        // Generate a random salt
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        // Hash the password with the salt
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        byte[] hash = pbkdf2.GetBytes(HashSize);

        // Combine salt and hash
        byte[] hashBytes = new byte[SaltSize + HashSize];
        Array.Copy(salt, 0, hashBytes, 0, SaltSize);
        Array.Copy(hash, 0, hashBytes, SaltSize, HashSize);

        // Convert to base64 for storage
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Verifies a password against a hash
    /// </summary>
    private static bool VerifyPassword(string password, string storedHash)
    {
        // Extract the bytes
        byte[] hashBytes = Convert.FromBase64String(storedHash);

        // Extract the salt
        byte[] salt = new byte[SaltSize];
        Array.Copy(hashBytes, 0, salt, 0, SaltSize);

        // Compute the hash on the password the user entered
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);

        byte[] hash = pbkdf2.GetBytes(HashSize);

        // Compare the results
        for (int i = 0; i < HashSize; i++)
        {
            if (hashBytes[i + SaltSize] != hash[i])
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Hash;
    }

    /// <summary>
    /// Returns the hash value for display (not recommended for security)
    /// </summary>
    public override string ToString()
    {
        return $"[Password Hash: {Hash.Substring(0, Math.Min(10, Hash.Length))}...]";
    }
}
