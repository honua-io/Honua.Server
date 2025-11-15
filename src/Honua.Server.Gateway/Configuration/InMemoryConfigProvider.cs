// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Honua.Server.Gateway.Configuration;

/// <summary>
/// In-memory implementation of IProxyConfigProvider that supports dynamic configuration updates.
/// This provider allows runtime updates to routes and clusters, which is essential for
/// blue-green deployments, canary releases, and dynamic traffic management.
/// </summary>
/// <remarks>
/// Thread-safe implementation using locks to ensure configuration consistency.
/// Uses IChangeToken to notify YARP when configuration changes occur.
/// </remarks>
public sealed class InMemoryConfigProvider : IProxyConfigProvider
{
    private readonly ILogger<InMemoryConfigProvider> _logger;
    private readonly object _lock = new object();
    private volatile InMemoryConfig _config;

    /// <summary>
    /// Initializes a new instance of the InMemoryConfigProvider.
    /// </summary>
    /// <param name="logger">Logger for debugging and monitoring configuration changes</param>
    /// <param name="routes">Initial route configurations</param>
    /// <param name="clusters">Initial cluster configurations</param>
    public InMemoryConfigProvider(
        ILogger<InMemoryConfigProvider> logger,
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<ClusterConfig> clusters)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = new InMemoryConfig(routes, clusters, logger);

        _logger.LogInformation(
            "InMemoryConfigProvider initialized with {RouteCount} routes and {ClusterCount} clusters",
            routes.Count,
            clusters.Count);
    }

    /// <summary>
    /// Gets the current proxy configuration.
    /// </summary>
    /// <returns>The current proxy configuration with routes and clusters</returns>
    public IProxyConfig GetConfig()
    {
        return _config;
    }

    /// <summary>
    /// Updates the proxy configuration with new routes and clusters.
    /// This triggers a change notification to YARP, causing it to reload the configuration.
    /// </summary>
    /// <param name="routes">New route configurations</param>
    /// <param name="clusters">New cluster configurations</param>
    /// <remarks>
    /// This method is thread-safe and can be called from multiple threads.
    /// The change notification ensures YARP picks up the new configuration immediately.
    /// </remarks>
    public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        if (routes == null)
        {
            throw new ArgumentNullException(nameof(routes));
        }

        if (clusters == null)
        {
            throw new ArgumentNullException(nameof(clusters));
        }

        lock (_lock)
        {
            var oldConfig = _config;
            _config = new InMemoryConfig(routes, clusters, _logger);

            _logger.LogInformation(
                "Configuration updated: {RouteCount} routes, {ClusterCount} clusters (previous: {OldRouteCount} routes, {OldClusterCount} clusters)",
                routes.Count,
                clusters.Count,
                oldConfig.Routes.Count,
                oldConfig.Clusters.Count);

            // Signal the old config's change token to notify YARP of the update
            oldConfig.SignalChange();
        }
    }

    /// <summary>
    /// Internal implementation of IProxyConfig that holds the actual configuration.
    /// </summary>
    private sealed class InMemoryConfig : IProxyConfig
    {
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public InMemoryConfig(
            IReadOnlyList<RouteConfig> routes,
            IReadOnlyList<ClusterConfig> clusters,
            ILogger logger)
        {
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            Clusters = clusters ?? throw new ArgumentNullException(nameof(clusters));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        /// <summary>
        /// Gets the list of route configurations.
        /// </summary>
        public IReadOnlyList<RouteConfig> Routes { get; }

        /// <summary>
        /// Gets the list of cluster configurations.
        /// </summary>
        public IReadOnlyList<ClusterConfig> Clusters { get; }

        /// <summary>
        /// Gets the change token that signals when this configuration is superseded.
        /// </summary>
        public IChangeToken ChangeToken { get; }

        /// <summary>
        /// Signals that this configuration has been replaced by a new one.
        /// This triggers the change token, notifying YARP to reload configuration.
        /// </summary>
        public void SignalChange()
        {
            try
            {
                if (!_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                    _logger.LogDebug("Configuration change signaled");
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected if already disposed
                _logger.LogDebug("Attempted to signal change on disposed configuration");
            }
        }
    }
}
