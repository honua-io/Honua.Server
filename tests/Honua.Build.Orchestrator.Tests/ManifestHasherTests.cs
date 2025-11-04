using FluentAssertions;
using Xunit;

namespace Honua.Build.Orchestrator.Tests;

/// <summary>
/// Tests for ManifestHasher - ensures deterministic hash generation for build manifests.
/// Hashes are used to determine if a build already exists in the registry cache.
/// </summary>
[Trait("Category", "Unit")]
public class ManifestHasherTests
{
    private readonly ManifestHasher _hasher;

    public ManifestHasherTests()
    {
        _hasher = new ManifestHasher();
    }

    [Fact]
    public void ComputeHash_SameConfig_ReturnsSameHash()
    {
        // Arrange
        var manifest1 = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc", "Stac" }
        );
        var manifest2 = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc", "Stac" }
        );
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");

        // Act
        var hash1 = _hasher.ComputeHash(manifest1, target);
        var hash2 = _hasher.ComputeHash(manifest2, target);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(8); // Short hash (first 8 chars of SHA256)
        hash1.Should().MatchRegex("^[a-f0-9]{8}$"); // Hex format
    }

    [Fact]
    public void ComputeHash_DifferentModuleOrder_ReturnsSameHash()
    {
        // Arrange - Modules should be sorted before hashing for determinism
        var manifest1 = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Stac", "Core", "Ogc" } // Different order
        );
        var manifest2 = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc", "Stac" } // Sorted order
        );
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");

        // Act
        var hash1 = _hasher.ComputeHash(manifest1, target);
        var hash2 = _hasher.ComputeHash(manifest2, target);

        // Assert
        hash1.Should().Be(hash2, "module order should not affect hash");
    }

    [Fact]
    public void ComputeHash_DifferentCustomerId_ReturnsSameHash()
    {
        // Arrange - Customer ID should NOT be part of the hash
        // The hash identifies the BUILD configuration, not the deployment
        var manifest1 = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc" },
            customerId: "customer-123"
        );
        var manifest2 = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc" },
            customerId: "customer-456"
        );
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");

        // Act
        var hash1 = _hasher.ComputeHash(manifest1, target);
        var hash2 = _hasher.ComputeHash(manifest2, target);

        // Assert
        hash1.Should().Be(hash2, "customer ID should not affect build hash");
    }

    [Fact]
    public void ComputeHash_DifferentVersion_ReturnsDifferentHash()
    {
        // Arrange
        var manifest1 = CreateTestManifest(tier: "Pro", version: "1.0.0");
        var manifest2 = CreateTestManifest(tier: "Pro", version: "1.0.1");
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");

        // Act
        var hash1 = _hasher.ComputeHash(manifest1, target);
        var hash2 = _hasher.ComputeHash(manifest2, target);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentBuildTarget_ReturnsDifferentHash()
    {
        // Arrange
        var manifest = CreateTestManifest(tier: "Pro", version: "1.0.0");
        var targetArm = CreateBuildTarget("linux-arm64", "graviton3", "aws");
        var targetAmd = CreateBuildTarget("linux-amd64", "cascade-lake", "azure");

        // Act
        var hashArm = _hasher.ComputeHash(manifest, targetArm);
        var hashAmd = _hasher.ComputeHash(manifest, targetAmd);

        // Assert
        hashArm.Should().NotBe(hashAmd);
    }

    [Fact]
    public void ComputeHash_DifferentModules_ReturnsDifferentHash()
    {
        // Arrange
        var manifest1 = CreateTestManifest(modules: new[] { "Core", "Ogc" });
        var manifest2 = CreateTestManifest(modules: new[] { "Core", "Ogc", "Stac" });
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");

        // Act
        var hash1 = _hasher.ComputeHash(manifest1, target);
        var hash2 = _hasher.ComputeHash(manifest2, target);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void DetermineRequiredTier_AllCommunityModules_ReturnsCommunity()
    {
        // Arrange
        var manifest = CreateTestManifest(
            tier: null, // Not specified
            modules: new[] { "Core", "Ogc" } // All community modules
        );

        // Act
        var tier = _hasher.DetermineRequiredTier(manifest);

        // Assert
        tier.Should().Be("Community");
    }

    [Fact]
    public void DetermineRequiredTier_MixedModules_ReturnsHighestTier()
    {
        // Arrange
        var manifest = CreateTestManifest(
            tier: null,
            modules: new[] { "Core", "Ogc", "Serverless", "GeoArrow" }
            // Core/Ogc = Community, Serverless/GeoArrow = Pro
        );

        // Act
        var tier = _hasher.DetermineRequiredTier(manifest);

        // Assert
        tier.Should().Be("Pro", "highest tier required by any module should be returned");
    }

    [Fact]
    public void DetermineRequiredTier_ExplicitTierInManifest_ReturnsManifestTier()
    {
        // Arrange
        var manifest = CreateTestManifest(
            tier: "Enterprise", // Explicitly set
            modules: new[] { "Core", "Ogc" }
        );

        // Act
        var tier = _hasher.DetermineRequiredTier(manifest);

        // Assert
        tier.Should().Be("Enterprise");
    }

    [Fact]
    public void GenerateImageTag_ValidManifest_ReturnsCorrectFormat()
    {
        // Arrange
        var manifest = CreateTestManifest(
            tier: "Pro",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc", "Stac" }
        );
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");
        var hash = "a1b2c3d4";

        // Act
        var tag = _hasher.GenerateImageTag(manifest, target, hash);

        // Assert
        // Expected format: {version}-{tier}-{arch}-{hash}
        tag.Should().Be("1.0.0-pro-linux-arm64-a1b2c3d4");
        tag.Should().MatchRegex(@"^\d+\.\d+\.\d+-\w+-linux-(arm64|amd64)-[a-f0-9]{8}$");
    }

    [Fact]
    public void GenerateImageTag_CommunityTier_OmitsTierFromTag()
    {
        // Arrange
        var manifest = CreateTestManifest(
            tier: "Community",
            version: "1.0.0",
            modules: new[] { "Core", "Ogc" }
        );
        var target = CreateBuildTarget("linux-amd64", "cascade-lake", "azure");
        var hash = "x9y8z7w6";

        // Act
        var tag = _hasher.GenerateImageTag(manifest, target, hash);

        // Assert
        // Community builds omit tier for cleaner tags
        tag.Should().Be("1.0.0-linux-amd64-x9y8z7w6");
    }

    [Fact]
    public void GenerateImageTag_LatestVersion_IncludesLatestAlias()
    {
        // Arrange
        var manifest = CreateTestManifest(
            tier: "Pro",
            version: "latest",
            modules: new[] { "Core", "Ogc" }
        );
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");
        var hash = "abcd1234";

        // Act
        var tag = _hasher.GenerateImageTag(manifest, target, hash);

        // Assert
        tag.Should().Be("latest-pro-linux-arm64-abcd1234");
    }

    [Theory]
    [InlineData("linux-arm64", "graviton2", "neon")]
    [InlineData("linux-arm64", "graviton3", "neon")]
    [InlineData("linux-amd64", "cascade-lake", "avx512")]
    [InlineData("linux-amd64", "skylake", "avx2")]
    public void ComputeHash_IncludesCpuOptimizations(string arch, string cpuModel, string expectedOptimization)
    {
        // Arrange
        var manifest = CreateTestManifest(tier: "Pro", version: "1.0.0");
        var target = CreateBuildTarget(arch, cpuModel, "aws");

        // Act
        var hash1 = _hasher.ComputeHash(manifest, target);

        // Change CPU model to different optimization
        var differentTarget = CreateBuildTarget(arch, "generic", "aws");
        var hash2 = _hasher.ComputeHash(manifest, differentTarget);

        // Assert
        hash1.Should().NotBe(hash2, "CPU optimizations should affect hash");
    }

    [Fact]
    public void ComputeHash_NullOrEmptyModules_ThrowsArgumentException()
    {
        // Arrange
        var manifest = CreateTestManifest(modules: Array.Empty<string>());
        var target = CreateBuildTarget("linux-arm64", "graviton3", "aws");

        // Act & Assert
        var act = () => _hasher.ComputeHash(manifest, target);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one module*");
    }

    [Fact]
    public void ComputeHash_NullTarget_ThrowsArgumentNullException()
    {
        // Arrange
        var manifest = CreateTestManifest(tier: "Pro", version: "1.0.0");

        // Act & Assert
        var act = () => _hasher.ComputeHash(manifest, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // Helper methods to create test data
    private static BuildManifest CreateTestManifest(
        string? tier = "Pro",
        string version = "1.0.0",
        string[]? modules = null,
        string? customerId = null)
    {
        return new BuildManifest
        {
            Tier = tier,
            Version = version,
            Modules = modules?.ToList() ?? new List<string> { "Core", "Ogc" },
            CustomerId = customerId,
            EnableAot = true,
            EnableTrimming = true,
            BuildDate = DateTime.UtcNow
        };
    }

    private static BuildTarget CreateBuildTarget(
        string architecture,
        string cpuModel,
        string cloudProvider)
    {
        return new BuildTarget
        {
            Architecture = architecture,
            CpuModel = cpuModel,
            CloudProvider = cloudProvider,
            OptimizationLevel = cpuModel switch
            {
                "graviton2" or "graviton3" => "neon",
                "cascade-lake" => "avx512",
                "skylake" => "avx2",
                _ => "generic"
            }
        };
    }
}

// Placeholder classes - these would be in the actual implementation
public class ManifestHasher
{
    public string ComputeHash(BuildManifest manifest, BuildTarget target)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public string DetermineRequiredTier(BuildManifest manifest)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }

    public string GenerateImageTag(BuildManifest manifest, BuildTarget target, string hash)
    {
        throw new NotImplementedException("To be implemented in Honua.Build.Orchestrator project");
    }
}

public class BuildManifest
{
    public string? Tier { get; set; }
    public string Version { get; set; } = "1.0.0";
    public List<string> Modules { get; set; } = new();
    public string? CustomerId { get; set; }
    public bool EnableAot { get; set; }
    public bool EnableTrimming { get; set; }
    public DateTime BuildDate { get; set; }
    public List<RepositoryReference> Repositories { get; set; } = new();
}

public class BuildTarget
{
    public string Architecture { get; set; } = "linux-amd64";
    public string CpuModel { get; set; } = "generic";
    public string CloudProvider { get; set; } = "aws";
    public string OptimizationLevel { get; set; } = "generic";
}
