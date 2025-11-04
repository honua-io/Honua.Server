using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Geoprocessing.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

public class NtsExecutorTests
{
    private readonly NtsExecutor _executor;

    public NtsExecutorTests()
    {
        _executor = new NtsExecutor(NullLogger<NtsExecutor>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_BufferOperation_ShouldReturnBufferedGeometry()
    {
        // Arrange
        var run = CreateProcessRun("buffer", new Dictionary<string, object>
        {
            ["geometry"] = "POINT(0 0)",
            ["distance"] = 10.0,
            ["segments"] = 8
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
        result.DurationMs.Should().BeGreaterThanOrEqualTo(0);
        result.FeaturesProcessed.Should().Be(1);
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
        result.Output["isEmpty"].Should().Be(false);
        result.Output.Should().ContainKey("area");
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
        result.Output.Should().ContainKey("area");
    }

    [Fact]
    public async Task ExecuteAsync_DifferenceOperation_ShouldReturnDifference()
    {
        // Arrange
        var run = CreateProcessRun("difference", new Dictionary<string, object>
        {
            ["geometry1"] = "POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))",
            ["geometry2"] = "POLYGON((5 5, 15 5, 15 15, 5 15, 5 5))"
        });

        var process = CreateProcessDefinition("difference");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().ContainKey("geojson");
        result.Output.Should().ContainKey("area");
    }

    [Fact]
    public async Task ExecuteAsync_ConvexHullOperation_ShouldReturnConvexHull()
    {
        // Arrange
        var run = CreateProcessRun("convex-hull", new Dictionary<string, object>
        {
            ["geometry"] = "MULTIPOINT((0 0), (1 1), (2 0), (1 -1))"
        });

        var process = CreateProcessDefinition("convex-hull");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().ContainKey("geojson");
        result.Output.Should().ContainKey("area");
    }

    [Fact]
    public async Task ExecuteAsync_CentroidOperation_ShouldReturnCentroid()
    {
        // Arrange
        var run = CreateProcessRun("centroid", new Dictionary<string, object>
        {
            ["geometry"] = "POLYGON((0 0, 4 0, 4 4, 0 4, 0 0))"
        });

        var process = CreateProcessDefinition("centroid");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().ContainKey("geojson");
        result.Output.Should().ContainKey("x");
        result.Output.Should().ContainKey("y");
        result.Output["x"].Should().Be(2.0);
        result.Output["y"].Should().Be(2.0);
    }

    [Fact]
    public async Task ExecuteAsync_SimplifyOperation_ShouldReturnSimplifiedGeometry()
    {
        // Arrange
        var run = CreateProcessRun("simplify", new Dictionary<string, object>
        {
            ["geometry"] = "LINESTRING(0 0, 1 1, 2 0, 3 1, 4 0, 5 1, 6 0)",
            ["tolerance"] = 1.0
        });

        var process = CreateProcessDefinition("simplify");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Output.Should().ContainKey("geojson");
        result.Output.Should().ContainKey("originalPoints");
        result.Output.Should().ContainKey("simplifiedPoints");
        result.Output.Should().ContainKey("reduction");

        var originalPoints = Convert.ToInt32(result.Output["originalPoints"]);
        var simplifiedPoints = Convert.ToInt32(result.Output["simplifiedPoints"]);
        simplifiedPoints.Should().BeLessThan(originalPoints);
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
    public async Task ExecuteAsync_InvalidGeometry_ShouldReturnFailure()
    {
        // Arrange
        var run = CreateProcessRun("buffer", new Dictionary<string, object>
        {
            ["geometry"] = "INVALID GEOMETRY",
            ["distance"] = 10.0
        });

        var process = CreateProcessDefinition("buffer");

        // Act
        var result = await _executor.ExecuteAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Status.Should().Be(ProcessRunStatus.Failed);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var run = CreateProcessRun("buffer", new Dictionary<string, object>
        {
            ["geometry"] = "POINT(0 0)",
            ["distance"] = 10.0
        });

        var process = CreateProcessDefinition("buffer");
        var progressReports = new List<ProcessProgress>();
        var progress = new Progress<ProcessProgress>(p => progressReports.Add(p));

        // Act
        var result = await _executor.ExecuteAsync(run, process, progress);

        // Assert
        result.Success.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Percent == 0);
        progressReports.Should().Contain(p => p.Percent == 100);
        progressReports.Should().Contain(p => p.Stage == "NTS Execution");
    }

    [Theory]
    [InlineData("buffer")]
    [InlineData("intersection")]
    [InlineData("union")]
    [InlineData("difference")]
    [InlineData("convex-hull")]
    [InlineData("centroid")]
    [InlineData("simplify")]
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
        var process = CreateProcessDefinition("raster-analysis");
        var request = new ProcessExecutionRequest
        {
            ProcessId = "raster-analysis",
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
