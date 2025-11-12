// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Implementation of OAuth 2.0 Authorization Code flow with PKCE for mobile authentication
/// </summary>
public class AuthenticationService : IAuthenticationService
{
	private const string ACCESS_TOKEN_KEY = "access_token";
	private const string REFRESH_TOKEN_KEY = "refresh_token";
	private const string TOKEN_EXPIRY_KEY = "token_expiry";
	private const string USER_INFO_KEY = "user_info";

	private readonly ISettingsService _settingsService;
	private readonly IApiClient _apiClient;

	public AuthenticationService(ISettingsService settingsService, IApiClient apiClient)
	{
		_settingsService = settingsService;
		_apiClient = apiClient;
	}

	public async Task<bool> IsAuthenticatedAsync()
	{
		var accessToken = await _settingsService.GetAsync<string>(ACCESS_TOKEN_KEY);
		if (string.IsNullOrEmpty(accessToken))
		{
			return false;
		}

		// Check if token is expired
		var expiry = await _settingsService.GetAsync<DateTime>(TOKEN_EXPIRY_KEY);
		if (expiry <= DateTime.UtcNow)
		{
			// Try to refresh
			return await RefreshTokenAsync();
		}

		return true;
	}

	public async Task<bool> LoginAsync(string username, string password)
	{
		try
		{
			// Generate PKCE challenge and verifier
			var (codeVerifier, codeChallenge) = GeneratePkce();

			// Step 1: Get authorization code using username/password
			// In a real implementation, this would redirect to the authorization server
			// For now, we'll simulate the OAuth flow
			var authRequest = new
			{
				username,
				password,
				client_id = "honua_field_mobile",
				response_type = "code",
				code_challenge = codeChallenge,
				code_challenge_method = "S256",
				redirect_uri = "honuafield://callback"
			};

			var authResponse = await _apiClient.PostAsync<dynamic>("/oauth/authorize", authRequest);
			var authorizationCode = authResponse?.code?.ToString();

			if (string.IsNullOrEmpty(authorizationCode))
			{
				return false;
			}

			// Step 2: Exchange authorization code for tokens using PKCE verifier
			var tokenRequest = new
			{
				grant_type = "authorization_code",
				code = authorizationCode,
				code_verifier = codeVerifier,
				client_id = "honua_field_mobile",
				redirect_uri = "honuafield://callback"
			};

			var tokenResponse = await _apiClient.PostAsync<TokenResponse>("/oauth/token", tokenRequest);

			if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
			{
				return false;
			}

			// Store tokens securely
			await _settingsService.SetAsync(ACCESS_TOKEN_KEY, tokenResponse.AccessToken);
			await _settingsService.SetAsync(REFRESH_TOKEN_KEY, tokenResponse.RefreshToken);
			await _settingsService.SetAsync(TOKEN_EXPIRY_KEY, DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn));

			// Fetch and store user info
			var userInfo = await FetchUserInfoAsync(tokenResponse.AccessToken);
			if (userInfo != null)
			{
				await _settingsService.SetAsync(USER_INFO_KEY, JsonSerializer.Serialize(userInfo));
			}

			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Login failed: {ex.Message}");
			return false;
		}
	}

	public async Task LogoutAsync()
	{
		var accessToken = await _settingsService.GetAsync<string>(ACCESS_TOKEN_KEY);

		if (!string.IsNullOrEmpty(accessToken))
		{
			try
			{
				// Revoke tokens on server
				await _apiClient.PostAsync("/oauth/revoke", new { token = accessToken });
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Token revocation failed: {ex.Message}");
			}
		}

		// Clear local tokens
		await _settingsService.RemoveAsync(ACCESS_TOKEN_KEY);
		await _settingsService.RemoveAsync(REFRESH_TOKEN_KEY);
		await _settingsService.RemoveAsync(TOKEN_EXPIRY_KEY);
		await _settingsService.RemoveAsync(USER_INFO_KEY);
	}

	public async Task<string?> GetAccessTokenAsync()
	{
		var accessToken = await _settingsService.GetAsync<string>(ACCESS_TOKEN_KEY);
		if (string.IsNullOrEmpty(accessToken))
		{
			return null;
		}

		// Check if token is expired
		var expiry = await _settingsService.GetAsync<DateTime>(TOKEN_EXPIRY_KEY);
		if (expiry <= DateTime.UtcNow.AddMinutes(5)) // Refresh if expires in 5 minutes
		{
			var refreshed = await RefreshTokenAsync();
			if (!refreshed)
			{
				return null;
			}
			accessToken = await _settingsService.GetAsync<string>(ACCESS_TOKEN_KEY);
		}

		return accessToken;
	}

	public async Task<UserInfo?> GetCurrentUserAsync()
	{
		var userInfoJson = await _settingsService.GetAsync<string>(USER_INFO_KEY);
		if (string.IsNullOrEmpty(userInfoJson))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize<UserInfo>(userInfoJson);
		}
		catch
		{
			return null;
		}
	}

	public async Task<bool> RefreshTokenAsync()
	{
		try
		{
			var refreshToken = await _settingsService.GetAsync<string>(REFRESH_TOKEN_KEY);
			if (string.IsNullOrEmpty(refreshToken))
			{
				return false;
			}

			var tokenRequest = new
			{
				grant_type = "refresh_token",
				refresh_token = refreshToken,
				client_id = "honua_field_mobile"
			};

			var tokenResponse = await _apiClient.PostAsync<TokenResponse>("/oauth/token", tokenRequest);

			if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
			{
				return false;
			}

			// Store new tokens
			await _settingsService.SetAsync(ACCESS_TOKEN_KEY, tokenResponse.AccessToken);
			if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
			{
				await _settingsService.SetAsync(REFRESH_TOKEN_KEY, tokenResponse.RefreshToken);
			}
			await _settingsService.SetAsync(TOKEN_EXPIRY_KEY, DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn));

			return true;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Token refresh failed: {ex.Message}");
			return false;
		}
	}

	private async Task<UserInfo?> FetchUserInfoAsync(string accessToken)
	{
		try
		{
			return await _apiClient.GetAsync<UserInfo>("/oauth/userinfo", accessToken);
		}
		catch
		{
			return null;
		}
	}

	/// <summary>
	/// Generate PKCE code verifier and challenge
	/// Implements RFC 7636: Proof Key for Code Exchange
	/// </summary>
	private (string codeVerifier, string codeChallenge) GeneratePkce()
	{
		// Generate code verifier (random string 43-128 characters)
		var verifierBytes = new byte[32];
		RandomNumberGenerator.Fill(verifierBytes);
		var codeVerifier = Convert.ToBase64String(verifierBytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');

		// Generate code challenge (SHA256 hash of verifier)
		using var sha256 = SHA256.Create();
		var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
		var codeChallenge = Convert.ToBase64String(challengeBytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');

		return (codeVerifier, codeChallenge);
	}
}

/// <summary>
/// OAuth token response
/// </summary>
internal record TokenResponse
{
	public required string AccessToken { get; init; }
	public required string RefreshToken { get; init; }
	public required string TokenType { get; init; }
	public required int ExpiresIn { get; init; }
}
