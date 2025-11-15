# Honua.Server.Domain.Tests

Comprehensive unit tests for the DDD (Domain-Driven Design) foundation infrastructure implemented in Phase 1.1.

## Overview

This test project provides extensive coverage for all core domain building blocks:

- **Entity Base Class**: Identity-based equality testing
- **Value Object Base Class**: Structural equality testing
- **Aggregate Root**: Domain event management testing
- **Domain Event Dispatcher**: Event handling and dispatching
- **Value Objects**: Email, Username, Permission, and strongly-typed IDs
- **Domain Exception**: Error handling and context management

## Test Coverage

### 1. Entity<TId> Tests (`EntityTests.cs`)
**24 test cases** covering:
- Identity-based equality (same ID = equal)
- Different IDs = not equal
- Null handling in comparisons
- Equality operators (==, !=)
- GetHashCode consistency
- Different types with same ID value
- Support for various ID types (Guid, string, int)

### 2. ValueObject Tests (`ValueObjectTests.cs`)
**21 test cases** covering:
- Structural equality (same values = equal)
- Different values = not equal
- Null handling in equality components
- Equality operators (==, !=)
- GetHashCode for collections
- Complex value objects with multiple components
- Copy functionality

### 3. AggregateRoot<TId> Tests (`AggregateRootTests.cs`)
**16 test cases** covering:
- RaiseDomainEvent adds events to collection
- ClearDomainEvents empties collection
- DomainEvents is read-only
- Event ordering preservation
- Inherits Entity equality behavior
- Null event validation
- Version property support

### 4. DomainEventDispatcher Tests (`DomainEventDispatcherTests.cs`)
**16 test cases** covering:
- DispatchAsync calls all registered handlers
- DispatchManyAsync handles multiple events
- No handlers registered = no error
- Multiple handlers for same event type
- Exception handling and propagation
- Parallel handler execution
- Mixed event type dispatching
- Cancellation token support

### 5. EmailAddress Tests (`EmailAddressTests.cs`)
**27 test cases** covering:
- Valid email format validation
- Invalid email rejection
- Email normalization (lowercase, trim)
- Length constraints (254 total, 64 local part)
- TryCreate pattern
- LocalPart and Domain extraction
- Equality and immutability

### 6. Username Tests (`UsernameTests.cs`)
**29 test cases** covering:
- Valid username validation
- Invalid format rejection
- Length constraints (3-30 characters)
- Special character rules
- Consecutive special character prevention
- Case preservation
- TryCreate pattern
- Equality and immutability

### 7. Permission Tests (`PermissionTests.cs`)
**38 test cases** covering:
- Permission creation and parsing
- Matches() logic with scopes
- Implies() with wildcards
- Factory methods (Read, Write, Delete, etc.)
- String formatting
- Normalization
- Equality and immutability

### 8. Strongly-Typed ID Tests (`StronglyTypedIdTests.cs`)
**51 test cases** (17 per ID type × 3 types) covering:
- **ShareTokenId, MapId, UserId** each tested for:
  - Valid GUID construction
  - Empty GUID rejection
  - New() unique ID generation
  - Parse() and TryParse() methods
  - ToString() conversion
  - Implicit GUID conversion
  - Equality operators
  - Type safety (preventing ID mixing)
  - Immutability

### 9. DomainException Tests (`DomainExceptionTests.cs`)
**22 test cases** covering:
- All constructor overloads
- Error code preservation
- Context dictionary support
- Inner exception handling
- Inheritance from Exception
- Read-only properties
- Multiple context value types
- Custom derived exceptions

## Total Test Count

**244 comprehensive unit tests** across 9 test files

## Running the Tests

### All Tests
```bash
dotnet test tests/Honua.Server.Domain.Tests/
```

### Specific Test Class
```bash
dotnet test tests/Honua.Server.Domain.Tests/ --filter ClassName~EntityTests
```

### With Coverage
```bash
dotnet test tests/Honua.Server.Domain.Tests/ /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Patterns Used

### AAA Pattern (Arrange-Act-Assert)
All tests follow the AAA pattern for clarity and consistency:

```csharp
[Fact]
public void Method_Scenario_ExpectedResult()
{
    // Arrange
    var input = "test";

    // Act
    var result = Method(input);

    // Assert
    result.Should().Be(expected);
}
```

### Test Naming Convention
Tests use descriptive names following the pattern:
```
MethodName_Scenario_ExpectedResult
```

Examples:
- `Constructor_ShouldSetId_WhenCalled`
- `Equals_ShouldReturnTrue_WhenEntitiesHaveSameId`
- `Parse_ShouldThrowDomainException_WhenFormatIsInvalid`

### Test Categories
All tests are tagged with `[Trait("Category", "Unit")]` for easy filtering.

## Frameworks and Tools

- **xUnit**: Test framework
- **FluentAssertions**: Fluent assertion library for readable tests
- **Moq**: Mocking framework for DomainEventDispatcher tests
- **Microsoft.Extensions.DependencyInjection**: DI container for service provider tests

## Code Coverage Goals

These tests aim for **80%+ code coverage** of the domain infrastructure:

- Entity<TId>: ~95% coverage
- ValueObject: ~95% coverage
- AggregateRoot<TId>: ~100% coverage
- DomainEventDispatcher: ~90% coverage
- Value Objects: ~95% coverage each
- DomainException: ~100% coverage

## Key Testing Strategies

### 1. Positive and Negative Cases
Every validation rule is tested for both valid and invalid inputs.

### 2. Boundary Testing
Edge cases like minimum/maximum lengths, empty/null values, etc.

### 3. Equality Testing
Comprehensive testing of equality operators, GetHashCode(), and Equals() methods.

### 4. Immutability Verification
Tests document and verify that domain objects are immutable after construction.

### 5. Type Safety
Strongly-typed ID tests verify compile-time type safety prevents ID mixing.

## Test Fixtures and Helpers

### Test Entity Implementations
```csharp
private class TestEntity : Entity<Guid>
{
    public TestEntity(Guid id) : base(id) { }
}
```

### Test Value Object Implementations
```csharp
private class Address : ValueObject
{
    public string Street { get; }
    public string City { get; }
    // ...
}
```

### Test Domain Events
```csharp
private record TestDomainEvent : DomainEvent;
```

## Integration with CI/CD

These tests are designed to run in CI/CD pipelines:

```yaml
- name: Run Domain Tests
  run: dotnet test tests/Honua.Server.Domain.Tests/ --logger "trx;LogFileName=domain-tests.xml"
```

## Future Enhancements

Potential areas for test expansion:
1. Performance tests for large collections
2. Concurrency tests for AggregateRoot events
3. Additional domain event scenarios
4. More complex value object compositions
5. Custom exception types derived from DomainException

## Contributing

When adding new domain infrastructure:
1. Add corresponding unit tests following existing patterns
2. Maintain AAA pattern and naming conventions
3. Aim for 80%+ code coverage
4. Use FluentAssertions for readable assertions
5. Add test categories with `[Trait("Category", "Unit")]`
6. Test both positive and negative scenarios
