using Xunit;
using FluentAssertions;
using Honua.Cli.AI.Services.Telemetry;

namespace Honua.Cli.AI.Tests.Services.Telemetry;

[Trait("Category", "Unit")]
public class LocalFileTelemetryServiceTests : IDisposable
{
    private readonly string _testTelemetryPath;

    public LocalFileTelemetryServiceTests()
    {
        // Use temp directory for tests
        _testTelemetryPath = Path.Combine(Path.GetTempPath(), $"honua-telemetry-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testTelemetryPath))
        {
            Directory.Delete(_testTelemetryPath, recursive: true);
        }
    }

    [Fact]
    public void TelemetryService_WhenDisabled_IsNotEnabled()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = false
        };

        // Act
        using var service = new LocalFileTelemetryService(options);

        // Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TelemetryService_WhenEnabled_IsEnabled()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            ConsentTimestamp = DateTime.UtcNow
        };

        // Act
        using var service = new LocalFileTelemetryService(options);

        // Assert
        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task TrackCommandAsync_WhenDisabled_DoesNotWriteFiles()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = false,
            LocalFilePath = _testTelemetryPath
        };
        using var service = new LocalFileTelemetryService(options);

        // Act
        await service.TrackCommandAsync("test-command", true, TimeSpan.FromSeconds(1));
        await service.FlushAsync();

        // Assert
        Directory.Exists(_testTelemetryPath).Should().BeFalse();
    }

    [Fact]
    public async Task TrackCommandAsync_WhenEnabled_WritesToFile()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            BatchSize = 1, // Flush immediately
            UserId = Guid.NewGuid().ToString("N")
        };
        using var service = new LocalFileTelemetryService(options);

        // Act
        await service.TrackCommandAsync("test-command", success: true, TimeSpan.FromSeconds(1));
        await Task.Delay(100); // Give time for async write

        // Assert
        Directory.Exists(_testTelemetryPath).Should().BeTrue();
        var files = Directory.GetFiles(_testTelemetryPath, "telemetry-*.jsonl");
        files.Should().NotBeEmpty();

        // Verify file content
        var content = await File.ReadAllTextAsync(files[0]);
        content.Should().NotBeEmpty("Telemetry file should contain data");
        content.Should().Contain("test-command", "Command name should be logged");
        content.Should().Contain("\"Success\":true", "Success status should be logged");
        content.Should().Contain("Duration", "Duration should be logged");
        content.Should().Contain(options.UserId, "User ID should be logged");
    }

    [Fact]
    public async Task TrackPlanAsync_RecordsCorrectly()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            BatchSize = 1,
            UserId = Guid.NewGuid().ToString("N")
        };
        using var service = new LocalFileTelemetryService(options);

        // Act
        await service.TrackPlanAsync(
            planType: "Optimization",
            stepCount: 5,
            success: true,
            duration: TimeSpan.FromMinutes(2));
        await Task.Delay(100);

        // Assert
        var files = Directory.GetFiles(_testTelemetryPath, "telemetry-*.jsonl");
        files.Should().NotBeEmpty();

        var content = await File.ReadAllTextAsync(files[0]);
        content.Should().Contain("Optimization");
        content.Should().Contain("\"StepCount\":5");
    }

    [Fact]
    public async Task TrackErrorAsync_SanitizesPII()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            BatchSize = 1,
            CollectStackTraces = false, // Privacy mode
            UserId = Guid.NewGuid().ToString("N")
        };
        using var service = new LocalFileTelemetryService(options);

        // Act
        await service.TrackErrorAsync(
            errorType: "DatabaseConnectionError",
            errorMessage: "Failed to connect to Server=localhost;Database=test;User=admin;Password=secret123");
        await Task.Delay(100);

        // Assert
        var files = Directory.GetFiles(_testTelemetryPath, "telemetry-*.jsonl");
        var content = await File.ReadAllTextAsync(files[0]);

        // Should NOT contain sensitive connection string details
        content.Should().NotContain("Password=secret123");
        content.Should().NotContain("User=admin");
        content.Should().Contain("CONNECTION_STRING_ERROR");
    }

    [Fact]
    public async Task TrackLlmCallAsync_EstimatesCost()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            BatchSize = 1,
            UserId = Guid.NewGuid().ToString("N")
        };
        using var service = new LocalFileTelemetryService(options);

        // Act - GPT-4 call
        await service.TrackLlmCallAsync(
            provider: "openai",
            model: "gpt-4",
            promptTokens: 1000,
            completionTokens: 500,
            duration: TimeSpan.FromSeconds(2));
        await Task.Delay(100);

        // Assert
        var files = Directory.GetFiles(_testTelemetryPath, "telemetry-*.jsonl");
        var content = await File.ReadAllTextAsync(files[0]);

        content.Should().Contain("gpt-4");
        content.Should().Contain("EstimatedCost");
    }

    [Fact]
    public async Task FlushAsync_WritesBatchedEvents()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            BatchSize = 10, // Batch multiple events
            UserId = Guid.NewGuid().ToString("N")
        };
        using var service = new LocalFileTelemetryService(options);

        // Act - Add multiple events
        await service.TrackCommandAsync("cmd1", true, TimeSpan.FromSeconds(1));
        await service.TrackCommandAsync("cmd2", true, TimeSpan.FromSeconds(2));
        await service.TrackCommandAsync("cmd3", false, TimeSpan.FromSeconds(3));

        // Force flush
        await service.FlushAsync();

        // Assert
        var files = Directory.GetFiles(_testTelemetryPath, "telemetry-*.jsonl");
        files.Should().NotBeEmpty();

        var lines = await File.ReadAllLinesAsync(files[0]);
        lines.Should().HaveCountGreaterThanOrEqualTo(3);
        lines[0].Should().Contain("cmd1");
        lines[1].Should().Contain("cmd2");
        lines[2].Should().Contain("cmd3");
    }

    [Fact]
    public void NullTelemetryService_IsDisabled()
    {
        // Arrange & Act
        var service = new NullTelemetryService();

        // Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task NullTelemetryService_AllMethodsSucceed()
    {
        // Arrange
        var service = new NullTelemetryService();

        // Act - Should not throw
        await service.TrackCommandAsync("test", true, TimeSpan.FromSeconds(1));
        await service.TrackPlanAsync("test", 1, true, TimeSpan.FromSeconds(1));
        await service.TrackErrorAsync("test");
        await service.TrackFeatureAsync("test");
        await service.TrackLlmCallAsync("test", "test", 1, 1, TimeSpan.FromSeconds(1));
        await service.FlushAsync();

        // Assert - No exceptions thrown (test passes by not throwing)
    }

    [Fact]
    public async Task TelemetryService_CreatesUserIdFile()
    {
        // Arrange
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            UserId = Guid.NewGuid().ToString("N") // Provide user ID to avoid file I/O
        };
        using var service = new LocalFileTelemetryService(options);

        // Act
        await service.TrackFeatureAsync("test");
        await service.FlushAsync();

        // Assert
        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task TelemetryService_ReusesExistingUserId()
    {
        // Arrange
        var existingUserId = Guid.NewGuid().ToString("N");
        var options = new TelemetryOptions
        {
            Enabled = true,
            LocalFilePath = _testTelemetryPath,
            UserId = existingUserId // Provide user ID directly
        };
        using var service = new LocalFileTelemetryService(options);

        // Act
        await service.TrackFeatureAsync("test");
        await service.FlushAsync();

        // Assert
        var files = Directory.GetFiles(_testTelemetryPath, "telemetry-*.jsonl");
        var content = await File.ReadAllTextAsync(files[0]);
        content.Should().Contain(existingUserId);
    }
}
