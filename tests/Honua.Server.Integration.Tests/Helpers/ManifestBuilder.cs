using Honua.Build.Orchestrator.Models;

namespace Honua.Server.Integration.Tests.Helpers;

/// <summary>
/// Fluent builder for creating test build manifests.
/// </summary>
public class ManifestBuilder
{
    private string _id = Guid.NewGuid().ToString("N")[..12];
    private string _name = "Test Build";
    private string _version = "1.0.0";
    private readonly List<string> _modules = new();
    private readonly List<CloudTarget> _targets = new();
    private readonly Dictionary<string, string> _properties = new();
    private BuildOptimizations? _optimizations;
    private DeploymentConfig? _deployment;

    private ManifestBuilder()
    {
    }

    /// <summary>
    /// Creates a new manifest builder with default values.
    /// </summary>
    public static ManifestBuilder CreateDefault()
    {
        return new ManifestBuilder()
            .WithModule("Core")
            .WithModule("WMS");
    }

    /// <summary>
    /// Sets the manifest ID.
    /// </summary>
    public ManifestBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the manifest name.
    /// </summary>
    public ManifestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the version.
    /// </summary>
    public ManifestBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }

    /// <summary>
    /// Adds a module to include in the build.
    /// </summary>
    public ManifestBuilder WithModule(string module)
    {
        _modules.Add(module);
        return this;
    }

    /// <summary>
    /// Adds a cloud target.
    /// </summary>
    public ManifestBuilder WithTarget(string id, string provider, string compute, string architecture)
    {
        _targets.Add(new CloudTarget
        {
            Id = id,
            Provider = provider,
            Compute = compute,
            Architecture = architecture
        });
        return this;
    }

    /// <summary>
    /// Adds a custom MSBuild property.
    /// </summary>
    public ManifestBuilder WithProperty(string key, string value)
    {
        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Sets build optimizations.
    /// </summary>
    public ManifestBuilder WithOptimizations(BuildOptimizations optimizations)
    {
        _optimizations = optimizations;
        return this;
    }

    /// <summary>
    /// Sets deployment configuration.
    /// </summary>
    public ManifestBuilder WithDeployment(DeploymentConfig deployment)
    {
        _deployment = deployment;
        return this;
    }

    /// <summary>
    /// Builds the manifest.
    /// </summary>
    public BuildManifest Build()
    {
        return new BuildManifest
        {
            Id = _id,
            Version = _version,
            Name = _name,
            Repositories = new List<RepositoryReference>
            {
                new()
                {
                    Name = "honua-server",
                    Url = "https://github.com/honua-io/honua-server",
                    Ref = "main",
                    Access = "public"
                }
            },
            Modules = _modules,
            Targets = _targets,
            Properties = _properties.Count > 0 ? _properties : null,
            Optimizations = _optimizations,
            Deployment = _deployment ?? new DeploymentConfig
            {
                Environment = "test",
                EnableCache = true,
                Parallelism = 2
            },
            CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Builder for creating test cloud targets.
/// </summary>
public class CloudTargetBuilder
{
    private string _id = "default-target";
    private string _provider = "aws";
    private string _compute = "graviton3";
    private string _architecture = "linux-arm64";
    private BuildOptimizations? _optimizations;
    private ContainerRegistry? _registry;

    public static CloudTargetBuilder Create() => new();

    public CloudTargetBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public CloudTargetBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    public CloudTargetBuilder WithCompute(string compute)
    {
        _compute = compute;
        return this;
    }

    public CloudTargetBuilder WithArchitecture(string architecture)
    {
        _architecture = architecture;
        return this;
    }

    public CloudTargetBuilder WithOptimizations(BuildOptimizations optimizations)
    {
        _optimizations = optimizations;
        return this;
    }

    public CloudTargetBuilder WithRegistry(ContainerRegistry registry)
    {
        _registry = registry;
        return this;
    }

    public CloudTarget Build()
    {
        return new CloudTarget
        {
            Id = _id,
            Provider = _provider,
            Compute = _compute,
            Architecture = _architecture,
            Optimizations = _optimizations,
            Registry = _registry
        };
    }
}

/// <summary>
/// Builder for creating test optimizations.
/// </summary>
public class OptimizationsBuilder
{
    private bool _pgo = true;
    private bool _dynamicPgo = true;
    private bool _tieredCompilation = true;
    private bool _readyToRun = true;
    private bool _trim = true;
    private string? _vectorization;
    private string _optimizationPreference = "speed";

    public static OptimizationsBuilder Create() => new();

    public OptimizationsBuilder ForSpeed()
    {
        _optimizationPreference = "speed";
        _pgo = true;
        _readyToRun = true;
        return this;
    }

    public OptimizationsBuilder ForSize()
    {
        _optimizationPreference = "size";
        _trim = true;
        return this;
    }

    public OptimizationsBuilder WithVectorization(string vectorization)
    {
        _vectorization = vectorization;
        return this;
    }

    public BuildOptimizations Build()
    {
        return new BuildOptimizations
        {
            Pgo = _pgo,
            DynamicPgo = _dynamicPgo,
            TieredCompilation = _tieredCompilation,
            ReadyToRun = _readyToRun,
            Trim = _trim,
            Vectorization = _vectorization,
            OptimizationPreference = _optimizationPreference
        };
    }
}
