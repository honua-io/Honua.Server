# Writing Tests Guide

**Copyright (c) 2025 HonuaIO**
**Licensed under the Elastic License 2.0**

**Last Updated:** 2025-11-11
**Version:** 2.0

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Step-by-Step Guide](#step-by-step-guide)
3. [Choosing the Right Test Type](#choosing-the-right-test-type)
4. [Using Test Base Classes](#using-test-base-classes)
5. [Working with TestContainers](#working-with-testcontainers)
6. [Configuration V2 Patterns](#configuration-v2-patterns)
7. [Common Test Patterns](#common-test-patterns)
8. [Best Practices](#best-practices)
9. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
10. [Examples](#examples)

---

## Quick Start

### Create a Unit Test

```csharp
// tests/Honua.Server.Core.Tests.MyFeature/MyServiceTests.cs

using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.MyFeature;

public class MyServiceTests
{
    [Fact]
    public void MyMethod_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var mockDep = new Mock<IDependency>();
        mockDep.Setup(x => x.DoSomething()).Returns(42);
        var service = new MyService(mockDep.Object);

        // Act
        var result = service.MyMethod();

        // Assert
        result.Should().Be(42);
    }
}
```

### Create an Integration Test

```csharp
// tests/Honua.Server.Integration.Tests/MyIntegrationTests.cs

using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Xunit;

namespace Honua.Server.Integration.Tests;

[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class MyIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public MyIntegrationTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task TestWithRealDatabase()
    {
        // Arrange
        var connectionString = _db.PostgresConnectionString;

        // Act & Assert
        // Test with real database
    }
}
```

### Create a Configuration V2 Test

```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
{
    builder
        .AddDataSource("db", "postgresql")
        .AddService("wfs", new() { ["version"] = "2.0.0" })
        .AddLayer("test", "db", "test_table");
});

var client = factory.CreateClient();
var response = await client.GetAsync("/wfs?request=GetCapabilities");
```

---

## Step-by-Step Guide

### Step 1: Determine Test Type

Ask yourself:

| Question | Answer → Test Type |
|----------|-------------------|
| Testing a single class/method in isolation? | **Unit Test** |
| Testing interaction between components? | **Integration Test** |
| Testing through HTTP API? | **API Integration Test** |
| Testing Configuration V2 (HCL)? | **Configuration V2 Test** |
| Testing complete user workflow? | **E2E Test** |

### Step 2: Choose Test Project

```
Unit Tests → Honua.Server.Core.Tests.{Component}
Integration Tests → Honua.Server.Integration.Tests
E2E Tests → Honua.Server.Deployment.E2ETests
```

**Examples:**
- Security unit tests → `Honua.Server.Core.Tests.Security`
- API integration tests → `Honua.Server.Integration.Tests`
- Data operation tests → `Honua.Server.Core.Tests.DataOperations`

### Step 3: Create Test File

**File naming:**
```
{FeatureName}Tests.cs
{FeatureName}ConfigV2Tests.cs  (for Configuration V2 tests)
```

**File location:**
```csharp
// Unit test
tests/Honua.Server.Core.Tests.Security/Authentication/PasswordHasherTests.cs

// Integration test
tests/Honua.Server.Integration.Tests/Ogc/WfsTests.cs

// Configuration V2 test
tests/Honua.Server.Integration.Tests/ConfigurationV2/WfsConfigV2Tests.cs
```

### Step 4: Add Copyright Header

```csharp
// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Xunit;

namespace Honua.Server.Core.Tests.MyFeature;
```

### Step 5: Add Class Declaration

**Unit Test:**
```csharp
public class MyServiceTests
{
}
```

**Integration Test:**
```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class MyIntegrationTests : IClassFixture<DatabaseFixture>
{
}
```

**API Test:**
```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "WFS")]
public class WfsApiTests : IClassFixture<DatabaseFixture>
{
}
```

### Step 6: Add Constructor (if needed)

**Unit Test with Mocks:**
```csharp
public class MyServiceTests
{
    private readonly Mock<IDependency> _mockDep;
    private readonly MyService _service;

    public MyServiceTests()
    {
        _mockDep = new Mock<IDependency>();
        _service = new MyService(_mockDep.Object);
    }
}
```

**Integration Test:**
```csharp
public class MyIntegrationTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public MyIntegrationTests(DatabaseFixture db)
    {
        _db = db;
    }
}
```

### Step 7: Write Test Methods

**Test method naming:**
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange

    // Act

    // Assert
}
```

**Examples:**
```csharp
GetCapabilities_WFS20_ReturnsValidXml()
AuthenticateUser_WithInvalidPassword_ReturnsFalse()
SaveFeature_WhenDatabaseUnavailable_ThrowsException()
```

### Step 8: Write Assertions

**Use FluentAssertions:**
```csharp
// Good
result.Should().Be(42);
user.Should().NotBeNull();
response.StatusCode.Should().Be(HttpStatusCode.OK);

// Avoid
Assert.Equal(42, result);
Assert.NotNull(user);
Assert.True(response.StatusCode == HttpStatusCode.OK);
```

---

## Choosing the Right Test Type

### Decision Tree

```
┌─ Does it use external dependencies (DB, HTTP, files)?
│  ├─ No → Unit Test
│  └─ Yes → Can dependencies be easily mocked?
│           ├─ Yes → Unit Test with Mocks
│           └─ No → Integration Test
│
├─ Does it test HTTP endpoints?
│  └─ Yes → API Integration Test
│
├─ Does it test Configuration V2 (HCL)?
│  └─ Yes → Configuration V2 Test
│
└─ Does it test complete user workflow?
   └─ Yes → E2E Test
```

### Test Type Characteristics

| Type | Speed | Dependencies | Isolation | When to Use |
|------|-------|--------------|-----------|-------------|
| **Unit** | Fast (< 100ms) | Mocked | High | Single class/method logic |
| **Integration** | Medium (100ms-2s) | Real DB | Medium | Component interactions |
| **API** | Medium (100ms-2s) | Real DB + HTTP | Medium | API contract testing |
| **Config V2** | Medium (100ms-2s) | Real DB + HTTP | Medium | HCL configuration testing |
| **E2E** | Slow (2s-10s) | Full stack | Low | Complete workflows |

### Examples by Type

**Unit Test Example:**
```csharp
// Testing password hashing in isolation
[Fact]
public void HashPassword_GeneratesValidHash()
{
    var hasher = new PasswordHasher();
    var hash = hasher.HashPassword("password123");
    hash.Should().NotBeNull();
    hash.Length.Should().BeGreaterThan(0);
}
```

**Integration Test Example:**
```csharp
// Testing repository with real database
[Fact]
public async Task SaveUser_StoresInDatabase()
{
    var repo = new UserRepository(_db.PostgresConnectionString);
    var user = new User { Name = "Test" };

    await repo.SaveAsync(user);

    var retrieved = await repo.GetAsync(user.Id);
    retrieved.Should().NotBeNull();
    retrieved.Name.Should().Be("Test");
}
```

**API Test Example:**
```csharp
// Testing WFS GetCapabilities endpoint
[Fact]
public async Task GetCapabilities_ReturnsValidXml()
{
    using var factory = new WebApplicationFactoryFixture<Program>(_db);
    var client = factory.CreateClient();

    var response = await client.GetAsync("/wfs?request=GetCapabilities");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var xml = await response.Content.ReadAsStringAsync();
    xml.Should().Contain("WFS_Capabilities");
}
```

---

## Using Test Base Classes

### Pattern 1: No Base Class (Simple Unit Tests)

**When:** Testing pure logic with no setup needed

```csharp
public class CalculatorTests
{
    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 1, 0)]
    public void Add_ReturnsCorrectSum(int a, int b, int expected)
    {
        var calc = new Calculator();
        var result = calc.Add(a, b);
        result.Should().Be(expected);
    }
}
```

### Pattern 2: Constructor Setup (Unit Tests with Mocks)

**When:** Multiple tests share same mocks/setup

```csharp
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockRepo;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _mockRepo = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<UserService>>();
        _service = new UserService(_mockRepo.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetUser_CallsRepository()
    {
        // _mockRepo and _service already configured
        _mockRepo.Setup(x => x.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(new User());

        var result = await _service.GetUserAsync("user1");

        _mockRepo.Verify(x => x.GetAsync("user1"), Times.Once);
    }
}
```

### Pattern 3: IClassFixture (Integration Tests)

**When:** Tests need shared expensive resources (database containers)

```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class FeatureRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public FeatureRepositoryTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetFeatures_ReturnsData()
    {
        var repo = new FeatureRepository(_db.PostgresConnectionString);
        var features = await repo.GetAllAsync();
        features.Should().NotBeNull();
    }
}
```

### Pattern 4: WebApplicationFactory (API Tests)

**When:** Testing HTTP endpoints

```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
public class WfsEndpointTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsEndpointTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetCapabilities_Works()
    {
        using var factory = new WebApplicationFactoryFixture<Program>(_db);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/wfs?request=GetCapabilities");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

### Pattern 5: ConfigurationV2TestFixture (Config V2 Tests)

**When:** Testing declarative HCL configuration

```csharp
[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]
public class WfsConfigV2Tests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsConfigV2Tests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task WfsConfig_LoadsCorrectly()
    {
        using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
        {
            builder
                .AddDataSource("db", "postgresql")
                .AddService("wfs", new() { ["max_features"] = 1000 });
        });

        factory.LoadedConfig!.Services["wfs"].Enabled.Should().BeTrue();
    }
}
```

---

## Working with TestContainers

### Accessing Containers

**PostgreSQL:**
```csharp
[Fact]
public async Task TestPostgres()
{
    if (!_db.IsPostgresReady)
    {
        // Skip or throw
        return;
    }

    var connString = _db.PostgresConnectionString;
    using var conn = new NpgsqlConnection(connString);
    await conn.OpenAsync();

    // Test database operations
}
```

**MySQL:**
```csharp
[Fact]
public async Task TestMySQL()
{
    if (!_db.IsMySqlReady)
    {
        return;
    }

    var connString = _db.MySqlConnectionString;
    using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
}
```

**Redis:**
```csharp
[Fact]
public async Task TestRedis()
{
    if (!_db.IsRedisReady)
    {
        return;
    }

    var connString = _db.RedisConnectionString;
    var redis = ConnectionMultiplexer.Connect(connString);
    var db = redis.GetDatabase();

    await db.StringSetAsync("key", "value");
    var value = await db.StringGetAsync("key");
    value.Should().Be("value");
}
```

### Container Lifecycle

**Containers are:**
- Started once per test run
- Shared across all tests in `DatabaseCollection`
- Automatically cleaned up after tests complete

**Don't:**
- Stop/start containers in tests
- Recreate containers
- Manually dispose containers

**Do:**
- Check `IsPostgresReady`, `IsMySqlReady`, `IsRedisReady`
- Clean up test data
- Use transactions for isolation

### Test Data Isolation

**Use unique identifiers:**
```csharp
[Fact]
public async Task Test1()
{
    var testId = Guid.NewGuid();
    var tableName = $"test_{testId:N}";

    await CreateTable(tableName);
    try
    {
        // Test logic
    }
    finally
    {
        await DropTable(tableName);
    }
}
```

**Use transactions:**
```csharp
[Fact]
public async Task Test2()
{
    using var conn = new NpgsqlConnection(_db.PostgresConnectionString);
    await conn.OpenAsync();

    using var tx = await conn.BeginTransactionAsync();
    try
    {
        // All operations in transaction
        // Automatic rollback on dispose
    }
    finally
    {
        await tx.RollbackAsync();
    }
}
```

---

## Configuration V2 Patterns

### Builder Pattern

**Advantages:**
- Type-safe
- IntelliSense support
- Easy to read

```csharp
using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
{
    builder
        .AddDataSource("spatial_db", "postgresql")
        .AddService("wfs", new()
        {
            ["version"] = "2.0.0",
            ["max_features"] = 10000
        })
        .AddLayer("parcels", "spatial_db", "parcels", "geom", "Polygon", 4326)
        .AddRedisCache("cache", "REDIS_URL");
});
```

### Inline HCL Pattern

**Advantages:**
- Full HCL syntax
- Test complex configurations
- Mirrors production configs

```csharp
var hcl = @"
honua {
  version = ""1.0""
  environment = ""test""
}

data_source ""main_db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")

  pool {
    min_size = 2
    max_size = 10
  }
}

service ""ogc_api"" {
  enabled = true
  item_limit = 1000
}

layer ""features"" {
  title = ""Test Features""
  data_source = data_source.main_db
  table = ""features""
  id_field = ""id""

  geometry {
    column = ""geom""
    type = ""Point""
    srid = 4326
  }

  services = [service.ogc_api]
}
";

using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);
```

### Verifying Configuration

**Check parsed configuration:**
```csharp
[Fact]
public async Task Config_ParsedCorrectly()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder.AddDataSource("db", "postgresql");
    });

    // Verify configuration loaded
    factory.LoadedConfig.Should().NotBeNull();
    factory.LoadedConfig!.DataSources.Should().ContainKey("db");
    factory.LoadedConfig.DataSources["db"].Provider.Should().Be("postgresql");
}
```

**Check service configuration:**
```csharp
[Fact]
public async Task WfsService_ConfiguredWithCorrectSettings()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder.AddService("wfs", new()
        {
            ["max_features"] = 5000,
            ["default_count"] = 100
        });
    });

    var wfs = factory.LoadedConfig!.Services["wfs"];
    wfs.Enabled.Should().BeTrue();
    wfs.Settings["max_features"].Should().Be(5000);
    wfs.Settings["default_count"].Should().Be(100);
}
```

**Test API behavior:**
```csharp
[Fact]
public async Task ConfiguredService_RespondsCorrectly()
{
    using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
    {
        builder
            .AddDataSource("db", "postgresql")
            .AddService("ogc_api", new() { ["item_limit"] = 50 })
            .AddLayer("test", "db", "test_table");
    });

    var client = factory.CreateClient();
    var response = await client.GetAsync("/ogc/features/collections");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

---

## Common Test Patterns

### Testing Async Methods

```csharp
[Fact]
public async Task AsyncMethod_ReturnsExpectedValue()
{
    // Arrange
    var service = new MyService();

    // Act
    var result = await service.GetDataAsync();

    // Assert
    result.Should().NotBeNull();
}
```

### Testing Exceptions

```csharp
[Fact]
public async Task Method_WhenInvalidInput_ThrowsException()
{
    // Arrange
    var service = new MyService();

    // Act & Assert
    await service.Invoking(s => s.ProcessAsync(null))
        .Should().ThrowAsync<ArgumentNullException>();
}
```

### Parameterized Tests

```csharp
[Theory]
[InlineData("valid@email.com", true)]
[InlineData("invalid", false)]
[InlineData(null, false)]
public void ValidateEmail_ReturnsExpectedResult(string email, bool expected)
{
    var validator = new EmailValidator();
    var result = validator.IsValid(email);
    result.Should().Be(expected);
}
```

### Testing with Mock Data

```csharp
[Fact]
public async Task GetUser_ReturnsUserFromRepository()
{
    // Arrange
    var expectedUser = new User { Id = "1", Name = "Test" };
    _mockRepo.Setup(x => x.GetAsync("1"))
        .ReturnsAsync(expectedUser);

    // Act
    var result = await _service.GetUserAsync("1");

    // Assert
    result.Should().BeEquivalentTo(expectedUser);
    _mockRepo.Verify(x => x.GetAsync("1"), Times.Once);
}
```

### Testing HTTP Responses

```csharp
[Fact]
public async Task GetCapabilities_ReturnsCorrectContentType()
{
    using var factory = new WebApplicationFactoryFixture<Program>(_db);
    var client = factory.CreateClient();

    var response = await client.GetAsync("/wfs?request=GetCapabilities");

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    response.Content.Headers.ContentType!.MediaType
        .Should().Be("application/xml");
}
```

### Testing Collections

```csharp
[Fact]
public void GetItems_ReturnsExpectedCollection()
{
    var service = new ItemService();

    var items = service.GetItems();

    items.Should().NotBeNull();
    items.Should().HaveCount(3);
    items.Should().Contain(x => x.Name == "Item1");
    items.Should().AllSatisfy(x => x.IsActive.Should().BeTrue());
}
```

---

## Best Practices

### 1. Follow AAA Pattern

```csharp
[Fact]
public async Task Test_Example()
{
    // Arrange - Setup test data and dependencies
    var input = new Request { Value = 42 };
    var service = new MyService();

    // Act - Execute the method under test
    var result = await service.ProcessAsync(input);

    // Assert - Verify the results
    result.Should().NotBeNull();
    result.Value.Should().Be(42);
}
```

### 2. One Assertion Concept Per Test

```csharp
// Good - Tests one concept
[Fact]
public void User_WhenCreated_HasValidId()
{
    var user = new User("test");
    user.Id.Should().NotBeNullOrEmpty();
}

[Fact]
public void User_WhenCreated_IsActive()
{
    var user = new User("test");
    user.IsActive.Should().BeTrue();
}

// Avoid - Tests multiple unrelated concepts
[Fact]
public void User_WhenCreated_IsValid()
{
    var user = new User("test");
    user.Id.Should().NotBeNullOrEmpty();  // Concept 1
    user.IsActive.Should().BeTrue();      // Concept 2
    user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow);  // Concept 3
}
```

### 3. Use Descriptive Test Names

```csharp
// Good
GetCapabilities_WhenServiceDisabled_ReturnsNotFound()
AuthenticateUser_WithExpiredPassword_RequiresPasswordChange()
SaveFeature_WhenGeometryInvalid_ThrowsValidationException()

// Avoid
Test1()
TestWfs()
AuthTest()
```

### 4. Test the Behavior, Not Implementation

```csharp
// Good - Tests behavior
[Fact]
public async Task SaveUser_PersistsToDatabase()
{
    await _service.SaveUserAsync(user);

    var saved = await _repo.GetAsync(user.Id);
    saved.Should().NotBeNull();
}

// Avoid - Tests implementation details
[Fact]
public async Task SaveUser_CallsRepositorySaveMethod()
{
    await _service.SaveUserAsync(user);

    _mockRepo.Verify(x => x.SaveAsync(It.IsAny<User>()), Times.Once);
}
```

### 5. Keep Tests Independent

```csharp
// Good - Each test is independent
[Fact]
public async Task Test1()
{
    var id = Guid.NewGuid();
    await CreateData(id);
    try
    {
        // Test
    }
    finally
    {
        await DeleteData(id);
    }
}

[Fact]
public async Task Test2()
{
    var id = Guid.NewGuid();  // Different ID
    await CreateData(id);
    try
    {
        // Test
    }
    finally
    {
        await DeleteData(id);
    }
}

// Avoid - Tests depend on execution order
private static int _testCounter = 0;

[Fact]
public void Test1()
{
    _testCounter++;
    _testCounter.Should().Be(1);
}

[Fact]
public void Test2()
{
    _testCounter++;  // Depends on Test1 running first!
    _testCounter.Should().Be(2);
}
```

### 6. Use Traits for Categorization

```csharp
[Trait("Category", "Integration")]
[Trait("API", "WFS")]
[Trait("Component", "OgcProtocols")]
public class WfsIntegrationTests
{
}
```

Run specific categories:
```bash
dotnet test --filter "Category=Integration"
dotnet test --filter "API=WFS"
```

### 7. Clean Up Resources

```csharp
[Fact]
public async Task Test_WithCleanup()
{
    var tempFile = Path.GetTempFileName();
    try
    {
        // Test logic
        File.WriteAllText(tempFile, "data");
        // ...
    }
    finally
    {
        if (File.Exists(tempFile))
        {
            File.Delete(tempFile);
        }
    }
}
```

### 8. Use FluentAssertions

```csharp
// Good - Readable and descriptive
result.Should().NotBeNull();
result.Value.Should().Be(42);
users.Should().HaveCount(3);
exception.Should().BeOfType<ArgumentNullException>();

// Avoid - Less readable
Assert.NotNull(result);
Assert.Equal(42, result.Value);
Assert.Equal(3, users.Count);
Assert.IsType<ArgumentNullException>(exception);
```

---

## Anti-Patterns to Avoid

### 1. Don't Test Framework/Library Code

```csharp
// Bad - Testing LINQ (already tested by Microsoft)
[Fact]
public void List_Where_FiltersCorrectly()
{
    var list = new List<int> { 1, 2, 3, 4 };
    var result = list.Where(x => x > 2).ToList();
    result.Should().HaveCount(2);
}

// Good - Test YOUR business logic
[Fact]
public void GetActiveUsers_ReturnsOnlyActiveUsers()
{
    var users = _service.GetActiveUsers();
    users.Should().AllSatisfy(u => u.IsActive.Should().BeTrue());
}
```

### 2. Don't Create Multiple Containers

```csharp
// Bad - Creates new container per test class
public class MyTests : IClassFixture<DatabaseFixture>  // NO!
{
    private readonly DatabaseFixture _db;

    public MyTests()
    {
        _db = new DatabaseFixture();  // Creates new container!
    }
}

// Good - Uses shared container
[Collection("DatabaseCollection")]
public class MyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public MyTests(DatabaseFixture db)  // Injected by xUnit
    {
        _db = db;
    }
}
```

### 3. Don't Use Thread.Sleep

```csharp
// Bad
[Fact]
public async Task Test()
{
    await service.StartAsync();
    Thread.Sleep(1000);  // Wait for async operation
    var result = await service.GetStatusAsync();
}

// Good - Use proper async/await
[Fact]
public async Task Test()
{
    await service.StartAsync();
    await service.WaitForCompletionAsync();  // Proper async wait
    var result = await service.GetStatusAsync();
}
```

### 4. Don't Test Private Methods

```csharp
// Bad
[Fact]
public void TestPrivateMethod()
{
    var service = new MyService();
    var method = typeof(MyService).GetMethod("PrivateMethod",
        BindingFlags.NonPublic | BindingFlags.Instance);
    var result = method.Invoke(service, null);
    // ...
}

// Good - Test public interface
[Fact]
public void TestPublicBehavior()
{
    var service = new MyService();
    var result = service.PublicMethod();  // Indirectly tests private methods
    result.Should().NotBeNull();
}
```

### 5. Don't Ignore Test Failures

```csharp
// Bad
[Fact(Skip = "Broken, will fix later")]
public async Task ImportantTest()
{
    // Critical functionality
}

// Good - Fix immediately or create issue
[Fact]
public async Task ImportantTest()
{
    // Working test
}
```

### 6. Don't Make Tests Dependent on External State

```csharp
// Bad - Depends on specific data existing
[Fact]
public async Task GetUser_ReturnsUser()
{
    var user = await _repo.GetAsync("admin");  // Assumes "admin" exists!
    user.Should().NotBeNull();
}

// Good - Create test data
[Fact]
public async Task GetUser_ReturnsUser()
{
    var testId = await CreateTestUser("testuser");
    try
    {
        var user = await _repo.GetAsync(testId);
        user.Should().NotBeNull();
    }
    finally
    {
        await DeleteTestUser(testId);
    }
}
```

### 7. Don't Use Production Configuration

```csharp
// Bad - Uses production database!
var connectionString = "Server=prod-db;Database=production;";

// Good - Uses test container
var connectionString = _db.PostgresConnectionString;
```

---

## Examples

### Complete Unit Test Example

```csharp
// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security;

public class PasswordValidatorTests
{
    private readonly PasswordValidator _validator;

    public PasswordValidatorTests()
    {
        _validator = new PasswordValidator();
    }

    [Theory]
    [InlineData("Str0ng!Pass", true)]
    [InlineData("weak", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validate_WithVariousPasswords_ReturnsExpectedResult(
        string password, bool expectedValid)
    {
        // Act
        var result = _validator.Validate(password);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void Validate_WithWeakPassword_ReturnsValidationErrors()
    {
        // Arrange
        var password = "weak";

        // Act
        var result = _validator.Validate(password);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("length"));
    }
}
```

### Complete Integration Test Example

```csharp
// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Xunit;

namespace Honua.Server.Integration.Tests.Ogc;

[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "WFS")]
public class WfsGetCapabilitiesTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public WfsGetCapabilitiesTests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task GetCapabilities_WFS20_ReturnsValidXml()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_db);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            "/wfs?service=WFS&version=2.0.0&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("WFS_Capabilities");
        content.Should().Contain("ows:ServiceIdentification");
    }

    [Fact]
    public async Task GetCapabilities_InvalidVersion_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new WebApplicationFactoryFixture<Program>(_db);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync(
            "/wfs?service=WFS&version=99.99.99&request=GetCapabilities");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

### Complete Configuration V2 Test Example

```csharp
// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Xunit;

namespace Honua.Server.Integration.Tests.ConfigurationV2;

[Collection("DatabaseCollection")]
[Trait("Category", "Integration")]
[Trait("API", "ConfigurationV2")]
[Trait("Endpoint", "OgcApi")]
public class OgcApiConfigV2Tests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public OgcApiConfigV2Tests(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public async Task OgcApi_WithMultipleLayers_ReturnsAllCollections()
    {
        // Arrange
        using var factory = new ConfigurationV2TestFixture<Program>(_db, builder =>
        {
            builder
                .AddDataSource("spatial_db", "postgresql")
                .AddService("ogc_api", new() { ["item_limit"] = 1000 })
                .AddLayer("parcels", "spatial_db", "parcels", "geom", "Polygon")
                .AddLayer("roads", "spatial_db", "roads", "geom", "LineString")
                .AddLayer("buildings", "spatial_db", "buildings", "geom", "Polygon");
        });

        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/features/collections");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify configuration
        factory.LoadedConfig.Should().NotBeNull();
        factory.LoadedConfig!.Layers.Should().HaveCount(3);
        factory.LoadedConfig.Services["ogc_api"].Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task OgcApi_WithCustomSettings_RespectsConfiguration()
    {
        // Arrange
        var hcl = @"
honua {
  version = ""1.0""
  environment = ""test""
}

data_source ""db"" {
  provider = ""postgresql""
  connection = env(""DATABASE_URL"")
}

service ""ogc_api"" {
  enabled = true
  item_limit = 50
  max_limit = 500
}

layer ""test"" {
  title = ""Test Layer""
  data_source = data_source.db
  table = ""test_table""
  id_field = ""id""

  geometry {
    column = ""geom""
    type = ""Point""
    srid = 4326
  }

  services = [service.ogc_api]
}
";

        using var factory = new ConfigurationV2TestFixture<Program>(_db, hcl);
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/ogc/features/collections/test/items?limit=100");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify service settings were applied
        var ogcApi = factory.LoadedConfig!.Services["ogc_api"];
        ogcApi.Settings["item_limit"].Should().Be(50);
        ogcApi.Settings["max_limit"].Should().Be(500);
    }
}
```

---

## Quick Reference

### Test Method Template

```csharp
[Fact]  // or [Theory] with [InlineData]
public async Task MethodName_Scenario_ExpectedBehavior()
{
    // Arrange
    var input = ...;
    var expected = ...;

    // Act
    var result = await service.MethodAsync(input);

    // Assert
    result.Should().Be(expected);
}
```

### Common Imports

```csharp
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Moq;
using System.Net;
using Xunit;
```

### Fixture Injection

```csharp
[Collection("DatabaseCollection")]
public class MyTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _db;

    public MyTests(DatabaseFixture db)
    {
        _db = db;
    }
}
```

---

**Next Steps:**
- Review [TEST_INFRASTRUCTURE.md](./TEST_INFRASTRUCTURE.md) for infrastructure details
- Check [CONFIGURATION_V2_MIGRATION_GUIDE.md](./CONFIGURATION_V2_MIGRATION_GUIDE.md) for migration guidance
- See [TEST_PERFORMANCE.md](./TEST_PERFORMANCE.md) for optimization tips

**Last Updated:** 2025-11-11
**Maintained by:** Honua.Server Team
