// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the Entity<TId> base class.
/// Tests identity-based equality and related operations.
/// </summary>
[Trait("Category", "Unit")]
public class EntityTests
{
    // Test entity implementations for testing purposes
    private class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) : base(id) { }
    }

    private class AnotherTestEntity : Entity<Guid>
    {
        public AnotherTestEntity(Guid id) : base(id) { }
    }

    private class StringIdEntity : Entity<string>
    {
        public StringIdEntity(string id) : base(id) { }
    }

    [Fact]
    public void Constructor_ShouldSetId_WhenCalled()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var entity = new TestEntity(id);

        // Assert
        entity.Id.Should().Be(id);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenEntitiesHaveSameId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var result = entity1.Equals(entity2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenEntitiesHaveDifferentIds()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act
        var result = entity1.Equals(entity2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenOtherIsNull()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid());

        // Act
        var result = entity.Equals(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Equals_ShouldReturnTrue_WhenComparingToSelf()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid());

        // Act
        var result = entity.Equals(entity);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Equals_ShouldReturnFalse_WhenEntitiesAreDifferentTypes()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new AnotherTestEntity(id);

        // Act
        var result = entity1.Equals(entity2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void EqualsObject_ShouldReturnTrue_WhenEntitiesHaveSameId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        object entity2 = new TestEntity(id);

        // Act
        var result = entity1.Equals(entity2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void EqualsObject_ShouldReturnFalse_WhenObjectIsNotEntity()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid());
        object other = "not an entity";

        // Act
        var result = entity.Equals(other);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnTrue_WhenEntitiesHaveSameId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var result = entity1 == entity2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnFalse_WhenEntitiesHaveDifferentIds()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act
        var result = entity1 == entity2;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnTrue_WhenBothAreNull()
    {
        // Arrange
        TestEntity? entity1 = null;
        TestEntity? entity2 = null;

        // Act
        var result = entity1 == entity2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void OperatorEquals_ShouldReturnFalse_WhenOneIsNull()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        TestEntity? entity2 = null;

        // Act
        var result1 = entity1 == entity2;
        var result2 = entity2 == entity1;

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_ShouldReturnFalse_WhenEntitiesHaveSameId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var result = entity1 != entity2;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void OperatorNotEquals_ShouldReturnTrue_WhenEntitiesHaveDifferentIds()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act
        var result = entity1 != entity2;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_ShouldBeConsistent_WhenCalledMultipleTimes()
    {
        // Arrange
        var entity = new TestEntity(Guid.NewGuid());

        // Act
        var hash1 = entity.GetHashCode();
        var hash2 = entity.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_ShouldBeSame_ForEntitiesWithSameId()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        // Act
        var hash1 = entity1.GetHashCode();
        var hash2 = entity2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void GetHashCode_ShouldBeDifferent_ForEntitiesWithDifferentIds()
    {
        // Arrange
        var entity1 = new TestEntity(Guid.NewGuid());
        var entity2 = new TestEntity(Guid.NewGuid());

        // Act
        var hash1 = entity1.GetHashCode();
        var hash2 = entity2.GetHashCode();

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Entity_SupportsStringIds_WhenUsed()
    {
        // Arrange
        var id = "test-id-123";
        var entity1 = new StringIdEntity(id);
        var entity2 = new StringIdEntity(id);

        // Act & Assert
        entity1.Should().Be(entity2);
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    [Fact]
    public void Entity_SupportsIntIds_WhenUsed()
    {
        // Arrange
        var entity1 = new IntIdEntity(42);
        var entity2 = new IntIdEntity(42);
        var entity3 = new IntIdEntity(99);

        // Act & Assert
        entity1.Should().Be(entity2);
        entity1.Should().NotBe(entity3);
        entity1.GetHashCode().Should().Be(entity2.GetHashCode());
    }

    private class IntIdEntity : Entity<int>
    {
        public IntIdEntity(int id) : base(id) { }
    }
}
