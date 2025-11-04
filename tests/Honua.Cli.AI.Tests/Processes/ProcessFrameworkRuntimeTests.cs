using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Cli.AI.Tests.Processes;

/// <summary>
/// Runtime behavior tests for Process Framework.
/// Tests state persistence, error handling, retry logic, cancellation, and concurrency.
/// </summary>
[Trait("Category", "ProcessFramework")]
[Trait("Category", "Runtime")]
[Collection("ProcessFramework")]
public class ProcessFrameworkRuntimeTests
{
    private readonly ITestOutputHelper _output;

    public ProcessFrameworkRuntimeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region State Persistence Tests - InMemoryProcessStateStore

    [Fact]
    public async Task InMemoryStore_SaveProcess_PersistsCorrectly()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-process-1",
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "ValidateRequirements",
            CompletionPercentage = 10,
            StartTime = DateTime.UtcNow
        };

        // Act
        await store.SaveProcessAsync(processInfo);
        var retrieved = await store.GetProcessAsync("test-process-1");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.ProcessId.Should().Be("test-process-1");
        retrieved.WorkflowType.Should().Be("Deployment");
        retrieved.Status.Should().Be("Running");
        retrieved.CurrentStep.Should().Be("ValidateRequirements");
        retrieved.CompletionPercentage.Should().Be(10);

        _output.WriteLine($"Process saved and retrieved: {retrieved.ProcessId}");
    }

    [Fact]
    public async Task InMemoryStore_UpdateProcessStatus_UpdatesCorrectly()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-process-2",
            WorkflowType = "Upgrade",
            Status = "Running",
            CurrentStep = "DetectVersion",
            CompletionPercentage = 25,
            StartTime = DateTime.UtcNow
        };
        await store.SaveProcessAsync(processInfo);

        // Act
        await store.UpdateProcessStatusAsync("test-process-2", "Running", 50);
        var updated = await store.GetProcessAsync("test-process-2");

        // Assert
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Running");
        updated.CompletionPercentage.Should().Be(50);

        _output.WriteLine($"Process status updated to {updated.CompletionPercentage}%");
    }

    [Fact]
    public async Task InMemoryStore_UpdateProcessStatus_CompletedSetsEndTime()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-process-3",
            WorkflowType = "Metadata",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };
        await store.SaveProcessAsync(processInfo);

        // Act
        await store.UpdateProcessStatusAsync("test-process-3", "Completed", 100);
        var completed = await store.GetProcessAsync("test-process-3");

        // Assert
        completed.Should().NotBeNull();
        completed!.Status.Should().Be("Completed");
        completed.CompletionPercentage.Should().Be(100);
        completed.EndTime.Should().NotBeNull();
        completed.EndTime.Should().BeAfter(completed.StartTime);

        _output.WriteLine($"Process completed at {completed.EndTime}");
    }

    [Fact]
    public async Task InMemoryStore_UpdateProcessStatus_FailedSetsEndTimeAndError()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-process-4",
            WorkflowType = "GitOps",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };
        await store.SaveProcessAsync(processInfo);

        // Act
        await store.UpdateProcessStatusAsync("test-process-4", "Failed", 45, "Network timeout");
        var failed = await store.GetProcessAsync("test-process-4");

        // Assert
        failed.Should().NotBeNull();
        failed!.Status.Should().Be("Failed");
        failed.CompletionPercentage.Should().Be(45);
        failed.ErrorMessage.Should().Be("Network timeout");
        failed.EndTime.Should().NotBeNull();

        _output.WriteLine($"Process failed with error: {failed.ErrorMessage}");
    }

    [Fact]
    public async Task InMemoryStore_GetActiveProcesses_ReturnsOnlyRunningAndPending()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        await store.SaveProcessAsync(new ProcessInfo { ProcessId = "p1", Status = "Running", WorkflowType = "Deploy", StartTime = DateTime.UtcNow });
        await store.SaveProcessAsync(new ProcessInfo { ProcessId = "p2", Status = "Pending", WorkflowType = "Upgrade", StartTime = DateTime.UtcNow });
        await store.SaveProcessAsync(new ProcessInfo { ProcessId = "p3", Status = "Completed", WorkflowType = "Metadata", StartTime = DateTime.UtcNow });
        await store.SaveProcessAsync(new ProcessInfo { ProcessId = "p4", Status = "Failed", WorkflowType = "GitOps", StartTime = DateTime.UtcNow });

        // Act
        var activeProcesses = await store.GetActiveProcessesAsync();

        // Assert
        activeProcesses.Should().HaveCount(2);
        activeProcesses.Should().Contain(p => p.ProcessId == "p1");
        activeProcesses.Should().Contain(p => p.ProcessId == "p2");

        _output.WriteLine($"Active processes: {activeProcesses.Count}");
    }

    [Fact]
    public async Task InMemoryStore_DeleteProcess_RemovesProcess()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-delete",
            WorkflowType = "Benchmark",
            Status = "Completed",
            StartTime = DateTime.UtcNow
        };
        await store.SaveProcessAsync(processInfo);

        // Act
        var deleted = await store.DeleteProcessAsync("test-delete");
        var retrieved = await store.GetProcessAsync("test-delete");

        // Assert
        deleted.Should().BeTrue();
        retrieved.Should().BeNull();

        _output.WriteLine("Process successfully deleted");
    }

    [Fact]
    public async Task InMemoryStore_CancelProcess_UpdatesStatusToCancelled()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-cancel",
            WorkflowType = "Deployment",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };
        await store.SaveProcessAsync(processInfo);

        // Act
        var cancelled = await store.CancelProcessAsync("test-cancel");
        var retrieved = await store.GetProcessAsync("test-cancel");

        // Assert
        cancelled.Should().BeTrue();
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be("Cancelled");

        _output.WriteLine("Process successfully cancelled");
    }

    [Fact]
    public async Task InMemoryStore_CancelProcess_OnlyWorksForRunningOrPending()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-completed",
            WorkflowType = "Metadata",
            Status = "Completed",
            StartTime = DateTime.UtcNow
        };
        await store.SaveProcessAsync(processInfo);

        // Act
        var cancelled = await store.CancelProcessAsync("test-completed");
        var retrieved = await store.GetProcessAsync("test-completed");

        // Assert
        cancelled.Should().BeFalse();
        retrieved!.Status.Should().Be("Completed"); // Should not change

        _output.WriteLine("Cannot cancel completed process - correctly rejected");
    }

    [Fact]
    public async Task InMemoryStore_UpdateProcessStatus_ThrowsForNonExistentProcess()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpdateProcessStatusAsync("non-existent", "Running"));

        exception.Message.Should().Contain("not found");
        _output.WriteLine($"Correctly threw exception: {exception.Message}");
    }

    [Fact]
    public async Task InMemoryStore_GetProcess_ThrowsForNullOrEmptyId()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        // Act & Assert
        // VSTHRD110: These assertions observe the task result by awaiting the exception
        _ = await Assert.ThrowsAsync<ArgumentException>(() => store.GetProcessAsync(""));
        _ = await Assert.ThrowsAsync<ArgumentException>(() => store.GetProcessAsync(null!));
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task RetryHelper_ExecuteWithRetry_SucceedsOnFirstAttempt()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var callCount = 0;
        Func<Task<string>> operation = async () =>
        {
            callCount++;
            await Task.Delay(10);
            return "Success";
        };

        // Act
        var result = await ProcessStepRetryHelper.ExecuteWithRetryAsync(
            operation,
            logger,
            "TestOperation");

        // Assert
        result.Should().Be("Success");
        callCount.Should().Be(1);
        _output.WriteLine($"Operation succeeded on first attempt");
    }

    [Fact]
    public async Task RetryHelper_ExecuteWithRetry_RetriesOnTransientFailure()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var callCount = 0;
        Func<Task<string>> operation = async () =>
        {
            callCount++;
            await Task.Delay(10);
            if (callCount < 3)
            {
                throw new HttpRequestException("Network error");
            }
            return "Success";
        };

        // Act
        var result = await ProcessStepRetryHelper.ExecuteWithRetryAsync(
            operation,
            logger,
            "TestOperation",
            maxRetries: 3);

        // Assert
        result.Should().Be("Success");
        callCount.Should().Be(3);
        _output.WriteLine($"Operation succeeded after {callCount} attempts");
    }

    [Fact]
    public async Task RetryHelper_ExecuteWithRetry_FailsAfterMaxRetries()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var callCount = 0;
        Func<Task<string>> operation = async () =>
        {
            callCount++;
            await Task.Delay(10);
            throw new HttpRequestException("Persistent network error");
        };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            ProcessStepRetryHelper.ExecuteWithRetryAsync(
                operation,
                logger,
                "TestOperation",
                maxRetries: 3));

        // Should be 1 initial + 3 retries = 4 total attempts
        callCount.Should().Be(4);
        _output.WriteLine($"Operation failed after {callCount} attempts");
    }

    [Fact]
    public async Task RetryHelper_ExecuteWithRetry_DoesNotRetryValidationErrors()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var callCount = 0;
        Func<Task<string>> operation = async () =>
        {
            callCount++;
            await Task.Delay(10);
            throw new ArgumentException("Invalid parameter");
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            ProcessStepRetryHelper.ExecuteWithRetryAsync(
                operation,
                logger,
                "TestOperation",
                maxRetries: 3));

        // Should only be called once - no retries for validation errors
        callCount.Should().Be(1);
        _output.WriteLine($"Validation error - no retries (call count: {callCount})");
    }

    [Fact]
    public void RetryHelper_IsRetryableException_IdentifiesTransientErrors()
    {
        // Arrange & Act & Assert
        ProcessStepRetryHelper.IsRetryableException(new HttpRequestException()).Should().BeTrue();
        ProcessStepRetryHelper.IsRetryableException(new TimeoutException()).Should().BeTrue();
        ProcessStepRetryHelper.IsRetryableException(new TaskCanceledException()).Should().BeTrue();
        ProcessStepRetryHelper.IsRetryableException(new IOException("File locked")).Should().BeTrue();

        _output.WriteLine("Transient exceptions correctly identified");
    }

    [Fact]
    public void RetryHelper_IsRetryableException_IdentifiesNonRetryableErrors()
    {
        // Arrange & Act & Assert
        ProcessStepRetryHelper.IsRetryableException(new ArgumentException()).Should().BeFalse();
        ProcessStepRetryHelper.IsRetryableException(new ArgumentNullException()).Should().BeFalse();
        ProcessStepRetryHelper.IsRetryableException(new InvalidOperationException()).Should().BeFalse();
        ProcessStepRetryHelper.IsRetryableException(new UnauthorizedAccessException()).Should().BeFalse();

        _output.WriteLine("Non-retryable exceptions correctly identified");
    }

    [Fact]
    public async Task RetryHelper_ExecuteWithRetry_UsesExponentialBackoff()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var callTimes = new List<DateTime>();
        Func<Task<string>> operation = async () =>
        {
            callTimes.Add(DateTime.UtcNow);
            await Task.Delay(10);
            if (callTimes.Count < 4)
            {
                throw new TimeoutException("Timeout");
            }
            return "Success";
        };

        // Act
        await ProcessStepRetryHelper.ExecuteWithRetryAsync(
            operation,
            logger,
            "TestOperation",
            maxRetries: 3,
            initialDelayMs: 100,
            backoffMultiplier: 2.0);

        // Assert
        callTimes.Should().HaveCount(4); // 1 initial + 3 retries

        // Verify exponential backoff: delays should be ~100ms, ~200ms, ~400ms
        var delay1 = (callTimes[1] - callTimes[0]).TotalMilliseconds;
        var delay2 = (callTimes[2] - callTimes[1]).TotalMilliseconds;
        var delay3 = (callTimes[3] - callTimes[2]).TotalMilliseconds;

        delay1.Should().BeGreaterThanOrEqualTo(90); // ~100ms (with some tolerance)
        delay2.Should().BeGreaterThanOrEqualTo(180); // ~200ms
        delay3.Should().BeGreaterThanOrEqualTo(380); // ~400ms

        _output.WriteLine($"Backoff delays: {delay1:F0}ms, {delay2:F0}ms, {delay3:F0}ms");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task Process_CancellationToken_StopsExecution()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var executionStarted = false;
        var executionCompleted = false;

        Func<CancellationToken, Task> operation = async (ct) =>
        {
            executionStarted = true;
            await Task.Delay(5000, ct); // Long operation
            executionCompleted = true;
        };

        // Act
        // VSTHRD003: We intentionally create and manage the task separately to test cancellation
        var task = operation(cts.Token);
        await Task.Delay(100); // Let it start
        cts.Cancel();

        // Assert
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        executionStarted.Should().BeTrue();
        executionCompleted.Should().BeFalse();

        _output.WriteLine("Cancellation token correctly stopped execution");
    }

    [Fact]
    public async Task Process_CancellationToken_PropagatesThrough()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var innerCancelled = false;

        Func<CancellationToken, Task> innerOperation = async (ct) =>
        {
            try
            {
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                innerCancelled = true;
                throw;
            }
        };

        Func<CancellationToken, Task> outerOperation = async (ct) =>
        {
            await innerOperation(ct);
        };

        // Act
        // VSTHRD003: We intentionally create and manage the task separately to test cancellation propagation
        var task = outerOperation(cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        // Assert
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        innerCancelled.Should().BeTrue();

        _output.WriteLine("Cancellation token propagated through nested operations");
    }

    [Fact]
    public async Task StateStore_OperationsRespectCancellationToken()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Already cancelled

        // Act & Assert
        var processInfo = new ProcessInfo
        {
            ProcessId = "test",
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // These should complete even with cancelled token (they're synchronous)
        await store.SaveProcessAsync(processInfo, cts.Token);
        var result = await store.GetProcessAsync("test", cts.Token);

        result.Should().NotBeNull();
        _output.WriteLine("State store operations completed");
    }

    #endregion

    #region Concurrent Execution Tests

    [Fact]
    public async Task InMemoryStore_ConcurrentSaves_HandlesCorrectly()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var tasks = new List<Task>();

        // Act - Save 100 processes concurrently
        for (int i = 0; i < 100; i++)
        {
            var processId = $"process-{i}";
            tasks.Add(store.SaveProcessAsync(new ProcessInfo
            {
                ProcessId = processId,
                WorkflowType = "Concurrent",
                Status = "Running",
                StartTime = DateTime.UtcNow
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All should be saved
        var allProcesses = await store.GetActiveProcessesAsync();
        allProcesses.Should().HaveCount(100);

        _output.WriteLine($"Successfully saved {allProcesses.Count} processes concurrently");
    }

    [Fact]
    public async Task InMemoryStore_ConcurrentUpdates_HandlesCorrectly()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = "shared",
            WorkflowType = "Concurrent",
            Status = "Running",
            CompletionPercentage = 0,
            StartTime = DateTime.UtcNow
        });

        // Act - Update the same process concurrently
        var tasks = new List<Task>();
        for (int i = 1; i <= 10; i++)
        {
            var percentage = i * 10;
            tasks.Add(store.UpdateProcessStatusAsync("shared", "Running", percentage));
        }

        await Task.WhenAll(tasks);

        // Assert - Process should exist with one of the percentages
        var result = await store.GetProcessAsync("shared");
        result.Should().NotBeNull();
        result!.CompletionPercentage.Should().BeGreaterThan(0);

        _output.WriteLine($"Final completion percentage: {result.CompletionPercentage}%");
    }

    [Fact]
    public async Task InMemoryStore_ConcurrentGetActiveProcesses_ReturnsConsistently()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        // Add some processes
        for (int i = 0; i < 20; i++)
        {
            await store.SaveProcessAsync(new ProcessInfo
            {
                ProcessId = $"active-{i}",
                WorkflowType = "Test",
                Status = "Running",
                StartTime = DateTime.UtcNow
            });
        }

        // Act - Query active processes concurrently
        var tasks = new List<Task<IReadOnlyList<ProcessInfo>>>();
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(store.GetActiveProcessesAsync());
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All queries should return the same count
        results.Should().AllSatisfy(r => r.Should().HaveCount(20));

        _output.WriteLine($"All {results.Length} concurrent queries returned consistent results");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InMemoryStore_SaveProcess_ThrowsOnNullProcess()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SaveProcessAsync(null!));
    }

    [Fact]
    public async Task InMemoryStore_SaveProcess_ThrowsOnEmptyProcessId()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processInfo = new ProcessInfo
        {
            ProcessId = "", // Empty
            WorkflowType = "Test",
            Status = "Running",
            StartTime = DateTime.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.SaveProcessAsync(processInfo));
    }

    [Fact]
    public async Task InMemoryStore_DeleteProcess_ReturnsFalseForNonExistent()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        // Act
        var deleted = await store.DeleteProcessAsync("non-existent");

        // Assert
        deleted.Should().BeFalse();
        _output.WriteLine("Delete non-existent process returned false correctly");
    }

    [Fact]
    public async Task InMemoryStore_CancelProcess_ReturnsFalseForNonExistent()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);

        // Act
        var cancelled = await store.CancelProcessAsync("non-existent");

        // Assert
        cancelled.Should().BeFalse();
        _output.WriteLine("Cancel non-existent process returned false correctly");
    }

    #endregion

    #region ProcessInfo Validation Tests

    [Fact]
    public void ProcessInfo_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var processInfo = new ProcessInfo();

        // Assert
        processInfo.ProcessId.Should().BeEmpty();
        processInfo.WorkflowType.Should().BeEmpty();
        processInfo.Status.Should().Be("Running");
        processInfo.CurrentStep.Should().BeEmpty();
        processInfo.CompletionPercentage.Should().Be(0);
        processInfo.EndTime.Should().BeNull();
        processInfo.ErrorMessage.Should().BeNull();

        _output.WriteLine("ProcessInfo default values are correct");
    }

    [Fact]
    public void ProcessInfo_CanSetAllProperties()
    {
        // Arrange & Act
        var now = DateTime.UtcNow;
        var processInfo = new ProcessInfo
        {
            ProcessId = "test-123",
            WorkflowType = "Deployment",
            Status = "Completed",
            CurrentStep = "FinalStep",
            CompletionPercentage = 100,
            StartTime = now,
            EndTime = now.AddMinutes(5),
            ErrorMessage = null
        };

        // Assert
        processInfo.ProcessId.Should().Be("test-123");
        processInfo.WorkflowType.Should().Be("Deployment");
        processInfo.Status.Should().Be("Completed");
        processInfo.CurrentStep.Should().Be("FinalStep");
        processInfo.CompletionPercentage.Should().Be(100);
        processInfo.StartTime.Should().Be(now);
        processInfo.EndTime.Should().Be(now.AddMinutes(5));
        processInfo.ErrorMessage.Should().BeNull();

        _output.WriteLine("All ProcessInfo properties can be set");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task StateStore_CompleteWorkflow_TracksProgressCorrectly()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processId = "workflow-test";

        // Act - Simulate complete workflow lifecycle
        // 1. Start
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Deployment",
            Status = "Running",
            CurrentStep = "Validate",
            CompletionPercentage = 0,
            StartTime = DateTime.UtcNow
        });

        // 2. Progress through steps
        await store.UpdateProcessStatusAsync(processId, "Running", 25);
        await Task.Delay(50);
        await store.UpdateProcessStatusAsync(processId, "Running", 50);
        await Task.Delay(50);
        await store.UpdateProcessStatusAsync(processId, "Running", 75);
        await Task.Delay(50);

        // 3. Complete
        await store.UpdateProcessStatusAsync(processId, "Completed", 100);

        // Assert
        var finalProcess = await store.GetProcessAsync(processId);
        finalProcess.Should().NotBeNull();
        finalProcess!.Status.Should().Be("Completed");
        finalProcess.CompletionPercentage.Should().Be(100);
        finalProcess.EndTime.Should().NotBeNull();
        finalProcess.EndTime.Should().BeAfter(finalProcess.StartTime);

        var activeProcesses = await store.GetActiveProcessesAsync();
        activeProcesses.Should().NotContain(p => p.ProcessId == processId);

        _output.WriteLine($"Workflow completed in {(finalProcess.EndTime!.Value - finalProcess.StartTime).TotalMilliseconds:F0}ms");
    }

    [Fact]
    public async Task StateStore_FailedWorkflow_CapturesErrorCorrectly()
    {
        // Arrange
        var logger = NullLogger<InMemoryProcessStateStore>.Instance;
        var store = new InMemoryProcessStateStore(logger);
        var processId = "failed-workflow";

        // Act - Simulate workflow that fails
        await store.SaveProcessAsync(new ProcessInfo
        {
            ProcessId = processId,
            WorkflowType = "Upgrade",
            Status = "Running",
            CurrentStep = "BackupDatabase",
            CompletionPercentage = 30,
            StartTime = DateTime.UtcNow
        });

        await Task.Delay(50);
        await store.UpdateProcessStatusAsync(processId, "Failed", 30, "Database backup failed: connection timeout");

        // Assert
        var failedProcess = await store.GetProcessAsync(processId);
        failedProcess.Should().NotBeNull();
        failedProcess!.Status.Should().Be("Failed");
        failedProcess.CompletionPercentage.Should().Be(30);
        failedProcess.ErrorMessage.Should().Contain("Database backup failed");
        failedProcess.EndTime.Should().NotBeNull();

        _output.WriteLine($"Workflow failed with error: {failedProcess.ErrorMessage}");
    }

    #endregion
}
