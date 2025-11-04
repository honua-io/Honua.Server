using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Fake implementation of <see cref="IFeatureRepository"/> for OGC handler testing.
/// Contains 2 sample road features with LineString geometries.
/// </summary>
/// <remarks>
/// This repository provides realistic test data for OGC handler integration tests:
/// - Feature 1: road_id=1, name="First", LineString from [-122.4,45.6] to [-122.39,45.61]
/// - Feature 2: road_id=2, name="Second", LineString from [-122.41,45.61] to [-122.4,45.62]
/// </remarks>
public sealed class FakeFeatureRepository : IFeatureRepository
{
    private readonly List<FeatureRecord> _records = new()
    {
        new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["road_id"] = 1,
            ["name"] = "First",
            ["geom"] = ParseGeometry("{\"type\":\"LineString\",\"coordinates\":[[-122.4,45.6],[-122.39,45.61]]}")
        }),
        new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["road_id"] = 2,
            ["name"] = "Second",
            ["geom"] = ParseGeometry("{\"type\":\"LineString\",\"coordinates\":[[-122.41,45.61],[-122.4,45.62]]}")
        })
    };

    private static JsonNode ParseGeometry(string json)
    {
        return JsonNode.Parse(json) ?? throw new InvalidOperationException("Invalid geometry JSON.");
    }

    public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Create operation not supported in fake repository.");

    public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Delete operation not supported in fake repository.");

    public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return Task.FromResult<FeatureRecord?>(null);
        }

        var match = _records.Find(r => Convert.ToInt32(r.Attributes["road_id"], CultureInfo.InvariantCulture) == id);
        return Task.FromResult(match);
    }

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        string serviceId,
        string layerId,
        FeatureQuery? query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var record in _records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }

        await Task.CompletedTask;
    }

    public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Update operation not supported in fake repository.");

    public Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
        => Task.FromResult((long)_records.Count);

    public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        string serviceId,
        string layerId,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());

    public Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default)
        => Task.FromResult<BoundingBox?>(null);
}
