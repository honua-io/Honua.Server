// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using FluentAssertions;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Configuration;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class ConnectionPoolWarmupOptionsTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var options = new ConnectionPoolWarmupOptions();

        // Assert
        options.Enabled.Should().BeTrue("warmup should be enabled by default");
        options.EnableInDevelopment.Should().BeFalse("warmup should be disabled in dev by default");
        options.StartupDelayMs.Should().Be(1000, "default delay is 1 second");
        options.MaxConcurrentWarmups.Should().Be(3, "default concurrency is 3");
        options.MaxDataSources.Should().Be(10, "default max data sources is 10");
        options.TimeoutMs.Should().Be(5000, "default timeout is 5 seconds");
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        ConnectionPoolWarmupOptions.SectionName.Should().Be("ConnectionPoolWarmup");
    }

    [Fact]
    public void LoadFromConfiguration_AllProperties()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionPoolWarmup:Enabled"] = "false",
                ["ConnectionPoolWarmup:EnableInDevelopment"] = "true",
                ["ConnectionPoolWarmup:StartupDelayMs"] = "2000",
                ["ConnectionPoolWarmup:MaxConcurrentWarmups"] = "5",
                ["ConnectionPoolWarmup:MaxDataSources"] = "20",
                ["ConnectionPoolWarmup:TimeoutMs"] = "10000"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Enabled.Should().BeFalse();
        options.EnableInDevelopment.Should().BeTrue();
        options.StartupDelayMs.Should().Be(2000);
        options.MaxConcurrentWarmups.Should().Be(5);
        options.MaxDataSources.Should().Be(20);
        options.TimeoutMs.Should().Be(10000);
    }

    [Fact]
    public void LoadFromConfiguration_PartialConfiguration_UsesDefaults()
    {
        // Arrange - Only configure some properties
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionPoolWarmup:Enabled"] = "false",
                ["ConnectionPoolWarmup:MaxConcurrentWarmups"] = "7"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert - Configured properties should be set, others should use defaults
        options.Should().NotBeNull();
        options!.Enabled.Should().BeFalse("was configured");
        options.MaxConcurrentWarmups.Should().Be(7, "was configured");
        options.StartupDelayMs.Should().Be(1000, "should use default");
        options.MaxDataSources.Should().Be(10, "should use default");
        options.TimeoutMs.Should().Be(5000, "should use default");
    }

    [Fact]
    public void LoadFromConfiguration_EmptyConfiguration_UsesDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act - Get returns null for missing section, use new instance instead
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>() ?? new ConnectionPoolWarmupOptions();

        // Assert - Should create instance with default values
        options.Should().NotBeNull();
        options.Enabled.Should().BeTrue();
        options.StartupDelayMs.Should().Be(1000);
    }

    [Fact]
    public void LoadFromConfiguration_InvalidValues_ThrowsException()
    {
        // Arrange - Invalid numeric values
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionPoolWarmup:StartupDelayMs"] = "invalid",
                ["ConnectionPoolWarmup:MaxConcurrentWarmups"] = "not-a-number"
            })
            .Build();

        // Act & Assert - .NET 9 Configuration system throws for invalid values
        var act = () => configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Failed to convert configuration value*");
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions();

        // Act
        options.Enabled = false;
        options.EnableInDevelopment = true;
        options.StartupDelayMs = 3000;
        options.MaxConcurrentWarmups = 10;
        options.MaxDataSources = 50;
        options.TimeoutMs = 15000;

        // Assert
        options.Enabled.Should().BeFalse();
        options.EnableInDevelopment.Should().BeTrue();
        options.StartupDelayMs.Should().Be(3000);
        options.MaxConcurrentWarmups.Should().Be(10);
        options.MaxDataSources.Should().Be(50);
        options.TimeoutMs.Should().Be(15000);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(5000)]
    public void StartupDelayMs_AcceptsValidValues(int delay)
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions();

        // Act
        options.StartupDelayMs = delay;

        // Assert
        options.StartupDelayMs.Should().Be(delay);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(100)]
    public void MaxConcurrentWarmups_AcceptsValidValues(int max)
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions();

        // Act
        options.MaxConcurrentWarmups = max;

        // Assert
        options.MaxConcurrentWarmups.Should().Be(max);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void MaxDataSources_AcceptsValidValues(int max)
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions();

        // Act
        options.MaxDataSources = max;

        // Assert
        options.MaxDataSources.Should().Be(max);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(30000)]
    public void TimeoutMs_AcceptsValidValues(int timeout)
    {
        // Arrange
        var options = new ConnectionPoolWarmupOptions();

        // Act
        options.TimeoutMs = timeout;

        // Assert
        options.TimeoutMs.Should().Be(timeout);
    }

    [Fact]
    public void LoadFromJson_ProductionConfiguration()
    {
        // Arrange - Simulate production configuration
        var json = @"{
            ""ConnectionPoolWarmup"": {
                ""Enabled"": true,
                ""EnableInDevelopment"": false,
                ""StartupDelayMs"": 1000,
                ""MaxConcurrentWarmups"": 3,
                ""MaxDataSources"": 10,
                ""TimeoutMs"": 5000
            }
        }";

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Enabled.Should().BeTrue();
        options.EnableInDevelopment.Should().BeFalse();
        options.StartupDelayMs.Should().Be(1000);
        options.MaxConcurrentWarmups.Should().Be(3);
        options.MaxDataSources.Should().Be(10);
        options.TimeoutMs.Should().Be(5000);
    }

    [Fact]
    public void LoadFromJson_DevelopmentConfiguration()
    {
        // Arrange - Simulate development configuration (disabled)
        var json = @"{
            ""ConnectionPoolWarmup"": {
                ""Enabled"": false
            }
        }";

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Enabled.Should().BeFalse();
        // Other properties should use defaults
        options.StartupDelayMs.Should().Be(1000);
        options.MaxConcurrentWarmups.Should().Be(3);
    }

    [Fact]
    public void LoadFromJson_ServerlessConfiguration()
    {
        // Arrange - Simulate serverless/Cloud Run configuration
        var json = @"{
            ""ConnectionPoolWarmup"": {
                ""Enabled"": true,
                ""StartupDelayMs"": 0,
                ""MaxConcurrentWarmups"": 5,
                ""MaxDataSources"": 5,
                ""TimeoutMs"": 3000
            }
        }";

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Enabled.Should().BeTrue();
        options.StartupDelayMs.Should().Be(0, "serverless should warmup immediately");
        options.MaxConcurrentWarmups.Should().Be(5, "higher concurrency for fast warmup");
        options.MaxDataSources.Should().Be(5, "fewer data sources to warmup quickly");
        options.TimeoutMs.Should().Be(3000, "shorter timeout for serverless");
    }

    [Fact]
    public void LoadFromEnvironmentVariables()
    {
        // Arrange - Environment variables use __ as separator, but need : for GetSection
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionPoolWarmup:Enabled"] = "true",
                ["ConnectionPoolWarmup:StartupDelayMs"] = "2000",
                ["ConnectionPoolWarmup:MaxConcurrentWarmups"] = "7"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Enabled.Should().BeTrue();
        options.StartupDelayMs.Should().Be(2000);
        options.MaxConcurrentWarmups.Should().Be(7);
    }

    [Fact]
    public void ConfigurationOverride_EnvironmentOverridesJson()
    {
        // Arrange - JSON says enabled, environment says disabled
        var json = @"{
            ""ConnectionPoolWarmup"": {
                ""Enabled"": true,
                ""StartupDelayMs"": 1000
            }
        }";

        var configuration = new ConfigurationBuilder()
            .AddJsonStream(new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionPoolWarmup:Enabled"] = "false"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(ConnectionPoolWarmupOptions.SectionName)
            .Get<ConnectionPoolWarmupOptions>();

        // Assert - Environment variable should override JSON
        options.Should().NotBeNull();
        options!.Enabled.Should().BeFalse("environment variable should override JSON");
        options.StartupDelayMs.Should().Be(1000, "non-overridden value from JSON");
    }
}
