// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the DomainException class.
/// Tests exception creation, error codes, context, and inheritance.
/// </summary>
[Trait("Category", "Unit")]
public class DomainExceptionTests
{
    [Fact]
    public void Constructor_Default_ShouldCreateException()
    {
        // Arrange & Act
        var exception = new DomainException();

        // Assert
        exception.Should().NotBeNull();
        exception.Message.Should().Be("Exception of type 'Honua.Server.Core.Domain.DomainException' was thrown.");
        exception.ErrorCode.Should().BeNull();
        exception.Context.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessage_ShouldSetMessage()
    {
        // Arrange
        var message = "This is a domain error";

        // Act
        var exception = new DomainException(message);

        // Assert
        exception.Message.Should().Be(message);
        exception.ErrorCode.Should().BeNull();
        exception.Context.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_ShouldSetBoth()
    {
        // Arrange
        var message = "This is a domain error";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new DomainException(message, innerException);

        // Assert
        exception.Message.Should().Be(message);
        exception.InnerException.Should().Be(innerException);
        exception.ErrorCode.Should().BeNull();
        exception.Context.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndErrorCode_ShouldSetBoth()
    {
        // Arrange
        var message = "This is a domain error";
        var errorCode = "DOMAIN_ERROR_001";

        // Act
        var exception = new DomainException(message, errorCode);

        // Assert
        exception.Message.Should().Be(message);
        exception.ErrorCode.Should().Be(errorCode);
        exception.Context.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageErrorCodeAndContext_ShouldSetAll()
    {
        // Arrange
        var message = "This is a domain error";
        var errorCode = "DOMAIN_ERROR_001";
        var context = new Dictionary<string, object>
        {
            { "UserId", "123" },
            { "Action", "Delete" }
        };

        // Act
        var exception = new DomainException(message, errorCode, context);

        // Assert
        exception.Message.Should().Be(message);
        exception.ErrorCode.Should().Be(errorCode);
        exception.Context.Should().NotBeNull();
        exception.Context.Should().BeEquivalentTo(context);
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithAllParameters_ShouldSetAll()
    {
        // Arrange
        var message = "This is a domain error";
        var errorCode = "DOMAIN_ERROR_001";
        var innerException = new InvalidOperationException("Inner error");
        var context = new Dictionary<string, object>
        {
            { "UserId", "123" },
            { "Action", "Delete" }
        };

        // Act
        var exception = new DomainException(message, errorCode, innerException, context);

        // Assert
        exception.Message.Should().Be(message);
        exception.ErrorCode.Should().Be(errorCode);
        exception.InnerException.Should().Be(innerException);
        exception.Context.Should().NotBeNull();
        exception.Context.Should().BeEquivalentTo(context);
    }

    [Fact]
    public void Constructor_WithAllParametersButNullContext_ShouldAllowNullContext()
    {
        // Arrange
        var message = "This is a domain error";
        var errorCode = "DOMAIN_ERROR_001";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new DomainException(message, errorCode, innerException, null);

        // Assert
        exception.Message.Should().Be(message);
        exception.ErrorCode.Should().Be(errorCode);
        exception.InnerException.Should().Be(innerException);
        exception.Context.Should().BeNull();
    }

    [Fact]
    public void ErrorCode_ShouldBeReadOnly_AfterConstruction()
    {
        // Arrange
        var errorCode = "TEST_ERROR";
        var exception = new DomainException("Test", errorCode);

        // Act & Assert
        exception.ErrorCode.Should().Be(errorCode);
        // ErrorCode is a get-only property, so it cannot be modified
        // This test documents the immutability design
    }

    [Fact]
    public void Context_ShouldBeReadOnly_AfterConstruction()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            { "Key1", "Value1" },
            { "Key2", 123 }
        };
        var exception = new DomainException("Test", "ERROR", context);

        // Act & Assert
        exception.Context.Should().NotBeNull();
        exception.Context.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>();
        exception.Context!["Key1"].Should().Be("Value1");
        exception.Context["Key2"].Should().Be(123);
    }

    [Fact]
    public void Context_ShouldContainMultipleTypes_OfValues()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            { "StringValue", "test" },
            { "IntValue", 123 },
            { "BoolValue", true },
            { "DateValue", DateTime.Now },
            { "ObjectValue", new { Id = 1, Name = "Test" } }
        };
        var exception = new DomainException("Test", "ERROR", context);

        // Act & Assert
        exception.Context.Should().NotBeNull();
        exception.Context!["StringValue"].Should().Be("test");
        exception.Context["IntValue"].Should().Be(123);
        exception.Context["BoolValue"].Should().Be(true);
        exception.Context["DateValue"].Should().BeOfType<DateTime>();
        exception.Context["ObjectValue"].Should().NotBeNull();
    }

    [Fact]
    public void DomainException_ShouldInheritFromException()
    {
        // Arrange & Act
        var exception = new DomainException("Test");

        // Assert
        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void DomainException_CanBeThrown_AsException()
    {
        // Arrange
        var errorCode = "TEST_ERROR";
        var message = "Test error message";

        // Act & Assert
        var act = () => throw new DomainException(message, errorCode);
        act.Should().Throw<DomainException>()
            .WithMessage(message)
            .Which.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void DomainException_CanBeCaught_AsDomainException()
    {
        // Arrange
        DomainException? caughtException = null;

        // Act
        try
        {
            throw new DomainException("Test", "ERROR");
        }
        catch (DomainException ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException!.ErrorCode.Should().Be("ERROR");
    }

    [Fact]
    public void DomainException_CanBeCaught_AsGeneralException()
    {
        // Arrange
        Exception? caughtException = null;

        // Act
        try
        {
            throw new DomainException("Test", "ERROR");
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        caughtException.Should().NotBeNull();
        caughtException.Should().BeOfType<DomainException>();
        ((DomainException)caughtException).ErrorCode.Should().Be("ERROR");
    }

    [Fact]
    public void InnerException_ShouldBePreserved_ThroughConstructor()
    {
        // Arrange
        var innerException = new InvalidOperationException("Database error");
        var exception = new DomainException("Domain error", innerException);

        // Act & Assert
        exception.InnerException.Should().Be(innerException);
        exception.InnerException.Message.Should().Be("Database error");
    }

    [Fact]
    public void Context_ShouldBeEmptyDictionary_WhenProvidedAsEmpty()
    {
        // Arrange
        var emptyContext = new Dictionary<string, object>();
        var exception = new DomainException("Test", "ERROR", emptyContext);

        // Act & Assert
        exception.Context.Should().NotBeNull();
        exception.Context.Should().BeEmpty();
    }

    [Fact]
    public void ErrorCode_CanBeNull_WhenNotProvided()
    {
        // Arrange & Act
        var exception1 = new DomainException();
        var exception2 = new DomainException("Message");
        var exception3 = new DomainException("Message", new Exception());

        // Assert
        exception1.ErrorCode.Should().BeNull();
        exception2.ErrorCode.Should().BeNull();
        exception3.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void DomainException_WithContext_ShouldProvideUsefulDebugInfo()
    {
        // Arrange
        var context = new Dictionary<string, object>
        {
            { "UserId", "user-123" },
            { "ResourceId", "resource-456" },
            { "Action", "Delete" },
            { "Timestamp", DateTimeOffset.UtcNow }
        };
        var exception = new DomainException(
            "User not authorized to delete resource",
            "AUTHORIZATION_ERROR",
            context);

        // Act & Assert
        exception.Message.Should().Be("User not authorized to delete resource");
        exception.ErrorCode.Should().Be("AUTHORIZATION_ERROR");
        exception.Context.Should().ContainKey("UserId");
        exception.Context.Should().ContainKey("ResourceId");
        exception.Context.Should().ContainKey("Action");
        exception.Context.Should().ContainKey("Timestamp");
    }

    [Fact]
    public void DomainException_ShouldSupportDerivedClasses()
    {
        // Arrange & Act
        var exception = new CustomDomainException("Custom error", "CUSTOM_001");

        // Assert
        exception.Should().BeAssignableTo<DomainException>();
        exception.ErrorCode.Should().Be("CUSTOM_001");
        exception.CustomProperty.Should().Be("Custom");
    }

    // Helper class to test inheritance
    private class CustomDomainException : DomainException
    {
        public string CustomProperty { get; } = "Custom";

        public CustomDomainException(string message, string errorCode)
            : base(message, errorCode)
        {
        }
    }
}
