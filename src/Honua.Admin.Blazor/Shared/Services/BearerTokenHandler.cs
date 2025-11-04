// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// HTTP delegating handler that adds Bearer token to outgoing requests.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(
        AuthenticationStateProvider authStateProvider,
        ILogger<BearerTokenHandler> logger)
    {
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get token from auth state provider
        if (_authStateProvider is AdminAuthenticationStateProvider adminAuth)
        {
            var token = adminAuth.CurrentToken;
            var expiration = adminAuth.TokenExpiration;

            // Check if token exists and is not expired
            if (!string.IsNullOrEmpty(token))
            {
                if (expiration.HasValue && expiration.Value > DateTimeOffset.UtcNow)
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                else
                {
                    _logger.LogWarning("Token has expired. User needs to re-authenticate.");
                    // Token expired - don't add it to request
                    // The API will return 401 and the UI should redirect to login
                }
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
