using FluentAssertions;
using Honua.Server.Core.Authentication;

namespace Honua.Server.Core.Tests.Security.Authentication;

/// <summary>
/// Comprehensive tests for password complexity validation.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class PasswordComplexityValidatorTests
{
    [Fact]
    public void Validate_WithValidPassword_ReturnsSuccess()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "MySecureP@ssw0rd123";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithNullPassword_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();

        // Act
        var result = validator.Validate(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password is required.");
    }

    [Fact]
    public void Validate_WithEmptyPassword_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();

        // Act
        var result = validator.Validate("");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password is required.");
    }

    [Fact]
    public void Validate_WithTooShortPassword_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 12);
        var password = "Short1!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must be at least 12 characters long.");
    }

    [Fact]
    public void Validate_WithNoUppercase_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireUppercase: true);
        var password = "mysecurepassword123!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one uppercase letter (A-Z).");
    }

    [Fact]
    public void Validate_WithNoLowercase_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireLowercase: true);
        var password = "MYSECUREPASSWORD123!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one lowercase letter (a-z).");
    }

    [Fact]
    public void Validate_WithNoDigit_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireDigit: true);
        var password = "MySecurePassword!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one digit (0-9).");
    }

    [Fact]
    public void Validate_WithNoSpecialCharacter_ReturnsError()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(requireSpecialCharacter: true);
        var password = "MySecurePassword123";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must contain at least one special character (!@#$%^&* etc).");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password123!")]
    [InlineData("123456")]
    [InlineData("qwerty")]
    [InlineData("abc123")]
    [InlineData("Admin123!")]
    [InlineData("Welcome123!")]
    public void Validate_WithCommonPassword_ReturnsError(string password)
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 6);

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password is too common. Please choose a more unique password.");
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
        result.Errors.Should().HaveCountGreaterThan(1);
        result.Errors.Should().Contain("Password must be at least 12 characters long.");
        result.Errors.Should().Contain("Password must contain at least one uppercase letter (A-Z).");
        result.Errors.Should().Contain("Password must contain at least one digit (0-9).");
        result.Errors.Should().Contain("Password must contain at least one special character (!@#$%^&* etc).");
    }

    [Fact]
    public void Validate_WithCustomMinimumLength_EnforcesCorrectly()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(
            minimumLength: 16,
            requireUppercase: false,
            requireLowercase: false,
            requireDigit: false,
            requireSpecialCharacter: false);
        var password = "ShortPassword1!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must be at least 16 characters long.");
    }

    [Fact]
    public void Validate_WithNoRequirements_OnlyChecksLength()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(
            minimumLength: 8,
            requireUppercase: false,
            requireLowercase: false,
            requireDigit: false,
            requireSpecialCharacter: false);
        var password = "simplepw";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("MyP@ssw0rd123")]
    [InlineData("Secure!Password456")]
    [InlineData("C0mplex#Pass789")]
    [InlineData("Strong$Key2024")]
    public void Validate_WithStrongPasswords_ReturnsSuccess(string password)
    {
        // Arrange
        var validator = new PasswordComplexityValidator();

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
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
    [InlineData("+")]
    [InlineData("=")]
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
    public void Validate_WithSpecialCharacter_RecognizesAsValid(string specialChar)
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 12);
        var password = $"MyPassword123{specialChar}";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue($"'{specialChar}' should be recognized as a special character");
    }

    [Fact]
    public void Validate_WithMixedCase_PassesUpperAndLowerChecks()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "MixedCaseP@ss123";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMultipleDigits_PassesDigitCheck()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "MyPassword1234567890!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMultipleSpecialChars_PassesSpecialCharCheck()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "MyPassword!@#$%123";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "MyP@ssw0rd123αβγ";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithWhitespace_StillValidatesOtherRequirements()
    {
        // Arrange
        var validator = new PasswordComplexityValidator();
        var password = "My Secure P@ssw0rd 123";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ExactlyMinimumLength_Passes()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 12);
        var password = "MyP@ssw0rd12"; // Exactly 12 characters

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        password.Length.Should().Be(12);
    }

    [Fact]
    public void Validate_OneLessThanMinimumLength_Fails()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 12);
        var password = "MyP@ssw0rd1"; // 11 characters

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Password must be at least 12 characters long.");
        password.Length.Should().Be(11);
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("PASSWORD", true)]
    [InlineData("PaSsWoRd", true)]
    [InlineData("PASSWORD123", true)]
    [InlineData("qwerty", true)]
    [InlineData("QWERTY", true)]
    [InlineData("123456", true)]
    [InlineData("letmein", true)]
    [InlineData("admin", true)]
    [InlineData("ADMIN", true)]
    [InlineData("welcome", true)]
    [InlineData("WELCOME", true)]
    public void Validate_CommonPasswordVariations_AreDetected(string password, bool shouldFail)
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 6);

        // Act
        var result = validator.Validate(password);

        // Assert
        if (shouldFail)
        {
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain("Password is too common. Please choose a more unique password.");
        }
    }

    [Fact]
    public void Validate_LongPasswordWithAllRequirements_Succeeds()
    {
        // Arrange
        var validator = new PasswordComplexityValidator(minimumLength: 20);
        var password = "ThisIsAVeryLongAndSecureP@ssw0rd123!";

        // Act
        var result = validator.Validate(password);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PasswordComplexityResult_Success_HasCorrectProperties()
    {
        // Act
        var result = PasswordComplexityResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void PasswordComplexityResult_Failure_HasCorrectProperties()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2", "Error 3" };

        // Act
        var result = PasswordComplexityResult.Failure(errors);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().BeEquivalentTo(errors);
    }
}
