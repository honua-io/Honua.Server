using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

[Collection("GcpStorageEmulator")]
public sealed class ConfigureServicesStepGcsTests : IAsyncLifetime
{
    private readonly GcpStorageEmulatorFixture _fixture;
    private FakeGcsApiClient? _gcsApi;
    private string? _projectId;

    public ConfigureServicesStepGcsTests(GcpStorageEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfigureServices_Gcp_UpdatesBucketMetadata()
    {
        var gcsApi = await EnsureGcsClientAsync();

        var bucketName = $"honua-gcs-{Guid.NewGuid():N}";
        await gcsApi.CreateBucketAsync(_projectId!, bucketName, CancellationToken.None);

        var cli = new GcloudCliEmulator(gcsApi);

        var state = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "GCP",
            DeploymentName = "gcs-e2e",
            Tier = "Development",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = bucketName,
                ["database_endpoint"] = string.Empty
            }
        };

        var step = new ConfigureServicesStep(NullLogger<ConfigureServicesStep>.Instance, gcloudCli: cli);
        var stepState = new KernelProcessStepState<DeploymentState>("ConfigureServicesStep", "ConfigureServices", "1.0")
        {
            State = state
        };

        await step.ActivateAsync(stepState);

        var channel = new TestKernelProcessMessageChannel();
        var context = new KernelProcessStepContext(channel);

        await step.ConfigureServicesAsync(context, CancellationToken.None);

        channel.Events.Should().Contain(evt => evt.Id == "ServicesConfigured");

        var bucket = await gcsApi.GetBucketAsync(bucketName, CancellationToken.None);
        bucket.Versioning.Should().NotBeNull();
        bucket.Versioning!.Enabled.Should().BeTrue();

        bucket.Cors.Should().NotBeNull();
        bucket.Cors!.Should().ContainSingle(rule =>
            rule.Origin.Contains("http://localhost:3000") &&
            rule.Method.Contains("GET") &&
            rule.MaxAgeSeconds == 3000);
    }

    Task IAsyncLifetime.InitializeAsync()
    {
        return EnsureGcsClientAsync();
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", null);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
        _gcsApi?.Dispose();
        _gcsApi = null;
        _projectId = null;
        return Task.CompletedTask;
    }

    private async Task<FakeGcsApiClient> EnsureGcsClientAsync()
    {
        if (_gcsApi is not null)
        {
            return _gcsApi;
        }

        await _fixture.InitializeAsync();

        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "test-project");
        _projectId = "test-project";

        _gcsApi = new FakeGcsApiClient(new Uri(_fixture.Endpoint));
        return _gcsApi;
    }
}
