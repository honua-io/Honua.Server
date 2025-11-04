using System.Text.Json.Serialization;

namespace Honua.Build.Orchestrator.Models;

/// <summary>
/// Root manifest defining a custom GIS server deployment build.
/// </summary>
public sealed class BuildManifest
{
    /// <summary>
    /// Unique identifier for this build configuration.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Semantic version of the build manifest schema.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }

    /// <summary>
    /// Human-readable name for this deployment.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Optional description of the deployment configuration.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// List of git repositories to clone and include in the build.
    /// </summary>
    [JsonPropertyName("repositories")]
    public required List<RepositoryReference> Repositories { get; set; }

    /// <summary>
    /// List of module names to include from the cloned repositories.
    /// </summary>
    [JsonPropertyName("modules")]
    public required List<string> Modules { get; set; }

    /// <summary>
    /// Cloud platforms and architectures to build for.
    /// </summary>
    [JsonPropertyName("targets")]
    public required List<CloudTarget> Targets { get; set; }

    /// <summary>
    /// Deployment configuration settings.
    /// </summary>
    [JsonPropertyName("deployment")]
    public DeploymentConfig? Deployment { get; set; }

    /// <summary>
    /// Global build optimizations applied to all targets.
    /// </summary>
    [JsonPropertyName("optimizations")]
    public BuildOptimizations? Optimizations { get; set; }

    /// <summary>
    /// Custom MSBuild properties to apply during build.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, string>? Properties { get; set; }

    /// <summary>
    /// Timestamp when this manifest was created or last modified.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Reference to a git repository containing projects to include.
/// </summary>
public sealed class RepositoryReference
{
    /// <summary>
    /// Logical name for this repository (used for directory naming).
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Git clone URL (HTTPS or SSH).
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// Branch, tag, or commit SHA to checkout.
    /// </summary>
    [JsonPropertyName("ref")]
    public string Ref { get; set; } = "main";

    /// <summary>
    /// Access level: "public" or "private".
    /// </summary>
    [JsonPropertyName("access")]
    public string Access { get; set; } = "public";

    /// <summary>
    /// Name of environment variable containing PAT token for private repos.
    /// </summary>
    [JsonPropertyName("credentials")]
    public string? Credentials { get; set; }

    /// <summary>
    /// Relative paths to projects within this repository to include.
    /// </summary>
    [JsonPropertyName("projects")]
    public List<string>? Projects { get; set; }
}

/// <summary>
/// Cloud platform target with architecture and optimization settings.
/// </summary>
public sealed class CloudTarget
{
    /// <summary>
    /// Unique identifier for this target (e.g., "aws-graviton-prod").
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Cloud provider: "aws", "azure", "gcp", "on-premises".
    /// </summary>
    [JsonPropertyName("provider")]
    public required string Provider { get; set; }

    /// <summary>
    /// Compute type: "graviton2", "graviton3", "ampere", "tau-t2a", "x86_64".
    /// </summary>
    [JsonPropertyName("compute")]
    public required string Compute { get; set; }

    /// <summary>
    /// .NET runtime identifier (e.g., "linux-arm64", "linux-x64", "win-x64").
    /// </summary>
    [JsonPropertyName("architecture")]
    public required string Architecture { get; set; }

    /// <summary>
    /// Platform-specific optimization settings.
    /// </summary>
    [JsonPropertyName("optimizations")]
    public BuildOptimizations? Optimizations { get; set; }

    /// <summary>
    /// Container registry configuration for this target.
    /// </summary>
    [JsonPropertyName("registry")]
    public ContainerRegistry? Registry { get; set; }

    /// <summary>
    /// Expected deployment tier for cost estimation.
    /// </summary>
    [JsonPropertyName("tier")]
    public string? Tier { get; set; }
}

/// <summary>
/// Build optimization flags and compiler settings.
/// </summary>
public sealed class BuildOptimizations
{
    /// <summary>
    /// Enable Profile-Guided Optimization.
    /// </summary>
    [JsonPropertyName("pgo")]
    public bool Pgo { get; set; } = true;

    /// <summary>
    /// Enable dynamic PGO for AOT builds.
    /// </summary>
    [JsonPropertyName("dynamicPgo")]
    public bool DynamicPgo { get; set; } = true;

    /// <summary>
    /// Enable tiered compilation.
    /// </summary>
    [JsonPropertyName("tieredCompilation")]
    public bool TieredCompilation { get; set; } = true;

    /// <summary>
    /// Vectorization instruction set: "neon", "avx2", "avx512", null.
    /// </summary>
    [JsonPropertyName("vectorization")]
    public string? Vectorization { get; set; }

    /// <summary>
    /// Enable ready-to-run compilation.
    /// </summary>
    [JsonPropertyName("readyToRun")]
    public bool ReadyToRun { get; set; } = true;

    /// <summary>
    /// Trim unused code for smaller binaries.
    /// </summary>
    [JsonPropertyName("trim")]
    public bool Trim { get; set; } = true;

    /// <summary>
    /// Size optimization level: "speed", "size", "balanced".
    /// </summary>
    [JsonPropertyName("optimizationPreference")]
    public string OptimizationPreference { get; set; } = "speed";

    /// <summary>
    /// Custom IL compiler options.
    /// </summary>
    [JsonPropertyName("ilcOptions")]
    public List<string>? IlcOptions { get; set; }
}

/// <summary>
/// Container registry configuration for pushing images.
/// </summary>
public sealed class ContainerRegistry
{
    /// <summary>
    /// Registry URL (e.g., "123456789.dkr.ecr.us-east-1.amazonaws.com").
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }

    /// <summary>
    /// Image repository name.
    /// </summary>
    [JsonPropertyName("repository")]
    public required string Repository { get; set; }

    /// <summary>
    /// Tag strategy: "manifest-hash", "semantic-version", "commit-sha".
    /// </summary>
    [JsonPropertyName("tagStrategy")]
    public string TagStrategy { get; set; } = "manifest-hash";

    /// <summary>
    /// Additional static tags to apply (e.g., ["latest", "prod"]).
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Name of environment variable containing registry credentials.
    /// </summary>
    [JsonPropertyName("credentialsVariable")]
    public string? CredentialsVariable { get; set; }
}

/// <summary>
/// Deployment configuration and metadata.
/// </summary>
public sealed class DeploymentConfig
{
    /// <summary>
    /// Target environment: "development", "staging", "production".
    /// </summary>
    [JsonPropertyName("environment")]
    public required string Environment { get; set; }

    /// <summary>
    /// Geographic region for deployment.
    /// </summary>
    [JsonPropertyName("region")]
    public string? Region { get; set; }

    /// <summary>
    /// Enable build caching for faster incremental builds.
    /// </summary>
    [JsonPropertyName("enableCache")]
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// Cache backend: "local", "redis", "s3".
    /// </summary>
    [JsonPropertyName("cacheBackend")]
    public string CacheBackend { get; set; } = "local";

    /// <summary>
    /// Cache connection string or path.
    /// </summary>
    [JsonPropertyName("cacheConnection")]
    public string? CacheConnection { get; set; }

    /// <summary>
    /// Maximum parallel build jobs.
    /// </summary>
    [JsonPropertyName("parallelism")]
    public int Parallelism { get; set; } = 4;

    /// <summary>
    /// Resource limits for build containers.
    /// </summary>
    [JsonPropertyName("resources")]
    public BuildResources? Resources { get; set; }

    /// <summary>
    /// Estimated cost per build.
    /// </summary>
    [JsonPropertyName("costEstimate")]
    public CostEstimate? CostEstimate { get; set; }
}

/// <summary>
/// Resource limits for build operations.
/// </summary>
public sealed class BuildResources
{
    /// <summary>
    /// CPU limit in cores.
    /// </summary>
    [JsonPropertyName("cpuLimit")]
    public double? CpuLimit { get; set; }

    /// <summary>
    /// Memory limit in GB.
    /// </summary>
    [JsonPropertyName("memoryLimit")]
    public double? MemoryLimit { get; set; }

    /// <summary>
    /// Disk space limit in GB.
    /// </summary>
    [JsonPropertyName("diskLimit")]
    public double? DiskLimit { get; set; }

    /// <summary>
    /// Build timeout in minutes.
    /// </summary>
    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 60;
}

/// <summary>
/// Cost estimation for build and deployment.
/// </summary>
public sealed class CostEstimate
{
    /// <summary>
    /// Estimated compute cost per build in USD.
    /// </summary>
    [JsonPropertyName("buildCost")]
    public decimal BuildCost { get; set; }

    /// <summary>
    /// Estimated storage cost per month in USD.
    /// </summary>
    [JsonPropertyName("storageCost")]
    public decimal StorageCost { get; set; }

    /// <summary>
    /// Estimated bandwidth cost per month in USD.
    /// </summary>
    [JsonPropertyName("bandwidthCost")]
    public decimal BandwidthCost { get; set; }

    /// <summary>
    /// Currency code (default: USD).
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";
}

/// <summary>
/// Build target result information.
/// </summary>
public sealed class BuildTarget
{
    /// <summary>
    /// Target ID from the manifest.
    /// </summary>
    public required string TargetId { get; set; }

    /// <summary>
    /// Path to the built binary.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Docker image tag if containerized.
    /// </summary>
    public string? ImageTag { get; set; }

    /// <summary>
    /// Build success status.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if build failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Build duration in seconds.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Output binary size in bytes.
    /// </summary>
    public long? BinarySize { get; set; }
}
