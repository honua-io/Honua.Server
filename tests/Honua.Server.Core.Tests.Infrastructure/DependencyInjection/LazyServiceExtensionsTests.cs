// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using FluentAssertions;
using Honua.Server.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.DependencyInjection;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public class LazyServiceExtensionsTests
{
    [Fact]
    public void AddLazySingleton_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLazySingleton<ITestService, TestService>();

        // Act
        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ITestService>();

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<TestService>();
    }

    [Fact]
    public void AddLazySingleton_DefersInstantiation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLazySingleton<ITestService, TestServiceWithInstantiationTracking>();

        TestServiceWithInstantiationTracking.InstanceCount = 0;

        // Act
        var provider = services.BuildServiceProvider();

        // Assert - Service should NOT be instantiated yet
        TestServiceWithInstantiationTracking.InstanceCount.Should().Be(0);
    }

    [Fact]
    public void AddLazySingleton_InstantiatesOnFirstAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLazySingleton<ITestService, TestServiceWithInstantiationTracking>();

        TestServiceWithInstantiationTracking.InstanceCount = 0;
        var provider = services.BuildServiceProvider();

        // Act
        var service = provider.GetRequiredService<ITestService>();

        // Assert
        TestServiceWithInstantiationTracking.InstanceCount.Should().Be(1);
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddLazySingleton_ReturnsSameInstanceOnMultipleAccesses()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLazySingleton<ITestService, TestService>();

        var provider = services.BuildServiceProvider();

        // Act
        var instance1 = provider.GetRequiredService<ITestService>();
        var instance2 = provider.GetRequiredService<ITestService>();

        // Assert
        instance1.Should().BeSameAs(instance2, "should be singleton");
    }

    [Fact]
    public void AddLazySingleton_WithFactory_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        var factoryCalled = false;
        services.AddLazySingleton<ITestService>(sp =>
        {
            factoryCalled = true;
            return new TestService();
        });

        // Act
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ITestService>();

        // Assert
        factoryCalled.Should().BeTrue();
        service.Should().NotBeNull();
    }

    [Fact]
    public void AddLazySingleton_WithFactory_DefersInstantiation()
    {
        // Arrange
        var services = new ServiceCollection();
        var factoryCalled = false;
        services.AddLazySingleton<ITestService>(sp =>
        {
            factoryCalled = true;
            return new TestService();
        });

        // Act
        var provider = services.BuildServiceProvider();

        // Assert - Factory should NOT be called yet
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public void AddLazyWrapper_RegistersLazyService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddLazyWrapper<ITestService>();

        // Act
        var provider = services.BuildServiceProvider();
        var lazy = provider.GetRequiredService<Lazy<ITestService>>();

        // Assert
        lazy.Should().NotBeNull();
        lazy.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public void AddLazyWrapper_LazyValueIsService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddLazyWrapper<ITestService>();

        // Act
        var provider = services.BuildServiceProvider();
        var lazy = provider.GetRequiredService<Lazy<ITestService>>();
        var service = lazy.Value;

        // Assert
        lazy.IsValueCreated.Should().BeTrue();
        service.Should().NotBeNull();
        service.Should().BeOfType<TestService>();
    }

    [Fact]
    public void AddLazyWrapper_MultipleLazyInstancesShareSameService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddLazyWrapper<ITestService>();

        // Act
        var provider = services.BuildServiceProvider();
        var lazy1 = provider.GetRequiredService<Lazy<ITestService>>();
        var lazy2 = provider.GetRequiredService<Lazy<ITestService>>();

        var service1 = lazy1.Value;
        var service2 = lazy2.Value;

        // Assert
        service1.Should().BeSameAs(service2, "lazy instances should resolve to same singleton service");
    }

    [Fact]
    public void LazyService_Constructor_ThrowsWhenServiceProviderIsNull()
    {
        // Act & Assert
        var act = () => new LazyService<ITestService>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LazyService_Value_ResolvesService()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazyService = new LazyService<ITestService>(provider);

        // Act
        var service = lazyService.Value;

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<TestService>();
    }

    [Fact]
    public void LazyService_IsValueCreated_ReturnsFalseBeforeAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazyService = new LazyService<ITestService>(provider);

        // Assert
        lazyService.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public void LazyService_IsValueCreated_ReturnsTrueAfterAccess()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazyService = new LazyService<ITestService>(provider);

        // Act
        _ = lazyService.Value;

        // Assert
        lazyService.IsValueCreated.Should().BeTrue();
    }

    [Fact]
    public void LazyService_Value_ReturnsSameInstanceOnMultipleAccesses()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazyService = new LazyService<ITestService>(provider);

        // Act
        var instance1 = lazyService.Value;
        var instance2 = lazyService.Value;

        // Assert
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void LazyService_WorksWithDependencyInjection()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddSingleton(typeof(LazyService<>), typeof(LazyService<>));

        var provider = services.BuildServiceProvider();

        // Act
        var lazyService = provider.GetRequiredService<LazyService<ITestService>>();
        var service = lazyService.Value;

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<TestService>();
    }

    [Fact]
    public void LazyService_WithTransientDependency_ResolvesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazyService = new LazyService<ITestService>(provider);

        // Act
        var service = lazyService.Value;

        // Assert
        service.Should().NotBeNull();
        service.Should().BeOfType<TestService>();
    }

    [Fact]
    public void AddLazySingleton_CanBeUsedInController()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLazySingleton<ITestService, TestServiceWithInstantiationTracking>();
        services.AddTransient<TestController>();

        TestServiceWithInstantiationTracking.InstanceCount = 0;

        // Act
        var provider = services.BuildServiceProvider();
        var controller = provider.GetRequiredService<TestController>();

        // Assert
        TestServiceWithInstantiationTracking.InstanceCount.Should().Be(0, "service not accessed yet");

        // Access service through controller
        controller.DoWork();
        TestServiceWithInstantiationTracking.InstanceCount.Should().Be(1, "service should be instantiated on first use");
    }

    // Test helper classes
    public interface ITestService
    {
        string GetData();
    }

    public class TestService : ITestService
    {
        public string GetData() => "test data";
    }

    public class TestServiceWithInstantiationTracking : ITestService
    {
        public static int InstanceCount { get; set; }

        public TestServiceWithInstantiationTracking()
        {
            InstanceCount++;
        }

        public string GetData() => "tracked data";
    }

    public class TestController
    {
        private readonly ITestService _service;

        public TestController(ITestService service)
        {
            _service = service;
        }

        public string DoWork()
        {
            return _service.GetData();
        }
    }
}
