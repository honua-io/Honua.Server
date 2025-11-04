using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Commands;
using Honua.Cli.Services.ControlPlane;
using Honua.Cli.Tests.Support;
using Honua.Server.Core.Import;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class DataIngestionCommandsTests
{
    private static readonly ControlPlaneConnection DefaultConnection = ControlPlaneConnection.Create("http://localhost:5000", null);

    [Fact]
    public async Task IngestionCommand_ShouldRejectOverwriteFlag()
    {
        var console = new TestConsole();
        var client = new StubIngestionApiClient();
        var resolver = new StubConnectionResolver(DefaultConnection);
        var command = new DataIngestionCommand(console, client, resolver, NullLogger<DataIngestionCommand>.Instance);

        using var tempDirectory = new TemporaryDirectory();
        var datasetPath = Path.Combine(tempDirectory.Path, "dataset.geojson");
        await File.WriteAllTextAsync(datasetPath, "{}");

        var settings = new DataIngestionCommand.Settings
        {
            DatasetPath = datasetPath,
            LayerId = "layer",
            ServiceId = "svc",
            Overwrite = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("Overwrite imports are not supported");
    }

    [Fact]
    public async Task JobsCommand_ShouldRenderTable()
    {
        var console = new TestConsole();
        var client = new StubIngestionApiClient
        {
            JobsToReturn = new List<DataIngestionJobSnapshot>
            {
                CreateSnapshot(Guid.NewGuid(), DataIngestionJobStatus.Completed, 1d)
            }
        };
        var resolver = new StubConnectionResolver(DefaultConnection);
        var command = new DataIngestionJobsCommand(console, client, resolver, NullLogger<DataIngestionJobsCommand>.Instance);

        var exitCode = await command.ExecuteAsync(null!, new DataIngestionJobsCommand.Settings());

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Job Id");
    }

    [Fact]
    public async Task StatusCommand_ShouldShowPanel()
    {
        var console = new TestConsole();
        var jobId = Guid.NewGuid();
        var client = new StubIngestionApiClient
        {
            JobToReturn = CreateSnapshot(jobId, DataIngestionJobStatus.Importing, 0.5)
        };
        var resolver = new StubConnectionResolver(DefaultConnection);
        var command = new DataIngestionStatusCommand(console, client, resolver);

        var exitCode = await command.ExecuteAsync(null!, new DataIngestionStatusCommand.Settings { JobId = jobId });

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Status:");
    }

    [Fact]
    public async Task CancelCommand_ShouldWarnWhenNotFound()
    {
        var console = new TestConsole();
        var client = new StubIngestionApiClient();
        var resolver = new StubConnectionResolver(DefaultConnection);
        var command = new DataIngestionCancelCommand(console, client, resolver, NullLogger<DataIngestionCancelCommand>.Instance);

        var exitCode = await command.ExecuteAsync(null!, new DataIngestionCancelCommand.Settings { JobId = Guid.NewGuid() });

        exitCode.Should().Be(1);
        console.Output.Should().Contain("not found");
    }

    [Fact]
    public async Task CancelCommand_ShouldReportCancellation()
    {
        var console = new TestConsole();
        var jobId = Guid.NewGuid();
        var client = new StubIngestionApiClient
        {
            CancelResult = CreateSnapshot(jobId, DataIngestionJobStatus.Cancelled, 0.5)
        };
        var resolver = new StubConnectionResolver(DefaultConnection);
        var command = new DataIngestionCancelCommand(console, client, resolver, NullLogger<DataIngestionCancelCommand>.Instance);

        var exitCode = await command.ExecuteAsync(null!, new DataIngestionCancelCommand.Settings { JobId = jobId });

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Cancellation requested");
    }

    private static DataIngestionJobSnapshot CreateSnapshot(Guid id, DataIngestionJobStatus status, double progress)
    {
        return new DataIngestionJobSnapshot(
            id,
            "svc",
            "layer",
            "dataset",
            status,
            "Stage",
            progress,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class StubIngestionApiClient : IDataIngestionApiClient
    {
        public IReadOnlyList<DataIngestionJobSnapshot> JobsToReturn { get; set; } = Array.Empty<DataIngestionJobSnapshot>();
        public DataIngestionJobSnapshot? JobToReturn { get; set; }
        public DataIngestionJobSnapshot? CancelResult { get; set; }

        public Task<DataIngestionJobSnapshot> CreateJobAsync(ControlPlaneConnection connection, string serviceId, string layerId, string filePath, bool overwrite, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<DataIngestionJobSnapshot?> GetJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
            => Task.FromResult(JobToReturn);

        public Task<DataIngestionJobSnapshot?> CancelJobAsync(ControlPlaneConnection connection, Guid jobId, CancellationToken cancellationToken)
            => Task.FromResult(CancelResult);

        public Task<IReadOnlyList<DataIngestionJobSnapshot>> ListJobsAsync(ControlPlaneConnection connection, CancellationToken cancellationToken)
            => Task.FromResult(JobsToReturn);
    }

    private sealed class StubConnectionResolver : IControlPlaneConnectionResolver
    {
        private readonly ControlPlaneConnection _connection;

        public StubConnectionResolver(ControlPlaneConnection connection)
        {
            _connection = connection;
        }

        public Task<ControlPlaneConnection> ResolveAsync(string? hostOverride, string? tokenOverride, CancellationToken cancellationToken)
            => Task.FromResult(_connection);
    }
}
