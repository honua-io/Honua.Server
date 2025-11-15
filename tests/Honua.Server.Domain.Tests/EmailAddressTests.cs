// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Honua.Server.Core.Domain.ValueObjects;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the EmailAddress value object.
/// Tests email validation, normalization, and equality.
/// </summary>
[Trait("Category", "Unit")]
public class EmailAddressTests
{
    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@example.com")]
    [InlineData("user+tag@example.com")]
    [InlineData("user_name@example.com")]
    [InlineData("user123@example.co.uk")]
    [InlineData("a@example.com")]
    [InlineData("test@subdomain.example.com")]
    public void Constructor_ShouldAcceptValidEmail_WhenProvided(string email)
    {
        // Arrange, Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Constructor_ShouldThrowDomainException_WhenEmailIsEmpty(string email)
    {
        // Arrange, Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().Throw<DomainException>()
            .WithMessage("Email address cannot be empty.")
            .Which.ErrorCode.Should().Be("EMAIL_EMPTY");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@example.com")]
    [InlineData("invalid@.com")]
    [InlineData("invalid..email@example.com")]
    [InlineData("invalid@example")]
    [InlineData("invalid @example.com")]
    [InlineData("invalid@exa mple.com")]
    public void Constructor_ShouldThrowDomainException_WhenEmailFormatIsInvalid(string email)
    {
        // Arrange, Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("EMAIL_INVALID_FORMAT");
    }

    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenEmailIsTooLong()
    {
        // Arrange
        var localPart = new string('a', 64);
        var domain = new string('b', 200) + ".com";
        var email = $"{localPart}@{domain}";

        // Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().Throw<DomainException>()
            .WithMessage("Email address cannot exceed 254 characters.")
            .Which.ErrorCode.Should().Be("EMAIL_TOO_LONG");
    }

    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenLocalPartIsTooLong()
    {
        // Arrange
        var localPart = new string('a', 65);
        var email = $"{localPart}@example.com";

        // Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().Throw<DomainException>()
            .WithMessage("Email local part cannot exceed 64 characters.")
            .Which.ErrorCode.Should().Be("EMAIL_LOCAL_PART_TOO_LONG");
    }

    [Fact]
    public void Constructor_ShouldNormalizeEmail_ToLowerCase()
    {
        // Arrange
        var email = "User@Example.COM";

        // Act
        var emailAddress = new EmailAddress(email);

        // Assert
        emailAddress.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Constructor_ShouldTrimWhitespace_FromEmail()
    {
        // Arrange
        var email = "  user@example.com  ";

        // Act
        var emailAddress = new EmailAddress(email);

        // Assert
        emailAddress.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Constructor_ShouldSetLocalPartAndDomain_Correctly()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var emailAddress = new EmailAddress(email);

        // Assert
        emailAddress.LocalPart.Should().Be("user");
        emailAddress.Domain.Should().Be("example.com");
    }

    [Fact]
    public void From_ShouldCreateEmailAddress_WhenValidEmailProvided()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var emailAddress = EmailAddress.From(email);

        // Assert
        emailAddress.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void From_ShouldThrowDomainException_WhenInvalidEmailProvided()
    {
        // Arrange
        var email = "invalid-email";

        // Act & Assert
        var act = () => EmailAddress.From(email);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TryCreate_ShouldReturnTrue_WhenValidEmailProvided()
    {
        // Arrange
        var email = "user@example.com";

        // Act
        var result = EmailAddress.TryCreate(email, out var emailAddress);

        // Assert
        result.Should().BeTrue();
        emailAddress.Should().NotBeNull();
        emailAddress!.Value.Should().Be("user@example.com");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("@example.com")]
    public void TryCreate_ShouldReturnFalse_WhenInvalidEmailProvided(string email)
    {
        // Arrange, Act
        var result = EmailAddress.TryCreate(email, out var emailAddress);

        // Assert
        result.Should().BeFalse();
        emailAddress.Should().BeNull();
    }

    [Fact]
    public void ToString_ShouldReturnEmailValue_WhenCalled()
    {
        // Arrange
        var emailAddress = new EmailAddress("user@example.com");

        // Act
        var result = emailAddress.ToString();

        // Assert
        result.Should().Be("user@example.com");
    }

    [Fact]
    public void ImplicitConversion_ShouldConvertToString_Correctly()
    {
        // Arrange
        var emailAddress = new EmailAddress("user@example.com");

        // Act
        string result = emailAddress;

        // Assert
        result.Should().Be("user@example.com");
    }

    [Fact]
    public void Equality_ShouldBeTrue_ForSameEmailAddress()
    {
        // Arrange
        var email1 = new EmailAddress("user@example.com");
        var email2 = new EmailAddress("user@example.com");

        // Act & Assert
        email1.Should().Be(email2);
        (email1 == email2).Should().BeTrue();
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    [Fact]
    public void Equality_ShouldBeTrue_ForSameEmailWithDifferentCasing()
    {
        // Arrange
        var email1 = new EmailAddress("User@Example.COM");
        var email2 = new EmailAddress("user@example.com");

        // Act & Assert
        email1.Should().Be(email2, "emails are normalized to lowercase");
    }

    [Fact]
    public void Equality_ShouldBeFalse_ForDifferentEmailAddresses()
    {
        // Arrange
        var email1 = new EmailAddress("user1@example.com");
        var email2 = new EmailAddress("user2@example.com");

        // Act & Assert
        email1.Should().NotBe(email2);
        (email1 != email2).Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldThrowDomainException_WhenEmailHasMultipleAtSymbols()
    {
        // Arrange
        var email = "user@@example.com";

        // Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("EMAIL_INVALID_FORMAT");
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name+tag@example.co.uk")]
    [InlineData("valid_email123@subdomain.example.org")]
    public void Value_ShouldReturnNormalizedEmail_AfterConstruction(string originalEmail)
    {
        // Arrange & Act
        var emailAddress = new EmailAddress(originalEmail);

        // Assert
        emailAddress.Value.Should().Be(originalEmail.ToLowerInvariant().Trim());
    }

    [Fact]
    public void EmailAddress_ShouldBeImmutable_AfterConstruction()
    {
        // Arrange
        var emailAddress = new EmailAddress("user@example.com");

        // Act & Assert
        emailAddress.Value.Should().Be("user@example.com");
        emailAddress.LocalPart.Should().Be("user");
        emailAddress.Domain.Should().Be("example.com");

        // Cannot modify properties (compile-time guarantee via init-only setters)
        // This test documents the immutability design
    }

    [Fact]
    public void Constructor_ShouldAcceptMaxLengthEmail_At254Characters()
    {
        // Arrange - Create a valid email exactly 254 characters long
        // Format: localpart@domain.tld
        var localPart = new string('a', 64); // Max local part
        var domainPart = new string('b', 183); // 254 - 64 - 1(@) - 6(.c.com) = 183
        var email = $"{localPart}@{domainPart}.c.com";

        // Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ShouldAcceptMaxLocalPartLength_At64Characters()
    {
        // Arrange
        var localPart = new string('a', 64);
        var email = $"{localPart}@example.com";

        // Act & Assert
        var act = () => new EmailAddress(email);
        act.Should().NotThrow();
    }
}
