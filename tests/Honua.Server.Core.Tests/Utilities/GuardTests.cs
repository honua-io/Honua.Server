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
    public void AgainstNull_WhenValueIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        string? value = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            Guard.AgainstNull(value, nameof(value)));
    }

    [Fact]
    public void AgainstNull_WhenValueIsNotNull_DoesNotThrow()
    {
        // Arrange
        var value = "test";

        // Act & Assert
        var exception = Record.Exception(() =>
            Guard.AgainstNull(value, nameof(value)));

        exception.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AgainstNullOrWhiteSpace_WithInvalidValue_ThrowsException(string? value)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            Guard.AgainstNullOrWhiteSpace(value, nameof(value)));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("test string")]
    public void AgainstNullOrWhiteSpace_WithValidValue_DoesNotThrow(string value)
    {
        // Act & Assert
        var exception = Record.Exception(() =>
            Guard.AgainstNullOrWhiteSpace(value, nameof(value)));

        exception.Should().BeNull();
    }

    [Fact]
    public void AgainstNegative_WithNegativeValue_ThrowsException()
    {
        // Arrange
        var value = -1;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstNegative(value, nameof(value)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public void AgainstNegative_WithNonNegativeValue_DoesNotThrow(int value)
    {
        // Act & Assert
        var exception = Record.Exception(() =>
            Guard.AgainstNegative(value, nameof(value)));

        exception.Should().BeNull();
    }

    [Fact]
    public void AgainstNegativeOrZero_WithZero_ThrowsException()
    {
        // Arrange
        var value = 0;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstNegativeOrZero(value, nameof(value)));
    }

    [Fact]
    public void AgainstNegativeOrZero_WithNegative_ThrowsException()
    {
        // Arrange
        var value = -10;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Guard.AgainstNegativeOrZero(value, nameof(value)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void AgainstNegativeOrZero_WithPositiveValue_DoesNotThrow(int value)
    {
        // Act & Assert
        var exception = Record.Exception(() =>
            Guard.AgainstNegativeOrZero(value, nameof(value)));

        exception.Should().BeNull();
    }
}
