// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for user management operations.
/// Note: This client assumes backend endpoints at /admin/users will be implemented.
/// </summary>
public sealed class UserApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserApiClient> _logger;

    public UserApiClient(IHttpClientFactory httpClientFactory, ILogger<UserApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Lists all users with pagination.
    /// </summary>
    public async Task<UserListResponse?> ListUsersAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/users?page={page}&pageSize={pageSize}", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<UserListResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific user by ID.
    /// </summary>
    public async Task<UserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/users/{Uri.EscapeDataString(userId)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    public async Task<UserResponse?> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/users", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing user.
    /// </summary>
    public async Task<UserResponse?> UpdateUserAsync(string userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/users/{Uri.EscapeDataString(userId)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    public async Task<bool> DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/users/{Uri.EscapeDataString(userId)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/admin/users/{Uri.EscapeDataString(userId)}/password", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Assigns roles to a user.
    /// </summary>
    public async Task<UserResponse?> AssignRolesAsync(string userId, AssignRolesRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/admin/users/{Uri.EscapeDataString(userId)}/roles", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning roles to user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Enables a user account.
    /// </summary>
    public async Task<bool> EnableUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/admin/users/{Uri.EscapeDataString(userId)}/enable", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Disables a user account.
    /// </summary>
    public async Task<bool> DisableUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/admin/users/{Uri.EscapeDataString(userId)}/disable", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Unlocks a locked user account.
    /// </summary>
    public async Task<bool> UnlockUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/admin/users/{Uri.EscapeDataString(userId)}/unlock", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Gets user statistics.
    /// </summary>
    public async Task<UserStatistics?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/users/statistics", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<UserStatistics>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user statistics");
            throw;
        }
    }

    /// <summary>
    /// Gets available roles.
    /// </summary>
    public List<RoleInfo> GetAvailableRoles()
    {
        return RoleOptions.AvailableRoles;
    }
}
