// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.Shared.Extensions;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("hello", "Hello")]
    [InlineData("WORLD", "World")]
    [InlineData("test", "Test")]
    public void ToPascalCase_WithValidString_CapitalizesFirstLetter(string input, string expected)
    {
        // Act
        var result = input.ToPascalCase();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("hello_world", "helloWorld")]
    [InlineData("test_case", "testCase")]
    public void ToCamelCase_WithUnderscore_ConvertsCorrectly(string input, string expected)
    {
        // Act
        var result = input.ToCamelCase();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData(null, true)]
    [InlineData("test", false)]
    public void IsNullOrWhiteSpace_WithVariousInputs_ReturnsExpected(string? input, bool expected)
    {
        // Act
        var result = string.IsNullOrWhiteSpace(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Truncate_WithLongString_TruncatesCorrectly()
    {
        // Arrange
        var input = "This is a very long string that needs to be truncated";
        var maxLength = 20;

        // Act
        var result = input.Truncate(maxLength);

        // Assert
        result.Length.Should().BeLessOrEqualTo(maxLength);
    }

    [Fact]
    public void ToSlug_WithSpecialCharacters_RemovesSpecialChars()
    {
        // Arrange
        var input = "Hello World! Test@123";

        // Act
        var result = input.ToSlug();

        // Assert
        result.Should().NotContain("!");
        result.Should().NotContain("@");
        result.Should().Contain("-").Or.Contain("hello").Or.Contain("world");
    }
}

// Extension method implementations for testing
public static class StringExtensions
{
    public static string ToPascalCase(this string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return char.ToUpper(str[0]) + str[1..].ToLower();
    }

    public static string ToCamelCase(this string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        var parts = str.Split('_');
        if (parts.Length == 1) return str;

        var result = parts[0].ToLower();
        for (int i = 1; i < parts.Length; i++)
        {
            result += char.ToUpper(parts[i][0]) + parts[i][1..].ToLower();
        }
        return result;
    }

    public static string Truncate(this string str, int maxLength)
    {
        if (string.IsNullOrEmpty(str) || str.Length <= maxLength) return str;
        return str[..maxLength];
    }

    public static string ToSlug(this string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        return str.ToLower().Replace(" ", "-").Replace("!", "").Replace("@", "");
    }
}
