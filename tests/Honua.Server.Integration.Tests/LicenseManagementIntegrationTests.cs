using Dapper;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Npgsql;

namespace Honua.Server.Integration.Tests;

/// <summary>
/// Integration tests for customer license lifecycle management.
/// Tests license generation, expiration, upgrades, and access control.
/// </summary>
[Collection("Integration")]
public class LicenseManagementIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public LicenseManagementIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Test_GenerateLicense_ValidCustomer_Success()
    {
        // Arrange
        var customerId = "new-customer-001";

        // Act - Generate new license
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var licenseId = await connection.QuerySingleAsync<Guid>(@"
            INSERT INTO customer_licenses (
                customer_id,
                license_tier,
                status,
                max_concurrent_builds,
                allowed_registries
            )
            VALUES (
                @CustomerId,
                @Tier,
                'active',
                @MaxBuilds,
                @Registries
            )
            RETURNING id
        ", new
        {
            CustomerId = customerId,
            Tier = "Professional",
            MaxBuilds = 2,
            Registries = new[] { "GitHubContainerRegistry", "AwsEcr" }
        });

        // Assert - License created
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE id = @LicenseId
        ", new { LicenseId = licenseId });

        license.CustomerId.Should().Be(customerId);
        license.LicenseTier.Should().Be("Professional");
        license.Status.Should().Be("active");
        license.MaxConcurrentBuilds.Should().Be(2);
        license.AllowedRegistries.Should().Contain("GitHubContainerRegistry");
        license.AllowedRegistries.Should().Contain("AwsEcr");
        license.IssuedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Test_ExpiredLicense_AutoRevoke_Success()
    {
        // Arrange - License that expired yesterday
        var customerId = "expired-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                licenseTier: "Standard",
                status: "active",
                expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
        });

        // Act - Run expiration check
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var expiredCount = await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET status = 'expired'
            WHERE expires_at IS NOT NULL
            AND expires_at < NOW()
            AND status = 'active'
        ");

        // Assert - License marked as expired
        expiredCount.Should().BeGreaterThan(0);

        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.Status.Should().Be("expired");
    }

    [Fact]
    public async Task Test_UpgradeLicense_PreserveHistory_Success()
    {
        // Arrange - Existing Standard license
        var customerId = "upgrade-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                licenseTier: "Standard",
                maxConcurrentBuilds: 1,
                allowedRegistries: new[] { "GitHubContainerRegistry" });
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Upgrade to Professional
        await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET license_tier = 'Professional',
                max_concurrent_builds = 3,
                allowed_registries = @Registries,
                metadata = jsonb_build_object(
                    'upgraded_from', 'Standard',
                    'upgraded_at', NOW()
                )
            WHERE customer_id = @CustomerId
        ", new
        {
            CustomerId = customerId,
            Registries = new[] { "GitHubContainerRegistry", "AwsEcr", "AzureAcr" }
        });

        // Assert - License upgraded
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.LicenseTier.Should().Be("Professional");
        license.MaxConcurrentBuilds.Should().Be(3);
        license.AllowedRegistries.Should().HaveCount(3);
        license.Metadata.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Test_SuspendLicense_RevokeAccess_Success()
    {
        // Arrange - Active license
        var customerId = "suspend-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Enterprise");
            builder.WithRegistryCredentials(
                customerId,
                "GitHubContainerRegistry",
                "honua/customer",
                "ghcr.io");
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Suspend license
        await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET status = 'suspended'
            WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        // Revoke all credentials
        await connection.ExecuteAsync(@"
            UPDATE registry_credentials
            SET revoked_at = NOW()
            WHERE customer_id = @CustomerId
            AND revoked_at IS NULL
        ", new { CustomerId = customerId });

        // Assert - License suspended
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.Status.Should().Be("suspended");

        // Assert - Credentials revoked
        var activeCredentials = await connection.QueryAsync<RegistryCredentialRecord>(@"
            SELECT * FROM registry_credentials
            WHERE customer_id = @CustomerId
            AND revoked_at IS NULL
        ", new { CustomerId = customerId });

        activeCredentials.Should().BeEmpty();
    }

    [Fact]
    public async Task Test_LicenseTierFeatures_StandardVsEnterprise()
    {
        // Arrange - Two customers with different tiers
        var standardCustomer = "standard-tier";
        var enterpriseCustomer = "enterprise-tier";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                standardCustomer,
                "Standard",
                maxConcurrentBuilds: 1,
                allowedRegistries: new[] { "GitHubContainerRegistry" });

            builder.WithCustomerLicense(
                enterpriseCustomer,
                "Enterprise",
                maxConcurrentBuilds: 10,
                allowedRegistries: new[]
                {
                    "GitHubContainerRegistry",
                    "AwsEcr",
                    "AzureAcr",
                    "GcpArtifactRegistry"
                });
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Query both licenses
        var licenses = await connection.QueryAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses
            WHERE customer_id IN (@Customer1, @Customer2)
            ORDER BY license_tier
        ", new { Customer1 = standardCustomer, Customer2 = enterpriseCustomer });

        var licenseList = licenses.ToList();

        // Assert - Enterprise has more features
        var enterprise = licenseList.First(l => l.LicenseTier == "Enterprise");
        var standard = licenseList.First(l => l.LicenseTier == "Standard");

        enterprise.MaxConcurrentBuilds.Should().BeGreaterThan(standard.MaxConcurrentBuilds);
        enterprise.AllowedRegistries.Should().HaveCountGreaterThan(standard.AllowedRegistries.Length);
    }

    [Fact]
    public async Task Test_RenewLicense_ExtendExpiration()
    {
        // Arrange - License expiring soon
        var customerId = "renew-customer";
        var originalExpiration = DateTimeOffset.UtcNow.AddDays(7);

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                "Professional",
                expiresAt: originalExpiration);
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Renew for another year
        var newExpiration = DateTimeOffset.UtcNow.AddYears(1);
        await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET expires_at = @NewExpiration,
                metadata = jsonb_build_object(
                    'renewed_at', NOW(),
                    'previous_expiration', @OriginalExpiration
                )
            WHERE customer_id = @CustomerId
        ", new
        {
            CustomerId = customerId,
            NewExpiration = newExpiration,
            OriginalExpiration = originalExpiration
        });

        // Assert - Expiration extended
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.ExpiresAt.Should().NotBeNull();
        license.ExpiresAt!.Value.Should().BeCloseTo(newExpiration, TimeSpan.FromMinutes(1));
        license.Status.Should().Be("active");
    }

    [Fact]
    public async Task Test_DowngradeLicense_RemoveFeatures()
    {
        // Arrange - Enterprise license
        var customerId = "downgrade-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                "Enterprise",
                maxConcurrentBuilds: 10,
                allowedRegistries: new[]
                {
                    "GitHubContainerRegistry",
                    "AwsEcr",
                    "AzureAcr",
                    "GcpArtifactRegistry"
                });

            // Customer has credentials for multiple registries
            builder.WithRegistryCredentials(customerId, "GitHubContainerRegistry", "namespace1", "ghcr.io");
            builder.WithRegistryCredentials(customerId, "AwsEcr", "namespace2", "ecr.aws");
            builder.WithRegistryCredentials(customerId, "GcpArtifactRegistry", "namespace3", "gcr.io");
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Downgrade to Standard (only GHCR allowed)
        await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET license_tier = 'Standard',
                max_concurrent_builds = 1,
                allowed_registries = @Registries
            WHERE customer_id = @CustomerId
        ", new
        {
            CustomerId = customerId,
            Registries = new[] { "GitHubContainerRegistry" }
        });

        // Revoke credentials for non-allowed registries
        await connection.ExecuteAsync(@"
            UPDATE registry_credentials
            SET revoked_at = NOW()
            WHERE customer_id = @CustomerId
            AND registry_type != 'GitHubContainerRegistry'
            AND revoked_at IS NULL
        ", new { CustomerId = customerId });

        // Assert - License downgraded
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.LicenseTier.Should().Be("Standard");
        license.MaxConcurrentBuilds.Should().Be(1);
        license.AllowedRegistries.Should().HaveCount(1);

        // Assert - Only GHCR credentials remain active
        var activeCredentials = await connection.QueryAsync<RegistryCredentialRecord>(@"
            SELECT * FROM registry_credentials
            WHERE customer_id = @CustomerId
            AND revoked_at IS NULL
        ", new { CustomerId = customerId });

        activeCredentials.Should().HaveCount(1);
        activeCredentials.Single().RegistryType.Should().Be("GitHubContainerRegistry");
    }

    [Fact]
    public async Task Test_BulkLicenseExpiration_BatchProcessing()
    {
        // Arrange - Multiple licenses at different expiration states
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense("customer-active-1", expiresAt: DateTimeOffset.UtcNow.AddMonths(6));
            builder.WithCustomerLicense("customer-active-2", expiresAt: DateTimeOffset.UtcNow.AddYears(1));
            builder.WithCustomerLicense("customer-expiring-1", expiresAt: DateTimeOffset.UtcNow.AddDays(-1));
            builder.WithCustomerLicense("customer-expiring-2", expiresAt: DateTimeOffset.UtcNow.AddDays(-7));
            builder.WithCustomerLicense("customer-permanent", expiresAt: null); // Never expires
        });

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act - Batch expire all expired licenses
        var expiredCount = await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET status = 'expired'
            WHERE expires_at IS NOT NULL
            AND expires_at < NOW()
            AND status = 'active'
        ");

        // Assert - Correct number of licenses expired
        expiredCount.Should().Be(2);

        var expiredLicenses = await connection.QueryAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE status = 'expired'
        ");

        expiredLicenses.Should().HaveCount(2);
        expiredLicenses.Select(l => l.CustomerId).Should().BeEquivalentTo(
            new[] { "customer-expiring-1", "customer-expiring-2" });
    }

    [Fact]
    public async Task Test_LicenseMetadata_CustomFields()
    {
        // Arrange & Act - Create license with custom metadata
        var customerId = "metadata-customer";

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            INSERT INTO customer_licenses (
                customer_id,
                license_tier,
                status,
                max_concurrent_builds,
                allowed_registries,
                metadata
            )
            VALUES (
                @CustomerId,
                'Enterprise',
                'active',
                5,
                @Registries,
                @Metadata::jsonb
            )
        ", new
        {
            CustomerId = customerId,
            Registries = new[] { "GitHubContainerRegistry", "AwsEcr" },
            Metadata = System.Text.Json.JsonSerializer.Serialize(new
            {
                company_name = "Acme Corporation",
                sales_rep = "john.doe@honua.io",
                contract_number = "CNT-2025-001",
                custom_features = new[] { "priority_support", "custom_sla" }
            })
        });

        // Assert - Metadata stored and retrievable
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.Metadata.Should().NotBeNullOrEmpty();
        license.Metadata.Should().Contain("Acme Corporation");
        license.Metadata.Should().Contain("priority_support");
    }

    [Fact]
    public async Task Test_TrialLicense_AutoConvertOrExpire()
    {
        // Arrange - Trial license expiring today
        var customerId = "trial-customer";

        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        await connection.ExecuteAsync(@"
            INSERT INTO customer_licenses (
                customer_id,
                license_tier,
                status,
                expires_at,
                max_concurrent_builds,
                allowed_registries,
                metadata
            )
            VALUES (
                @CustomerId,
                'Standard',
                'active',
                @ExpiresAt,
                1,
                @Registries,
                '{{""is_trial"": true}}'::jsonb
            )
        ", new
        {
            CustomerId = customerId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1), // Trial ended
            Registries = new[] { "GitHubContainerRegistry" }
        });

        // Act - Check for expired trials and convert or suspend
        await connection.ExecuteAsync(@"
            UPDATE customer_licenses
            SET status = CASE
                WHEN metadata->>'is_trial' = 'true' THEN 'expired'
                ELSE status
            END
            WHERE expires_at < NOW()
            AND status = 'active'
        ");

        // Assert - Trial expired
        var license = await connection.QuerySingleAsync<CustomerLicenseRecord>(@"
            SELECT * FROM customer_licenses WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        license.Status.Should().Be("expired");
        license.Metadata.Should().Contain("is_trial");
    }
}

internal record CustomerLicenseRecord
{
    public Guid Id { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string LicenseTier { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset IssuedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public int MaxConcurrentBuilds { get; init; }
    public string[] AllowedRegistries { get; init; } = Array.Empty<string>();
    public string? Metadata { get; init; }
}
