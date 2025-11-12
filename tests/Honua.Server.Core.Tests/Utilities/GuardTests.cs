// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.Utilities;
using Xunit;

namespace Honua.Server.Core.Tests.Utilities;

public class GuardTests
{
    [Fact]
    public void NotNull_WhenValueIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        string? value = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Guard.NotNull(value));
    }

    [Fact]
    public void NotNull_WhenValueIsNotNull_DoesNotThrow()
    {
        // Arrange
        var value = "test";

        // Act & Assert
        var exception = Record.Exception(() =>
            Guard.NotNull(value));

        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NotNullOrWhiteSpace_WithInvalidValue_ThrowsException(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Guard.NotNullOrWhiteSpace(value));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("test string")]
    public void NotNullOrWhiteSpace_WithValidValue_DoesNotThrow(string value)
    {
        // Act & Assert
        var exception = Record.Exception(() =>
            Guard.NotNullOrWhiteSpace(value));

        exception.Should().BeNull();
    }

    [Fact]
    public void ThrowIfNegative_WithNegativeValue_ThrowsException()
    {
        // Arrange
        var value = -1;

        // Act & Assert
        // Using built-in .NET ArgumentOutOfRangeException.ThrowIfNegative
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(value)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void ThrowIfNegative_WithNonNegativeValue_DoesNotThrow(int value)
    {
        // Act & Assert
        // Using built-in .NET ArgumentOutOfRangeException.ThrowIfNegative
        var exception = Record.Exception(() =>
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(value)));

        exception.Should().BeNull();
    }

    [Fact]
    public void ThrowIfNegativeOrZero_WithZero_ThrowsException()
    {
        // Arrange
        var value = 0;

        // Act & Assert
        // Using built-in .NET ArgumentOutOfRangeException.ThrowIfNegativeOrZero
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(value)));
    }

    [Fact]
    public void ThrowIfNegativeOrZero_WithNegative_ThrowsException()
    {
        // Arrange
        var value = -10;

        // Act & Assert
        // Using built-in .NET ArgumentOutOfRangeException.ThrowIfNegativeOrZero
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(value)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void ThrowIfNegativeOrZero_WithPositiveValue_DoesNotThrow(int value)
    {
        // Act & Assert
        // Using built-in .NET ArgumentOutOfRangeException.ThrowIfNegativeOrZero
        var exception = Record.Exception(() =>
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, nameof(value)));

        exception.Should().BeNull();
    }
}
