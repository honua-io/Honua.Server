// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.Authentication;

/// <summary>
/// PostgreSQL implementation of SAML Identity Provider configuration store
/// </summary>
public class PostgresSamlIdentityProviderStore : ISamlIdentityProviderStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresSamlIdentityProviderStore> _logger;

    public PostgresSamlIdentityProviderStore(
        string connectionString,
        ILogger<PostgresSamlIdentityProviderStore> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SamlIdentityProviderConfiguration?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                id,
                tenant_id,
                name,
                entity_id,
                single_sign_on_service_url,
                single_logout_service_url,
                signing_certificate,
                sign_authentication_requests,
                want_assertions_signed,
                binding_type,
                attribute_mappings,
                enable_jit_provisioning,
                default_role,
                enabled,
                created_at,
                updated_at,
                metadata_xml,
                allow_unsolicited_authn_response,
                name_id_format
            FROM saml_identity_providers
            WHERE id = @Id";

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { Id = id });
        return row != null ? MapToConfiguration(row) : null;
    }

    public async Task<SamlIdentityProviderConfiguration?> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                id,
                tenant_id,
                name,
                entity_id,
                single_sign_on_service_url,
                single_logout_service_url,
                signing_certificate,
                sign_authentication_requests,
                want_assertions_signed,
                binding_type,
                attribute_mappings,
                enable_jit_provisioning,
                default_role,
                enabled,
                created_at,
                updated_at,
                metadata_xml,
                allow_unsolicited_authn_response,
                name_id_format
            FROM saml_identity_providers
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT 1";

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { TenantId = tenantId });
        return row != null ? MapToConfiguration(row) : null;
    }

    public async Task<List<SamlIdentityProviderConfiguration>> GetAllByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                id,
                tenant_id,
                name,
                entity_id,
                single_sign_on_service_url,
                single_logout_service_url,
                signing_certificate,
                sign_authentication_requests,
                want_assertions_signed,
                binding_type,
                attribute_mappings,
                enable_jit_provisioning,
                default_role,
                enabled,
                created_at,
                updated_at,
                metadata_xml,
                allow_unsolicited_authn_response,
                name_id_format
            FROM saml_identity_providers
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC";

        var rows = await connection.QueryAsync<dynamic>(sql, new { TenantId = tenantId });
        return rows.Select(MapToConfiguration).ToList();
    }

    public async Task<SamlIdentityProviderConfiguration> CreateAsync(
        SamlIdentityProviderConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        configuration.Id = Guid.NewGuid();
        configuration.CreatedAt = DateTimeOffset.UtcNow;
        configuration.UpdatedAt = DateTimeOffset.UtcNow;

        const string sql = @"
            INSERT INTO saml_identity_providers (
                id,
                tenant_id,
                name,
                entity_id,
                single_sign_on_service_url,
                single_logout_service_url,
                signing_certificate,
                sign_authentication_requests,
                want_assertions_signed,
                binding_type,
                attribute_mappings,
                enable_jit_provisioning,
                default_role,
                enabled,
                created_at,
                updated_at,
                metadata_xml,
                allow_unsolicited_authn_response,
                name_id_format
            ) VALUES (
                @Id,
                @TenantId,
                @Name,
                @EntityId,
                @SingleSignOnServiceUrl,
                @SingleLogoutServiceUrl,
                @SigningCertificate,
                @SignAuthenticationRequests,
                @WantAssertionsSigned,
                @BindingType,
                @AttributeMappings::jsonb,
                @EnableJitProvisioning,
                @DefaultRole,
                @Enabled,
                @CreatedAt,
                @UpdatedAt,
                @MetadataXml,
                @AllowUnsolicitedAuthnResponse,
                @NameIdFormat
            )";

        await connection.ExecuteAsync(sql, new
        {
            configuration.Id,
            configuration.TenantId,
            configuration.Name,
            configuration.EntityId,
            configuration.SingleSignOnServiceUrl,
            configuration.SingleLogoutServiceUrl,
            configuration.SigningCertificate,
            configuration.SignAuthenticationRequests,
            configuration.WantAssertionsSigned,
            BindingType = configuration.BindingType.ToString(),
            AttributeMappings = JsonSerializer.Serialize(configuration.AttributeMappings),
            configuration.EnableJitProvisioning,
            configuration.DefaultRole,
            configuration.Enabled,
            configuration.CreatedAt,
            configuration.UpdatedAt,
            configuration.MetadataXml,
            configuration.AllowUnsolicitedAuthnResponse,
            configuration.NameIdFormat
        });

        _logger.LogInformation(
            "Created SAML IdP configuration {IdpId} for tenant {TenantId}",
            configuration.Id, configuration.TenantId);

        return configuration;
    }

    public async Task<SamlIdentityProviderConfiguration> UpdateAsync(
        SamlIdentityProviderConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        configuration.UpdatedAt = DateTimeOffset.UtcNow;

        const string sql = @"
            UPDATE saml_identity_providers
            SET
                name = @Name,
                entity_id = @EntityId,
                single_sign_on_service_url = @SingleSignOnServiceUrl,
                single_logout_service_url = @SingleLogoutServiceUrl,
                signing_certificate = @SigningCertificate,
                sign_authentication_requests = @SignAuthenticationRequests,
                want_assertions_signed = @WantAssertionsSigned,
                binding_type = @BindingType,
                attribute_mappings = @AttributeMappings::jsonb,
                enable_jit_provisioning = @EnableJitProvisioning,
                default_role = @DefaultRole,
                enabled = @Enabled,
                updated_at = @UpdatedAt,
                metadata_xml = @MetadataXml,
                allow_unsolicited_authn_response = @AllowUnsolicitedAuthnResponse,
                name_id_format = @NameIdFormat
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            configuration.Id,
            configuration.Name,
            configuration.EntityId,
            configuration.SingleSignOnServiceUrl,
            configuration.SingleLogoutServiceUrl,
            configuration.SigningCertificate,
            configuration.SignAuthenticationRequests,
            configuration.WantAssertionsSigned,
            BindingType = configuration.BindingType.ToString(),
            AttributeMappings = JsonSerializer.Serialize(configuration.AttributeMappings),
            configuration.EnableJitProvisioning,
            configuration.DefaultRole,
            configuration.Enabled,
            configuration.UpdatedAt,
            configuration.MetadataXml,
            configuration.AllowUnsolicitedAuthnResponse,
            configuration.NameIdFormat
        });

        _logger.LogInformation("Updated SAML IdP configuration {IdpId}", configuration.Id);

        return configuration;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = "DELETE FROM saml_identity_providers WHERE id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });

        _logger.LogInformation("Deleted SAML IdP configuration {IdpId}", id);
    }

    public async Task<SamlIdentityProviderConfiguration?> GetEnabledByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                id,
                tenant_id,
                name,
                entity_id,
                single_sign_on_service_url,
                single_logout_service_url,
                signing_certificate,
                sign_authentication_requests,
                want_assertions_signed,
                binding_type,
                attribute_mappings,
                enable_jit_provisioning,
                default_role,
                enabled,
                created_at,
                updated_at,
                metadata_xml,
                allow_unsolicited_authn_response,
                name_id_format
            FROM saml_identity_providers
            WHERE tenant_id = @TenantId AND enabled = true
            ORDER BY created_at DESC
            LIMIT 1";

        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(sql, new { TenantId = tenantId });
        return row != null ? MapToConfiguration(row) : null;
    }

    private static SamlIdentityProviderConfiguration MapToConfiguration(dynamic row)
    {
        return new SamlIdentityProviderConfiguration
        {
            Id = row.id,
            TenantId = row.tenant_id,
            Name = row.name,
            EntityId = row.entity_id,
            SingleSignOnServiceUrl = row.single_sign_on_service_url,
            SingleLogoutServiceUrl = row.single_logout_service_url,
            SigningCertificate = row.signing_certificate,
            SignAuthenticationRequests = row.sign_authentication_requests,
            WantAssertionsSigned = row.want_assertions_signed,
            BindingType = Enum.Parse<SamlBindingType>(row.binding_type, ignoreCase: true),
            AttributeMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(row.attribute_mappings) ?? new Dictionary<string, string>(),
            EnableJitProvisioning = row.enable_jit_provisioning,
            DefaultRole = row.default_role,
            Enabled = row.enabled,
            CreatedAt = row.created_at,
            UpdatedAt = row.updated_at,
            MetadataXml = row.metadata_xml,
            AllowUnsolicitedAuthnResponse = row.allow_unsolicited_authn_response,
            NameIdFormat = row.name_id_format
        };
    }
}
