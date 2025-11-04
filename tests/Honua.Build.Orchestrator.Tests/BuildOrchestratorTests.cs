using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Build.Orchestrator.Tests;

/// <summary>
/// Tests for BuildOrchestrator - the main orchestration service that coordinates
/// cross-repo builds with AOT compilation for multiple cloud targets.
/// </summary>
[Trait("Category", "Unit")]
public class BuildOrchestratorTests
{
    private readonly Mock<IGitRepositoryService> _mockGitService;
    private readonly Mock<ISolutionGenerator> _mockSolutionGenerator;
    private readonly Mock<IDotNetBuilder> _mockDotNetBuilder;
    private readonly Mock<IManifestHasher> _mockHasher;
    private readonly Mock<IRegistryCacheChecker> _mockCacheChecker;
    private readonly Mock<IBuildDeliveryService> _mockDeliveryService;
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly BuildOrchestrator _orchestrator;

    public BuildOrchestratorTests()
    {
        _mockGitService = new Mock<IGitRepositoryService>();
        _mockSolutionGenerator = new Mock<ISolutionGenerator>();
        _mockDotNetBuilder = new Mock<IDotNetBuilder>();
        _mockHasher = new Mock<IManifestHasher>();
        _mockCacheChecker = new Mock<IRegistryCacheChecker>();
        _mockDeliveryService = new Mock<IBuildDeliveryService>();
        _mockFileSystem = new Mock<IFileSystem>();

        _orchestrator = new BuildOrchestrator(
            _mockGitService.Object,
            _mockSolutionGenerator.Object,
            _mockDotNetBuilder.Object,
            _mockHasher.Object,
            _mockCacheChecker.Object,
            _mockDeliveryService.Object,
            _mockFileSystem.Object
        );
    }

    [Fact]
    public async Task CloneRepositories_PublicRepo_ClonesSuccessfully()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            Modules = new List<string> { "Core", "Ogc" },
            Repositories = new List<RepositoryReference>
            {
                new() { Url = "https://github.com/honua/core.git", Branch = "main", IsPublic = true }
            }
        };

        var workingDir = "/tmp/build-workspace-123";

        _mockFileSystem
            .Setup(x => x.CreateTempDirectory())
            .Returns(workingDir);

        _mockGitService
            .Setup(x => x.CloneAsync(
                "https://github.com/honua/core.git",
                It.IsAny<string>(),
                "main",
                null))
            .ReturnsAsync(new CloneResult { Success = true, LocalPath = $"{workingDir}/core" });

        // Act
        var result = await _orchestrator.CloneRepositoriesAsync(manifest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ClonedRepositories.Should().HaveCount(1);
        result.WorkspaceDirectory.Should().Be(workingDir);

        _mockGitService.Verify(
            x => x.CloneAsync(
                "https://github.com/honua/core.git",
                It.IsAny<string>(),
                "main",
                null),
            Times.Once
        );
    }

    [Fact]
    public async Task CloneRepositories_PrivateRepo_UsesPat()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            Modules = new List<string> { "Enterprise" },
            Repositories = new List<RepositoryReference>
            {
                new()
                {
                    Url = "https://github.com/honua/enterprise.git",
                    Branch = "main",
                    IsPublic = false,
                    PersonalAccessToken = "ghp_privatetoken123"
                }
            }
        };

        _mockFileSystem.Setup(x => x.CreateTempDirectory()).Returns("/tmp/workspace");

        _mockGitService
            .Setup(x => x.CloneAsync(
                "https://github.com/honua/enterprise.git",
                It.IsAny<string>(),
                "main",
                "ghp_privatetoken123"))
            .ReturnsAsync(new CloneResult { Success = true });

        // Act
        var result = await _orchestrator.CloneRepositoriesAsync(manifest);

        // Assert
        result.Success.Should().BeTrue();

        _mockGitService.Verify(
            x => x.CloneAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                "ghp_privatetoken123"),
            Times.Once
        );
    }

    [Fact]
    public async Task GenerateSolution_ValidManifest_CreatesSln()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            Tier = "Pro",
            Modules = new List<string> { "Core", "Ogc", "Stac" }
        };

        var clonedRepos = new List<string>
        {
            "/workspace/honua-core",
            "/workspace/honua-ogc",
            "/workspace/honua-stac"
        };

        _mockSolutionGenerator
            .Setup(x => x.GenerateSolutionAsync(manifest, clonedRepos))
            .ReturnsAsync(new SolutionGenerationResult
            {
                Success = true,
                SolutionPath = "/workspace/Honua.Pro.sln",
                ProjectReferences = new List<string>
                {
                    "honua-core/Honua.Server.Core.csproj",
                    "honua-ogc/Honua.Server.Ogc.csproj",
                    "honua-stac/Honua.Server.Stac.csproj"
                }
            });

        // Act
        var result = await _orchestrator.GenerateSolutionAsync(manifest, clonedRepos);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SolutionPath.Should().EndWith(".sln");
        result.ProjectReferences.Should().HaveCount(3);

        _mockSolutionGenerator.Verify(
            x => x.GenerateSolutionAsync(manifest, clonedRepos),
            Times.Once
        );
    }

    [Fact]
    public async Task BuildForCloudTarget_Graviton_UsesNeonOptimization()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            Tier = "Pro",
            EnableAot = true
        };

        var target = new BuildTarget
        {
            Architecture = "linux-arm64",
            CpuModel = "graviton3",
            CloudProvider = "aws",
            OptimizationLevel = "neon"
        };

        var solutionPath = "/workspace/Honua.Pro.sln";

        _mockDotNetBuilder
            .Setup(x => x.PublishAsync(
                solutionPath,
                It.Is<BuildOptions>(opts =>
                    opts.Architecture == "linux-arm64" &&
                    opts.EnableAot == true &&
                    opts.CpuOptimization == "neon")))
            .ReturnsAsync(new PublishResult
            {
                Success = true,
                OutputPath = "/workspace/publish/linux-arm64",
                BinarySize = 45_000_000, // 45 MB with AOT
                BuildDurationSeconds = 180
            });

        // Act
        var result = await _orchestrator.BuildForTargetAsync(manifest, target, solutionPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Contain("linux-arm64");
        result.BinarySize.Should().BeLessThan(50_000_000); // Verify small AOT binary

        _mockDotNetBuilder.Verify(
            x => x.PublishAsync(
                solutionPath,
                It.Is<BuildOptions>(opts => opts.CpuOptimization == "neon")),
            Times.Once
        );
    }

    [Fact]
    public async Task BuildForCloudTarget_Intel_UsesAvx512Optimization()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            Tier = "Pro",
            EnableAot = true
        };

        var target = new BuildTarget
        {
            Architecture = "linux-amd64",
            CpuModel = "cascade-lake",
            CloudProvider = "azure",
            OptimizationLevel = "avx512"
        };

        var solutionPath = "/workspace/Honua.Pro.sln";

        _mockDotNetBuilder
            .Setup(x => x.PublishAsync(
                solutionPath,
                It.Is<BuildOptions>(opts =>
                    opts.Architecture == "linux-amd64" &&
                    opts.CpuOptimization == "avx512")))
            .ReturnsAsync(new PublishResult
            {
                Success = true,
                OutputPath = "/workspace/publish/linux-amd64",
                BinarySize = 48_000_000
            });

        // Act
        var result = await _orchestrator.BuildForTargetAsync(manifest, target, solutionPath);

        // Assert
        result.Success.Should().BeTrue();

        _mockDotNetBuilder.Verify(
            x => x.PublishAsync(
                It.IsAny<string>(),
                It.Is<BuildOptions>(opts => opts.CpuOptimization == "avx512")),
            Times.Once
        );
    }

    [Fact]
    public async Task BuildForCloudTarget_AotEnabled_SetsCorrectFlags()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            EnableAot = true,
            EnableTrimming = true
        };

        var target = new BuildTarget { Architecture = "linux-arm64" };
        var solutionPath = "/workspace/Honua.sln";

        _mockDotNetBuilder
            .Setup(x => x.PublishAsync(solutionPath, It.IsAny<BuildOptions>()))
            .ReturnsAsync(new PublishResult { Success = true });

        // Act
        await _orchestrator.BuildForTargetAsync(manifest, target, solutionPath);

        // Assert
        _mockDotNetBuilder.Verify(
            x => x.PublishAsync(
                solutionPath,
                It.Is<BuildOptions>(opts =>
                    opts.EnableAot == true &&
                    opts.EnableTrimming == true &&
                    opts.InvariantGlobalization == true)),
            Times.Once
        );
    }

    [Fact]
    public async Task OrchestrateBuild_EndToEnd_ExecutesAllSteps()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Version = "1.0.0",
            Tier = "Pro",
            Modules = new List<string> { "Core", "Ogc" },
            CustomerId = "customer-123",
            Repositories = new List<RepositoryReference>
            {
                new() { Url = "https://github.com/honua/core.git", Branch = "main", IsPublic = true }
            }
        };

        var target = new BuildTarget
        {
            Architecture = "linux-arm64",
            CpuModel = "graviton3",
            CloudProvider = "aws"
        };

        var imageTag = "1.0.0-pro-linux-arm64-abc123";

        // Setup all mocks for end-to-end flow
        _mockFileSystem.Setup(x => x.CreateTempDirectory()).Returns("/workspace");

        _mockGitService
            .Setup(x => x.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CloneResult { Success = true, LocalPath = "/workspace/core" });

        _mockSolutionGenerator
            .Setup(x => x.GenerateSolutionAsync(It.IsAny<BuildManifest>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new SolutionGenerationResult
            {
                Success = true,
                SolutionPath = "/workspace/Honua.sln"
            });

        _mockHasher
            .Setup(x => x.ComputeHash(manifest, target))
            .Returns("abc123");

        _mockHasher
            .Setup(x => x.GenerateImageTag(manifest, target, "abc123"))
            .Returns(imageTag);

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), imageTag))
            .ReturnsAsync(new CacheCheckResult { Exists = false }); // Cache miss

        _mockDotNetBuilder
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<BuildOptions>()))
            .ReturnsAsync(new PublishResult
            {
                Success = true,
                OutputPath = "/workspace/publish",
                BuildDurationSeconds = 120
            });

        _mockDeliveryService
            .Setup(x => x.DeliverBuildAsync(
                It.IsAny<BuildManifest>(),
                It.IsAny<BuildTarget>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new DeliveryResult { Success = true });

        // Act
        var result = await _orchestrator.OrchestrateFullBuildAsync(manifest, target);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ImageTag.Should().Be(imageTag);

        // Verify all steps were executed in order
        _mockGitService.Verify(x => x.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockSolutionGenerator.Verify(x => x.GenerateSolutionAsync(It.IsAny<BuildManifest>(), It.IsAny<List<string>>()), Times.Once);
        _mockHasher.Verify(x => x.ComputeHash(manifest, target), Times.Once);
        _mockCacheChecker.Verify(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), imageTag), Times.Once);
        _mockDotNetBuilder.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<BuildOptions>()), Times.Once);
        _mockDeliveryService.Verify(x => x.DeliverBuildAsync(It.IsAny<BuildManifest>(), It.IsAny<BuildTarget>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task OrchestrateBuild_CacheHit_SkipsBuild()
    {
        // Arrange
        var manifest = new BuildManifest { Version = "1.0.0" };
        var target = new BuildTarget { Architecture = "linux-arm64" };
        var imageTag = "1.0.0-linux-arm64-cached";

        _mockHasher.Setup(x => x.ComputeHash(manifest, target)).Returns("cached");
        _mockHasher.Setup(x => x.GenerateImageTag(manifest, target, "cached")).Returns(imageTag);

        // Cache hit
        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), imageTag))
            .ReturnsAsync(new CacheCheckResult { Exists = true, CacheHit = true });

        _mockDeliveryService
            .Setup(x => x.DeliverBuildAsync(It.IsAny<BuildManifest>(), It.IsAny<BuildTarget>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new DeliveryResult { Success = true, CacheHit = true });

        // Act
        var result = await _orchestrator.OrchestrateFullBuildAsync(manifest, target);

        // Assert
        result.Success.Should().BeTrue();
        result.CacheHit.Should().BeTrue();

        // Verify build was skipped
        _mockGitService.Verify(x => x.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockDotNetBuilder.Verify(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<BuildOptions>()), Times.Never);
    }

    [Fact]
    public async Task OrchestrateBuild_CloneFails_ReturnsFailure()
    {
        // Arrange
        var manifest = new BuildManifest
        {
            Repositories = new List<RepositoryReference>
            {
                new() { Url = "https://github.com/invalid/repo.git", Branch = "main" }
            }
        };
        var target = new BuildTarget { Architecture = "linux-arm64" };

        _mockFileSystem.Setup(x => x.CreateTempDirectory()).Returns("/workspace");

        _mockGitService
            .Setup(x => x.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CloneResult
            {
                Success = false,
                ErrorMessage = "Repository not found"
            });

        // Act
        var result = await _orchestrator.OrchestrateFullBuildAsync(manifest, target);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Repository not found");

        // Verify subsequent steps were not executed
        _mockSolutionGenerator.Verify(x => x.GenerateSolutionAsync(It.IsAny<BuildManifest>(), It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task OrchestrateBuild_BuildFails_CleansUpWorkspace()
    {
        // Arrange
        var manifest = new BuildManifest { Version = "1.0.0" };
        var target = new BuildTarget { Architecture = "linux-arm64" };
        var workspace = "/workspace/failed-build";

        _mockFileSystem.Setup(x => x.CreateTempDirectory()).Returns(workspace);
        _mockGitService.Setup(x => x.CloneAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CloneResult { Success = true });
        _mockSolutionGenerator.Setup(x => x.GenerateSolutionAsync(It.IsAny<BuildManifest>(), It.IsAny<List<string>>()))
            .ReturnsAsync(new SolutionGenerationResult { Success = true, SolutionPath = "/workspace/test.sln" });
        _mockHasher.Setup(x => x.ComputeHash(It.IsAny<BuildManifest>(), It.IsAny<BuildTarget>())).Returns("hash");
        _mockCacheChecker.Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        _mockDotNetBuilder
            .Setup(x => x.PublishAsync(It.IsAny<string>(), It.IsAny<BuildOptions>()))
            .ReturnsAsync(new PublishResult
            {
                Success = false,
                ErrorMessage = "Compilation error"
            });

        // Act
        var result = await _orchestrator.OrchestrateFullBuildAsync(manifest, target);

        // Assert
        result.Success.Should().BeFalse();

        // Verify workspace cleanup was attempted
        _mockFileSystem.Verify(x => x.DeleteDirectory(workspace, true), Times.Once);
    }

    [Theory]
    [InlineData("1.0.0", "Pro", "linux-arm64", "graviton3")]
    [InlineData("1.0.1", "Enterprise", "linux-amd64", "cascade-lake")]
    [InlineData("2.0.0", "Community", "linux-arm64", "graviton2")]
    public async Task OrchestrateBuild_VariousConfigurations_GeneratesCorrectTags(
        string version, string tier, string arch, string cpu)
    {
        // Arrange
        var manifest = new BuildManifest { Version = version, Tier = tier };
        var target = new BuildTarget { Architecture = arch, CpuModel = cpu };

        var expectedHash = "test123";
        var expectedTag = $"{version}-{tier.ToLower()}-{arch}-{expectedHash}";

        _mockHasher.Setup(x => x.ComputeHash(manifest, target)).Returns(expectedHash);
        _mockHasher.Setup(x => x.GenerateImageTag(manifest, target, expectedHash)).Returns(expectedTag);

        _mockCacheChecker.Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), expectedTag))
            .ReturnsAsync(new CacheCheckResult { Exists = true });
        _mockDeliveryService.Setup(x => x.DeliverBuildAsync(It.IsAny<BuildManifest>(), It.IsAny<BuildTarget>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(new DeliveryResult { Success = true, ImageUrl = $"registry.io/honua:{expectedTag}" });

        // Act
        var result = await _orchestrator.OrchestrateFullBuildAsync(manifest, target);

        // Assert
        result.Success.Should().BeTrue();
        result.ImageTag.Should().Be(expectedTag);
    }

    [Fact]
    public async Task OrchestrateBuild_ParallelTargets_BuildsMultipleArchitectures()
    {
        // Arrange
        var manifest = new BuildManifest { Version = "1.0.0" };
        var targets = new[]
        {
            new BuildTarget { Architecture = "linux-arm64", CpuModel = "graviton3" },
            new BuildTarget { Architecture = "linux-amd64", CpuModel = "cascade-lake" }
        };

        foreach (var target in targets)
        {
            _mockHasher.Setup(x => x.ComputeHash(manifest, target)).Returns($"hash-{target.Architecture}");
            _mockCacheChecker.Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new CacheCheckResult { Exists = false });
            _mockDeliveryService.Setup(x => x.DeliverBuildAsync(manifest, target, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new DeliveryResult { Success = true });
        }

        // Act
        var results = await _orchestrator.OrchestrateMultiTargetBuildAsync(manifest, targets);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Success);

        _mockDeliveryService.Verify(
            x => x.DeliverBuildAsync(It.IsAny<BuildManifest>(), It.IsAny<BuildTarget>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()),
            Times.Exactly(2)
        );
    }
}

// Placeholder classes and interfaces
public class BuildOrchestrator
{
    private readonly IGitRepositoryService _gitService;
    private readonly ISolutionGenerator _solutionGenerator;
    private readonly IDotNetBuilder _dotNetBuilder;
    private readonly IManifestHasher _hasher;
    private readonly IRegistryCacheChecker _cacheChecker;
    private readonly IBuildDeliveryService _deliveryService;
    private readonly IFileSystem _fileSystem;

    public BuildOrchestrator(
        IGitRepositoryService gitService,
        ISolutionGenerator solutionGenerator,
        IDotNetBuilder dotNetBuilder,
        IManifestHasher hasher,
        IRegistryCacheChecker cacheChecker,
        IBuildDeliveryService deliveryService,
        IFileSystem fileSystem)
    {
        _gitService = gitService;
        _solutionGenerator = solutionGenerator;
        _dotNetBuilder = dotNetBuilder;
        _hasher = hasher;
        _cacheChecker = cacheChecker;
        _deliveryService = deliveryService;
        _fileSystem = fileSystem;
    }

    public Task<CloneRepositoriesResult> CloneRepositoriesAsync(BuildManifest manifest)
        => throw new NotImplementedException();

    public Task<SolutionGenerationResult> GenerateSolutionAsync(BuildManifest manifest, List<string> clonedRepos)
        => throw new NotImplementedException();

    public Task<PublishResult> BuildForTargetAsync(BuildManifest manifest, BuildTarget target, string solutionPath)
        => throw new NotImplementedException();

    public Task<OrchestrationResult> OrchestrateFullBuildAsync(BuildManifest manifest, BuildTarget target)
        => throw new NotImplementedException();

    public Task<List<OrchestrationResult>> OrchestrateMultiTargetBuildAsync(BuildManifest manifest, BuildTarget[] targets)
        => throw new NotImplementedException();
}

public interface IGitRepositoryService
{
    Task<CloneResult> CloneAsync(string repoUrl, string localPath, string branch, string? pat);
}

public interface ISolutionGenerator
{
    Task<SolutionGenerationResult> GenerateSolutionAsync(BuildManifest manifest, List<string> repositoryPaths);
}

public interface IDotNetBuilder
{
    Task<PublishResult> PublishAsync(string solutionPath, BuildOptions options);
}

public interface IManifestHasher
{
    string ComputeHash(BuildManifest manifest, BuildTarget target);
    string GenerateImageTag(BuildManifest manifest, BuildTarget target, string hash);
}

public interface IBuildDeliveryService
{
    Task<DeliveryResult> DeliverBuildAsync(
        BuildManifest manifest, BuildTarget target, string cacheRegistry, string customerRegistry, bool storeToCacheRegistry);
}

public interface IFileSystem
{
    string CreateTempDirectory();
    void DeleteDirectory(string path, bool recursive);
}

public class RepositoryReference
{
    public string Url { get; set; } = "";
    public string Branch { get; set; } = "main";
    public bool IsPublic { get; set; }
    public string? PersonalAccessToken { get; set; }
}

public class CloneResult
{
    public bool Success { get; set; }
    public string? LocalPath { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CloneRepositoriesResult
{
    public bool Success { get; set; }
    public string WorkspaceDirectory { get; set; } = "";
    public List<string> ClonedRepositories { get; set; } = new();
}

public class SolutionGenerationResult
{
    public bool Success { get; set; }
    public string SolutionPath { get; set; } = "";
    public List<string> ProjectReferences { get; set; } = new();
}

public class PublishResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public long BinarySize { get; set; }
    public int BuildDurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BuildOptions
{
    public string Architecture { get; set; } = "";
    public bool EnableAot { get; set; }
    public bool EnableTrimming { get; set; }
    public bool InvariantGlobalization { get; set; }
    public string? CpuOptimization { get; set; }
}

public class OrchestrationResult
{
    public bool Success { get; set; }
    public bool CacheHit { get; set; }
    public string? ImageTag { get; set; }
    public string? ErrorMessage { get; set; }
}
