// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Custom authentication state provider for Admin UI.
/// Manages user authentication state and claims.
/// </summary>
public sealed class AdminAuthenticationStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private string? _currentToken;
    private DateTimeOffset? _tokenExpiration;

    /// <summary>
    /// Gets the current authentication token.
    /// </summary>
    public string? CurrentToken => _currentToken;

    /// <summary>
    /// Gets the token expiration time.
    /// </summary>
    public DateTimeOffset? TokenExpiration => _tokenExpiration;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    /// <summary>
    /// Sets the authentication state with user claims and token.
    /// </summary>
    public void SetAuthenticationState(List<Claim> claims, string token, DateTimeOffset expiration)
    {
        var identity = new ClaimsIdentity(claims, "jwt");
        _currentUser = new ClaimsPrincipal(identity);
        _currentToken = token;
        _tokenExpiration = expiration;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    /// <summary>
    /// Clears the authentication state (logout).
    /// </summary>
    public void ClearAuthenticationState()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _currentToken = null;
        _tokenExpiration = null;

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }
}
