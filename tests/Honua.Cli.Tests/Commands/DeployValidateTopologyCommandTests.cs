using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.Commands;
using Honua.Cli.Services;
using Honua.Cli.Tests.Support;
using Honua.Server.Core.Performance;
using Spectre.Console.Cli;
using Spectre.Console.Testing;
using Xunit;

namespace Honua.Cli.Tests.Commands;

[Collection("CliTests")]
[Trait("Category", "Integration")]
public sealed class DeployValidateTopologyCommandTests : IDisposable
{
    private readonly TestConsole _console;
    private readonly TestEnvironment _environment;
    private readonly TemporaryDirectory _workspaceDir;

    public DeployValidateTopologyCommandTests()
    {
        _console = new TestConsole();
        _workspaceDir = new TemporaryDirectory();
        _environment = new TestEnvironment(_workspaceDir.Path);
    }

    public void Dispose()
    {
        _workspaceDir?.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenNoTopologyFileSpecified()
    {
        // Arrange
        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = null
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("No topology file specified");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidTopology_ShouldPass()
    {
        // Arrange
        var topologyFile = await CreateValidTopologyFileAsync("valid-topology.json");

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Topology is valid");
        _console.Output.Should().Contain("No issues found");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCloudProvider_ShouldFail()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("invalid-cloud.json", cloudProvider: "invalid-cloud");

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Invalid cloud provider");
        _console.Output.Should().Contain("aws");
        _console.Output.Should().Contain("azure");
        _console.Output.Should().Contain("gcp");
        _console.Output.Should().Contain("kubernetes");
        _console.Output.Should().Contain("docker");
    }

    [Fact]
    public async Task ExecuteAsync_WithMissingRegion_ShouldFail()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("missing-region.json", region: "");

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Region is required");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonStandardEnvironment_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("custom-env.json", environment: "custom");

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0); // Warning, not error
        _console.Output.Should().Contain("Non-standard environment");
        _console.Output.Should().Contain("Recommended");
        _console.Output.Should().Contain("dev");
        _console.Output.Should().Contain("staging");
        _console.Output.Should().Contain("prod");
    }

    [Fact]
    public async Task ExecuteAsync_WithSmallDatabaseStorage_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("small-db.json", databaseStorageGB: 5);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Database storage");
        _console.Output.Should().Contain("very small");
        _console.Output.Should().Contain("Minimum recommended: 20GB");
    }

    [Fact]
    public async Task ExecuteAsync_ProdWithoutHA_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("prod-no-ha.json",
            environment: "prod",
            databaseHighAvailability: false);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("High availability is recommended for production");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoComputeConfig_ShouldFail()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("no-compute.json", includeCompute: false);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Compute configuration is required");
    }

    [Fact]
    public async Task ExecuteAsync_ProdWithSingleInstance_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("prod-single.json",
            environment: "prod",
            instanceCount: 1);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Multiple instances recommended for production");
    }

    [Fact]
    public async Task ExecuteAsync_ProdWithoutAutoScaling_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("prod-no-scaling.json",
            environment: "prod",
            autoScaling: false);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Auto-scaling is recommended for production");
    }

    [Fact]
    public async Task ExecuteAsync_WithVeryLargeStorage_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("large-storage.json",
            attachmentStorageGB: 8000,
            rasterCacheGB: 5000);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Total storage allocation");
        _console.Output.Should().Contain("very large");
        _console.Output.Should().Contain("Verify");
        _console.Output.Should().Contain("intentional");
    }

    [Fact]
    public async Task ExecuteAsync_ProdWithSingleRegionReplication_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("prod-single-region.json",
            environment: "prod",
            storageReplication: "single-region");

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Cross-region replication recommended");
    }

    [Fact]
    public async Task ExecuteAsync_MultipleInstancesWithoutLoadBalancer_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("no-lb.json",
            instanceCount: 3,
            loadBalancer: false);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Load balancer recommended when running multiple compute instances");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutMonitoring_ShouldWarn()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("no-monitoring.json", includeMonitoring: false);

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(0);
        _console.Output.Should().Contain("Monitoring configuration not specified");
        _console.Output.Should().Contain("Observability");
        _console.Output.Should().Contain("recommended");
    }

    [Fact]
    public async Task ExecuteAsync_WithWarningsAsErrors_ShouldFailOnWarnings()
    {
        // Arrange
        var topologyFile = await CreateTopologyFileAsync("warnings.json",
            environment: "custom"); // This generates a warning

        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = topologyFile,
            WarningsAsErrors = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("warning(s)");
    }

    [Fact]
    public async Task ExecuteAsync_WithVerboseFlag_ShouldShowDetailedErrors()
    {
        // Arrange
        var command = new DeployValidateTopologyCommand(_console, _environment);
        var context = new CommandContext(Array.Empty<string>(), new MockRemainingArguments(), "deploy-validate-topology", null);
        var settings = new DeployValidateTopologyCommand.Settings
        {
            TopologyFile = "/nonexistent/topology.json",
            Verbose = true
        };

        // Act
        var result = await command.ExecuteAsync(context, settings);

        // Assert
        result.Should().Be(1);
        _console.Output.Should().Contain("Error:");
    }

    // Helper methods
    private async Task<string> CreateValidTopologyFileAsync(string filename)
    {
        return await CreateTopologyFileAsync(filename);
    }

    private async Task<string> CreateTopologyFileAsync(
        string filename,
        string cloudProvider = "aws",
        string region = "us-east-1",
        string environment = "dev",
        bool includeCompute = true,
        bool includeMonitoring = true,
        int instanceCount = 1,
        bool autoScaling = false,
        bool databaseHighAvailability = false,
        int databaseStorageGB = 20,
        bool loadBalancer = true,
        int attachmentStorageGB = 100,
        int rasterCacheGB = 500,
        string storageReplication = "single-region")
    {
        var topologyPath = Path.Combine(_workspaceDir.Path, filename);

        var topology = new
        {
            CloudProvider = cloudProvider,
            Region = region,
            Environment = environment,
            Database = new
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = "db.t4g.micro",
                StorageGB = databaseStorageGB,
                HighAvailability = databaseHighAvailability
            },
            Compute = includeCompute ? new
            {
                Type = "container",
                InstanceSize = "t3.medium",
                InstanceCount = instanceCount,
                AutoScaling = autoScaling
            } : null,
            Storage = new
            {
                Type = "s3",
                AttachmentStorageGB = attachmentStorageGB,
                RasterCacheGB = rasterCacheGB,
                Replication = storageReplication
            },
            Networking = new
            {
                LoadBalancer = loadBalancer,
                PublicAccess = true,
                VpnRequired = false
            },
            Monitoring = includeMonitoring ? new
            {
                Provider = "cloudwatch",
                EnableMetrics = true,
                EnableLogs = true,
                EnableTracing = false
            } : null,
            Features = new[] { "OGC WFS 2.0", "OGC WMS 1.3", "Vector Tiles" }
        };

        var json = JsonSerializer.Serialize(topology, JsonSerializerOptionsRegistry.WebIndented);
        await File.WriteAllTextAsync(topologyPath, json);

        return topologyPath;
    }
}
