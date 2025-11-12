// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Utilities;
using Xunit;

namespace Honua.Server.Core.Tests.Utilities;

public class JsonHelperTests
{
    [Fact]
    public void Serialize_WithSimpleObject_ReturnsJson()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 123 };

        // Act
        var json = JsonHelper.Serialize(obj);

        // Assert
        json.Should().Contain("Name");
        json.Should().Contain("Test");
        json.Should().Contain("123");
    }

    [Fact]
    public void Deserialize_WithValidJson_ReturnsObject()
    {
        // Arrange
        var json = """{"Name":"Test","Value":123}""";

        // Act
        var result = JsonHelper.Deserialize<TestObject>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(123);
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ReturnsNull()
    {
        // Arrange
        var json = "invalid json";

        // Act
        var result = JsonHelper.Deserialize<TestObject>(json);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_WithValidJson_ReturnsTrue()
    {
        // Arrange
        var json = """{"Name":"Test","Value":456}""";

        // Act
        var success = JsonHelper.TryDeserialize<TestObject>(json, out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(456);
    }

    [Fact]
    public void TryDeserialize_WithInvalidJson_ReturnsFalse()
    {
        // Arrange
        var json = "not valid json";

        // Act
        var success = JsonHelper.TryDeserialize<TestObject>(json, out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void SerializeIndented_ReturnsFormattedJson()
    {
        // Arrange
        var obj = new { Name = "Test", Value = 789 };

        // Act
        var json = JsonHelper.SerializeIndented(obj);

        // Assert
        json.Should().Contain("\n"); // Should have line breaks
        json.Should().Contain("Name");
        json.Should().Contain("Test");
    }

    private class TestObject
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
