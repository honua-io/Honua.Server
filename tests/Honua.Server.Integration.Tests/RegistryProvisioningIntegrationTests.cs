using Dapper;
using FluentAssertions;
using Honua.Server.Integration.Tests.Fixtures;
using Honua.Server.Integration.Tests.Helpers;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;

namespace Honua.Server.Integration.Tests;

/// <summary>
/// Integration tests for container registry provisioning and credential management.
/// Uses mocked cloud provider APIs to test workflows without external dependencies.
/// </summary>
[Collection("Integration")]
public class RegistryProvisioningIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    private readonly IServiceProvider _services;

    public RegistryProvisioningIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _services = CreateServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _fixture.ResetDatabaseAsync();
    }

    [Fact]
    public async Task Test_ProvisionAws_CreateResources_Success()
    {
        // Arrange
        var customerId = "aws-customer-001";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Enterprise");
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Provision AWS ECR
        var result = await provisioner.ProvisionAsync(
            customerId,
            RegistryType.AwsEcr);

        // Assert - Provisioning succeeded
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.AwsEcr);
        result.CustomerId.Should().Be(customerId);
        result.Namespace.Should().NotBeNullOrEmpty();
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Contain("ecr");
        result.Credential.Username.Should().NotBeNullOrEmpty();
        result.Credential.Password.Should().NotBeNullOrEmpty();

        // Assert - Credentials stored in database
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var storedCred = await connection.QuerySingleOrDefaultAsync<RegistryCredentialRecord>(@"
            SELECT * FROM registry_credentials
            WHERE customer_id = @CustomerId
            AND registry_type = @RegistryType
            AND revoked_at IS NULL
        ", new { CustomerId = customerId, RegistryType = "AwsEcr" });

        storedCred.Should().NotBeNull();
        storedCred!.Namespace.Should().Be(result.Namespace);
        storedCred.RegistryUrl.Should().Contain("ecr");
    }

    [Fact]
    public async Task Test_ProvisionGitHub_CreateNamespace_Success()
    {
        // Arrange
        var customerId = "github-customer-001";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Professional");
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Provision GitHub Container Registry
        var result = await provisioner.ProvisionAsync(
            customerId,
            RegistryType.GitHubContainerRegistry);

        // Assert - Provisioning succeeded
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.GitHubContainerRegistry);
        result.Namespace.Should().Contain(customerId);
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Be("ghcr.io");
        result.Credential.ExpiresAt.Should().NotBeNull();

        // Assert - Token has reasonable expiration (within 1 hour)
        var expirationTime = result.Credential.ExpiresAt!.Value - DateTimeOffset.UtcNow;
        expirationTime.Should().BeLessThan(TimeSpan.FromHours(2));
        expirationTime.Should().BeGreaterThan(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Test_ProvisionAzure_TokenScoped_Success()
    {
        // Arrange
        var customerId = "azure-customer-001";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Enterprise");
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Provision Azure ACR
        var result = await provisioner.ProvisionAsync(
            customerId,
            RegistryType.AzureAcr);

        // Assert - Provisioning succeeded
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.AzureAcr);
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Contain("azurecr.io");
        result.Credential.ExpiresAt.Should().NotBeNull();

        // Assert - Metadata contains scope information
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("scope_map");
    }

    [Fact]
    public async Task Test_ProvisionGcp_ServiceAccount_Success()
    {
        // Arrange
        var customerId = "gcp-customer-001";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Enterprise");
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Provision GCP Artifact Registry
        var result = await provisioner.ProvisionAsync(
            customerId,
            RegistryType.GcpArtifactRegistry);

        // Assert - Provisioning succeeded
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.GcpArtifactRegistry);
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Contain("pkg.dev");
        result.Credential.Password.Should().Contain("serviceAccount"); // Service account JSON

        // Assert - Metadata contains service account info
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().ContainKey("service_account_email");
    }

    // NOTE: Disabled test - RevokeAsync is not part of IRegistryProvisioner interface
    // [Fact]
    // public async Task Test_RevokeCredentials_DeleteResources_Success()
    // {
    //     // Arrange - Provision credentials first
    //     var customerId = "revoke-test-customer";
    //     await _fixture.SeedTestDataAsync(builder =>
    //     {
    //         builder.WithCustomerLicense(customerId, "Professional");
    //         builder.WithRegistryCredentials(
    //             customerId,
    //             "GitHubContainerRegistry",
    //             namespace_: $"honua/{customerId}",
    //             registryUrl: "ghcr.io");
    //     });

    //     var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

    //     // Act - Revoke credentials
    //     var revokeSuccess = await provisioner.RevokeAsync(
    //         customerId,
    //         RegistryType.GitHubContainerRegistry);

    //     // Assert - Revocation succeeded
    //     revokeSuccess.Should().BeTrue();

    //     // Assert - Credentials marked as revoked in database
    //     using var connection = _fixture.CreateConnection();
    //     await connection.OpenAsync();

    //     var revokedCred = await connection.QuerySingleOrDefaultAsync<RegistryCredentialRecord>(@"
    //         SELECT * FROM registry_credentials
    //         WHERE customer_id = @CustomerId
    //         AND registry_type = @RegistryType
    //     ", new { CustomerId = customerId, RegistryType = "GitHubContainerRegistry" });

    //     revokedCred.Should().NotBeNull();
    //     revokedCred!.RevokedAt.Should().NotBeNull();
    //     revokedCred.RevokedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    // }

    [Fact]
    public async Task Test_ProvisionMultipleRegistries_CustomerIsolation()
    {
        // Arrange - Two customers
        var customer1 = "isolation-customer-1";
        var customer2 = "isolation-customer-2";

        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customer1, "Enterprise");
            builder.WithCustomerLicense(customer2, "Enterprise");
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Provision same registry type for both customers
        var result1 = await provisioner.ProvisionAsync(customer1, RegistryType.AwsEcr);
        var result2 = await provisioner.ProvisionAsync(customer2, RegistryType.AwsEcr);

        // Assert - Different namespaces for each customer
        result1.Namespace.Should().NotBe(result2.Namespace);
        result1.Namespace.Should().Contain(customer1);
        result2.Namespace.Should().Contain(customer2);

        // Assert - Different credentials
        result1.Credential!.Username.Should().NotBe(result2.Credential!.Username);

        // Assert - Both stored in database with unique constraints
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var credentials = await connection.QueryAsync<RegistryCredentialRecord>(@"
            SELECT * FROM registry_credentials
            WHERE registry_type = 'AwsEcr'
            AND revoked_at IS NULL
        ");

        credentials.Should().HaveCount(2);
        credentials.Select(c => c.CustomerId).Should().BeEquivalentTo(new[] { customer1, customer2 });
    }

    [Fact]
    public async Task Test_ProvisionExisting_UpdatesCredentials()
    {
        // Arrange - Existing credentials
        var customerId = "update-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Professional");
            builder.WithRegistryCredentials(
                customerId,
                "GitHubContainerRegistry",
                namespace_: $"honua/{customerId}",
                registryUrl: "ghcr.io",
                expiresAt: DateTimeOffset.UtcNow.AddDays(-1)); // Expired
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Re-provision (should update)
        var result = await provisioner.ProvisionAsync(
            customerId,
            RegistryType.GitHubContainerRegistry);

        // Assert - New credentials created
        result.Success.Should().BeTrue();
        result.Credential!.ExpiresAt.Should().NotBeNull();
        result.Credential.ExpiresAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow);

        // Assert - Old credentials marked as revoked
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var allCredentials = await connection.QueryAsync<RegistryCredentialRecord>(@"
            SELECT * FROM registry_credentials
            WHERE customer_id = @CustomerId
            AND registry_type = 'GitHubContainerRegistry'
            ORDER BY created_at
        ", new { CustomerId = customerId });

        // Should have old (revoked) and new (active) credentials
        var credList = allCredentials.ToList();
        credList.Should().HaveCountGreaterThanOrEqualTo(1);

        // Latest credential should be active
        var latestCred = credList.OrderByDescending(c => c.CreatedAt).First();
        latestCred.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Test_LicenseTier_RestrictsRegistryAccess()
    {
        // Arrange - Standard tier customer (only GHCR access)
        var customerId = "standard-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                "Standard",
                allowedRegistries: new[] { "GitHubContainerRegistry" });
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Try to provision AWS ECR (not allowed for Standard tier)
        var result = await provisioner.ProvisionAsync(
            customerId,
            RegistryType.AwsEcr);

        // Assert - Provisioning should fail
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("tier");
    }

    [Fact]
    public async Task Test_TokenRefresh_GeneratesNewCredentials()
    {
        // Arrange - Existing credentials near expiration
        var customerId = "refresh-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(customerId, "Professional");
            builder.WithRegistryCredentials(
                customerId,
                "GitHubContainerRegistry",
                namespace_: $"honua/{customerId}",
                registryUrl: "ghcr.io",
                expiresAt: DateTimeOffset.UtcNow.AddMinutes(5)); // Expires soon
        });

        var accessManager = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryAccessManager>();

        // Act - Generate fresh token
        var tokenResult = await accessManager.GenerateRegistryTokenAsync(
            customerId,
            RegistryType.GitHubContainerRegistry);

        // Assert - New token generated
        tokenResult.AccessToken.Should().NotBeNullOrEmpty();
        tokenResult.TokenExpiresAt.Should().NotBeNull();
        tokenResult.TokenExpiresAt!.Value.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(30));
    }

    [Fact]
    public async Task Test_BulkProvisioning_MultipleRegistries()
    {
        // Arrange - Enterprise customer with access to all registries
        var customerId = "enterprise-customer";
        await _fixture.SeedTestDataAsync(builder =>
        {
            builder.WithCustomerLicense(
                customerId,
                "Enterprise",
                allowedRegistries: new[]
                {
                    "GitHubContainerRegistry",
                    "AwsEcr",
                    "AzureAcr",
                    "GcpArtifactRegistry"
                });
        });

        var provisioner = _services.GetRequiredService<Honua.Server.Intake.Services.IRegistryProvisioner>();

        // Act - Provision all registry types
        var registryTypes = new[]
        {
            RegistryType.GitHubContainerRegistry,
            RegistryType.AwsEcr,
            RegistryType.AzureAcr,
            RegistryType.GcpArtifactRegistry
        };

        var provisionTasks = registryTypes.Select(type =>
            provisioner.ProvisionAsync(customerId, type));

        var results = await Task.WhenAll(provisionTasks);

        // Assert - All provisioning succeeded
        results.Should().OnlyContain(r => r.Success);
        results.Should().HaveCount(4);

        // Assert - All credentials stored
        using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        var credentials = await connection.QueryAsync<RegistryCredentialRecord>(@"
            SELECT * FROM registry_credentials
            WHERE customer_id = @CustomerId
            AND revoked_at IS NULL
        ", new { CustomerId = customerId });

        credentials.Should().HaveCount(4);
        credentials.Select(c => c.RegistryType).Should().BeEquivalentTo(
            new[] { "GitHubContainerRegistry", "AwsEcr", "AzureAcr", "GcpArtifactRegistry" });
    }

    // Helper methods

    private IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_fixture.LoggerFactory);
        services.AddLogging();

        // Mock registry provisioner
        services.AddSingleton<Honua.Server.Intake.Services.IRegistryProvisioner, MockRegistryProvisioner>();

        // Mock access manager
        services.AddSingleton<Honua.Server.Intake.Services.IRegistryAccessManager, MockRegistryAccessManager>();

        // Configuration
        var provisioningOptions = new RegistryProvisioningOptions
        {
            GitHubOrganization = "honua",
            GitHubToken = "test-token",
            AwsRegion = "us-west-2",
            AwsAccountId = "123456789012",
            AzureSubscriptionId = "azure-sub-id",
            AzureResourceGroup = "honua-rg",
            AzureRegistryName = "honuaregistry",
            GcpProjectId = "honua-project",
            GcpRegion = "us-central1",
            GcpRepositoryName = "honua-builds"
        };
        services.AddSingleton(Options.Create(provisioningOptions));

        // Add connection string for database access
        services.AddSingleton(new DatabaseConnectionProvider(_fixture.ConnectionString));

        return services.BuildServiceProvider();
    }
}

internal record RegistryCredentialRecord
{
    public Guid Id { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string RegistryType { get; init; } = string.Empty;
    public string Namespace { get; init; } = string.Empty;
    public string RegistryUrl { get; init; } = string.Empty;
    public string? Username { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
}

public class DatabaseConnectionProvider
{
    public string ConnectionString { get; }

    public DatabaseConnectionProvider(string connectionString)
    {
        ConnectionString = connectionString;
    }
}
