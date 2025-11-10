// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.Licensing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Enterprise.Licensing.Storage;

/// <summary>
/// Database storage implementation for license data using Dapper.
/// </summary>
public sealed class LicenseStore : ILicenseStore
{
    private readonly IOptionsMonitor<LicenseOptions> _options;
    private readonly ILogger<LicenseStore> _logger;

    public LicenseStore(
        IOptionsMonitor<LicenseOptions> options,
        ILogger<LicenseStore> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LicenseInfo?> GetByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, customer_id, license_key, tier, status, issued_at, expires_at,
                   features, revoked_at, email, metadata
            FROM licenses
            WHERE customer_id = @CustomerId
            LIMIT 1";

        var row = await connection.QueryFirstOrDefaultAsync<LicenseRow>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));

        return row?.ToLicenseInfo();
    }

    public async Task<LicenseInfo?> GetByIdAsync(
        Guid licenseId,
        CancellationToken cancellationToken = default)
    {
        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, customer_id, license_key, tier, status, issued_at, expires_at,
                   features, revoked_at, email, metadata
            FROM licenses
            WHERE id = @LicenseId
            LIMIT 1";

        var row = await connection.QueryFirstOrDefaultAsync<LicenseRow>(
            new CommandDefinition(sql, new { LicenseId = licenseId }, cancellationToken: cancellationToken));

        return row?.ToLicenseInfo();
    }

    public async Task<LicenseInfo> CreateAsync(
        LicenseInfo license,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(license);

        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO licenses (id, customer_id, license_key, tier, status, issued_at, expires_at,
                                  features, revoked_at, email, metadata)
            VALUES (@Id, @CustomerId, @LicenseKey, @Tier, @Status, @IssuedAt, @ExpiresAt,
                    @Features::jsonb, @RevokedAt, @Email, @Metadata::jsonb)
            RETURNING id, customer_id, license_key, tier, status, issued_at, expires_at,
                      features, revoked_at, email, metadata";

        var row = new LicenseRow
        {
            Id = license.Id,
            CustomerId = license.CustomerId,
            LicenseKey = license.LicenseKey,
            Tier = license.Tier.ToString(),
            Status = license.Status.ToString(),
            IssuedAt = license.IssuedAt,
            ExpiresAt = license.ExpiresAt,
            Features = JsonSerializer.Serialize(license.Features),
            RevokedAt = license.RevokedAt,
            Email = license.Email,
            Metadata = license.Metadata != null ? JsonSerializer.Serialize(license.Metadata) : null
        };

        var created = await connection.QueryFirstAsync<LicenseRow>(
            new CommandDefinition(sql, row, cancellationToken: cancellationToken));

        _logger.LogInformation(
            "Created license {LicenseId} for customer {CustomerId}",
            created.Id,
            created.CustomerId);

        return created.ToLicenseInfo();
    }

    public async Task UpdateAsync(
        LicenseInfo license,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(license);

        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            UPDATE licenses
            SET license_key = @LicenseKey,
                tier = @Tier,
                status = @Status,
                expires_at = @ExpiresAt,
                features = @Features::jsonb,
                revoked_at = @RevokedAt,
                email = @Email,
                metadata = @Metadata::jsonb
            WHERE id = @Id";

        var row = new LicenseRow
        {
            Id = license.Id,
            CustomerId = license.CustomerId,
            LicenseKey = license.LicenseKey,
            Tier = license.Tier.ToString(),
            Status = license.Status.ToString(),
            IssuedAt = license.IssuedAt,
            ExpiresAt = license.ExpiresAt,
            Features = JsonSerializer.Serialize(license.Features),
            RevokedAt = license.RevokedAt,
            Email = license.Email,
            Metadata = license.Metadata != null ? JsonSerializer.Serialize(license.Metadata) : null
        };

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, row, cancellationToken: cancellationToken));

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"License {license.Id} not found for update");
        }

        _logger.LogInformation(
            "Updated license {LicenseId} for customer {CustomerId}",
            license.Id,
            license.CustomerId);
    }

    public async Task<LicenseInfo[]> GetExpiringLicensesAsync(
        int daysFromNow,
        CancellationToken cancellationToken = default)
    {
        using var connection = await CreateConnectionAsync(cancellationToken);

        var expirationThreshold = DateTimeOffset.UtcNow.AddDays(daysFromNow);

        const string sql = @"
            SELECT id, customer_id, license_key, tier, status, issued_at, expires_at,
                   features, revoked_at, email, metadata
            FROM licenses
            WHERE status = 'Active'
              AND revoked_at IS NULL
              AND expires_at > @Now
              AND expires_at <= @ExpirationThreshold
            ORDER BY expires_at ASC";

        var rows = await connection.QueryAsync<LicenseRow>(
            new CommandDefinition(
                sql,
                new { Now = DateTimeOffset.UtcNow, ExpirationThreshold = expirationThreshold },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToLicenseInfo()).ToArray();
    }

    public async Task<LicenseInfo[]> GetExpiredLicensesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, customer_id, license_key, tier, status, issued_at, expires_at,
                   features, revoked_at, email, metadata
            FROM licenses
            WHERE expires_at <= @Now
              AND revoked_at IS NULL
              AND status != 'Revoked'
            ORDER BY expires_at ASC";

        var rows = await connection.QueryAsync<LicenseRow>(
            new CommandDefinition(
                sql,
                new { Now = DateTimeOffset.UtcNow },
                cancellationToken: cancellationToken));

        return rows.Select(r => r.ToLicenseInfo()).ToArray();
    }

    public async Task<LicenseInfo?> GetFirstActiveLicenseAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, customer_id, license_key, tier, status, issued_at, expires_at,
                   features, revoked_at, email, metadata
            FROM licenses
            WHERE status = 'Active'
              AND revoked_at IS NULL
              AND expires_at > @Now
            ORDER BY tier DESC, expires_at DESC
            LIMIT 1";

        var row = await connection.QueryFirstOrDefaultAsync<LicenseRow>(
            new CommandDefinition(sql, new { Now = DateTimeOffset.UtcNow }, cancellationToken: cancellationToken));

        return row?.ToLicenseInfo();
    }

    private async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var connectionString = opts.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("License database connection string is not configured");
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private sealed class LicenseRow
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public string Features { get; set; } = string.Empty;
        public DateTimeOffset? RevokedAt { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Metadata { get; set; }

        public LicenseInfo ToLicenseInfo()
        {
            return new LicenseInfo
            {
                Id = Id,
                CustomerId = CustomerId,
                LicenseKey = LicenseKey,
                Tier = Enum.Parse<LicenseTier>(Tier),
                Status = Enum.Parse<LicenseStatus>(Status),
                IssuedAt = IssuedAt,
                ExpiresAt = ExpiresAt,
                Features = JsonSerializer.Deserialize<LicenseFeatures>(Features) ?? new LicenseFeatures(),
                RevokedAt = RevokedAt,
                Email = Email,
                Metadata = !string.IsNullOrWhiteSpace(Metadata)
                    ? JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(Metadata)
                    : null
            };
        }
    }
}

/// <summary>
/// Database storage implementation for credential revocation records.
/// </summary>
public sealed class CredentialRevocationStore : ICredentialRevocationStore
{
    private readonly IOptionsMonitor<LicenseOptions> _options;
    private readonly ILogger<CredentialRevocationStore> _logger;

    public CredentialRevocationStore(
        IOptionsMonitor<LicenseOptions> options,
        ILogger<CredentialRevocationStore> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RecordRevocationAsync(
        CredentialRevocation revocation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revocation);

        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO credential_revocations (customer_id, registry_type, revoked_at, reason, revoked_by)
            VALUES (@CustomerId, @RegistryType, @RevokedAt, @Reason, @RevokedBy)
            RETURNING id";

        var id = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, revocation, cancellationToken: cancellationToken));

        _logger.LogInformation(
            "Recorded credential revocation {RevocationId} for customer {CustomerId}, registry {RegistryType}",
            id,
            revocation.CustomerId,
            revocation.RegistryType);
    }

    public async Task<CredentialRevocation[]> GetRevocationsByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Array.Empty<CredentialRevocation>();
        }

        using var connection = await CreateConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, customer_id, registry_type, revoked_at, reason, revoked_by
            FROM credential_revocations
            WHERE customer_id = @CustomerId
            ORDER BY revoked_at DESC";

        var revocations = await connection.QueryAsync<CredentialRevocation>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: cancellationToken));

        return revocations.ToArray();
    }

    private async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var connectionString = opts.ConnectionString;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("License database connection string is not configured");
        }

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
