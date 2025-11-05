// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Blazor.Tests.Infrastructure;

/// <summary>
/// Test implementation of AuthenticationStateProvider for unit tests.
/// </summary>
public class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private ClaimsPrincipal _user;

    public TestAuthenticationStateProvider(ClaimsPrincipal? user = null)
    {
        _user = user ?? new ClaimsPrincipal(new ClaimsIdentity());
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_user));
    }

    /// <summary>
    /// Sets the current user for testing.
    /// </summary>
    public void SetUser(ClaimsPrincipal user)
    {
        _user = user;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Creates an authenticated user with specified roles.
    /// </summary>
    public static ClaimsPrincipal CreateAuthenticatedUser(
        string username = "testuser",
        string displayName = "Test User",
        params string[] roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("display_name", displayName),
            new Claim(ClaimTypes.NameIdentifier, username)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates an administrator user for testing.
    /// </summary>
    public static ClaimsPrincipal CreateAdministrator(string username = "admin")
    {
        return CreateAuthenticatedUser(username, "Administrator", "administrator");
    }

    /// <summary>
    /// Creates a data publisher user for testing.
    /// </summary>
    public static ClaimsPrincipal CreateDataPublisher(string username = "publisher")
    {
        return CreateAuthenticatedUser(username, "Data Publisher", "datapublisher");
    }

    /// <summary>
    /// Creates a viewer user for testing.
    /// </summary>
    public static ClaimsPrincipal CreateViewer(string username = "viewer")
    {
        return CreateAuthenticatedUser(username, "Viewer", "viewer");
    }
}
