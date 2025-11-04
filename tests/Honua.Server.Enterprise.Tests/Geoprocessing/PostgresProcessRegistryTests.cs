using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Geoprocessing;
using Honua.Server.Enterprise.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Geoprocessing;

[Collection("SharedPostgres")]
public class PostgresProcessRegistryTests : IAsyncLifetime
{
    private readonly SharedPostgresFixture _fixture;
    private string _connectionString;
    private PostgresProcessRegistry _registry;

    public PostgresProcessRegistryTests(SharedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            throw new Xunit.SkipException("PostgreSQL test container is not available");
        }

        _connectionString = _fixture.ConnectionString;

        _registry = new PostgresProcessRegistry(
            _connectionString,
            NullLogger<PostgresProcessRegistry>.Instance);

        await TestDatabaseHelper.RunMigrationsAsync(_connectionString);
        await TestDatabaseHelper.CleanupAsync(_connectionString);
    }

    public async Task DisposeAsync()
    {
        if (_fixture.IsAvailable)
        {
            await TestDatabaseHelper.CleanupAsync(_connectionString);
        }
    }

    [Fact]
    public async Task RegisterProcessAsync_NewProcess_ShouldRegister()
    {
        // Arrange
        var process = CreateBufferProcessDefinition();

        // Act
        await _registry.RegisterProcessAsync(process);

        // Assert
        var retrieved = await _registry.GetProcessAsync(process.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(process.Id);
        retrieved.Title.Should().Be(process.Title);
        retrieved.Version.Should().Be(process.Version);
    }

    [Fact]
    public async Task RegisterProcessAsync_ExistingProcess_ShouldUpdate()
    {
        // Arrange
        var process = CreateBufferProcessDefinition();
        await _registry.RegisterProcessAsync(process);

        // Update the process
        var updatedProcess = new ProcessDefinition
        {
            Id = process.Id,
            Title = "Updated Buffer",
            Version = "2.0.0",
            Description = "Updated description",
            Category = process.Category,
            Keywords = process.Keywords,
            Inputs = process.Inputs,
            OutputFormats = process.OutputFormats,
            ExecutionConfig = process.ExecutionConfig,
            Enabled = process.Enabled,
            Links = process.Links
        };

        // Act
        await _registry.RegisterProcessAsync(updatedProcess);

        // Assert
        var retrieved = await _registry.GetProcessAsync(process.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Title.Should().Be("Updated Buffer");
        retrieved.Version.Should().Be("2.0.0");
        retrieved.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task GetProcessAsync_NonExistentProcess_ShouldReturnNull()
    {
        // Act
        var process = await _registry.GetProcessAsync("nonexistent");

        // Assert
        process.Should().BeNull();
    }

    [Fact]
    public async Task ListProcessesAsync_MultipleProcesses_ShouldReturnAll()
    {
        // Arrange
        var buffer = CreateBufferProcessDefinition();
        var intersection = CreateIntersectionProcessDefinition();

        await _registry.RegisterProcessAsync(buffer);
        await _registry.RegisterProcessAsync(intersection);

        // Act
        var processes = await _registry.ListProcessesAsync();

        // Assert
        processes.Should().HaveCount(2);
        processes.Should().Contain(p => p.Id == "buffer");
        processes.Should().Contain(p => p.Id == "intersection");
    }

    [Fact]
    public async Task ListProcessesAsync_DisabledProcess_ShouldNotInclude()
    {
        // Arrange
        var enabledProcess = CreateBufferProcessDefinition();
        var intersection = CreateIntersectionProcessDefinition();
        var disabledProcess = new ProcessDefinition
        {
            Id = intersection.Id,
            Title = intersection.Title,
            Version = intersection.Version,
            Description = intersection.Description,
            Category = intersection.Category,
            Keywords = intersection.Keywords,
            Inputs = intersection.Inputs,
            OutputFormats = intersection.OutputFormats,
            ExecutionConfig = intersection.ExecutionConfig,
            Enabled = false,
            Links = intersection.Links
        };

        await _registry.RegisterProcessAsync(enabledProcess);
        await _registry.RegisterProcessAsync(disabledProcess);

        // Act
        var processes = await _registry.ListProcessesAsync();

        // Assert
        processes.Should().HaveCount(1);
        processes.Should().Contain(p => p.Id == "buffer");
        processes.Should().NotContain(p => p.Id == "intersection");
    }

    [Fact]
    public async Task UnregisterProcessAsync_ExistingProcess_ShouldRemove()
    {
        // Arrange
        var process = CreateBufferProcessDefinition();
        await _registry.RegisterProcessAsync(process);

        // Act
        await _registry.UnregisterProcessAsync(process.Id);

        // Assert
        var retrieved = await _registry.GetProcessAsync(process.Id);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task IsAvailableAsync_RegisteredProcess_ShouldReturnTrue()
    {
        // Arrange
        var process = CreateBufferProcessDefinition();
        await _registry.RegisterProcessAsync(process);

        // Act
        var isAvailable = await _registry.IsAvailableAsync(process.Id);

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_UnregisteredProcess_ShouldReturnFalse()
    {
        // Act
        var isAvailable = await _registry.IsAvailableAsync("nonexistent");

        // Assert
        isAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ReloadAsync_ShouldRefreshCache()
    {
        // Arrange
        var process = CreateBufferProcessDefinition();
        await _registry.RegisterProcessAsync(process);

        // Act
        await _registry.ReloadAsync();

        // Assert - Process should be in cache
        var isAvailable = await _registry.IsAvailableAsync(process.Id);
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessDefinition_SerializationRoundTrip_ShouldPreserveData()
    {
        // Arrange
        var process = CreateComplexProcessDefinition();

        // Act
        await _registry.RegisterProcessAsync(process);
        var retrieved = await _registry.GetProcessAsync(process.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(process.Id);
        retrieved.Inputs.Should().HaveCount(process.Inputs.Count);
        retrieved.ExecutionConfig.SupportedTiers.Should().BeEquivalentTo(process.ExecutionConfig.SupportedTiers);
        retrieved.Keywords.Should().BeEquivalentTo(process.Keywords);
        retrieved.OutputFormats.Should().BeEquivalentTo(process.OutputFormats);
        retrieved.Links.Should().HaveCount(process.Links.Count);
    }

    private static ProcessDefinition CreateBufferProcessDefinition()
    {
        return new ProcessDefinition
        {
            Id = "buffer",
            Title = "Buffer",
            Description = "Creates a buffer around input geometries",
            Version = "1.0.0",
            Category = "vector",
            Keywords = new List<string> { "buffer", "proximity", "spatial" },
            Inputs = new List<ProcessParameter>
            {
                new()
                {
                    Name = "geometry",
                    Title = "Input Geometry",
                    Description = "Geometry to buffer",
                    Type = "geometry",
                    Required = true,
                    GeometryTypes = new List<string> { "Point", "LineString", "Polygon" }
                },
                new()
                {
                    Name = "distance",
                    Title = "Buffer Distance",
                    Description = "Distance in meters",
                    Type = "number",
                    Required = true,
                    MinValue = 0,
                    MaxValue = 100000
                },
                new()
                {
                    Name = "segments",
                    Title = "Segments",
                    Description = "Number of segments per quarter circle",
                    Type = "number",
                    Required = false,
                    DefaultValue = 8,
                    MinValue = 4,
                    MaxValue = 64
                }
            },
            Output = new ProcessOutput
            {
                Type = "geometry",
                Description = "Buffered geometry",
                GeometryType = "Polygon"
            },
            OutputFormats = new List<string> { "geojson", "wkt", "geoparquet" },
            ExecutionConfig = new ProcessExecutionConfig
            {
                SupportedTiers = new List<ProcessExecutionTier>
                {
                    ProcessExecutionTier.NTS,
                    ProcessExecutionTier.PostGIS,
                    ProcessExecutionTier.CloudBatch
                },
                DefaultTier = ProcessExecutionTier.NTS,
                DefaultTimeoutSeconds = 300,
                MaxInputSizeMB = 100,
                EstimatedDurationSeconds = 5,
                Thresholds = new TierThresholds
                {
                    NtsMaxFeatures = 1000,
                    PostGisMaxFeatures = 100000,
                    NtsMaxInputMB = 10,
                    PostGisMaxInputMB = 500
                }
            },
            Links = new List<ProcessLink>
            {
                new()
                {
                    Rel = "documentation",
                    Href = "https://docs.honua.io/processes/buffer",
                    Title = "Buffer Documentation",
                    Type = "text/html"
                }
            },
            Enabled = true
        };
    }

    private static ProcessDefinition CreateIntersectionProcessDefinition()
    {
        return new ProcessDefinition
        {
            Id = "intersection",
            Title = "Intersection",
            Description = "Computes the geometric intersection of two geometries",
            Version = "1.0.0",
            Category = "vector",
            Keywords = new List<string> { "intersection", "overlay", "spatial" },
            Inputs = new List<ProcessParameter>
            {
                new()
                {
                    Name = "geometry1",
                    Title = "First Geometry",
                    Type = "geometry",
                    Required = true
                },
                new()
                {
                    Name = "geometry2",
                    Title = "Second Geometry",
                    Type = "geometry",
                    Required = true
                }
            },
            Output = new ProcessOutput
            {
                Type = "geometry",
                Description = "Intersection result"
            },
            OutputFormats = new List<string> { "geojson" },
            ExecutionConfig = new ProcessExecutionConfig
            {
                SupportedTiers = new List<ProcessExecutionTier>
                {
                    ProcessExecutionTier.NTS,
                    ProcessExecutionTier.PostGIS
                },
                DefaultTier = ProcessExecutionTier.NTS
            },
            Enabled = true
        };
    }

    private static ProcessDefinition CreateComplexProcessDefinition()
    {
        return new ProcessDefinition
        {
            Id = "complex-analysis",
            Title = "Complex Spatial Analysis",
            Description = "A complex geoprocessing operation for testing serialization",
            Version = "2.5.1",
            Category = "analysis",
            Keywords = new List<string> { "analysis", "spatial", "advanced", "complex" },
            Inputs = new List<ProcessParameter>
            {
                new()
                {
                    Name = "input_features",
                    Title = "Input Features",
                    Description = "Input feature collection",
                    Type = "featurecollection",
                    Required = true,
                    GeometryTypes = new List<string> { "Polygon", "MultiPolygon" },
                    AllowedSrids = new List<int> { 4326, 3857 }
                },
                new()
                {
                    Name = "analysis_type",
                    Title = "Analysis Type",
                    Description = "Type of analysis to perform",
                    Type = "string",
                    Required = true,
                    AllowedValues = new List<object> { "density", "cluster", "hotspot" }
                },
                new()
                {
                    Name = "threshold",
                    Title = "Threshold",
                    Type = "number",
                    Required = false,
                    DefaultValue = 0.5,
                    MinValue = 0.0,
                    MaxValue = 1.0
                }
            },
            Output = new ProcessOutput
            {
                Type = "featurecollection",
                Description = "Analysis results",
                Metadata = new Dictionary<string, object>
                {
                    ["includesStatistics"] = true,
                    ["coordinateSystem"] = "WGS84"
                }
            },
            OutputFormats = new List<string> { "geojson", "geoparquet", "shapefile" },
            ExecutionConfig = new ProcessExecutionConfig
            {
                SupportedTiers = new List<ProcessExecutionTier>
                {
                    ProcessExecutionTier.PostGIS,
                    ProcessExecutionTier.CloudBatch
                },
                DefaultTier = ProcessExecutionTier.PostGIS,
                DefaultTimeoutSeconds = 600,
                MaxTimeoutSeconds = 3600,
                MaxInputSizeMB = 500,
                MaxFeatures = 1000000,
                EstimatedDurationSeconds = 120,
                EstimatedMemoryMB = 2048,
                Thresholds = new TierThresholds
                {
                    PostGisMaxFeatures = 100000,
                    PostGisMaxInputMB = 200,
                    PostGisMaxDurationSeconds = 300
                }
            },
            Links = new List<ProcessLink>
            {
                new()
                {
                    Rel = "documentation",
                    Href = "https://docs.honua.io/processes/complex-analysis",
                    Title = "Documentation",
                    Type = "text/html"
                },
                new()
                {
                    Rel = "example",
                    Href = "https://examples.honua.io/complex-analysis.json",
                    Title = "Example Request",
                    Type = "application/json"
                }
            },
            Metadata = new Dictionary<string, object>
            {
                ["author"] = "Honua Team",
                ["version_date"] = "2025-01-15",
                ["experimental"] = false
            },
            Enabled = true
        };
    }
}
