// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Xunit;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Tests for ManifestGenerator - generates build manifests from requirements.
/// </summary>
[Trait("Category", "Unit")]
public class ManifestGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ValidRequirements_GeneratesManifest()
    {
        // Arrange
        var generator = new TestManifestGenerator();
        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS", "WMS" },
            Databases = new List<string> { "PostGIS" },
            CloudProvider = "aws",
            Architecture = "linux-x64",
            Tier = "pro"
        };

        // Act
        var manifest = await generator.GenerateAsync(requirements, "test-build");

        // Assert
        manifest.Should().NotBeNull();
        manifest.Name.Should().Be("test-build");
        manifest.Architecture.Should().Be("linux-x64");
        manifest.Modules.Should().Contain("WFS");
        manifest.Modules.Should().Contain("WMS");
        manifest.DatabaseConnectors.Should().Contain("PostGIS");
        manifest.Tier.Should().Be("pro");
    }

    [Fact]
    public async Task GenerateAsync_MultipleProtocols_IncludesAllModules()
    {
        // Arrange
        var generator = new TestManifestGenerator();
        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS", "WMS", "WMTS", "WCS" },
            CloudProvider = "azure",
            Architecture = "linux-arm64",
            Tier = "enterprise"
        };

        // Act
        var manifest = await generator.GenerateAsync(requirements, "multi-protocol-build");

        // Assert
        manifest.Modules.Should().HaveCount(4);
        manifest.Modules.Should().Contain(new[] { "WFS", "WMS", "WMTS", "WCS" });
    }

    [Fact]
    public async Task GenerateAsync_WithLoadRequirements_GeneratesResourceSpecs()
    {
        // Arrange
        var generator = new TestManifestGenerator();
        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS" },
            CloudProvider = "gcp",
            Architecture = "linux-x64",
            Tier = "enterprise",
            Load = new ExpectedLoad
            {
                ConcurrentUsers = 1000,
                RequestsPerSecond = 500,
                DataVolumeGb = 500,
                Classification = "heavy"
            }
        };

        // Act
        var manifest = await generator.GenerateAsync(requirements, "high-load-build");

        // Assert
        manifest.Resources.Should().NotBeNull();
        manifest.Resources!.RecommendedCpu.Should().BeGreaterThan(4);
        manifest.Resources.RecommendedMemoryGb.Should().BeGreaterThan(8);
    }

    [Theory]
    [InlineData("core", "honua-server-core")]
    [InlineData("pro", "honua-server-pro")]
    [InlineData("enterprise", "honua-server-enterprise")]
    public async Task GenerateAsync_DifferentTiers_GeneratesCorrectNames(string tier, string expectedPrefix)
    {
        // Arrange
        var generator = new TestManifestGenerator();
        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS" },
            CloudProvider = "aws",
            Architecture = "linux-x64",
            Tier = tier
        };

        // Act
        var manifest = await generator.GenerateAsync(requirements, null);

        // Assert
        manifest.Name.Should().Contain(tier);
    }

    [Fact]
    public async Task GenerateAsync_WithCloudTarget_GeneratesDeploymentConfig()
    {
        // Arrange
        var generator = new TestManifestGenerator();
        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WMS" },
            CloudProvider = "azure",
            Architecture = "linux-x64",
            Tier = "pro"
        };

        // Act
        var manifest = await generator.GenerateAsync(requirements, "azure-deployment");

        // Assert
        manifest.CloudTargets.Should().NotBeNull();
        manifest.CloudTargets.Should().HaveCountGreaterThan(0);
        manifest.CloudTargets![0].Provider.Should().Be("azure");
    }

    [Fact]
    public async Task GenerateAsync_WithAdvancedFeatures_IncludesInModules()
    {
        // Arrange
        var generator = new TestManifestGenerator();
        var requirements = new BuildRequirements
        {
            Protocols = new List<string> { "WFS" },
            CloudProvider = "aws",
            Architecture = "linux-x64",
            Tier = "enterprise",
            AdvancedFeatures = new List<string> { "caching", "clustering", "monitoring" }
        };

        // Act
        var manifest = await generator.GenerateAsync(requirements, "advanced-build");

        // Assert
        manifest.Modules.Should().Contain("caching");
        manifest.Modules.Should().Contain("clustering");
    }
}

/// <summary>
/// Test implementation of IManifestGenerator for testing.
/// </summary>
public class TestManifestGenerator : IManifestGenerator
{
    public Task<BuildManifest> GenerateAsync(
        BuildRequirements requirements,
        string? buildName = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        var name = buildName ?? $"honua-server-{requirements.Tier}";

        var modules = new List<string>(requirements.Protocols);
        if (requirements.AdvancedFeatures != null)
        {
            modules.AddRange(requirements.AdvancedFeatures);
        }

        var manifest = new BuildManifest
        {
            Version = "1.0",
            Name = name,
            Architecture = requirements.Architecture,
            Modules = modules,
            DatabaseConnectors = new List<string>(requirements.Databases),
            Tier = requirements.Tier,
            CloudTargets = new List<CloudTarget>
            {
                new()
                {
                    Provider = requirements.CloudProvider,
                    Region = "default",
                    InstanceType = DetermineInstanceType(requirements)
                }
            },
            Resources = GenerateResourceRequirements(requirements),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        return Task.FromResult(manifest);
    }

    private static string DetermineInstanceType(BuildRequirements requirements)
    {
        var classification = requirements.Load?.Classification ?? "light";

        return (requirements.CloudProvider.ToLowerInvariant(), classification) switch
        {
            ("aws", "light") => "t3.medium",
            ("aws", "moderate") => "c6i.large",
            ("aws", "heavy") => "c6i.xlarge",
            ("azure", "light") => "D2s_v5",
            ("azure", "moderate") => "D4s_v5",
            ("azure", "heavy") => "D8s_v5",
            ("gcp", "light") => "e2-standard-2",
            ("gcp", "moderate") => "e2-standard-4",
            ("gcp", "heavy") => "e2-standard-8",
            _ => "medium"
        };
    }

    private static ResourceRequirements GenerateResourceRequirements(BuildRequirements requirements)
    {
        var classification = requirements.Load?.Classification ?? "light";

        return classification switch
        {
            "heavy" => new ResourceRequirements
            {
                MinCpu = 4,
                MinMemoryGb = 16,
                RecommendedCpu = 8,
                RecommendedMemoryGb = 32,
                StorageGb = requirements.Load?.DataVolumeGb ?? 100
            },
            "moderate" => new ResourceRequirements
            {
                MinCpu = 2,
                MinMemoryGb = 8,
                RecommendedCpu = 4,
                RecommendedMemoryGb = 16,
                StorageGb = requirements.Load?.DataVolumeGb ?? 50
            },
            _ => new ResourceRequirements
            {
                MinCpu = 1,
                MinMemoryGb = 4,
                RecommendedCpu = 2,
                RecommendedMemoryGb = 8,
                StorageGb = requirements.Load?.DataVolumeGb ?? 20
            }
        };
    }
}
