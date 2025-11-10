# Honua.Integration.PowerBI.Tests

Comprehensive test suite for Power BI integration services.

## Test Coverage

### PowerBIDatasetServiceTests
Tests for dataset management operations including:
- Dataset creation and updates (Traffic, AirQuality, 311Requests, AssetManagement, BuildingOccupancy, Generic)
- Streaming dataset creation
- Row pushing with batching
- Dataset deletion
- Dataset refresh operations
- Embed token generation
- Error handling (401, 403, 429, 500)
- Cancellation token support
- Configuration validation

### PowerBIStreamingServiceTests
Tests for real-time streaming operations including:
- Single observation streaming
- Batch observation streaming
- Rate limiting (15 concurrent requests max)
- Anomaly alert streaming
- Severity calculation (Critical, High, Medium, Low)
- Auto-streaming lifecycle (start/stop)
- Dataset grouping for multiple targets
- Error handling and resilience
- Cancellation token support

## Test Patterns

### Mocking
- Uses Moq for mocking Power BI services
- Mock Azure AD authentication (via service configuration)
- Mock HTTP responses and error codes
- Mock IPowerBIDatasetService for streaming tests

### Test Fixtures
- `PowerBITestFixture`: Shared configuration and mock setup
- `PowerBIMockResponseBuilder`: Helper methods for creating mock responses

### Assertions
- Uses FluentAssertions for readable test assertions
- Verifies service calls with Moq's Verify methods
- Tests async operations with proper async/await patterns

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~PowerBIDatasetServiceTests"

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## Test Structure

```
Honua.Integration.PowerBI.Tests/
├── Services/
│   ├── PowerBIDatasetServiceTests.cs      # Dataset service tests
│   └── PowerBIStreamingServiceTests.cs    # Streaming service tests
├── TestFixtures/
│   └── PowerBITestFixture.cs              # Shared test utilities
└── README.md                               # This file
```

## Notes

- Tests use xUnit as the test framework
- FluentAssertions provides expressive assertion syntax
- Moq enables flexible mocking of dependencies
- Tests are designed to run in isolation without external dependencies
- Some tests demonstrate structure but require service refactoring for full testability
  (specifically tests that need to mock the internal PowerBIClient creation)

## Future Improvements

To improve testability, consider:
1. Extracting IPowerBIClient wrapper interface
2. Injecting IPowerBIClientFactory for better unit testing
3. Adding integration tests with actual Power BI API (requiring credentials)
4. Adding performance/load tests for streaming scenarios
