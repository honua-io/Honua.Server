// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace Honua.Server.Core.BlueGreen;

/// <summary>
/// In-memory configuration provider for YARP that allows dynamic updates
/// to routes and clusters without restarting the gateway.
///
/// This provider enables blue-green deployments and traffic management by
/// allowing runtime configuration changes.
/// </summary>
public sealed class InMemoryConfigProvider : IProxyConfigProvider
{
    private volatile InMemoryConfig _config;

    /// <summary>
    /// Initializes the provider with initial route and cluster configurations.
    /// </summary>
    /// <param name="routes">Initial routes</param>
    /// <param name="clusters">Initial clusters</param>
    public InMemoryConfigProvider(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        _config = new InMemoryConfig(routes, clusters);
    }

    /// <summary>
    /// Gets the current proxy configuration.
    /// </summary>
    public IProxyConfig GetConfig() => _config;

    /// <summary>
    /// Updates the proxy configuration with new routes and clusters.
    /// This triggers a configuration reload in YARP without restarting the gateway.
    /// </summary>
    /// <param name="routes">New routes configuration</param>
    /// <param name="clusters">New clusters configuration</param>
    public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        var oldConfig = _config;
        _config = new InMemoryConfig(routes, clusters);
        oldConfig.SignalChange();
    }

    private sealed class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();

        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
            Routes = routes;
            Clusters = clusters;
            ChangeToken = new CancellationChangeToken(_cts.Token);
        }

        public IReadOnlyList<RouteConfig> Routes { get; }

        public IReadOnlyList<ClusterConfig> Clusters { get; }

        public IChangeToken ChangeToken { get; }

        internal void SignalChange()
        {
            _cts.Cancel();
        }
    }
}
