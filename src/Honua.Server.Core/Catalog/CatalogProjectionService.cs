// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Catalog;

public interface ICatalogProjectionService : IDisposable
{
    CatalogProjectionSnapshot GetSnapshot();
    IReadOnlyList<CatalogGroupView> GetGroups();
    CatalogGroupView? GetGroup(string groupId);
    CatalogServiceView? GetService(string serviceId);
    CatalogDiscoveryRecord? GetRecord(string recordId);
    IReadOnlyList<CatalogDiscoveryRecord> Search(string? query, string? groupId = null, int limit = 100, int offset = 0);
    Task WarmupAsync(CancellationToken cancellationToken = default);
}

public sealed class CatalogProjectionService : ICatalogProjectionService
{
    private const string CacheKey = "honua:catalog:projection";

    private readonly IMetadataRegistry _registry;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CatalogProjectionService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly IDisposable _metadataSubscription;

    private CatalogProjectionSnapshot? _snapshot;

    public CatalogProjectionService(
        IMetadataRegistry registry,
        IMemoryCache cache,
        ILogger<CatalogProjectionService> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _metadataSubscription = ChangeToken.OnChange(_registry.GetChangeToken, ScheduleRefresh);
    }

    public CatalogProjectionSnapshot GetSnapshot()
    {
        using var measurement = PerformanceMeasurement.Measure(_logger, "GetCatalogSnapshot", LogLevel.Debug);
        return ActivityScope.Execute(
            HonuaTelemetry.Metadata,
            "CatalogGetSnapshot",
            [("catalog.operation", "GetSnapshot")],
            activity =>
            {
                var cachedSnapshot = Volatile.Read(ref _snapshot);
                if (cachedSnapshot is not null)
                {
                    activity.AddTag("catalog.source", "memory");
                    activity.AddTag("catalog.record_count", cachedSnapshot.RecordIndex.Count);
                    _logger.LogDebug("Catalog snapshot retrieved from memory cache: {RecordCount} records",
                        cachedSnapshot.RecordIndex.Count);
                    return cachedSnapshot;
                }

                if (_cache.TryGetValue(CacheKey, out CatalogProjectionSnapshot snapshotFromCache) && snapshotFromCache is not null)
                {
                    Volatile.Write(ref _snapshot, snapshotFromCache);
                    activity.AddTag("catalog.source", "cache");
                    activity.AddTag("catalog.record_count", snapshotFromCache.RecordIndex.Count);
                    _logger.LogDebug("Catalog snapshot retrieved from memory cache: {RecordCount} records",
                        snapshotFromCache.RecordIndex.Count);
                    return snapshotFromCache;
                }

                _logger.LogError("Catalog projection not initialized");
                throw new InvalidOperationException(
                    "Catalog projection has not been initialized. Call WarmupAsync during startup to prime the cache.");
            });
    }

    public IReadOnlyList<CatalogGroupView> GetGroups() => GetSnapshot().Groups;

    public CatalogGroupView? GetGroup(string groupId)
    {
        if (groupId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var snapshot = GetSnapshot();
        return snapshot.GroupIndex.TryGetValue(groupId, out var group)
            ? group
            : null;
    }

    public CatalogServiceView? GetService(string serviceId)
    {
        if (serviceId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var snapshot = GetSnapshot();
        return snapshot.ServiceIndex.TryGetValue(serviceId, out var service)
            ? service
            : null;
    }

    public CatalogDiscoveryRecord? GetRecord(string recordId)
    {
        if (recordId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var snapshot = GetSnapshot();
        return snapshot.RecordIndex.TryGetValue(recordId, out var record)
            ? record
            : null;
    }

    /// <summary>
    /// Searches catalog records by query terms and optional group filter with pagination.
    /// </summary>
    /// <param name="query">Search query containing space-separated terms to match against title, summary, keywords, etc.</param>
    /// <param name="groupId">Optional group/folder ID to filter results.</param>
    /// <param name="limit">Maximum number of results to return (default 100, max 1000).</param>
    /// <param name="offset">Number of results to skip for pagination (default 0).</param>
    /// <returns>List of matching catalog discovery records.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when limit or offset is out of valid range.</exception>
    public IReadOnlyList<CatalogDiscoveryRecord> Search(string? query, string? groupId = null, int limit = 100, int offset = 0)
    {
        using var measurement = PerformanceMeasurement.Measure(_logger, "CatalogSearch", LogLevel.Information);
        return ActivityScope.Execute(
            HonuaTelemetry.Metadata,
            "CatalogSearch",
            [
                ("catalog.operation", "Search"),
                ("catalog.query", query ?? "(none)"),
                ("catalog.group", groupId ?? "(all)"),
                ("catalog.limit", limit),
                ("catalog.offset", offset)
            ],
            activity =>
            {
                if (limit <= 0 || limit > 1000)
                {
                    throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 1 and 1000.");
                }

                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be non-negative.");
                }

                _logger.LogInformation("Catalog search started: query={Query}, group={Group}, limit={Limit}, offset={Offset}",
                    query ?? "(none)", groupId ?? "(all)", limit, offset);

                var snapshot = GetSnapshot();
                IEnumerable<CatalogDiscoveryRecord> records = snapshot.RecordIndex.Values;

                if (groupId.HasValue())
                {
                    records = records.Where(r => string.Equals(r.GroupId, groupId, StringComparison.OrdinalIgnoreCase));
                }

                var terms = NormalizeSearchTerms(query);
                activity.AddTag("catalog.term_count", terms.Count);

                if (terms.Count > 0)
                {
                    records = records.Where(r => MatchesAllTerms(r, terms));
                }

                var results = records
                    .OrderBy(r => OrderingKey(r.Ordering))
                    .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                    .Skip(offset)
                    .Take(limit)
                    .ToList();

                activity.AddTag("catalog.result_count", results.Count);
                _logger.LogInformation("Catalog search completed: query={Query}, group={Group}, results={ResultCount}",
                    query ?? "(none)", groupId ?? "(all)", results.Count);

                return results;
            });
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Catalog projection warmup started");

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ActivityScope.ExecuteAsync(
                HonuaTelemetry.Metadata,
                "CatalogWarmup",
                [("catalog.operation", "Warmup")],
                async activity =>
                {
                    await PerformanceMeasurement.MeasureAsync(
                        _logger,
                        "CatalogWarmup",
                        async () =>
                        {
                            var metadata = await _registry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
                            var snapshot = BuildProjection(metadata);

                            activity.AddTag("catalog.group_count", snapshot.Groups.Count);
                            activity.AddTag("catalog.service_count", snapshot.ServiceIndex.Count);
                            activity.AddTag("catalog.record_count", snapshot.RecordIndex.Count);

                            var cacheEntryOptions = CacheOptionsBuilder.ForCatalogProjection()
                                .WithSize(1)
                                .BuildMemory();
                            cacheEntryOptions.AddExpirationToken(_registry.GetChangeToken());

                            _cache.Set(CacheKey, snapshot, cacheEntryOptions);
                            Volatile.Write(ref _snapshot, snapshot);

                            _logger.LogInformation(
                                "Catalog projection warmed successfully: {GroupCount} groups, {ServiceCount} services, {RecordCount} records",
                                snapshot.Groups.Count,
                                snapshot.ServiceIndex.Count,
                                snapshot.RecordIndex.Count);
                        },
                        LogLevel.Information).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build catalog projection snapshot: {Error}", ex.Message);
            throw;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _metadataSubscription.Dispose();
        _refreshLock.Dispose();
    }

    private void ScheduleRefresh()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await WarmupAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog projection refresh failed; continuing with the previous snapshot.");
            }
        });
    }

    private static CatalogProjectionSnapshot BuildProjection(MetadataSnapshot metadata)
    {
        // Pre-allocate dictionaries with capacity hints based on known counts to reduce reallocations
        var folderCount = metadata.Folders.Count;
        var serviceCount = metadata.Services.Count;

        var folderOrderLookup = new Dictionary<string, int>(folderCount, StringComparer.OrdinalIgnoreCase);
        var folderTitleLookup = new Dictionary<string, string>(folderCount, StringComparer.OrdinalIgnoreCase);
        var groupBuffers = new Dictionary<string, List<CatalogServiceView>>(folderCount, StringComparer.OrdinalIgnoreCase);

        foreach (var folder in metadata.Folders)
        {
            folderOrderLookup[folder.Id] = folder.Order ?? int.MaxValue;
            folderTitleLookup[folder.Id] = folder.Title.IsNullOrWhiteSpace() ? folder.Id : folder.Title!;
            groupBuffers[folder.Id] = new List<CatalogServiceView>();
        }

        var serviceIndex = new Dictionary<string, CatalogServiceView>(serviceCount, StringComparer.OrdinalIgnoreCase);
        // Estimate record count: services × average layers per service (assume ~3 layers per service)
        var estimatedRecordCount = serviceCount * 3;
        var recordIndex = new Dictionary<string, CatalogDiscoveryRecord>(estimatedRecordCount, StringComparer.OrdinalIgnoreCase);

        foreach (var service in metadata.Services)
        {
            if (!service.Enabled)
            {
                continue;
            }

            if (!folderTitleLookup.TryGetValue(service.FolderId, out var folderTitle))
            {
                folderTitle = service.FolderId;
                folderTitleLookup[service.FolderId] = folderTitle;
            }

            if (!folderOrderLookup.ContainsKey(service.FolderId))
            {
                folderOrderLookup[service.FolderId] = int.MaxValue;
            }

            if (!groupBuffers.TryGetValue(service.FolderId, out var serviceBuffer))
            {
                serviceBuffer = new List<CatalogServiceView>();
                groupBuffers[service.FolderId] = serviceBuffer;
            }

            var layerViews = service.Layers
                .Select(layer => BuildLayerView(metadata, service, layer))
                .OrderBy(l => OrderingKey(l.Ordering))
                .ThenBy(l => l.Layer.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var readOnlyLayers = new ReadOnlyCollection<CatalogLayerView>(layerViews);
            var serviceKeywords = CombineKeywords(service.Catalog.Keywords, service.Keywords, metadata.Catalog.Keywords);
            var serviceLinks = CombineLinks(service.Catalog.Links, service.Links, metadata.Catalog.Links);
            var serviceSummary = ResolveServiceSummary(service);

            var serviceView = new CatalogServiceView
            {
                Service = service,
                FolderTitle = folderTitle,
                FolderOrder = folderOrderLookup[service.FolderId],
                Summary = serviceSummary,
                Keywords = serviceKeywords,
                Links = serviceLinks,
                Layers = readOnlyLayers
            };

            serviceIndex[service.Id] = serviceView;
            serviceBuffer.Add(serviceView);

            foreach (var layerView in readOnlyLayers)
            {
                var recordId = $"{service.Id}:{layerView.Layer.Id}";
                var recordKeywords = CombineKeywords(layerView.Keywords, serviceKeywords, metadata.Catalog.Keywords);
                var recordLinks = CombineLinks(layerView.Links, serviceLinks);
                var recordContacts = layerView.Contacts.Count > 0
                    ? layerView.Contacts
                    : CombineContacts(service.Catalog.Contacts, ToContacts(metadata.Catalog.Contact));

                var record = new CatalogDiscoveryRecord
                {
                    Id = recordId,
                    GroupId = service.FolderId,
                    GroupTitle = folderTitle,
                    ServiceId = service.Id,
                    ServiceTitle = service.Title,
                    ServiceType = service.ServiceType,
                    LayerId = layerView.Layer.Id,
                    Title = layerView.Layer.Title,
                    Summary = layerView.Summary,
                    Keywords = recordKeywords,
                    Themes = layerView.Themes ?? Array.Empty<string>(),
                    Links = recordLinks,
                    Contacts = recordContacts,
                    SpatialExtent = layerView.SpatialExtent,
                    TemporalExtent = layerView.TemporalExtent,
                    Ordering = layerView.Ordering,
                    Thumbnail = layerView.Thumbnail
                };

                recordIndex[recordId] = record;
            }
        }

        var groups = new List<CatalogGroupView>();
        foreach (var (groupId, services) in groupBuffers)
        {
            var orderedServices = services
                .OrderBy(s => OrderingKey(s.FolderOrder))
                .ThenBy(s => s.Service.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            groups.Add(new CatalogGroupView
            {
                Id = groupId,
                Title = folderTitleLookup[groupId],
                Order = folderOrderLookup.TryGetValue(groupId, out var order) ? order : null,
                Services = new ReadOnlyCollection<CatalogServiceView>(orderedServices)
            });
        }

        groups = groups
            .OrderBy(g => OrderingKey(g.Order))
            .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Build group index dictionary with capacity hint
        var groupIndexDict = new Dictionary<string, CatalogGroupView>(groups.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            groupIndexDict[group.Id] = group;
        }

        return new CatalogProjectionSnapshot(
            new ReadOnlyCollection<CatalogGroupView>(groups),
            new ReadOnlyDictionary<string, CatalogGroupView>(groupIndexDict),
            new ReadOnlyDictionary<string, CatalogServiceView>(serviceIndex),
            new ReadOnlyDictionary<string, CatalogDiscoveryRecord>(recordIndex));
    }

    private static CatalogLayerView BuildLayerView(MetadataSnapshot metadata, ServiceDefinition service, LayerDefinition layer)
    {
        var keywords = CombineKeywords(layer.Catalog.Keywords, layer.Keywords, metadata.Catalog.Keywords);
        var links = CombineLinks(layer.Catalog.Links, layer.Links, service.Catalog.Links);
        var contacts = layer.Catalog.Contacts.Count > 0
            ? layer.Catalog.Contacts
            : CombineContacts(service.Catalog.Contacts, ToContacts(metadata.Catalog.Contact));
        var summary = ResolveLayerSummary(layer);

        return new CatalogLayerView
        {
            Layer = layer,
            Summary = summary,
            Keywords = keywords,
            Links = links,
            Contacts = contacts,
            SpatialExtent = layer.Catalog.SpatialExtent,
            TemporalExtent = layer.Catalog.TemporalExtent,
            Ordering = layer.Catalog.Ordering,
            Thumbnail = layer.Catalog.Thumbnail,
            Themes = layer.Catalog.Themes ?? Array.Empty<string>(),
            Relationships = layer.Relationships,
            MinScale = layer.MinScale,
            MaxScale = layer.MaxScale
        };
    }

    /// <summary>
    /// Normalizes and validates search terms from a query string.
    /// Applies security limits to prevent ReDoS and memory exhaustion attacks.
    /// </summary>
    /// <param name="query">The search query string.</param>
    /// <returns>List of normalized search terms.</returns>
    /// <exception cref="ArgumentException">Thrown when query exceeds security limits.</exception>
    private static IReadOnlyList<string> NormalizeSearchTerms(string? query)
    {
        const int MaxQueryLength = 500;
        const int MaxTermCount = 50;
        const int MaxTermLength = 100;

        if (query.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        if (query.Length > MaxQueryLength)
        {
            throw new ArgumentException($"Query exceeds maximum length of {MaxQueryLength} characters.", nameof(query));
        }

        var terms = query
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (terms.Count > MaxTermCount)
        {
            throw new ArgumentException($"Query contains too many terms. Maximum allowed: {MaxTermCount}.", nameof(query));
        }

        foreach (var term in terms)
        {
            if (term.Length > MaxTermLength)
            {
                throw new ArgumentException($"Search term exceeds maximum length of {MaxTermLength} characters: {term.Substring(0, Math.Min(20, term.Length))}...", nameof(query));
            }
        }

        return new ReadOnlyCollection<string>(terms);
    }

    private static bool MatchesAllTerms(CatalogDiscoveryRecord record, IReadOnlyList<string> terms)
    {
        foreach (var term in terms)
        {
            if (!MatchesTerm(record, term))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesTerm(CatalogDiscoveryRecord record, string term)
    {
        if (record.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (record.Summary.HasValue() && record.Summary.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (record.ServiceTitle.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (record.GroupTitle.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (record.Keywords.Any(k => k.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (record.Themes.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> CombineKeywords(params IEnumerable<string>[] sources)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var value in source)
            {
                if (value.IsNullOrWhiteSpace())
                {
                    continue;
                }

                set.Add(value.Trim());
            }
        }

        if (set.Count == 0)
        {
            return Array.Empty<string>();
        }

        return new ReadOnlyCollection<string>(set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList());
    }

    private static IReadOnlyList<CatalogContactDefinition> CombineContacts(params IEnumerable<CatalogContactDefinition>[] sources)
    {
        var list = new List<CatalogContactDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var contact in source)
            {
                if (contact is null)
                {
                    continue;
                }

                var key = $"{contact.Name}|{contact.Email}|{contact.Organization}|{contact.Role}";
                if (seen.Add(key))
                {
                    list.Add(contact);
                }
            }
        }

        if (list.Count == 0)
        {
            return Array.Empty<CatalogContactDefinition>();
        }

        return new ReadOnlyCollection<CatalogContactDefinition>(list);
    }

    private static IReadOnlyList<LinkDefinition> CombineLinks(params IEnumerable<LinkDefinition>[] sources)
    {
        var list = new List<LinkDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            foreach (var link in source)
            {
                if (link is null)
                {
                    continue;
                }

                if (seen.Add(link.Href))
                {
                    list.Add(link);
                }
            }
        }

        if (list.Count == 0)
        {
            return Array.Empty<LinkDefinition>();
        }

        return new ReadOnlyCollection<LinkDefinition>(list);
    }

    private static IReadOnlyList<CatalogContactDefinition> ToContacts(CatalogContactDefinition? contact)
    {
        if (contact is null)
        {
            return Array.Empty<CatalogContactDefinition>();
        }

        return new ReadOnlyCollection<CatalogContactDefinition>(new List<CatalogContactDefinition> { contact });
    }

    private static string? ResolveServiceSummary(ServiceDefinition service)
    {
        if (service.Catalog.Summary.HasValue())
        {
            return service.Catalog.Summary;
        }

        if (service.Description.HasValue())
        {
            return service.Description;
        }

        return null;
    }

    private static string? ResolveLayerSummary(LayerDefinition layer)
    {
        if (layer.Catalog.Summary.HasValue())
        {
            return layer.Catalog.Summary;
        }

        if (layer.Description.HasValue())
        {
            return layer.Description;
        }

        return null;
    }

    private static int OrderingKey(int? order) => order ?? int.MaxValue;
}

public sealed class CatalogProjectionSnapshot
{
    public CatalogProjectionSnapshot(
        IReadOnlyList<CatalogGroupView> groups,
        IReadOnlyDictionary<string, CatalogGroupView> groupIndex,
        IReadOnlyDictionary<string, CatalogServiceView> serviceIndex,
        IReadOnlyDictionary<string, CatalogDiscoveryRecord> recordIndex)
    {
        Groups = groups ?? throw new ArgumentNullException(nameof(groups));
        GroupIndex = groupIndex ?? throw new ArgumentNullException(nameof(groupIndex));
        ServiceIndex = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));
        RecordIndex = recordIndex ?? throw new ArgumentNullException(nameof(recordIndex));
    }

    public IReadOnlyList<CatalogGroupView> Groups { get; }
    public IReadOnlyDictionary<string, CatalogGroupView> GroupIndex { get; }
    public IReadOnlyDictionary<string, CatalogServiceView> ServiceIndex { get; }
    public IReadOnlyDictionary<string, CatalogDiscoveryRecord> RecordIndex { get; }
}

public sealed record CatalogGroupView
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int? Order { get; init; }
    public IReadOnlyList<CatalogServiceView> Services { get; init; } = Array.Empty<CatalogServiceView>();
}

public sealed record CatalogServiceView
{
    public required ServiceDefinition Service { get; init; }
    public required string FolderTitle { get; init; }
    public int FolderOrder { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public IReadOnlyList<CatalogLayerView> Layers { get; init; } = Array.Empty<CatalogLayerView>();
}

public sealed record CatalogLayerView
{
    public required LayerDefinition Layer { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CatalogContactDefinition> Contacts { get; init; } = Array.Empty<CatalogContactDefinition>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public CatalogSpatialExtentDefinition? SpatialExtent { get; init; }
    public CatalogTemporalExtentDefinition? TemporalExtent { get; init; }
    public string? Thumbnail { get; init; }
    public int? Ordering { get; init; }
    public IReadOnlyList<LayerRelationshipDefinition> Relationships { get; init; } = Array.Empty<LayerRelationshipDefinition>();
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
}

public sealed record CatalogDiscoveryRecord
{
    public required string Id { get; init; }
    public required string GroupId { get; init; }
    public required string GroupTitle { get; init; }
    public required string ServiceId { get; init; }
    public required string ServiceTitle { get; init; }
    public required string ServiceType { get; init; }
    public required string LayerId { get; init; }
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CatalogContactDefinition> Contacts { get; init; } = Array.Empty<CatalogContactDefinition>();
    public IReadOnlyList<LinkDefinition> Links { get; init; } = Array.Empty<LinkDefinition>();
    public CatalogSpatialExtentDefinition? SpatialExtent { get; init; }
    public CatalogTemporalExtentDefinition? TemporalExtent { get; init; }
    public string? Thumbnail { get; init; }
    public int? Ordering { get; init; }
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
}
