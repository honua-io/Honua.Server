// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2;

/// <summary>
/// Defines a contract for notifying and subscribing to configuration changes.
/// Supports both local in-process notifications and distributed notifications via Redis.
/// </summary>
public interface IConfigurationChangeNotifier
{
    /// <summary>
    /// Notifies all subscribers that a configuration has changed.
    /// In a high availability setup, this publishes the change notification to all server instances.
    /// </summary>
    /// <param name="configPath">
    /// The path to the configuration that changed.
    /// Example: "/etc/honua/config.hcl" or "appsettings.json"
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous notification operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when configPath is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the notifier is not initialized or connection is lost.</exception>
    Task NotifyConfigurationChangedAsync(string configPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to configuration change notifications.
    /// The provided callback will be invoked whenever a configuration change is detected.
    /// </summary>
    /// <param name="onConfigChanged">
    /// A callback function to invoke when configuration changes.
    /// Receives the configuration path that changed.
    /// The callback should be idempotent and handle errors gracefully.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the subscription.</param>
    /// <returns>
    /// A disposable subscription handle. Disposing this handle unsubscribes from notifications.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when onConfigChanged is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the notifier is not initialized.</exception>
    Task<IDisposable> SubscribeAsync(Func<string, Task> onConfigChanged, CancellationToken cancellationToken = default);
}
