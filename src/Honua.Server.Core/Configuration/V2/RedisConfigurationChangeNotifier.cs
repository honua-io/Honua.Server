// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Redis-backed implementation of configuration change notifications using Pub/Sub.
/// Enables distributed configuration change notifications across multiple server instances in high availability deployments.
/// </summary>
/// <remarks>
/// This implementation uses Redis Pub/Sub to broadcast configuration changes to all subscribed server instances.
/// When a configuration file changes on one server, all other servers in the cluster are notified via Redis.
/// </remarks>
public sealed class RedisConfigurationChangeNotifier : IConfigurationChangeNotifier, IAsyncDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisConfigurationChangeNotifier> _logger;
    private readonly HonuaHighAvailabilityOptions _options;
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions;
    private readonly SemaphoreSlim _subscribeLock;
    private ISubscriber? _subscriber;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisConfigurationChangeNotifier"/> class.
    /// </summary>
    /// <param name="redis">Redis connection multiplexer.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="options">High availability configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public RedisConfigurationChangeNotifier(
        IConnectionMultiplexer redis,
        ILogger<RedisConfigurationChangeNotifier> logger,
        IOptions<HonuaHighAvailabilityOptions> options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _subscriptions = new ConcurrentDictionary<Guid, Subscription>();
        _subscribeLock = new SemaphoreSlim(1, 1);

        if (!_options.Enabled)
        {
            throw new InvalidOperationException(
                "RedisConfigurationChangeNotifier requires HighAvailability to be enabled. " +
                "Set 'HighAvailability:Enabled' to true in configuration.");
        }

        if (string.IsNullOrWhiteSpace(_options.RedisConnectionString))
        {
            throw new InvalidOperationException(
                "RedisConnectionString is required when HighAvailability is enabled. " +
                "Set 'HighAvailability:RedisConnectionString' in configuration.");
        }

        _logger.LogInformation(
            "RedisConfigurationChangeNotifier initialized. Channel: {Channel}",
            _options.ConfigurationChannel);
    }

    /// <inheritdoc/>
    public async Task NotifyConfigurationChangedAsync(string configPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentNullException(nameof(configPath));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RedisConfigurationChangeNotifier));
        }

        try
        {
            var subscriber = GetSubscriber();
            var channel = new RedisChannel(_options.ConfigurationChannel, RedisChannel.PatternMode.Literal);

            var subscriberCount = await subscriber.PublishAsync(channel, configPath);

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Published configuration change notification for '{ConfigPath}' to {SubscriberCount} subscribers on channel '{Channel}'",
                    configPath,
                    subscriberCount,
                    _options.ConfigurationChannel);
            }
            else
            {
                _logger.LogInformation(
                    "Published configuration change notification for '{ConfigPath}' to {SubscriberCount} subscribers",
                    configPath,
                    subscriberCount);
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish configuration change notification. Redis connection error for config: {ConfigPath}",
                configPath);
            throw new InvalidOperationException(
                $"Redis connection error when publishing configuration change for '{configPath}'", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error publishing configuration change notification for: {ConfigPath}",
                configPath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IDisposable> SubscribeAsync(
        Func<string, Task> onConfigChanged,
        CancellationToken cancellationToken = default)
    {
        if (onConfigChanged == null)
        {
            throw new ArgumentNullException(nameof(onConfigChanged));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RedisConfigurationChangeNotifier));
        }

        var subscriptionId = Guid.NewGuid();

        try
        {
            await _subscribeLock.WaitAsync(cancellationToken);
            try
            {
                var subscriber = GetSubscriber();
                var channel = new RedisChannel(_options.ConfigurationChannel, RedisChannel.PatternMode.Literal);

                // Create subscription handler
                async void MessageHandler(RedisChannel ch, RedisValue value)
                {
                    var configPath = value.ToString();

                    if (_options.EnableDetailedLogging)
                    {
                        _logger.LogDebug(
                            "Received configuration change notification for '{ConfigPath}' on channel '{Channel}'",
                            configPath,
                            ch);
                    }

                    try
                    {
                        await onConfigChanged(configPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error in configuration change handler for '{ConfigPath}'",
                            configPath);
                    }
                }

                // Subscribe to Redis channel
                await subscriber.SubscribeAsync(channel, MessageHandler);

                // Store subscription for cleanup
                var subscription = new Subscription(subscriptionId, channel, MessageHandler, this);
                _subscriptions[subscriptionId] = subscription;

                _logger.LogInformation(
                    "Subscribed to configuration changes on channel '{Channel}' (subscription: {SubscriptionId})",
                    _options.ConfigurationChannel,
                    subscriptionId);

                return subscription;
            }
            finally
            {
                _subscribeLock.Release();
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Failed to subscribe to configuration changes. Redis connection error");
            throw new InvalidOperationException("Redis connection error when subscribing to configuration changes", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error subscribing to configuration changes");
            throw;
        }
    }

    /// <summary>
    /// Gets the Redis subscriber, initializing it if necessary.
    /// </summary>
    private ISubscriber GetSubscriber()
    {
        if (_subscriber == null)
        {
            if (!_redis.IsConnected)
            {
                throw new InvalidOperationException(
                    "Redis is not connected. Ensure Redis connection string is valid and Redis server is accessible.");
            }

            _subscriber = _redis.GetSubscriber();
        }

        return _subscriber;
    }

    /// <summary>
    /// Unsubscribes from a specific subscription.
    /// </summary>
    private async Task UnsubscribeAsync(Guid subscriptionId)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            return;
        }

        try
        {
            await _subscribeLock.WaitAsync();
            try
            {
                var subscriber = GetSubscriber();
                await subscriber.UnsubscribeAsync(subscription.Channel, subscription.Handler);

                _logger.LogInformation(
                    "Unsubscribed from configuration changes (subscription: {SubscriptionId})",
                    subscriptionId);
            }
            finally
            {
                _subscribeLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error unsubscribing from configuration changes (subscription: {SubscriptionId})",
                subscriptionId);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            // Unsubscribe from all subscriptions
            var unsubscribeTasks = _subscriptions.Keys
                .Select(id => UnsubscribeAsync(id))
                .ToArray();

            await Task.WhenAll(unsubscribeTasks);

            _subscribeLock.Dispose();

            _logger.LogInformation("RedisConfigurationChangeNotifier disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RedisConfigurationChangeNotifier");
        }
    }

    /// <summary>
    /// Represents a subscription to configuration changes.
    /// </summary>
    private sealed class Subscription : IDisposable
    {
        private readonly Guid _id;
        private readonly RedisConfigurationChangeNotifier _notifier;
        private bool _disposed;

        public RedisChannel Channel { get; }
        public Action<RedisChannel, RedisValue> Handler { get; }

        public Subscription(
            Guid id,
            RedisChannel channel,
            Action<RedisChannel, RedisValue> handler,
            RedisConfigurationChangeNotifier notifier)
        {
            _id = id;
            Channel = channel;
            Handler = handler;
            _notifier = notifier;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Fire and forget unsubscribe
            _ = _notifier.UnsubscribeAsync(_id);
        }
    }
}
