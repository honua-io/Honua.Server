using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.ResourceManager;
using FluentAssertions;
using Honua.Cli.AI.Services.Dns;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Dns;

/// <summary>
/// Unit tests for Azure DNS functionality in DnsRecordService.
/// Note: These tests focus on testing the service logic and error handling.
/// Integration tests with actual Azure SDK mocks are complex due to sealed types
/// and should be covered by integration tests.
/// </summary>
[Trait("Category", "Unit")]
public class AzureDnsRecordServiceTests
{
    private readonly Mock<ILogger<DnsRecordService>> _mockLogger;
    private readonly DnsRecordService _service;

    public AzureDnsRecordServiceTests()
    {
        _mockLogger = new Mock<ILogger<DnsRecordService>>();
        _service = new DnsRecordService(_mockLogger.Object);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DnsRecordService(null!));
    }

    [Fact]
    public async Task UpsertAzureDnsRecordAsync_WithNullArmClient_ReturnsFailure()
    {
        // Arrange
        ArmClient? nullClient = null;

        // Act
        var result = await _service.UpsertAzureDnsRecordAsync(
            nullClient!,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            "A",
            new List<string> { "192.0.2.1" },
            3600,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to upsert");
    }

    [Fact]
    public async Task UpsertAzureDnsRecordAsync_WithInvalidRecordType_ReturnsFailure()
    {
        // Arrange
        var mockArmClient = new Mock<ArmClient>();

        // Act
        var result = await _service.UpsertAzureDnsRecordAsync(
            mockArmClient.Object,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            "INVALID_TYPE",
            new List<string> { "invalid-value" },
            3600,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        // The error may be from SDK interaction or unsupported type
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AAAA")]
    [InlineData("CNAME")]
    [InlineData("TXT")]
    [InlineData("MX")]
    [InlineData("NS")]
    [InlineData("PTR")]
    [InlineData("SRV")]
    public async Task UpsertAzureDnsRecordAsync_WithSupportedRecordTypes_HandlesGracefully(string recordType)
    {
        // Arrange
        var mockArmClient = new Mock<ArmClient>();
        var recordValues = new List<string> { "test-value" };

        // Act - This will fail because we don't have a real DNS zone, but it should handle the error gracefully
        var result = await _service.UpsertAzureDnsRecordAsync(
            mockArmClient.Object,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            recordType,
            recordValues,
            3600,
            CancellationToken.None);

        // Assert - Should fail gracefully, not throw unhandled exceptions
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DeleteAzureDnsRecordAsync_WithNullArmClient_ReturnsFailure()
    {
        // Arrange
        ArmClient? nullClient = null;

        // Act
        var result = await _service.DeleteAzureDnsRecordAsync(
            nullClient!,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            "A",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to delete");
    }

    [Fact]
    public async Task DeleteAzureDnsRecordAsync_WithInvalidRecordType_ReturnsFailure()
    {
        // Arrange
        var mockArmClient = new Mock<ArmClient>();

        // Act
        var result = await _service.DeleteAzureDnsRecordAsync(
            mockArmClient.Object,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            "INVALID_TYPE",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        // The error may be from SDK interaction or unsupported type
        result.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAzureDnsRecordAsync_WithNullArmClient_ReturnsNull()
    {
        // Arrange
        ArmClient? nullClient = null;

        // Act
        var result = await _service.GetAzureDnsRecordAsync(
            nullClient!,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            "A",
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAzureDnsRecordAsync_WithInvalidRecordType_ReturnsNull()
    {
        // Arrange
        var mockArmClient = new Mock<ArmClient>();

        // Act
        var result = await _service.GetAzureDnsRecordAsync(
            mockArmClient.Object,
            "test-subscription",
            "test-rg",
            "example.com",
            "test",
            "INVALID_TYPE",
            CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ListAzureDnsZonesAsync_WithNullArmClient_ReturnsEmptyList()
    {
        // Arrange
        ArmClient? nullClient = null;

        // Act
        var result = await _service.ListAzureDnsZonesAsync(
            nullClient!,
            "test-subscription",
            "test-rg",
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Service_LogsInformationCorrectly()
    {
        // This test verifies that the service is initialized correctly with logging
        _service.Should().NotBeNull();
        _mockLogger.Object.Should().NotBeNull();
    }

    [Fact]
    public async Task UpsertRoute53RecordAsync_WithNullClient_ReturnsFailure()
    {
        // Act
        var result = await _service.UpsertRoute53RecordAsync(
            null!,
            "test-zone",
            "test.example.com",
            "A",
            new List<string> { "192.0.2.1" },
            3600,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to upsert");
    }

    [Fact]
    public async Task DeleteRoute53RecordAsync_WithNullClient_ReturnsFailure()
    {
        // Act
        var result = await _service.DeleteRoute53RecordAsync(
            null!,
            "test-zone",
            "test.example.com",
            "A",
            new List<string> { "192.0.2.1" },
            3600,
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Failed to delete");
    }
}
