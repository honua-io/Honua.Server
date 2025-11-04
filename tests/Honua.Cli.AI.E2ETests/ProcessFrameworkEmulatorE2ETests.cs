using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Amazon.S3;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Honua.Cli.AI.E2ETests.Infrastructure;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Telemetry;
using Amazon.Route53;
using Amazon.Route53.Model;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.TestSupport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Cli.AI.E2ETests;

[Collection("ProcessFramework")]
public sealed class ProcessFrameworkEmulatorE2ETests :
    IClassFixture<E2ETestFixture>,
    IClassFixture<DeploymentEmulatorFixture>,
    IClassFixture<AzuriteEmulatorFixture>,
    IClassFixture<GcpStorageEmulatorFixture>,
    IClassFixture<PostgresEmulatorFixture>
{
    private readonly E2ETestFixture _kernelFixture;
    private readonly DeploymentEmulatorFixture _deploymentFixture;
    private readonly AzuriteEmulatorFixture _azuriteFixture;
    private readonly GcpStorageEmulatorFixture _gcsFixture;
    private readonly PostgresEmulatorFixture _postgresFixture;
    private readonly ITestOutputHelper _output;

    public ProcessFrameworkEmulatorE2ETests(
        E2ETestFixture kernelFixture,
        DeploymentEmulatorFixture deploymentFixture,
        AzuriteEmulatorFixture azuriteFixture,
        GcpStorageEmulatorFixture gcsFixture,
        PostgresEmulatorFixture postgresFixture,
        ITestOutputHelper output)
    {
        _kernelFixture = kernelFixture;
        _deploymentFixture = deploymentFixture;
        _azuriteFixture = azuriteFixture;
        _gcsFixture = gcsFixture;
        _postgresFixture = postgresFixture;
        _output = output;
    }

    [Fact]
    public async Task DeploymentWorkflow_LocalStack_AwsCliFailureEmitsConfigurationFailed()
    {
        RequireLocalStack();

        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "AWS",
  "region": "us-west-2",
  "deploymentName": "aws-failure",
  "tier": "Development",
  "features": ["GeoServer"]
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to AWS with GeoServer");

        using var s3Client = _deploymentFixture.CreateS3Client();
        var bucketName = $"honua-e2e-{Guid.NewGuid():N}";
        await s3Client.PutBucketAsync(bucketName);

        try
        {
            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "AWS",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = "app.localstack.test",
                InfrastructureOutputs =
                {
                    ["storage_bucket"] = bucketName,
                    ["database_endpoint"] = string.Empty,
                    ["load_balancer_endpoint"] = "lb.localstack.test"
                }
            };

            var step = new LocalStackConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                new FailingAwsCli(),
                new NoopAzureCli(),
                new NoopGcloudCli(),
                () => new AmazonRoute53Client(
                    _deploymentFixture.AwsAccessKey,
                    _deploymentFixture.AwsSecretKey,
                    new AmazonRoute53Config
                    {
                        ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                        AuthenticationRegion = _deploymentFixture.AwsRegion,
                        UseHttp = true
                    }),
                hostedZoneId: "unused");

            var stepState = new KernelProcessStepState<DeploymentState>("ConfigureServicesStep", "ConfigureServices", "1.0")
            {
                State = state
            };

            await step.ActivateAsync(stepState);

            var channel = new TestKernelProcessMessageChannel();
            var context = new KernelProcessStepContext(channel);

            await step.ConfigureServicesAsync(context, CancellationToken.None);

            channel.Events.Should().Contain(evt => evt.Id == "ConfigurationFailed");
            channel.Events.Should().NotContain(evt => evt.Id == "ServicesConfigured");
            stepState.State.Status.Should().Be("ConfigurationFailed");

            var versioning = await s3Client.GetBucketVersioningAsync(bucketName);
            versioning.VersioningConfig?.Status.Should().NotBe(VersionStatus.Enabled);
        }
        finally
        {
            await s3Client.DeleteBucketAsync(bucketName);
        }
    }

    [Fact]
    public async Task DeploymentWorkflow_LocalStack_ParameterExtractionAndStorageConfigured()
    {
        RequireLocalStack();

        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "AWS",
  "region": "us-west-2",
  "deploymentName": "aws-emulator",
  "tier": "Development",
  "features": ["GeoServer", "STAC"]
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to AWS us-west-2 with GeoServer and STAC support");

        request.CloudProvider.Should().Be("AWS");
        request.Region.Should().Be("us-west-2");
        request.DeploymentName.Should().Be("aws-emulator");

        using var route53Client = new AmazonRoute53Client(
            _deploymentFixture.AwsAccessKey,
            _deploymentFixture.AwsSecretKey,
            new AmazonRoute53Config
            {
                ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                AuthenticationRegion = _deploymentFixture.AwsRegion,
                UseHttp = true
            });

        var hostedZoneName = "localstack.test";
        var createHostedZone = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
        {
            Name = hostedZoneName + ".",
            CallerReference = Guid.NewGuid().ToString(),
            HostedZoneConfig = new HostedZoneConfig
            {
                Comment = "localstack-test",
                PrivateZone = false
            }
        });

        var hostedZoneId = createHostedZone.HostedZone.Id.Replace("/hostedzone/", string.Empty);

        using var s3Client = _deploymentFixture.CreateS3Client();
        var bucketName = $"honua-e2e-{Guid.NewGuid():N}";
        await s3Client.PutBucketAsync(bucketName);

        var route53Emulator = new DnsEmulator();
        route53Emulator.RegisterZone(hostedZoneName);

        try
        {
            using var awsCli = new LocalStackAwsCli(
                _deploymentFixture.ServiceEndpoint,
                _deploymentFixture.AwsRegion,
                _deploymentFixture.AwsAccessKey,
                _deploymentFixture.AwsSecretKey);

            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "AWS",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = $"app.{hostedZoneName}",
                InfrastructureOutputs =
                {
                    ["storage_bucket"] = bucketName,
                    ["database_endpoint"] = string.Empty,
                    ["load_balancer_endpoint"] = $"lb.{hostedZoneName}",
                    ["application_url"] = "http://localhost"
                }
            };
            state.GuardrailDecision = CreateGuardrailDecision("AWS");

            var terraformArtifacts = await GenerateTerraformArtifactsAsync(state);
            terraformArtifacts.GeneratedSecret.Should().NotBeNullOrEmpty();
            terraformArtifacts.GeneratedSecret!.Length.Should().BeGreaterThanOrEqualTo(16);
            terraformArtifacts.Outputs.Should().ContainKey("db_password");
            terraformArtifacts.Outputs["db_password"].Should().Be(terraformArtifacts.GeneratedSecret);

            ApplyDatabaseOutputsOrSkip(state);
            state.InfrastructureOutputs["db_password"].Should().Be(_postgresFixture.Password);

            using var validation = new ValidationEnvironment(success: true);

            var configureStep = new LocalStackConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                awsCli,
                new NoopAzureCli(),
                new NoopGcloudCli(),
                () => new AmazonRoute53Client(
                    _deploymentFixture.AwsAccessKey,
                    _deploymentFixture.AwsSecretKey,
                    new AmazonRoute53Config
                    {
                        ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                        AuthenticationRegion = _deploymentFixture.AwsRegion,
                        UseHttp = true
                    }),
                hostedZoneId,
                route53Emulator);

            var deployStep = new RecordingDeployStep();

            var pipelineResult = await RunConfigureDeployValidatePipelineAsync(
                configureStep,
                deployStep,
                validation.Step,
                state,
                CancellationToken.None);

            pipelineResult.ConfigureEvents.Should().Contain(evt => evt.Id == "ServicesConfigured");
            _kernelFixture.MockLLM.GetCallCount().Should().BeGreaterThan(0);

            var versioning = await s3Client.GetBucketVersioningAsync(bucketName);
            versioning.VersioningConfig.Should().NotBeNull();
            versioning.VersioningConfig.Status.Should().Be(VersionStatus.Enabled);

            var corsResponse = await s3Client.GetCORSConfigurationAsync(bucketName);
            corsResponse.Configuration.Should().NotBeNull();
            corsResponse.Configuration.Rules.Should().ContainSingle(rule =>
                rule.AllowedOrigins.Contains("http://localhost:3000") &&
                rule.AllowedMethods.Contains("GET") &&
                rule.MaxAgeSeconds == 3000);

            var records = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
            {
                HostedZoneId = hostedZoneId
            });

            records.ResourceRecordSets.Should().Contain(set =>
                set.Type == RRType.CNAME &&
                set.Name.StartsWith(state.CustomDomain!, StringComparison.OrdinalIgnoreCase));

            route53Emulator.TryGetCnameByDnsName(state.CustomDomain!, out var awsCname).Should().BeTrue();
            awsCname.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);

            deployStep.ExecutionCount.Should().Be(1);
            pipelineResult.DeployEvents.Should().Contain(evt => evt.Id == "ApplicationDeployed");
            pipelineResult.ValidateEvents.Should().Contain(evt => evt.Id == "DeploymentValidated");
        }
        finally
        {
            try
            {
                var recordSets = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId
                });

                foreach (var record in recordSets.ResourceRecordSets
                             .Where(r => r.Type != RRType.NS && r.Type != RRType.SOA))
                {
                    var changeResponse = await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                    {
                        HostedZoneId = hostedZoneId,
                        ChangeBatch = new ChangeBatch
                        {
                            Changes = new List<Change>
                            {
                                new Change
                                {
                                    Action = ChangeAction.DELETE,
                                    ResourceRecordSet = record
                                }
                            }
                        }
                    });

                    var changeId = changeResponse.ChangeInfo.Id;
                    for (var attempt = 0; attempt < 10; attempt++)
                    {
                        var changeStatus = await route53Client.GetChangeAsync(new GetChangeRequest(changeId));
                        if (changeStatus.ChangeInfo.Status == ChangeStatus.INSYNC)
                        {
                            break;
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(200));
                    }
                }
            }
            catch
            {
            }

            await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = hostedZoneId });
            await s3Client.DeleteBucketAsync(bucketName);
        }
    }

    [Fact]
    public async Task DeploymentWorkflow_LocalStack_DnsVerificationFailureEmitsConfigurationFailed()
    {
        RequireLocalStack();

        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "AWS",
  "region": "us-west-2",
  "deploymentName": "aws-emulator",
  "tier": "Development",
  "features": ["GeoServer", "STAC"]
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to AWS us-west-2 with GeoServer and STAC support");

        using var route53Client = new AmazonRoute53Client(
            _deploymentFixture.AwsAccessKey,
            _deploymentFixture.AwsSecretKey,
            new AmazonRoute53Config
            {
                ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                AuthenticationRegion = _deploymentFixture.AwsRegion,
                UseHttp = true
            });

        var hostedZoneName = "localstack.test";
        var createHostedZone = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
        {
            Name = hostedZoneName + ".",
            CallerReference = Guid.NewGuid().ToString(),
            HostedZoneConfig = new HostedZoneConfig
            {
                Comment = "localstack-test",
                PrivateZone = false
            }
        });

        var hostedZoneId = createHostedZone.HostedZone.Id.Replace("/hostedzone/", string.Empty);

        using var s3Client = _deploymentFixture.CreateS3Client();
        var bucketName = $"honua-e2e-{Guid.NewGuid():N}";
        await s3Client.PutBucketAsync(bucketName);

        var route53Emulator = new DnsEmulator();
        route53Emulator.RegisterZone(hostedZoneName);

        using var awsCli = new LocalStackAwsCli(
            _deploymentFixture.ServiceEndpoint,
            _deploymentFixture.AwsRegion,
            _deploymentFixture.AwsAccessKey,
            _deploymentFixture.AwsSecretKey);

            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "AWS",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = $"app.{hostedZoneName}",
                InfrastructureOutputs =
            {
                ["storage_bucket"] = bucketName,
                ["database_endpoint"] = string.Empty,
                ["load_balancer_endpoint"] = $"lb.{hostedZoneName}",
                ["application_url"] = "http://localhost"
            }
        };

        var configureStep = new FaultingLocalStackConfigureServicesStep(
            NullLogger<ConfigureServicesStep>.Instance,
            awsCli,
            new NoopAzureCli(),
            new NoopGcloudCli(),
            () => new AmazonRoute53Client(
                _deploymentFixture.AwsAccessKey,
                _deploymentFixture.AwsSecretKey,
                new AmazonRoute53Config
                {
                    ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                    AuthenticationRegion = _deploymentFixture.AwsRegion,
                    UseHttp = true
                }),
            hostedZoneId,
            route53Emulator);

        var deploymentStep = new RecordingDeployStep();
        var validateStep = new HarnessValidateDeploymentStep();

        var pipeline = await RunConfigureDeployValidatePipelineAsync(
            configureStep,
            deploymentStep,
            validateStep,
            state,
            CancellationToken.None);

        pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ConfigurationFailed");
        pipeline.ConfigureEvents.Should().NotContain(evt => evt.Id == "ServicesConfigured");
        deploymentStep.ExecutionCount.Should().Be(0);
        pipeline.DeployEvents.Should().BeEmpty();
        pipeline.ValidateEvents.Should().BeEmpty();

        try
        {
            var recordSets = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
            {
                HostedZoneId = hostedZoneId
            });

            foreach (var record in recordSets.ResourceRecordSets
                         .Where(r => r.Type != RRType.NS && r.Type != RRType.SOA))
            {
                var changeResponse = await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.DELETE,
                                ResourceRecordSet = record
                            }
                        }
                    }
                });

                var changeId = changeResponse.ChangeInfo.Id;
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    var changeStatus = await route53Client.GetChangeAsync(new GetChangeRequest(changeId));
                    if (changeStatus.ChangeInfo.Status == ChangeStatus.INSYNC)
                    {
                        break;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                }
            }
        }
        catch
        {
        }

        await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = hostedZoneId });
        await s3Client.DeleteBucketAsync(bucketName);
    }


    [Fact]
    public async Task DeploymentWorkflow_Azurite_ParameterExtractionAndStorageConfigured()
    {
        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "Azure",
  "region": "eastus",
  "deploymentName": "azure-emulator",
  "tier": "Production"
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Azure East US for production workloads");

        request.CloudProvider.Should().Be("Azure");
        request.DeploymentName.Should().Be("azure-emulator");

        await _azuriteFixture.InitializeAsync();
        RequireAzurite();
        var blobServiceClient = _azuriteFixture.CreateBlobServiceClient();
        await blobServiceClient.GetBlobContainerClient("sample").CreateIfNotExistsAsync();

        var capturedCors = new List<BlobCorsRule>();
        var versioningEnabled = false;
        var azureDnsEmulator = new DnsEmulator();
        azureDnsEmulator.RegisterZone("azure.test");

        using var azureCli = new AzuriteAzureCli(
            blobServiceClient,
            enabled => versioningEnabled = enabled,
            rules =>
            {
                capturedCors.Clear();
                capturedCors.AddRange(rules);
            });

        var state = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "Azure",
            DeploymentName = request.DeploymentName,
            Region = request.Region,
            Tier = request.Tier,
            CustomDomain = "app.azure.test",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = "honuaazure",
                ["database_endpoint"] = string.Empty,
                ["load_balancer_endpoint"] = "lb.azure.test",
                ["application_url"] = "http://localhost"
            }
        };
        state.GuardrailDecision = CreateGuardrailDecision("Azure");
        var terraformArtifacts = await GenerateTerraformArtifactsAsync(state);
        terraformArtifacts.GeneratedSecret.Should().NotBeNullOrEmpty();
        terraformArtifacts.GeneratedSecret!.Length.Should().BeGreaterThanOrEqualTo(16);
        terraformArtifacts.Outputs.Should().ContainKey("db_admin_password");
        terraformArtifacts.Outputs["db_admin_password"].Should().Be(terraformArtifacts.GeneratedSecret);
        ApplyDatabaseOutputsOrSkip(state);
        state.InfrastructureOutputs["db_password"].Should().Be(_postgresFixture.Password);

        var configureStep = new AzureEmulatorConfigureServicesStep(
            NullLogger<ConfigureServicesStep>.Instance,
            azureCli,
            azureDnsEmulator);
        var deployStep = new RecordingDeployStep();
        using var validation = new ValidationEnvironment(success: true);

        var pipeline = await RunConfigureDeployValidatePipelineAsync(
            configureStep,
            deployStep,
            validation.Step,
            state,
            CancellationToken.None);

        pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ServicesConfigured");
        _kernelFixture.MockLLM.GetCallCount().Should().BeGreaterThan(0);

        versioningEnabled.Should().BeTrue();
        var corsRule = capturedCors.Should().ContainSingle().Subject;
        var origins = corsRule.AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        origins.Should().BeEquivalentTo(new[]
        {
            "https://app.honua.io",
            "https://honua.io",
            "https://grafana.honua.io"
        });

        var methods = corsRule.AllowedMethods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        methods.Should().BeEquivalentTo(new[] { "GET", "HEAD" });
        corsRule.MaxAgeInSeconds.Should().Be(3000);

        configureStep.RecordedDnsName.Should().Be(state.CustomDomain);
        configureStep.RecordedEndpoint.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);
        configureStep.VerifiedDnsName.Should().Be(state.CustomDomain);
        configureStep.VerifiedEndpoint.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);
        azureDnsEmulator.TryGetCnameByDnsName(state.CustomDomain!, out var azureCname).Should().BeTrue();
        azureCname.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);

        deployStep.ExecutionCount.Should().Be(1);
        pipeline.DeployEvents.Should().Contain(evt => evt.Id == "ApplicationDeployed");
        pipeline.ValidateEvents.Should().Contain(evt => evt.Id == "DeploymentValidated");
    }

    [Fact]
    public async Task DeploymentWorkflow_Azurite_DnsVerificationFailureEmitsConfigurationFailed()
    {
        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "Azure",
  "region": "eastus",
  "deploymentName": "azure-emulator",
  "tier": "Production"
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Azure East US for production workloads");

        await _azuriteFixture.InitializeAsync();
        RequireAzurite();
        var blobServiceClient = _azuriteFixture.CreateBlobServiceClient();
        await blobServiceClient.GetBlobContainerClient("sample").CreateIfNotExistsAsync();

        var capturedCors = new List<BlobCorsRule>();
        var versioningEnabled = false;
        var azureDnsEmulator = new DnsEmulator();

        using var azureCli = new AzuriteAzureCli(
            blobServiceClient,
            enabled => versioningEnabled = enabled,
            rules =>
            {
                capturedCors.Clear();
                capturedCors.AddRange(rules);
            });

        var state = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "Azure",
            DeploymentName = request.DeploymentName,
            Region = request.Region,
            Tier = request.Tier,
            CustomDomain = "app.azure.test",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = "honuaazure",
                ["database_endpoint"] = string.Empty,
                ["load_balancer_endpoint"] = "lb.azure.test",
                ["application_url"] = "http://localhost"
            }
        };

        var configureStep = new FaultingAzureEmulatorConfigureServicesStep(
            NullLogger<ConfigureServicesStep>.Instance,
            azureCli,
            azureDnsEmulator);
        var deployStep = new RecordingDeployStep();

        var pipeline = await RunConfigureDeployPipelineAsync(
            configureStep,
            deployStep,
            state,
            CancellationToken.None);

        pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ConfigurationFailed");
        pipeline.ConfigureEvents.Should().NotContain(evt => evt.Id == "ServicesConfigured");

        versioningEnabled.Should().BeTrue();
        capturedCors.Should().NotBeEmpty();
        azureDnsEmulator.TryGetCnameByDnsName(state.CustomDomain!, out _).Should().BeTrue();
        deployStep.ExecutionCount.Should().Be(0);
        pipeline.DeployEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DeploymentWorkflow_Azurite_AzureCliFailureEmitsConfigurationFailed()
    {
        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "Azure",
  "region": "eastus",
  "deploymentName": "azure-failure",
  "tier": "Production"
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Azure East US");

        await _azuriteFixture.InitializeAsync();
        RequireAzurite();
        var blobServiceClient = _azuriteFixture.CreateBlobServiceClient();
        await blobServiceClient.GetBlobContainerClient("sample").CreateIfNotExistsAsync();

        var azureDnsEmulator = new DnsEmulator();
        azureDnsEmulator.RegisterZone("azure.test");

        var state = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "Azure",
            DeploymentName = request.DeploymentName,
            Region = request.Region,
            Tier = request.Tier,
            CustomDomain = "app.azure.test",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = "honuaazure",
                ["database_endpoint"] = string.Empty,
                ["load_balancer_endpoint"] = "lb.azure.test"
            }
        };

        var configureStep = new AzureEmulatorConfigureServicesStep(
            NullLogger<ConfigureServicesStep>.Instance,
            new FailingAzureCli(),
            azureDnsEmulator);

        var deployStep = new RecordingDeployStep();

        var pipeline = await RunConfigureDeployPipelineAsync(
            configureStep,
            deployStep,
            state,
            CancellationToken.None);

        pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ConfigurationFailed");
        pipeline.ConfigureEvents.Should().NotContain(evt => evt.Id == "ServicesConfigured");
        azureDnsEmulator.TryGetCnameByDnsName(state.CustomDomain!, out _).Should().BeFalse();
        deployStep.ExecutionCount.Should().Be(0);
        pipeline.DeployEvents.Should().BeEmpty();
    }


    [Fact]
    public async Task DeploymentWorkflow_Gcs_ParameterExtractionAndStorageConfigured()
    {
        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "GCP",
  "region": "us-central1",
  "deploymentName": "gcp-emulator",
  "tier": "Development"
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Google Cloud us-central1 for development");

        request.CloudProvider.Should().Be("GCP");
        request.DeploymentName.Should().Be("gcp-emulator");

        await _gcsFixture.InitializeAsync();
        RequireGcpEmulator();
        Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", _gcsFixture.Endpoint);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "test-project");

        using var gcsApi = new FakeGcsApiClient(new Uri(_gcsFixture.Endpoint));
        var bucketName = $"honua-gcs-{Guid.NewGuid():N}";
        await gcsApi.CreateBucketAsync("test-project", bucketName, CancellationToken.None);
        var gcpDnsEmulator = new DnsEmulator();
        gcpDnsEmulator.RegisterZone("gcp.test");

        try
        {
            var cli = new GcloudCliEmulator(gcsApi);

            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "GCP",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = "app.gcp.test",
                InfrastructureOutputs =
                {
                    ["storage_bucket"] = bucketName,
                    ["database_endpoint"] = string.Empty,
                    ["load_balancer_endpoint"] = "lb.gcp.test",
                    ["application_url"] = "http://localhost"
                }
            };
            state.GuardrailDecision = CreateGuardrailDecision("GCP");

            var terraformArtifacts = await GenerateTerraformArtifactsAsync(state);
            terraformArtifacts.GeneratedSecret.Should().NotBeNullOrEmpty();
            terraformArtifacts.GeneratedSecret!.Length.Should().BeGreaterThanOrEqualTo(16);
            terraformArtifacts.Outputs.Should().ContainKey("db_root_password");
            terraformArtifacts.Outputs["db_root_password"].Should().Be(terraformArtifacts.GeneratedSecret);

            ApplyDatabaseOutputsOrSkip(state);
            state.InfrastructureOutputs["db_password"].Should().Be(_postgresFixture.Password);

            var configureStep = new GcsEmulatorConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                cli,
                gcpDnsEmulator);
            var deployStep = new RecordingDeployStep();
            using var validation = new ValidationEnvironment(success: true);

            var pipeline = await RunConfigureDeployValidatePipelineAsync(
                configureStep,
                deployStep,
                validation.Step,
                state,
                CancellationToken.None);

            pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ServicesConfigured");
            _kernelFixture.MockLLM.GetCallCount().Should().BeGreaterThan(0);

            var bucket = await gcsApi.GetBucketAsync(bucketName, CancellationToken.None);
            bucket.Versioning.Should().NotBeNull();
            bucket.Versioning!.Enabled.Should().BeTrue();
            bucket.Cors.Should().ContainSingle(rule =>
                rule.Origin.Contains("http://localhost:3000") &&
                rule.Method.Contains("GET") &&
                rule.Method.Contains("HEAD") &&
                rule.MaxAgeSeconds == 3000);

            configureStep.RecordedDnsName.Should().Be(state.CustomDomain);
            configureStep.RecordedEndpoint.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);
            configureStep.VerifiedDnsName.Should().Be(state.CustomDomain);
            configureStep.VerifiedEndpoint.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);
            gcpDnsEmulator.TryGetCnameByDnsName(state.CustomDomain!, out var gcpCname).Should().BeTrue();
            gcpCname.Should().Be(state.InfrastructureOutputs["load_balancer_endpoint"]);
            deployStep.ExecutionCount.Should().Be(1);
            pipeline.DeployEvents.Should().Contain(evt => evt.Id == "ApplicationDeployed");
            pipeline.ValidateEvents.Should().Contain(evt => evt.Id == "DeploymentValidated");
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", null);
            Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
        }
    }

    [Fact]
    public async Task DeploymentWorkflow_LocalStack_PostgresRequiresSslFailsConfiguration()
    {
        RequireLocalStack();

        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "AWS",
  "region": "us-west-2",
  "deploymentName": "aws-emulator",
  "tier": "Development",
  "features": ["GeoServer", "STAC"]
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to AWS us-west-2 with GeoServer and STAC support");

        using var route53Client = new AmazonRoute53Client(
            _deploymentFixture.AwsAccessKey,
            _deploymentFixture.AwsSecretKey,
            new AmazonRoute53Config
            {
                ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                AuthenticationRegion = _deploymentFixture.AwsRegion,
                UseHttp = true
            });

        var hostedZoneName = "localstack-ssl.test";
        var createHostedZone = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
        {
            Name = hostedZoneName + ".",
            CallerReference = Guid.NewGuid().ToString(),
            HostedZoneConfig = new HostedZoneConfig
            {
                Comment = "localstack-ssl-test",
                PrivateZone = false
            }
        });

        var hostedZoneId = createHostedZone.HostedZone.Id.Replace("/hostedzone/", string.Empty);

        using var s3Client = _deploymentFixture.CreateS3Client();
        var bucketName = $"honua-ssl-{Guid.NewGuid():N}";
        await s3Client.PutBucketAsync(bucketName);

        var route53Emulator = new DnsEmulator();
        route53Emulator.RegisterZone(hostedZoneName);

        try
        {
            using var awsCli = new LocalStackAwsCli(
                _deploymentFixture.ServiceEndpoint,
                _deploymentFixture.AwsRegion,
                _deploymentFixture.AwsAccessKey,
                _deploymentFixture.AwsSecretKey);

            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "AWS",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = $"app.{hostedZoneName}",
                InfrastructureOutputs =
                {
                    ["storage_bucket"] = bucketName,
                    ["database_endpoint"] = string.Empty,
                    ["load_balancer_endpoint"] = $"lb.{hostedZoneName}",
                    ["application_url"] = "http://localhost"
                }
            };
            state.GuardrailDecision = CreateGuardrailDecision("AWS");

            var terraformArtifacts = await GenerateTerraformArtifactsAsync(state);
            terraformArtifacts.GeneratedSecret.Should().NotBeNullOrEmpty();
            terraformArtifacts.GeneratedSecret!.Length.Should().BeGreaterThanOrEqualTo(16);

            ApplyDatabaseOutputsOrSkip(state);
            state.InfrastructureOutputs["database_ssl_mode"] = "Require";
            state.InfrastructureOutputs["database_trust_server_certificate"] = "false";

            var configureStep = new LocalStackConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                awsCli,
                new NoopAzureCli(),
                new NoopGcloudCli(),
                () => new AmazonRoute53Client(
                    _deploymentFixture.AwsAccessKey,
                    _deploymentFixture.AwsSecretKey,
                    new AmazonRoute53Config
                    {
                        ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                        AuthenticationRegion = _deploymentFixture.AwsRegion,
                        UseHttp = true
                    }),
                hostedZoneId,
                route53Emulator);

            var deployStep = new RecordingDeployStep();

            var pipeline = await RunConfigureDeployPipelineAsync(
                configureStep,
                deployStep,
                state,
                CancellationToken.None);

            pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ConfigurationFailed");
            pipeline.ConfigureEvents.Should().NotContain(evt => evt.Id == "ServicesConfigured");
            deployStep.ExecutionCount.Should().Be(0);
            pipeline.DeployEvents.Should().BeEmpty();
            state.Status.Should().Be("ConfigurationFailed");
        }
        finally
        {
            try
            {
                var records = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId
                });

                foreach (var record in records.ResourceRecordSets.Where(r => r.Type != RRType.NS && r.Type != RRType.SOA))
                {
                    await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                    {
                        HostedZoneId = hostedZoneId,
                        ChangeBatch = new ChangeBatch
                        {
                            Changes = new List<Change>
                            {
                                new Change
                                {
                                    Action = ChangeAction.DELETE,
                                    ResourceRecordSet = record
                                }
                            }
                        }
                    });
                }
            }
            catch
            {
            }

            await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = hostedZoneId });
            await s3Client.DeleteBucketAsync(bucketName);
        }
    }

    [Fact]
    public async Task DeploymentWorkflow_Gcs_DnsVerificationFailureEmitsConfigurationFailed()
    {
        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "GCP",
  "region": "us-central1",
  "deploymentName": "gcp-emulator",
  "tier": "Development"
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Google Cloud us-central1 for development");

        await _gcsFixture.InitializeAsync();
        RequireGcpEmulator();
        Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", _gcsFixture.Endpoint);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "test-project");

        using var gcsApi = new FakeGcsApiClient(new Uri(_gcsFixture.Endpoint));
        var bucketName = $"honua-gcs-{Guid.NewGuid():N}";
        await gcsApi.CreateBucketAsync("test-project", bucketName, CancellationToken.None);
        var gcpDnsEmulator = new DnsEmulator();

        try
        {
            var cli = new GcloudCliEmulator(gcsApi);

            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "GCP",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = "app.gcp.test",
                InfrastructureOutputs =
                {
                    ["storage_bucket"] = bucketName,
                    ["database_endpoint"] = string.Empty,
                    ["load_balancer_endpoint"] = "lb.gcp.test",
                    ["application_url"] = "http://localhost"
                }
            };

            var configureStep = new FaultingGcsEmulatorConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                cli,
                gcpDnsEmulator);
            var deployStep = new RecordingDeployStep();

            var pipeline = await RunConfigureDeployPipelineAsync(
                configureStep,
                deployStep,
                state,
                CancellationToken.None);

            pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ConfigurationFailed");
            pipeline.ConfigureEvents.Should().NotContain(evt => evt.Id == "ServicesConfigured");

            var bucket = await gcsApi.GetBucketAsync(bucketName, CancellationToken.None);
            bucket.Versioning.Should().NotBeNull();
            bucket.Cors.Should().NotBeEmpty();
            gcpDnsEmulator.TryGetCnameByDnsName(state.CustomDomain!, out _).Should().BeTrue();
            deployStep.ExecutionCount.Should().Be(0);
            pipeline.DeployEvents.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", null);
            Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
        }
    }

    [Fact]
    public async Task DeploymentWorkflow_Gcs_GcloudCliFailureEmitsConfigurationFailed()
    {
        _kernelFixture.ResetTelemetry();

        var parameterService = CreateParameterExtractionService();
        _kernelFixture.MockLLM.QueueResponse("""
{
  "cloudProvider": "GCP",
  "region": "us-central1",
  "deploymentName": "gcp-cli-failure",
  "tier": "Development"
}
""");

        var request = await parameterService.ExtractDeploymentParametersAsync(
            "Deploy Honua to Google Cloud us-central1 for development");

        await _gcsFixture.InitializeAsync();
        RequireGcpEmulator();
        Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", _gcsFixture.Endpoint);
        Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", "test-project");

        using var gcsApi = new FakeGcsApiClient(new Uri(_gcsFixture.Endpoint));
        var bucketName = $"honua-gcs-{Guid.NewGuid():N}";
        await gcsApi.CreateBucketAsync("test-project", bucketName, CancellationToken.None);
        var gcpDnsEmulator = new DnsEmulator();
        gcpDnsEmulator.RegisterZone("gcp.test");

        try
        {
            var state = new DeploymentState
            {
                DeploymentId = Guid.NewGuid().ToString(),
                CloudProvider = "GCP",
                DeploymentName = request.DeploymentName,
                Region = request.Region,
                Tier = request.Tier,
                CustomDomain = "app.gcp.test",
                InfrastructureOutputs =
                {
                    ["storage_bucket"] = bucketName,
                    ["database_endpoint"] = string.Empty,
                    ["load_balancer_endpoint"] = "lb.gcp.test"
                }
            };

            var configureStep = new GcsEmulatorConfigureServicesStep(
                NullLogger<ConfigureServicesStep>.Instance,
                new FailingGcloudCli(),
                gcpDnsEmulator);
            var deployStep = new RecordingDeployStep();

            var pipeline = await RunConfigureDeployPipelineAsync(
                configureStep,
                deployStep,
                state,
                CancellationToken.None);

            pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ConfigurationFailed");
            pipeline.ConfigureEvents.Should().NotContain(evt => evt.Id == "ServicesConfigured");
            gcpDnsEmulator.TryGetCnameByDnsName(state.CustomDomain!, out _).Should().BeFalse();
            deployStep.ExecutionCount.Should().Be(0);
            pipeline.DeployEvents.Should().BeEmpty();
        }
        finally
        {
            Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", null);
            Environment.SetEnvironmentVariable("GOOGLE_CLOUD_PROJECT", null);
        }
    }


    [Fact]
    public async Task ProcessPipeline_LocalStack_ValidationFailureStopsAfterDeployment()
    {
        RequireLocalStack();

        _kernelFixture.ResetTelemetry();

        using var route53Client = new AmazonRoute53Client(
            _deploymentFixture.AwsAccessKey,
            _deploymentFixture.AwsSecretKey,
            new AmazonRoute53Config
            {
                ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                AuthenticationRegion = _deploymentFixture.AwsRegion,
                UseHttp = true
            });

        var hostedZoneName = $"validation-failure.{Guid.NewGuid():N}.test";
        var createHostedZone = await route53Client.CreateHostedZoneAsync(new CreateHostedZoneRequest
        {
            Name = hostedZoneName + ".",
            CallerReference = Guid.NewGuid().ToString(),
            HostedZoneConfig = new HostedZoneConfig
            {
                Comment = "pipeline-validation-failure",
                PrivateZone = false
            }
        });

        var hostedZoneId = createHostedZone.HostedZone.Id.Replace("/hostedzone/", string.Empty);

        using var s3Client = _deploymentFixture.CreateS3Client();
        var bucketName = $"honua-validation-fail-{Guid.NewGuid():N}";
        await s3Client.PutBucketAsync(bucketName);

        var route53Emulator = new DnsEmulator();
        route53Emulator.RegisterZone(hostedZoneName);

        using var awsCli = new LocalStackAwsCli(
            _deploymentFixture.ServiceEndpoint,
            _deploymentFixture.AwsRegion,
            _deploymentFixture.AwsAccessKey,
            _deploymentFixture.AwsSecretKey);

        var state = new DeploymentState
        {
            DeploymentId = Guid.NewGuid().ToString(),
            CloudProvider = "AWS",
            DeploymentName = "validation-failure",
            Region = _deploymentFixture.AwsRegion,
            Tier = "Development",
            CustomDomain = $"app.{hostedZoneName}",
            InfrastructureOutputs =
            {
                ["storage_bucket"] = bucketName,
                ["database_endpoint"] = string.Empty,
                ["load_balancer_endpoint"] = $"lb.{hostedZoneName}",
                ["application_url"] = "http://localhost"
            }
        };
        state.GuardrailDecision = CreateGuardrailDecision("AWS");

        var terraformArtifacts = await GenerateTerraformArtifactsAsync(state);
        terraformArtifacts.GeneratedSecret.Should().NotBeNullOrEmpty();
        terraformArtifacts.GeneratedSecret!.Length.Should().BeGreaterThanOrEqualTo(16);

        ApplyDatabaseOutputsOrSkip(state);
        state.InfrastructureOutputs["db_password"].Should().Be(_postgresFixture.Password);

        var configureStep = new LocalStackConfigureServicesStepNoVerification(
            NullLogger<ConfigureServicesStep>.Instance,
            awsCli,
            new NoopAzureCli(),
            new NoopGcloudCli(),
            () => new AmazonRoute53Client(
                _deploymentFixture.AwsAccessKey,
                _deploymentFixture.AwsSecretKey,
                new AmazonRoute53Config
                {
                    ServiceURL = _deploymentFixture.ServiceEndpoint.ToString(),
                    AuthenticationRegion = _deploymentFixture.AwsRegion,
                    UseHttp = true
                }),
            hostedZoneId,
            route53Emulator);

        var deployStep = new RecordingDeployStep();
        using var validation = new ValidationEnvironment(success: false);

        var pipeline = await RunConfigureDeployValidatePipelineAsync(
            configureStep,
            deployStep,
            validation.Step,
            state,
            CancellationToken.None);

        pipeline.ConfigureEvents.Should().Contain(evt => evt.Id == "ServicesConfigured");
        pipeline.DeployEvents.Should().Contain(evt => evt.Id == "ApplicationDeployed");
        pipeline.ValidateEvents.Should().Contain(evt => evt.Id == "ValidationFailed");
        deployStep.ExecutionCount.Should().Be(1);

        var versioning = await s3Client.GetBucketVersioningAsync(bucketName);
        versioning.VersioningConfig.Should().NotBeNull();
        var records = await route53Client.ListResourceRecordSetsAsync(new ListResourceRecordSetsRequest
        {
            HostedZoneId = hostedZoneId
        });
        records.ResourceRecordSets.Should().Contain(set => set.Type == RRType.CNAME);

        try
        {
            foreach (var record in records.ResourceRecordSets
                         .Where(r => r.Type != RRType.NS && r.Type != RRType.SOA))
            {
                await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest
                {
                    HostedZoneId = hostedZoneId,
                    ChangeBatch = new ChangeBatch
                    {
                        Changes = new List<Change>
                        {
                            new Change
                            {
                                Action = ChangeAction.DELETE,
                                ResourceRecordSet = record
                            }
                        }
                    }
                });
            }
        }
        catch
        {
        }

        await route53Client.DeleteHostedZoneAsync(new DeleteHostedZoneRequest { Id = hostedZoneId });
        await s3Client.DeleteBucketAsync(bucketName);
    }


    private class LocalStackConfigureServicesStep : ConfigureServicesStep
    {
        private readonly Func<IAmazonRoute53> _clientFactory;
        private readonly string _hostedZoneId;
        private readonly DnsEmulator? _dns;

        public LocalStackConfigureServicesStep(
            ILogger<ConfigureServicesStep> logger,
            IAwsCli awsCli,
            IAzureCli azureCli,
            IGcloudCli gcloudCli,
            Func<IAmazonRoute53> clientFactory,
            string hostedZoneId,
            DnsEmulator? dns = null)
            : base(logger, awsCli, azureCli, gcloudCli)
        {
            _clientFactory = clientFactory;
            _hostedZoneId = hostedZoneId;
            _dns = dns;
        }

        protected override async Task ConfigureRoute53(string dnsName, string endpoint, CancellationToken cancellationToken)
        {
            using var client = _clientFactory();

            var changeRequest = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = _hostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.UPSERT,
                            ResourceRecordSet = new ResourceRecordSet
                            {
                                Name = dnsName.EndsWith('.') ? dnsName : dnsName + '.',
                                Type = RRType.CNAME,
                                TTL = 60,
                                ResourceRecords = new List<ResourceRecord>
                                {
                                    new ResourceRecord(endpoint.EndsWith('.') ? endpoint : endpoint + '.')
                                }
                            }
                        }
                    }
                }
            };

            await client.ChangeResourceRecordSetsAsync(changeRequest, cancellationToken);

            if (_dns is not null)
            {
                var zoneName = ExtractZoneName(dnsName);
                _dns.RegisterZone(zoneName);
                var recordName = DnsEmulator.GetRecordSegment(dnsName, zoneName);
                _dns.DeleteCname(zoneName, recordName);
                _dns.UpsertCname(zoneName, recordName, endpoint.TrimEnd('.'));
            }

            await VerifyDnsRecordAsync(dnsName, endpoint, cancellationToken);
        }

        protected override Task WaitForRoute53PropagationAsync(IAmazonRoute53 route53Client, string changeId, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
        {
            if (_dns is null)
            {
                return Task.CompletedTask;
            }

            if (!_dns.TryGetCnameByDnsName(dnsName, out var actual))
            {
                throw new InvalidOperationException($"DNS record {dnsName} does not exist in Route53 emulator.");
            }

            if (!string.Equals(actual, expectedValue.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected Route53 record {dnsName} to be {expectedValue}, but found {actual}.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class LocalStackConfigureServicesStepNoVerification : LocalStackConfigureServicesStep
    {
        public LocalStackConfigureServicesStepNoVerification(
            ILogger<ConfigureServicesStep> logger,
            IAwsCli awsCli,
            IAzureCli azureCli,
            IGcloudCli gcloudCli,
            Func<IAmazonRoute53> clientFactory,
            string hostedZoneId,
            DnsEmulator? dns = null)
            : base(logger, awsCli, azureCli, gcloudCli, clientFactory, hostedZoneId, dns)
        {
        }

        protected override Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class FaultingLocalStackConfigureServicesStep : LocalStackConfigureServicesStep
    {
        public FaultingLocalStackConfigureServicesStep(
            ILogger<ConfigureServicesStep> logger,
            IAwsCli awsCli,
            IAzureCli azureCli,
            IGcloudCli gcloudCli,
            Func<IAmazonRoute53> clientFactory,
            string hostedZoneId,
            DnsEmulator? dns = null)
            : base(logger, awsCli, azureCli, gcloudCli, clientFactory, hostedZoneId, dns)
        {
        }

        protected override async Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
        {
            await base.VerifyDnsRecordAsync(dnsName, expectedValue, cancellationToken);
            throw new InvalidOperationException("Simulated Route53 verification failure");
        }
    }

    private class AzureEmulatorConfigureServicesStep : ConfigureServicesStep
    {
        public string? RecordedDnsName { get; private set; }
        public string? RecordedEndpoint { get; private set; }
        public string? VerifiedDnsName { get; private set; }
        public string? VerifiedEndpoint { get; private set; }
        private readonly DnsEmulator _dns;

        public AzureEmulatorConfigureServicesStep(
            ILogger<ConfigureServicesStep> logger,
            IAzureCli azureCli,
            DnsEmulator dns)
            : base(logger, awsCli: null, azureCli: azureCli, gcloudCli: null)
        {
            _dns = dns ?? throw new ArgumentNullException(nameof(dns));
        }

        protected override Task ConfigureAzureDNS(string dnsName, string endpoint, CancellationToken cancellationToken)
        {
            RecordedDnsName = dnsName;
            RecordedEndpoint = endpoint;
            var zoneName = ExtractZoneName(dnsName);
            _dns.RegisterZone(zoneName);
            var recordName = DnsEmulator.GetRecordSegment(dnsName, zoneName);
            _dns.DeleteCname(zoneName, recordName);
            _dns.UpsertCname(zoneName, recordName, endpoint);
            return VerifyDnsRecordAsync(dnsName, endpoint, cancellationToken);
        }

        protected override Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
        {
            VerifiedDnsName = dnsName;
            VerifiedEndpoint = expectedValue;
            if (!_dns.TryGetCnameByDnsName(dnsName, out var actual))
            {
                throw new InvalidOperationException($"DNS record {dnsName} was not created in emulator.");
            }

            if (!string.Equals(actual, expectedValue.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected DNS record {dnsName} to resolve to {expectedValue} but found {actual}.");
            }

            return Task.CompletedTask;
        }
    }

    private class GcsEmulatorConfigureServicesStep : ConfigureServicesStep
    {
        public string? RecordedDnsName { get; private set; }
        public string? RecordedEndpoint { get; private set; }
        public string? VerifiedDnsName { get; private set; }
        public string? VerifiedEndpoint { get; private set; }
        private readonly DnsEmulator _dns;

        public GcsEmulatorConfigureServicesStep(
            ILogger<ConfigureServicesStep> logger,
            IGcloudCli gcloudCli,
            DnsEmulator dns)
            : base(logger, awsCli: null, azureCli: null, gcloudCli: gcloudCli)
        {
            _dns = dns ?? throw new ArgumentNullException(nameof(dns));
        }

        protected override Task ConfigureCloudDNS(string dnsName, string endpoint, CancellationToken cancellationToken)
        {
            RecordedDnsName = dnsName;
            RecordedEndpoint = endpoint;
            var zoneName = ExtractZoneName(dnsName);
            _dns.RegisterZone(zoneName);
            var recordName = DnsEmulator.GetRecordSegment(dnsName, zoneName);
            _dns.DeleteCname(zoneName, recordName);
            _dns.UpsertCname(zoneName, recordName, endpoint);
            return VerifyDnsRecordAsync(dnsName, endpoint, cancellationToken);
        }

        protected override Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
        {
            VerifiedDnsName = dnsName;
            VerifiedEndpoint = expectedValue;
            if (!_dns.TryGetCnameByDnsName(dnsName, out var actual))
            {
                throw new InvalidOperationException($"DNS record {dnsName} was not created in emulator.");
            }

            if (!string.Equals(actual, expectedValue.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Expected DNS record {dnsName} to resolve to {expectedValue} but found {actual}.");
            }

            return Task.CompletedTask;
        }
    }

    private ParameterExtractionService CreateParameterExtractionService()
    {
        return new ParameterExtractionService(
            _kernelFixture.MockLLM,
            NullLogger<ParameterExtractionService>.Instance);
    }

    private sealed class FaultingAzureEmulatorConfigureServicesStep : AzureEmulatorConfigureServicesStep
    {
        public FaultingAzureEmulatorConfigureServicesStep(
            ILogger<ConfigureServicesStep> logger,
            IAzureCli azureCli,
            DnsEmulator dns)
            : base(logger, azureCli, dns)
        {
        }

        protected override async Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
        {
            await base.VerifyDnsRecordAsync(dnsName, expectedValue, cancellationToken);
            throw new InvalidOperationException("Simulated Azure DNS verification failure");
        }
    }

    private sealed class FaultingGcsEmulatorConfigureServicesStep : GcsEmulatorConfigureServicesStep
    {
        public FaultingGcsEmulatorConfigureServicesStep(
            ILogger<ConfigureServicesStep> logger,
            IGcloudCli gcloudCli,
            DnsEmulator dns)
            : base(logger, gcloudCli, dns)
        {
        }

        protected override async Task VerifyDnsRecordAsync(string dnsName, string expectedValue, CancellationToken cancellationToken)
        {
            await base.VerifyDnsRecordAsync(dnsName, expectedValue, cancellationToken);
            throw new InvalidOperationException("Simulated GCP DNS verification failure");
        }
    }

    private sealed class FailingAwsCli : IAwsCli
    {
        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            throw new InvalidOperationException("Simulated AWS CLI failure");
        }
    }

    private sealed class FailingAzureCli : IAzureCli
    {
        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            throw new InvalidOperationException("Simulated Azure CLI failure");
        }
    }

    private sealed class FailingGcloudCli : IGcloudCli
    {
        public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
        {
            throw new InvalidOperationException("Simulated gcloud CLI failure");
        }
    }

    private sealed record ProcessHarnessResult(
        IReadOnlyList<KernelProcessEvent> ConfigureEvents,
        IReadOnlyList<KernelProcessEvent> DeployEvents);

    private sealed record FullProcessHarnessResult(
        IReadOnlyList<KernelProcessEvent> ConfigureEvents,
        IReadOnlyList<KernelProcessEvent> DeployEvents,
        IReadOnlyList<KernelProcessEvent> ValidateEvents);

    private static async Task<ProcessHarnessResult> RunConfigureDeployPipelineAsync(
        ConfigureServicesStep configureStep,
        DeployHonuaApplicationStep deployStep,
        DeploymentState deploymentState,
        CancellationToken cancellationToken)
    {
        var configureState = new KernelProcessStepState<DeploymentState>("ConfigureServicesStep", "ConfigureServices", "1.0")
        {
            State = deploymentState
        };

        await configureStep.ActivateAsync(configureState);

        var configureChannel = new TestKernelProcessMessageChannel();
        var configureContext = new KernelProcessStepContext(configureChannel);

        await configureStep.ConfigureServicesAsync(configureContext, cancellationToken);

        var deployState = new KernelProcessStepState<DeploymentState>("DeployApplicationStep", "DeployApplication", "1.0")
        {
            State = configureState.State
        };

        await deployStep.ActivateAsync(deployState);

        var deployChannel = new TestKernelProcessMessageChannel();
        var deployContext = new KernelProcessStepContext(deployChannel);

        if (configureChannel.Events.Any(evt => evt.Id == "ServicesConfigured"))
        {
            if (deployStep is RecordingDeployStep recording)
            {
                await recording.DeployApplicationAsync(deployContext, cancellationToken);
            }
            else
            {
                await deployStep.DeployApplicationAsync(deployContext, cancellationToken);
            }
        }

        return new ProcessHarnessResult(
            configureChannel.Events.ToList(),
            deployChannel.Events.ToList());
    }

    private static async Task<FullProcessHarnessResult> RunConfigureDeployValidatePipelineAsync(
        ConfigureServicesStep configureStep,
        DeployHonuaApplicationStep deployStep,
        KernelProcessStep<DeploymentState> validateStep,
        DeploymentState deploymentState,
        CancellationToken cancellationToken)
    {
        var configureDeploy = await RunConfigureDeployPipelineAsync(
            configureStep,
            deployStep,
            deploymentState,
            cancellationToken);

        var validateEvents = new List<KernelProcessEvent>();

        if (configureDeploy.DeployEvents.Any(evt => evt.Id == "ApplicationDeployed"))
        {
            var validateState = new KernelProcessStepState<DeploymentState>("ValidateDeploymentStep", "ValidateDeployment", "1.0")
            {
                State = deploymentState
            };

            await validateStep.ActivateAsync(validateState);

            var validateChannel = new TestKernelProcessMessageChannel();
            var validateContext = new KernelProcessStepContext(validateChannel);

            switch (validateStep)
            {
                case HarnessValidateDeploymentStep harnessStep:
                    await harnessStep.ValidateAsync(validateContext, cancellationToken);
                    break;
                case ValidateDeploymentStep realStep:
                    await realStep.ValidateDeploymentAsync(validateContext, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported validation step type: {validateStep.GetType().Name}");
            }

            validateEvents.AddRange(validateChannel.Events);
        }

        return new FullProcessHarnessResult(
            configureDeploy.ConfigureEvents,
            configureDeploy.DeployEvents,
            validateEvents);
    }

    private sealed class RecordingDeployStep : DeployHonuaApplicationStep
    {
        private DeploymentState _state = new();

        public RecordingDeployStep()
            : base(NullLogger<DeployHonuaApplicationStep>.Instance, new NoopAwsCli(), new NoopAzureCli(), new NoopGcloudCli())
        {
        }

        public int ExecutionCount { get; private set; }

        public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
        {
            _state = state.State ?? new DeploymentState();
            return base.ActivateAsync(state);
        }

        public new async Task DeployApplicationAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ApplicationDeployed",
                Data = _state
            });
        }
    }

    private sealed class HarnessValidateDeploymentStep : KernelProcessStep<DeploymentState>
    {
        private DeploymentState _state = new();
        private readonly bool _shouldFail;

        public HarnessValidateDeploymentStep(bool shouldFail = false)
        {
            _shouldFail = shouldFail;
        }

        public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
        {
            _state = state.State ?? new DeploymentState();
            return ValueTask.CompletedTask;
        }

        [KernelFunction("ValidateDeployment")]
        public async Task ValidateAsync(KernelProcessStepContext context, CancellationToken cancellationToken = default)
        {
            if (_shouldFail)
            {
                _state.Status = "ValidationFailed";
                await context.EmitEventAsync(new KernelProcessEvent
                {
                    Id = "ValidationFailed",
                    Data = new { _state.DeploymentId, Error = "Harness validation failure" }
                });
                return;
            }

            _state.Status = "DeploymentValidated";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "DeploymentValidated",
                Data = _state
            });
        }
    }

    private async Task<TerraformArtifacts> GenerateTerraformArtifactsAsync(DeploymentState state)
    {
        var generateStep = new GenerateInfrastructureCodeStep(NullLogger<GenerateInfrastructureCodeStep>.Instance);
        var stepState = new KernelProcessStepState<DeploymentState>("GenerateInfrastructureStep", "GenerateInfrastructure", "1.0")
        {
            State = state
        };

        await generateStep.ActivateAsync(stepState);

        var channel = new TestKernelProcessMessageChannel();
        var context = new KernelProcessStepContext(channel);

        await generateStep.GenerateInfrastructureAsync(context);

        channel.Events.Should().Contain(evt => evt.Id == "InfrastructureGenerated");

        state.TerraformWorkspacePath.Should().NotBeNullOrEmpty("Terraform generation should set workspace path");

        var workspace = state.TerraformWorkspacePath!;
        var mainTfPath = Path.Combine(workspace, "main.tf");
        var tfVarsPath = Path.Combine(workspace, "terraform.tfvars");

        File.Exists(mainTfPath).Should().BeTrue($"main.tf should exist in workspace {workspace}");
        File.Exists(tfVarsPath).Should().BeTrue($"terraform.tfvars should exist in workspace {workspace}");

        var outputsSnapshot = new Dictionary<string, string>(state.InfrastructureOutputs, StringComparer.OrdinalIgnoreCase);
        var secret = ResolveGeneratedSecret(state.CloudProvider, outputsSnapshot);

        AssertTerraformFileContents(state.CloudProvider, mainTfPath, tfVarsPath, secret);
        LogTerraformArtifacts(workspace, tfVarsPath);

        var artifacts = new TerraformArtifacts(workspace, mainTfPath, tfVarsPath, secret, outputsSnapshot);
        PersistTerraformArtifacts(state.CloudProvider, artifacts);
        return artifacts;
    }

    private static string? ResolveGeneratedSecret(string provider, IReadOnlyDictionary<string, string> outputs)
    {
        return provider.ToLowerInvariant() switch
        {
            "aws" => outputs.TryGetValue("db_password", out var value) ? value : null,
            "azure" => outputs.TryGetValue("db_admin_password", out var value) ? value : null,
            "gcp" => outputs.TryGetValue("db_root_password", out var value) ? value : null,
            _ => null
        };
    }

    private void AssertTerraformFileContents(string provider, string mainTfPath, string tfVarsPath, string? generatedSecret)
    {
        var mainContents = File.ReadAllText(mainTfPath);
        var providerKey = provider.ToLowerInvariant() switch
        {
            "aws" => "provider \"aws\"",
            "azure" => "provider \"azurerm\"",
            "gcp" => "provider \"google\"",
            _ => throw new InvalidOperationException($"Unsupported provider {provider}")
        };

        mainContents.Should().Contain(providerKey);

        var tfVarsContents = File.ReadAllText(tfVarsPath);
        switch (provider.ToLowerInvariant())
        {
            case "aws":
                tfVarsContents.Should().Contain("db_password =");
                mainContents.Should().Contain("resource \"aws_lb\"");
                mainContents.Should().Contain("resource \"aws_db_instance\"");
                break;
            case "azure":
                tfVarsContents.Should().Contain("db_admin_password =");
                mainContents.Should().Contain("resource \"azurerm_storage_account\"");
                mainContents.Should().Contain("resource \"azurerm_postgresql_server\"");
                break;
            case "gcp":
                tfVarsContents.Should().Contain("db_root_password =");
                mainContents.Should().Contain("resource \"google_storage_bucket\"");
                mainContents.Should().Contain("resource \"google_sql_database_instance\"");
                break;
        }

        if (!string.IsNullOrEmpty(generatedSecret))
        {
            tfVarsContents.Should().Contain(generatedSecret);
        }
    }

    private void LogTerraformArtifacts(string workspace, string tfVarsPath)
    {
        var tfVarsSize = new FileInfo(tfVarsPath).Length;
        _output.WriteLine($"Terraform workspace: {workspace}");
        _output.WriteLine($"terraform.tfvars size: {tfVarsSize} bytes");
    }

    private void PersistTerraformArtifacts(string provider, TerraformArtifacts artifacts, [CallerMemberName] string testName = "")
    {
        var sanitizedTest = SanitizeFileSegment(testName);
        var sanitizedProvider = SanitizeFileSegment(provider.ToLowerInvariant());

        var targetRoot = Path.Combine(AppContext.BaseDirectory, "terraform-artifacts", sanitizedTest, sanitizedProvider);
        Directory.CreateDirectory(targetRoot);

        var mainTarget = Path.Combine(targetRoot, "main.tf");
        File.Copy(artifacts.MainTfPath, mainTarget, overwrite: true);

        var tfVarsTarget = Path.Combine(targetRoot, "terraform.tfvars");
        var sanitizedVars = SanitizeTfVars(File.ReadAllText(artifacts.TfVarsPath));
        File.WriteAllText(tfVarsTarget, sanitizedVars);

        _output.WriteLine($"Saved Terraform artifacts → {targetRoot}");
    }

    private static string SanitizeFileSegment(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static string SanitizeTfVars(string content)
    {
        // Mask any obvious secret values to avoid leaking credentials into artifacts
        var passwordPattern = new Regex(@"(?i)(password\s*=\s*"")[^""]*(""|\n)", RegexOptions.Multiline);
        var secretPattern = new Regex(@"(?i)(secret\s*=\s*"")[^""]*(""|\n)", RegexOptions.Multiline);

        var sanitized = passwordPattern.Replace(content, "$1***REDACTED***$2");
        sanitized = secretPattern.Replace(sanitized, "$1***REDACTED***$2");
        return sanitized;
    }

    private sealed record TerraformArtifacts(
        string WorkspacePath,
        string MainTfPath,
        string TfVarsPath,
        string? GeneratedSecret,
        IReadOnlyDictionary<string, string> Outputs);

    private void ApplyDatabaseOutputsOrSkip(DeploymentState state)
    {
        Skip.If(!_postgresFixture.IsDockerAvailable, "Docker is required for Postgres emulator tests.");

        try
        {
            var endpoint = _postgresFixture.Endpoint;
            var database = _postgresFixture.DatabaseName;
            var username = _postgresFixture.Username;
            var password = _postgresFixture.Password;

            var outputs = state.InfrastructureOutputs;
            outputs["database_endpoint"] = endpoint;
            outputs["database_name"] = database;
            outputs["database_username"] = username;
            outputs["db_password"] = password;
            outputs["database_password"] = password;
            outputs["postgres_password"] = password;
            outputs["database_type"] = "postgresql";
            outputs["database_ssl_mode"] = "Disable";
            outputs["database_trust_server_certificate"] = "false";
        }
        catch (InvalidOperationException)
        {
            Skip.If(true, "Postgres emulator endpoint not initialised.");
        }
    }

    private void RequireLocalStack()
    {
        Skip.If(!_deploymentFixture.IsDockerAvailable, "Docker is required for LocalStack emulator tests.");
        Skip.If(!_deploymentFixture.LocalStackAvailable, "LocalStack container failed to start; check Docker logs.");
    }

    private void RequireAzurite()
    {
        Skip.If(!_azuriteFixture.IsDockerAvailable, "Docker is required for Azurite emulator tests.");
    }

    private void RequireGcpEmulator()
    {
        Skip.If(!_gcsFixture.IsDockerAvailable, "Docker is required for fake GCS emulator tests.");
    }

    private static DeploymentGuardrailDecision CreateGuardrailDecision(string provider) => new DeploymentGuardrailDecision
    {
        WorkloadProfile = "standard",
        Envelope = new ResourceEnvelope(
            id: $"{provider.ToLowerInvariant()}-standard",
            cloudProvider: provider,
            platform: "test",
            workloadProfile: "standard",
            minVCpu: 1m,
            minMemoryGb: 2m,
            minEphemeralGb: 1m,
            minInstances: 1)
    };

    private sealed class ValidationEnvironment : IDisposable
    {
        private readonly ValidationTestServer _server;
        public ValidateDeploymentStep Step { get; }

        public ValidationEnvironment(bool success)
        {
            _server = new ValidationTestServer(success);
            var guardrailMonitor = new PostDeployGuardrailMonitor(
                new NullTelemetryService(),
                NullLogger<PostDeployGuardrailMonitor>.Instance);
            var metricsProvider = new StaticMetricsProvider();
            Step = new ValidateDeploymentStep(
                NullLogger<ValidateDeploymentStep>.Instance,
                guardrailMonitor,
                metricsProvider,
                _server);
        }

        public void Dispose()
        {
            _server.Dispose();
        }
    }

    private sealed class StaticMetricsProvider : IDeploymentMetricsProvider
    {
        public Task<DeploymentGuardrailMetrics> GetMetricsAsync(DeploymentState state, DeploymentGuardrailDecision decision, CancellationToken cancellationToken = default)
        {
            var metrics = new DeploymentGuardrailMetrics(
                CpuUtilization: 0.25m,
                MemoryUtilizationGb: 2m,
                ColdStartsPerHour: 0,
                QueueBacklog: 0,
                AverageLatencyMs: 120m);
            return Task.FromResult(metrics);
        }
    }

    private sealed class ValidationTestServer : IHttpClientFactory, IDisposable
    {
        private readonly WebApplication _app;
        private readonly bool _shouldSucceed;

        public ValidationTestServer(bool shouldSucceed)
        {
            _shouldSucceed = shouldSucceed;
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            _app = builder.Build();

            if (_shouldSucceed)
            {
                _app.MapGet("/", () => Results.Ok("landing"));
                _app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
                _app.MapGet("/conformance", () => Results.Ok(new { conformsTo = Array.Empty<string>() }));
                _app.MapGet("/collections", () => Results.Ok(new { collections = Array.Empty<object>() }));
            }
            else
            {
                _app.MapGet("/", () => Results.Ok("landing"));
                _app.MapGet("/health", () => Results.StatusCode(500));
                _app.MapGet("/conformance", () => Results.Ok(new { conformsTo = Array.Empty<string>() }));
                _app.MapGet("/collections", () => Results.Ok(new { collections = Array.Empty<object>() }));
            }

            _app.StartAsync().GetAwaiter().GetResult();
        }

        public HttpClient CreateClient(string name)
        {
            var client = _app.GetTestClient();
            client.BaseAddress = new Uri("http://localhost");
            return client;
        }

        public void Dispose()
        {
            _app.StopAsync().GetAwaiter().GetResult();
            ((IAsyncDisposable)_app).DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
