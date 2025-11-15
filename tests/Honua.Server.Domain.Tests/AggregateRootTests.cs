// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the AggregateRoot<TId> base class.
/// Tests domain event management and entity behavior inheritance.
/// </summary>
[Trait("Category", "Unit")]
public class AggregateRootTests
{
    // Test aggregate root implementation for testing purposes
    private class TestAggregate : AggregateRoot<Guid>
    {
        public TestAggregate(Guid id) : base(id) { }

        public void DoSomething()
        {
            RaiseDomainEvent(new TestDomainEvent());
        }

        public void DoMultipleThings()
        {
            RaiseDomainEvent(new TestDomainEvent());
            RaiseDomainEvent(new AnotherTestDomainEvent());
            RaiseDomainEvent(new TestDomainEvent());
        }

        public void RaiseNullEvent()
        {
            RaiseDomainEvent(null!);
        }
    }

    private class TestDomainEvent : DomainEvent
    {
    }

    private class AnotherTestDomainEvent : DomainEvent
    {
    }

    [Fact]
    public void Constructor_ShouldInitializeWithEmptyDomainEvents_WhenCalled()
    {
        // Arrange & Act
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddEventToCollection_WhenCalled()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        aggregate.DoSomething();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(1);
        aggregate.DomainEvents.First().Should().BeOfType<TestDomainEvent>();
    }

    [Fact]
    public void RaiseDomainEvent_ShouldAddMultipleEvents_WhenCalledMultipleTimes()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        aggregate.DoMultipleThings();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(3);
        aggregate.DomainEvents.Should().ContainItemsAssignableTo<IDomainEvent>();
    }

    [Fact]
    public void RaiseDomainEvent_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        Action act = () => aggregate.RaiseNullEvent();

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents_WhenCalled()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DoMultipleThings();

        // Act
        aggregate.ClearDomainEvents();

        // Assert
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void ClearDomainEvents_ShouldDoNothing_WhenNoEventsExist()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        Action act = () => aggregate.ClearDomainEvents();

        // Assert
        act.Should().NotThrow();
        aggregate.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_ShouldBeReadOnly_WhenAccessed()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.DoSomething();

        // Act
        var events = aggregate.DomainEvents;

        // Assert
        events.Should().BeAssignableTo<IReadOnlyCollection<IDomainEvent>>();
    }

    [Fact]
    public void DomainEvents_ShouldMaintainOrder_WhenEventsAreRaised()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        aggregate.DoMultipleThings();

        // Assert
        aggregate.DomainEvents.Should().HaveCount(3);
        aggregate.DomainEvents.ElementAt(0).Should().BeOfType<TestDomainEvent>();
        aggregate.DomainEvents.ElementAt(1).Should().BeOfType<AnotherTestDomainEvent>();
        aggregate.DomainEvents.ElementAt(2).Should().BeOfType<TestDomainEvent>();
    }

    [Fact]
    public void AggregateRoot_ShouldInheritEntityBehavior_ForEquality()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate1 = new TestAggregate(id);
        var aggregate2 = new TestAggregate(id);

        // Act
        var areEqual = aggregate1.Equals(aggregate2);

        // Assert
        areEqual.Should().BeTrue("aggregates with same ID should be equal");
    }

    [Fact]
    public void AggregateRoot_ShouldInheritEntityBehavior_ForHashCode()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate1 = new TestAggregate(id);
        var aggregate2 = new TestAggregate(id);

        // Act
        var hash1 = aggregate1.GetHashCode();
        var hash2 = aggregate2.GetHashCode();

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void AggregateRoot_ShouldInheritEntityBehavior_ForEqualityOperator()
    {
        // Arrange
        var id = Guid.NewGuid();
        var aggregate1 = new TestAggregate(id);
        var aggregate2 = new TestAggregate(id);

        // Act
        var areEqual = aggregate1 == aggregate2;

        // Assert
        areEqual.Should().BeTrue();
    }

    [Fact]
    public void Version_ShouldBeNull_ByDefault()
    {
        // Arrange & Act
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Assert
        aggregate.Version.Should().BeNull();
    }

    [Fact]
    public void Version_CanBeOverridden_InDerivedClass()
    {
        // Arrange
        var aggregate = new VersionedAggregate(Guid.NewGuid());

        // Act
        aggregate.UpdateVersion();

        // Assert
        aggregate.Version.Should().NotBeNull();
        aggregate.Version.Should().HaveCount(8);
    }

    [Fact]
    public void DomainEvents_ShouldPersistAfterMultipleOperations_UntilCleared()
    {
        // Arrange
        var aggregate = new TestAggregate(Guid.NewGuid());

        // Act
        aggregate.DoSomething();
        var countAfterFirst = aggregate.DomainEvents.Count;

        aggregate.DoSomething();
        var countAfterSecond = aggregate.DomainEvents.Count;

        aggregate.ClearDomainEvents();
        var countAfterClear = aggregate.DomainEvents.Count;

        // Assert
        countAfterFirst.Should().Be(1);
        countAfterSecond.Should().Be(2);
        countAfterClear.Should().Be(0);
    }

    // Helper class for testing version property
    private class VersionedAggregate : AggregateRoot<Guid>
    {
        public VersionedAggregate(Guid id) : base(id) { }

        public override byte[]? Version { get; protected set; }

        public void UpdateVersion()
        {
            Version = BitConverter.GetBytes(DateTimeOffset.UtcNow.Ticks);
        }
    }
}
