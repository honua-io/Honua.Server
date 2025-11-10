// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher;

    public PasswordHasherTests()
    {
        _hasher = new PasswordHasher();
    }

    [Fact]
    public void HashPassword_WithValidPassword_ReturnsArgon2idHash()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var result = _hasher.HashPassword(password);

        // Assert
        result.Should().NotBeNull();
        result.Hash.Should().NotBeEmpty();
        result.Salt.Should().NotBeEmpty();
        result.Algorithm.Should().Be("Argon2id");
        result.Parameters.Should().NotBeNullOrEmpty();
        result.Parameters.Should().Contain("timeCost");
        result.Parameters.Should().Contain("memoryCost");
        result.Parameters.Should().Contain("parallelism");
    }

    [Fact]
    public void HashPassword_GeneratesUniqueSalts()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var result1 = _hasher.HashPassword(password);
        var result2 = _hasher.HashPassword(password);

        // Assert
        result1.Salt.Should().NotBeEquivalentTo(result2.Salt);
        result1.Hash.Should().NotBeEquivalentTo(result2.Hash);
    }

    [Fact]
    public void HashPassword_WithSamePassword_GeneratesDifferentHashes()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var result1 = _hasher.HashPassword(password);
        var result2 = _hasher.HashPassword(password);

        // Assert - Due to different salts, hashes should be different
        result1.Hash.Should().NotBeEquivalentTo(result2.Hash);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void HashPassword_WithInvalidPassword_ThrowsException(string? password)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _hasher.HashPassword(password!));
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hashResult = _hasher.HashPassword(password);

        // Act
        var isValid = _hasher.VerifyPassword(
            password,
            hashResult.Hash,
            hashResult.Salt,
            hashResult.Algorithm,
            hashResult.Parameters);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var correctPassword = "SecurePassword123!";
        var wrongPassword = "WrongPassword456!";
        var hashResult = _hasher.HashPassword(correctPassword);

        // Act
        var isValid = _hasher.VerifyPassword(
            wrongPassword,
            hashResult.Hash,
            hashResult.Salt,
            hashResult.Algorithm,
            hashResult.Parameters);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_WithDifferentSalt_ReturnsFalse()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hashResult = _hasher.HashPassword(password);
        var differentSalt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

        // Act
        var isValid = _hasher.VerifyPassword(
            password,
            hashResult.Hash,
            differentSalt,
            hashResult.Algorithm,
            hashResult.Parameters);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_SupportsArgon2id()
    {
        // Arrange
        var password = "TestPassword123!";
        var hashResult = _hasher.HashPassword(password);

        // Act
        var isValid = _hasher.VerifyPassword(
            password,
            hashResult.Hash,
            hashResult.Salt,
            "Argon2id",
            hashResult.Parameters);

        // Assert
        isValid.Should().BeTrue();
        hashResult.Algorithm.Should().Be("Argon2id");
    }

    [Fact]
    public void VerifyPassword_SupportsPBKDF2()
    {
        // Arrange - Manually create a PBKDF2 hash for testing backward compatibility
        var password = "TestPassword123!";
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        var iterations = 210000;
        var hash = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            32);

        var parameters = $"iterations={iterations};hashSize=32";

        // Act
        var isValid = _hasher.VerifyPassword(
            password,
            hash,
            salt,
            "PBKDF2-SHA256",
            parameters);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithUnsupportedAlgorithm_ThrowsException()
    {
        // Arrange
        var password = "TestPassword123!";
        var hashResult = _hasher.HashPassword(password);

        // Act & Assert
        Assert.Throws<NotSupportedException>(() =>
            _hasher.VerifyPassword(
                password,
                hashResult.Hash,
                hashResult.Salt,
                "UnsupportedAlgorithm",
                hashResult.Parameters));
    }

    [Fact]
    public void VerifyPassword_WithEmptyHash_ReturnsFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var emptyHash = Array.Empty<byte>();

        // Act
        var isValid = _hasher.VerifyPassword(
            password,
            emptyHash,
            salt,
            "Argon2id",
            "timeCost=4;memoryCost=65536");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_GeneratesConsistentHashLength()
    {
        // Arrange
        var password1 = "Short1!";
        var password2 = "VeryLongPasswordWithManyCharacters123!@#";

        // Act
        var result1 = _hasher.HashPassword(password1);
        var result2 = _hasher.HashPassword(password2);

        // Assert - All Argon2id hashes should have the same length
        result1.Hash.Length.Should().Be(result2.Hash.Length);
        result1.Hash.Length.Should().Be(32); // Default hash length
    }

    [Fact]
    public void VerifyPassword_IsTimingAttackResistant()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hashResult = _hasher.HashPassword(password);
        var iterations = 100;

        // Act - Verify correct password multiple times
        var correctTimings = new System.Collections.Generic.List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _hasher.VerifyPassword(password, hashResult.Hash, hashResult.Salt, hashResult.Algorithm, hashResult.Parameters);
            sw.Stop();
            correctTimings.Add(sw.ElapsedTicks);
        }

        // Verify incorrect password multiple times
        var incorrectTimings = new System.Collections.Generic.List<long>();
        for (int i = 0; i < iterations; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _hasher.VerifyPassword("WrongPassword", hashResult.Hash, hashResult.Salt, hashResult.Algorithm, hashResult.Parameters);
            sw.Stop();
            incorrectTimings.Add(sw.ElapsedTicks);
        }

        // Assert - Timing should be relatively consistent (within 50% variance) to prevent timing attacks
        var correctAvg = correctTimings.Average();
        var incorrectAvg = incorrectTimings.Average();
        var timingDifference = Math.Abs(correctAvg - incorrectAvg) / correctAvg;

        // The timing difference should be minimal (cryptographic operations should take similar time)
        // We allow up to 50% variance due to system factors, but the comparison itself uses constant-time
        timingDifference.Should().BeLessThan(0.5, "because password verification should use constant-time comparison");
    }
}
