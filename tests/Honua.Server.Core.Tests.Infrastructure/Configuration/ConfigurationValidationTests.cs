using System;
using System.Collections.Generic;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Configuration;

/// <summary>
/// Comprehensive tests for configuration validation.
/// Tests all validators to ensure invalid configuration is properly detected and rejected.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class ConfigurationValidationTests
{
    #region HonuaConfigurationValidator Tests

    [Fact]
    public void HonuaConfigurationValidator_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = CreateValidHonuaConfiguration();

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void HonuaConfigurationValidator_WithNullMetadata_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = null!
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("Metadata configuration is required"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithInvalidMetadataProvider_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "invalid-provider",
                Path = "/path/to/metadata"
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("invalid"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithEmptyMetadataPath_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = ""
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("path is required"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithODataDefaultPageSizeExceedingMax_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/path" },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = 2000,
                    MaxPageSize = 1000
                }
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("DefaultPageSize") && f.Contains("exceed"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithExcessiveODataMaxPageSize_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/path" },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = 100,
                    MaxPageSize = 10000
                }
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("MaxPageSize") && f.Contains("5000"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithStacEnabledButNoProvider_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/path" },
            Services = new ServicesConfiguration
            {
                Stac = new StacCatalogConfiguration
                {
                    Enabled = true,
                    Provider = ""
                }
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("STAC provider"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithExcessiveGeometryLimits_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/path" },
            Services = new ServicesConfiguration
            {
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = true,
                    MaxGeometries = 20000,
                    MaxCoordinateCount = 2_000_000
                }
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("MaxGeometries") || f.Contains("MaxCoordinateCount"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithRasterTilesS3EnabledButNoBucket_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/path" },
            Services = new ServicesConfiguration
            {
                RasterTiles = new RasterTileCacheConfiguration
                {
                    Enabled = true,
                    Provider = "s3",
                    S3 = new RasterTileS3Configuration
                    {
                        BucketName = null
                    }
                }
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("bucket name"));
    }

    [Fact]
    public void HonuaConfigurationValidator_WithInvalidCogCompression_ReturnsFail()
    {
        // Arrange
        var validator = new HonuaConfigurationValidator();
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/path" },
            RasterCache = new RasterCacheConfiguration
            {
                CogCacheEnabled = true,
                CogCacheProvider = "filesystem",
                CogCompression = "INVALID_CODEC"
            }
        };

        // Act
        var result = validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("compression"));
    }

    #endregion

    #region HonuaAuthenticationOptionsValidator Tests

    [Fact]
    public void AuthenticationValidator_WithOidcModeButNoAuthority_ReturnsFail()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = null,
                Audience = "honua-api"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("Authority"));
    }

    [Fact]
    public void AuthenticationValidator_WithOidcModeButNoAudience_ReturnsFail()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com",
                Audience = null
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("Audience"));
    }

    [Fact]
    public void AuthenticationValidator_WithEnforcedQuickStart_ReturnsFail()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart,
            Enforce = true
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("QuickStart") && f.Contains("enforced"));
    }

    [Fact]
    public void AuthenticationValidator_WithLocalModeAndInvalidSessionLifetime_ReturnsFail()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SessionLifetime = TimeSpan.Zero
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("SessionLifetime"));
    }

    #endregion

    #region ConnectionStringOptionsValidator Tests

    [Fact]
    public void ConnectionStringValidator_WithValidRedisConnectionString_ReturnsSuccess()
    {
        // Arrange
        var validator = new ConnectionStringOptionsValidator();
        var options = new ConnectionStringOptions
        {
            Redis = "localhost:6379,password=secret,ssl=true"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ConnectionStringValidator_WithInvalidRedisConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new ConnectionStringOptionsValidator();
        var options = new ConnectionStringOptions
        {
            Redis = "invalid-no-port"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("Redis") && f.Contains("invalid"));
    }

    [Fact]
    public void ConnectionStringValidator_WithInvalidPostgresConnectionString_ReturnsFail()
    {
        // Arrange
        var validator = new ConnectionStringOptionsValidator();
        var options = new ConnectionStringOptions
        {
            Postgres = "invalid-missing-host-and-database"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("PostgreSQL") && f.Contains("invalid"));
    }

    [Fact]
    public void ConnectionStringValidator_WithValidPostgresConnectionString_ReturnsSuccess()
    {
        // Arrange
        var validator = new ConnectionStringOptionsValidator();
        var options = new ConnectionStringOptions
        {
            Postgres = "Host=localhost;Database=honua;Username=postgres;Password=secret"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region OpenRosaOptionsValidator Tests

    [Fact]
    public void OpenRosaValidator_WithEnabledButNoBaseUrl_ReturnsFail()
    {
        // Arrange
        var validator = new OpenRosaOptionsValidator();
        var options = new OpenRosaOptions
        {
            Enabled = true,
            BaseUrl = null!
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("BaseUrl"));
    }

    [Fact]
    public void OpenRosaValidator_WithInvalidBaseUrl_ReturnsFail()
    {
        // Arrange
        var validator = new OpenRosaOptionsValidator();
        var options = new OpenRosaOptions
        {
            Enabled = true,
            BaseUrl = "not-a-valid-url"
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("BaseUrl") && f.Contains("invalid"));
    }

    [Fact]
    public void OpenRosaValidator_WithExcessiveMaxSubmissionSize_ReturnsFail()
    {
        // Arrange
        var validator = new OpenRosaOptionsValidator();
        var options = new OpenRosaOptions
        {
            Enabled = true,
            BaseUrl = "/openrosa",
            MaxSubmissionSizeMB = 300
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("MaxSubmissionSizeMB"));
    }

    [Fact]
    public void OpenRosaValidator_WithNoAllowedMediaTypes_ReturnsFail()
    {
        // Arrange
        var validator = new OpenRosaOptionsValidator();
        var options = new OpenRosaOptions
        {
            Enabled = true,
            BaseUrl = "/openrosa",
            AllowedMediaTypes = Array.Empty<string>()
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("AllowedMediaTypes"));
    }

    #endregion

    #region Helper Methods

    private static HonuaConfiguration CreateValidHonuaConfiguration()
    {
        return new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = "/path/to/metadata.json"
            },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = 100,
                    MaxPageSize = 1000
                }
            },
            Attachments = AttachmentConfiguration.Default,
            RasterCache = RasterCacheConfiguration.Default
        };
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Honua.Server.Core.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    #endregion
}
