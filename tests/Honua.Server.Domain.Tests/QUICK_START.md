# Quick Start Guide - Domain Tests

## Running Tests

### Quick Commands

```bash
# Run all domain tests
dotnet test tests/Honua.Server.Domain.Tests/

# Run with detailed output
dotnet test tests/Honua.Server.Domain.Tests/ -v detailed

# Run specific test file
dotnet test --filter FullyQualifiedName~EntityTests

# Run tests matching a pattern
dotnet test --filter Name~Equality

# Run with code coverage
dotnet test tests/Honua.Server.Domain.Tests/ /p:CollectCoverage=true
```

## Test File Reference

| What to Test | Test File | Example Test |
|--------------|-----------|--------------|
| Entity equality | `EntityTests.cs` | `Equals_ShouldReturnTrue_WhenEntitiesHaveSameId` |
| Value object equality | `ValueObjectTests.cs` | `Equals_ShouldReturnTrue_WhenValueObjectsHaveSameValues` |
| Domain events | `AggregateRootTests.cs` | `RaiseDomainEvent_ShouldAddEventToCollection_WhenCalled` |
| Event dispatching | `DomainEventDispatcherTests.cs` | `DispatchAsync_ShouldCallAllHandlers_WhenMultipleHandlersAreRegistered` |
| Email validation | `EmailAddressTests.cs` | `Constructor_ShouldNormalizeEmail_ToLowerCase` |
| Username validation | `UsernameTests.cs` | `Constructor_ShouldThrowDomainException_WhenUsernameIsTooShort` |
| Permission logic | `PermissionTests.cs` | `Implies_ShouldReturnTrue_WhenBothAreWildcards` |
| ID types | `StronglyTypedIdTests.cs` | `ShareTokenId_TryParse_ShouldReturnTrue_WhenValidGuidStringProvided` |
| Exception handling | `DomainExceptionTests.cs` | `Constructor_WithAllParameters_ShouldSetAll` |

## Common Test Patterns

### Testing Entity Equality
```csharp
var id = Guid.NewGuid();
var entity1 = new TestEntity(id);
var entity2 = new TestEntity(id);

entity1.Should().Be(entity2);
entity1.GetHashCode().Should().Be(entity2.GetHashCode());
```

### Testing Value Object Equality
```csharp
var vo1 = new EmailAddress("test@example.com");
var vo2 = new EmailAddress("test@example.com");

vo1.Should().Be(vo2);
```

### Testing Domain Events
```csharp
var aggregate = new TestAggregate(Guid.NewGuid());
aggregate.DoSomething(); // Raises event

aggregate.DomainEvents.Should().HaveCount(1);
aggregate.DomainEvents.First().Should().BeOfType<TestDomainEvent>();
```

### Testing Validation
```csharp
var act = () => new EmailAddress("invalid-email");

act.Should().Throw<DomainException>()
    .WithMessage("Email address format is invalid.")
    .Which.ErrorCode.Should().Be("EMAIL_INVALID_FORMAT");
```

### Testing TryParse Pattern
```csharp
var result = EmailAddress.TryCreate("test@example.com", out var email);

result.Should().BeTrue();
email.Should().NotBeNull();
email!.Value.Should().Be("test@example.com");
```

## Adding New Tests

### 1. Choose the Right Test File
- Domain building blocks → `EntityTests.cs`, `ValueObjectTests.cs`, `AggregateRootTests.cs`
- Value objects → Create `[ValueObjectName]Tests.cs`
- Infrastructure → `DomainEventDispatcherTests.cs`, `DomainExceptionTests.cs`

### 2. Follow Naming Convention
```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Test implementation
}
```

### 3. Use AAA Pattern
```csharp
// Arrange - Setup
var input = "test";

// Act - Execute
var result = Method(input);

// Assert - Verify
result.Should().Be(expected);
```

### 4. Add Test Category
```csharp
[Trait("Category", "Unit")]
public class MyNewTests
{
    // Tests here
}
```

## FluentAssertions Cheat Sheet

```csharp
// Equality
result.Should().Be(expected);
result.Should().NotBe(unexpected);

// Nullability
result.Should().BeNull();
result.Should().NotBeNull();

// Booleans
result.Should().BeTrue();
result.Should().BeFalse();

// Exceptions
act.Should().Throw<DomainException>();
act.Should().NotThrow();

// Collections
collection.Should().HaveCount(3);
collection.Should().BeEmpty();
collection.Should().Contain(item);
collection.Should().ContainItemsAssignableTo<IType>();

// Strings
str.Should().Be("exact");
str.Should().StartWith("prefix");
str.Should().EndWith("suffix");
str.Should().Contain("substring");

// Types
obj.Should().BeOfType<SpecificType>();
obj.Should().BeAssignableTo<IInterface>();

// With reasons
result.Should().BeTrue("because we expect a valid result");
```

## Debugging Tests

### Run Single Test in Debug Mode
```bash
dotnet test --filter "FullyQualifiedName~TestName" --logger "console;verbosity=detailed"
```

### View Test Output
```csharp
// Use test output helper
private readonly ITestOutputHelper _output;

public MyTests(ITestOutputHelper output)
{
    _output = output;
}

[Fact]
public void MyTest()
{
    _output.WriteLine("Debug message");
}
```

### Common Issues

**Tests not found?**
```bash
# Rebuild the project
dotnet build tests/Honua.Server.Domain.Tests/
```

**Assertion failures unclear?**
```csharp
// Add because clause for clarity
result.Should().Be(expected, "because this value should match the calculation");
```

## CI/CD Integration

### GitHub Actions
```yaml
- name: Run Domain Tests
  run: dotnet test tests/Honua.Server.Domain.Tests/ --logger "trx;LogFileName=domain-tests.xml"
```

### Azure DevOps
```yaml
- task: DotNetCoreCLI@2
  inputs:
    command: 'test'
    projects: 'tests/Honua.Server.Domain.Tests/*.csproj'
    arguments: '--configuration Release'
```

## Test Coverage

### Generate Coverage Report
```bash
dotnet test /p:CollectCoverage=true \
            /p:CoverletOutputFormat=opencover \
            /p:CoverletOutput=./coverage/
```

### View Coverage
```bash
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator -reports:./coverage/coverage.opencover.xml \
                -targetdir:./coverage/report \
                -reporttypes:Html

# Open report
open ./coverage/report/index.html
```

## Best Practices

✅ **DO**:
- Write descriptive test names
- Test both positive and negative cases
- Test boundary conditions
- Use FluentAssertions for readability
- Follow AAA pattern
- Make tests independent
- Test one thing per test method

❌ **DON'T**:
- Share state between tests
- Test implementation details
- Use magic numbers/strings
- Create overly complex tests
- Test private methods directly
- Ignore test failures

## Need Help?

- **Documentation**: See `README.md` for detailed information
- **Examples**: Look at existing tests in the same file
- **Patterns**: Review `TEST_SUITE_SUMMARY.md` for patterns
- **Coverage**: Check which areas need more tests

---

**Happy Testing!** 🧪
