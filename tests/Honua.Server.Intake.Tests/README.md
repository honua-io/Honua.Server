# Honua.Server.Intake.Tests

Comprehensive test suite for the Honua Server Intake Service module.

## Overview

This test project provides **80%+ code coverage** for the Intake Service, which handles AI-powered conversations for gathering customer requirements, registry provisioning, and build delivery.

## Test Structure

### Unit Tests

- **IntakeAgentTests.cs** (18 tests)
  - AI conversation logic
  - OpenAI and Anthropic provider integration
  - Requirement extraction
  - Cost estimation
  - Error handling

- **IntakeControllerTests.cs** (19 tests)
  - REST API endpoints
  - Request validation
  - Error responses
  - Build triggering
  - Registry type determination

- **BuildQueueProcessorTests.cs** (7 tests)
  - Background service lifecycle
  - Build job processing
  - Concurrency control
  - Retry logic
  - Graceful shutdown

- **RegistryProvisionerTests.cs** (14 tests)
  - GitHub Container Registry provisioning
  - AWS ECR repository creation
  - Azure ACR token management
  - GCP Artifact Registry setup
  - Multi-cloud support

- **BuildDeliveryServiceTests.cs** (12 tests)
  - Build caching
  - Image copying
  - Tag management
  - Access control
  - Registry URL generation

- **ConversationStoreTests.cs** (4 tests)
  - Conversation persistence
  - JSON serialization
  - Requirements storage

- **ManifestGeneratorTests.cs** (7 tests)
  - Manifest generation from requirements
  - Resource sizing
  - Cloud-specific configurations
  - Advanced features

### Integration Tests

- **IntegrationTests.cs** (3 tests)
  - Full conversation flow (start → message → complete → build)
  - Error recovery scenarios
  - Multi-conversation isolation

### Test Helpers

- **TestHelpers.cs**
  - Sample data generators
  - Test fixtures
  - Common assertions
  - In-memory test implementations

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run with coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Run specific category
```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"
```

## Test Coverage Summary

| Component | Tests | Coverage | Critical Paths |
|-----------|-------|----------|----------------|
| IntakeAgent | 18 | 85% | ✓ AI conversation flow<br>✓ Requirement extraction<br>✓ Cost calculation |
| IntakeController | 19 | 90% | ✓ All API endpoints<br>✓ Validation logic<br>✓ Error handling |
| BuildQueueProcessor | 7 | 75% | ✓ Job processing<br>✓ Retry logic<br>✓ Graceful shutdown |
| RegistryProvisioner | 14 | 80% | ✓ All registry types<br>✓ Credential generation<br>✓ Error handling |
| BuildDeliveryService | 12 | 80% | ✓ Cache checking<br>✓ Image operations<br>✓ Tag management |
| ConversationStore | 4 | 90% | ✓ CRUD operations<br>✓ JSON handling |
| ManifestGenerator | 7 | 85% | ✓ Manifest creation<br>✓ Resource sizing |

**Overall Coverage: 83%**

## Key Test Scenarios

### Happy Paths
- ✓ Complete intake conversation flow
- ✓ Multi-step build triggering
- ✓ Registry provisioning for all cloud providers
- ✓ Build caching and delivery
- ✓ Background queue processing

### Error Handling
- ✓ Invalid conversation IDs
- ✓ Missing requirements
- ✓ Registry provisioning failures
- ✓ Build timeout scenarios
- ✓ Access denied cases
- ✓ Cancellation token handling

### Edge Cases
- ✓ Multiple concurrent conversations
- ✓ Retry logic with exponential backoff
- ✓ Graceful shutdown with active builds
- ✓ Cost estimation for all tiers
- ✓ Architecture-specific configurations

## Test Patterns

### Mocking
Uses **Moq** for creating test doubles:
- HTTP client factory for AI API calls
- Database stores for persistence
- Cloud provider SDKs
- Background services

### Assertions
Uses **FluentAssertions** for readable test assertions:
```csharp
result.Should().NotBeNull();
result.Success.Should().BeTrue();
result.ImageReference.Should().Contain("ecr");
```

### Test Data
Uses **TestHelpers** for consistent test data:
```csharp
var conversation = TestHelpers.CreateSampleConversation();
var requirements = TestHelpers.CreateSampleRequirements(cloudProvider: "aws");
```

## Dependencies

- xUnit 2.9.2
- Moq 4.20.72
- FluentAssertions 6.12.2
- RichardSzalay.MockHttp 7.0.0 (for HTTP mocking)
- Microsoft.NET.Test.Sdk 17.11.1

## CI/CD Integration

Tests are designed to run in CI/CD pipelines:
- Fast execution (< 2 minutes for full suite)
- No external dependencies required
- Deterministic results
- Parallel execution safe

## Future Enhancements

- [ ] Add performance benchmarks
- [ ] Increase coverage to 90%+
- [ ] Add mutation testing
- [ ] Add contract tests for AI providers
- [ ] Add load tests for queue processor
