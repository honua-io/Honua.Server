using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Build.Orchestrator.Tests;

/// <summary>
/// Tests for BuildDeliveryService - handles delivering builds to customer registries.
/// Supports cache hits (copy existing build) and cache misses (build from source).
/// </summary>
[Trait("Category", "Unit")]
public class BuildDeliveryServiceTests
{
    private readonly Mock<IRegistryCacheChecker> _mockCacheChecker;
    private readonly Mock<IImageCopyService> _mockImageCopyService;
    private readonly Mock<IBuildExecutor> _mockBuildExecutor;
    private readonly Mock<IImageTagger> _mockImageTagger;
    private readonly BuildDeliveryService _service;

    public BuildDeliveryServiceTests()
    {
        _mockCacheChecker = new Mock<IRegistryCacheChecker>();
        _mockImageCopyService = new Mock<IImageCopyService>();
        _mockBuildExecutor = new Mock<IBuildExecutor>();
        _mockImageTagger = new Mock<IImageTagger>();

        _service = new BuildDeliveryService(
            _mockCacheChecker.Object,
            _mockImageCopyService.Object,
            _mockBuildExecutor.Object,
            _mockImageTagger.Object
        );
    }

    [Fact]
    public async Task DeliverBuild_CacheHit_CopiesFromCache()
    {
        // Arrange
        var manifest = CreateTestManifest("1.0.0", "Pro");
        var target = CreateBuildTarget("linux-arm64");
        var cacheRegistry = "ghcr.io/honua-cache";
        var customerRegistry = "123456789012.dkr.ecr.us-west-2.amazonaws.com/customer-123";
        var imageTag = "1.0.0-pro-linux-arm64-abc123";

        // Cache hit in cache registry
        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(cacheRegistry, "server", imageTag))
            .ReturnsAsync(new CacheCheckResult
            {
                Exists = true,
                CacheHit = true,
                Digest = "sha256:cacheddigest123"
            });

        _mockImageCopyService
            .Setup(x => x.CopyImageAsync(
                $"{cacheRegistry}/server:{imageTag}",
                $"{customerRegistry}/server:{imageTag}"))
            .ReturnsAsync(new ImageCopyResult { Success = true });

        // Act
        var result = await _service.DeliverBuildAsync(manifest, target, cacheRegistry, customerRegistry);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CacheHit.Should().BeTrue();
        result.BuildRequired.Should().BeFalse();
        result.ImageUrl.Should().Be($"{customerRegistry}/server:{imageTag}");

        // Verify cache was checked
        _mockCacheChecker.Verify(
            x => x.CheckCacheAsync(cacheRegistry, "server", imageTag),
            Times.Once
        );

        // Verify image was copied
        _mockImageCopyService.Verify(
            x => x.CopyImageAsync(
                It.Is<string>(s => s.Contains(cacheRegistry)),
                It.Is<string>(s => s.Contains(customerRegistry))),
            Times.Once
        );

        // Verify build was NOT executed
        _mockBuildExecutor.Verify(
            x => x.BuildImageAsync(It.IsAny<BuildManifest>(), It.IsAny<BuildTarget>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeliverBuild_CacheMiss_BuildsFromSource()
    {
        // Arrange
        var manifest = CreateTestManifest("1.0.1", "Pro");
        var target = CreateBuildTarget("linux-amd64");
        var cacheRegistry = "ghcr.io/honua-cache";
        var customerRegistry = "honua.azurecr.io/customer-456";
        var imageTag = "1.0.1-pro-linux-amd64-def456";

        // Cache miss
        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(cacheRegistry, "server", imageTag))
            .ReturnsAsync(new CacheCheckResult
            {
                Exists = false,
                CacheHit = false
            });

        // Build succeeds
        _mockBuildExecutor
            .Setup(x => x.BuildImageAsync(manifest, target))
            .ReturnsAsync(new BuildResult
            {
                Success = true,
                ImageDigest = "sha256:newbuilddigest789",
                BuildDurationSeconds = 120
            });

        // Tag and push to customer registry
        _mockImageTagger
            .Setup(x => x.TagAndPushAsync(It.IsAny<string>(), customerRegistry, "server", imageTag))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeliverBuildAsync(manifest, target, cacheRegistry, customerRegistry);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CacheHit.Should().BeFalse();
        result.BuildRequired.Should().BeTrue();
        result.BuildDurationSeconds.Should().Be(120);

        // Verify build was executed
        _mockBuildExecutor.Verify(
            x => x.BuildImageAsync(manifest, target),
            Times.Once
        );

        // Verify image was tagged and pushed
        _mockImageTagger.Verify(
            x => x.TagAndPushAsync(It.IsAny<string>(), customerRegistry, "server", imageTag),
            Times.Once
        );

        // Verify copy was NOT used
        _mockImageCopyService.Verify(
            x => x.CopyImageAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never
        );
    }

    [Fact]
    public async Task CopyImage_ValidRegistries_UsesCrane()
    {
        // Arrange
        var sourceImage = "ghcr.io/honua/server:1.0.0-pro-linux-arm64-abc";
        var targetImage = "123456789012.dkr.ecr.us-west-2.amazonaws.com/customer/server:1.0.0-pro-linux-arm64-abc";

        _mockImageCopyService
            .Setup(x => x.CopyImageAsync(sourceImage, targetImage))
            .ReturnsAsync(new ImageCopyResult
            {
                Success = true,
                CopiedBytes = 250_000_000, // 250 MB
                DurationSeconds = 15
            });

        // Act
        var result = await _service.CopyImageAsync(sourceImage, targetImage);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.CopiedBytes.Should().Be(250_000_000);
        result.DurationSeconds.Should().Be(15);

        _mockImageCopyService.Verify(
            x => x.CopyImageAsync(sourceImage, targetImage),
            Times.Once
        );
    }

    [Fact]
    public async Task CopyImage_CrossCloudProviders_HandlesAuthentication()
    {
        // Arrange - Copy from GitHub to AWS ECR
        var sourceImage = "ghcr.io/honua/server:tag";
        var targetImage = "123456789012.dkr.ecr.us-west-2.amazonaws.com/server:tag";

        var sourceAuth = new RegistryAuth { Token = "ghp_github_token" };
        var targetAuth = new RegistryAuth
        {
            AwsAccessKeyId = "AKIAIOSFODNN7EXAMPLE",
            AwsSecretAccessKey = "secret"
        };

        _mockImageCopyService
            .Setup(x => x.CopyImageWithAuthAsync(sourceImage, targetImage, sourceAuth, targetAuth))
            .ReturnsAsync(new ImageCopyResult { Success = true });

        // Act
        var result = await _service.CopyImageWithAuthAsync(sourceImage, targetImage, sourceAuth, targetAuth);

        // Assert
        result.Success.Should().BeTrue();

        _mockImageCopyService.Verify(
            x => x.CopyImageWithAuthAsync(sourceImage, targetImage, sourceAuth, targetAuth),
            Times.Once
        );
    }

    [Fact]
    public async Task TagImage_MultipleTargets_CreatesAllTags()
    {
        // Arrange
        var sourceDigest = "sha256:abc123def456";
        var registry = "ghcr.io/honua";
        var imageName = "server";
        var tags = new[] { "1.0.0", "1.0.0-pro-linux-arm64-abc", "latest-pro" };

        _mockImageTagger
            .Setup(x => x.CreateMultipleTagsAsync(sourceDigest, registry, imageName, tags))
            .ReturnsAsync(new TagResult
            {
                Success = true,
                TagsCreated = tags.ToList()
            });

        // Act
        var result = await _service.TagImageAsync(sourceDigest, registry, imageName, tags);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TagsCreated.Should().HaveCount(3);
        result.TagsCreated.Should().Contain(tags);

        _mockImageTagger.Verify(
            x => x.CreateMultipleTagsAsync(sourceDigest, registry, imageName, tags),
            Times.Once
        );
    }

    [Fact]
    public async Task DeliverBuild_CacheHitButCopyFails_FallbacksToBuild()
    {
        // Arrange
        var manifest = CreateTestManifest("1.0.0", "Pro");
        var target = CreateBuildTarget("linux-arm64");
        var cacheRegistry = "ghcr.io/honua-cache";
        var customerRegistry = "customer-registry.io/honua";
        var imageTag = "1.0.0-pro-linux-arm64-xyz";

        // Cache hit
        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(cacheRegistry, "server", imageTag))
            .ReturnsAsync(new CacheCheckResult { Exists = true, CacheHit = true });

        // Copy fails
        _mockImageCopyService
            .Setup(x => x.CopyImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ImageCopyResult
            {
                Success = false,
                ErrorMessage = "Network timeout"
            });

        // Build succeeds as fallback
        _mockBuildExecutor
            .Setup(x => x.BuildImageAsync(manifest, target))
            .ReturnsAsync(new BuildResult { Success = true });

        // Act
        var result = await _service.DeliverBuildAsync(manifest, target, cacheRegistry, customerRegistry);

        // Assert
        result.Success.Should().BeTrue();
        result.CacheHit.Should().BeTrue(); // Was a cache hit originally
        result.BuildRequired.Should().BeTrue(); // But build was required due to copy failure
        result.FallbackUsed.Should().BeTrue();

        _mockImageCopyService.Verify(x => x.CopyImageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _mockBuildExecutor.Verify(x => x.BuildImageAsync(manifest, target), Times.Once);
    }

    [Fact]
    public async Task DeliverBuild_BuildFails_ReturnsFailureResult()
    {
        // Arrange
        var manifest = CreateTestManifest("1.0.0", "Pro");
        var target = CreateBuildTarget("linux-arm64");
        var cacheRegistry = "ghcr.io/honua-cache";
        var customerRegistry = "customer.io/repo";
        var imageTag = "1.0.0-pro-linux-arm64-fail";

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        _mockBuildExecutor
            .Setup(x => x.BuildImageAsync(manifest, target))
            .ReturnsAsync(new BuildResult
            {
                Success = false,
                ErrorMessage = "Compilation failed: Missing dependency"
            });

        // Act
        var result = await _service.DeliverBuildAsync(manifest, target, cacheRegistry, customerRegistry);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Compilation failed");
    }

    [Fact]
    public async Task DeliverBuild_MultiArchImage_CopiesBothArchitectures()
    {
        // Arrange
        var manifest = CreateTestManifest("1.0.0", "Pro");
        var targetArm = CreateBuildTarget("linux-arm64");
        var targetAmd = CreateBuildTarget("linux-amd64");
        var cacheRegistry = "ghcr.io/honua-cache";
        var customerRegistry = "customer.io/repo";

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CacheCheckResult { Exists = true, CacheHit = true });

        _mockImageCopyService
            .Setup(x => x.CopyImageAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ImageCopyResult { Success = true });

        // Act
        var resultArm = await _service.DeliverBuildAsync(manifest, targetArm, cacheRegistry, customerRegistry);
        var resultAmd = await _service.DeliverBuildAsync(manifest, targetAmd, cacheRegistry, customerRegistry);

        // Assert
        resultArm.Success.Should().BeTrue();
        resultAmd.Success.Should().BeTrue();

        _mockImageCopyService.Verify(
            x => x.CopyImageAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task DeliverBuild_StoresCacheEntryAfterBuild()
    {
        // Arrange
        var manifest = CreateTestManifest("1.0.0", "Pro");
        var target = CreateBuildTarget("linux-arm64");
        var cacheRegistry = "ghcr.io/honua-cache";
        var customerRegistry = "customer.io/repo";
        var imageTag = "1.0.0-pro-linux-arm64-store";

        _mockCacheChecker
            .Setup(x => x.CheckCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new CacheCheckResult { Exists = false });

        _mockBuildExecutor
            .Setup(x => x.BuildImageAsync(manifest, target))
            .ReturnsAsync(new BuildResult
            {
                Success = true,
                ImageDigest = "sha256:newbuild123"
            });

        _mockImageTagger
            .Setup(x => x.TagAndPushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        // Also push to cache registry for future use
        _mockImageCopyService
            .Setup(x => x.CopyImageAsync(
                It.Is<string>(s => s.Contains(customerRegistry)),
                It.Is<string>(s => s.Contains(cacheRegistry))))
            .ReturnsAsync(new ImageCopyResult { Success = true });

        // Act
        var result = await _service.DeliverBuildAsync(
            manifest, target, cacheRegistry, customerRegistry, storeToCacheRegistry: true);

        // Assert
        result.Success.Should().BeTrue();
        result.StoredToCache.Should().BeTrue();

        // Verify image was copied back to cache registry
        _mockImageCopyService.Verify(
            x => x.CopyImageAsync(
                It.Is<string>(s => s.Contains(customerRegistry)),
                It.Is<string>(s => s.Contains(cacheRegistry))),
            Times.Once
        );
    }

    [Theory]
    [InlineData(50_000_000, 5)] // 50 MB in 5 seconds
    [InlineData(250_000_000, 15)] // 250 MB in 15 seconds
    [InlineData(500_000_000, 30)] // 500 MB in 30 seconds
    public async Task CopyImage_ReportsProgressMetrics(long bytes, int seconds)
    {
        // Arrange
        var sourceImage = "source.io/image:tag";
        var targetImage = "target.io/image:tag";

        _mockImageCopyService
            .Setup(x => x.CopyImageAsync(sourceImage, targetImage))
            .ReturnsAsync(new ImageCopyResult
            {
                Success = true,
                CopiedBytes = bytes,
                DurationSeconds = seconds
            });

        // Act
        var result = await _service.CopyImageAsync(sourceImage, targetImage);

        // Assert
        result.CopiedBytes.Should().Be(bytes);
        result.DurationSeconds.Should().Be(seconds);
        result.ThroughputMbps.Should().BeApproximately(
            (bytes / 1_000_000.0) / seconds,
            0.1
        );
    }

    // Helper methods
    private static BuildManifest CreateTestManifest(string version, string tier)
    {
        return new BuildManifest
        {
            Version = version,
            Tier = tier,
            Modules = new List<string> { "Core", "Ogc", "Stac" }
        };
    }

    private static BuildTarget CreateBuildTarget(string architecture)
    {
        return new BuildTarget
        {
            Architecture = architecture,
            CpuModel = architecture.Contains("arm64") ? "graviton3" : "cascade-lake",
            CloudProvider = "aws"
        };
    }
}

// Placeholder classes and interfaces
public class BuildDeliveryService
{
    private readonly IRegistryCacheChecker _cacheChecker;
    private readonly IImageCopyService _imageCopyService;
    private readonly IBuildExecutor _buildExecutor;
    private readonly IImageTagger _imageTagger;

    public BuildDeliveryService(
        IRegistryCacheChecker cacheChecker,
        IImageCopyService imageCopyService,
        IBuildExecutor buildExecutor,
        IImageTagger imageTagger)
    {
        _cacheChecker = cacheChecker;
        _imageCopyService = imageCopyService;
        _buildExecutor = buildExecutor;
        _imageTagger = imageTagger;
    }

    public Task<DeliveryResult> DeliverBuildAsync(
        BuildManifest manifest,
        BuildTarget target,
        string cacheRegistry,
        string customerRegistry,
        bool storeToCacheRegistry = false)
        => throw new NotImplementedException();

    public Task<ImageCopyResult> CopyImageAsync(string sourceImage, string targetImage)
        => throw new NotImplementedException();

    public Task<ImageCopyResult> CopyImageWithAuthAsync(
        string sourceImage, string targetImage, RegistryAuth sourceAuth, RegistryAuth targetAuth)
        => throw new NotImplementedException();

    public Task<TagResult> TagImageAsync(string sourceDigest, string registry, string imageName, string[] tags)
        => throw new NotImplementedException();
}

public interface IRegistryCacheChecker
{
    Task<CacheCheckResult> CheckCacheAsync(string registry, string imageName, string tag);
}

public interface IImageCopyService
{
    Task<ImageCopyResult> CopyImageAsync(string sourceImage, string targetImage);
    Task<ImageCopyResult> CopyImageWithAuthAsync(
        string sourceImage, string targetImage, RegistryAuth sourceAuth, RegistryAuth targetAuth);
}

public interface IBuildExecutor
{
    Task<BuildResult> BuildImageAsync(BuildManifest manifest, BuildTarget target);
}

public interface IImageTagger
{
    Task<bool> TagAndPushAsync(string sourceDigest, string registry, string imageName, string tag);
    Task<TagResult> CreateMultipleTagsAsync(string sourceDigest, string registry, string imageName, string[] tags);
}

public class DeliveryResult
{
    public bool Success { get; set; }
    public bool CacheHit { get; set; }
    public bool BuildRequired { get; set; }
    public bool FallbackUsed { get; set; }
    public bool StoredToCache { get; set; }
    public string? ImageUrl { get; set; }
    public int BuildDurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ImageCopyResult
{
    public bool Success { get; set; }
    public long CopiedBytes { get; set; }
    public int DurationSeconds { get; set; }
    public double ThroughputMbps { get; set; }
    public string? ErrorMessage { get; set; }
}

public class BuildResult
{
    public bool Success { get; set; }
    public string? ImageDigest { get; set; }
    public int BuildDurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TagResult
{
    public bool Success { get; set; }
    public List<string> TagsCreated { get; set; } = new();
}

public class RegistryAuth
{
    public string? Token { get; set; }
    public string? AwsAccessKeyId { get; set; }
    public string? AwsSecretAccessKey { get; set; }
}
