using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Geoprocessing.Executors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

public class CloudBatchExecutorTests
{
    private readonly CloudBatchExecutor _executor;

    public CloudBatchExecutorTests()
    {
        _executor = new CloudBatchExecutor(NullLogger<CloudBatchExecutor>.Instance, "aws");
    }

    [Fact]
    public async Task SubmitAsync_ValidJob_ShouldReturnCloudJobId()
    {
        // Arrange
        var run = CreateProcessRun("complex-analysis", new Dictionary<string, object>
        {
            ["features"] = "large-dataset",
            ["analysisType"] = "density"
        });

        var process = CreateProcessDefinition("complex-analysis");

        // Act
        var result = await _executor.SubmitAsync(run, process);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Status.Should().Be(ProcessRunStatus.Running);
        result.Output.Should().ContainKey("cloudJobId");
        result.Output.Should().ContainKey("cloudProvider");
        result.Output.Should().ContainKey("status");
        result.Output["cloudProvider"].Should().Be("aws");
        result.Output["status"].Should().Be("SUBMITTED");
    }

    [Fact]
    public async Task SubmitAsync_WithProgressReporting_ShouldReportProgress()
    {
        // Arrange
        var run = CreateProcessRun("complex-analysis", new Dictionary<string, object>());
        var process = CreateProcessDefinition("complex-analysis");
        var progressReports = new List<ProcessProgress>();
        var progress = new Progress<ProcessProgress>(p => progressReports.Add(p));

        // Act
        var result = await _executor.SubmitAsync(run, process, progress);

        // Assert
        result.Success.Should().BeTrue();
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Stage == "AWS Batch Submission");
        progressReports.Should().Contain(p => p.Percent == 10);
        progressReports.Should().Contain(p => p.Percent == 100);
    }

    [Fact]
    public async Task GetJobStatusAsync_SubmittedJob_ShouldReturnStatus()
    {
        // Arrange
        var run = CreateProcessRun("test-process", new Dictionary<string, object>());
        var process = CreateProcessDefinition("test-process");
        var submitResult = await _executor.SubmitAsync(run, process);
        var cloudJobId = submitResult.Output["cloudJobId"].ToString()!;

        // Act
        var status = await _executor.GetJobStatusAsync(cloudJobId);

        // Assert
        status.Should().NotBeNull();
        status.CloudJobId.Should().Be(cloudJobId);
        status.Status.Should().Be("SUBMITTED");
        status.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetJobStatusAsync_UnknownJob_ShouldReturnCompletedStatus()
    {
        // Arrange
        var unknownJobId = "unknown-job-id";

        // Act
        var status = await _executor.GetJobStatusAsync(unknownJobId);

        // Assert
        status.Should().NotBeNull();
        status.CloudJobId.Should().Be(unknownJobId);
        status.Status.Should().Be("COMPLETED");
    }

    [Fact]
    public async Task CancelJobAsync_SubmittedJob_ShouldCancelSuccessfully()
    {
        // Arrange
        var run = CreateProcessRun("test-process", new Dictionary<string, object>());
        var process = CreateProcessDefinition("test-process");
        var submitResult = await _executor.SubmitAsync(run, process);
        var cloudJobId = submitResult.Output["cloudJobId"].ToString()!;

        // Act
        var cancelled = await _executor.CancelJobAsync(cloudJobId);

        // Assert
        cancelled.Should().BeTrue();

        var status = await _executor.GetJobStatusAsync(cloudJobId);
        status.Status.Should().Be("CANCELLED");
        status.Message.Should().Contain("cancelled");
    }

    [Fact]
    public async Task CancelJobAsync_UnknownJob_ShouldReturnFalse()
    {
        // Arrange
        var unknownJobId = "unknown-job-id";

        // Act
        var cancelled = await _executor.CancelJobAsync(unknownJobId);

        // Assert
        cancelled.Should().BeFalse();
    }

    [Fact]
    public async Task CanExecuteAsync_AnyOperation_ShouldReturnTrue()
    {
        // Arrange - Cloud batch can handle anything
        var process = CreateProcessDefinition("any-complex-operation");
        var request = new ProcessExecutionRequest
        {
            ProcessId = "any-complex-operation",
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
    public async Task HandleCompletionNotificationAsync_ValidNotification_ShouldUpdateStatus()
    {
        // Arrange
        var run = CreateProcessRun("test-process", new Dictionary<string, object>());
        var process = CreateProcessDefinition("test-process");
        var submitResult = await _executor.SubmitAsync(run, process);
        var cloudJobId = submitResult.Output["cloudJobId"].ToString()!;

        var completionStatus = new CloudBatchJobStatus
        {
            CloudJobId = cloudJobId,
            HonuaJobId = run.JobId,
            Status = "COMPLETED",
            Progress = 100,
            Message = "Job completed successfully",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow,
            ExitCode = 0,
            OutputUrl = "s3://bucket/output.json"
        };

        // Act
        await _executor.HandleCompletionNotificationAsync(cloudJobId, completionStatus);

        // Assert
        var status = await _executor.GetJobStatusAsync(cloudJobId);
        status.Status.Should().Be("COMPLETED");
        status.Progress.Should().Be(100);
        status.ExitCode.Should().Be(0);
        status.OutputUrl.Should().Be("s3://bucket/output.json");
    }

    [Theory]
    [InlineData("aws")]
    [InlineData("azure")]
    [InlineData("gcp")]
    public async Task Constructor_DifferentProviders_ShouldSetProvider(string provider)
    {
        // Arrange
        var executor = new CloudBatchExecutor(NullLogger<CloudBatchExecutor>.Instance, provider);
        var run = CreateProcessRun("test", new Dictionary<string, object>());
        var process = CreateProcessDefinition("test");

        // Act
        var result = await executor.SubmitAsync(run, process);

        // Assert
        result.Output["cloudProvider"].Should().Be(provider);
        result.Output["cloudJobId"].ToString().Should().StartWith($"{provider}-batch-");
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
            Category = "analysis",
            Inputs = new List<ProcessParameter>(),
            OutputFormats = new List<string> { "geojson" },
            ExecutionConfig = new ProcessExecutionConfig(),
            Enabled = true
        };
    }
}
