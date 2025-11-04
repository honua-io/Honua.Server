using System.Net;
using System.Threading.Tasks;
using Amazon.S3;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

[Collection("DeploymentEmulators")]
public class InfrastructureEmulatorSmokeTests
{
    private readonly DeploymentEmulatorFixture _fixture;
    private readonly ITestOutputHelper _output;

    public InfrastructureEmulatorSmokeTests(DeploymentEmulatorFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task LocalStack_S3_ListBuckets_Succeeds()
    {
        if (!_fixture.IsDockerAvailable)
        {
            _output.WriteLine("Docker is not available. Skipping emulator smoke test.");
            return;
        }

        if (!_fixture.LocalStackAvailable)
        {
            _output.WriteLine("LocalStack failed to start. Skipping emulator smoke test.");
            return;
        }

        var serviceUrl = _fixture.ServiceEndpoint.ToString();
        _output.WriteLine($"LocalStack S3 endpoint: {serviceUrl}");

        var config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = _fixture.AwsRegion,
            UseHttp = serviceUrl.StartsWith("http://")
        };

        using var s3Client = new AmazonS3Client(
            _fixture.AwsAccessKey,
            _fixture.AwsSecretKey,
            config);

        var response = await s3Client.ListBucketsAsync();
        response.HttpStatusCode.Should().Be(HttpStatusCode.OK);
    }
}
