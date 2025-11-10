# HonuaField.Tests

Unit tests for the Honua Field mobile application.

## Test Framework

- **xUnit** - Testing framework
- **Moq** - Mocking framework
- **FluentAssertions** - Assertion library for readable test assertions

## Project Structure

```
HonuaField.Tests/
├── Services/
│   ├── AuthenticationServiceTests.cs    # OAuth 2.0 + PKCE authentication tests
│   ├── SettingsServiceTests.cs          # Settings and secure storage tests
│   ├── ApiClientTests.cs                # HTTP client and bearer token tests
│   └── BiometricServiceTests.cs         # Biometric authentication tests
├── ViewModels/
│   └── LoginViewModelTests.cs           # Login view model logic tests
├── HonuaField.Tests.csproj
├── Usings.cs                            # Global using directives
└── README.md
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "FullyQualifiedName~AuthenticationServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~AuthenticationServiceTests.LoginAsync_ShouldReturnTrue_WhenCredentialsAreValid"
```

### Visual Studio / Rider

- Open Test Explorer
- Click "Run All" or run individual tests

## Test Categories

### Service Tests

**AuthenticationServiceTests**
- OAuth 2.0 Authorization Code + PKCE flow
- Token storage and retrieval
- Login success/failure scenarios
- Token refresh logic
- Logout functionality

**SettingsServiceTests**
- Key-value storage (string, int, bool, objects)
- Default value handling
- Key removal
- Clear all settings
- Secure storage for sensitive data

**ApiClientTests**
- HTTP request methods (GET, POST, PUT, DELETE)
- Bearer token authentication
- Authorization header injection
- Error handling and status code mapping
- Base URL configuration

**BiometricServiceTests**
- Biometric availability checks
- Biometric type detection
- Enrollment verification
- Authentication flow
- Error type handling (user canceled, locked, not enrolled, etc.)

### ViewModel Tests

**LoginViewModelTests**
- Property initialization
- Settings loading on view appearance
- Input validation (username, password)
- Login success/failure handling
- Remember me functionality
- Biometric authentication integration
- Navigation to main app
- Password clearing after login attempt

## Test Naming Convention

Tests follow the pattern: `MethodName_Should<Behavior>_When<Condition>`

Examples:
- `LoginAsync_ShouldReturnTrue_WhenCredentialsAreValid`
- `GetAsync_ShouldReturnDefaultValue_WhenKeyDoesNotExist`
- `AuthenticateAsync_ShouldReturnFailed_WhenBiometricNotAvailable`

## Mocking Strategy

- **IAuthenticationService** - Mocked in ViewModel tests
- **INavigationService** - Mocked in ViewModel tests
- **ISettingsService** - Mocked in Service and ViewModel tests
- **IBiometricService** - Mocked in ViewModel tests
- **IApiClient** - Mocked in AuthenticationService tests

## Code Coverage Goals

- **Services**: 80%+ coverage
- **ViewModels**: 70%+ coverage
- **Overall**: 75%+ coverage

## Adding New Tests

1. Create test class in appropriate folder (Services/, ViewModels/, etc.)
2. Name class with `Tests` suffix: `MyServiceTests.cs`
3. Use constructor to set up mocks and system under test
4. Write tests following naming convention
5. Use FluentAssertions for readable assertions:
   ```csharp
   result.Should().BeTrue();
   result.Should().NotBeNull();
   result.Should().BeOfType<MyType>();
   ```

## Continuous Integration

Tests run automatically on:
- Every commit to feature branches
- Pull requests to main branch
- Nightly builds

CI pipeline fails if:
- Any test fails
- Code coverage drops below threshold
- Build errors occur

## Sprint 2 Test Coverage

Sprint 2 tests cover:
- ✅ OAuth 2.0 Authorization Code + PKCE authentication
- ✅ JWT token storage and retrieval
- ✅ Login UI logic and validation
- ✅ Biometric authentication (Face ID, Touch ID, Fingerprint)
- ✅ Settings service with secure storage
- ✅ API client with bearer token authentication

## Future Test Additions

Sprint 3+:
- Database repository tests (SQLite + spatial queries)
- Feature CRUD operation tests
- Sync service tests (conflict resolution)
- Map integration tests (Mapsui rendering)
- Location service tests (GPS accuracy)
- Offline storage tests (caching strategies)
