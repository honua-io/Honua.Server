using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

[Collection("DeploymentEmulators")]
public sealed class ConfigureServicesStepLocalStackTests : IAsyncLifetime
{
    private readonly DeploymentEmulatorFixture _fixture;
    private readonly ITestOutputHelper _output;
    private string? _bucketName;

    public ConfigureServicesStepLocalStackTests(DeploymentEmulatorFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task ConfigureServices_AppliesS3VersioningAndCorsPolicies()
    {
        if (!EnsureEmulatorAvailable())
        {
            return;
        }

        using var awsCli = CreateLocalStackAwsCli();
        await ConfigureServicesAndAssertAsync(awsCli);
    }

    [Fact]
    public async Task ConfigureServices_UsesSdkFallbackWhenCliUnavailable()
    {
        if (!EnsureEmulatorAvailable())
        {
            return;
        }

        var originalPath = Environment.GetEnvironmentVariable("PATH");
        var originalHonuaCli = Environment.GetEnvironmentVariable("HONUA_AWS_CLI");
        var originalAwsCliPath = Environment.GetEnvironmentVariable("AWS_CLI_PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", string.Empty);
            Environment.SetEnvironmentVariable("HONUA_AWS_CLI", null);
            Environment.SetEnvironmentVariable("AWS_CLI_PATH", null);

            using var awsCli = CreateLocalStackAwsCli();
            await ConfigureServicesAndAssertAsync(awsCli);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            Environment.SetEnvironmentVariable("HONUA_AWS_CLI", originalHonuaCli);
            Environment.SetEnvironmentVariable("AWS_CLI_PATH", originalAwsCliPath);
        }
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (_fixture.IsDockerAvailable && _fixture.LocalStackAvailable)
        {
            using var s3Client = _fixture.CreateS3Client();
            // Ensure fixture is ready by listing buckets (smoke interaction)
            await s3Client.ListBucketsAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_bucketName is null)
        {
            return;
        }

        try
        {
            using var s3Client = _fixture.CreateS3Client();
            await s3Client.DeleteBucketAsync(_bucketName);
        }
        catch (Exception ex)
        {
            _output.WriteLine("Bucket cleanup failed for {0}: {1}", _bucketName, ex.Message);
        }
    }

    private bool EnsureEmulatorAvailable()
    {
        if (!_fixture.IsDockerAvailable)
        {
            _output.WriteLine("Docker is not available. Skipping emulator test.");
            return false;
        }

        if (!_fixture.LocalStackAvailable)
        {
            _output.WriteLine("LocalStack failed to start. Skipping emulator test.");
            return false;
        }

        return true;
    }

    private LocalStackAwsCli CreateLocalStackAwsCli() =>
        new LocalStackAwsCli(
            _fixture.ServiceEndpoint,
            _fixture.AwsRegion,
            _fixture.AwsAccessKey,
            _fixture.AwsSecretKey);

    private async Task ConfigureServicesAndAssertAsync(LocalStackAwsCli awsCli)
    {
        using var s3Client = _fixture.CreateS3Client();

        _bucketName = $"honua-e2e-{Guid.NewGuid():N}";
        _output.WriteLine("Creating bucket {0}", _bucketName);

        await s3Client.PutBucketAsync(_bucketName);

        var deploymentState = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "AWS",
            DeploymentName = "localstack-e2e",
            Tier = "Development",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = _bucketName!,
                ["database_endpoint"] = string.Empty
            }
        };

        var step = new ConfigureServicesStep(
            NullLogger<ConfigureServicesStep>.Instance,
            awsCli,
            new NoopAzureCli(),
            new NoopGcloudCli());
        var stepState = new KernelProcessStepState<DeploymentState>(
            id: "ConfigureServicesStep",
            name: "ConfigureServices",
            version: "1.0")
        {
            State = deploymentState
        };

        await step.ActivateAsync(stepState);

        var channel = new TestKernelProcessMessageChannel();
        var context = new KernelProcessStepContext(channel);

        await step.ConfigureServicesAsync(context, CancellationToken.None);

        var versioning = await s3Client.GetBucketVersioningAsync(new GetBucketVersioningRequest
        {
            BucketName = _bucketName
        });

        versioning.VersioningConfig.Should().NotBeNull();
        versioning.VersioningConfig.Status.Should().Be(VersionStatus.Enabled);

        var corsResponse = await s3Client.GetCORSConfigurationAsync(new GetCORSConfigurationRequest
        {
            BucketName = _bucketName
        });

        corsResponse.Configuration.Should().NotBeNull();
        corsResponse.Configuration.Rules.Should().ContainSingle(rule =>
            rule.AllowedOrigins.Contains("http://localhost:3000") &&
            rule.AllowedMethods.Contains("GET") &&
            rule.MaxAgeSeconds == 3000);

        channel.Events.Should().Contain(evt => evt.Id == "ServicesConfigured");
    }

}
