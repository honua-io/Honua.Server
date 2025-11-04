// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class LazyRedisInitializerTests
{
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IConfigurationSection> _mockConnectionStringsSection;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly ILogger<LazyRedisInitializer> _logger;

    public LazyRedisInitializerTests()
    {
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConnectionStringsSection = new Mock<IConfigurationSection>();
        _mockEnvironment = new Mock<IHostEnvironment>();
        _logger = NullLogger<LazyRedisInitializer>.Instance;

        // Setup the ConnectionStrings section
        _mockConfiguration
            .Setup(c => c.GetSection("ConnectionStrings"))
            .Returns(_mockConnectionStringsSection.Object);

        // Default to production environment
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
    }

    [Fact]
    public async Task StartAsync_WhenRedisNotConfigured_CompletesWithoutError()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var initializer = CreateInitializer();

        // Act & Assert
        await initializer.StartAsync(CancellationToken.None);

        initializer.IsConnected.Should().BeFalse();
        initializer.Redis.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_InProduction_LogsWarningWhenRedisNotConfigured()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var loggerMock = new Mock<ILogger<LazyRedisInitializer>>();
        var initializer = new LazyRedisInitializer(
            _mockConfiguration.Object,
            loggerMock.Object,
            _mockEnvironment.Object);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Redis connection string not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_InDevelopment_LogsDebugWhenRedisNotConfigured()
    {
        // Arrange
        _mockEnvironment.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var loggerMock = new Mock<ILogger<LazyRedisInitializer>>();
        var initializer = new LazyRedisInitializer(
            _mockConfiguration.Object,
            loggerMock.Object,
            _mockEnvironment.Object);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Redis not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_WhenRedisConfigured_DoesNotBlockStartup()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns("localhost:6379");

        var initializer = CreateInitializer();

        // Act
        var startTime = DateTime.UtcNow;
        await initializer.StartAsync(CancellationToken.None);
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        // Assert - Should return immediately, not wait for connection
        elapsedMs.Should().BeLessThan(100, "StartAsync should not block on Redis connection");

        // Redis should not be connected yet
        initializer.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task StartAsync_InitializesRedisInBackground()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns("localhost:6379");

        var loggerMock = new Mock<ILogger<LazyRedisInitializer>>();
        var initializer = new LazyRedisInitializer(
            _mockConfiguration.Object,
            loggerMock.Object,
            _mockEnvironment.Object);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Wait a bit longer than the internal delay (1500ms) plus connection time
        // Note: This will fail to connect since we don't have a real Redis instance,
        // but we can verify the attempt was made via logs
        await Task.Delay(2000);

        // Assert - Should have attempted to log the background initialization
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Establishing Redis connection in background")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InitializeRedisAsync_HandlesConnectionFailureGracefully()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns("invalid-host:6379"); // This will fail to connect

        var loggerMock = new Mock<ILogger<LazyRedisInitializer>>();
        var initializer = new LazyRedisInitializer(
            _mockConfiguration.Object,
            loggerMock.Object,
            _mockEnvironment.Object);

        // Act
        await initializer.StartAsync(CancellationToken.None);
        await Task.Delay(2000); // Wait for background initialization to fail

        // Assert - Should have logged a warning about the failure
        loggerMock.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Warning || l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task StopAsync_DisposesRedisConnection()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var initializer = CreateInitializer();
        await initializer.StartAsync(CancellationToken.None);

        // Act & Assert - Should not throw
        await initializer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Dispose_DisposesRedisConnection()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var initializer = CreateInitializer();

        // Act & Assert - Should not throw
        initializer.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var initializer = CreateInitializer();

        // Act & Assert
        initializer.Dispose();
        initializer.Dispose(); // Should not throw
    }

    [Fact]
    public async Task Redis_Property_ReturnsNullWhenNotConnected()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var initializer = CreateInitializer();

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        initializer.Redis.Should().BeNull();
    }

    [Fact]
    public async Task IsConnected_Property_ReturnsFalseWhenNotConnected()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns((string)null!);

        var initializer = CreateInitializer();

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Assert
        initializer.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Constructor_ThrowsWhenConfigurationIsNull()
    {
        // Act & Assert
        var act = () => new LazyRedisInitializer(null!, _logger, _mockEnvironment.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Constructor_ThrowsWhenLoggerIsNull()
    {
        // Act & Assert
        var act = () => new LazyRedisInitializer(_mockConfiguration.Object, null!, _mockEnvironment.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Constructor_ThrowsWhenEnvironmentIsNull()
    {
        // Act & Assert
        var act = () => new LazyRedisInitializer(_mockConfiguration.Object, _logger, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_WithEmptyConnectionString_DoesNotInitialize()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns("");

        var initializer = CreateInitializer();

        // Act
        await initializer.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Assert
        initializer.IsConnected.Should().BeFalse();
        initializer.Redis.Should().BeNull();
    }

    [Fact]
    public async Task StartAsync_WithWhitespaceConnectionString_DoesNotInitialize()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns("   ");

        var initializer = CreateInitializer();

        // Act
        await initializer.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // Assert
        initializer.IsConnected.Should().BeFalse();
        initializer.Redis.Should().BeNull();
    }

    [Fact]
    public async Task BackgroundInitialization_RespectsDelay()
    {
        // Arrange
        _mockConnectionStringsSection
            .Setup(c => c["Redis"])
            .Returns("localhost:6379");

        var loggerMock = new Mock<ILogger<LazyRedisInitializer>>();
        var initializer = new LazyRedisInitializer(
            _mockConfiguration.Object,
            loggerMock.Object,
            _mockEnvironment.Object);

        // Act
        await initializer.StartAsync(CancellationToken.None);

        // Check before the delay period (1500ms)
        await Task.Delay(500);

        // Assert - Should not have attempted connection yet
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Establishing Redis connection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);

        // Wait for the delay to complete
        await Task.Delay(1500);

        // Should have attempted connection by now
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Establishing Redis connection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private LazyRedisInitializer CreateInitializer()
    {
        return new LazyRedisInitializer(
            _mockConfiguration.Object,
            _logger,
            _mockEnvironment.Object);
    }
}
