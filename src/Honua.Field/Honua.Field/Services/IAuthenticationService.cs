// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace HonuaField.Services;

/// <summary>
/// Service for handling user authentication using OAuth 2.0 Authorization Code flow with PKCE
/// </summary>
public interface IAuthenticationService
{
	/// <summary>
	/// Check if user is currently authenticated
	/// </summary>
	Task<bool> IsAuthenticatedAsync();

	/// <summary>
	/// Login with username and password using OAuth 2.0 Authorization Code + PKCE flow
	/// </summary>
	Task<bool> LoginAsync(string username, string password);

	/// <summary>
	/// Logout current user and revoke tokens
	/// </summary>
	Task LogoutAsync();

	/// <summary>
	/// Get current access token (refreshes if expired)
	/// </summary>
	Task<string?> GetAccessTokenAsync();

	/// <summary>
	/// Get current user information
	/// </summary>
	Task<UserInfo?> GetCurrentUserAsync();

	/// <summary>
	/// Refresh access token using refresh token
	/// </summary>
	Task<bool> RefreshTokenAsync();
}

/// <summary>
/// User information
/// </summary>
public record UserInfo
{
	public required string Id { get; init; }
	public required string Username { get; init; }
	public required string Email { get; init; }
	public string? FullName { get; init; }
	public string? OrganizationId { get; init; }
}
