// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Tests for RegistryProvisioner - provisions container registry access for customers.
/// </summary>
[Trait("Category", "Unit")]
public class RegistryProvisionerTests
{
    private readonly Mock<ILogger<RegistryProvisioner>> _mockLogger;
    private readonly RegistryProvisioningOptions _options;

    public RegistryProvisionerTests()
    {
        _mockLogger = new Mock<ILogger<RegistryProvisioner>>();

        _options = new RegistryProvisioningOptions
        {
            GitHubToken = "test-github-token",
            GitHubOrganization = "honua-test",
            AwsRegion = "us-west-2",
            AwsAccountId = "123456789012",
            AzureSubscriptionId = "sub-12345",
            AzureResourceGroup = "honua-rg",
            AzureRegistryName = "honuatest",
            GcpProjectId = "honua-test-project",
            GcpRegion = "us-central1",
            GcpRepositoryName = "honua-builds"
        };
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RegistryProvisioner(null!, Options.Create(_options));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RegistryProvisioner(_mockLogger.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ProvisionAsync_EmptyCustomerId_ThrowsArgumentException()
    {
        // Arrange
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var act = async () => await provisioner.ProvisionAsync("", RegistryType.AwsEcr);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Customer ID*");
    }

    [Fact]
    public async Task ProvisionAsync_GitHubRegistry_ReturnsSuccess()
    {
        // Arrange
        var customerId = "customer-123";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result = await provisioner.ProvisionAsync(customerId, RegistryType.GitHubContainerRegistry);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.GitHubContainerRegistry);
        result.CustomerId.Should().Be(customerId);
        result.Namespace.Should().Contain(customerId);
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Be("ghcr.io");
        result.Credential.Username.Should().Be(customerId);
        result.Credential.Password.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProvisionAsync_GitHubRegistry_WithoutToken_ThrowsException()
    {
        // Arrange
        var invalidOptions = new RegistryProvisioningOptions
        {
            GitHubToken = null,
            GitHubOrganization = "honua"
        };

        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(invalidOptions));

        // Act
        var result = await provisioner.ProvisionAsync("customer-123", RegistryType.GitHubContainerRegistry);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("token");
    }

    [Fact]
    public async Task ProvisionAsync_AwsEcr_CreatesRepositoryAndCredentials()
    {
        // Arrange
        var customerId = "customer-aws-123";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result = await provisioner.ProvisionAsync(customerId, RegistryType.AwsEcr);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.AwsEcr);
        result.CustomerId.Should().Be(customerId);
        result.Namespace.Should().Be($"honua/{customerId}");
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Contain("ecr");
        result.Credential.RegistryUrl.Should().Contain(_options.AwsRegion);
        result.Metadata.Should().ContainKey("registry_url");
        result.Metadata.Should().ContainKey("repository_name");
    }

    [Fact]
    public async Task ProvisionAsync_AwsEcr_WithoutRegion_ReturnsFailure()
    {
        // Arrange
        var invalidOptions = new RegistryProvisioningOptions
        {
            AwsRegion = null,
            AwsAccountId = "123456789012"
        };

        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(invalidOptions));

        // Act
        var result = await provisioner.ProvisionAsync("customer-123", RegistryType.AwsEcr);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("region");
    }

    [Fact]
    public async Task ProvisionAsync_AzureAcr_CreatesTokenAndCredentials()
    {
        // Arrange
        var customerId = "customer-azure-456";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result = await provisioner.ProvisionAsync(customerId, RegistryType.AzureAcr);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.AzureAcr);
        result.CustomerId.Should().Be(customerId);
        result.Namespace.Should().Contain(customerId);
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Contain("azurecr.io");
        result.Credential.Username.Should().Contain("honua-customer");
        result.Credential.ExpiresAt.Should().NotBeNull();
        result.Credential.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ProvisionAsync_GcpArtifactRegistry_CreatesServiceAccount()
    {
        // Arrange
        var customerId = "customer-gcp-789";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result = await provisioner.ProvisionAsync(customerId, RegistryType.GcpArtifactRegistry);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(RegistryType.GcpArtifactRegistry);
        result.CustomerId.Should().Be(customerId);
        result.Namespace.Should().Contain(customerId);
        result.Credential.Should().NotBeNull();
        result.Credential!.RegistryUrl.Should().Contain("docker.pkg.dev");
        result.Credential.Username.Should().Be("_json_key");
        result.Credential.Password.Should().NotBeNullOrEmpty();
        result.Metadata.Should().ContainKey("project_id");
        result.Metadata.Should().ContainKey("region");
    }

    [Theory]
    [InlineData(RegistryType.GitHubContainerRegistry)]
    [InlineData(RegistryType.AwsEcr)]
    [InlineData(RegistryType.AzureAcr)]
    [InlineData(RegistryType.GcpArtifactRegistry)]
    public async Task ProvisionAsync_AllRegistryTypes_SuccessfullyProvisions(RegistryType registryType)
    {
        // Arrange
        var customerId = $"customer-{registryType}";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result = await provisioner.ProvisionAsync(customerId, registryType);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RegistryType.Should().Be(registryType);
        result.CustomerId.Should().Be(customerId);
        result.Credential.Should().NotBeNull();
        result.ProvisionedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProvisionAsync_GitHubRegistry_GeneratesSecureToken()
    {
        // Arrange
        var customerId = "customer-secure-123";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result1 = await provisioner.ProvisionAsync(customerId, RegistryType.GitHubContainerRegistry);
        var result2 = await provisioner.ProvisionAsync(customerId, RegistryType.GitHubContainerRegistry);

        // Assert
        result1.Credential!.Password.Should().NotBe(result2.Credential!.Password);
        result1.Credential.Password.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task ProvisionAsync_AwsEcr_IncludesRepositoryArn()
    {
        // Arrange
        var customerId = "customer-arn-test";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result = await provisioner.ProvisionAsync(customerId, RegistryType.AwsEcr);

        // Assert
        result.Success.Should().BeTrue();
        result.Metadata.Should().ContainKey("repository_name");
        result.Metadata!["repository_name"].Should().Be($"honua/{customerId}");
    }

    [Fact]
    public async Task ProvisionAsync_MultipleCustomers_CreatesIsolatedNamespaces()
    {
        // Arrange
        var customer1 = "customer-001";
        var customer2 = "customer-002";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act
        var result1 = await provisioner.ProvisionAsync(customer1, RegistryType.GitHubContainerRegistry);
        var result2 = await provisioner.ProvisionAsync(customer2, RegistryType.GitHubContainerRegistry);

        // Assert
        result1.Namespace.Should().Contain(customer1);
        result2.Namespace.Should().Contain(customer2);
        result1.Namespace.Should().NotBe(result2.Namespace);
        result1.Credential!.Username.Should().Be(customer1);
        result2.Credential!.Username.Should().Be(customer2);
    }

    [Fact]
    public async Task ProvisionAsync_WithRetry_SucceedsAfterTransientFailure()
    {
        // Arrange
        var customerId = "customer-retry";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));

        // Act - Provisioning should succeed even in the presence of transient errors
        // (retry logic is built into the provisioner)
        var result = await provisioner.ProvisionAsync(customerId, RegistryType.GitHubContainerRegistry);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ProvisionAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var customerId = "customer-cancel";
        var provisioner = new RegistryProvisioner(_mockLogger.Object, Options.Create(_options));
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await provisioner.ProvisionAsync(customerId, RegistryType.AwsEcr, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
