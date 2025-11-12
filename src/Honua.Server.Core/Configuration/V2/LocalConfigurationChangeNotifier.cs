// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Local in-process implementation of configuration change notifications using events.
/// Suitable for single-instance deployments where distributed notifications are not required.
/// </summary>
/// <remarks>
/// This implementation uses local events to notify subscribers within the same process.
/// Unlike <see cref="RedisConfigurationChangeNotifier"/>, changes are not propagated
/// across multiple server instances. Use this for development or single-server deployments.
/// </remarks>
public sealed class LocalConfigurationChangeNotifier : IConfigurationChangeNotifier, IDisposable
{
    private readonly ILogger<LocalConfigurationChangeNotifier> _logger;
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions;
    private readonly SemaphoreSlim _eventLock;
    private bool _disposed;

    /// <summary>
    /// Event raised when configuration changes are detected.
    /// </summary>
    private event Func<string, Task>? ConfigurationChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalConfigurationChangeNotifier"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public LocalConfigurationChangeNotifier(ILogger<LocalConfigurationChangeNotifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _subscriptions = new ConcurrentDictionary<Guid, Subscription>();
        _eventLock = new SemaphoreSlim(1, 1);

        _logger.LogInformation("LocalConfigurationChangeNotifier initialized (in-process mode)");
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
            throw new ObjectDisposedException(nameof(LocalConfigurationChangeNotifier));
        }

        await _eventLock.WaitAsync(cancellationToken);
        try
        {
            var handlers = ConfigurationChanged;
            if (handlers == null)
            {
                _logger.LogDebug(
                    "No subscribers for configuration change notification: {ConfigPath}",
                    configPath);
                return;
            }

            var invocationList = handlers.GetInvocationList();
            _logger.LogInformation(
                "Notifying {SubscriberCount} local subscribers of configuration change: {ConfigPath}",
                invocationList.Length,
                configPath);

            // Invoke all handlers in parallel
            var tasks = invocationList
                .Cast<Func<string, Task>>()
                .Select(handler => InvokeHandlerSafelyAsync(handler, configPath))
                .ToArray();

            await Task.WhenAll(tasks);
        }
        finally
        {
            _eventLock.Release();
        }
    }

    /// <inheritdoc/>
    public Task<IDisposable> SubscribeAsync(
        Func<string, Task> onConfigChanged,
        CancellationToken cancellationToken = default)
    {
        if (onConfigChanged == null)
        {
            throw new ArgumentNullException(nameof(onConfigChanged));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LocalConfigurationChangeNotifier));
        }

        var subscriptionId = Guid.NewGuid();

        // Add handler to event
        ConfigurationChanged += onConfigChanged;

        // Store subscription for tracking and cleanup
        var subscription = new Subscription(subscriptionId, onConfigChanged, this);
        _subscriptions[subscriptionId] = subscription;

        _logger.LogInformation(
            "Subscribed to local configuration changes (subscription: {SubscriptionId})",
            subscriptionId);

        return Task.FromResult<IDisposable>(subscription);
    }

    /// <summary>
    /// Safely invokes a handler, catching and logging any exceptions.
    /// </summary>
    private async Task InvokeHandlerSafelyAsync(Func<string, Task> handler, string configPath)
    {
        try
        {
            await handler(configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in local configuration change handler for '{ConfigPath}'",
                configPath);
        }
    }

    /// <summary>
    /// Unsubscribes a handler from configuration change notifications.
    /// </summary>
    private void Unsubscribe(Guid subscriptionId)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out var subscription))
        {
            return;
        }

        try
        {
            ConfigurationChanged -= subscription.Handler;

            _logger.LogInformation(
                "Unsubscribed from local configuration changes (subscription: {SubscriptionId})",
                subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error unsubscribing from local configuration changes (subscription: {SubscriptionId})",
                subscriptionId);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            // Clear all subscriptions
            foreach (var subscriptionId in _subscriptions.Keys.ToArray())
            {
                Unsubscribe(subscriptionId);
            }

            _subscriptions.Clear();
            ConfigurationChanged = null;

            _eventLock.Dispose();

            _logger.LogInformation("LocalConfigurationChangeNotifier disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing LocalConfigurationChangeNotifier");
        }
    }

    /// <summary>
    /// Represents a subscription to local configuration changes.
    /// </summary>
    private sealed class Subscription : IDisposable
    {
        private readonly Guid _id;
        private readonly LocalConfigurationChangeNotifier _notifier;
        private bool _disposed;

        public Func<string, Task> Handler { get; }

        public Subscription(
            Guid id,
            Func<string, Task> handler,
            LocalConfigurationChangeNotifier notifier)
        {
            _id = id;
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
            _notifier.Unsubscribe(_id);
        }
    }
}
