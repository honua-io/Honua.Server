// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// Service for Just-in-Time (JIT) user provisioning from SAML assertions
/// </summary>
public class SamlUserProvisioningService : ISamlUserProvisioningService
{
    private readonly string _connectionString;
    private readonly ISamlIdentityProviderStore _idpStore;
    private readonly ILogger<SamlUserProvisioningService> _logger;

    public SamlUserProvisioningService(
        string connectionString,
        ISamlIdentityProviderStore idpStore,
        ILogger<SamlUserProvisioningService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _idpStore = idpStore ?? throw new ArgumentNullException(nameof(idpStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SamlProvisionedUser> ProvisionUserAsync(
        Guid tenantId,
        Guid idpConfigurationId,
        SamlAssertionResult assertionResult,
        CancellationToken cancellationToken = default)
    {
        if (!assertionResult.IsValid)
        {
            throw new InvalidOperationException("Cannot provision user from invalid assertion");
        }

        if (assertionResult.NameId.IsNullOrEmpty())
        {
            throw new InvalidOperationException("NameID is required for user provisioning");
        }

        _logger.LogInformation(
            "Provisioning user for NameID {NameId} in tenant {TenantId}",
            assertionResult.NameId, tenantId);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check if user mapping already exists
        var existingMapping = await GetUserMappingAsync(
            tenantId,
            idpConfigurationId,
            assertionResult.NameId,
            cancellationToken);

        if (existingMapping != null)
        {
            // Update last login
            await UpdateLastLoginAsync(existingMapping.Id, assertionResult.SessionIndex, cancellationToken);

            // Get user details with tenant isolation
            var user = await GetUserDetailsAsync(connection, existingMapping.UserId, tenantId, cancellationToken);

            _logger.LogInformation(
                "User {UserId} already exists for NameID {NameId}",
                existingMapping.UserId, assertionResult.NameId);

            return new SamlProvisionedUser
            {
                UserId = existingMapping.UserId,
                MappingId = existingMapping.Id,
                IsNewUser = false,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role,
                NameId = assertionResult.NameId
            };
        }

        // Get IdP configuration for default role
        var idpConfig = await _idpStore.GetByIdAsync(idpConfigurationId, cancellationToken);
        if (idpConfig == null || !idpConfig.EnableJitProvisioning)
        {
            throw new InvalidOperationException("JIT provisioning is not enabled for this IdP");
        }

        // Extract user attributes from assertion
        var email = assertionResult.Attributes.GetValueOrDefault("email") ?? assertionResult.NameId;
        var firstName = assertionResult.Attributes.GetValueOrDefault("firstName") ?? "";
        var lastName = assertionResult.Attributes.GetValueOrDefault("lastName") ?? "";
        var displayName = assertionResult.Attributes.GetValueOrDefault("displayName");

        if (displayName.IsNullOrEmpty())
        {
            displayName = $"{firstName} {lastName}".Trim();
            if (displayName.IsNullOrEmpty())
            {
                displayName = email.Split('@')[0];
            }
        }

        // Start transaction for atomic user creation
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // Create user account
            var userId = Guid.NewGuid();
            const string createUserSql = @"
                INSERT INTO users (
                    id,
                    tenant_id,
                    email,
                    display_name,
                    role,
                    is_active,
                    is_saml_user,
                    created_at,
                    updated_at
                ) VALUES (
                    @UserId,
                    @TenantId,
                    @Email,
                    @DisplayName,
                    @Role,
                    true,
                    true,
                    @Now,
                    @Now
                )";

            await connection.ExecuteAsync(createUserSql, new
            {
                UserId = userId,
                TenantId = tenantId,
                Email = email,
                DisplayName = displayName,
                Role = idpConfig.DefaultRole,
                Now = DateTimeOffset.UtcNow
            }, transaction);

            _logger.LogInformation(
                "Created new user {UserId} ({Email}) via JIT provisioning",
                userId, email);

            // Create SAML user mapping
            var mappingId = Guid.NewGuid();
            const string createMappingSql = @"
                INSERT INTO saml_user_mappings (
                    id,
                    tenant_id,
                    user_id,
                    idp_configuration_id,
                    name_id,
                    session_index,
                    last_login_at,
                    created_at,
                    updated_at
                ) VALUES (
                    @MappingId,
                    @TenantId,
                    @UserId,
                    @IdpConfigurationId,
                    @NameId,
                    @SessionIndex,
                    @Now,
                    @Now,
                    @Now
                )";

            await connection.ExecuteAsync(createMappingSql, new
            {
                MappingId = mappingId,
                TenantId = tenantId,
                UserId = userId,
                IdpConfigurationId = idpConfigurationId,
                NameId = assertionResult.NameId,
                SessionIndex = assertionResult.SessionIndex,
                Now = DateTimeOffset.UtcNow
            }, transaction);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully provisioned user {UserId} for NameID {NameId}",
                userId, assertionResult.NameId);

            return new SamlProvisionedUser
            {
                UserId = userId,
                MappingId = mappingId,
                IsNewUser = true,
                Email = email,
                DisplayName = displayName,
                Role = idpConfig.DefaultRole,
                NameId = assertionResult.NameId
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to provision user for NameID {NameId}", assertionResult.NameId);
            throw;
        }
    }

    public async Task<SamlUserMapping?> GetUserMappingAsync(
        Guid tenantId,
        Guid idpConfigurationId,
        string nameId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                id,
                tenant_id,
                user_id,
                idp_configuration_id,
                name_id,
                session_index,
                last_login_at,
                created_at,
                updated_at
            FROM saml_user_mappings
            WHERE tenant_id = @TenantId
              AND idp_configuration_id = @IdpConfigurationId
              AND name_id = @NameId";

        return await connection.QuerySingleOrDefaultAsync<SamlUserMapping>(sql, new
        {
            TenantId = tenantId,
            IdpConfigurationId = idpConfigurationId,
            NameId = nameId
        });
    }

    public async Task UpdateLastLoginAsync(
        Guid mappingId,
        string? sessionIndex,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            UPDATE saml_user_mappings
            SET
                last_login_at = @Now,
                session_index = @SessionIndex,
                updated_at = @Now
            WHERE id = @MappingId";

        await connection.ExecuteAsync(sql, new
        {
            MappingId = mappingId,
            SessionIndex = sessionIndex,
            Now = DateTimeOffset.UtcNow
        });

        _logger.LogDebug("Updated last login for SAML user mapping {MappingId}", mappingId);
    }

    private async Task<(string Email, string DisplayName, string Role)> GetUserDetailsAsync(
        NpgsqlConnection connection,
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // SECURITY: Add tenant filter to enforce tenant isolation
        const string sql = @"
            SELECT email, display_name, role
            FROM users
            WHERE id = @UserId AND tenant_id = @TenantId";

        var result = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new
        {
            UserId = userId,
            TenantId = tenantId
        });

        if (result == null)
        {
            _logger.LogError(
                "User {UserId} not found or does not belong to tenant {TenantId}",
                userId, tenantId);
            throw new InvalidOperationException($"User {userId} not found or does not belong to tenant");
        }

        return (result.email, result.display_name, result.role);
    }
}
