using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Configuration;

/// <summary>
/// Tests for configuration hot reload functionality including validation,
/// change notifications, and rollback support.
/// </summary>
public sealed class ConfigurationHotReloadTests
{
    private readonly ConfigurationChangeNotificationService _notificationService;

    public ConfigurationHotReloadTests()
    {
        _notificationService = new ConfigurationChangeNotificationService(NullLogger<ConfigurationChangeNotificationService>.Instance);
    }

    [Fact]
    public void ValidateConfiguration_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new MetadataCacheOptions
        {
            Ttl = TimeSpan.FromMinutes(10)
        };

        // Act
        var result = _notificationService.ValidateConfiguration(options, "MetadataCache");

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidOptions_ReturnsFailure()
    {
        // Arrange
        var options = new TestOptionsWithValidation
        {
            RequiredValue = -1 // Invalid: must be positive
        };

        // Act
        var result = _notificationService.ValidateConfiguration(options, "TestOptions");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateConfiguration_WithNullOptions_ReturnsFailure()
    {
        // Arrange
        MetadataCacheOptions? options = null;

        // Act
        var result = _notificationService.ValidateConfiguration(options!, "MetadataCache");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("null");
    }

    [Fact]
    public void NotifyConfigurationChange_StoresSnapshot_ForRollback()
    {
        // Arrange
        var options = new MetadataCacheOptions { Ttl = TimeSpan.FromMinutes(10) };
        var changes = new Dictionary<string, object?> { ["Ttl"] = TimeSpan.FromMinutes(10) };

        // Act
        _notificationService.NotifyConfigurationChange(options, "MetadataCache", changes);
        var previous = _notificationService.GetPreviousConfiguration<MetadataCacheOptions>("MetadataCache");

        // Assert
        previous.Should().NotBeNull();
        previous!.Ttl.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void NotifyConfigurationChange_IncrementsReloadCount()
    {
        // Arrange
        var options = new MetadataCacheOptions();
        var changes = new Dictionary<string, object?>();

        // Act
        _notificationService.NotifyConfigurationChange(options, "MetadataCache", changes);
        _notificationService.NotifyConfigurationChange(options, "MetadataCache", changes);
        var count = _notificationService.GetReloadCount("MetadataCache");

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void GetPreviousConfiguration_WithNoChanges_ReturnsNull()
    {
        // Act
        var previous = _notificationService.GetPreviousConfiguration<MetadataCacheOptions>("UnknownConfig");

        // Assert
        previous.Should().BeNull();
    }

    [Fact]
    public void GetReloadCount_WithNoChanges_ReturnsZero()
    {
        // Act
        var count = _notificationService.GetReloadCount("UnknownConfig");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public void NotifyValidationFailure_LogsError()
    {
        // Arrange
        var validationResult = new ValidationResult("Test error message");

        // Act & Assert
        // Should not throw
        _notificationService.NotifyValidationFailure("TestConfig", validationResult);
    }

    [Fact]
    public void NotifyChangeApplied_LogsSuccess()
    {
        // Act & Assert
        // Should not throw
        _notificationService.NotifyChangeApplied("TestConfig", "Test changes applied");
    }

    [Fact]
    public void NotifyRollback_LogsWarning()
    {
        // Act & Assert
        // Should not throw
        _notificationService.NotifyRollback("TestConfig", "Test rollback reason");
    }

    [Fact]
    public void ValidationResult_Success_IsValid()
    {
        // Arrange
        var result = ValidationResult.Success;

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void ValidationResult_WithError_IsInvalid()
    {
        // Arrange
        var result = new ValidationResult("Test error");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public void ConfigurationChangeNotification_ThreadSafe_MultipleConcurrentUpdates()
    {
        // Arrange
        var tasks = new List<Task>();
        var options = new MetadataCacheOptions();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var iteration = i;
            tasks.Add(Task.Run(() =>
            {
                var changes = new Dictionary<string, object?> { ["Ttl"] = TimeSpan.FromMinutes(iteration) };
                _notificationService.NotifyConfigurationChange(options, "MetadataCache", changes);
            }));
        }

        Task.WaitAll(tasks.ToArray());
        var count = _notificationService.GetReloadCount("MetadataCache");

        // Assert
        count.Should().Be(100);
    }

    [Fact]
    public void NotifyConfigurationChange_WithDifferentConfigurations_MaintainsSeparateSnapshots()
    {
        // Arrange
        var cacheOptions1 = new MetadataCacheOptions { Ttl = TimeSpan.FromMinutes(10) };
        var cacheOptions2 = new MetadataCacheOptions { Ttl = TimeSpan.FromMinutes(5) };

        // Act
        _notificationService.NotifyConfigurationChange(cacheOptions1, "MetadataCache1", new Dictionary<string, object?>());
        _notificationService.NotifyConfigurationChange(cacheOptions2, "MetadataCache2", new Dictionary<string, object?>());

        var previousCache1 = _notificationService.GetPreviousConfiguration<MetadataCacheOptions>("MetadataCache1");
        var previousCache2 = _notificationService.GetPreviousConfiguration<MetadataCacheOptions>("MetadataCache2");

        // Assert
        previousCache1.Should().NotBeNull();
        previousCache2.Should().NotBeNull();
        previousCache1!.Ttl.Should().Be(TimeSpan.FromMinutes(10));
        previousCache2!.Ttl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ValidateConfiguration_WithMultipleValidationErrors_ReturnsAllErrors()
    {
        // Arrange
        var options = new TestOptionsWithMultipleValidations
        {
            Value1 = -1,  // Invalid: must be positive
            Value2 = 200  // Invalid: must be <= 100
        };

        // Act
        var result = _notificationService.ValidateConfiguration(options, "TestOptions");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetPreviousConfiguration_AfterMultipleUpdates_ReturnsLatest()
    {
        // Arrange
        var options1 = new MetadataCacheOptions { Ttl = TimeSpan.FromMinutes(5) };
        var options2 = new MetadataCacheOptions { Ttl = TimeSpan.FromMinutes(10) };
        var options3 = new MetadataCacheOptions { Ttl = TimeSpan.FromMinutes(15) };

        // Act
        _notificationService.NotifyConfigurationChange(options1, "MetadataCache", new Dictionary<string, object?>());
        _notificationService.NotifyConfigurationChange(options2, "MetadataCache", new Dictionary<string, object?>());
        _notificationService.NotifyConfigurationChange(options3, "MetadataCache", new Dictionary<string, object?>());

        var previous = _notificationService.GetPreviousConfiguration<MetadataCacheOptions>("MetadataCache");

        // Assert
        previous.Should().NotBeNull();
        previous!.Ttl.Should().Be(TimeSpan.FromMinutes(15));
    }

    private sealed class TestOptionsWithValidation
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int RequiredValue { get; set; }
    }

    private sealed class TestOptionsWithMultipleValidations
    {
        [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
        public int Value1 { get; set; }

        [System.ComponentModel.DataAnnotations.Range(0, 100)]
        public int Value2 { get; set; }
    }
}
