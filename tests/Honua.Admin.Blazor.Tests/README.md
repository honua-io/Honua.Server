# Honua.Admin.Blazor.Tests

Comprehensive test suite for the Honua Admin Blazor UI, covering component tests, API client tests, and integration tests.

## Test Structure

```
Honua.Admin.Blazor.Tests/
├── Components/
│   ├── Pages/              # Page component tests
│   │   └── UserManagementTests.cs
│   └── Shared/             # Shared component tests
│       └── UserDialogTests.cs
├── Services/               # API client tests
│   └── UserApiClientTests.cs
├── Integration/            # Integration tests
│   ├── AuthenticationFlowTests.cs
│   └── ServiceCrudIntegrationTests.cs
├── Infrastructure/         # Test utilities and helpers
│   ├── BunitTestContext.cs
│   ├── MockHttpClientFactory.cs
│   └── TestAuthenticationState.cs
└── README.md
```

## Test Types

### 1. Component Tests (bUnit)

Component tests use [bUnit](https://bunit.dev/) to test Blazor components in isolation.

**Example: Testing a dialog component**

```csharp
public class UserDialogTests : ComponentTestBase
{
    [Fact]
    public void UserDialog_CreateMode_RendersAllFields()
    {
        // Arrange
        var model = new CreateUserRequest();

        // Act
        var cut = Context.RenderComponent<UserDialog>(parameters => parameters
            .Add(p => p.IsVisible, true)
            .Add(p => p.Model, model)
            .Add(p => p.IsEditMode, false));

        // Assert
        cut.Find("input[placeholder='Enter username']").Should().NotBeNull();
    }
}
```

**Key Features:**
- `ComponentTestBase`: Base class that provides a fresh `BunitTestContext` per test
- `Context.RenderComponent<T>()`: Renders a component with parameters
- `cut.Find()` / `cut.FindAll()`: Query rendered markup
- Automatic MudBlazor and JSInterop setup

### 2. API Client Tests

API client tests use mocked HTTP responses to test API integration logic without a real server.

**Example: Testing an API client method**

```csharp
public class UserApiClientTests
{
    [Fact]
    public async Task ListUsersAsync_Success_ReturnsUserList()
    {
        // Arrange
        var expectedUsers = new UserListResponse { /* ... */ };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/users", expectedUsers);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new UserApiClient(httpClient);

        // Act
        var result = await apiClient.ListUsersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().HaveCount(1);
    }
}
```

**Key Features:**
- `MockHttpClientFactory`: Fluent API for mocking HTTP responses
- Supports GET, POST, PUT, DELETE with JSON responses
- Can mock errors and exceptions
- No real HTTP requests

### 3. Integration Tests

Integration tests verify end-to-end workflows against a real or test API server.

**Example: Testing CRUD operations**

```csharp
[Fact(Skip = "Integration test - requires backend API")]
public async Task CreateService_ValidRequest_ReturnsCreatedService()
{
    // Arrange
    using var client = await CreateAuthenticatedClient();
    var createRequest = new CreateServiceRequest { /* ... */ };

    // Act
    var response = await client.PostAsJsonAsync("/admin/metadata/services", createRequest);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

**Note:** Integration tests are skipped by default and require a running backend API.

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run specific test file
```bash
dotnet test --filter FullyQualifiedName~UserDialogTests
```

### Run tests with coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run only component tests
```bash
dotnet test --filter Category=Component
```

### Run integration tests (requires API)
```bash
dotnet test --filter Category=Integration
```

## Test Infrastructure

### BunitTestContext

Base test context for bUnit component tests. Automatically registers:
- MudBlazor services
- Logger services (NullLogger)
- Common UI state services (NavigationState, EditorState, etc.)
- JSInterop in loose mode

```csharp
public class MyComponentTests : ComponentTestBase
{
    // Context is automatically available
}
```

### MockHttpClientFactory

Helper for creating mocked HTTP clients with predefined responses.

**Methods:**
- `MockGetJson<T>(url, data)` - Mock GET request returning JSON
- `MockPostJson<T>(url, data)` - Mock POST request returning JSON
- `MockPutJson<T>(url, data)` - Mock PUT request returning JSON
- `MockDelete(url)` - Mock DELETE request
- `MockError(method, url, statusCode)` - Mock error response
- `MockException(method, url, exception)` - Mock exception

**Example:**
```csharp
var mockFactory = new MockHttpClientFactory()
    .MockGetJson("/api/users", userList)
    .MockPostJson("/api/users", createdUser)
    .MockError(HttpMethod.Get, "/api/error", HttpStatusCode.InternalServerError);

var client = mockFactory.CreateClient();
```

### TestAuthenticationStateProvider

Test implementation of `AuthenticationStateProvider` for simulating authentication states.

**Static Factory Methods:**
- `CreateAdministrator(username)` - Creates admin user
- `CreateDataPublisher(username)` - Creates data publisher user
- `CreateViewer(username)` - Creates viewer user
- `CreateAuthenticatedUser(username, displayName, roles)` - Custom user

**Example:**
```csharp
var authStateProvider = new TestAuthenticationStateProvider(
    TestAuthenticationStateProvider.CreateAdministrator());

Context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);

var cut = Context.RenderComponent<UserManagement>();
// Component sees authenticated admin user
```

## Writing New Tests

### Component Test Template

```csharp
public class MyComponentTests : ComponentTestBase
{
    [Fact]
    public void MyComponent_Scenario_ExpectedBehavior()
    {
        // Arrange
        // - Set up mocks
        // - Prepare test data

        // Act
        var cut = Context.RenderComponent<MyComponent>(parameters => parameters
            .Add(p => p.Property, value));

        // Assert
        cut.Markup.Should().Contain("expected text");
    }
}
```

### API Client Test Template

```csharp
public class MyApiClientTests
{
    [Fact]
    public async Task MyMethod_Scenario_ExpectedResult()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/api/endpoint", expectedData);

        var httpClient = mockFactory.CreateClient();
        var apiClient = new MyApiClient(httpClient);

        // Act
        var result = await apiClient.MyMethodAsync();

        // Assert
        result.Should().NotBeNull();
    }
}
```

## Best Practices

### Component Tests
1. **Use ComponentTestBase** - Provides fresh context per test
2. **Mock dependencies** - Inject mocked services via `Context.Services`
3. **Test user interactions** - Use `.Find().Click()`, `.Change()`, etc.
4. **Verify rendering** - Check markup contains expected elements
5. **Test callbacks** - Verify component invokes event callbacks

### API Client Tests
1. **Use MockHttpClientFactory** - Simplifies HTTP mocking
2. **Test success and error paths** - Cover both happy and error scenarios
3. **Verify serialization** - Ensure models serialize/deserialize correctly
4. **Test authentication** - Verify bearer token handling
5. **Use FluentAssertions** - `.Should()` syntax for readable assertions

### Integration Tests
1. **Skip by default** - Use `[Fact(Skip = "...")]` for tests requiring API
2. **Use authenticated clients** - Helper methods for auth setup
3. **Test full workflows** - Create, read, update, delete operations
4. **Clean up data** - Reset state between tests
5. **Test authorization** - Verify role-based access control

## Dependencies

### Test Frameworks
- **xUnit 2.9.2** - Test framework
- **bUnit 1.31.3** - Blazor component testing
- **FluentAssertions 8.6.0** - Fluent assertion syntax

### Mocking
- **Moq 4.20.72** - General mocking
- **NSubstitute 5.3.0** - Alternative mocking library
- **RichardSzalay.MockHttp 7.0.0** - HTTP mocking

### Testing Utilities
- **Microsoft.AspNetCore.Mvc.Testing 9.0.9** - Integration testing
- **coverlet.collector 6.0.2** - Code coverage

## Code Coverage

Generate code coverage report:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

Coverage reports are generated in `TestResults/` directory.

### Coverage Goals
- **Component Tests**: 80%+ coverage for UI components
- **API Client Tests**: 90%+ coverage for API clients
- **Overall**: 75%+ coverage for Admin.Blazor project

## Continuous Integration

Tests run automatically on:
- Pull requests
- Commits to main/develop branches
- Nightly builds

**CI Configuration:**
```yaml
- name: Run Admin UI Tests
  run: dotnet test tests/Honua.Admin.Blazor.Tests --no-build --logger trx
```

## Troubleshooting

### JSInterop Errors
If you see JSInterop errors in component tests:
```csharp
// In your test
Context.JSInterop.Mode = JSRuntimeMode.Loose; // Already set in BunitTestContext
```

### MudBlazor Components Not Rendering
Ensure MudBlazor services are registered:
```csharp
Context.Services.AddMudServices(); // Already set in BunitTestContext
```

### Authentication Required
For components requiring authentication:
```csharp
var authStateProvider = new TestAuthenticationStateProvider(
    TestAuthenticationStateProvider.CreateAdministrator());
Context.Services.AddSingleton<AuthenticationStateProvider>(authStateProvider);
```

### Mock HttpClient Not Working
Ensure you're using `MockHttpClientFactory`:
```csharp
var mockFactory = new MockHttpClientFactory()
    .MockGetJson("/api/endpoint", data);
var httpClient = mockFactory.CreateClient();
```

## Resources

- [bUnit Documentation](https://bunit.dev/)
- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [MudBlazor Documentation](https://mudblazor.com/)

## Contributing

When adding new features to Admin UI:

1. **Write component tests** for new Blazor components
2. **Write API client tests** for new API clients
3. **Update existing tests** if behavior changes
4. **Maintain coverage** above 75%
5. **Document test patterns** for complex scenarios

## Questions?

Contact the development team or open an issue on GitHub.
