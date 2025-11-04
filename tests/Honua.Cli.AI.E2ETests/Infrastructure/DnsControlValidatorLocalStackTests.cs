using System;
using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
using FluentAssertions;
using Honua.Cli.AI.Services.Certificates;
using Honua.Cli.AI.Services.Certificates.DnsChallenge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

[Collection("DeploymentEmulators")]
public sealed class DnsControlValidatorLocalStackTests : IAsyncLifetime
{
    private readonly DeploymentEmulatorFixture _fixture;
    private string? _hostedZoneId;
    private string? _zoneName;

    public DnsControlValidatorLocalStackTests(DeploymentEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DnsControlValidator_Route53_LocalStack_Succeeds()
    {
        if (!_fixture.IsDockerAvailable || !_fixture.LocalStackAvailable)
        {
            return;
        }

        await EnsureHostedZoneAsync();

        var validator = new DnsControlValidator(
            NullLogger<DnsControlValidator>.Instance,
            new TestHttpClientFactory(),
            route53Options: new Route53DnsOptions
            {
                HostedZoneId = _hostedZoneId!,
                ServiceUrl = _fixture.ServiceEndpoint.ToString(),
                Region = _fixture.AwsRegion,
                AccessKeyId = _fixture.AwsAccessKey,
                SecretAccessKey = _fixture.AwsSecretKey,
                UseHttp = true
            });

        var domain = $"app.{_zoneName}";

        var result = await validator.ValidateDnsControlAsync(domain);

        result.HasControl.Should().BeTrue();
        result.ProviderName.Should().Be("Route53");
        result.ZoneIdentifier.Should().Be(_hostedZoneId);
    }

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (_fixture.IsDockerAvailable && _fixture.LocalStackAvailable)
        {
            await EnsureHostedZoneAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_hostedZoneId is null)
        {
            return;
        }

        try
        {
            using var client = _fixture.CreateRoute53Client();
            await client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest
            {
                Id = _hostedZoneId
            });
        }
        catch
        {
            // Ignore cleanup failures for disposable test data.
        }
    }

    private async Task EnsureHostedZoneAsync()
    {
        if (_hostedZoneId is not null)
        {
            return;
        }

        using var client = _fixture.CreateRoute53Client();
        _zoneName = $"validator-{Guid.NewGuid():N}.local";

        var createResponse = await client.CreateHostedZoneAsync(new CreateHostedZoneRequest
        {
            Name = $"{_zoneName}.",
            CallerReference = Guid.NewGuid().ToString(),
            HostedZoneConfig = new HostedZoneConfig
            {
                Comment = "honua-e2e",
                PrivateZone = false
            }
        });

        var hostedZoneId = createResponse.HostedZone.Id;
        _hostedZoneId = hostedZoneId.StartsWith("/hostedzone/", StringComparison.OrdinalIgnoreCase)
            ? hostedZoneId.Substring("/hostedzone/".Length)
            : hostedZoneId;
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client = new();

        public HttpClient CreateClient(string name) => _client;
    }
}
