// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the ValueObject base class.
/// Tests structural equality and related operations.
/// </summary>
[Trait("Category", "Unit")]
public class ValueObjectTests
{
    // Test value object implementations for testing purposes
    private class Address : ValueObject
    {
        public string Street { get; }
        public string City { get; }
        public string ZipCode { get; }

        public Address(string street, string city, string zipCode)
        {
            Street = street;
            City = city;
            ZipCode = zipCode;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Street;
            yield return City;
            yield return ZipCode;
        }
    }

    private class Person : ValueObject
    {
        public string Name { get; }
        public int Age { get; }

        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Name;
            yield return Age;
        }
    }

    private class ComplexValueObject : ValueObject
    {
        public string Value1 { get; }
        public int Value2 { get; }
        public List<string> Collection { get; }

        public ComplexValueObject(string value1, int value2, List<string> collection)
        {
            Value1 = value1;
            Value2 = value2;
            Collection = collection;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Value1;
            yield return Value2;
            foreach (var item in Collection)
            {
                yield return item;
            }
        }
    }

    private class NullableValueObject : ValueObject
    {
        public string? NullableValue { get; }
        public string NonNullableValue { get; }

        public NullableValueObject(string? nullableValue, string nonNullableValue)
        {
            NullableValue = nullableValue;
            NonNullableValue = nonNullableValue;
        }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return NullableValue;
            yield return NonNullableValue;
        }
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenValueObjectsHaveSameValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("123 Main St", "New York", "10001");

        // Act
        var result = address1.Equals(address2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenValueObjectsHaveDifferentValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("456 Oak Ave", "New York", "10001");

        // Act
        var result = address1.Equals(address2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenOtherIsNull()
    {
        // Arrange
        var address = new Address("123 Main St", "New York", "10001");

        // Act
        var result = address.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingToSelf()
    {
        // Arrange
        var address = new Address("123 Main St", "New York", "10001");

        // Act
        var result = address.Equals(address);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenValueObjectsAreDifferentTypes()
    {
        // Arrange
        var address = new Address("123 Main St", "New York", "10001");
        var person = new Person("John Doe", 30);

        // Act
        var result = address.Equals(person);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_ShouldReturnTrue_WhenValueObjectsHaveSameValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        object address2 = new Address("123 Main St", "New York", "10001");

        // Act
        var result = address1.Equals(address2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_ShouldReturnFalse_WhenObjectIsNotValueObject()
    {
        // Arrange
        var address = new Address("123 Main St", "New York", "10001");
        object other = "not a value object";

        // Act
        var result = address.Equals(other);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnTrue_WhenValueObjectsHaveSameValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("123 Main St", "New York", "10001");

        // Act
        var result = address1 == address2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnFalse_WhenValueObjectsHaveDifferentValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("456 Oak Ave", "New York", "10001");

        // Act
        var result = address1 == address2;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnTrue_WhenBothAreNull()
    {
        // Arrange
        Address? address1 = null;
        Address? address2 = null;

        // Act
        var result = address1 == address2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnFalse_WhenOneIsNull()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        Address? address2 = null;

        // Act
        var result1 = address1 == address2;
        var result2 = address2 == address1;

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_ShouldReturnFalse_WhenValueObjectsHaveSameValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("123 Main St", "New York", "10001");

        // Act
        var result = address1 != address2;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_ShouldReturnTrue_WhenValueObjectsHaveDifferentValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("456 Oak Ave", "New York", "10001");

        // Act
        var result = address1 != address2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent_WhenCalledMultipleTimes()
    {
        // Arrange
        var address = new Address("123 Main St", "New York", "10001");

        // Act
        var hash1 = address.GetHashCode();
        var hash2 = address.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForValueObjectsWithSameValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("123 Main St", "New York", "10001");

        // Act
        var hash1 = address1.GetHashCode();
        var hash2 = address2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForValueObjectsWithDifferentValues()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("456 Oak Ave", "New York", "10001");

        // Act
        var hash1 = address1.GetHashCode();
        var hash2 = address2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GetHashCode_ShouldHandleCollections_Correctly()
    {
        // Arrange
        var list1 = new List<string> { "a", "b", "c" };
        var list2 = new List<string> { "a", "b", "c" };
        var list3 = new List<string> { "x", "y", "z" };

        var obj1 = new ComplexValueObject("test", 123, list1);
        var obj2 = new ComplexValueObject("test", 123, list2);
        var obj3 = new ComplexValueObject("test", 123, list3);

        // Act
        var hash1 = obj1.GetHashCode();
        var hash2 = obj2.GetHashCode();
        var hash3 = obj3.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().NotBe(hash3);
    }

    [Fact]
    public void Equals_ShouldHandleNullValues_InComponents()
    {
        // Arrange
        var obj1 = new NullableValueObject(null, "test");
        var obj2 = new NullableValueObject(null, "test");
        var obj3 = new NullableValueObject("value", "test");

        // Act & Assert
        obj1.Should().Be(obj2);
        obj1.Should().NotBe(obj3);
    }

    [Fact]
    public void GetHashCode_ShouldHandleNullValues_InComponents()
    {
        // Arrange
        var obj1 = new NullableValueObject(null, "test");
        var obj2 = new NullableValueObject(null, "test");

        // Act
        var hash1 = obj1.GetHashCode();
        var hash2 = obj2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Equals_ShouldConsiderAllComponents_InComparison()
    {
        // Arrange
        var address1 = new Address("123 Main St", "New York", "10001");
        var address2 = new Address("123 Main St", "New York", "10002"); // Different zip

        // Act
        var result = address1.Equals(address2);

        // Assert
        result.Should().BeFalse("all components should be considered in equality comparison");
    }

    [Fact]
    public void Copy_ShouldCreateNewInstance_WithSameValues()
    {
        // Arrange
        var original = new Address("123 Main St", "New York", "10001");

        // Act
        var copy = original.Copy();

        // Assert
        copy.Should().NotBeSameAs(original, "copy should be a different instance");
        copy.Should().Be(original, "copy should have the same values");
    }
}
