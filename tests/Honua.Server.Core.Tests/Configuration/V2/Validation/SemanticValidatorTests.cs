// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Validation;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Validation;

public sealed class SemanticValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_LayerReferencesUndefinedDataSource_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Layers["test"] = new LayerBlock
        {
            Id = "test",
            Title = "Test Layer",
            DataSource = "nonexistent-datasource",
            Table = "test_table",
            IdField = "id",
            Services = new List<string> { "odata" }
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("undefined data source"));
    }

    [Fact]
    public async Task ValidateAsync_LayerReferencesUndefinedService_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Layers["test"] = new LayerBlock
        {
            Id = "test",
            Title = "Test Layer",
            DataSource = "sqlite-test",
            Table = "test_table",
            IdField = "id",
            Services = new List<string> { "nonexistent-service" }
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("undefined service"));
    }

    [Fact]
    public async Task ValidateAsync_EnabledServiceNotUsedByLayers_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Services["unused-service"] = new ServiceBlock
        {
            Id = "unused-service",
            Type = "wfs",
            Enabled = true
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("enabled but not used by any layers"));
    }

    [Fact]
    public async Task ValidateAsync_DuplicateIdAcrossBlocks_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Services["test-id"] = new ServiceBlock
        {
            Id = "test-id",
            Type = "wfs",
            Enabled = true
        };
        config.Layers["test-id"] = new LayerBlock
        {
            Id = "test-id",
            Title = "Duplicate ID",
            DataSource = "sqlite-test",
            Table = "test",
            IdField = "id",
            Services = new List<string> { "odata" }
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate ID"));
    }

    [Fact]
    public async Task ValidateAsync_LayerWithNoEnabledServices_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Services["odata"] = config.Services["odata"] with { Enabled = false };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("no enabled services"));
    }

    [Fact]
    public async Task ValidateAsync_ProductionWithAllowAnyOrigin_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with
        {
            Environment = "production",
            Cors = new CorsSettings { AllowAnyOrigin = true }
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("allow_any_origin is enabled in production"));
    }

    [Fact]
    public async Task ValidateAsync_ProductionWithOnlyMemoryCache_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with { Environment = "production" };
        config.Caches["memory"] = new CacheBlock
        {
            Id = "memory",
            Type = "memory",
            Enabled = true
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Only in-memory caching"));
    }

    [Fact]
    public async Task ValidateAsync_ProductionWithoutRateLimiting_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with { Environment = "production" };
        config.RateLimit = null;
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Rate limiting is not enabled"));
    }

    [Fact]
    public async Task ValidateAsync_UnusedDataSource_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.DataSources["unused-db"] = new DataSourceBlock
        {
            Id = "unused-db",
            Provider = "sqlite",
            Connection = "Data Source=:memory:"
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("not used by any layers"));
    }

    [Fact]
    public async Task ValidateAsync_RateLimitUsesRedisButNoCacheDefined_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.RateLimit = new RateLimitBlock
        {
            Enabled = true,
            Store = "redis",
            Rules = new Dictionary<string, RateLimitRule>()
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Rate limiting uses Redis"));
    }

    [Fact]
    public async Task ValidateAsync_CacheRequiredInEnvironmentButDisabled_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with { Environment = "production" };
        config.Caches["redis"] = new CacheBlock
        {
            Id = "redis",
            Type = "redis",
            Enabled = false,
            Connection = "localhost:6379",
            RequiredIn = new List<string> { "production" }
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("required in") && e.Message.Contains("disabled"));
    }

    [Fact]
    public async Task ValidateAsync_GeometryColumnNotInFields_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Layers["test"] = new LayerBlock
        {
            Id = "test",
            Title = "Test Layer",
            DataSource = "sqlite-test",
            Table = "test_table",
            IdField = "id",
            IntrospectFields = false,
            Geometry = new GeometrySettings
            {
                Column = "geom",
                Type = "Point",
                Srid = 4326
            },
            Fields = new Dictionary<string, FieldDefinition>
            {
                ["id"] = new FieldDefinition { Type = "int", Nullable = false },
                ["name"] = new FieldDefinition { Type = "string", Nullable = true }
                // Missing geom field
            },
            Services = new List<string> { "odata" }
        };
        var validator = new SemanticValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Geometry column") && w.Message.Contains("not defined in fields"));
    }

    private static HonuaConfig CreateValidConfiguration()
    {
        return new HonuaConfig
        {
            Honua = new HonuaGlobalSettings
            {
                Version = "1.0",
                Environment = "development",
                LogLevel = "information"
            },
            DataSources = new Dictionary<string, DataSourceBlock>
            {
                ["sqlite-test"] = new DataSourceBlock
                {
                    Id = "sqlite-test",
                    Provider = "sqlite",
                    Connection = "Data Source=:memory:"
                }
            },
            Services = new Dictionary<string, ServiceBlock>
            {
                ["odata"] = new ServiceBlock
                {
                    Id = "odata",
                    Type = "odata",
                    Enabled = true
                }
            },
            Layers = new Dictionary<string, LayerBlock>
            {
                ["test-layer"] = new LayerBlock
                {
                    Id = "test-layer",
                    Title = "Test Layer",
                    DataSource = "sqlite-test",
                    Table = "test_table",
                    IdField = "id",
                    IntrospectFields = true,
                    Services = new List<string> { "odata" }
                }
            }
        };
    }
}
