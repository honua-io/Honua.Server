// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Honua.Server.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Domain.Tests;

/// <summary>
/// Unit tests for the DomainEventDispatcher class.
/// Tests event dispatching to registered handlers.
/// </summary>
[Trait("Category", "Unit")]
public class DomainEventDispatcherTests
{
    private readonly Mock<ILogger<DomainEventDispatcher>> _mockLogger;
    private readonly ServiceCollection _services;

    public DomainEventDispatcherTests()
    {
        _mockLogger = new Mock<ILogger<DomainEventDispatcher>>();
        _services = new ServiceCollection();
    }

    // Test domain events for testing purposes
    private record TestDomainEvent : DomainEvent;
    private record AnotherTestDomainEvent : DomainEvent;

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenServiceProviderIsNull()
    {
        // Arrange, Act & Assert
        var act = () => new DomainEventDispatcher(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("serviceProvider");
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();

        // Act & Assert
        var act = () => new DomainEventDispatcher(serviceProvider, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task DispatchAsync_ShouldCallHandler_WhenHandlerIsRegistered()
    {
        // Arrange
        var mockHandler = new Mock<IDomainEventHandler<TestDomainEvent>>();
        _services.AddSingleton(mockHandler.Object);
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();

        // Act
        await dispatcher.DispatchAsync(domainEvent);

        // Assert
        mockHandler.Verify(
            h => h.HandleAsync(domainEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldCallAllHandlers_WhenMultipleHandlersAreRegistered()
    {
        // Arrange
        var mockHandler1 = new Mock<IDomainEventHandler<TestDomainEvent>>();
        var mockHandler2 = new Mock<IDomainEventHandler<TestDomainEvent>>();
        var mockHandler3 = new Mock<IDomainEventHandler<TestDomainEvent>>();

        _services.AddSingleton(mockHandler1.Object);
        _services.AddSingleton(mockHandler2.Object);
        _services.AddSingleton(mockHandler3.Object);

        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();

        // Act
        await dispatcher.DispatchAsync(domainEvent);

        // Assert
        mockHandler1.Verify(
            h => h.HandleAsync(domainEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        mockHandler2.Verify(
            h => h.HandleAsync(domainEvent, It.IsAny<CancellationToken>()),
            Times.Once);
        mockHandler3.Verify(
            h => h.HandleAsync(domainEvent, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldNotThrow_WhenNoHandlersAreRegistered()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();

        // Act
        Func<Task> act = async () => await dispatcher.DispatchAsync(domainEvent);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_ShouldThrowArgumentNullException_WhenEventIsNull()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);

        // Act
        Func<Task> act = async () => await dispatcher.DispatchAsync<TestDomainEvent>(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DispatchAsync_ShouldPassCancellationToken_ToHandlers()
    {
        // Arrange
        var mockHandler = new Mock<IDomainEventHandler<TestDomainEvent>>();
        _services.AddSingleton(mockHandler.Object);
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();
        var cancellationToken = new CancellationToken();

        // Act
        await dispatcher.DispatchAsync(domainEvent, cancellationToken);

        // Assert
        mockHandler.Verify(
            h => h.HandleAsync(domainEvent, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldRethrowException_WhenHandlerThrows()
    {
        // Arrange
        var mockHandler = new Mock<IDomainEventHandler<TestDomainEvent>>();
        var expectedException = new InvalidOperationException("Handler error");
        mockHandler.Setup(h => h.HandleAsync(It.IsAny<TestDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        _services.AddSingleton(mockHandler.Object);
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();

        // Act
        Func<Task> act = async () => await dispatcher.DispatchAsync(domainEvent);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Handler error");
    }

    [Fact]
    public async Task DispatchAsync_MultipleEvents_ShouldDispatchAllEvents_WhenCalled()
    {
        // Arrange
        var mockHandler1 = new Mock<IDomainEventHandler<TestDomainEvent>>();
        var mockHandler2 = new Mock<IDomainEventHandler<AnotherTestDomainEvent>>();

        _services.AddSingleton(mockHandler1.Object);
        _services.AddSingleton(mockHandler2.Object);

        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);

        var events = new List<IDomainEvent>
        {
            new TestDomainEvent(),
            new AnotherTestDomainEvent(),
            new TestDomainEvent()
        };

        // Act
        await dispatcher.DispatchAsync(events);

        // Assert
        mockHandler1.Verify(
            h => h.HandleAsync(It.IsAny<TestDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        mockHandler2.Verify(
            h => h.HandleAsync(It.IsAny<AnotherTestDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_MultipleEvents_ShouldThrowArgumentNullException_WhenEventsIsNull()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);

        // Act
        Func<Task> act = async () => await dispatcher.DispatchAsync((IEnumerable<IDomainEvent>)null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DispatchAsync_MultipleEvents_ShouldHandleEmptyCollection_Gracefully()
    {
        // Arrange
        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var events = new List<IDomainEvent>();

        // Act
        Func<Task> act = async () => await dispatcher.DispatchAsync(events);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DispatchAsync_ShouldOnlyCallHandlersForCorrectEventType_WhenMultipleTypesRegistered()
    {
        // Arrange
        var mockHandler1 = new Mock<IDomainEventHandler<TestDomainEvent>>();
        var mockHandler2 = new Mock<IDomainEventHandler<AnotherTestDomainEvent>>();

        _services.AddSingleton(mockHandler1.Object);
        _services.AddSingleton(mockHandler2.Object);

        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();

        // Act
        await dispatcher.DispatchAsync(domainEvent);

        // Assert
        mockHandler1.Verify(
            h => h.HandleAsync(It.IsAny<TestDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockHandler2.Verify(
            h => h.HandleAsync(It.IsAny<AnotherTestDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_ShouldHandleMixedEventTypes_InCollection()
    {
        // Arrange
        var testEventHandler = new Mock<IDomainEventHandler<TestDomainEvent>>();
        var anotherEventHandler = new Mock<IDomainEventHandler<AnotherTestDomainEvent>>();

        _services.AddSingleton(testEventHandler.Object);
        _services.AddSingleton(anotherEventHandler.Object);

        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);

        var events = new List<IDomainEvent>
        {
            new TestDomainEvent(),
            new AnotherTestDomainEvent()
        };

        // Act
        await dispatcher.DispatchAsync(events);

        // Assert
        testEventHandler.Verify(
            h => h.HandleAsync(It.IsAny<TestDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        anotherEventHandler.Verify(
            h => h.HandleAsync(It.IsAny<AnotherTestDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ShouldExecuteHandlersInParallel_ForSingleEvent()
    {
        // Arrange
        var handler1Executed = false;
        var handler2Executed = false;
        var handler1Started = new TaskCompletionSource<bool>();
        var handler2Started = new TaskCompletionSource<bool>();

        var mockHandler1 = new Mock<IDomainEventHandler<TestDomainEvent>>();
        mockHandler1.Setup(h => h.HandleAsync(It.IsAny<TestDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                handler1Started.SetResult(true);
                await Task.Delay(100);
                handler1Executed = true;
            });

        var mockHandler2 = new Mock<IDomainEventHandler<TestDomainEvent>>();
        mockHandler2.Setup(h => h.HandleAsync(It.IsAny<TestDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                handler2Started.SetResult(true);
                await Task.Delay(100);
                handler2Executed = true;
            });

        _services.AddSingleton(mockHandler1.Object);
        _services.AddSingleton(mockHandler2.Object);

        var serviceProvider = _services.BuildServiceProvider();
        var dispatcher = new DomainEventDispatcher(serviceProvider, _mockLogger.Object);
        var domainEvent = new TestDomainEvent();

        // Act
        await dispatcher.DispatchAsync(domainEvent);

        // Assert
        handler1Executed.Should().BeTrue();
        handler2Executed.Should().BeTrue();
    }
}
