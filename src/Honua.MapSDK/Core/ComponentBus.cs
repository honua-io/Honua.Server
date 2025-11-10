// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Core;

/// <summary>
/// Message bus for loosely coupled communication between map components
/// Based on pub/sub pattern - components never reference each other directly
/// </summary>
public class ComponentBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private readonly ILogger<ComponentBus>? _logger;

    public ComponentBus(ILogger<ComponentBus>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to a message type with synchronous handler
    /// </summary>
    public void Subscribe<TMessage>(Action<MessageArgs<TMessage>> handler)
        where TMessage : class
    {
        var messageType = typeof(TMessage);

        if (!_subscriptions.ContainsKey(messageType))
        {
            _subscriptions[messageType] = new List<Delegate>();
        }

        _subscriptions[messageType].Add(handler);
        _logger?.LogDebug("Subscribed to {MessageType}", messageType.Name);
    }

    /// <summary>
    /// Subscribe to a message type with asynchronous handler
    /// </summary>
    public void Subscribe<TMessage>(Func<MessageArgs<TMessage>, Task> handler)
        where TMessage : class
    {
        var messageType = typeof(TMessage);

        if (!_subscriptions.ContainsKey(messageType))
        {
            _subscriptions[messageType] = new List<Delegate>();
        }

        _subscriptions[messageType].Add(handler);
        _logger?.LogDebug("Subscribed to {MessageType} (async)", messageType.Name);
    }

    /// <summary>
    /// Publish a message to all subscribers (async)
    /// </summary>
    public async Task PublishAsync<TMessage>(TMessage message, string? source = null)
        where TMessage : class
    {
        var messageType = typeof(TMessage);

        if (!_subscriptions.ContainsKey(messageType))
        {
            _logger?.LogTrace("No subscribers for {MessageType}", messageType.Name);
            return;
        }

        var args = new MessageArgs<TMessage>
        {
            Message = message,
            Source = source,
            Timestamp = DateTime.UtcNow,
            CorrelationId = Guid.NewGuid().ToString()
        };

        var handlers = _subscriptions[messageType].ToList();

        foreach (var handler in handlers)
        {
            try
            {
                if (handler is Func<MessageArgs<TMessage>, Task> asyncHandler)
                {
                    await asyncHandler(args);
                }
                else if (handler is Action<MessageArgs<TMessage>> syncHandler)
                {
                    syncHandler(args);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling message {MessageType} from {Source}",
                    messageType.Name, source ?? "unknown");
            }
        }

        _logger?.LogTrace("Published {MessageType} from {Source} to {Count} handlers",
            messageType.Name, source ?? "unknown", handlers.Count);
    }

    /// <summary>
    /// Publish a message synchronously (use sparingly - prefer PublishAsync)
    /// </summary>
    public void Publish<TMessage>(TMessage message, string? source = null)
        where TMessage : class
    {
        _ = PublishAsync(message, source);
    }

    /// <summary>
    /// Unsubscribe a specific handler
    /// </summary>
    public void Unsubscribe<TMessage>(Delegate handler)
        where TMessage : class
    {
        var messageType = typeof(TMessage);

        if (_subscriptions.ContainsKey(messageType))
        {
            _subscriptions[messageType].Remove(handler);
            _logger?.LogDebug("Unsubscribed from {MessageType}", messageType.Name);
        }
    }

    /// <summary>
    /// Clear all subscriptions (useful for testing)
    /// </summary>
    public void Clear()
    {
        _subscriptions.Clear();
        _logger?.LogDebug("Cleared all subscriptions");
    }

    /// <summary>
    /// Get count of subscribers for a message type
    /// </summary>
    public int GetSubscriberCount<TMessage>() where TMessage : class
    {
        var messageType = typeof(TMessage);
        return _subscriptions.ContainsKey(messageType)
            ? _subscriptions[messageType].Count
            : 0;
    }
}

/// <summary>
/// Wrapper for messages passed to handlers
/// </summary>
public class MessageArgs<TMessage> where TMessage : class
{
    public required TMessage Message { get; init; }
    public string? Source { get; init; }
    public DateTime Timestamp { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
}
