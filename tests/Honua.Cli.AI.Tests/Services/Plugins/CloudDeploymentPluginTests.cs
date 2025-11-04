using System;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Honua.Cli.AI.Services.Plugins;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.Plugins;

/// <summary>
/// Comprehensive tests for CloudDeploymentPlugin - critical deployment operations plugin.
/// Tests Docker, Kubernetes, Terraform generation, and cloud provider recommendations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Priority", "Critical")]
[Trait("Plugin", "CloudDeployment")]
public class CloudDeploymentPluginTests
{
    private readonly CloudDeploymentPlugin _plugin;

    public CloudDeploymentPluginTests()
    {
        _plugin = new CloudDeploymentPlugin();
    }

    #region GenerateDockerfile Tests

    [Fact]
    public void GenerateDockerfile_DefaultConfig_ReturnsValidDockerfile()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.TryGetProperty("dockerfile", out var dockerfile).Should().BeTrue();
        dockerfile.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateDockerfile_IncludesMultiStageBuilds()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        var dockerfile = json.RootElement.GetProperty("dockerfile").GetString()!;

        dockerfile.Should().Contain("AS build");
        dockerfile.Should().Contain("AS publish");
        dockerfile.Should().Contain("AS final");
    }

    [Fact]
    public void GenerateDockerfile_UsesSecureBaseImages()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        var dockerfile = json.RootElement.GetProperty("dockerfile").GetString()!;

        dockerfile.Should().Contain("mcr.microsoft.com/dotnet/sdk:8.0");
        dockerfile.Should().Contain("mcr.microsoft.com/dotnet/aspnet:8.0");
    }

    [Fact]
    public void GenerateDockerfile_IncludesNonRootUser()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        var dockerfile = json.RootElement.GetProperty("dockerfile").GetString()!;

        dockerfile.Should().Contain("useradd");
        dockerfile.Should().Contain("USER honua");
        dockerfile.Should().NotContain("USER root");
    }

    [Fact]
    public void GenerateDockerfile_IncludesHealthCheck()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        var dockerfile = json.RootElement.GetProperty("dockerfile").GetString()!;

        dockerfile.Should().Contain("HEALTHCHECK");
        dockerfile.Should().Contain("/health");
    }

    [Fact]
    public void GenerateDockerfile_IncludesGDAL()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        var dockerfile = json.RootElement.GetProperty("dockerfile").GetString()!;

        dockerfile.Should().Contain("gdal-bin");
        dockerfile.Should().Contain("libgdal-dev");
    }

    [Fact]
    public void GenerateDockerfile_IncludesDockerIgnore()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("dockerignore", out var dockerignore).Should().BeTrue();

        var ignoreContent = dockerignore.GetString()!;
        ignoreContent.Should().Contain("**/bin/");
        ignoreContent.Should().Contain("**/obj/");
        ignoreContent.Should().Contain("**/.env");
        ignoreContent.Should().Contain("**/secrets.json");
    }

    [Fact]
    public void GenerateDockerfile_IncludesBuildCommands()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("buildCommand", out var buildCmd).Should().BeTrue();
        json.RootElement.TryGetProperty("runCommand", out var runCmd).Should().BeTrue();

        buildCmd.GetString().Should().Contain("docker build");
        runCmd.GetString().Should().Contain("docker run");
    }

    [Fact]
    public void GenerateDockerfile_IncludesOptimizations()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("optimizations", out var optimizations).Should().BeTrue();

        var opts = optimizations.EnumerateArray().Select(e => e.GetString()).ToList();
        opts.Should().Contain(o => o!.Contains("Multi-stage"));
        opts.Should().Contain(o => o!.Contains("Non-root user"));
        opts.Should().Contain(o => o!.Contains("Health check"));
    }

    [Fact]
    public void GenerateDockerfile_JsonIsIndented()
    {
        // Act
        var result = _plugin.GenerateDockerfile();

        // Assert
        result.Should().Contain("\n");
        result.Should().Contain("  ");
    }

    [Fact]
    public void GenerateDockerfile_WithCustomConfig_ReturnsValidJson()
    {
        // Arrange
        var config = @"{""platform"":""docker"",""runtime"":""dotnet""}";

        // Act
        var result = _plugin.GenerateDockerfile(config);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region GenerateKubernetesManifests Tests

    [Fact]
    public void GenerateKubernetesManifests_ReturnsAllManifests()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var manifests = json.RootElement.GetProperty("manifests");

        manifests.TryGetProperty("deployment", out _).Should().BeTrue();
        manifests.TryGetProperty("service", out _).Should().BeTrue();
        manifests.TryGetProperty("ingress", out _).Should().BeTrue();
        manifests.TryGetProperty("hpa", out _).Should().BeTrue();
        manifests.TryGetProperty("configMap", out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateKubernetesManifests_DeploymentHasReplicas()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var deployment = json.RootElement.GetProperty("manifests")
            .GetProperty("deployment").GetString()!;

        deployment.Should().Contain("replicas: 3");
    }

    [Fact]
    public void GenerateKubernetesManifests_DeploymentHasResourceLimits()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var deployment = json.RootElement.GetProperty("manifests")
            .GetProperty("deployment").GetString()!;

        deployment.Should().Contain("resources:");
        deployment.Should().Contain("requests:");
        deployment.Should().Contain("limits:");
        deployment.Should().Contain("memory:");
        deployment.Should().Contain("cpu:");
    }

    [Fact]
    public void GenerateKubernetesManifests_DeploymentHasHealthProbes()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var deployment = json.RootElement.GetProperty("manifests")
            .GetProperty("deployment").GetString()!;

        deployment.Should().Contain("livenessProbe:");
        deployment.Should().Contain("readinessProbe:");
        deployment.Should().Contain("/health/live");
        deployment.Should().Contain("/health/ready");
    }

    [Fact]
    public void GenerateKubernetesManifests_DeploymentUsesSecrets()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var deployment = json.RootElement.GetProperty("manifests")
            .GetProperty("deployment").GetString()!;

        deployment.Should().Contain("secretKeyRef:");
        deployment.Should().Contain("honua-secrets");
    }

    [Fact]
    public void GenerateKubernetesManifests_ServiceHasLoadBalancer()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var service = json.RootElement.GetProperty("manifests")
            .GetProperty("service").GetString()!;

        service.Should().Contain("type: LoadBalancer");
        service.Should().Contain("port: 80");
    }

    [Fact]
    public void GenerateKubernetesManifests_IngressHasTLS()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var ingress = json.RootElement.GetProperty("manifests")
            .GetProperty("ingress").GetString()!;

        ingress.Should().Contain("tls:");
        ingress.Should().Contain("cert-manager.io/cluster-issuer");
        ingress.Should().Contain("letsencrypt-prod");
    }

    [Fact]
    public void GenerateKubernetesManifests_HPAHasScalingMetrics()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var hpa = json.RootElement.GetProperty("manifests")
            .GetProperty("hpa").GetString()!;

        hpa.Should().Contain("minReplicas: 3");
        hpa.Should().Contain("maxReplicas: 10");
        hpa.Should().Contain("cpu");
        hpa.Should().Contain("memory");
    }

    [Fact]
    public void GenerateKubernetesManifests_ConfigMapHasMetadata()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var configMap = json.RootElement.GetProperty("manifests")
            .GetProperty("configMap").GetString()!;

        configMap.Should().Contain("metadata.yaml");
        configMap.Should().Contain("collections:");
    }

    [Fact]
    public void GenerateKubernetesManifests_IncludesDeploymentCommands()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("deploymentCommands", out var commands).Should().BeTrue();

        var commandList = commands.EnumerateArray().Select(e => e.GetString()).ToList();
        commandList.Should().Contain(c => c!.Contains("kubectl apply"));
        commandList.Should().Contain(c => c!.Contains("secrets"));
        commandList.Should().Contain(c => c!.Contains("ingress"));
    }

    [Fact]
    public void GenerateKubernetesManifests_AllManifestsHaveNamespace()
    {
        // Act
        var result = _plugin.GenerateKubernetesManifests();

        // Assert
        var json = JsonDocument.Parse(result);
        var manifests = json.RootElement.GetProperty("manifests");

        foreach (var property in manifests.EnumerateObject())
        {
            var manifest = property.Value.GetString()!;
            manifest.Should().Contain("namespace:");
        }
    }

    [Fact]
    public void GenerateKubernetesManifests_WithCustomRequirements_ReturnsValidJson()
    {
        // Arrange
        var requirements = @"{""platform"":""kubernetes"",""namespace"":""custom""}";

        // Act
        var result = _plugin.GenerateKubernetesManifests(requirements);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region SuggestCloudProvider Tests

    [Fact]
    public void SuggestCloudProvider_ReturnsAllMajorProviders()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var providers = json.RootElement.GetProperty("providers");

        var providerNames = providers.EnumerateArray()
            .Select(p => p.GetProperty("provider").GetString())
            .ToList();

        providerNames.Should().Contain("AWS");
        providerNames.Should().Contain("Azure");
        providerNames.Should().Contain("GCP");
        providerNames.Should().Contain("DigitalOcean");
    }

    [Fact]
    public void SuggestCloudProvider_AllProvidersHaveServices()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var providers = json.RootElement.GetProperty("providers");

        foreach (var provider in providers.EnumerateArray())
        {
            provider.TryGetProperty("services", out var services).Should().BeTrue();
            var servicesObj = services;
            servicesObj.TryGetProperty("compute", out _).Should().BeTrue();
            servicesObj.TryGetProperty("database", out _).Should().BeTrue();
            servicesObj.TryGetProperty("storage", out _).Should().BeTrue();
        }
    }

    [Fact]
    public void SuggestCloudProvider_AllProvidersHaveProsAndCons()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var providers = json.RootElement.GetProperty("providers");

        foreach (var provider in providers.EnumerateArray())
        {
            provider.TryGetProperty("pros", out var pros).Should().BeTrue();
            provider.TryGetProperty("cons", out var cons).Should().BeTrue();

            pros.GetArrayLength().Should().BeGreaterThan(0);
            cons.GetArrayLength().Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void SuggestCloudProvider_AllProvidersHaveCostEstimate()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var providers = json.RootElement.GetProperty("providers");

        foreach (var provider in providers.EnumerateArray())
        {
            provider.TryGetProperty("estimatedCost", out var cost).Should().BeTrue();
            cost.GetString().Should().NotBeNullOrEmpty();
            cost.GetString().Should().Contain("$");
        }
    }

    [Fact]
    public void SuggestCloudProvider_AllProvidersHaveSetupInstructions()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var providers = json.RootElement.GetProperty("providers");

        foreach (var provider in providers.EnumerateArray())
        {
            provider.TryGetProperty("setup", out var setup).Should().BeTrue();
            setup.GetString().Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void SuggestCloudProvider_IncludesComparisonMatrix()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("comparisonMatrix", out var matrix).Should().BeTrue();

        matrix.GetArrayLength().Should().BeGreaterThan(0);

        // Check for key comparison features
        var features = matrix.EnumerateArray()
            .Select(f => f.GetProperty("feature").GetString())
            .ToList();

        features.Should().Contain("Price");
        features.Should().Contain("Ease of Use");
        features.Should().Contain("PostGIS Support");
    }

    [Fact]
    public void SuggestCloudProvider_IncludesRecommendations()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        json.RootElement.TryGetProperty("recommendations", out var recs).Should().BeTrue();

        recs.TryGetProperty("enterprise", out _).Should().BeTrue();
        recs.TryGetProperty("startups", out _).Should().BeTrue();
        recs.TryGetProperty("costSensitive", out _).Should().BeTrue();
        recs.TryGetProperty("dotnetShops", out _).Should().BeTrue();
    }

    [Fact]
    public void SuggestCloudProvider_AWSRecommendedForEnterprise()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var recs = json.RootElement.GetProperty("recommendations");

        var enterprise = recs.GetProperty("enterprise").GetString()!;
        enterprise.Should().Contain("AWS");
    }

    [Fact]
    public void SuggestCloudProvider_DigitalOceanRecommendedForCostSensitive()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var recs = json.RootElement.GetProperty("recommendations");

        var costSensitive = recs.GetProperty("costSensitive").GetString()!;
        costSensitive.Should().Contain("DigitalOcean");
    }

    [Fact]
    public void SuggestCloudProvider_AzureRecommendedForDotNet()
    {
        // Act
        var result = _plugin.SuggestCloudProvider();

        // Assert
        var json = JsonDocument.Parse(result);
        var recs = json.RootElement.GetProperty("recommendations");

        var dotnet = recs.GetProperty("dotnetShops").GetString()!;
        dotnet.Should().Contain("Azure");
    }

    [Fact]
    public void SuggestCloudProvider_WithConstraints_ReturnsValidJson()
    {
        // Arrange
        var constraints = @"{""budget"":""low"",""region"":""eu""}";

        // Act
        var result = _plugin.SuggestCloudProvider(constraints);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region GenerateTerraformConfig Tests

    [Fact]
    public void GenerateTerraformConfig_ReturnsCompleteConfig()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.TryGetProperty("terraformConfig", out _).Should().BeTrue();
        root.TryGetProperty("commands", out _).Should().BeTrue();
        root.TryGetProperty("bestPractices", out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesProviderConfiguration()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("terraform {");
        config.Should().Contain("required_providers {");
        config.Should().Contain("aws = {");
        config.Should().Contain("provider \"aws\"");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesRemoteBackend()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("backend \"s3\"");
        config.Should().Contain("terraform-state");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesVPC()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("aws_vpc");
        config.Should().Contain("enable_dns_hostnames");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesRDSPostgreSQL()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("aws_db_instance");
        config.Should().Contain("engine");
        config.Should().Contain("\"postgres\"");
        config.Should().Contain("storage_encrypted");
        config.Should().Contain("backup_retention_period");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesECSCluster()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("aws_ecs_cluster");
        config.Should().Contain("containerInsights");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesFargateTaskDefinition()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("aws_ecs_task_definition");
        config.Should().Contain("FARGATE");
        config.Should().Contain("awsvpc");
    }

    [Fact]
    public void GenerateTerraformConfig_TaskDefinitionUsesSecrets()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("secrets = [");
        config.Should().Contain("aws_secretsmanager_secret");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesLoadBalancer()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("aws_lb");
        config.Should().Contain("load_balancer_type = \"application\"");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesOutputs()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var config = json.RootElement.GetProperty("terraformConfig").GetString()!;

        config.Should().Contain("output");
        config.Should().Contain("alb_dns_name");
        config.Should().Contain("rds_endpoint");
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesTerraformCommands()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var commands = json.RootElement.GetProperty("commands");

        var commandList = commands.EnumerateArray().Select(e => e.GetString()).ToList();
        commandList.Should().Contain("terraform init");
        commandList.Should().Contain(c => c!.Contains("terraform plan"));
        commandList.Should().Contain(c => c!.Contains("terraform apply"));
    }

    [Fact]
    public void GenerateTerraformConfig_IncludesBestPractices()
    {
        // Act
        var result = _plugin.GenerateTerraformConfig();

        // Assert
        var json = JsonDocument.Parse(result);
        var practices = json.RootElement.GetProperty("bestPractices");

        var practiceList = practices.EnumerateArray().Select(e => e.GetString()).ToList();
        practiceList.Should().Contain(p => p!.Contains("remote state"));
        practiceList.Should().Contain(p => p!.Contains("secrets"));
        practiceList.Should().Contain(p => p!.Contains("encryption"));
    }

    [Fact]
    public void GenerateTerraformConfig_WithCustomInfrastructure_ReturnsValidJson()
    {
        // Arrange
        var infrastructure = @"{""platform"":""gcp"",""region"":""us-west1""}";

        // Act
        var result = _plugin.GenerateTerraformConfig(infrastructure);

        // Assert
        result.Should().NotBeNullOrEmpty();
        var action = () => JsonDocument.Parse(result);
        action.Should().NotThrow();
    }

    #endregion

    #region OptimizeForServerless Tests

    [Fact]
    public void OptimizeForServerless_ReturnsServerlessOptimizations()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        result.Should().NotBeNullOrEmpty();
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        root.TryGetProperty("serverlessOptimizations", out _).Should().BeTrue();
        root.TryGetProperty("awsLambda", out _).Should().BeTrue();
        root.TryGetProperty("azureFunctions", out _).Should().BeTrue();
    }

    [Fact]
    public void OptimizeForServerless_IncludesColdStartMitigation()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var optimizations = json.RootElement.GetProperty("serverlessOptimizations");

        var hasColdStartOptimization = false;
        foreach (var opt in optimizations.EnumerateArray())
        {
            if (opt.GetProperty("aspect").GetString()!.Contains("Cold Start"))
            {
                hasColdStartOptimization = true;
                opt.TryGetProperty("strategies", out var strategies).Should().BeTrue();
                strategies.GetArrayLength().Should().BeGreaterThan(0);
            }
        }

        hasColdStartOptimization.Should().BeTrue();
    }

    [Fact]
    public void OptimizeForServerless_IncludesConnectionPooling()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var optimizations = json.RootElement.GetProperty("serverlessOptimizations");

        var hasConnectionPooling = false;
        foreach (var opt in optimizations.EnumerateArray())
        {
            if (opt.GetProperty("aspect").GetString()!.Contains("Connection Pooling"))
            {
                hasConnectionPooling = true;
                opt.TryGetProperty("strategies", out var strategies).Should().BeTrue();
                var strategyList = strategies.EnumerateArray().Select(s => s.GetString()).ToList();
                strategyList.Should().Contain(s => s!.Contains("RDS Proxy"));
            }
        }

        hasConnectionPooling.Should().BeTrue();
    }

    [Fact]
    public void OptimizeForServerless_IncludesStatelessDesign()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var optimizations = json.RootElement.GetProperty("serverlessOptimizations");

        var hasStatelessDesign = false;
        foreach (var opt in optimizations.EnumerateArray())
        {
            if (opt.GetProperty("aspect").GetString()!.Contains("Stateless"))
            {
                hasStatelessDesign = true;
            }
        }

        hasStatelessDesign.Should().BeTrue();
    }

    [Fact]
    public void OptimizeForServerless_IncludesResourceLimits()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var optimizations = json.RootElement.GetProperty("serverlessOptimizations");

        var hasResourceLimits = false;
        foreach (var opt in optimizations.EnumerateArray())
        {
            if (opt.GetProperty("aspect").GetString()!.Contains("Resource Limits"))
            {
                hasResourceLimits = true;
                opt.TryGetProperty("configuration", out var config).Should().BeTrue();
                config.TryGetProperty("memory", out _).Should().BeTrue();
                config.TryGetProperty("timeout", out _).Should().BeTrue();
            }
        }

        hasResourceLimits.Should().BeTrue();
    }

    [Fact]
    public void OptimizeForServerless_IncludesAWSLambdaImplementation()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var awsLambda = json.RootElement.GetProperty("awsLambda").GetString()!;

        awsLambda.Should().Contain("Amazon.Lambda.AspNetCoreServer");
        awsLambda.Should().Contain("APIGatewayProxyFunction");
        awsLambda.Should().Contain("serverless.template");
    }

    [Fact]
    public void OptimizeForServerless_IncludesAzureFunctionsImplementation()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        var json = JsonDocument.Parse(result);
        var azureFunctions = json.RootElement.GetProperty("azureFunctions").GetString()!;

        azureFunctions.Should().Contain("Azure Functions");
        azureFunctions.Should().Contain("Isolated Worker Model");
        azureFunctions.Should().Contain("HttpTrigger");
    }

    [Fact]
    public void OptimizeForServerless_JsonIsIndented()
    {
        // Act
        var result = _plugin.OptimizeForServerless("{}");

        // Assert
        result.Should().Contain("\n");
        result.Should().Contain("  ");
    }

    #endregion

    #region Integration and Edge Case Tests

    [Fact]
    public void AllMethods_ReturnValidJson()
    {
        // Act & Assert
        var methods = new Func<string>[]
        {
            () => _plugin.GenerateDockerfile(),
            () => _plugin.GenerateKubernetesManifests(),
            () => _plugin.SuggestCloudProvider(),
            () => _plugin.GenerateTerraformConfig(),
            () => _plugin.OptimizeForServerless("{}")
        };

        foreach (var method in methods)
        {
            var result = method();
            result.Should().NotBeNullOrEmpty();
            var action = () => JsonDocument.Parse(result);
            action.Should().NotThrow();
        }
    }

    [Fact]
    public void CloudDeploymentPlugin_IsSealed()
    {
        // Assert
        typeof(CloudDeploymentPlugin).IsSealed.Should().BeTrue(
            "Deployment plugins should be sealed to prevent tampering");
    }

    [Fact]
    public void CloudDeploymentPlugin_HasKernelFunctionAttributes()
    {
        // Assert
        var methods = typeof(CloudDeploymentPlugin).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var publicMethods = methods.Where(m =>
            !m.IsSpecialName &&
            m.DeclaringType == typeof(CloudDeploymentPlugin)).ToList();

        publicMethods.Count.Should().BeGreaterThan(0);

        foreach (var method in publicMethods)
        {
            var hasKernelFunction = method.GetCustomAttributes(false)
                .Any(attr => attr.GetType().Name == "KernelFunctionAttribute");

            hasKernelFunction.Should().BeTrue(
                $"Method {method.Name} should have KernelFunction attribute");
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData(@"{""key"":""value""}")]
    public void AllMethodsWithParameters_HandleVariousInputs(string input)
    {
        // Act & Assert - Should not throw
        var action1 = () => _plugin.GenerateDockerfile(input);
        var action2 = () => _plugin.GenerateKubernetesManifests(input);
        var action3 = () => _plugin.SuggestCloudProvider(input);
        var action4 = () => _plugin.GenerateTerraformConfig(input);
        var action5 = () => _plugin.OptimizeForServerless(input);

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
        action4.Should().NotThrow();
        action5.Should().NotThrow();
    }

    #endregion
}
