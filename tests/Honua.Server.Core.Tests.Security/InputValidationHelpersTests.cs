// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.Security;
using Xunit;

namespace Honua.Server.Core.Tests.Security;

public class InputValidationHelpersTests
{
    #region IsAlphanumeric Tests

    [Theory]
    [InlineData("abc123")]
    [InlineData("test_user")]
    [InlineData("data-source")]
    [InlineData("MyTable123")]
    [InlineData("user_name-123")]
    [InlineData("a")]
    [InlineData("1")]
    [InlineData("_")]
    [InlineData("-")]
    public void IsAlphanumeric_WithValidInput_ReturnsTrue(string input)
    {
        // Act
        var result = InputValidationHelpers.IsAlphanumeric(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void IsAlphanumeric_WithNullOrWhitespace_ReturnsFalse(string? input)
    {
        // Act
        var result = InputValidationHelpers.IsAlphanumeric(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("user@domain")]
    [InlineData("user name")]
    [InlineData("user.name")]
    [InlineData("user/name")]
    [InlineData("user\\name")]
    [InlineData("user;name")]
    [InlineData("user'name")]
    [InlineData("user\"name")]
    [InlineData("user<name>")]
    [InlineData("user%name")]
    [InlineData("user&name")]
    public void IsAlphanumeric_WithInvalidCharacters_ReturnsFalse(string input)
    {
        // Act
        var result = InputValidationHelpers.IsAlphanumeric(input);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidEmail Tests

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("test.user@example.com")]
    [InlineData("user+tag@example.co.uk")]
    [InlineData("admin@localhost.local")]
    [InlineData("user123@test-domain.com")]
    [InlineData("first.last@company.io")]
    public void IsValidEmail_WithValidEmail_ReturnsTrue(string email)
    {
        // Act
        var result = InputValidationHelpers.IsValidEmail(email);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsValidEmail_WithNullOrWhitespace_ReturnsFalse(string? email)
    {
        // Act
        var result = InputValidationHelpers.IsValidEmail(email);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user @example.com")]
    [InlineData("user@ example.com")]
    [InlineData("user@example .com")]
    [InlineData("user@@example.com")]
    [InlineData("user@example")]
    public void IsValidEmail_WithInvalidEmail_ReturnsFalse(string email)
    {
        // Act
        var result = InputValidationHelpers.IsValidEmail(email);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidUuid Tests

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("6ba7b810-9dad-11d1-80b4-00c04fd430c8")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("123e4567-e89b-12d3-a456-426614174000")]
    [InlineData("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE")]
    public void IsValidUuid_WithValidUuid_ReturnsTrue(string uuid)
    {
        // Act
        var result = InputValidationHelpers.IsValidUuid(uuid);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsValidUuid_WithNullOrWhitespace_ReturnsFalse(string? uuid)
    {
        // Act
        var result = InputValidationHelpers.IsValidUuid(uuid);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-a-uuid")]
    [InlineData("550e8400-e29b-41d4-a716")]
    [InlineData("550e8400e29b41d4a716446655440000")]
    [InlineData("550e8400-e29b-41d4-a716-446655440000-extra")]
    [InlineData("ZZZZZZZZ-ZZZZ-ZZZZ-ZZZZ-ZZZZZZZZZZZZ")]
    public void IsValidUuid_WithInvalidUuid_ReturnsFalse(string uuid)
    {
        // Act
        var result = InputValidationHelpers.IsValidUuid(uuid);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidUuid_WithGuidParseableString_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid().ToString();

        // Act
        var result = InputValidationHelpers.IsValidUuid(guid);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsValidLength Tests

    [Theory]
    [InlineData("a", 1, 10)]
    [InlineData("test", 1, 10)]
    [InlineData("1234567890", 1, 10)]
    [InlineData("hello", 5, 5)]
    public void IsValidLength_WithValidLength_ReturnsTrue(string input, int minLength, int maxLength)
    {
        // Act
        var result = InputValidationHelpers.IsValidLength(input, minLength, maxLength);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidLength_WithNullInput_ReturnsFalse()
    {
        // Act
        var result = InputValidationHelpers.IsValidLength(null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("", 1, 10)]
    [InlineData("a", 2, 10)]
    [InlineData("12345678901", 1, 10)]
    public void IsValidLength_WithInvalidLength_ReturnsFalse(string input, int minLength, int maxLength)
    {
        // Act
        var result = InputValidationHelpers.IsValidLength(input, minLength, maxLength);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidLength_WithDefaultParameters_UsesDefaults()
    {
        // Arrange
        var validInput = "test string";
        var tooLongInput = new string('a', 1001);

        // Act
        var validResult = InputValidationHelpers.IsValidLength(validInput);
        var invalidResult = InputValidationHelpers.IsValidLength(tooLongInput);

        // Assert
        validResult.Should().BeTrue();
        invalidResult.Should().BeFalse();
    }

    #endregion

    #region ContainsSqlInjectionPatterns Tests

    [Theory]
    [InlineData("SELECT * FROM users; DROP TABLE users;")]
    [InlineData("admin' OR '1'='1")]
    [InlineData("user' OR 1=1--")]
    [InlineData("data\"; DROP TABLE data;--")]
    [InlineData("test' UNION SELECT * FROM passwords")]
    [InlineData("input'; EXEC sp_executesql")]
    [InlineData("value\"; EXEC xp_cmdshell")]
    [InlineData("test\" OR \"1\"=\"1")]
    [InlineData("value\" OR 1=1")]
    public void ContainsSqlInjectionPatterns_WithSqlInjection_ReturnsTrue(string input)
    {
        // Act
        var result = InputValidationHelpers.ContainsSqlInjectionPatterns(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("normal text")]
    [InlineData("user@example.com")]
    [InlineData("My name is O'Brien")]
    [InlineData("The union of two sets")]
    [InlineData("SELECT is a valid word")]
    [InlineData("DROP is okay in normal text")]
    [InlineData("123-456-789")]
    public void ContainsSqlInjectionPatterns_WithSafeInput_ReturnsFalse(string input)
    {
        // Act
        var result = InputValidationHelpers.ContainsSqlInjectionPatterns(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ContainsSqlInjectionPatterns_WithNullOrWhitespace_ReturnsFalse(string? input)
    {
        // Act
        var result = InputValidationHelpers.ContainsSqlInjectionPatterns(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("UNION SELECT")]
    [InlineData("uNiOn SeLeCt")]
    [InlineData("'; DROP")]
    [InlineData("'; drop")]
    public void ContainsSqlInjectionPatterns_IsCaseInsensitive(string input)
    {
        // Act
        var result = InputValidationHelpers.ContainsSqlInjectionPatterns(input);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region ContainsXssPatterns Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<SCRIPT>alert('XSS')</SCRIPT>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("<div onload=alert('xss')>")]
    [InlineData("<body onclick=alert('xss')>")]
    [InlineData("<input onmouseover=alert('xss')>")]
    [InlineData("<iframe src='javascript:alert(1)'>")]
    [InlineData("<object data='data:text/html,<script>alert(1)</script>'>")]
    [InlineData("<embed src='javascript:alert(1)'>")]
    [InlineData("eval(document.cookie)")]
    [InlineData("expression(alert('xss'))")]
    public void ContainsXssPatterns_WithXssAttack_ReturnsTrue(string input)
    {
        // Act
        var result = InputValidationHelpers.ContainsXssPatterns(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("normal text")]
    [InlineData("This is a description")]
    [InlineData("JavaScript is a programming language")]
    [InlineData("Scripts are useful")]
    [InlineData("Evaluate your options")]
    [InlineData("Express your thoughts")]
    [InlineData("123-456-789")]
    public void ContainsXssPatterns_WithSafeInput_ReturnsFalse(string input)
    {
        // Act
        var result = InputValidationHelpers.ContainsXssPatterns(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ContainsXssPatterns_WithNullOrWhitespace_ReturnsFalse(string? input)
    {
        // Act
        var result = InputValidationHelpers.ContainsXssPatterns(input);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("<SCRIPT>")]
    [InlineData("<script>")]
    [InlineData("<ScRiPt>")]
    [InlineData("JAVASCRIPT:")]
    [InlineData("javascript:")]
    [InlineData("JavaScript:")]
    public void ContainsXssPatterns_IsCaseInsensitive(string input)
    {
        // Act
        var result = InputValidationHelpers.ContainsXssPatterns(input);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsSafeInput Tests

    [Theory]
    [InlineData("normal text")]
    [InlineData("user@example.com")]
    [InlineData("123-456-789")]
    [InlineData("This is a valid description")]
    [InlineData("Data source name")]
    public void IsSafeInput_WithSafeInput_ReturnsTrue(string input)
    {
        // Act
        var result = InputValidationHelpers.IsSafeInput(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsSafeInput_WithNullOrWhitespace_ReturnsTrue(string? input)
    {
        // Act
        var result = InputValidationHelpers.IsSafeInput(input);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("admin' OR '1'='1")]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("'; DROP TABLE users;--")]
    [InlineData("javascript:alert(1)")]
    [InlineData("UNION SELECT password FROM users")]
    [InlineData("<img onerror=alert('xss')>")]
    public void IsSafeInput_WithDangerousInput_ReturnsFalse(string input)
    {
        // Act
        var result = InputValidationHelpers.IsSafeInput(input);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SanitizeLikePattern Tests

    [Fact]
    public void SanitizeLikePattern_WithPercentSign_EscapesPercent()
    {
        // Arrange
        var input = "test%value";

        // Act
        var result = InputValidationHelpers.SanitizeLikePattern(input);

        // Assert
        result.Should().Be("test[%]value");
    }

    [Fact]
    public void SanitizeLikePattern_WithUnderscore_EscapesUnderscore()
    {
        // Arrange
        var input = "test_value";

        // Act
        var result = InputValidationHelpers.SanitizeLikePattern(input);

        // Assert
        result.Should().Be("test[_]value");
    }

    [Fact]
    public void SanitizeLikePattern_WithSquareBrackets_EscapesBrackets()
    {
        // Arrange
        var input = "test[value]";

        // Act
        var result = InputValidationHelpers.SanitizeLikePattern(input);

        // Assert
        result.Should().Be("test[[]value]");
    }

    [Fact]
    public void SanitizeLikePattern_WithMultipleSpecialChars_EscapesAll()
    {
        // Arrange
        var input = "test%_[data]";

        // Act
        var result = InputValidationHelpers.SanitizeLikePattern(input);

        // Assert
        result.Should().Be("test[%][_][[]data]");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void SanitizeLikePattern_WithNullOrEmpty_ReturnsEmpty(string? input)
    {
        // Act
        var result = InputValidationHelpers.SanitizeLikePattern(input!);

        // Assert
        result.Should().Be(string.Empty);
    }

    [Fact]
    public void SanitizeLikePattern_WithNormalText_ReturnsUnchanged()
    {
        // Arrange
        var input = "normal text";

        // Act
        var result = InputValidationHelpers.SanitizeLikePattern(input);

        // Assert
        result.Should().Be("normal text");
    }

    #endregion

    #region IsValidResourceId Tests

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("123")]
    [InlineData("9876543210")]
    [InlineData("resource_123")]
    [InlineData("data-source-1")]
    public void IsValidResourceId_WithValidId_ReturnsTrue(string resourceId)
    {
        // Act
        var result = InputValidationHelpers.IsValidResourceId(resourceId);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsValidResourceId_WithNullOrWhitespace_ReturnsFalse(string? resourceId)
    {
        // Act
        var result = InputValidationHelpers.IsValidResourceId(resourceId);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("resource@123")]
    [InlineData("resource.id")]
    [InlineData("resource/123")]
    [InlineData("resource\\id")]
    [InlineData("resource;123")]
    public void IsValidResourceId_WithInvalidCharacters_ReturnsFalse(string resourceId)
    {
        // Act
        var result = InputValidationHelpers.IsValidResourceId(resourceId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsValidPageSize Tests

    [Theory]
    [InlineData(1, 100)]
    [InlineData(50, 100)]
    [InlineData(100, 100)]
    [InlineData(10, 50)]
    public void IsValidPageSize_WithValidSize_ReturnsTrue(int pageSize, int maxPageSize)
    {
        // Act
        var result = InputValidationHelpers.IsValidPageSize(pageSize, maxPageSize);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(-100, 100)]
    public void IsValidPageSize_WithZeroOrNegative_ReturnsFalse(int pageSize, int maxPageSize)
    {
        // Act
        var result = InputValidationHelpers.IsValidPageSize(pageSize, maxPageSize);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(101, 100)]
    [InlineData(200, 100)]
    [InlineData(1000, 100)]
    public void IsValidPageSize_WithTooLarge_ReturnsFalse(int pageSize, int maxPageSize)
    {
        // Act
        var result = InputValidationHelpers.IsValidPageSize(pageSize, maxPageSize);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidPageSize_WithDefaultMaxPageSize_Uses100()
    {
        // Act
        var validResult = InputValidationHelpers.IsValidPageSize(100);
        var invalidResult = InputValidationHelpers.IsValidPageSize(101);

        // Assert
        validResult.Should().BeTrue();
        invalidResult.Should().BeFalse();
    }

    #endregion

    #region IsValidPageNumber Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(999999)]
    public void IsValidPageNumber_WithValidNumber_ReturnsTrue(int pageNumber)
    {
        // Act
        var result = InputValidationHelpers.IsValidPageNumber(pageNumber);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(-999999)]
    public void IsValidPageNumber_WithNegativeNumber_ReturnsFalse(int pageNumber)
    {
        // Act
        var result = InputValidationHelpers.IsValidPageNumber(pageNumber);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ThrowIfSqlInjection Tests

    [Fact]
    public void ThrowIfSqlInjection_WithSafeInput_DoesNotThrow()
    {
        // Arrange
        var safeInput = "normal text";

        // Act
        var exception = Record.Exception(() =>
            InputValidationHelpers.ThrowIfSqlInjection(safeInput, "testParam"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ThrowIfSqlInjection_WithSqlInjection_ThrowsArgumentException()
    {
        // Arrange
        var maliciousInput = "admin' OR '1'='1";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            InputValidationHelpers.ThrowIfSqlInjection(maliciousInput, "testParam"));

        exception.Message.Should().Contain("SQL patterns");
        exception.ParamName.Should().Be("testParam");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ThrowIfSqlInjection_WithNullOrWhitespace_DoesNotThrow(string? input)
    {
        // Act
        var exception = Record.Exception(() =>
            InputValidationHelpers.ThrowIfSqlInjection(input, "testParam"));

        // Assert
        exception.Should().BeNull();
    }

    #endregion

    #region ThrowIfXss Tests

    [Fact]
    public void ThrowIfXss_WithSafeInput_DoesNotThrow()
    {
        // Arrange
        var safeInput = "normal text";

        // Act
        var exception = Record.Exception(() =>
            InputValidationHelpers.ThrowIfXss(safeInput, "testParam"));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public void ThrowIfXss_WithXssPattern_ThrowsArgumentException()
    {
        // Arrange
        var maliciousInput = "<script>alert('xss')</script>";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            InputValidationHelpers.ThrowIfXss(maliciousInput, "testParam"));

        exception.Message.Should().Contain("XSS patterns");
        exception.ParamName.Should().Be("testParam");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ThrowIfXss_WithNullOrWhitespace_DoesNotThrow(string? input)
    {
        // Act
        var exception = Record.Exception(() =>
            InputValidationHelpers.ThrowIfXss(input, "testParam"));

        // Assert
        exception.Should().BeNull();
    }

    #endregion

    #region ThrowIfUnsafeInput Tests

    [Fact]
    public void ThrowIfUnsafeInput_WithSafeInput_DoesNotThrow()
    {
        // Arrange
        var safeInput = "normal text";

        // Act
        var exception = Record.Exception(() =>
            InputValidationHelpers.ThrowIfUnsafeInput(safeInput, "testParam"));

        // Assert
        exception.Should().BeNull();
    }

    [Theory]
    [InlineData("admin' OR '1'='1")]
    [InlineData("<script>alert('xss')</script>")]
    public void ThrowIfUnsafeInput_WithDangerousInput_ThrowsArgumentException(string maliciousInput)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            InputValidationHelpers.ThrowIfUnsafeInput(maliciousInput, "testParam"));

        exception.Message.Should().Contain("dangerous patterns");
        exception.ParamName.Should().Be("testParam");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ThrowIfUnsafeInput_WithNullOrWhitespace_DoesNotThrow(string? input)
    {
        // Act
        var exception = Record.Exception(() =>
            InputValidationHelpers.ThrowIfUnsafeInput(input, "testParam"));

        // Assert
        exception.Should().BeNull();
    }

    #endregion
}
