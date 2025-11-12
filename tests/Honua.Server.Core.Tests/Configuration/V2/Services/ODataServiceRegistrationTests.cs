// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Services.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Services;

public sealed class ODataServiceRegistrationTests
{
    [Fact]
    public void ValidateConfiguration_ValidSettings_ReturnsSuccess()
    {
        // Arrange
        var registration = new ODataServiceRegistration();
        var serviceConfig = new ServiceBlock
        {
            Id = "odata",
            Type = "odata",
            Enabled = true,
            Settings = new Dictionary<string, object?>
            {
                ["allow_writes"] = true,
                ["max_page_size"] = 1000,
                ["default_page_size"] = 100
            }
        };

        // Act
        var result = registration.ValidateConfiguration(serviceConfig);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateConfiguration_InvalidMaxPageSize_ReturnsError()
    {
        // Arrange
        var registration = new ODataServiceRegistration();
        var serviceConfig = new ServiceBlock
        {
            Id = "odata",
            Type = "odata",
            Enabled = true,
            Settings = new Dictionary<string, object?>
            {
                ["max_page_size"] = 0
            }
        };

        // Act
        var result = registration.ValidateConfiguration(serviceConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("max_page_size"));
    }

    [Fact]
    public void ValidateConfiguration_DefaultPageSizeGreaterThanMax_ReturnsError()
    {
        // Arrange
        var registration = new ODataServiceRegistration();
        var serviceConfig = new ServiceBlock
        {
            Id = "odata",
            Type = "odata",
            Enabled = true,
            Settings = new Dictionary<string, object?>
            {
                ["max_page_size"] = 100,
                ["default_page_size"] = 200
            }
        };

        // Act
        var result = registration.ValidateConfiguration(serviceConfig);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("default_page_size") && e.Contains("max_page_size"));
    }

    [Fact]
    public void ConfigureServices_RegistersConfiguration()
    {
        // Arrange
        var registration = new ODataServiceRegistration();
        var services = new ServiceCollection();
        var serviceConfig = new ServiceBlock
        {
            Id = "odata",
            Type = "odata",
            Enabled = true,
            Settings = new Dictionary<string, object?>
            {
                ["allow_writes"] = true,
                ["max_page_size"] = 500,
                ["default_page_size"] = 50,
                ["emit_wkt_shadow_properties"] = true
            }
        };

        // Act
        registration.ConfigureServices(services, serviceConfig);

        // Assert
        var provider = services.BuildServiceProvider();
        var config = provider.GetService<ODataServiceConfiguration>();
        Assert.NotNull(config);
        Assert.True(config.AllowWrites);
        Assert.Equal(500, config.MaxPageSize);
        Assert.Equal(50, config.DefaultPageSize);
        Assert.True(config.EmitWktShadowProperties);
    }

    [Fact]
    public void ConfigureServices_DefaultValues_UsedWhenNotSpecified()
    {
        // Arrange
        var registration = new ODataServiceRegistration();
        var services = new ServiceCollection();
        var serviceConfig = new ServiceBlock
        {
            Id = "odata",
            Type = "odata",
            Enabled = true,
            Settings = new Dictionary<string, object?>()
        };

        // Act
        registration.ConfigureServices(services, serviceConfig);

        // Assert
        var provider = services.BuildServiceProvider();
        var config = provider.GetService<ODataServiceConfiguration>();
        Assert.NotNull(config);
        Assert.False(config.AllowWrites); // Default
        Assert.Equal(1000, config.MaxPageSize); // Default
        Assert.Equal(100, config.DefaultPageSize); // Default
    }

    [Fact]
    public void ServiceId_ReturnsCorrectValue()
    {
        // Arrange
        var registration = new ODataServiceRegistration();

        // Act & Assert
        Assert.Equal("odata", registration.ServiceId);
    }

    [Fact]
    public void DisplayName_ReturnsCorrectValue()
    {
        // Arrange
        var registration = new ODataServiceRegistration();

        // Act & Assert
        Assert.Equal("OData v4", registration.DisplayName);
    }
}
