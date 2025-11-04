using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Services.Metadata;
using Honua.Cli.Tests.Support;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Cli.Tests.Metadata;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class FileMetadataSnapshotServiceTests
{
    [Fact]
    public async Task CreateSnapshotAsync_ShouldCopyWorkspaceAndWriteManifest()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var workspaceFile = Path.Combine(workspaceDir.Path, "metadata.json");
        await File.WriteAllTextAsync(workspaceFile, "{\"name\":\"test\"}");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var service = new FileMetadataSnapshotService(environment, clock, MetadataSchemaValidator.CreateDefault());

        var result = await service.CreateSnapshotAsync(
            new MetadataSnapshotRequest(workspaceDir.Path, "release:v1", "Initial cut", null),
            CancellationToken.None);

        Directory.Exists(result.SnapshotPath).Should().BeTrue();
        result.Label.IndexOfAny(Path.GetInvalidFileNameChars()).Should().Be(-1);
        result.CreatedAtUtc.Should().Be(clock.UtcNow);
        result.Notes.Should().Be("Initial cut");

        var copiedFile = Path.Combine(result.SnapshotPath, "metadata.json");
        File.Exists(copiedFile).Should().BeTrue();

        var manifestPath = Path.Combine(result.SnapshotPath, "manifest.json");
        File.Exists(manifestPath).Should().BeTrue();
    }

    [Fact]
    public async Task ListSnapshotsAsync_ShouldReturnDescriptorsSortedByCreatedTime()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        await File.WriteAllTextAsync(Path.Combine(workspaceDir.Path, "metadata.json"), "{}");

        var environment = new TestEnvironment(configDir.Path);
        var clock = new TestClock(new DateTimeOffset(2025, 9, 21, 12, 0, 0, TimeSpan.Zero));
        var service = new FileMetadataSnapshotService(environment, clock, MetadataSchemaValidator.CreateDefault());

        await service.CreateSnapshotAsync(new MetadataSnapshotRequest(workspaceDir.Path, "first", null, null), CancellationToken.None);
        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        await service.CreateSnapshotAsync(new MetadataSnapshotRequest(workspaceDir.Path, "second", null, null), CancellationToken.None);

        var descriptors = await service.ListSnapshotsAsync(null, CancellationToken.None);
        descriptors.Should().HaveCount(2);
        descriptors.Should().BeInDescendingOrder(descriptor => descriptor.CreatedAtUtc);
    }

    [Fact]
    public async Task ValidateAsync_ShouldReportSchemaViolations()
    {
        using var workspaceDir = new TemporaryDirectory();
        using var configDir = new TemporaryDirectory();

        var metadataPath = Path.Combine(workspaceDir.Path, "metadata.json");
        await File.WriteAllTextAsync(metadataPath, "{\"invalid\":true}");

        var environment = new TestEnvironment(configDir.Path);
        var service = new FileMetadataSnapshotService(environment, new TestClock(DateTimeOffset.UtcNow), MetadataSchemaValidator.CreateDefault());

        var result = await service.ValidateAsync(new MetadataValidationRequest(workspaceDir.Path), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
