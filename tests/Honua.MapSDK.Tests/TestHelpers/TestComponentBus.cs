using Honua.MapSDK.Core;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Tests.TestHelpers;

/// <summary>
/// Test implementation of ComponentBus that tracks published messages
/// </summary>
public class TestComponentBus : ComponentBus
{
    /// <summary>
    /// All messages published through this bus
    /// </summary>
    public List<PublishedMessage> PublishedMessages { get; } = new();

    /// <summary>
    /// Whether to actually publish messages to subscribers
    /// </summary>
    public bool EnablePublishing { get; set; } = true;

    public TestComponentBus(ILogger<ComponentBus>? logger = null) : base(logger)
    {
    }

    public override async Task PublishAsync<TMessage>(TMessage message, string? source = null)
    {
        // Track the message
        PublishedMessages.Add(new PublishedMessage
        {
            MessageType = typeof(TMessage),
            Message = message,
            Source = source,
            Timestamp = DateTime.UtcNow
        });

        // Optionally publish to actual subscribers
        if (EnablePublishing)
        {
            await base.PublishAsync(message, source);
        }
    }

    /// <summary>
    /// Get all messages of a specific type
    /// </summary>
    public List<TMessage> GetMessages<TMessage>() where TMessage : class
    {
        return PublishedMessages
            .Where(m => m.MessageType == typeof(TMessage))
            .Select(m => (TMessage)m.Message)
            .ToList();
    }

    /// <summary>
    /// Get the last message of a specific type
    /// </summary>
    public TMessage? GetLastMessage<TMessage>() where TMessage : class
    {
        return GetMessages<TMessage>().LastOrDefault();
    }

    /// <summary>
    /// Clear all tracked messages
    /// </summary>
    public void ClearMessages()
    {
        PublishedMessages.Clear();
    }

    /// <summary>
    /// Wait for a message of a specific type (useful for async testing)
    /// </summary>
    public async Task<TMessage> WaitForMessageAsync<TMessage>(TimeSpan? timeout = null) where TMessage : class
    {
        var timeoutValue = timeout ?? TimeSpan.FromSeconds(5);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeoutValue)
        {
            var message = GetLastMessage<TMessage>();
            if (message != null)
            {
                return message;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No message of type {typeof(TMessage).Name} received within {timeoutValue}");
    }
}

/// <summary>
/// Represents a published message for tracking
/// </summary>
public class PublishedMessage
{
    public required Type MessageType { get; init; }
    public required object Message { get; init; }
    public string? Source { get; init; }
    public DateTime Timestamp { get; init; }
}
