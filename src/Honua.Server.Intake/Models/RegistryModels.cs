// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Intake.Models;

/// <summary>
/// Supported container registry types.
/// </summary>
public enum RegistryType
{
    /// <summary>GitHub Container Registry (ghcr.io)</summary>
    GitHubContainerRegistry,

    /// <summary>AWS Elastic Container Registry</summary>
    AwsEcr,

    /// <summary>Azure Container Registry</summary>
    AzureAcr,

    /// <summary>Google Cloud Artifact Registry</summary>
    GcpArtifactRegistry
}

/// <summary>
/// Result of registry provisioning operation.
/// </summary>
public sealed class RegistryProvisioningResult
{
    /// <summary>
    /// Indicates if provisioning was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The registry type that was provisioned.
    /// </summary>
    public RegistryType RegistryType { get; init; }

    /// <summary>
    /// The customer identifier.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// The registry namespace assigned to the customer.
    /// </summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>
    /// The credentials for accessing the registry.
    /// </summary>
    public RegistryCredential? Credential { get; init; }

    /// <summary>
    /// Error message if provisioning failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional metadata about the provisioning operation.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// When the registry was provisioned.
    /// </summary>
    public DateTimeOffset ProvisionedAt { get; init; }
}

/// <summary>
/// Container registry credentials.
/// </summary>
public sealed class RegistryCredential
{
    /// <summary>
    /// The registry URL (e.g., ghcr.io, 123456789012.dkr.ecr.us-west-2.amazonaws.com).
    /// </summary>
    public string RegistryUrl { get; init; } = string.Empty;

    /// <summary>
    /// The username for authentication.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The password or token for authentication.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// When the credentials expire (if applicable).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Additional authentication metadata.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result of registry access validation.
/// </summary>
public sealed class RegistryAccessResult
{
    /// <summary>
    /// Indicates if access is granted.
    /// </summary>
    public bool AccessGranted { get; init; }

    /// <summary>
    /// The customer identifier.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// The registry type being accessed.
    /// </summary>
    public RegistryType RegistryType { get; init; }

    /// <summary>
    /// Short-lived access token.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTimeOffset? TokenExpiresAt { get; init; }

    /// <summary>
    /// Reason for access denial (if applicable).
    /// </summary>
    public string? DenialReason { get; init; }

    /// <summary>
    /// License tier information.
    /// </summary>
    public string? LicenseTier { get; init; }
}

/// <summary>
/// Result of checking if a build exists in the registry cache.
/// </summary>
public sealed class CacheCheckResult
{
    /// <summary>
    /// Indicates if the build exists in the cache.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    /// The registry type that was checked.
    /// </summary>
    public RegistryType RegistryType { get; init; }

    /// <summary>
    /// The image tag that was checked.
    /// </summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>
    /// The full image reference.
    /// </summary>
    public string ImageReference { get; init; } = string.Empty;

    /// <summary>
    /// The image digest (if it exists).
    /// </summary>
    public string? Digest { get; init; }

    /// <summary>
    /// When the image was last updated (if available).
    /// </summary>
    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>
    /// Error message if check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Key for identifying a build in the cache.
/// </summary>
public sealed class BuildCacheKey
{
    /// <summary>
    /// The customer identifier.
    /// </summary>
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>
    /// The build/application name.
    /// </summary>
    public string BuildName { get; init; } = string.Empty;

    /// <summary>
    /// The version tag.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// The target architecture (e.g., amd64, arm64).
    /// </summary>
    public string? Architecture { get; init; }

    /// <summary>
    /// Additional metadata for cache key generation.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Generates the full image tag.
    /// </summary>
    public string GenerateTag()
    {
        if (Architecture != null)
        {
            return $"{Version}-{Architecture}";
        }
        return Version;
    }

    /// <summary>
    /// Generates the full image reference for a given registry.
    /// </summary>
    public string GenerateImageReference(string registryUrl, string organizationOrNamespace)
    {
        var tag = GenerateTag();
        return $"{registryUrl}/{organizationOrNamespace}/{CustomerId}/{BuildName}:{tag}";
    }
}

/// <summary>
/// Result of a build delivery operation.
/// </summary>
public sealed class BuildDeliveryResult
{
    /// <summary>
    /// Indicates if delivery was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The build cache key.
    /// </summary>
    public BuildCacheKey CacheKey { get; init; } = new();

    /// <summary>
    /// The target registry type.
    /// </summary>
    public RegistryType TargetRegistry { get; init; }

    /// <summary>
    /// The delivered image reference.
    /// </summary>
    public string ImageReference { get; init; } = string.Empty;

    /// <summary>
    /// The image digest.
    /// </summary>
    public string? Digest { get; init; }

    /// <summary>
    /// Indicates if the image was built or retrieved from cache.
    /// </summary>
    public bool WasCached { get; init; }

    /// <summary>
    /// Additional tags that were applied.
    /// </summary>
    public List<string>? AdditionalTags { get; init; }

    /// <summary>
    /// Error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// When the delivery completed.
    /// </summary>
    public DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// Configuration for registry provisioning.
/// </summary>
public sealed class RegistryProvisioningOptions
{
    /// <summary>
    /// GitHub organization for GHCR.
    /// </summary>
    public string? GitHubOrganization { get; init; }

    /// <summary>
    /// GitHub PAT with package permissions.
    /// </summary>
    public string? GitHubToken { get; init; }

    /// <summary>
    /// AWS region for ECR.
    /// </summary>
    public string? AwsRegion { get; init; }

    /// <summary>
    /// AWS account ID.
    /// </summary>
    public string? AwsAccountId { get; init; }

    /// <summary>
    /// Azure subscription ID.
    /// </summary>
    public string? AzureSubscriptionId { get; init; }

    /// <summary>
    /// Azure resource group for ACR.
    /// </summary>
    public string? AzureResourceGroup { get; init; }

    /// <summary>
    /// Azure ACR registry name.
    /// </summary>
    public string? AzureRegistryName { get; init; }

    /// <summary>
    /// GCP project ID.
    /// </summary>
    public string? GcpProjectId { get; init; }

    /// <summary>
    /// GCP region for Artifact Registry.
    /// </summary>
    public string? GcpRegion { get; init; }

    /// <summary>
    /// GCP Artifact Registry repository name.
    /// </summary>
    public string? GcpRepositoryName { get; init; }
}
