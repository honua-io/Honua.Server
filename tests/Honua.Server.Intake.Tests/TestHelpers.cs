// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using Honua.Server.Intake.Models;

namespace Honua.Server.Intake.Tests;

/// <summary>
/// Helper methods and fixtures for tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Creates a sample ConversationRecord for testing.
    /// </summary>
    public static ConversationRecord CreateSampleConversation(
        string conversationId = "test-conv",
        string customerId = "test-customer",
        string status = "active",
        bool includeRequirements = false)
    {
        var messages = new List<ConversationMessage>
        {
            new() { Role = "system", Content = "System prompt" },
            new() { Role = "user", Content = "I need WFS support" },
            new() { Role = "assistant", Content = "What cloud provider?" }
        };

        string? requirementsJson = null;
        if (includeRequirements)
        {
            var requirements = CreateSampleRequirements();
            requirementsJson = JsonSerializer.Serialize(requirements);
        }

        return new ConversationRecord
        {
            ConversationId = conversationId,
            CustomerId = customerId,
            MessagesJson = JsonSerializer.Serialize(messages),
            Status = status,
            RequirementsJson = requirementsJson,
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            UpdatedAt = DateTimeOffset.UtcNow,
            CompletedAt = status == "completed" ? DateTimeOffset.UtcNow : null
        };
    }

    /// <summary>
    /// Creates sample BuildRequirements for testing.
    /// </summary>
    public static BuildRequirements CreateSampleRequirements(
        string cloudProvider = "aws",
        string architecture = "linux-x64",
        string tier = "pro")
    {
        return new BuildRequirements
        {
            Protocols = new List<string> { "WFS", "WMS" },
            Databases = new List<string> { "PostGIS" },
            CloudProvider = cloudProvider,
            Architecture = architecture,
            Tier = tier,
            Load = new ExpectedLoad
            {
                ConcurrentUsers = 100,
                RequestsPerSecond = 50,
                DataVolumeGb = 100,
                Classification = "moderate"
            }
        };
    }

    /// <summary>
    /// Creates a sample BuildManifest for testing.
    /// </summary>
    public static BuildManifest CreateSampleManifest(
        string name = "test-build",
        string architecture = "linux-x64",
        string tier = "pro")
    {
        return new BuildManifest
        {
            Version = "1.0",
            Name = name,
            Architecture = architecture,
            Modules = new List<string> { "WFS", "WMS", "PostGIS" },
            DatabaseConnectors = new List<string> { "PostGIS" },
            Tier = tier,
            CloudTargets = new List<CloudTarget>
            {
                new()
                {
                    Provider = "aws",
                    Region = "us-west-2",
                    InstanceType = "t3.medium"
                }
            },
            Resources = new ResourceRequirements
            {
                MinCpu = 2,
                MinMemoryGb = 8,
                RecommendedCpu = 4,
                RecommendedMemoryGb = 16,
                StorageGb = 50
            },
            GeneratedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a sample BuildJob for testing.
    /// </summary>
    public static BuildJob CreateSampleBuildJob(
        string customerId = "test-customer",
        string cloudProvider = "aws",
        BuildJobStatus status = BuildJobStatus.Pending)
    {
        return new BuildJob
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            ConfigurationName = "honua-server-pro",
            ManifestPath = "/tmp/manifest.json",
            CloudProvider = cloudProvider,
            Priority = 1,
            Status = status,
            RetryCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a sample RegistryProvisioningResult for testing.
    /// </summary>
    public static RegistryProvisioningResult CreateSampleRegistryResult(
        string customerId = "test-customer",
        RegistryType registryType = RegistryType.AwsEcr,
        bool success = true)
    {
        return new RegistryProvisioningResult
        {
            Success = success,
            RegistryType = registryType,
            CustomerId = customerId,
            Namespace = $"honua/{customerId}",
            Credential = success ? new RegistryCredential
            {
                RegistryUrl = "123456789012.dkr.ecr.us-west-2.amazonaws.com",
                Username = "AWS",
                Password = "test-token"
            } : null,
            ProvisionedAt = DateTimeOffset.UtcNow,
            ErrorMessage = success ? null : "Provisioning failed"
        };
    }

    /// <summary>
    /// Creates a sample BuildCacheKey for testing.
    /// </summary>
    public static BuildCacheKey CreateSampleCacheKey(
        string customerId = "test-customer",
        string buildName = "honua-server",
        string version = "latest",
        string architecture = "linux-x64")
    {
        return new BuildCacheKey
        {
            CustomerId = customerId,
            BuildName = buildName,
            Version = version,
            Architecture = architecture
        };
    }

    /// <summary>
    /// Asserts that a cost breakdown is valid and contains expected keys.
    /// </summary>
    public static void AssertValidCostBreakdown(Dictionary<string, decimal>? breakdown)
    {
        if (breakdown == null)
        {
            throw new ArgumentNullException(nameof(breakdown), "Cost breakdown should not be null");
        }

        if (!breakdown.ContainsKey("license"))
        {
            throw new ArgumentException("Cost breakdown must contain 'license' key");
        }

        if (!breakdown.ContainsKey("infrastructure"))
        {
            throw new ArgumentException("Cost breakdown must contain 'infrastructure' key");
        }

        if (!breakdown.ContainsKey("storage"))
        {
            throw new ArgumentException("Cost breakdown must contain 'storage' key");
        }

        foreach (var cost in breakdown.Values)
        {
            if (cost < 0)
            {
                throw new ArgumentException("All costs must be non-negative");
            }
        }
    }
}

/// <summary>
/// Test fixtures for xUnit tests.
/// </summary>
public class IntakeServiceTestFixture : IDisposable
{
    public InMemoryConversationStore ConversationStore { get; }

    public IntakeServiceTestFixture()
    {
        ConversationStore = new InMemoryConversationStore();
    }

    public void Dispose()
    {
        // Cleanup if needed
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Collection definition for sharing test context.
/// </summary>
[Xunit.CollectionDefinition("IntakeService")]
public class IntakeServiceCollection : Xunit.ICollectionFixture<IntakeServiceTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
