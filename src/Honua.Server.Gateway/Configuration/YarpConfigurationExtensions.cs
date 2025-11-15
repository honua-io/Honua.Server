// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Honua.Server.Gateway.Configuration;

/// <summary>
/// Extension methods for loading YARP configuration into InMemoryConfigProvider
/// </summary>
public static class YarpConfigurationExtensions
{
    /// <summary>
    /// Loads YARP configuration from IConfiguration and returns an InMemoryConfigProvider.
    /// This allows the initial static configuration to be loaded from appsettings.json
    /// while still enabling dynamic updates at runtime.
    /// </summary>
    /// <param name="configuration">Configuration section containing ReverseProxy settings</param>
    /// <param name="logger">Logger for the InMemoryConfigProvider</param>
    /// <returns>InMemoryConfigProvider initialized with the configuration</returns>
    public static InMemoryConfigProvider LoadFromConfiguration(
        IConfigurationSection configuration,
        ILogger<InMemoryConfigProvider> logger)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        // Load routes
        var routesSection = configuration.GetSection("Routes");
        foreach (var routeSection in routesSection.GetChildren())
        {
            var route = new RouteConfig
            {
                RouteId = routeSection.Key,
                ClusterId = routeSection["ClusterId"],
                Order = routeSection.GetValue<int?>("Order"),
                Match = LoadRouteMatch(routeSection.GetSection("Match")),
                Metadata = LoadMetadata(routeSection.GetSection("Metadata")),
                Transforms = LoadTransforms(routeSection.GetSection("Transforms"))
            };

            routes.Add(route);
        }

        // Load clusters
        var clustersSection = configuration.GetSection("Clusters");
        foreach (var clusterSection in clustersSection.GetChildren())
        {
            var cluster = new ClusterConfig
            {
                ClusterId = clusterSection.Key,
                Destinations = LoadDestinations(clusterSection.GetSection("Destinations")),
                HealthCheck = LoadHealthCheck(clusterSection.GetSection("HealthCheck")),
                HttpRequest = LoadHttpRequest(clusterSection.GetSection("HttpRequest")),
                LoadBalancingPolicy = clusterSection["LoadBalancingPolicy"],
                Metadata = LoadMetadata(clusterSection.GetSection("Metadata"))
            };

            clusters.Add(cluster);
        }

        return new InMemoryConfigProvider(logger, routes, clusters);
    }

    private static RouteMatch LoadRouteMatch(IConfigurationSection section)
    {
        if (!section.Exists())
            return new RouteMatch();

        return new RouteMatch
        {
            Path = section["Path"],
            Hosts = section.GetSection("Hosts").Get<string[]>()?.ToList(),
            Methods = section.GetSection("Methods").Get<string[]>()?.ToList(),
            Headers = LoadRouteHeaders(section.GetSection("Headers"))
        };
    }

    private static IReadOnlyList<RouteHeader>? LoadRouteHeaders(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        var headers = new List<RouteHeader>();
        foreach (var headerSection in section.GetChildren())
        {
            headers.Add(new RouteHeader
            {
                Name = headerSection["Name"] ?? headerSection.Key,
                Values = headerSection.GetSection("Values").Get<string[]>()?.ToList(),
                Mode = Enum.TryParse<HeaderMatchMode>(headerSection["Mode"], out var mode)
                    ? mode
                    : HeaderMatchMode.ExactHeader,
                IsCaseSensitive = headerSection.GetValue<bool>("IsCaseSensitive")
            });
        }
        return headers;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>>? LoadTransforms(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        var transforms = new List<IReadOnlyDictionary<string, string>>();
        foreach (var transformSection in section.GetChildren())
        {
            var transform = new Dictionary<string, string>();
            foreach (var kvp in transformSection.AsEnumerable(makePathsRelative: true))
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                {
                    transform[kvp.Key] = kvp.Value;
                }
            }
            if (transform.Count > 0)
            {
                transforms.Add(transform);
            }
        }
        return transforms.Count > 0 ? transforms : null;
    }

    private static IReadOnlyDictionary<string, DestinationConfig> LoadDestinations(IConfigurationSection section)
    {
        var destinations = new Dictionary<string, DestinationConfig>();

        foreach (var destSection in section.GetChildren())
        {
            destinations[destSection.Key] = new DestinationConfig
            {
                Address = destSection["Address"] ?? string.Empty,
                Health = destSection["Health"],
                Metadata = LoadMetadata(destSection.GetSection("Metadata"))
            };
        }

        return destinations;
    }

    private static HealthCheckConfig? LoadHealthCheck(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        return new HealthCheckConfig
        {
            Active = LoadActiveHealthCheck(section.GetSection("Active")),
            Passive = LoadPassiveHealthCheck(section.GetSection("Passive")),
            AvailableDestinationsPolicy = section["AvailableDestinationsPolicy"]
        };
    }

    private static ActiveHealthCheckConfig? LoadActiveHealthCheck(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        return new ActiveHealthCheckConfig
        {
            Enabled = section.GetValue<bool>("Enabled"),
            Interval = section.GetValue<TimeSpan?>("Interval"),
            Timeout = section.GetValue<TimeSpan?>("Timeout"),
            Policy = section["Policy"],
            Path = section["Path"]
        };
    }

    private static PassiveHealthCheckConfig? LoadPassiveHealthCheck(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        return new PassiveHealthCheckConfig
        {
            Enabled = section.GetValue<bool>("Enabled"),
            Policy = section["Policy"],
            ReactivationPeriod = section.GetValue<TimeSpan?>("ReactivationPeriod")
        };
    }

    private static ForwarderRequestConfig? LoadHttpRequest(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        return new ForwarderRequestConfig
        {
            ActivityTimeout = section.GetValue<TimeSpan?>("Timeout"),
            Version = section.GetValue<Version?>("Version"),
            VersionPolicy = Enum.TryParse<System.Net.Http.HttpVersionPolicy>(section["VersionPolicy"], out var policy)
                ? policy
                : (System.Net.Http.HttpVersionPolicy?)null
        };
    }

    private static IReadOnlyDictionary<string, string>? LoadMetadata(IConfigurationSection section)
    {
        if (!section.Exists())
            return null;

        var metadata = new Dictionary<string, string>();
        foreach (var kvp in section.AsEnumerable(makePathsRelative: true))
        {
            if (!string.IsNullOrEmpty(kvp.Value))
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        return metadata.Count > 0 ? metadata : null;
    }
}
