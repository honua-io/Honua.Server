// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Honua.Admin.Blazor.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for managing authentication state and tokens.
/// </summary>
public sealed class AuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<AuthenticationService> _logger;
    private string? _currentToken;
    private DateTimeOffset? _tokenExpiration;

    public AuthenticationService(
        IHttpClientFactory httpClientFactory,
        AuthenticationStateProvider authStateProvider,
        ILogger<AuthenticationService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AuthApi");
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current authentication token, or null if not authenticated.
    /// </summary>
    public string? CurrentToken => _currentToken;

    /// <summary>
    /// Gets the token expiration time, or null if not authenticated.
    /// </summary>
    public DateTimeOffset? TokenExpiration => _tokenExpiration;

    /// <summary>
    /// Returns true if the user is currently authenticated.
    /// </summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_currentToken) &&
                                    _tokenExpiration.HasValue &&
                                    _tokenExpiration.Value > DateTimeOffset.UtcNow;

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    public async Task<AuthenticationResult> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new LoginRequest
            {
                Username = username,
                Password = password,
                ExpirationMinutes = 480 // 8 hours
            };

            // Convert to form data (ArcGIS token endpoint expects form data)
            var formData = new Dictionary<string, string>
            {
                ["username"] = request.Username,
                ["password"] = request.Password,
                ["expiration"] = request.ExpirationMinutes.ToString(),
                ["f"] = request.Format
            };

            var content = new FormUrlEncodedContent(formData);
            var response = await _httpClient.PostAsync("/api/tokens/generate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Login failed with status {StatusCode}: {Error}", response.StatusCode, errorContent);

                try
                {
                    var errorResponse = await response.Content.ReadFromJsonAsync<AuthErrorResponse>(cancellationToken: cancellationToken);
                    return new AuthenticationResult(false, errorResponse?.Error.Message ?? "Authentication failed.");
                }
                catch
                {
                    return new AuthenticationResult(false, $"Authentication failed with status {response.StatusCode}.");
                }
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
            if (tokenResponse == null)
            {
                _logger.LogError("Token response was null");
                return new AuthenticationResult(false, "Invalid token response.");
            }

            _currentToken = tokenResponse.Token;
            _tokenExpiration = tokenResponse.ExpiresAt;

            // Notify authentication state provider
            if (_authStateProvider is AdminAuthenticationStateProvider adminAuth)
            {
                var claims = ParseTokenClaims(tokenResponse.Token);
                adminAuth.SetAuthenticationState(claims, tokenResponse.Token, tokenResponse.ExpiresAt);
            }

            _logger.LogInformation("User {Username} logged in successfully. Token expires at {ExpiresAt}", username, _tokenExpiration);

            // Check for password expiration warning
            string? warningMessage = null;
            if (tokenResponse.PasswordInfo != null && tokenResponse.PasswordInfo.DaysRemaining.HasValue)
            {
                warningMessage = $"Your password will expire in {tokenResponse.PasswordInfo.DaysRemaining.Value} days.";
            }

            return new AuthenticationResult(true, null, warningMessage);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during login");
            return new AuthenticationResult(false, "Unable to connect to authentication service. Please check your network connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return new AuthenticationResult(false, "An unexpected error occurred during login.");
        }
    }

    /// <summary>
    /// Logs out the current user and clears authentication state.
    /// </summary>
    public Task LogoutAsync()
    {
        _currentToken = null;
        _tokenExpiration = null;

        if (_authStateProvider is AdminAuthenticationStateProvider adminAuth)
        {
            adminAuth.ClearAuthenticationState();
        }

        _logger.LogInformation("User logged out");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses JWT token and extracts claims.
    /// </summary>
    private List<Claim> ParseTokenClaims(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var claims = new List<Claim>();
            foreach (var claim in jwtToken.Claims)
            {
                // Map JWT claim types to ClaimTypes
                var claimType = claim.Type switch
                {
                    JwtRegisteredClaimNames.Sub => ClaimTypes.NameIdentifier,
                    JwtRegisteredClaimNames.Email => ClaimTypes.Email,
                    "name" => ClaimTypes.Name,
                    "role" => ClaimTypes.Role,
                    _ => claim.Type
                };

                claims.Add(new Claim(claimType, claim.Value));
            }

            // Ensure we have a name claim
            if (!claims.Any(c => c.Type == ClaimTypes.Name))
            {
                var subClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (subClaim != null)
                {
                    claims.Add(new Claim(ClaimTypes.Name, subClaim.Value));
                }
            }

            return claims;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing token claims");
            return new List<Claim>();
        }
    }
}

/// <summary>
/// Result of an authentication attempt.
/// </summary>
public sealed record AuthenticationResult(
    bool Success,
    string? ErrorMessage = null,
    string? WarningMessage = null);
