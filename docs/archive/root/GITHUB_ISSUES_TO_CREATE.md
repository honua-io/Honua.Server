# GitHub Issues for Architectural Debt Tracking

**Generated**: 2025-10-19
**Source**: ARCHITECTURAL_CODE_REVIEW.md
**Total Issues**: 10 priority items

---

## Issue 1: Refactor OgcHandlers.cs (4,816 lines) into Focused Handler Classes

### Priority
🔴 Critical

### Description
The OgcHandlers.cs file has grown to 4,816 lines, violating the Single Responsibility Principle (SRP) and making it nearly impossible to maintain, test, or collaborate on effectively. This god class contains all OGC service operations in a single file, creating a significant maintainability bottleneck.

### Current State
- Single file: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcHandlers.cs`
- Line count: 4,816 lines
- Contains multiple OGC operation handlers in one class
- High cognitive load for developers
- Guaranteed merge conflicts in team environment
- Difficult to unit test individual operations
- No clear separation of concerns

### Desired State
Break the monolithic handler into focused, single-responsibility classes:
- `OgcGetCapabilitiesHandler.cs` (GetCapabilities operation)
- `OgcGetFeatureHandler.cs` (GetFeature operation)
- `OgcDescribeFeatureTypeHandler.cs` (DescribeFeatureType operation)
- `OgcGetFeatureInfoHandler.cs` (GetFeatureInfo operation)
- `OgcTransactionHandler.cs` (Transaction/WFS-T operations)
- `OgcLockFeatureHandler.cs` (LockFeature operations)

Each handler should:
- Be <500 lines (ideally <300)
- Have focused unit tests
- Implement a common handler interface
- Use dependency injection properly

### Acceptance Criteria
- [ ] Extract GetCapabilities handler to separate class
- [ ] Extract GetFeature handler to separate class
- [ ] Extract DescribeFeatureType handler to separate class
- [ ] Extract GetFeatureInfo handler to separate class
- [ ] Extract Transaction handler to separate class
- [ ] Extract LockFeature handler to separate class
- [ ] Create IHandler interface or use MediatR pattern
- [ ] Update DI registrations in ServiceCollectionExtensions
- [ ] All existing tests pass
- [ ] Add unit tests for each new handler class (80%+ coverage)
- [ ] No file exceeds 500 lines
- [ ] Update documentation/README if needed
- [ ] Code review completed
- [ ] Original OgcHandlers.cs is deleted

### Estimated Effort
21 story points (Large - 2-3 sprints)

**Breakdown**:
- Analysis and interface design: 3 points
- Handler extraction: 13 points (2-3 points per handler)
- Testing: 3 points
- Documentation: 2 points

### Labels
`technical-debt`, `refactoring`, `code-quality`, `critical`, `ogc`, `architecture`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcHandlers.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/OgcSharedHandlers.cs` (2,953 lines - may need similar treatment)
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Ogc/WfsHandlers.cs` (2,413 lines - may need similar treatment)

### Implementation Notes
Consider using MediatR pattern:
```csharp
public class GetCapabilitiesQuery : IRequest<CapabilitiesResponse>
{
    public string Service { get; set; }
    public string Version { get; set; }
}

public class GetCapabilitiesHandler : IRequestHandler<GetCapabilitiesQuery, CapabilitiesResponse>
{
    private readonly ILogger<GetCapabilitiesHandler> _logger;
    private readonly ICapabilitiesBuilder _builder;

    // Implementation
}
```

---

## Issue 2: Refactor DeploymentConfigurationAgent.cs (4,235 lines) into Smaller Agent Classes

### Priority
🔴 Critical

### Description
DeploymentConfigurationAgent.cs contains 4,235 lines of code, making it a massive god class that violates SRP. This AI agent handles multiple deployment concerns that should be separated into focused, testable components.

### Current State
- Single file: `/home/mike/projects/HonuaIO/src/Honua.Cli.AI/Services/Agents/Specialized/DeploymentConfigurationAgent.cs`
- Line count: 4,235 lines
- Handles multiple deployment responsibilities
- Contains 30+ Console.WriteLine debug statements
- Difficult to test individual agent capabilities
- Hard to reason about agent behavior
- Impossible for multiple developers to work on simultaneously

### Desired State
Split into focused agent classes:
- `InfrastructureAnalysisAgent.cs` (Infrastructure analysis and recommendations)
- `CloudProviderAgent.cs` (Cloud provider configuration - AWS, Azure, GCP)
- `DeploymentValidationAgent.cs` (Pre-deployment validation)
- `ConfigurationGenerationAgent.cs` (Generate deployment configs)
- `DeploymentExecutionAgent.cs` (Execute deployment steps)
- `DeploymentMonitoringAgent.cs` (Monitor deployment health)

Each agent should:
- Be <400 lines
- Have single responsibility
- Use ILogger<T> instead of Console.WriteLine
- Have comprehensive unit tests
- Be independently testable

### Acceptance Criteria
- [ ] Extract infrastructure analysis logic to InfrastructureAnalysisAgent
- [ ] Extract cloud provider logic to CloudProviderAgent
- [ ] Extract validation logic to DeploymentValidationAgent
- [ ] Extract config generation to ConfigurationGenerationAgent
- [ ] Extract execution logic to DeploymentExecutionAgent
- [ ] Extract monitoring logic to DeploymentMonitoringAgent
- [ ] Replace all Console.WriteLine with ILogger<T> calls
- [ ] Create agent coordinator to orchestrate multi-agent workflows
- [ ] Update DI registrations
- [ ] All existing tests pass
- [ ] Add unit tests for each agent (80%+ coverage)
- [ ] No file exceeds 500 lines
- [ ] Integration tests for agent coordination
- [ ] Original DeploymentConfigurationAgent.cs is deleted

### Estimated Effort
21 story points (Large - 2-3 sprints)

**Breakdown**:
- Analysis and agent design: 3 points
- Agent extraction: 13 points (2 points per agent)
- Logging refactoring: 2 points
- Testing: 3 points

### Labels
`technical-debt`, `refactoring`, `code-quality`, `critical`, `ai-agents`, `architecture`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Cli.AI/Services/Agents/Specialized/DeploymentConfigurationAgent.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Cli.AI/Services/Agents/HonuaMagenticCoordinator.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Cli.AI/Services/Agents/HonuaAgentFactory.cs`

### Implementation Notes
Consider using agent composition pattern:
```csharp
public class DeploymentOrchestrator
{
    private readonly IInfrastructureAnalysisAgent _analysisAgent;
    private readonly ICloudProviderAgent _cloudAgent;
    private readonly IDeploymentValidationAgent _validationAgent;
    private readonly ILogger<DeploymentOrchestrator> _logger;

    public async Task<DeploymentResult> ExecuteDeploymentAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken)
    {
        // Orchestrate agents
    }
}
```

---

## Issue 3: Refactor GeoservicesRESTFeatureServerController.cs (3,562 lines) Using Handler Pattern

### Priority
🔴 Critical

### Description
GeoservicesRESTFeatureServerController.cs is a 3,562-line controller that violates MVC best practices and SRP. Controllers should be thin orchestrators, not contain business logic.

### Current State
- Single file: `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs`
- Line count: 3,562 lines
- Contains extensive business logic
- Multiple responsibilities mixed together
- Difficult to test controller actions
- Hard to maintain and extend
- Violates thin controller principle

### Desired State
Refactor to thin controller with handler pattern:

**Controller** (should be <300 lines):
- Route definitions
- Request validation
- Delegate to handlers
- Response mapping

**Handlers** (separate classes):
- `FeatureServerMetadataHandler.cs` (Service metadata)
- `LayerQueryHandler.cs` (Query operations)
- `FeatureGeometryHandler.cs` (Geometry operations)
- `FeatureAttachmentHandler.cs` (Attachment operations)
- `FeatureSyncHandler.cs` (Sync operations)
- `FeatureEditHandler.cs` (Edit operations)

### Acceptance Criteria
- [ ] Create handler interface (IFeatureServerHandler or use MediatR)
- [ ] Extract metadata logic to FeatureServerMetadataHandler
- [ ] Extract query logic to LayerQueryHandler
- [ ] Extract geometry logic to FeatureGeometryHandler
- [ ] Extract attachment logic to FeatureAttachmentHandler
- [ ] Extract sync logic to FeatureSyncHandler
- [ ] Extract edit logic to FeatureEditHandler
- [ ] Controller reduced to <300 lines (thin controller)
- [ ] All business logic moved to handlers/services
- [ ] Update DI registrations
- [ ] All existing tests pass
- [ ] Add handler unit tests (80%+ coverage)
- [ ] Integration tests for controller endpoints
- [ ] No handler exceeds 400 lines

### Estimated Effort
18 story points (Large - 2 sprints)

**Breakdown**:
- Handler pattern design: 2 points
- Handler extraction: 12 points (2 points per handler)
- Testing: 3 points
- Documentation: 1 point

### Labels
`technical-debt`, `refactoring`, `code-quality`, `critical`, `esri-rest`, `architecture`, `controller`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/GeoservicesREST/GeoservicesRESTFeatureServerController.cs`

### Implementation Notes
Recommended pattern:
```csharp
[ApiController]
[Route("rest/services")]
public class GeoservicesRESTFeatureServerController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpGet("{service}/FeatureServer")]
    public async Task<IActionResult> GetServiceMetadata(
        string service,
        CancellationToken cancellationToken)
    {
        var query = new GetServiceMetadataQuery { ServiceName = service };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
```

---

## Issue 4: Replace 304 Generic Exception Handlers with Specific Exception Types

### Priority
🔴 Critical

### Description
The codebase contains 304 files with generic `catch (Exception ex)` handlers, which is an anti-pattern that catches system exceptions that should crash (OutOfMemoryException, StackOverflowException), hides real errors, and makes debugging extremely difficult.

### Current State
- 304 files contain `catch (Exception ex)` patterns
- System exceptions are being caught inappropriately
- Exceptions are sometimes swallowed (return null)
- Poor error diagnostics in production
- Violates fail-fast principle
- Difficult to distinguish between expected and unexpected errors

### Desired State
All exception handlers should:
- Catch specific exception types only
- Let system exceptions propagate (StackOverflowException, OutOfMemoryException, etc.)
- Use custom domain exception types
- Provide meaningful error context
- Follow fail-fast principle for unexpected errors

### Acceptance Criteria
- [ ] Audit all 304 files with generic exception handlers
- [ ] Create custom exception hierarchy:
  - [ ] DataAccessException (for database errors)
  - [ ] ExternalServiceException (for API/HTTP errors)
  - [ ] ValidationException (for business rule violations)
  - [ ] AuthenticationException (for auth errors)
  - [ ] AuthorizationException (for authz errors)
- [ ] Replace generic handlers with specific exception types
- [ ] Document which exceptions should be caught vs allowed to propagate
- [ ] Add code analysis rule CA1031 (severity = warning)
- [ ] All tests pass
- [ ] Zero generic `catch (Exception ex)` in production code (except entry points)
- [ ] Update exception handling documentation

### Estimated Effort
13 story points (Large - 1.5-2 sprints)

**Breakdown**:
- Custom exception hierarchy: 2 points
- Audit and categorization: 3 points
- Refactoring (304 files): 7 points
- Testing: 1 point

### Labels
`technical-debt`, `refactoring`, `code-quality`, `critical`, `error-handling`, `exceptions`

### Related Files
304 files across the codebase (search for `catch (Exception`)

### Implementation Notes
**Anti-Pattern** (current):
```csharp
try
{
    await _dbContext.SaveChangesAsync();
}
catch (Exception ex) // ❌ TOO BROAD
{
    _logger.LogError(ex, "Something failed");
    return null; // ❌ Swallows error
}
```

**Correct Pattern**:
```csharp
try
{
    await _dbContext.SaveChangesAsync();
}
catch (DbUpdateException ex) // ✅ Specific
{
    _logger.LogError(ex, "Failed to save changes to database");
    throw new DataAccessException("Failed to persist data", ex);
}
catch (SqlException ex) when (ex.Number == -2) // ✅ Timeout
{
    _logger.LogWarning(ex, "Database timeout");
    throw new DataAccessException("Database operation timed out", ex);
}
// Let system exceptions propagate
```

**Custom Exception Base**:
```csharp
public abstract class HonuaException : Exception
{
    public string ErrorCode { get; }

    protected HonuaException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

public class DataAccessException : HonuaException
{
    public DataAccessException(string message, Exception? innerException = null)
        : base("DATA_ACCESS_ERROR", message, innerException)
    {
    }
}
```

---

## Issue 5: Add ILogger<T> to 65 Classes Using Non-Generic ILogger

### Priority
🟡 High

### Description
65 classes use the non-generic `ILogger` interface instead of `ILogger<T>`, which reduces logging performance, eliminates type context in logs, and makes filtering logs by component difficult.

### Current State
- 65 instances of non-generic `ILogger`
- Manual category specification required
- Extra string allocations
- No automatic type context
- Harder to filter logs in observability tools
- Less performant than generic version

### Desired State
All classes should use `ILogger<T>` where T is the class type:
- Automatic type context in all log entries
- Better performance (no string allocations)
- Easier log filtering by component
- Consistent logging pattern across codebase

### Acceptance Criteria
- [ ] Identify all 65 classes using non-generic ILogger
- [ ] Create tracking spreadsheet/checklist
- [ ] Update each class to use `ILogger<T>`
- [ ] Update constructor injection from `ILoggerFactory` to `ILogger<T>`
- [ ] Update DI registrations if needed
- [ ] Remove unused `ILoggerFactory` injections
- [ ] All tests pass
- [ ] Add analyzer rule to prevent future non-generic ILogger usage
- [ ] Verify log output includes proper type context
- [ ] Zero non-generic ILogger usage (except where truly needed)

### Estimated Effort
5 story points (Medium - 1 sprint)

**Breakdown**:
- Identify all instances: 1 point
- Refactoring: 3 points
- Testing and verification: 1 point

### Labels
`technical-debt`, `refactoring`, `code-quality`, `high-priority`, `logging`, `performance`

### Related Files
65 files across the codebase (search for `ILogger _logger` without generic parameter)

### Implementation Notes
**Anti-Pattern** (current):
```csharp
private readonly ILogger _logger; // ❌ No type info

public MyClass(ILoggerFactory loggerFactory)
{
    _logger = loggerFactory.CreateLogger("MyCategory"); // ❌ String allocation
}

public void DoWork()
{
    _logger.LogInformation("Working..."); // ❌ No type context
}
```

**Correct Pattern**:
```csharp
private readonly ILogger<MyClass> _logger; // ✅ Type-safe

public MyClass(ILogger<MyClass> logger)
{
    _logger = logger; // ✅ Efficient, automatic category
}

public void DoWork()
{
    _logger.LogInformation("Working..."); // ✅ Includes type context automatically
}
```

**Search Command**:
```bash
# Find non-generic ILogger usage
grep -r "private readonly ILogger _logger" src/ --include="*.cs"
grep -r "ILoggerFactory" src/ --include="*.cs"
```

---

## Issue 6: Increase Test Coverage from 42% to 80%

### Priority
🟡 High

### Description
Current test coverage is approximately 42% (419 test files for 988 source files), which creates significant regression risk, makes refactoring dangerous, and allows breaking changes to go undetected.

### Current State
- Source files: 988
- Test files: 419
- Estimated coverage: ~42%
- Large portions of critical code untested
- High regression risk
- Refactoring is risky without safety net
- Breaking changes may go undetected

### Desired State
- Target: 80% line coverage for all code
- 90%+ coverage for critical paths:
  - Authentication/Authorization
  - Data access layer
  - OGC handlers
  - Export functionality
  - Raster processing
- Coverage gates in CI/CD pipeline
- Automated coverage reporting

### Acceptance Criteria
- [ ] Set up Coverlet for code coverage measurement
- [ ] Configure coverage reporting in CI/CD
- [ ] Establish baseline coverage metrics
- [ ] Add tests for authentication/authorization (target 90%+)
- [ ] Add tests for data access layer (target 90%+)
- [ ] Add tests for OGC handlers (target 80%+)
- [ ] Add tests for export functionality (target 80%+)
- [ ] Add tests for raster processing (target 80%+)
- [ ] Add tests for remaining uncovered code (target 80%+)
- [ ] Configure coverage gate: fail build if coverage drops below 75%
- [ ] Generate coverage reports in CI
- [ ] Overall coverage reaches 80%+
- [ ] Coverage badge added to README

### Estimated Effort
34 story points (Epic - 4-5 sprints, ongoing)

**Breakdown**:
- Coverage infrastructure setup: 2 points
- Authentication/Authorization tests: 5 points
- Data access tests: 5 points
- OGC handlers tests: 8 points
- Export functionality tests: 5 points
- Raster processing tests: 5 points
- General coverage improvements: 4 points

### Labels
`technical-debt`, `testing`, `code-quality`, `high-priority`, `test-coverage`, `epic`

### Related Files
All source files in:
- `/home/mike/projects/HonuaIO/src/`

Test files in:
- `/home/mike/projects/HonuaIO/tests/`

### Implementation Notes
**Setup Coverlet**:
```xml
<!-- Add to test projects -->
<PackageReference Include="coverlet.collector" Version="6.0.0" />
<PackageReference Include="coverlet.msbuild" Version="6.0.0" />
```

**Generate Coverage Report**:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

**CI/CD Integration** (.github/workflows/integration-tests.yml):
```yaml
- name: Run tests with coverage
  run: dotnet test --no-build --verbosity normal /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

- name: Upload coverage to Codecov
  uses: codecov/codecov-action@v3
  with:
    files: ./coverage.opencover.xml
    fail_ci_if_error: true
```

**Coverage Goals by Priority**:
1. **Critical paths** (Sprint 1-2): Auth, Data Access - 90%
2. **Core functionality** (Sprint 3): OGC, Export - 80%
3. **Supporting features** (Sprint 4-5): Raster, Utilities - 80%

---

## Issue 7: Add Health Checks for All External Dependencies

### Priority
🟡 High

### Description
The application lacks comprehensive health checks for external dependencies (databases, caches, cloud storage), making it difficult to monitor application health in production and impossible for orchestrators (Kubernetes, ECS) to make informed routing decisions.

### Current State
- Basic health check endpoint exists
- Missing health checks for:
  - PostgreSQL/SQL Server databases
  - Redis cache
  - AWS S3 connectivity
  - Azure Blob Storage connectivity
  - GCS connectivity
  - External APIs
- No readiness vs liveness distinction
- Kubernetes/orchestrators cannot make informed decisions
- Difficult to diagnose startup failures

### Desired State
Comprehensive health checks for all dependencies:
- Database connectivity (Postgres, SQL Server)
- Cache connectivity (Redis)
- Cloud storage (S3, Azure Blob, GCS)
- External API dependencies
- Separate liveness and readiness probes
- Detailed health check responses
- Configurable health check timeouts

### Acceptance Criteria
- [ ] Install Microsoft.Extensions.Diagnostics.HealthChecks packages
- [ ] Add PostgreSQL health check (AspNetCore.HealthChecks.Npgsql)
- [ ] Add SQL Server health check (AspNetCore.HealthChecks.SqlServer)
- [ ] Add Redis health check (AspNetCore.HealthChecks.Redis)
- [ ] Add S3 health check (custom implementation)
- [ ] Add Azure Blob Storage health check (custom implementation)
- [ ] Add GCS health check (custom implementation)
- [ ] Implement liveness endpoint (/health/live)
- [ ] Implement readiness endpoint (/health/ready)
- [ ] Configure health check UI endpoint (/health-ui)
- [ ] Add health check tests
- [ ] Update Kubernetes/Docker deployment manifests
- [ ] Document health check endpoints
- [ ] All health checks pass in integration tests

### Estimated Effort
8 story points (Medium - 1 sprint)

**Breakdown**:
- Health check infrastructure: 2 points
- Database health checks: 2 points
- Storage health checks: 2 points
- Testing and documentation: 2 points

### Labels
`technical-debt`, `observability`, `operations`, `high-priority`, `health-checks`, `production-readiness`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Program.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Cli.AI/HealthChecks/ProcessFrameworkHealthCheck.cs` (example)

### Implementation Notes
**Install Packages**:
```xml
<PackageReference Include="AspNetCore.HealthChecks.Npgsql" Version="7.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.SqlServer" Version="7.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Redis" Version="7.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI" Version="7.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="7.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.InMemory.Storage" Version="7.0.0" />
```

**Configure Health Checks**:
```csharp
services.AddHealthChecks()
    .AddNpgSql(
        configuration.GetConnectionString("Postgres")!,
        name: "postgres",
        timeout: TimeSpan.FromSeconds(5),
        tags: new[] { "db", "ready" })
    .AddRedis(
        configuration.GetConnectionString("Redis")!,
        name: "redis",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "cache", "ready" })
    .AddCheck<S3HealthCheck>("s3", tags: new[] { "storage", "ready" })
    .AddCheck<AzureBlobHealthCheck>("azure-blob", tags: new[] { "storage", "ready" })
    .AddCheck<GcsHealthCheck>("gcs", tags: new[] { "storage", "ready" });

// UI (optional, for development)
services.AddHealthChecksUI()
    .AddInMemoryStorage();
```

**Custom Health Check Example**:
```csharp
public class S3HealthCheck : IHealthCheck
{
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<S3HealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple operation to verify connectivity
            await _s3Client.ListBucketsAsync(cancellationToken);
            return HealthCheckResult.Healthy("S3 connection is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 health check failed");
            return HealthCheckResult.Unhealthy("S3 connection failed", ex);
        }
    }
}
```

**Endpoints**:
```csharp
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options => options.UIPath = "/health-ui");
```

**Kubernetes Integration**:
```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 5
```

---

## Issue 8: Implement Comprehensive API Versioning Strategy

### Priority
🟢 Medium

### Description
The application lacks a clear API versioning strategy, making it difficult to introduce breaking changes without impacting existing clients. This creates technical debt and limits the ability to evolve the API.

### Current State
- No visible API versioning mechanism
- Breaking changes would impact all clients
- No deprecation strategy
- Difficult to support multiple API versions
- No version negotiation
- API evolution is blocked

### Desired State
Implement comprehensive API versioning using ASP.NET Core API Versioning:
- URL-based versioning (e.g., `/api/v1/features`, `/api/v2/features`)
- Header-based versioning support (optional)
- Version deprecation mechanism
- Multiple version support
- Clear version migration path
- Backward compatibility support

### Acceptance Criteria
- [ ] Install Asp.Versioning.Mvc and Asp.Versioning.Mvc.ApiExplorer packages
- [ ] Configure API versioning in Program.cs/Startup
- [ ] Apply versioning to all API controllers
- [ ] Set default version to 1.0
- [ ] Configure version reporting in responses
- [ ] Update OpenAPI/Swagger to show versions
- [ ] Document versioning strategy
- [ ] Add version deprecation workflow
- [ ] Update client documentation
- [ ] Add versioning tests
- [ ] All endpoints accessible via versioned URLs

### Estimated Effort
8 story points (Medium - 1 sprint)

**Breakdown**:
- Versioning infrastructure setup: 2 points
- Apply to all controllers: 3 points
- Swagger/OpenAPI integration: 2 points
- Documentation and testing: 1 point

### Labels
`technical-debt`, `api`, `versioning`, `medium-priority`, `architecture`, `breaking-changes`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Program.cs`
- All controllers in `/home/mike/projects/HonuaIO/src/Honua.Server.Host/`

### Implementation Notes
**Install Packages**:
```xml
<PackageReference Include="Asp.Versioning.Mvc" Version="7.1.0" />
<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" Version="7.1.0" />
```

**Configure Versioning**:
```csharp
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true; // Add version info to response headers
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

**Apply to Controllers**:
```csharp
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class FeaturesController : ControllerBase
{
    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<IActionResult> GetFeaturesV1()
    {
        // V1 implementation
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    public async Task<IActionResult> GetFeaturesV2()
    {
        // V2 implementation with breaking changes
    }
}
```

**Swagger Integration**:
```csharp
services.AddSwaggerGen(options =>
{
    var provider = services.BuildServiceProvider()
        .GetRequiredService<IApiVersionDescriptionProvider>();

    foreach (var description in provider.ApiVersionDescriptions)
    {
        options.SwaggerDoc(
            description.GroupName,
            new OpenApiInfo
            {
                Title = $"Honua API {description.ApiVersion}",
                Version = description.ApiVersion.ToString()
            });
    }
});
```

**Deprecation Example**:
```csharp
[ApiVersion("1.0", Deprecated = true)] // Mark as deprecated
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class FeaturesController : ControllerBase
{
    // V1 still works but clients are warned
}
```

---

## Issue 9: Complete KMS Encryption Integration for Production

### Priority
🟡 High

### Description
Configuration files indicate incomplete KMS (Key Management Service) integration for AWS and GCP, with TODOs suggesting that connection strings and sensitive data may not be properly encrypted at rest. This creates compliance and security risks for production deployments.

### Current State
- Configuration notes indicate incomplete KMS integration
- "TODO for full KMS integration" in configuration files
- Unclear if connection strings are properly encrypted
- May not meet compliance requirements (PCI-DSS, HIPAA, SOC 2)
- Production deployment risk
- Audit findings potential

### Desired State
- Full AWS KMS integration for secrets encryption
- Full GCP KMS integration for secrets encryption
- Azure Key Vault integration (if using Azure)
- All connection strings encrypted at rest
- Encryption keys rotated regularly
- Audit trail for key usage
- Compliance-ready configuration
- Documented encryption architecture

### Acceptance Criteria
- [ ] Audit current encryption implementation
- [ ] Document encryption gaps and requirements
- [ ] Implement AWS KMS integration for secret encryption
- [ ] Implement GCP KMS integration for secret encryption
- [ ] Implement Azure Key Vault integration (if needed)
- [ ] Encrypt all connection strings at rest
- [ ] Implement key rotation policy
- [ ] Add encryption health checks
- [ ] Add encryption integration tests
- [ ] Remove "TODO" notes from configuration
- [ ] Document encryption architecture
- [ ] Update deployment documentation
- [ ] Security review completed
- [ ] Compliance verification (PCI-DSS, HIPAA if applicable)

### Estimated Effort
13 story points (Large - 1.5-2 sprints)

**Breakdown**:
- Current state audit: 2 points
- AWS KMS integration: 3 points
- GCP KMS integration: 3 points
- Azure Key Vault integration: 2 points
- Testing and documentation: 3 points

### Labels
`technical-debt`, `security`, `encryption`, `high-priority`, `compliance`, `production-readiness`, `kms`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/appsettings.ConnectionEncryption.json`
- Configuration-related files in `/home/mike/projects/HonuaIO/src/Honua.Server.Host/`

### Implementation Notes
**AWS KMS Integration**:
```csharp
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

public class AwsKmsEncryptionService : IEncryptionService
{
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly string _keyId;

    public async Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken)
    {
        var request = new EncryptRequest
        {
            KeyId = _keyId,
            Plaintext = new MemoryStream(Encoding.UTF8.GetBytes(plaintext))
        };

        var response = await _kmsClient.EncryptAsync(request, cancellationToken);
        return Convert.ToBase64String(response.CiphertextBlob.ToArray());
    }

    public async Task<string> DecryptAsync(string ciphertext, CancellationToken cancellationToken)
    {
        var request = new DecryptRequest
        {
            CiphertextBlob = new MemoryStream(Convert.FromBase64String(ciphertext))
        };

        var response = await _kmsClient.DecryptAsync(request, cancellationToken);
        return Encoding.UTF8.GetString(response.Plaintext.ToArray());
    }
}
```

**Configuration Protection**:
```csharp
services.AddSingleton<IEncryptionService, AwsKmsEncryptionService>();

// Encrypt connection strings on startup
var encryptionService = services.BuildServiceProvider().GetRequiredService<IEncryptionService>();
var encryptedConnectionString = await encryptionService.EncryptAsync(connectionString);
```

**Key Rotation**:
```csharp
public class KeyRotationService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Rotate keys every 90 days
            await Task.Delay(TimeSpan.FromDays(90), stoppingToken);
            await RotateKeysAsync(stoppingToken);
        }
    }
}
```

**Security Requirements**:
1. Never log decrypted values
2. Use envelope encryption for large data
3. Implement key rotation (90-day cycle)
4. Enable CloudTrail/audit logging for key usage
5. Use separate keys per environment
6. Implement key expiration and revocation

---

## Issue 10: Refactor ServiceCollectionExtensions God Class into Focused Extension Classes

### Priority
🟡 High

### Description
The ServiceCollectionExtensions class has become a "god class" with 50+ using statements that registers the entire application's dependencies in a single file. This creates merge conflicts, poor discoverability, and violates SRP.

### Current State
- Large ServiceCollectionExtensions.cs file
- 50+ using statements
- Registers entire application in one file
- Hard to find specific registrations
- Merge conflict nightmare in team environment
- No logical grouping of related services
- Difficult to test DI configuration
- Poor maintainability

### Desired State
Break into focused, single-responsibility extension classes organized by domain:
- `CachingServiceExtensions.cs` (Redis, distributed cache)
- `DatabaseServiceExtensions.cs` (EF Core, DbContext, repositories)
- `AuthenticationServiceExtensions.cs` (JWT, OAuth, OIDC)
- `AuthorizationServiceExtensions.cs` (Policies, requirements)
- `RasterServiceExtensions.cs` (GDAL, COG, Zarr services)
- `VectorServiceExtensions.cs` (PostGIS, feature services)
- `OgcServiceExtensions.cs` (WMS, WFS, OGC handlers)
- `StacServiceExtensions.cs` (STAC catalog, items)
- `ObservabilityServiceExtensions.cs` (Logging, metrics, tracing)
- `ExportServiceExtensions.cs` (Export formats, converters)

### Acceptance Criteria
- [ ] Analyze current ServiceCollectionExtensions registrations
- [ ] Create logical grouping plan
- [ ] Create CachingServiceExtensions class
- [ ] Create DatabaseServiceExtensions class
- [ ] Create AuthenticationServiceExtensions class
- [ ] Create AuthorizationServiceExtensions class
- [ ] Create RasterServiceExtensions class
- [ ] Create VectorServiceExtensions class
- [ ] Create OgcServiceExtensions class
- [ ] Create StacServiceExtensions class
- [ ] Create ObservabilityServiceExtensions class
- [ ] Create ExportServiceExtensions class
- [ ] Update Program.cs/Startup to call new extensions
- [ ] All tests pass
- [ ] No extension class exceeds 300 lines
- [ ] Original ServiceCollectionExtensions deleted or minimal
- [ ] Documentation updated

### Estimated Effort
8 story points (Medium - 1 sprint)

**Breakdown**:
- Analysis and planning: 1 point
- Extension class creation: 5 points
- Testing and validation: 1 point
- Documentation: 1 point

### Labels
`technical-debt`, `refactoring`, `code-quality`, `high-priority`, `dependency-injection`, `architecture`

### Related Files
- `/home/mike/projects/HonuaIO/src/Honua.Server.Core/DependencyInjection/ServiceCollectionExtensions.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Extensions/ServiceCollectionExtensions.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Enterprise/DependencyInjection/ServiceCollectionExtensions.cs`
- `/home/mike/projects/HonuaIO/src/Honua.Server.Host/Program.cs`

### Implementation Notes
**Target Structure**:
```
src/Honua.Server.Core/DependencyInjection/
├── CachingServiceExtensions.cs
├── DatabaseServiceExtensions.cs
├── AuthenticationServiceExtensions.cs
├── AuthorizationServiceExtensions.cs
├── RasterServiceExtensions.cs
├── VectorServiceExtensions.cs
├── OgcServiceExtensions.cs
├── StacServiceExtensions.cs
├── ObservabilityServiceExtensions.cs
└── ExportServiceExtensions.cs
```

**Example Pattern**:
```csharp
// CachingServiceExtensions.cs
namespace Honua.Server.Core.DependencyInjection;

public static class CachingServiceExtensions
{
    public static IServiceCollection AddHonuaCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
            options.InstanceName = "Honua:";
        });

        services.AddDistributedMemoryCache(); // Fallback

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
```

**Usage in Program.cs**:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Organized, focused registrations
builder.Services.AddHonuaCaching(builder.Configuration);
builder.Services.AddHonuaDatabase(builder.Configuration);
builder.Services.AddHonuaAuthentication(builder.Configuration);
builder.Services.AddHonuaAuthorization();
builder.Services.AddHonuaRasterServices();
builder.Services.AddHonuaVectorServices();
builder.Services.AddHonuaOgcServices();
builder.Services.AddHonuaStacServices();
builder.Services.AddHonuaObservability(builder.Configuration);
builder.Services.AddHonuaExportServices();
```

**Benefits**:
1. Clear separation of concerns
2. Easy to find specific registrations
3. Reduced merge conflicts
4. Easier to test DI configuration
5. Better maintainability
6. Logical grouping by domain

---

## Summary Table

| # | Issue | Priority | Effort | Sprint Target |
|---|-------|----------|--------|---------------|
| 1 | Refactor OgcHandlers.cs | 🔴 Critical | 21 pts | Sprint 1-3 |
| 2 | Refactor DeploymentConfigurationAgent.cs | 🔴 Critical | 21 pts | Sprint 1-3 |
| 3 | Refactor GeoservicesRESTFeatureServerController.cs | 🔴 Critical | 18 pts | Sprint 2-3 |
| 4 | Replace 304 Generic Exception Handlers | 🔴 Critical | 13 pts | Sprint 2-3 |
| 5 | Add ILogger<T> to 65 Classes | 🟡 High | 5 pts | Sprint 4 |
| 6 | Increase Test Coverage to 80% | 🟡 High | 34 pts | Sprint 4-8 (Epic) |
| 7 | Add Health Checks | 🟡 High | 8 pts | Sprint 4 |
| 8 | Implement API Versioning | 🟢 Medium | 8 pts | Sprint 5 |
| 9 | Complete KMS Encryption | 🟡 High | 13 pts | Sprint 5-6 |
| 10 | Refactor ServiceCollectionExtensions | 🟡 High | 8 pts | Sprint 6 |

**Total Estimated Effort**: 149 story points (~8-10 sprints)

---

## Implementation Strategy

### Phase 1: Critical Refactoring (Sprint 1-3)
**Goal**: Break up god classes and fix critical anti-patterns

1. Issue #1: OgcHandlers.cs refactoring
2. Issue #2: DeploymentConfigurationAgent.cs refactoring
3. Issue #3: GeoservicesRESTFeatureServerController.cs refactoring
4. Issue #4: Generic exception handlers

**Outcome**: Improved maintainability, reduced complexity, better testability

### Phase 2: Infrastructure & Quality (Sprint 4-6)
**Goal**: Improve observability, testing, and production readiness

5. Issue #5: ILogger<T> migration
6. Issue #6: Test coverage increase (ongoing)
7. Issue #7: Health checks
8. Issue #9: KMS encryption
9. Issue #10: DI extensions refactoring

**Outcome**: Production-ready, observable, secure application

### Phase 3: API Evolution (Sprint 5+)
**Goal**: Enable API evolution and future growth

8. Issue #8: API versioning

**Outcome**: Ability to evolve API without breaking existing clients

---

## Notes for Issue Creation

When creating these issues in GitHub:

1. **Copy each issue section** into a new GitHub issue
2. **Assign appropriate labels** as specified
3. **Add to project board** (Technical Debt project)
4. **Assign to appropriate team members**
5. **Link related issues** using GitHub's linking feature
6. **Add milestones** for sprint planning
7. **Reference ARCHITECTURAL_CODE_REVIEW.md** in each issue description

**Issue Templates** are ready to copy-paste directly into GitHub's issue creation interface.

---

**Document Created**: 2025-10-19
**Based On**: ARCHITECTURAL_CODE_REVIEW.md
**Ready For**: GitHub Issue Creation
