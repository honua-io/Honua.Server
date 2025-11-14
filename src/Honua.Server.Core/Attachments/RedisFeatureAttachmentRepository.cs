// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

/// <summary>
/// Redis-backed implementation of IFeatureAttachmentRepository.
/// Stores attachment metadata in Redis for distributed deployments.
/// </summary>
public sealed class RedisFeatureAttachmentRepository : IFeatureAttachmentRepository, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisFeatureAttachmentRepository> _logger;
    private readonly string _keyPrefix;
    private readonly TimeSpan _defaultTtl;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisFeatureAttachmentRepository(
        IConnectionMultiplexer redis,
        ILogger<RedisFeatureAttachmentRepository> logger,
        string keyPrefix = "honua:attachment:",
        TimeSpan? defaultTtl = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyPrefix = keyPrefix;
        _defaultTtl = defaultTtl ?? TimeSpan.FromDays(90); // Longer TTL for attachments
        _database = _redis.GetDatabase();
        _jsonOptions = JsonSerializerOptionsRegistry.Web;

        _logger.LogInformation(
            "RedisFeatureAttachmentRepository initialized with prefix: {KeyPrefix}, TTL: {TTL}",
            _keyPrefix,
            _defaultTtl);
    }

    public async Task<AttachmentDescriptor?> FindByIdAsync(
        string serviceId,
        string layerId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var key = BuildKey(serviceId, layerId, attachmentId);
            var json = await _database.StringGetAsync(key);

            if (json.IsNullOrEmpty)
            {
                _logger.LogDebug("Attachment not found: {Key}", key);
                return null;
            }

            var descriptor = JsonSerializer.Deserialize<AttachmentDescriptor>(json!, _jsonOptions);
            _logger.LogDebug("Retrieved attachment: {AttachmentId}", attachmentId);
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding attachment by ID: {AttachmentId}", attachmentId);
            throw;
        }
    }

    public async Task<IReadOnlyList<AttachmentDescriptor>> ListByFeatureAsync(
        string serviceId,
        string layerId,
        string featureId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureSetKey = GetFeatureSetKey(serviceId, layerId, featureId);
            var attachmentIds = await _database.SetMembersAsync(featureSetKey);

            if (attachmentIds.Length == 0)
            {
                return Array.Empty<AttachmentDescriptor>();
            }

            // Use MGET for batch retrieval (single round trip instead of N queries)
            var keys = attachmentIds.Select(id => (RedisKey)BuildKey(serviceId, layerId, id.ToString()!)).ToArray();
            var values = await _database.StringGetAsync(keys);

            var results = new List<AttachmentDescriptor>();
            var staleIds = new List<RedisValue>();

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].HasValue)
                {
                    var attachment = JsonSerializer.Deserialize<AttachmentDescriptor>(values[i]!, _jsonOptions);
                    if (attachment != null)
                    {
                        results.Add(attachment);
                    }
                }
                else
                {
                    // Track stale entry for cleanup
                    staleIds.Add(attachmentIds[i]);
                }
            }

            // Clean up stale entries in batch if any found
            if (staleIds.Count > 0)
            {
                await _database.SetRemoveAsync(featureSetKey, staleIds.ToArray());
            }

            var sorted = results.OrderBy(a => a.CreatedUtc).ToList();
            _logger.LogDebug(
                "Retrieved {Count} attachments for feature: {ServiceId}/{LayerId}/{FeatureId}",
                sorted.Count,
                serviceId,
                layerId,
                featureId);

            return sorted;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error listing attachments for feature: {ServiceId}/{LayerId}/{FeatureId}",
                serviceId,
                layerId,
                featureId);
            throw;
        }
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<AttachmentDescriptor>>> ListByFeaturesAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(featureIds);

        if (featureIds.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var result = new Dictionary<string, IReadOnlyList<AttachmentDescriptor>>(StringComparer.OrdinalIgnoreCase);

            // Batch retrieve all feature set keys to get attachment IDs
            var featureSetKeys = featureIds
                .Select(fid => (RedisKey)GetFeatureSetKey(serviceId, layerId, fid))
                .ToArray();

            // Use Redis pipeline for parallel execution
            var batch = _database.CreateBatch();
            var setMembersTasks = featureSetKeys
                .Select(key => batch.SetMembersAsync(key))
                .ToArray();
            batch.Execute();

            await Task.WhenAll(setMembersTasks);

            // Collect all unique attachment IDs and map to feature IDs
            var attachmentIdToFeatureIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var allAttachmentKeys = new HashSet<RedisKey>();

            for (int i = 0; i < featureIds.Count; i++)
            {
                // SAFE .Result ACCESS: We already awaited Task.WhenAll(setMembersTasks) above (line 172),
                // so all tasks are guaranteed to be completed. Accessing .Result on completed tasks doesn't block.
                var attachmentIds = setMembersTasks[i].Result;
                if (attachmentIds.Length == 0)
                {
                    result[featureIds[i]] = Array.Empty<AttachmentDescriptor>();
                    continue;
                }

                foreach (var attachmentId in attachmentIds)
                {
                    var attachmentIdStr = attachmentId.ToString()!;
                    var key = (RedisKey)BuildKey(serviceId, layerId, attachmentIdStr);
                    allAttachmentKeys.Add(key);

                    if (!attachmentIdToFeatureIds.TryGetValue(attachmentIdStr, out var list))
                    {
                        list = new List<string>();
                        attachmentIdToFeatureIds[attachmentIdStr] = list;
                    }
                    list.Add(featureIds[i]);
                }
            }

            // Batch retrieve all attachment descriptors using MGET
            if (allAttachmentKeys.Count > 0)
            {
                var keys = allAttachmentKeys.ToArray();
                var values = await _database.StringGetAsync(keys);

                var attachments = new List<AttachmentDescriptor>();
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i].HasValue)
                    {
                        var attachment = JsonSerializer.Deserialize<AttachmentDescriptor>(values[i]!, _jsonOptions);
                        if (attachment != null)
                        {
                            attachments.Add(attachment);
                        }
                    }
                }

                // Group attachments by feature ID
                foreach (var attachment in attachments)
                {
                    if (!result.TryGetValue(attachment.FeatureId, out var list))
                    {
                        result[attachment.FeatureId] = new List<AttachmentDescriptor> { attachment };
                    }
                    else
                    {
                        ((List<AttachmentDescriptor>)list).Add(attachment);
                    }
                }

                // Sort each feature's attachments by creation date
                foreach (var featureId in result.Keys.ToList())
                {
                    if (result[featureId] is List<AttachmentDescriptor> list && list.Count > 1)
                    {
                        list.Sort((a, b) => a.CreatedUtc.CompareTo(b.CreatedUtc));
                    }
                }
            }

            // Ensure all requested feature IDs are in the result
            foreach (var featureId in featureIds)
            {
                if (!result.ContainsKey(featureId))
                {
                    result[featureId] = Array.Empty<AttachmentDescriptor>();
                }
            }

            _logger.LogDebug(
                "Retrieved attachments for {FeatureCount} features with {TotalAttachments} total attachments",
                featureIds.Count,
                result.Values.Sum(v => v.Count));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error listing attachments for {FeatureCount} features in {ServiceId}/{LayerId}",
                featureIds.Count,
                serviceId,
                layerId);
            throw;
        }
    }

    public async Task<AttachmentDescriptor> CreateAsync(
        AttachmentDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(descriptor);

        try
        {
            var counterKey = $"{_keyPrefix}counter:{descriptor.ServiceId}:{descriptor.LayerId}";
            var nextId = await _database.StringIncrementAsync(counterKey);

            var finalDescriptor = descriptor.AttachmentObjectId != 0
                ? descriptor
                : descriptor with { AttachmentObjectId = (int)nextId };

            var key = BuildKey(descriptor.ServiceId, descriptor.LayerId, descriptor.AttachmentId);
            var json = JsonSerializer.Serialize(finalDescriptor, _jsonOptions);
            await _database.StringSetAsync(key, json, _defaultTtl);

            // Add to feature index
            var featureSetKey = GetFeatureSetKey(descriptor.ServiceId, descriptor.LayerId, descriptor.FeatureId);
            await _database.SetAddAsync(featureSetKey, descriptor.AttachmentId);

            _logger.LogInformation(
                "Created attachment: {AttachmentId} for feature {FeatureId}",
                descriptor.AttachmentId,
                descriptor.FeatureId);

            return finalDescriptor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating attachment: {AttachmentId}", descriptor.AttachmentId);
            throw;
        }
    }

    public async Task<AttachmentDescriptor?> UpdateAsync(
        AttachmentDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(descriptor);

        try
        {
            var key = BuildKey(descriptor.ServiceId, descriptor.LayerId, descriptor.AttachmentId);
            var exists = await _database.KeyExistsAsync(key);

            if (!exists)
            {
                _logger.LogWarning("Cannot update non-existent attachment: {AttachmentId}", descriptor.AttachmentId);
                return null;
            }

            var json = JsonSerializer.Serialize(descriptor, _jsonOptions);
            await _database.StringSetAsync(key, json, _defaultTtl);

            _logger.LogInformation("Updated attachment: {AttachmentId}", descriptor.AttachmentId);
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attachment: {AttachmentId}", descriptor.AttachmentId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(
        string serviceId,
        string layerId,
        string attachmentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the descriptor to find the feature ID
            var descriptor = await FindByIdAsync(serviceId, layerId, attachmentId, cancellationToken);
            if (descriptor == null)
            {
                return false;
            }

            var key = BuildKey(serviceId, layerId, attachmentId);
            var deleted = await _database.KeyDeleteAsync(key);

            // Remove from feature index
            var featureSetKey = GetFeatureSetKey(serviceId, layerId, descriptor.FeatureId);
            await _database.SetRemoveAsync(featureSetKey, attachmentId);

            if (deleted)
            {
                _logger.LogInformation("Deleted attachment: {AttachmentId}", attachmentId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting attachment: {AttachmentId}", attachmentId);
            throw;
        }
    }

    public Task<IFeatureAttachmentTransaction> BeginTransactionAsync(
        string serviceId,
        string layerId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IFeatureAttachmentTransaction>(
            new RedisAttachmentTransaction(this, serviceId, layerId));
    }

    private string BuildKey(string serviceId, string layerId, string attachmentId)
    {
        return $"{_keyPrefix}{serviceId}:{layerId}:{attachmentId}";
    }

    private string GetFeatureSetKey(string serviceId, string layerId, string featureId)
    {
        return $"{_keyPrefix}feature:{serviceId}:{layerId}:{featureId}";
    }

    public void Dispose()
    {
        // Redis connection is managed by DI, don't dispose it here
    }

    private sealed class RedisAttachmentTransaction : IFeatureAttachmentTransaction
    {
        private readonly RedisFeatureAttachmentRepository _repository;
        private readonly string _serviceId;
        private readonly string _layerId;

        public RedisAttachmentTransaction(
            RedisFeatureAttachmentRepository repository,
            string serviceId,
            string layerId)
        {
            _repository = repository;
            _serviceId = serviceId;
            _layerId = layerId;
        }

        public Task<AttachmentDescriptor> CreateAsync(
            AttachmentDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return _repository.CreateAsync(descriptor, cancellationToken);
        }

        public Task<AttachmentDescriptor?> UpdateAsync(
            AttachmentDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return _repository.UpdateAsync(descriptor, cancellationToken);
        }

        public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
        {
            return _repository.DeleteAsync(_serviceId, _layerId, attachmentId, cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            // Redis operations are immediately committed, no explicit commit needed
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
