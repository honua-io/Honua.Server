using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.LocalStack;
using Amazon.Route53;
using Xunit;
using Amazon.S3;
using Amazon.Route53.Model;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Shared fixture that provisions LocalStack for AWS emulator scenarios.
/// Exposes service endpoints and credentials for tests that exercise
/// infrastructure deployment steps without hitting real AWS accounts.
/// </summary>
public sealed class DeploymentEmulatorFixture : IAsyncLifetime
{
    private LocalStackContainer? _localStack;
    private Uri? _edgeEndpoint;

    public bool IsDockerAvailable { get; private set; }

    public bool LocalStackAvailable { get; private set; }

    public LocalStackContainer LocalStack =>
        _localStack ?? throw new InvalidOperationException("LocalStack container has not been initialised.");

    public string AwsRegion { get; } = "us-east-1";
    public string AwsAccessKey { get; } = "test";
    public string AwsSecretKey { get; } = "test";

    public Uri ServiceEndpoint =>
        _edgeEndpoint ?? throw new InvalidOperationException("LocalStack endpoint not initialised.");

    public AmazonS3Client CreateS3Client()
    {
        if (!LocalStackAvailable || _localStack is null || _edgeEndpoint is null)
        {
            throw new InvalidOperationException("LocalStack is not available.");
        }
        var config = new AmazonS3Config
        {
            ServiceURL = _edgeEndpoint.ToString(),
            ForcePathStyle = true,
            AuthenticationRegion = AwsRegion,
            UseHttp = _edgeEndpoint.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        };

        return new AmazonS3Client(
            AwsAccessKey,
            AwsSecretKey,
            config);
    }

    public AmazonRoute53Client CreateRoute53Client()
    {
        if (!LocalStackAvailable || _edgeEndpoint is null)
        {
            throw new InvalidOperationException("LocalStack is not available.");
        }

        var config = new AmazonRoute53Config
        {
            ServiceURL = _edgeEndpoint.ToString(),
            AuthenticationRegion = AwsRegion,
            UseHttp = _edgeEndpoint.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
        };

        return new AmazonRoute53Client(AwsAccessKey, AwsSecretKey, config);
    }

    public async Task InitializeAsync()
    {
        IsDockerAvailable = await IsDockerRunningAsync();
        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is unavailable. Infrastructure emulator tests will be skipped.");
            return;
        }

        try
        {
            _localStack = new LocalStackBuilder()
                .WithEnvironment("SERVICES", "s3,secretsmanager,route53")
                .WithEnvironment("AWS_ACCESS_KEY_ID", AwsAccessKey)
                .WithEnvironment("AWS_SECRET_ACCESS_KEY", AwsSecretKey)
                .WithEnvironment("DEFAULT_REGION", AwsRegion)
                .Build();

            await _localStack.StartAsync();
            var port = _localStack.GetMappedPublicPort(4566);
            var host = _localStack.Hostname;
            _edgeEndpoint = new UriBuilder("http", host, port).Uri;
            LocalStackAvailable = true;

            await EnsureLocalStackReadyAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LocalStack container initialisation failed: {ex.Message}");
            LocalStackAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_localStack is not null)
        {
            await _localStack.DisposeAsync();
        }
    }

    private async Task EnsureLocalStackReadyAsync()
    {
        if (!LocalStackAvailable || _edgeEndpoint is null)
        {
            return;
        }

        using var s3Client = CreateS3Client();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await s3Client.ListBucketsAsync();
                using var route53Client = CreateRoute53Client();
                await route53Client.ListHostedZonesAsync(new ListHostedZonesRequest());
                return;
            }
            catch (Exception ex) when (attempt < 4)
            {
                Console.WriteLine($"LocalStack readiness probe failed (attempt {attempt + 1}): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new InvalidOperationException("LocalStack endpoints did not respond within the readiness window.");
    }

    internal static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var probe = new ContainerBuilder()
                .WithImage("alpine:latest")
                .WithCommand("echo", "docker-ok")
                .WithWaitStrategy(Wait.ForUnixContainer())
                .Build();

            await probe.StartAsync();
            await probe.DisposeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// xUnit collection to share the LocalStack container across tests.
/// </summary>
[CollectionDefinition("DeploymentEmulators")]
public sealed class DeploymentEmulatorCollection : ICollectionFixture<DeploymentEmulatorFixture>
{
}
