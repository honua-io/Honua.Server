// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Handles authorization for collection resources based on configured policies.
/// </summary>
public sealed class CollectionAuthorizationHandler : IResourceAuthorizationHandler
{
    private readonly IResourceAuthorizationCache _cache;
    private readonly ResourceAuthorizationMetrics _metrics;
    private readonly ILogger<CollectionAuthorizationHandler> _logger;
    private readonly IOptionsMonitor<ResourceAuthorizationOptions> _options;

    public string ResourceType => "collection";

    public CollectionAuthorizationHandler(
        IResourceAuthorizationCache cache,
        ResourceAuthorizationMetrics metrics,
        ILogger<CollectionAuthorizationHandler> logger,
        IOptionsMonitor<ResourceAuthorizationOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ResourceAuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        string resourceType,
        string resourceId,
        string operation,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(resourceType, ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return ResourceAuthorizationResult.Fail($"Resource type '{resourceType}' is not supported by this handler");
        }

        return await PerformanceMeasurement.MeasureAsync(
            _logger,
            "AuthorizeCollection",
            async () =>
            {
                try
                {
                    var options = _options.CurrentValue;

                    // Check if authorization is disabled
                    if (!options.Enabled)
                    {
                        _logger.LogTrace("Resource authorization is disabled, allowing access to collection {CollectionId}", resourceId);
                        return ResourceAuthorizationResult.Success();
                    }

                    // Build cache key
                    var userId = GetUserId(user);
                    var cacheKey = ResourceAuthorizationCache.BuildCacheKey(userId, resourceType, resourceId, operation);

                    // Check cache
                    if (_cache.TryGet(cacheKey, out var cachedResult))
                    {
                        _metrics.RecordAuthorizationCheck(resourceType, operation, cachedResult.Succeeded, 0, true);
                        return cachedResult;
                    }

                    // Evaluate policies
                    var result = await EvaluatePoliciesAsync(user, resourceId, operation, options, cancellationToken);

                    // Cache result
                    _cache.Set(cacheKey, result);

                    _metrics.RecordAuthorizationCheck(resourceType, operation, result.Succeeded, 0, false);

                    return result;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during authorization check for collection {CollectionId}, operation {Operation}", resourceId, operation);
                    _metrics.RecordAuthorizationCheck(resourceType, operation, false, 0, false);
                    return ResourceAuthorizationResult.Fail($"Authorization check failed: {ex.Message}");
                }
            },
            LogLevel.Debug).ConfigureAwait(false);
    }

    private Task<ResourceAuthorizationResult> EvaluatePoliciesAsync(
        ClaimsPrincipal user,
        string resourceId,
        string operation,
        ResourceAuthorizationOptions options,
        CancellationToken cancellationToken)
    {
        // If user is not authenticated in enforced mode, deny
        if (options.DefaultAction == DefaultAction.Deny && (user?.Identity?.IsAuthenticated != true))
        {
            return Task.FromResult(ResourceAuthorizationResult.Fail("User is not authenticated"));
        }

        // Get user roles
        var userRoles = user?.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .ToList() ?? new List<string>();

        var userId = GetUserId(user);

        // Filter applicable policies
        var applicablePolicies = options.Policies
            .Where(p => p.Enabled)
            .Where(p => string.Equals(p.ResourceType, ResourceType, StringComparison.OrdinalIgnoreCase))
            .Where(p => p.MatchesResource(resourceId))
            .Where(p => p.AllowsOperation(operation))
            .Where(p => p.AppliesTo(userRoles) || p.AppliesToUser(userId))
            .OrderByDescending(p => p.Priority)
            .ToList();

        if (applicablePolicies.Count > 0)
        {
            var matchedPolicy = applicablePolicies.First();
            _logger.LogDebug(
                "Collection authorization granted for {UserId} on collection {CollectionId} (operation: {Operation}) via policy {PolicyId}",
                userId,
                resourceId,
                operation,
                matchedPolicy.Id);
            return Task.FromResult(ResourceAuthorizationResult.Success());
        }

        // Check if user has administrator role (super-user access)
        if (userRoles.Contains("administrator", StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Collection authorization granted for administrator {UserId} on collection {CollectionId} (operation: {Operation})",
                userId,
                resourceId,
                operation);
            return Task.FromResult(ResourceAuthorizationResult.Success());
        }

        // Apply default action
        if (options.DefaultAction == DefaultAction.Allow)
        {
            _logger.LogDebug(
                "Collection authorization granted by default policy for {UserId} on collection {CollectionId} (operation: {Operation})",
                userId,
                resourceId,
                operation);
            return Task.FromResult(ResourceAuthorizationResult.Success());
        }

        _logger.LogWarning(
            "Collection authorization denied for {UserId} on collection {CollectionId} (operation: {Operation})",
            userId,
            resourceId,
            operation);

        return Task.FromResult(ResourceAuthorizationResult.Fail("No matching policy found and default action is deny"));
    }

    private static string GetUserId(ClaimsPrincipal? user)
    {
        return user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value
            ?? user?.Identity?.Name
            ?? "anonymous";
    }
}
