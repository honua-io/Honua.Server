// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Configuration;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.EventGrid;

public class EventGridOptionsTests
{
    [Fact]
    public void Validate_WhenDisabled_DoesNotThrow()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = false
        };

        // Act & Assert
        options.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_WithValidTopicEndpointAndManagedIdentity_DoesNotThrow()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true
        };

        // Act & Assert
        options.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_WithValidTopicEndpointAndKey_DoesNotThrow()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            TopicKey = "test-key-123",
            UseManagedIdentity = false
        };

        // Act & Assert
        options.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_WithValidDomainEndpointAndManagedIdentity_DoesNotThrow()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            DomainEndpoint = "https://mydomain.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true
        };

        // Act & Assert
        options.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_WithoutEndpoint_ThrowsException()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            UseManagedIdentity = true
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("neither TopicEndpoint nor DomainEndpoint is configured", exception.Message);
    }

    [Fact]
    public void Validate_WithoutKeyAndWithoutManagedIdentity_ThrowsException()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = false
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("no access key is provided", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidMaxBatchSize_ThrowsException()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true,
            MaxBatchSize = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxBatchSize must be between 1 and 1000", exception.Message);
    }

    [Fact]
    public void Validate_WithMaxBatchSizeExceeding1000_ThrowsException()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true,
            MaxBatchSize = 1001
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxBatchSize must be between 1 and 1000", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidFlushInterval_ThrowsException()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true,
            FlushIntervalSeconds = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("FlushIntervalSeconds must be greater than 0", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidMaxQueueSize_ThrowsException()
    {
        // Arrange
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true,
            MaxQueueSize = 0
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxQueueSize must be greater than 0", exception.Message);
    }

    [Fact]
    public void DefaultValues_AreValid()
    {
        // Arrange & Act
        var options = new EventGridOptions
        {
            Enabled = true,
            TopicEndpoint = "https://mytopic.westus-1.eventgrid.azure.net/api/events",
            UseManagedIdentity = true
        };

        // Assert
        Assert.Equal(100, options.MaxBatchSize);
        Assert.Equal(10, options.FlushIntervalSeconds);
        Assert.Equal(10000, options.MaxQueueSize);
        Assert.Equal(BackpressureMode.Drop, options.BackpressureMode);
        Assert.NotNull(options.Retry);
        Assert.NotNull(options.CircuitBreaker);
        Assert.Empty(options.EventTypeFilter);
        Assert.Empty(options.CollectionFilter);
        Assert.Empty(options.TenantFilter);
    }
}
