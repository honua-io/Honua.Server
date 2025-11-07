// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.ETL.Caching;

/// <summary>
/// Interface for caching workflow data
/// </summary>
public interface IWorkflowCache
{
    /// <summary>
    /// Gets a cached value
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets a cached value with optional TTL
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Removes a cached value
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key exists in cache
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a cached value
    /// </summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Clears all cached values (use with caution)
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Cache configuration options
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Whether caching is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Cache provider (Memory, Redis, Distributed)
    /// </summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>
    /// Default TTL in minutes
    /// </summary>
    public int DefaultTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Maximum memory size in MB (for memory cache)
    /// </summary>
    public int MaxMemorySizeMB { get; set; } = 512;

    /// <summary>
    /// Redis connection string (for Redis cache)
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Cache key prefix
    /// </summary>
    public string KeyPrefix { get; set; } = "geoetl:";
}

/// <summary>
/// Cache key builder for consistent key generation
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "geoetl";

    public static string WorkflowDefinition(Guid workflowId) => $"{Prefix}:workflow:def:{workflowId}";
    public static string WorkflowRun(Guid runId) => $"{Prefix}:workflow:run:{runId}";
    public static string Template(string templateId) => $"{Prefix}:template:{templateId}";
    public static string TemplateCatalog() => $"{Prefix}:template:catalog";
    public static string NodeRegistry() => $"{Prefix}:node:registry";
    public static string FeatureSchema(Guid workflowId, string nodeId) => $"{Prefix}:schema:{workflowId}:{nodeId}";
    public static string AiGeneratedWorkflow(string prompt) => $"{Prefix}:ai:workflow:{GetHash(prompt)}";
    public static string CircuitBreakerState(string service) => $"{Prefix}:circuit:{service}";

    private static string GetHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
