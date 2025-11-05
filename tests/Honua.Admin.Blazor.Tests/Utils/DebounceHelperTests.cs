// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Admin.Blazor.Shared.Utils;

namespace Honua.Admin.Blazor.Tests.Utils;

public class DebounceHelperTests : IDisposable
{
    private readonly DebounceHelper _debouncer;

    public DebounceHelperTests()
    {
        _debouncer = new DebounceHelper(delayMilliseconds: 100);
    }

    public void Dispose()
    {
        _debouncer.Dispose();
    }

    [Fact]
    public async Task Debounce_WhenCalledOnce_ShouldExecuteAfterDelay()
    {
        // Arrange
        var executed = false;

        // Act
        _debouncer.Debounce(async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Assert - should not execute immediately
        executed.Should().BeFalse();

        // Wait for debounce delay
        await Task.Delay(150);

        // Assert - should execute after delay
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Debounce_WhenCalledMultipleTimes_ShouldExecuteOnlyOnce()
    {
        // Arrange
        var executionCount = 0;

        // Act - call debounce multiple times rapidly
        _debouncer.Debounce(async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        await Task.Delay(50);

        _debouncer.Debounce(async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        await Task.Delay(50);

        _debouncer.Debounce(async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        // Wait for final execution
        await Task.Delay(150);

        // Assert - should execute only the last action
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task Debounce_WithSynchronousAction_ShouldExecuteAfterDelay()
    {
        // Arrange
        var executed = false;

        // Act
        _debouncer.Debounce(() => executed = true);

        // Assert - should not execute immediately
        executed.Should().BeFalse();

        // Wait for debounce delay
        await Task.Delay(150);

        // Assert - should execute after delay
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Debounce_WithSynchronousAction_WhenCalledMultipleTimes_ShouldExecuteOnlyOnce()
    {
        // Arrange
        var executionCount = 0;

        // Act - call debounce multiple times rapidly
        _debouncer.Debounce(() => executionCount++);
        await Task.Delay(50);
        _debouncer.Debounce(() => executionCount++);
        await Task.Delay(50);
        _debouncer.Debounce(() => executionCount++);

        // Wait for final execution
        await Task.Delay(150);

        // Assert - should execute only the last action
        executionCount.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_ShouldPreventExecution()
    {
        // Arrange
        var executed = false;

        // Act
        _debouncer.Debounce(async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        await Task.Delay(50);

        _debouncer.Cancel();

        // Wait beyond debounce delay
        await Task.Delay(150);

        // Assert - should not execute because it was cancelled
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task FlushAsync_ShouldExecuteImmediately()
    {
        // Arrange
        var executed = false;

        // Act
        _debouncer.Debounce(async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Assert - should not execute immediately
        executed.Should().BeFalse();

        // Flush immediately
        await _debouncer.FlushAsync();

        // Assert - should execute immediately after flush
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task FlushAsync_WhenNoPendingAction_ShouldNotThrow()
    {
        // Act & Assert - should not throw
        await _debouncer.FlushAsync();
    }

    [Fact]
    public async Task Debounce_WithException_ShouldNotThrow()
    {
        // Arrange
        var executed = false;

        // Act - debounce action that throws
        _debouncer.Debounce(async () =>
        {
            executed = true;
            await Task.CompletedTask;
            throw new InvalidOperationException("Test exception");
        });

        // Wait for execution
        await Task.Delay(150);

        // Assert - exception should be swallowed, but action should execute
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Debounce_AfterDispose_ShouldNotExecute()
    {
        // Arrange
        var executed = false;
        var debouncer = new DebounceHelper(delayMilliseconds: 100);

        // Act
        debouncer.Debounce(async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        debouncer.Dispose();

        // Wait beyond debounce delay
        await Task.Delay(150);

        // Assert - should not execute after dispose
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task Debounce_WithCustomDelay_ShouldRespectDelay()
    {
        // Arrange
        var debouncer = new DebounceHelper(delayMilliseconds: 300);
        var executed = false;

        // Act
        debouncer.Debounce(async () =>
        {
            executed = true;
            await Task.CompletedTask;
        });

        // Assert - should not execute after short delay
        await Task.Delay(150);
        executed.Should().BeFalse();

        // Wait for full delay
        await Task.Delay(200);

        // Assert - should execute after full delay
        executed.Should().BeTrue();

        debouncer.Dispose();
    }

    [Fact]
    public async Task Debounce_WhenResetBeforeExecution_ShouldResetTimer()
    {
        // Arrange
        var firstExecuted = false;
        var secondExecuted = false;

        // Act - first debounce
        _debouncer.Debounce(async () =>
        {
            firstExecuted = true;
            await Task.CompletedTask;
        });

        await Task.Delay(50);

        // Reset timer with new action
        _debouncer.Debounce(async () =>
        {
            secondExecuted = true;
            await Task.CompletedTask;
        });

        // Wait for execution
        await Task.Delay(150);

        // Assert - only second action should execute
        firstExecuted.Should().BeFalse();
        secondExecuted.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDebouncer_Extension_ShouldCreateDebouncer()
    {
        // Arrange
        var executed = false;
        Func<Task> action = async () =>
        {
            executed = true;
            await Task.CompletedTask;
        };

        // Act
        using var debouncer = action.CreateDebouncer(delayMilliseconds: 100);
        debouncer.Debounce(action);

        await Task.Delay(150);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDebouncer_SynchronousExtension_ShouldCreateDebouncer()
    {
        // Arrange
        var executed = false;
        Action action = () => executed = true;

        // Act
        using var debouncer = action.CreateDebouncer(delayMilliseconds: 100);
        debouncer.Debounce(action);

        await Task.Delay(150);

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Debounce_MultipleSequences_ShouldExecuteEachSequence()
    {
        // Arrange
        var executionCount = 0;

        // Act - first sequence
        _debouncer.Debounce(async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        await Task.Delay(150);

        // Second sequence
        _debouncer.Debounce(async () =>
        {
            executionCount++;
            await Task.CompletedTask;
        });

        await Task.Delay(150);

        // Assert - both sequences should execute
        executionCount.Should().Be(2);
    }
}
