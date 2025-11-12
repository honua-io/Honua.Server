// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Authentication;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

public class PasswordComplexityValidatorTests
{
    [Fact]
    public void Validate_WithStrongPassword_ReturnsSuccess()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "SecurePassword123!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithShortPassword_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 12);
        var password = "Short1!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least 12 characters"));
    }

    [Fact]
    public void Validate_WithoutUppercase_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireUppercase: true);
        var password = "alllowercase123!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("uppercase letter"));
    }

    [Fact]
    public void Validate_WithoutLowercase_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireLowercase: true);
        var password = "ALLUPPERCASE123!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("lowercase letter"));
    }

    [Fact]
    public void Validate_WithoutDigit_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireDigit: true);
        var password = "NoDigitsHere!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("digit"));
    }

    [Fact]
    public void Validate_WithoutSpecialCharacter_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireSpecialCharacter: true);
        var password = "NoSpecialChars123";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("special character"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WithEmptyPassword_ReturnsError(string? password)
    {
        // Arrange
        var validator = new PasswordComplexityValidator();

        // Act
        var result = validator.Validate(password!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("required"));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("password123")]
    [InlineData("password123!")]
    [InlineData("Password123!")]
    [InlineData("123456")]
    [InlineData("qwerty")]
    [InlineData("admin123!")]
    [InlineData("Admin123!")]
    [InlineData("Welcome123!")]
    public void Validate_WithCommonPassword_ReturnsError(string password)
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 8);

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("too common"));
    }

    [Fact]
    public void Validate_WithAllRequirementsDisabled_OnlyChecksLength()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(
            minimumLength: 8,
            requireUppercase: false,
            requireLowercase: false,
            requireDigit: false,
            requireSpecialCharacter: false);
        var password = "simplelong";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithCustomMinimumLength_EnforcesLength()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(
            minimumLength: 20,
            requireUppercase: false,
            requireLowercase: false,
            requireDigit: false,
            requireSpecialCharacter: false);
        var password = "ShortPassword!1";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least 20 characters"));
    }

    [Theory]
    [InlineData("!")]
    [InlineData("@")]
    [InlineData("#")]
    [InlineData("$")]
    [InlineData("%")]
    [InlineData("^")]
    [InlineData("&")]
    [InlineData("*")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData("-")]
    [InlineData("_")]
    [InlineData("=")]
    [InlineData("+")]
    [InlineData("[")]
    [InlineData("]")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData(";")]
    [InlineData(":")]
    [InlineData("'")]
    [InlineData("\"")]
    [InlineData("\\")]
    [InlineData("|")]
    [InlineData(",")]
    [InlineData(".")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("?")]
    [InlineData("/")]
    [InlineData("~")]
    [InlineData("`")]
    public void Validate_WithVariousSpecialCharacters_Succeeds(string specialChar)
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 8, requireSpecialCharacter: true);
        var password = $"Password123{specialChar}";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMultipleViolations_ReturnsAllErrors()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(
            minimumLength: 12,
            requireUppercase: true,
            requireLowercase: true,
            requireDigit: true,
            requireSpecialCharacter: true);
        var password = "short";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThan(1);
        result.Errors.Should().Contain(e => e.Contains("12 characters"));
        result.Errors.Should().Contain(e => e.Contains("uppercase"));
        result.Errors.Should().Contain(e => e.Contains("digit"));
        result.Errors.Should().Contain(e => e.Contains("special character"));
    }

    [Fact]
    public void Validate_WithStrongRandomPassword_Succeeds()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "X9$mK#pL2@qR7!nF";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
