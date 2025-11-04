using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Honua.Build.Orchestrator.Models;

namespace Honua.Build.Orchestrator;

/// <summary>
/// Generates deterministic hashes from build manifests for caching and versioning.
/// </summary>
public sealed class ManifestHasher
{
    private static readonly JsonSerializerOptions NormalizedJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Computes a deterministic 8-character hash from the build manifest.
    /// This hash uniquely identifies the build configuration.
    /// </summary>
    /// <param name="manifest">The build manifest to hash.</param>
    /// <returns>8-character hexadecimal hash string.</returns>
    public static string ComputeHash(BuildManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var normalized = NormalizeManifest(manifest);
        var json = JsonSerializer.Serialize(normalized, NormalizedJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);

        // Take first 4 bytes (8 hex characters) for brevity
        return Convert.ToHexString(hash[..4]).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a cache key incorporating the manifest hash and deployment tier.
    /// Used for build artifact caching.
    /// </summary>
    /// <param name="manifest">The build manifest.</param>
    /// <param name="targetId">Specific target ID to include in the key.</param>
    /// <returns>Cache key string in format: "{tier}-{hash}-{targetId}".</returns>
    public static string ComputeCacheKey(BuildManifest manifest, string targetId)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        var hash = ComputeHash(manifest);
        var target = manifest.Targets.FirstOrDefault(t => t.Id == targetId);
        var tier = target?.Tier ?? manifest.Deployment?.Environment ?? "default";

        return $"{tier}-{hash}-{targetId}";
    }

    /// <summary>
    /// Generates a Docker image tag from the manifest hash and optional version.
    /// </summary>
    /// <param name="manifest">The build manifest.</param>
    /// <param name="targetId">Target ID to include in the tag.</param>
    /// <param name="includeVersion">Whether to include the manifest version in the tag.</param>
    /// <returns>Docker tag string (e.g., "v1.0-abc123de-aws-graviton").</returns>
    public static string GenerateImageTag(BuildManifest manifest, string targetId, bool includeVersion = true)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        var hash = ComputeHash(manifest);
        var parts = new List<string>();

        if (includeVersion && !string.IsNullOrWhiteSpace(manifest.Version))
        {
            parts.Add($"v{manifest.Version}");
        }

        parts.Add(hash);
        parts.Add(targetId);

        return string.Join("-", parts);
    }

    /// <summary>
    /// Generates a semantic version-based Docker tag.
    /// </summary>
    /// <param name="manifest">The build manifest.</param>
    /// <param name="commitSha">Optional git commit SHA to append.</param>
    /// <returns>Semantic version tag (e.g., "1.0.0" or "1.0.0-abc1234").</returns>
    public static string GenerateSemanticTag(BuildManifest manifest, string? commitSha = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var version = manifest.Version;
        if (string.IsNullOrWhiteSpace(commitSha))
        {
            return version;
        }

        var shortSha = commitSha.Length > 7 ? commitSha[..7] : commitSha;
        return $"{version}-{shortSha}";
    }

    /// <summary>
    /// Normalizes the manifest for consistent hashing by removing non-deterministic fields
    /// and sorting collections.
    /// </summary>
    private static BuildManifest NormalizeManifest(BuildManifest manifest)
    {
        // Create a deep clone with normalized fields
        var normalized = new BuildManifest
        {
            Id = manifest.Id,
            Version = manifest.Version,
            Name = manifest.Name,
            Description = manifest.Description,
            Repositories = manifest.Repositories
                .OrderBy(r => r.Name)
                .Select(r => new RepositoryReference
                {
                    Name = r.Name,
                    Url = r.Url,
                    Ref = r.Ref,
                    Access = r.Access,
                    Credentials = r.Credentials,
                    Projects = r.Projects?.OrderBy(p => p).ToList()
                })
                .ToList(),
            Modules = manifest.Modules.OrderBy(m => m).ToList(),
            Targets = manifest.Targets
                .OrderBy(t => t.Id)
                .Select(t => new CloudTarget
                {
                    Id = t.Id,
                    Provider = t.Provider,
                    Compute = t.Compute,
                    Architecture = t.Architecture,
                    Tier = t.Tier,
                    Optimizations = t.Optimizations,
                    Registry = t.Registry
                })
                .ToList(),
            Optimizations = manifest.Optimizations,
            Properties = manifest.Properties?
                .OrderBy(kvp => kvp.Key)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Deployment = manifest.Deployment,
            CreatedAt = null // Exclude timestamp from hash
        };

        return normalized;
    }

    /// <summary>
    /// Validates that a manifest hash matches the current manifest state.
    /// </summary>
    /// <param name="manifest">The build manifest to validate.</param>
    /// <param name="expectedHash">The expected hash value.</param>
    /// <returns>True if the hash matches; false otherwise.</returns>
    public static bool ValidateHash(BuildManifest manifest, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedHash);

        var actualHash = ComputeHash(manifest);
        return actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Computes a content hash for a specific repository configuration.
    /// Useful for incremental repository cloning.
    /// </summary>
    /// <param name="repository">The repository reference to hash.</param>
    /// <returns>8-character hash of the repository configuration.</returns>
    public static string ComputeRepositoryHash(RepositoryReference repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var json = JsonSerializer.Serialize(repository, NormalizedJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(bytes);

        return Convert.ToHexString(hash[..4]).ToLowerInvariant();
    }
}
