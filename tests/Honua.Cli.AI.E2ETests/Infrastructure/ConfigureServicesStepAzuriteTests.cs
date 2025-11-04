using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

[Collection("AzuriteEmulator")]
public sealed class ConfigureServicesStepAzuriteTests : IAsyncLifetime
{
    private readonly AzuriteEmulatorFixture _fixture;
    private BlobServiceClient? _blobServiceClient;
    private readonly List<BlobCorsRule> _capturedCors = new();
    private bool _versioningEnabled;

    public ConfigureServicesStepAzuriteTests(AzuriteEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfigureServices_Azure_UpdatesStorageProperties()
    {
        var storageAccountName = "honuaazure";

        var blobServiceClient = await EnsureBlobServiceClientAsync();

        await blobServiceClient.GetBlobContainerClient("sample").CreateIfNotExistsAsync();

        using var azureCli = new AzuriteAzureCli(blobServiceClient, TrackVersioning, TrackCorsRules);

        var state = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "Azure",
            DeploymentName = "azurite-e2e",
            Tier = "Development",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = storageAccountName,
                ["database_endpoint"] = string.Empty
            }
        };

        var step = new ConfigureServicesStep(NullLogger<ConfigureServicesStep>.Instance, azureCli: azureCli);
        var stepState = new KernelProcessStepState<DeploymentState>("ConfigureServicesStep", "ConfigureServices", "1.0")
        {
            State = state
        };

        await step.ActivateAsync(stepState);

        var channel = new TestKernelProcessMessageChannel();
        var context = new KernelProcessStepContext(channel);

        await step.ConfigureServicesAsync(context, CancellationToken.None);

        channel.Events.Should().Contain(evt => evt.Id == "ServicesConfigured");

        _versioningEnabled.Should().BeTrue();
        _capturedCors.Should().ContainSingle(rule =>
            rule.AllowedOrigins.Contains("http://localhost:3000") &&
            rule.AllowedMethods.Contains("GET") &&
            rule.MaxAgeInSeconds == 3000);
    }

    Task IAsyncLifetime.InitializeAsync() => EnsureBlobServiceClientAsync();

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    private void TrackVersioning(bool enabled) => _versioningEnabled = enabled;

    private void TrackCorsRules(IEnumerable<BlobCorsRule> rules)
    {
        _capturedCors.Clear();
        _capturedCors.AddRange(rules);
    }

    private async Task<BlobServiceClient> EnsureBlobServiceClientAsync()
    {
        if (_blobServiceClient is not null)
        {
            return _blobServiceClient;
        }

        await _fixture.InitializeAsync();
        _blobServiceClient = _fixture.CreateBlobServiceClient();
        return _blobServiceClient;
    }

}
