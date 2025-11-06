// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
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
/// Tests for BuildDeliveryService - delivers builds to customer registries with caching.
/// </summary>
[Trait("Category", "Unit")]
public class BuildDeliveryServiceTests
{
    private readonly Mock<ILogger<BuildDeliveryService>> _mockLogger;
    private readonly Mock<IRegistryCacheChecker> _mockCacheChecker;
    private readonly Mock<IRegistryAccessManager> _mockAccessManager;
    private readonly BuildDeliveryOptions _deliveryOptions;
    private readonly RegistryProvisioningOptions _registryOptions;

    public BuildDeliveryServiceTests()
    {
        _mockLogger = new Mock<ILogger<BuildDeliveryService>>();
        _mockCacheChecker = new Mock<IRegistryCacheChecker>();
        _mockAccessManager = new Mock<IRegistryAccessManager>();

        _deliveryOptions = new BuildDeliveryOptions
        {
            CranePath = "crane",
            SkopeoPath = "skopeo",
            PreferredTool = "crane",
            CopyTimeoutSeconds = 600,
            AutoTagLatest = true,
            AutoTagArchitecture = true
        };

        _registryOptions = new RegistryProvisioningOptions
        {
            GitHubOrganization = "honua",
            AwsAccountId = "123456789012",
            AwsRegion = "us-west-2",
            AzureRegistryName = "honuatest",
            GcpProjectId = "honua-test",
            GcpRegion = "us-central1",
            GcpRepositoryName = "honua-builds"
        };
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new BuildDeliveryService(
            null!,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DeliverBuildAsync_NullCacheKey_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var act = async () => await service.DeliverBuildAsync(null!, RegistryType.AwsEcr);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DeliverBuildAsync_AccessDenied_ReturnsFailure()
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-123",
            BuildName = "honua-server",
            Version = "latest",
            Architecture = "linux-x64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync("customer-123", RegistryType.AwsEcr, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryAccessResult
            {
                AccessGranted = false,
                DenialReason = "Customer not authorized"
            });

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.DeliverBuildAsync(cacheKey, RegistryType.AwsEcr);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("authorized");
    }

    [Fact]
    public async Task DeliverBuildAsync_CacheHit_ReturnsCachedBuild()
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-123",
            BuildName = "honua-server",
            Version = "latest",
            Architecture = "linux-x64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync("customer-123", RegistryType.AwsEcr, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryAccessResult { AccessGranted = true });

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(cacheKey, RegistryType.AwsEcr, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheCheckResult
            {
                Exists = true,
                ImageReference = "123456789012.dkr.ecr.us-west-2.amazonaws.com/honua/customer-123/honua-server:latest",
                Digest = "sha256:abc123"
            });

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.DeliverBuildAsync(cacheKey, RegistryType.AwsEcr);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.WasCached.Should().BeTrue();
        result.ImageReference.Should().Contain("ecr");
        result.Digest.Should().Be("sha256:abc123");
    }

    [Fact]
    public async Task DeliverBuildAsync_CacheMiss_BuildsAndDelivers()
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-123",
            BuildName = "honua-server",
            Version = "1.0.0",
            Architecture = "linux-arm64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync("customer-123", RegistryType.GitHubContainerRegistry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryAccessResult { AccessGranted = true });

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(cacheKey, RegistryType.GitHubContainerRegistry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.DeliverBuildAsync(cacheKey, RegistryType.GitHubContainerRegistry);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.WasCached.Should().BeFalse();
        result.ImageReference.Should().Contain("ghcr.io");
    }

    [Fact]
    public async Task DeliverBuildAsync_AutoTagLatest_AppliesLatestTag()
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-456",
            BuildName = "honua-server-pro",
            Version = "2.0.0",
            Architecture = "linux-x64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync(It.IsAny<string>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryAccessResult { AccessGranted = true });

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<BuildCacheKey>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.DeliverBuildAsync(cacheKey, RegistryType.AwsEcr);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AdditionalTags.Should().Contain("latest");
    }

    [Fact]
    public async Task DeliverBuildAsync_AutoTagArchitecture_AppliesArchitectureTag()
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-789",
            BuildName = "honua-server",
            Version = "1.5.0",
            Architecture = "linux-arm64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync(It.IsAny<string>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryAccessResult { AccessGranted = true });

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<BuildCacheKey>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.DeliverBuildAsync(cacheKey, RegistryType.AzureAcr);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.AdditionalTags.Should().Contain(tag => tag.Contains("arm64"));
    }

    [Fact]
    public async Task CopyImageAsync_NullSourceImage_ThrowsArgumentException()
    {
        // Arrange
        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var act = async () => await service.CopyImageAsync(null!, "target:latest");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CopyImageAsync_NullTargetImage_ThrowsArgumentException()
    {
        // Arrange
        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var act = async () => await service.CopyImageAsync("source:latest", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TagImageAsync_NullImageReference_ThrowsArgumentException()
    {
        // Arrange
        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var act = async () => await service.TagImageAsync(null!, new[] { "v1", "latest" });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TagImageAsync_EmptyTagsList_ReturnsEmptyList()
    {
        // Arrange
        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.TagImageAsync("image:1.0", Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RegistryType.GitHubContainerRegistry, "ghcr.io")]
    [InlineData(RegistryType.AwsEcr, "dkr.ecr")]
    [InlineData(RegistryType.AzureAcr, "azurecr.io")]
    [InlineData(RegistryType.GcpArtifactRegistry, "docker.pkg.dev")]
    public async Task DeliverBuildAsync_GeneratesCorrectRegistryUrl(RegistryType registryType, string expectedUrlPart)
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-test",
            BuildName = "test-build",
            Version = "1.0",
            Architecture = "linux-x64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync(It.IsAny<string>(), registryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegistryAccessResult { AccessGranted = true });

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<BuildCacheKey>(), registryType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        // Act
        var result = await service.DeliverBuildAsync(cacheKey, registryType);

        // Assert
        result.Should().NotBeNull();
        result.ImageReference.Should().Contain(expectedUrlPart);
    }

    [Fact]
    public async Task DeliverBuildAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var cacheKey = new BuildCacheKey
        {
            CustomerId = "customer-cancel",
            BuildName = "test",
            Version = "1.0",
            Architecture = "linux-x64"
        };

        _mockAccessManager
            .Setup(x => x.ValidateAccessAsync(It.IsAny<string>(), It.IsAny<RegistryType>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new BuildDeliveryService(
            _mockLogger.Object,
            Options.Create(_deliveryOptions),
            _mockCacheChecker.Object,
            _mockAccessManager.Object,
            Options.Create(_registryOptions));

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = async () => await service.DeliverBuildAsync(cacheKey, RegistryType.AwsEcr, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
