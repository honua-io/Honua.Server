// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Health;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Health;

/// <summary>
/// Health check that verifies OIDC discovery endpoint is accessible.
/// Returns Degraded if unreachable (warn-only), not Unhealthy.
/// Caches discovery document for 15 minutes to avoid hammering the OIDC endpoint.
/// </summary>
public sealed class OidcDiscoveryHealthCheck : HealthCheckBase
{
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> authOptions;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache cache;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private const string CacheKeyPrefix = "OidcDiscoveryHealthCheck_";

    public OidcDiscoveryHealthCheck(
        IOptionsMonitor<HonuaAuthenticationOptions> authOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<OidcDiscoveryHealthCheck> logger,
        IMemoryCache cache)
        : base(logger, RequestTimeout)
    {
        this.authOptions = Guard.NotNull(authOptions);
        this.httpClientFactory = Guard.NotNull(httpClientFactory);
        this.cache = Guard.NotNull(cache);
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        var options = this.authOptions.CurrentValue;

        // Only check when OIDC mode is enabled
        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Oidc)
        {
            return HealthCheckResult.Healthy("OIDC mode not enabled");
        }

        if (options.Jwt.Authority.IsNullOrWhiteSpace())
        {
            return HealthCheckResult.Degraded("OIDC authority not configured");
        }

        var cacheKey = CacheKeyPrefix + options.Jwt.Authority;

        // Try to get cached result
        if (this.cache.TryGetValue<HealthCheckResult>(cacheKey, out var cachedResult))
        {
            Logger.LogDebug("Returning cached OIDC health check result for {Authority}", options.Jwt.Authority);
            return cachedResult;
        }

        var discoveryUrl = BuildDiscoveryUrl(options.Jwt.Authority);

        try
        {
            using var httpClient = this.httpClientFactory.CreateClient();
            httpClient.Timeout = RequestTimeout;

            var response = await httpClient.GetAsync(discoveryUrl, cancellationToken);

            HealthCheckResult result;
            if (response.IsSuccessStatusCode)
            {
                Logger.LogDebug("OIDC discovery endpoint is accessible: {DiscoveryUrl}", discoveryUrl);

                data["authority"] = options.Jwt.Authority;
                data["discovery_url"] = discoveryUrl;
                data["cached"] = false;
                data["cache_duration_minutes"] = CacheDuration.TotalMinutes;

                result = HealthCheckResult.Healthy(
                    $"OIDC discovery endpoint accessible at {discoveryUrl}",
                    data: data);
            }
            else
            {
                Logger.LogWarning(
                    "OIDC discovery endpoint returned {StatusCode}: {DiscoveryUrl}",
                    response.StatusCode,
                    discoveryUrl);

                data["authority"] = options.Jwt.Authority;
                data["status_code"] = (int)response.StatusCode;
                data["discovery_url"] = discoveryUrl;
                data["cached"] = false;

                result = HealthCheckResult.Degraded(
                    $"OIDC discovery endpoint returned {response.StatusCode}",
                    data: data);
            }

            // Cache the result
            this.cache.Set(cacheKey, result, CacheDuration);
            return result;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning(ex, "OIDC discovery endpoint is unreachable: {Authority}", options.Jwt.Authority);

            data["authority"] = options.Jwt.Authority;
            data["error"] = ex.Message;
            data["cached"] = false;

            var result = HealthCheckResult.Degraded(
                "OIDC discovery endpoint unreachable",
                ex,
                data: data);

            // Cache degraded results for a shorter duration (1 minute)
            this.cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));
            return result;
        }
    }

    protected override HealthCheckResult CreateUnhealthyResult(Exception ex, Dictionary<string, object> data)
    {
        var options = this.authOptions.CurrentValue;

        data["authority"] = options.Jwt.Authority;
        data["error"] = ex.Message;
        data["cached"] = false;

        Logger.LogOperationFailure(ex, "OIDC discovery health check", options.Jwt.Authority);

        // Return Degraded instead of Unhealthy for OIDC issues (warn-only)
        return HealthCheckResult.Degraded(
            "Unexpected error checking OIDC discovery",
            ex,
            data: data);
    }

    private static string BuildDiscoveryUrl(string authority)
    {
        var authorityUri = new Uri(authority.TrimEnd('/'));
        return new Uri(authorityUri, ".well-known/openid-configuration").ToString();
    }
}
