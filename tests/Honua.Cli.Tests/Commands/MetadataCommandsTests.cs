using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Commands;
using Honua.Cli.Services;
using Honua.Cli.Services.Metadata;
using Honua.Cli.Tests.Support;
using Honua.Server.Core.Metadata;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class MetadataCommandsTests
{
    [Fact]
    public async Task SnapshotCommand_ShouldCreateSnapshotAndEmitMessage()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var metadataFile = Path.Combine(workspaceDir.Path, "metadata.json");
        await File.WriteAllTextAsync(metadataFile, "{\"id\":1}");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var service = new FileMetadataSnapshotService(environment, clock, MetadataSchemaValidator.CreateDefault());
        var console = new TestConsole();

        var command = new MetadataSnapshotCommand(service, environment, console);
        var settings = new MetadataSnapshotCommand.Settings
        {
            Workspace = workspaceDir.Path,
            Label = "release:v1",
            Notes = "integration"
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(0);
        console.Output.Should().Contain("Snapshot");
        Directory.Exists(environment.SnapshotsRoot).Should().BeTrue();
        Directory.GetDirectories(environment.SnapshotsRoot).Should().NotBeEmpty();
    }

    [Fact]
    public async Task RestoreCommand_ShouldRequireLabel()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var environment = new TestEnvironment(configDir.Path);
        var service = new FileMetadataSnapshotService(environment, new TestClock(DateTimeOffset.UtcNow), MetadataSchemaValidator.CreateDefault());
        var console = new TestConsole();

        var command = new MetadataRestoreCommand(service, environment, console);
        var settings = new MetadataRestoreCommand.Settings
        {
            Workspace = workspaceDir.Path,
            Label = string.Empty
        };

        var exitCode = await command.ExecuteAsync(null!, settings);

        exitCode.Should().Be(1);
        console.Output.Should().Contain("Snapshot label is required");
    }
}
