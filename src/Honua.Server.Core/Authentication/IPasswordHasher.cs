// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Authentication;

/// <summary>
/// Service for securely hashing and verifying passwords using industry-standard algorithms.
/// Implements password security best practices including salting and key derivation functions.
/// </summary>
/// <remarks>
/// The password hasher uses PBKDF2 (Password-Based Key Derivation Function 2) by default,
/// which provides protection against brute-force attacks through:
/// - Cryptographic salt (unique random value per password)
/// - Iteration count (computational cost factor)
/// - Key derivation (stretches the password hash)
///
/// Stored password hashes are never reversible to plaintext, ensuring password security
/// even if the database is compromised.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    /// Hashes a password using a cryptographically secure algorithm with automatic salt generation.
    /// </summary>
    /// <param name="password">The plaintext password to hash.</param>
    /// <returns>
    /// A <see cref="PasswordHashResult"/> containing the hash bytes, salt bytes, algorithm name,
    /// and algorithm parameters (e.g., iteration count).
    /// </returns>
    /// <exception cref="System.ArgumentException">Thrown when password is null or empty.</exception>
    PasswordHashResult HashPassword(string password);

    /// <summary>
    /// Verifies that a plaintext password matches a previously hashed password.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    /// <param name="password">The plaintext password to verify.</param>
    /// <param name="hash">The stored password hash bytes.</param>
    /// <param name="salt">The stored salt bytes used during hashing.</param>
    /// <param name="algorithm">The algorithm name (e.g., "PBKDF2").</param>
    /// <param name="parameters">The algorithm parameters (e.g., iteration count, key length).</param>
    /// <returns>
    /// <c>true</c> if the password matches the hash; <c>false</c> otherwise.
    /// Returns <c>false</c> for any errors to prevent information disclosure.
    /// </returns>
    /// <exception cref="System.ArgumentException">Thrown when password is null or empty.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when hash or salt is null.</exception>
    bool VerifyPassword(string password, byte[] hash, byte[] salt, string algorithm, string parameters);
}
