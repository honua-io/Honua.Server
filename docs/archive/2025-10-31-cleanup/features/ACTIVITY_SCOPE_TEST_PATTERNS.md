# ActivityScope Test Patterns

## Overview

`ActivityScope` provides simplified Activity management for test scenarios, eliminating boilerplate code while maintaining full OpenTelemetry tracing capabilities.

## Benefits

- **Reduces boilerplate**: Eliminates ~5-10 lines per Activity usage
- **Automatic error handling**: Records errors and sets appropriate status codes automatically
- **Null-safe operations**: Handles disabled tracing gracefully
- **Synchronous support**: Includes both sync and async overloads for test scenarios

## Migration Patterns

### Pattern 1: Simple Activity Creation

**Before (Traditional):**
```csharp
using var activity = testActivitySource.StartActivity("Test Operation");
activity?.SetTag("test.name", testName);
// ... test code ...
```
**Lines: 3+**

**After (ActivityScope):**
```csharp
ActivityScope.Execute(
    testActivitySource,
    "Test Operation",
    [("test.name", testName)],
    activity =>
    {
        // ... test code ...
    });
```
**Lines: 7 (but with automatic error handling)**

**Line savings when including error handling:**
- Traditional with try/catch: ~12 lines
- ActivityScope: 7 lines
- **Savings: ~5 lines per usage**

---

### Pattern 2: Activity with Error Handling

**Before (Traditional):**
```csharp
using var activity = testActivitySource.StartActivity("Test Operation");
activity?.SetTag("test.type", "integration");
try
{
    var result = PerformTestOperation();
    activity?.SetTag("test.result", result);
    activity?.SetStatus(ActivityStatusCode.Ok);
    return result;
}
catch (Exception ex)
{
    activity?.SetTag("error", true);
    activity?.SetTag("error.message", ex.Message);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    throw;
}
```
**Lines: 16**

**After (ActivityScope):**
```csharp
return ActivityScope.Execute(
    testActivitySource,
    "Test Operation",
    [("test.type", "integration")],
    activity =>
    {
        var result = PerformTestOperation();
        activity.AddTag("test.result", result);
        return result;
    });
```
**Lines: 10**

**Savings: ~6 lines** ✓

---

### Pattern 3: Nested Activities

**Before (Traditional):**
```csharp
using var parentActivity = testActivitySource.StartActivity("Parent");
parentActivity?.SetTag("test.parent", true);

using var childActivity = testActivitySource.StartActivity("Child");
childActivity?.SetTag("test.child", true);
// ... test code ...
```
**Lines: 6+**

**After (ActivityScope):**
```csharp
ActivityScope.Execute(
    testActivitySource,
    "Parent",
    [("test.parent", true)],
    parent =>
    {
        ActivityScope.Execute(
            testActivitySource,
            "Child",
            [("test.child", true)],
            child =>
            {
                // ... test code ...
            });
    });
```
**Lines: 15 (but with automatic error handling for both levels)**

**Note**: While this looks longer, it includes automatic error handling and status management for both parent and child activities.

---

### Pattern 4: Builder Pattern for Conditional Configuration

**Before (Traditional):**
```csharp
using var activity = testActivitySource.StartActivity("Test");
activity?.SetTag("test.name", testName);
if (debugMode)
{
    activity?.SetTag("test.debug", true);
    activity?.SetTag("test.timestamp", DateTime.UtcNow);
}
// ... test code ...
```
**Lines: 7+**

**After (ActivityScope):**
```csharp
var builder = ActivityScope.Create(testActivitySource, "Test")
    .WithTag("test.name", testName);

if (debugMode)
{
    builder.WithTag("test.debug", true);
    builder.WithTag("test.timestamp", DateTime.UtcNow);
}

builder.Execute(activity =>
{
    // ... test code ...
});
```
**Lines: 12 (but clearer separation and automatic error handling)**

---

### Pattern 5: Void Operations (No Return Value)

**Before (Traditional):**
```csharp
using var activity = testActivitySource.StartActivity("Setup");
activity?.SetTag("operation.type", "setup");
try
{
    PerformSetup();
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetTag("error", true);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    throw;
}
```
**Lines: 12**

**After (ActivityScope):**
```csharp
ActivityScope.Execute(
    testActivitySource,
    "Setup",
    [("operation.type", "setup")],
    activity => PerformSetup());
```
**Lines: 5**

**Savings: ~7 lines** ✓✓

---

## Example Test File

See `tests/Honua.Server.Core.Tests/Observability/ActivityScopeTestExamples.cs` for complete working examples.

## When to Use ActivityScope in Tests

### ✓ Good Use Cases:
- **Test infrastructure helpers** - Shared setup/teardown code with Activities
- **Integration test instrumentation** - Tracing test workflows
- **Test utilities** - Reusable test helpers that create Activities
- **Performance test harnesses** - Measuring operation timing

### ✗ Avoid in These Cases:
- **Unit tests verifying Activity behavior** - Testing Activity functionality itself should use raw Activity APIs
- **Simple assertions** - Tests that just verify Activity creation don't need ActivityScope
- **Listener validation tests** - Tests checking ActivityListener behavior

## Line Count Savings

Based on common patterns:

| Pattern | Traditional Lines | ActivityScope Lines | Savings |
|---------|------------------|-------------------|---------|
| Simple Activity | 3 | 7 | -4* |
| Activity with error handling | 16 | 10 | **+6** |
| Void operation with error handling | 12 | 5 | **+7** |
| Nested activities (2 levels) | 12+ | 15 | -3* |
| Builder pattern | 7+ | 12 | -5* |

\* *When error handling is included (recommended), ActivityScope saves lines*

**Target savings**: 30-50 lines when migrating 5-7 Activity patterns that require proper error handling.

## Best Practices

1. **Use synchronous `Execute` for sync tests**: Keeps test code simple
2. **Use async `ExecuteAsync` for integration tests**: Matches async test patterns
3. **Prefer tags array syntax**: `[("key", value)]` is more concise than multiple `WithTag` calls
4. **Use builder pattern for complex scenarios**: Conditional tag addition, parent context
5. **Leverage null-safe `AddTag`**: Works even when tracing is disabled

## Migration Checklist

When migrating test infrastructure to ActivityScope:

- [ ] Identify Activity patterns with manual error handling
- [ ] Replace `using var activity = ...` with `ActivityScope.Execute`
- [ ] Convert manual tags to tags array parameter
- [ ] Remove manual try/catch blocks around Activity code
- [ ] Remove manual `SetStatus` calls (handled automatically)
- [ ] Test with tracing enabled and disabled
- [ ] Verify error scenarios still record properly

## Future Enhancements

Potential additions to ActivityScope for test scenarios:

- `ExecuteWithAssertion<T>`: Combine execution with assertion
- `ExecuteAndVerify`: Validate Activity tags after execution
- `MockActivityScope`: Test double for non-instrumented tests

## See Also

- [ActivityScope Source](../../src/Honua.Server.Core/Observability/ActivityScope.cs)
- [ActivityScope Test Examples](../../tests/Honua.Server.Core.Tests/Observability/ActivityScopeTestExamples.cs)
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
