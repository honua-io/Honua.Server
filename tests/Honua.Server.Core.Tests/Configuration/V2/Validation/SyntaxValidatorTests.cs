// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Validation;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Validation;

public sealed class SyntaxValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_NullConfiguration_ReturnsError()
    {
        // Arrange
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("cannot be null", result.Errors[0].Message);
    }

    [Fact]
    public async Task ValidateAsync_MissingVersion_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with { Version = "" };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("version is required"));
    }

    [Fact]
    public async Task ValidateAsync_InvalidLogLevel_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with { LogLevel = "invalid_level" };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Invalid log level"));
    }

    [Fact]
    public async Task ValidateAsync_CorsAllowAnyOriginWithCredentials_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Honua = config.Honua with
        {
            Cors = new CorsSettings
            {
                AllowAnyOrigin = true,
                AllowCredentials = true
            }
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("CORS cannot allow credentials"));
    }

    [Fact]
    public async Task ValidateAsync_DataSourceMissingProvider_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.DataSources["test"] = new DataSourceBlock
        {
            Id = "test",
            Provider = "",
            Connection = "test"
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("provider is required"));
    }

    [Fact]
    public async Task ValidateAsync_DataSourceInvalidProvider_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.DataSources["test"] = new DataSourceBlock
        {
            Id = "test",
            Provider = "unknown_provider",
            Connection = "test"
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Unknown provider"));
    }

    [Fact]
    public async Task ValidateAsync_PoolMinSizeGreaterThanMaxSize_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.DataSources["test"] = new DataSourceBlock
        {
            Id = "test",
            Provider = "postgresql",
            Connection = "test",
            Pool = new PoolSettings
            {
                MinSize = 20,
                MaxSize = 10
            }
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("min_size") && e.Message.Contains("max_size"));
    }

    [Fact]
    public async Task ValidateAsync_LayerMissingTitle_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Layers["test"] = new LayerBlock
        {
            Id = "test",
            Title = "",
            DataSource = "sqlite-test",
            Table = "test_table",
            IdField = "id"
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("title is required"));
    }

    [Fact]
    public async Task ValidateAsync_LayerInvalidGeometryType_ReturnsError()
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
            Geometry = new GeometrySettings
            {
                Column = "geom",
                Type = "InvalidType",
                Srid = 4326
            }
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Invalid geometry type"));
    }

    [Fact]
    public async Task ValidateAsync_LayerNoIntrospectionAndNoFields_ReturnsError()
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
            Fields = null
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("introspect_fields is false") && e.Message.Contains("explicit fields"));
    }

    [Fact]
    public async Task ValidateAsync_LayerUnknownFieldType_ReturnsWarning()
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
            Fields = new Dictionary<string, FieldDefinition>
            {
                ["test_field"] = new FieldDefinition { Type = "unknown_type" }
            }
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Unknown field type"));
    }

    [Fact]
    public async Task ValidateAsync_RateLimitInvalidWindow_ReturnsWarning()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.RateLimit = new RateLimitBlock
        {
            Enabled = true,
            Store = "memory",
            Rules = new Dictionary<string, RateLimitRule>
            {
                ["default"] = new RateLimitRule
                {
                    Requests = 1000,
                    Window = "invalid"
                }
            }
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.True(result.IsValid); // Warnings don't fail validation
        Assert.Contains(result.Warnings, w => w.Message.Contains("Invalid time window format"));
    }

    [Fact]
    public async Task ValidateAsync_RedisCacheWithoutConnection_ReturnsError()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.Caches["redis"] = new CacheBlock
        {
            Id = "redis",
            Type = "redis",
            Connection = null
        };
        var validator = new SyntaxValidator();

        // Act
        var result = await validator.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("Redis cache requires a connection string"));
    }

    private static HonuaConfig CreateValidConfiguration()
    {
        return new HonuaConfig
        {
            Honua = new HonuaGlobalSettings
            {
                Version = "1.0",
                Environment = "test",
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
