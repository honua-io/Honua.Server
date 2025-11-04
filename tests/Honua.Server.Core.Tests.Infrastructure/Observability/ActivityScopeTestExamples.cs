using System;
using System.Diagnostics;
using Honua.Server.Core.Observability;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Observability;

/// <summary>
/// Example tests demonstrating how to use ActivityScope in test scenarios.
/// These examples show best practices for Activity instrumentation in tests.
/// </summary>
[Trait("Category", "Unit")]
public class ActivityScopeTestExamples
{
    private static readonly ActivitySource TestActivitySource = new("Honua.Tests.Examples");

    [Fact]
    public void Example_Simple_SynchronousActivityScope()
    {
        // Arrange
        using var listener = CreateTestListener();

        // Act & Assert - using ActivityScope.Execute eliminates boilerplate
        var result = ActivityScope.Execute(
            TestActivitySource,
            "Test Operation",
            activity =>
            {
                Assert.NotNull(activity);
                return 42;
            });

        Assert.Equal(42, result);
    }

    [Fact]
    public void Example_WithTags_SynchronousActivityScope()
    {
        // Arrange
        using var listener = CreateTestListener();
        var testId = "test-123";

        // Act - ActivityScope automatically handles tags
        ActivityScope.Execute(
            TestActivitySource,
            "Test Operation",
            [("test.id", testId), ("test.category", "unit")],
            activity =>
            {
                // Additional tags can be added during execution
                activity.AddTag("test.result", "success");

                Assert.NotNull(activity);
                Assert.Contains(activity.Tags, t => t.Key == "test.id" && (string?)t.Value == testId);
            });
    }

    [Fact]
    public void Example_BuilderPattern_ForComplexScenarios()
    {
        // Arrange
        using var listener = CreateTestListener();
        var testName = "ComplexTest";
        var includeDebugInfo = true;

        // Act - Builder pattern is useful for conditional configuration
        var builder = ActivityScope.Create(TestActivitySource, "Complex Operation")
            .WithTag("test.name", testName);

        if (includeDebugInfo)
        {
            builder.WithTag("test.debug", true);
            builder.WithTag("test.timestamp", DateTimeOffset.UtcNow);
        }

        var result = builder.Execute(activity =>
        {
            // Perform test operations
            return "completed";
        });

        Assert.Equal("completed", result);
    }

    [Fact]
    public void Example_ErrorHandling_AutomaticallyRecorded()
    {
        // Arrange
        using var listener = CreateTestListener();

        // Act & Assert - ActivityScope automatically records errors
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            ActivityScope.Execute(
                TestActivitySource,
                "Failing Operation",
                activity =>
                {
                    throw new InvalidOperationException("Test error");
                });
        });

        Assert.Equal("Test error", exception.Message);
    }

    [Fact]
    public void Example_VoidOperation_NoReturnValue()
    {
        // Arrange
        using var listener = CreateTestListener();
        var operationExecuted = false;

        // Act - Use void overload when no return value is needed
        ActivityScope.Execute(
            TestActivitySource,
            "Void Operation",
            [("operation.type", "void")],
            activity =>
            {
                operationExecuted = true;
                activity.AddTag("operation.completed", true);
            });

        // Assert
        Assert.True(operationExecuted);
    }

    [Fact]
    public void Example_CompareWithTraditionalPattern()
    {
        using var listener = CreateTestListener();

        // Traditional pattern (verbose, ~8 lines):
        using var traditionalActivity = TestActivitySource.StartActivity("Traditional");
        traditionalActivity?.SetTag("approach", "traditional");
        try
        {
            // ... test code ...
            traditionalActivity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            traditionalActivity?.SetTag("error", true);
            traditionalActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }

        // ActivityScope pattern (concise, ~5 lines):
        ActivityScope.Execute(
            TestActivitySource,
            "Modern",
            [("approach", "modern")],
            activity =>
            {
                // ... test code ...
                // Error handling and status recording is automatic
            });
    }

    private static ActivityListener CreateTestListener()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Honua.Tests"),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
