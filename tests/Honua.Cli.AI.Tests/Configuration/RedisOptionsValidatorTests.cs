using Honua.Cli.AI.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Cli.AI.Tests.Configuration;

/// <summary>
/// Tests for Redis configuration validation.
/// </summary>
[Collection("AITests")]
[Trait("Category", "Integration")]
public sealed class RedisOptionsValidatorTests
{
    [Fact]
    public void RedisValidator_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            KeyPrefix = "honua:process:",
            TtlSeconds = 86400,
            ConnectTimeoutMs = 5000,
            SyncTimeoutMs = 1000
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RedisValidator_WithDisabled_ReturnsSuccess()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = false,
            ConnectionString = null // Connection string not required when disabled
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void RedisValidator_WithEnabledButNoConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = null
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("ConnectionString"));
    }

    [Fact]
    public void RedisValidator_WithInvalidConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "invalid-no-port"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("ConnectionString") && f.Contains("invalid"));
    }

    [Fact]
    public void RedisValidator_WithInvalidPort_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:99999" // Port out of range
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("ConnectionString"));
    }

    [Fact]
    public void RedisValidator_WithEmptyKeyPrefix_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            KeyPrefix = ""
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("KeyPrefix"));
    }

    [Fact]
    public void RedisValidator_WithNegativeTtlSeconds_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            TtlSeconds = -1
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("TtlSeconds"));
    }

    [Fact]
    public void RedisValidator_WithExcessiveTtlSeconds_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            TtlSeconds = 3_000_000 // More than 30 days
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("TtlSeconds"));
    }

    [Fact]
    public void RedisValidator_WithNegativeConnectTimeout_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            ConnectTimeoutMs = -1
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("ConnectTimeoutMs"));
    }

    [Fact]
    public void RedisValidator_WithExcessiveConnectTimeout_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            ConnectTimeoutMs = 120000 // 2 minutes
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("ConnectTimeoutMs"));
    }

    [Fact]
    public void RedisValidator_WithExcessiveSyncTimeout_ReturnsFail()
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = "localhost:6379",
            SyncTimeoutMs = 60000 // 60 seconds
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("SyncTimeoutMs"));
    }

    [Theory]
    [InlineData("localhost:6379")]
    [InlineData("redis.example.com:6380")]
    [InlineData("localhost:6379,password=secret")]
    [InlineData("localhost:6379,password=secret,ssl=true")]
    [InlineData("localhost:6379,abortConnect=false")]
    public void RedisValidator_WithValidConnectionStrings_ReturnsSuccess(string connectionString)
    {
        // Arrange
        var validator = new RedisOptionsValidator();
        var options = new RedisOptions
        {
            Enabled = true,
            ConnectionString = connectionString
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded, $"Connection string '{connectionString}' should be valid");
    }
}
