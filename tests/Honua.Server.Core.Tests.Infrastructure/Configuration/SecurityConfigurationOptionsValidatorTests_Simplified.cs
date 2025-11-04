using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Configuration;

/// <summary>
/// Tests for SecurityConfigurationOptionsValidator - critical security enforcement.
/// These validations MUST pass for the application to start.
/// </summary>
[Collection("UnitTests")]
public sealed class SecurityConfigurationOptionsValidatorTests_Simplified
{
    private readonly SecurityConfigurationOptionsValidator _validator = new();

    #region Valid Configuration Tests

    [Fact]
    public void Validate_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = "/metadata.json"
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region Metadata Security Tests

    [Fact]
    public void Validate_WithNullMetadata_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = null!
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("SECURITY") && f.Contains("Metadata configuration is missing"));
    }

    [Fact]
    public void Validate_WithEmptyMetadataPath_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = ""
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("SECURITY") && f.Contains("Metadata path"));
    }

    [Fact]
    public void Validate_WithEmptyMetadataProvider_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "",
                Path = "/metadata.json"
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("SECURITY") && f.Contains("Metadata provider"));
    }

    #endregion

    #region Geometry Service Security Tests

    [Fact]
    public void Validate_WithGeometryMaxGeometriesExceedingLimit_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = true,
                    MaxGeometries = 15000, // Exceeds limit of 10000
                    MaxCoordinateCount = 100000
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("MaxGeometries") &&
            f.Contains("15000"));
    }

    [Fact]
    public void Validate_WithGeometryMaxCoordinateCountExceedingLimit_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = true,
                    MaxGeometries = 1000,
                    MaxCoordinateCount = 2_000_000 // Exceeds limit of 1000000
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("MaxCoordinateCount") &&
            f.Contains("2000000"));
    }

    [Fact]
    public void Validate_WithGeometryMaxGeometriesZero_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = true,
                    MaxGeometries = 0,
                    MaxCoordinateCount = 100000
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("MaxGeometries must be > 0"));
    }

    [Fact]
    public void Validate_WithGeometryServiceDisabled_ReturnsSuccess()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = false,
                    MaxGeometries = 20000, // Would fail if enabled
                    MaxCoordinateCount = 2_000_000 // Would fail if enabled
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region OData Security Tests

    [Fact]
    public void Validate_WithODataMaxPageSizeExceedingLimit_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = 100,
                    MaxPageSize = 10000 // Exceeds limit of 5000
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("MaxPageSize") &&
            f.Contains("10000"));
    }

    [Fact]
    public void Validate_WithODataDefaultPageSizeExceedingMax_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = 2000,
                    MaxPageSize = 1000 // Default exceeds max
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("DefaultPageSize") &&
            f.Contains("MaxPageSize"));
    }

    [Fact]
    public void Validate_WithODataDefaultPageSizeZero_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = 0,
                    MaxPageSize = 1000
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("DefaultPageSize must be > 0"));
    }

    [Fact]
    public void Validate_WithODataDisabled_ReturnsSuccess()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = false,
                    DefaultPageSize = 100,
                    MaxPageSize = 10000 // Would fail if enabled
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Succeeded);
    }

    #endregion

    #region STAC Provider Validation Tests

    [Fact]
    public void Validate_WithInvalidStacProvider_ReturnsFail()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                Stac = new StacCatalogConfiguration
                {
                    Enabled = true,
                    Provider = "invalid-provider"
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("SECURITY") &&
            f.Contains("STAC provider") &&
            f.Contains("invalid-provider"));
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("postgres")]
    [InlineData("sqlserver")]
    [InlineData("SQLite")]
    [InlineData("POSTGRES")]
    public void Validate_WithValidStacProviders_ReturnsSuccess(string provider)
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration { Provider = "json", Path = "/metadata.json" },
            Services = new ServicesConfiguration
            {
                Stac = new StacCatalogConfiguration
                {
                    Enabled = true,
                    Provider = provider
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Succeeded, $"Provider '{provider}' should be valid");
    }

    #endregion

    #region Multiple Validation Failures Tests

    [Fact]
    public void Validate_WithMultipleSecurityIssues_ReturnsAllFailures()
    {
        // Arrange
        var config = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "",
                Path = ""
            },
            Services = new ServicesConfiguration
            {
                OData = new ODataConfiguration
                {
                    Enabled = true,
                    DefaultPageSize = -1,
                    MaxPageSize = 10000
                },
                Geometry = new GeometryServiceConfiguration
                {
                    Enabled = true,
                    MaxGeometries = 20000,
                    MaxCoordinateCount = 2_000_000
                }
            }
        };

        // Act
        var result = _validator.Validate(null, config);

        // Assert
        Assert.True(result.Failed);
        Assert.True(result.Failures.Count() >= 6, $"Should have at least 6 validation failures, got {result.Failures.Count()}");
    }

    #endregion
}
