using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Geoprocessing.Executors;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

[Collection("SharedPostgres")]
public class PostGisExecutorTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private string _connectionString;
    private PostGisExecutor _executor;

    public PostGisExecutorTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new Xunit.SkipException("PostgreSQL test container is not available");
        }

        _connectionString = _fixture.ConnectionString;
        _executor = new PostGisExecutor(_connectionString, NullLogger<PostGisExecutor>.Instance);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ExecuteAsync_BufferOperation_ShouldReturnBufferedGeometry()
    {
        // Arrange
        var run = CreateProcessRun("buffer", new Dictionary<string, object>
        {
            ["geometry"] = "POINT(0 0)",
            ["distance"] = 0.001 // Small buffer in degrees
        });

        var process = CreateProcessDefinition("buffer");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Status.Should().Be(ProcessRunStatus.Completed);
        result.Output.Should().ContainKey("geojson");
        result.Output.Should().ContainKey("area");
    }

    [Fact]
    public async Task ExecuteAsync_IntersectionOperation_ShouldReturnIntersection()
    {
        // Arrange
        var run = CreateProcessRun("intersection", new Dictionary<string, object>
        {
            ["geometry1"] = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))",
            ["geometry2"] = "POLYGON((5 5, 15 5, 15 15, 5 15, 5 5))"
        });

        var process = CreateProcessDefinition("intersection");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().ContainKey("geojson");
        result.Output.Should().ContainKey("isEmpty");
    }

    [Fact]
    public async Task ExecuteAsync_UnionOperation_ShouldReturnUnion()
    {
        // Arrange
        var run = CreateProcessRun("union", new Dictionary<string, object>
        {
            ["geometry1"] = "POLYGON((0 0, 5 0, 5 5, 0 5, 0 0))",
            ["geometry2"] = "POLYGON((3 3, 8 3, 8 8, 3 8, 3 3))"
        });

        var process = CreateProcessDefinition("union");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().ContainKey("geojson");
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedOperation_ShouldReturnFailure()
    {
        // Arrange
        var run = CreateProcessRun("unsupported-operation", new Dictionary<string, object>
        {
            ["geometry"] = "POINT(0 0)"
        });

        var process = CreateProcessDefinition("unsupported-operation");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Status.Should().Be(ProcessRunStatus.Failed);
        result.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public async Task ExecuteAsync_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var run = CreateProcessRun("spatial-join", new Dictionary<string, object>
        {
            ["collection1"] = "test",
            ["collection2"] = "test"
        });

        var process = CreateProcessDefinition("spatial-join");
        var progressReports = new List<ProcessProgress>();
        var progress = new Progress<ProcessProgress>(p => progressReports.Add(p));

        // Act
        var result = await _executor.ExecuteAsync(run, process, progress);

        // Assert - Even if it fails due to no database, should still report progress
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Stage == "PostGIS Execution");
    }

    [Theory]
    [InlineData("buffer")]
    [InlineData("intersection")]
    [InlineData("union")]
    [InlineData("spatial-join")]
    [InlineData("dissolve")]
    public async Task CanExecuteAsync_SupportedOperations_ShouldReturnTrue(string processId)
    {
        // Arrange
        var process = CreateProcessDefinition(processId);
        var request = new ProcessExecutionRequest
        {
            ProcessId = processId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        // Act
        var canExecute = await _executor.CanExecuteAsync(process, request);

        // Assert
        canExecute.Should().BeTrue();
    }

    [Fact]
    public async Task CanExecuteAsync_UnsupportedOperation_ShouldReturnFalse()
    {
        // Arrange
        var process = CreateProcessDefinition("unsupported-raster-op");
        var request = new ProcessExecutionRequest
        {
            ProcessId = "unsupported-raster-op",
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Inputs = new Dictionary<string, object>()
        };

        // Act
        var canExecute = await _executor.CanExecuteAsync(process, request);

        // Assert
        canExecute.Should().BeFalse();
    }

    // Helper methods

    private ProcessRun CreateProcessRun(string processId, Dictionary<string, object> inputs)
    {
        return new ProcessRun
        {
            JobId = $"job-test-{Guid.NewGuid():N}",
            ProcessId = processId,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ProcessRunStatus.Running,
            Inputs = inputs,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private ProcessDefinition CreateProcessDefinition(string processId)
    {
        return new ProcessDefinition
        {
            Id = processId,
            Title = processId,
            Description = $"Test {processId} operation",
            Version = "1.0.0",
            Category = "vector",
            Inputs = new List<ProcessParameter>(),
            OutputFormats = new List<string> { "geojson" },
            ExecutionConfig = new ProcessExecutionConfig(),
            Enabled = true
        };
    }
}
