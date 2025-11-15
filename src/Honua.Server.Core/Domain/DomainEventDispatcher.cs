// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Domain;

/// <summary>
/// Dispatcher for domain events that invokes all registered handlers for each event type.
/// </summary>
public class DomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DomainEventDispatcher> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEventDispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving event handlers.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    public DomainEventDispatcher(
        IServiceProvider serviceProvider,
        ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Dispatches a single domain event to all registered handlers.
    /// </summary>
    /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
    /// <param name="domainEvent">The domain event to dispatch.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous dispatch operation.</returns>
    public async Task DispatchAsync<TDomainEvent>(
        TDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
        where TDomainEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = domainEvent.GetType();
        _logger.LogDebug(
            "Dispatching domain event {EventType} with ID {EventId}",
            eventType.Name,
            domainEvent.EventId);

        var handlers = _serviceProvider.GetServices<IDomainEventHandler<TDomainEvent>>();

        var handlerTasks = handlers.Select(handler =>
            HandleEventAsync(handler, domainEvent, cancellationToken));

        await Task.WhenAll(handlerTasks);

        _logger.LogDebug(
            "Completed dispatching domain event {EventType} to {HandlerCount} handler(s)",
            eventType.Name,
            handlers.Count());
    }

    /// <summary>
    /// Dispatches multiple domain events to their registered handlers.
    /// </summary>
    /// <param name="domainEvents">The collection of domain events to dispatch.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous dispatch operation.</returns>
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            await DispatchEventAsync(domainEvent, cancellationToken);
        }
    }

    /// <summary>
    /// Dispatches a domain event by resolving its type dynamically.
    /// </summary>
    /// <param name="domainEvent">The domain event to dispatch.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous dispatch operation.</returns>
    private async Task DispatchEventAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        _logger.LogDebug(
            "Dispatching domain event {EventType} with ID {EventId}",
            eventType.Name,
            domainEvent.EventId);

        var handlers = _serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
            if (handleMethod != null)
            {
                var task = (Task?)handleMethod.Invoke(handler, [domainEvent, cancellationToken]);
                if (task != null)
                {
                    await task;
                }
            }
        }

        _logger.LogDebug(
            "Completed dispatching domain event {EventType} to {HandlerCount} handler(s)",
            eventType.Name,
            handlers.Count());
    }

    /// <summary>
    /// Handles a single event with error handling and logging.
    /// </summary>
    /// <typeparam name="TDomainEvent">The type of the domain event.</typeparam>
    /// <param name="handler">The handler to invoke.</param>
    /// <param name="domainEvent">The domain event to handle.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous handle operation.</returns>
    private async Task HandleEventAsync<TDomainEvent>(
        IDomainEventHandler<TDomainEvent> handler,
        TDomainEvent domainEvent,
        CancellationToken cancellationToken)
        where TDomainEvent : IDomainEvent
    {
        try
        {
            await handler.HandleAsync(domainEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling domain event {EventType} with handler {HandlerType}",
                domainEvent.GetType().Name,
                handler.GetType().Name);
            throw;
        }
    }
}
