// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Host.Ogc.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Ogc;

/// <summary>
/// Unit tests for FilterParsingCacheService.
/// </summary>
public sealed class FilterParsingCacheServiceTests : IDisposable
{
    private readonly FilterParsingCacheMetrics _metrics;
    private readonly FilterParsingCacheService _cache;
    private readonly ILogger<FilterParsingCacheService> _logger;

    public FilterParsingCacheServiceTests()
    {
        _metrics = new FilterParsingCacheMetrics();
        _logger = new LoggerFactory().CreateLogger<FilterParsingCacheService>();

        var options = Options.Create(new FilterParsingCacheOptions
        {
            Enabled = true,
            MaxEntries = 100,
            MaxSizeBytes = 1024 * 1024, // 1 MB
            SlidingExpirationMinutes = 60
        });

        _cache = new FilterParsingCacheService(options, _metrics, _logger);
    }

    [Fact]
    public void GetOrParse_CachesFilter_OnFirstCall()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var filterText = "population > 100000";
        var parseCount = 0;

        // Act
        var result1 = _cache.GetOrParse(
            filterText,
            "cql-text",
            layer,
            null,
            () =>
            {
                parseCount++;
                return CqlFilterParser.Parse(filterText, layer);
            });

        // Assert
        Assert.NotNull(result1);
        Assert.Equal(1, parseCount); // Should parse once

        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits); // First call is a miss
        Assert.Equal(1, stats.TotalMisses);
    }

    [Fact]
    public void GetOrParse_ReturnsCachedFilter_OnSecondCall()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var filterText = "population > 100000";
        var parseCount = 0;

        // Act - First call (cache miss)
        var result1 = _cache.GetOrParse(
            filterText,
            "cql-text",
            layer,
            null,
            () =>
            {
                parseCount++;
                return CqlFilterParser.Parse(filterText, layer);
            });

        // Act - Second call (cache hit)
        var result2 = _cache.GetOrParse(
            filterText,
            "cql-text",
            layer,
            null,
            () =>
            {
                parseCount++;
                return CqlFilterParser.Parse(filterText, layer);
            });

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, parseCount); // Should only parse once

        var stats = _metrics.GetStatistics();
        Assert.Equal(1, stats.TotalHits); // Second call should hit cache
        Assert.Equal(1, stats.TotalMisses);
        Assert.Equal(0.5, stats.HitRate); // 1 hit out of 2 total requests
    }

    [Fact]
    public void GetOrParse_DifferentFilters_CreatesSeparateEntries()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var filter1 = "population > 100000";
        var filter2 = "name = 'Tokyo'";
        var parseCount = 0;

        // Act
        var result1 = _cache.GetOrParse(filter1, "cql-text", layer, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filter1, layer);
        });

        var result2 = _cache.GetOrParse(filter2, "cql-text", layer, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filter2, layer);
        });

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, parseCount); // Should parse both

        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(2, stats.TotalMisses);
    }

    [Fact]
    public void GetOrParse_DifferentLayers_CreatesSeparateEntries()
    {
        // Arrange
        var layer1 = CreateTestLayer("layer1", new[] { "population" });
        var layer2 = CreateTestLayer("layer2", new[] { "population", "density" }); // Different schema
        var filterText = "population > 100000";
        var parseCount = 0;

        // Act
        var result1 = _cache.GetOrParse(filterText, "cql-text", layer1, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filterText, layer1);
        });

        var result2 = _cache.GetOrParse(filterText, "cql-text", layer2, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filterText, layer2);
        });

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, parseCount); // Should parse for each layer due to different schema

        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(2, stats.TotalMisses);
    }

    [Fact]
    public void GetOrParse_SameLayerSchema_SharesCache()
    {
        // Arrange
        var layer1 = CreateTestLayer("layer1", new[] { "population", "name" });
        var layer2 = CreateTestLayer("layer2", new[] { "population", "name" }); // Same schema
        var filterText = "population > 100000";
        var parseCount = 0;

        // Act
        var result1 = _cache.GetOrParse(filterText, "cql-text", layer1, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filterText, layer1);
        });

        var result2 = _cache.GetOrParse(filterText, "cql-text", layer2, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filterText, layer2);
        });

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, parseCount); // Should reuse cached entry since schema is identical

        var stats = _metrics.GetStatistics();
        Assert.Equal(1, stats.TotalHits);
        Assert.Equal(1, stats.TotalMisses);
    }

    [Fact]
    public void GetOrParse_DifferentCrs_CreatesSeparateEntries()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var filterText = "{\"op\":\"s_intersects\",\"args\":[{\"property\":\"geometry\"},{\"type\":\"Point\",\"coordinates\":[0,0]}]}";
        var parseCount = 0;

        // Act
        var result1 = _cache.GetOrParse(filterText, "cql2-json", layer, "EPSG:4326", () =>
        {
            parseCount++;
            return Cql2JsonParser.Parse(filterText, layer, "EPSG:4326");
        });

        var result2 = _cache.GetOrParse(filterText, "cql2-json", layer, "EPSG:3857", () =>
        {
            parseCount++;
            return Cql2JsonParser.Parse(filterText, layer, "EPSG:3857");
        });

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, parseCount); // Should parse for each CRS

        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(2, stats.TotalMisses);
    }

    [Fact]
    public void GetOrParse_DifferentLanguage_CreatesSeparateEntries()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var cqlText = "population > 100000";
        var cql2Json = "{\"op\":\">\",\"args\":[{\"property\":\"population\"},{\"value\":100000}]}";
        var parseCount = 0;

        // Act
        var result1 = _cache.GetOrParse(cqlText, "cql-text", layer, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(cqlText, layer);
        });

        var result2 = _cache.GetOrParse(cql2Json, "cql2-json", layer, null, () =>
        {
            parseCount++;
            return Cql2JsonParser.Parse(cql2Json, layer, null);
        });

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(2, parseCount); // Should parse both languages separately

        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(2, stats.TotalMisses);
    }

    [Fact]
    public void GetOrParse_ThrowsOnInvalidFilter()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var invalidFilter = "INVALID FILTER SYNTAX!!";

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
        {
            _cache.GetOrParse(invalidFilter, "cql-text", layer, null, () =>
            {
                return CqlFilterParser.Parse(invalidFilter, layer); // Will throw
            });
        });

        // Verify error is not cached
        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits);
        Assert.Equal(0, stats.TotalMisses); // Error should not count as miss
    }

    [Fact]
    public void Clear_RemovesAllCachedEntries()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var filterText = "population > 100000";
        var parseCount = 0;

        // Cache a filter
        _cache.GetOrParse(filterText, "cql-text", layer, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filterText, layer);
        });

        Assert.Equal(1, parseCount);

        // Act - Clear cache
        _cache.Clear();

        // Re-request should trigger parse again
        _cache.GetOrParse(filterText, "cql-text", layer, null, () =>
        {
            parseCount++;
            return CqlFilterParser.Parse(filterText, layer);
        });

        // Assert
        Assert.Equal(2, parseCount); // Should parse twice (before and after clear)

        var stats = _metrics.GetStatistics();
        Assert.Equal(0, stats.TotalHits); // First request was miss, second was miss after clear
        Assert.Equal(2, stats.TotalMisses);
    }

    [Fact]
    public void GetStatistics_ReturnsAccurateMetrics()
    {
        // Arrange
        var layer = CreateTestLayer("layer1");
        var filter1 = "population > 100000";
        var filter2 = "name = 'Tokyo'";

        // Act - Generate cache activity
        _cache.GetOrParse(filter1, "cql-text", layer, null, () => CqlFilterParser.Parse(filter1, layer)); // Miss
        _cache.GetOrParse(filter1, "cql-text", layer, null, () => CqlFilterParser.Parse(filter1, layer)); // Hit
        _cache.GetOrParse(filter2, "cql-text", layer, null, () => CqlFilterParser.Parse(filter2, layer)); // Miss
        _cache.GetOrParse(filter1, "cql-text", layer, null, () => CqlFilterParser.Parse(filter1, layer)); // Hit
        _cache.GetOrParse(filter2, "cql-text", layer, null, () => CqlFilterParser.Parse(filter2, layer)); // Hit

        // Assert
        var stats = _metrics.GetStatistics();
        Assert.Equal(3, stats.TotalHits);
        Assert.Equal(2, stats.TotalMisses);
        Assert.Equal(5, stats.TotalRequests);
        Assert.Equal(0.6, stats.HitRate); // 3/5 = 0.6
        Assert.True(stats.TotalParseTimeMs > 0); // Should have recorded parse time
    }

    public void Dispose()
    {
        _cache.Dispose();
        _metrics.Dispose();
    }

    /// <summary>
    /// Creates a test layer definition with the specified fields.
    /// </summary>
    private static LayerDefinition CreateTestLayer(string layerId, string[]? fieldNames = null)
    {
        fieldNames ??= new[] { "id", "name", "population" };

        var fields = new List<FieldDefinition>();
        foreach (var fieldName in fieldNames)
        {
            var dataType = fieldName switch
            {
                "id" => "integer",
                "population" => "integer",
                "density" => "double",
                _ => "string"
            };

            fields.Add(new FieldDefinition
            {
                Name = fieldName,
                DataType = dataType,
                StorageType = dataType,
                Nullable = fieldName != "id"
            });
        }

        return new LayerDefinition
        {
            Id = layerId,
            ServiceId = "test-service",
            Title = $"Test Layer {layerId}",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geometry",
            Fields = fields
        };
    }
}

/// <summary>
/// Field definition for testing.
/// </summary>
public sealed record FieldDefinition
{
    public required string Name { get; init; }
    public string? DataType { get; init; }
    public string? StorageType { get; init; }
    public bool Nullable { get; init; } = true;
}
