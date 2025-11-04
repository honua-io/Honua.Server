using System;
using FluentAssertions;
using Honua.Cli.AI.Services.Dns;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Dns;

/// <summary>
/// Unit tests for AzureDnsClientFactory.
/// </summary>
[Trait("Category", "Unit")]
public class AzureDnsClientFactoryTests
{
    private readonly Mock<ILogger<AzureDnsClientFactory>> _mockLogger;
    private readonly AzureDnsClientFactory _factory;

    public AzureDnsClientFactoryTests()
    {
        _mockLogger = new Mock<ILogger<AzureDnsClientFactory>>();
        _factory = new AzureDnsClientFactory(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AzureDnsClientFactory(null!));
    }

    [Fact]
    public void CreateClient_ReturnsArmClient()
    {
        // Act
        var client = _factory.CreateClient();

        // Assert
        client.Should().NotBeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Creating Azure Resource Manager client")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void CreateClient_CreatesClientWithDefaultAzureCredential()
    {
        // Act
        var client = _factory.CreateClient();

        // Assert
        client.Should().NotBeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DefaultAzureCredential")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void CreateManagedIdentityClient_WithoutClientId_ReturnsArmClient()
    {
        // Act
        var client = _factory.CreateManagedIdentityClient();

        // Assert
        client.Should().NotBeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Managed Identity")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void CreateManagedIdentityClient_WithClientId_ReturnsArmClient()
    {
        // Arrange
        var clientId = "12345678-1234-1234-1234-123456789012";

        // Act
        var client = _factory.CreateManagedIdentityClient(clientId);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateServicePrincipalClient_WithValidCredentials_ReturnsArmClient()
    {
        // Arrange
        var tenantId = "87654321-4321-4321-4321-210987654321";
        var clientId = "12345678-1234-1234-1234-123456789012";
        var clientSecret = "test-secret";

        // Act
        var client = _factory.CreateServicePrincipalClient(tenantId, clientId, clientSecret);

        // Assert
        client.Should().NotBeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Service Principal")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void CreateAzureCliClient_ReturnsArmClient()
    {
        // Act
        var client = _factory.CreateAzureCliClient();

        // Assert
        client.Should().NotBeNull();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Azure CLI")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void CreateClient_CalledMultipleTimes_CreatesNewClientEachTime()
    {
        // Act
        var client1 = _factory.CreateClient();
        var client2 = _factory.CreateClient();

        // Assert
        client1.Should().NotBeNull();
        client2.Should().NotBeNull();
        client1.Should().NotBeSameAs(client2);
    }

    [Fact]
    public void CreateManagedIdentityClient_WithNullClientId_UsesSystemAssignedIdentity()
    {
        // Act
        var client = _factory.CreateManagedIdentityClient(null);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateManagedIdentityClient_WithEmptyClientId_UsesSystemAssignedIdentity()
    {
        // Act
        var client = _factory.CreateManagedIdentityClient(string.Empty);

        // Assert
        client.Should().NotBeNull();
    }
}
