// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for RBAC (Role-Based Access Control) management operations.
/// </summary>
public sealed class RbacApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RbacApiClient> _logger;

    public RbacApiClient(IHttpClientFactory httpClientFactory, ILogger<RbacApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    #region Role Management

    /// <summary>
    /// Lists all roles.
    /// </summary>
    public async Task<RoleListResponse?> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/rbac/roles", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<RoleListResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing roles");
            throw;
        }
    }

    /// <summary>
    /// Gets a specific role by ID.
    /// </summary>
    public async Task<RoleDefinition?> GetRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/admin/metadata/rbac/roles/{Uri.EscapeDataString(roleId)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RoleDefinition>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role {RoleId}", roleId);
            throw;
        }
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    public async Task<RoleDefinition?> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/rbac/roles", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<RoleDefinition>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role");
            throw;
        }
    }

    /// <summary>
    /// Updates an existing role.
    /// </summary>
    public async Task<RoleDefinition?> UpdateRoleAsync(string roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/admin/metadata/rbac/roles/{Uri.EscapeDataString(roleId)}", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<RoleDefinition>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", roleId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a role.
    /// </summary>
    public async Task<bool> DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/admin/metadata/rbac/roles/{Uri.EscapeDataString(roleId)}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", roleId);
            throw;
        }
    }

    #endregion

    #region Permission Management

    /// <summary>
    /// Lists all available permissions.
    /// </summary>
    public async Task<PermissionListResponse?> ListPermissionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/metadata/rbac/permissions", cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PermissionListResponse>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing permissions");
            throw;
        }
    }

    /// <summary>
    /// Creates a custom permission.
    /// </summary>
    public async Task<PermissionDefinition?> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/admin/metadata/rbac/permissions", request, cancellationToken);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PermissionDefinition>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating permission");
            throw;
        }
    }

    #endregion
}
