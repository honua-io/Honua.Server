// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Reflection;
using Honua.Server.Core.Configuration.V2;
using Honua.Server.Core.Configuration.V2.Services;
using Honua.Server.Core.Configuration.V2.Services.Implementations;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2.Services;

public sealed class ServiceRegistrationDiscoveryTests
{
    [Fact]
    public void DiscoverServices_FindsODataRegistration()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();

        // Act
        discovery.DiscoverServices(Assembly.GetExecutingAssembly(), typeof(ODataServiceRegistration).Assembly);

        // Assert
        Assert.True(discovery.HasService("odata"));
        var service = discovery.GetService("odata");
        Assert.NotNull(service);
        Assert.Equal("odata", service.ServiceId);
        Assert.Equal("OData v4", service.DisplayName);
    }

    [Fact]
    public void DiscoverServices_FindsOgcApiRegistration()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();

        // Act
        discovery.DiscoverServices(typeof(OgcApiServiceRegistration).Assembly);

        // Assert
        Assert.True(discovery.HasService("ogc_api"));
        var service = discovery.GetService("ogc_api");
        Assert.NotNull(service);
        Assert.Equal("ogc_api", service.ServiceId);
        Assert.Equal("OGC API Features", service.DisplayName);
    }

    [Fact]
    public void DiscoverServices_ReturnsAllDiscoveredServices()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();

        // Act
        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);
        var allServices = discovery.GetAllServices();

        // Assert
        Assert.NotEmpty(allServices);
        Assert.Contains("odata", allServices.Keys);
        Assert.Contains("ogc_api", allServices.Keys);
    }

    [Fact]
    public void GetService_NonexistentService_ReturnsNull()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();
        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);

        // Act
        var service = discovery.GetService("nonexistent");

        // Assert
        Assert.Null(service);
    }

    [Fact]
    public void GetService_CaseInsensitive()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();
        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);

        // Act
        var service1 = discovery.GetService("odata");
        var service2 = discovery.GetService("ODATA");
        var service3 = discovery.GetService("OData");

        // Assert
        Assert.NotNull(service1);
        Assert.NotNull(service2);
        Assert.NotNull(service3);
        Assert.Same(service1, service2);
        Assert.Same(service1, service3);
    }

    [Fact]
    public void HasService_ExistingService_ReturnsTrue()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();
        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);

        // Act & Assert
        Assert.True(discovery.HasService("odata"));
        Assert.True(discovery.HasService("ogc_api"));
    }

    [Fact]
    public void HasService_NonexistentService_ReturnsFalse()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();
        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);

        // Act & Assert
        Assert.False(discovery.HasService("nonexistent"));
    }

    [Fact]
    public void DiscoverServices_CalledTwice_DoesNotDuplicateServices()
    {
        // Arrange
        var discovery = new ServiceRegistrationDiscovery();

        // Act
        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);
        var countAfterFirst = discovery.GetAllServices().Count;

        discovery.DiscoverServices(typeof(ODataServiceRegistration).Assembly);
        var countAfterSecond = discovery.GetAllServices().Count;

        // Assert
        Assert.Equal(countAfterFirst, countAfterSecond);
    }
}
