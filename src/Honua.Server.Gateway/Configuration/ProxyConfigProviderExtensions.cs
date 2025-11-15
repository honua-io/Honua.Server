// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace Honua.Server.Gateway.Configuration;

/// <summary>
/// Extension methods for configuring YARP reverse proxy with in-memory configuration provider.
/// </summary>
public static class ProxyConfigProviderExtensions
{
    /// <summary>
    /// Configures YARP to load configuration from memory with support for dynamic updates.
    /// This allows runtime configuration changes for blue-green deployments and traffic management.
    /// </summary>
    /// <param name="builder">The reverse proxy builder</param>
    /// <param name="configuration">Configuration containing the ReverseProxy section</param>
    /// <returns>The builder for method chaining</returns>
    /// <remarks>
    /// The configuration is read from IConfiguration under the "ReverseProxy" section.
    /// The InMemoryConfigProvider is registered as a singleton, allowing it to be injected
    /// and used for dynamic configuration updates throughout the application lifecycle.
    ///
    /// Example configuration structure:
    /// <code>
    /// {
    ///   "ReverseProxy": {
    ///     "Routes": {
    ///       "route1": {
    ///         "ClusterId": "cluster1",
    ///         "Match": {
    ///           "Path": "/api/{**catch-all}"
    ///         }
    ///       }
    ///     },
    ///     "Clusters": {
    ///       "cluster1": {
    ///         "Destinations": {
    ///           "destination1": {
    ///             "Address": "http://localhost:5000"
    ///           }
    ///         }
    ///       }
    ///     }
    ///   }
    /// }
    /// </code>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when builder or configuration is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when ReverseProxy configuration section is not found</exception>
    public static IReverseProxyBuilder LoadFromMemory(
        this IReverseProxyBuilder builder,
        IConfiguration configuration)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        // Get the ReverseProxy configuration section
        var proxyConfig = configuration.GetSection("ReverseProxy");
        if (!proxyConfig.Exists())
        {
            throw new InvalidOperationException(
                "ReverseProxy configuration section not found. Ensure appsettings.json contains a 'ReverseProxy' section.");
        }

        // Parse routes and clusters from configuration
        var routes = ParseRoutes(proxyConfig.GetSection("Routes"));
        var clusters = ParseClusters(proxyConfig.GetSection("Clusters"));

        // Register the InMemoryConfigProvider as a singleton
        builder.Services.AddSingleton<InMemoryConfigProvider>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<InMemoryConfigProvider>>();
            return new InMemoryConfigProvider(logger, routes, clusters);
        });

        // Register it as IProxyConfigProvider for YARP
        builder.Services.AddSingleton<IProxyConfigProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<InMemoryConfigProvider>());

        return builder;
    }

    /// <summary>
    /// Parses route configurations from the configuration section.
    /// </summary>
    private static IReadOnlyList<RouteConfig> ParseRoutes(IConfigurationSection routesSection)
    {
        var routes = new List<RouteConfig>();

        foreach (var section in routesSection.GetChildren())
        {
            var route = new RouteConfig
            {
                RouteId = section.Key,
                ClusterId = section["ClusterId"],
                AuthorizationPolicy = section["AuthorizationPolicy"],
                CorsPolicy = section["CorsPolicy"],
                Order = section.GetValue<int?>("Order"),
                MaxRequestBodySize = section.GetValue<long?>("MaxRequestBodySize")
            };

            // Parse Match section
            var matchSection = section.GetSection("Match");
            if (matchSection.Exists())
            {
                route = route with
                {
                    Match = new RouteMatch
                    {
                        Methods = matchSection.GetSection("Methods").Get<string[]>(),
                        Hosts = matchSection.GetSection("Hosts").Get<string[]>(),
                        Path = matchSection["Path"],
                        Headers = ParseHeaders(matchSection.GetSection("Headers")),
                        QueryParameters = ParseQueryParameters(matchSection.GetSection("QueryParameters"))
                    }
                };
            }

            // Parse Transforms section
            var transformsSection = section.GetSection("Transforms");
            if (transformsSection.Exists())
            {
                var transforms = new List<IReadOnlyDictionary<string, string>>();
                foreach (var transformSection in transformsSection.GetChildren())
                {
                    var transform = new Dictionary<string, string>();
                    foreach (var kvp in transformSection.AsEnumerable(makePathsRelative: true))
                    {
                        if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                        {
                            transform[kvp.Key] = kvp.Value;
                        }
                    }
                    if (transform.Count > 0)
                    {
                        transforms.Add(transform);
                    }
                }
                route = route with { Transforms = transforms };
            }

            // Parse Metadata section
            var metadataSection = section.GetSection("Metadata");
            if (metadataSection.Exists())
            {
                var metadata = new Dictionary<string, string>();
                foreach (var kvp in metadataSection.AsEnumerable(makePathsRelative: true))
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }
                route = route with { Metadata = metadata };
            }

            routes.Add(route);
        }

        return routes;
    }

    /// <summary>
    /// Parses cluster configurations from the configuration section.
    /// </summary>
    private static IReadOnlyList<ClusterConfig> ParseClusters(IConfigurationSection clustersSection)
    {
        var clusters = new List<ClusterConfig>();

        foreach (var section in clustersSection.GetChildren())
        {
            var cluster = new ClusterConfig
            {
                ClusterId = section.Key,
                LoadBalancingPolicy = section["LoadBalancingPolicy"],
                SessionAffinity = ParseSessionAffinity(section.GetSection("SessionAffinity")),
                HealthCheck = ParseHealthCheck(section.GetSection("HealthCheck")),
                HttpClient = ParseHttpClient(section.GetSection("HttpClient")),
                HttpRequest = ParseHttpRequest(section.GetSection("HttpRequest")),
                Destinations = ParseDestinations(section.GetSection("Destinations")),
                Metadata = ParseMetadata(section.GetSection("Metadata"))
            };

            clusters.Add(cluster);
        }

        return clusters;
    }

    /// <summary>
    /// Parses header match criteria from configuration.
    /// </summary>
    private static IReadOnlyList<RouteHeader>? ParseHeaders(IConfigurationSection headersSection)
    {
        if (!headersSection.Exists())
        {
            return null;
        }

        var headers = new List<RouteHeader>();
        foreach (var section in headersSection.GetChildren())
        {
            headers.Add(new RouteHeader
            {
                Name = section["Name"] ?? section.Key,
                Values = section.GetSection("Values").Get<string[]>(),
                Mode = Enum.TryParse<HeaderMatchMode>(section["Mode"], out var mode) ? mode : HeaderMatchMode.ExactHeader,
                IsCaseSensitive = section.GetValue<bool>("IsCaseSensitive")
            });
        }
        return headers;
    }

    /// <summary>
    /// Parses query parameter match criteria from configuration.
    /// </summary>
    private static IReadOnlyList<RouteQueryParameter>? ParseQueryParameters(IConfigurationSection querySection)
    {
        if (!querySection.Exists())
        {
            return null;
        }

        var parameters = new List<RouteQueryParameter>();
        foreach (var section in querySection.GetChildren())
        {
            parameters.Add(new RouteQueryParameter
            {
                Name = section["Name"] ?? section.Key,
                Values = section.GetSection("Values").Get<string[]>(),
                Mode = Enum.TryParse<QueryParameterMatchMode>(section["Mode"], out var mode) ? mode : QueryParameterMatchMode.Exact,
                IsCaseSensitive = section.GetValue<bool>("IsCaseSensitive")
            });
        }
        return parameters;
    }

    /// <summary>
    /// Parses session affinity configuration.
    /// </summary>
    private static SessionAffinityConfig? ParseSessionAffinity(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new SessionAffinityConfig
        {
            Enabled = section.GetValue<bool>("Enabled"),
            Policy = section["Policy"],
            FailurePolicy = section["FailurePolicy"],
            AffinityKeyName = section["AffinityKeyName"],
            Cookie = ParseSessionAffinityCookie(section.GetSection("Cookie"))
        };
    }

    /// <summary>
    /// Parses session affinity cookie configuration.
    /// </summary>
    private static SessionAffinityCookieConfig? ParseSessionAffinityCookie(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new SessionAffinityCookieConfig
        {
            Path = section["Path"],
            Domain = section["Domain"],
            HttpOnly = section.GetValue<bool?>("HttpOnly"),
            SecurePolicy = Enum.TryParse<Microsoft.AspNetCore.Http.CookieSecurePolicy>(section["SecurePolicy"], out var policy)
                ? policy
                : (Microsoft.AspNetCore.Http.CookieSecurePolicy?)null,
            SameSite = Enum.TryParse<Microsoft.AspNetCore.Http.SameSiteMode>(section["SameSite"], out var sameSite)
                ? sameSite
                : (Microsoft.AspNetCore.Http.SameSiteMode?)null,
            Expiration = section.GetValue<TimeSpan?>("Expiration"),
            MaxAge = section.GetValue<TimeSpan?>("MaxAge"),
            IsEssential = section.GetValue<bool?>("IsEssential")
        };
    }

    /// <summary>
    /// Parses health check configuration.
    /// </summary>
    private static HealthCheckConfig? ParseHealthCheck(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new HealthCheckConfig
        {
            Passive = ParsePassiveHealthCheck(section.GetSection("Passive")),
            Active = ParseActiveHealthCheck(section.GetSection("Active")),
            AvailableDestinationsPolicy = section["AvailableDestinationsPolicy"]
        };
    }

    /// <summary>
    /// Parses passive health check configuration.
    /// </summary>
    private static PassiveHealthCheckConfig? ParsePassiveHealthCheck(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new PassiveHealthCheckConfig
        {
            Enabled = section.GetValue<bool?>("Enabled"),
            Policy = section["Policy"],
            ReactivationPeriod = section.GetValue<TimeSpan?>("ReactivationPeriod")
        };
    }

    /// <summary>
    /// Parses active health check configuration.
    /// </summary>
    private static ActiveHealthCheckConfig? ParseActiveHealthCheck(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new ActiveHealthCheckConfig
        {
            Enabled = section.GetValue<bool?>("Enabled"),
            Interval = section.GetValue<TimeSpan?>("Interval"),
            Timeout = section.GetValue<TimeSpan?>("Timeout"),
            Policy = section["Policy"],
            Path = section["Path"]
        };
    }

    /// <summary>
    /// Parses HTTP client configuration.
    /// </summary>
    private static HttpClientConfig? ParseHttpClient(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new HttpClientConfig
        {
            SslProtocols = Enum.TryParse<System.Security.Authentication.SslProtocols>(section["SslProtocols"], out var protocols)
                ? protocols
                : (System.Security.Authentication.SslProtocols?)null,
            DangerousAcceptAnyServerCertificate = section.GetValue<bool?>("DangerousAcceptAnyServerCertificate"),
            MaxConnectionsPerServer = section.GetValue<int?>("MaxConnectionsPerServer"),
            EnableMultipleHttp2Connections = section.GetValue<bool?>("EnableMultipleHttp2Connections"),
            RequestHeaderEncoding = section["RequestHeaderEncoding"],
            ResponseHeaderEncoding = section["ResponseHeaderEncoding"],
            WebProxy = ParseWebProxyConfig(section.GetSection("WebProxy"))
        };
    }

    /// <summary>
    /// Parses web proxy configuration.
    /// </summary>
    private static WebProxyConfig? ParseWebProxyConfig(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new WebProxyConfig
        {
            Address = section.GetValue<Uri?>("Address"),
            BypassOnLocal = section.GetValue<bool?>("BypassOnLocal"),
            UseDefaultCredentials = section.GetValue<bool?>("UseDefaultCredentials")
        };
    }

    /// <summary>
    /// Parses HTTP request configuration.
    /// </summary>
    private static ForwarderRequestConfig? ParseHttpRequest(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        return new ForwarderRequestConfig
        {
            ActivityTimeout = section.GetValue<TimeSpan?>("ActivityTimeout"),
            Version = section.GetValue<Version?>("Version"),
            VersionPolicy = Enum.TryParse<System.Net.Http.HttpVersionPolicy>(section["VersionPolicy"], out var policy)
                ? policy
                : (System.Net.Http.HttpVersionPolicy?)null,
            AllowResponseBuffering = section.GetValue<bool?>("AllowResponseBuffering")
        };
    }

    /// <summary>
    /// Parses destination configurations.
    /// </summary>
    private static IReadOnlyDictionary<string, DestinationConfig>? ParseDestinations(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var destinations = new Dictionary<string, DestinationConfig>();
        foreach (var destSection in section.GetChildren())
        {
            destinations[destSection.Key] = new DestinationConfig
            {
                Address = destSection["Address"] ?? string.Empty,
                Health = destSection["Health"],
                Metadata = ParseMetadata(destSection.GetSection("Metadata"))
            };
        }
        return destinations;
    }

    /// <summary>
    /// Parses metadata key-value pairs.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? ParseMetadata(IConfigurationSection section)
    {
        if (!section.Exists())
        {
            return null;
        }

        var metadata = new Dictionary<string, string>();
        foreach (var kvp in section.AsEnumerable(makePathsRelative: true))
        {
            if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }
        return metadata.Count > 0 ? metadata : null;
    }
}
