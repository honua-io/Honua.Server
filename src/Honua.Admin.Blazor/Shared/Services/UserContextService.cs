// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for accessing the current user's tenant and identity information.
/// Extracts tenant ID and user ID from JWT claims for use in Blazor components.
/// </summary>
/// <remarks>
/// SECURITY: This service is critical for multi-tenant isolation.
/// All API calls should use the tenant ID and user ID from this service
/// instead of hardcoded values to prevent data leakage across tenants.
/// </remarks>
public sealed class UserContextService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<UserContextService> _logger;

    public UserContextService(
        AuthenticationStateProvider authStateProvider,
        ILogger<UserContextService> logger)
    {
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current tenant ID from JWT claims.
    /// </summary>
    /// <returns>Tenant ID as a Guid, or throws if not authenticated or tenant claim missing.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user is not authenticated or tenant claim is missing.</exception>
    public async Task<Guid> GetTenantIdAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Attempted to get tenant ID for unauthenticated user");
            throw new InvalidOperationException("User is not authenticated. Please log in.");
        }

        // Try to find tenant claim (could be "tenant_id", "tenantId", or "tid")
        var tenantClaim = user.FindFirst("tenant_id")
                         ?? user.FindFirst("tenantId")
                         ?? user.FindFirst("tid");

        if (tenantClaim == null)
        {
            _logger.LogError("Tenant claim not found in user claims for user {User}", user.Identity.Name);
            throw new InvalidOperationException("Tenant ID not found in authentication claims. This is a configuration error.");
        }

        if (!Guid.TryParse(tenantClaim.Value, out var tenantId))
        {
            _logger.LogError("Invalid tenant ID format in claims: {TenantId}", tenantClaim.Value);
            throw new InvalidOperationException($"Invalid tenant ID format: {tenantClaim.Value}");
        }

        return tenantId;
    }

    /// <summary>
    /// Gets the current tenant ID, or returns a default value if not authenticated.
    /// </summary>
    /// <param name="defaultTenantId">Default tenant ID to use if not authenticated (for development only).</param>
    /// <returns>Tenant ID as a Guid.</returns>
    /// <remarks>
    /// WARNING: Use this method only in development environments.
    /// Production code should use GetTenantIdAsync() which throws on missing claims.
    /// </remarks>
    public async Task<Guid> GetTenantIdOrDefaultAsync(Guid defaultTenantId)
    {
        try
        {
            return await GetTenantIdAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tenant ID from claims, using default: {DefaultTenantId}", defaultTenantId);
            return defaultTenantId;
        }
    }

    /// <summary>
    /// Gets the current user ID from JWT claims.
    /// </summary>
    /// <returns>User ID as a Guid, or throws if not authenticated or user ID claim missing.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user is not authenticated or user ID claim is missing.</exception>
    public async Task<Guid> GetUserIdAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogWarning("Attempted to get user ID for unauthenticated user");
            throw new InvalidOperationException("User is not authenticated. Please log in.");
        }

        // Try to find user ID claim (could be "sub", "user_id", "userId", or NameIdentifier)
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                         ?? user.FindFirst("sub")
                         ?? user.FindFirst("user_id")
                         ?? user.FindFirst("userId");

        if (userIdClaim == null)
        {
            _logger.LogError("User ID claim not found in user claims for user {User}", user.Identity.Name);
            throw new InvalidOperationException("User ID not found in authentication claims. This is a configuration error.");
        }

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
        {
            _logger.LogError("Invalid user ID format in claims: {UserId}", userIdClaim.Value);
            throw new InvalidOperationException($"Invalid user ID format: {userIdClaim.Value}");
        }

        return userId;
    }

    /// <summary>
    /// Gets the current user ID, or returns a default value if not authenticated.
    /// </summary>
    /// <param name="defaultUserId">Default user ID to use if not authenticated (for development only).</param>
    /// <returns>User ID as a Guid.</returns>
    /// <remarks>
    /// WARNING: Use this method only in development environments.
    /// Production code should use GetUserIdAsync() which throws on missing claims.
    /// </remarks>
    public async Task<Guid> GetUserIdOrDefaultAsync(Guid defaultUserId)
    {
        try
        {
            return await GetUserIdAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user ID from claims, using default: {DefaultUserId}", defaultUserId);
            return defaultUserId;
        }
    }

    /// <summary>
    /// Gets the current user's name from claims.
    /// </summary>
    /// <returns>User name, or "Unknown" if not found.</returns>
    public async Task<string> GetUserNameAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return "Anonymous";
        }

        return user.Identity?.Name ?? user.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }

    /// <summary>
    /// Gets the current user's email from claims.
    /// </summary>
    /// <returns>User email, or null if not found.</returns>
    public async Task<string?> GetUserEmailAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            return null;
        }

        return user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;
    }

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    /// <returns>True if user is authenticated, false otherwise.</returns>
    public async Task<bool> IsAuthenticatedAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        return authState.User.Identity?.IsAuthenticated ?? false;
    }

    /// <summary>
    /// Gets both tenant ID and user ID in a single call for efficiency.
    /// </summary>
    /// <returns>Tuple containing (tenantId, userId).</returns>
    public async Task<(Guid tenantId, Guid userId)> GetContextAsync()
    {
        var authState = await _authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (!user.Identity?.IsAuthenticated ?? true)
        {
            throw new InvalidOperationException("User is not authenticated. Please log in.");
        }

        // Extract tenant ID
        var tenantClaim = user.FindFirst("tenant_id")
                         ?? user.FindFirst("tenantId")
                         ?? user.FindFirst("tid");

        if (tenantClaim == null || !Guid.TryParse(tenantClaim.Value, out var tenantId))
        {
            // For development: use a default tenant ID
            // TODO: Remove this fallback in production
            tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            _logger.LogWarning("Using default tenant ID for development. This should not happen in production!");
        }

        // Extract user ID
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                         ?? user.FindFirst("sub")
                         ?? user.FindFirst("user_id")
                         ?? user.FindFirst("userId");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            // For development: use a default user ID
            // TODO: Remove this fallback in production
            userId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            _logger.LogWarning("Using default user ID for development. This should not happen in production!");
        }

        return (tenantId, userId);
    }
}
