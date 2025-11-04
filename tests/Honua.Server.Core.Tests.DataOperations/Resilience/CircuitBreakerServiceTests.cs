using System;
using System.Data.Common;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Resilience;

public class CircuitBreakerServiceTests
{
    private readonly ICircuitBreakerMetrics _metrics;
    private readonly NullLoggerFactory _loggerFactory;

    public CircuitBreakerServiceTests()
    {
        _metrics = new CircuitBreakerMetrics();
        _loggerFactory = NullLoggerFactory.Instance;
    }

    [Fact]
    public void Constructor_ValidOptions_CreatesService()
    {
        // Arrange
        var options = CreateDefaultOptions();

        // Act
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new CircuitBreakerService(null!, _loggerFactory, _metrics));
    }

    [Fact]
    public void GetDatabasePolicy_ReturnsPipeline()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Act
        var pipeline = service.GetDatabasePolicy();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void GetExternalApiPolicy_ReturnsPipeline()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Act
        var pipeline = service.GetExternalApiPolicy();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void GetStoragePolicy_ReturnsPipeline()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Act
        var pipeline = service.GetStoragePolicy();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task DatabasePolicy_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);
        var pipeline = service.GetDatabasePolicy();

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return "success";
        }, CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task DatabasePolicy_TransientException_RetriesAndSucceeds()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);
        var pipeline = service.GetDatabasePolicy();
        var attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            await Task.Delay(10, ct);

            if (attemptCount < 2)
            {
                throw new TimeoutException("Simulated timeout");
            }

            return "success";
        }, CancellationToken.None);

        // Assert
        result.Should().Be("success");
        attemptCount.Should().Be(2); // Initial attempt + 1 retry
    }

    [Fact]
    public async Task StoragePolicy_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);
        var pipeline = service.GetStoragePolicy();

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return "storage-success";
        }, CancellationToken.None);

        // Assert
        result.Should().Be("storage-success");
    }

    [Fact]
    public async Task ExternalApiPolicy_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);
        var pipeline = service.GetExternalApiPolicy();

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return "api-success";
        }, CancellationToken.None);

        // Assert
        result.Should().Be("api-success");
    }

    [Fact]
    public void GetCircuitState_DefaultState_ReturnsClosed()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Act
        var state = service.GetCircuitState("database");

        // Assert
        state.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void GetCircuitState_UnknownService_ReturnsClosed()
    {
        // Arrange
        var options = CreateDefaultOptions();
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Act
        var state = service.GetCircuitState("unknown-service");

        // Assert
        state.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void DatabasePolicy_Disabled_ReturnsPassthroughPipeline()
    {
        // Arrange
        var options = CreateDefaultOptions();
        options.Value.Database.Enabled = false;
        var service = new CircuitBreakerService(options, _loggerFactory, _metrics);

        // Act
        var pipeline = service.GetDatabasePolicy();

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_ValidOptions()
    {
        // Arrange
        var options = new DatabaseCircuitBreakerOptions
        {
            Enabled = true,
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30)
        };

        // Act & Assert
        options.Validate(); // Should not throw
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_InvalidFailureRatio_Throws()
    {
        // Arrange
        var options = new DatabaseCircuitBreakerOptions
        {
            FailureRatio = 1.5 // Invalid - must be 0.0 to 1.0
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_NegativeFailureRatio_Throws()
    {
        // Arrange
        var options = new DatabaseCircuitBreakerOptions
        {
            FailureRatio = -0.1 // Invalid - must be 0.0 to 1.0
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_InvalidMinimumThroughput_Throws()
    {
        // Arrange
        var options = new DatabaseCircuitBreakerOptions
        {
            MinimumThroughput = 0 // Invalid - must be at least 1
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_InvalidSamplingDuration_Throws()
    {
        // Arrange
        var options = new DatabaseCircuitBreakerOptions
        {
            SamplingDuration = TimeSpan.Zero // Invalid - must be greater than zero
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void CircuitBreakerOptions_Validation_InvalidBreakDuration_Throws()
    {
        // Arrange
        var options = new DatabaseCircuitBreakerOptions
        {
            BreakDuration = TimeSpan.FromSeconds(-1) // Invalid - must be greater than zero
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    private IOptions<CircuitBreakerOptions> CreateDefaultOptions()
    {
        var options = new CircuitBreakerOptions
        {
            Database = new DatabaseCircuitBreakerOptions
            {
                Enabled = true,
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30)
            },
            ExternalApi = new ExternalApiCircuitBreakerOptions
            {
                Enabled = true,
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(60)
            },
            Storage = new StorageCircuitBreakerOptions
            {
                Enabled = true,
                FailureRatio = 0.5,
                MinimumThroughput = 10,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30)
            }
        };

        return Options.Create(options);
    }
}
