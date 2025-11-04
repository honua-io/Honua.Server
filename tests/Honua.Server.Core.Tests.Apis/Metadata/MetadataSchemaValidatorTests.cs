using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class MetadataSchemaValidatorTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    [Fact]
    public void Validate_ShouldSucceed_ForMinimalCompositeMetadata()
    {
        var validator = MetadataSchemaValidator.CreateDefault();
        var payload = CreateSampleMetadataPayload();

        var result = validator.Validate(payload);

        result.IsValid.Should().BeTrue(result.Errors.Count == 0 ? null : string.Join("; ", result.Errors));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldReportMissingSections()
    {
        var validator = MetadataSchemaValidator.CreateDefault();
        var metadata = new
        {
            catalog = new { id = "catalog" },
            folders = Array.Empty<object>()
        };

        var payload = JsonSerializer.Serialize(metadata, SerializerOptions);

        var result = validator.Validate(payload);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.Contains("services", StringComparison.OrdinalIgnoreCase));
        result.Errors.Should().Contain(e => e.Contains("layers", StringComparison.OrdinalIgnoreCase));
        result.Errors.Should().Contain(e => e.Contains("dataSources", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_ShouldBeThreadSafeAcrossInstances()
    {
        var payload = CreateSampleMetadataPayload();
        var validators = Enumerable.Range(0, 8)
            .Select(_ => MetadataSchemaValidator.CreateDefault())
            .ToArray();

        var results = await Task.WhenAll(validators.Select(v => Task.Run(() => v.Validate(payload))));

        results.Should().OnlyContain(r => r.IsValid);
    }

    private static string CreateSampleMetadataPayload()
    {
        var metadata = new
        {
            catalog = new
            {
                id = "catalog",
                title = "Sample Catalog",
                links = new[]
                {
                    new { href = "https://honua.dev/docs", rel = "related", title = "Docs" }
                }
            },
            folders = new[]
            {
                new { id = "transportation", title = "Transportation" }
            },
            dataSources = new[]
            {
                new { id = "postgis-primary", provider = "postgis", connectionString = "Host=localhost;Database=honua" }
            },
            services = new[]
            {
                new
                {
                    id = "roads",
                    folderId = "transportation",
                    serviceType = "feature",
                    dataSourceId = "postgis-primary",
                    ogc = new
                    {
                        collectionsEnabled = true,
                        defaultCrs = "EPSG:4326",
                        additionalCrs = new[] { "EPSG:3857" }
                    }
                }
            },
            layers = new[]
            {
                new
                {
                    id = "roads-primary",
                    serviceId = "roads",
                    geometryType = "LineString",
                    idField = "road_id",
                    geometryField = "geom",
                    extent = new
                    {
                        bbox = new[]
                        {
                            new[] { -122.6, 45.4, -122.3, 45.7 }
                        },
                        crs = "EPSG:4326"
                    },
                    storage = new
                    {
                        table = "roads_primary",
                        geometryColumn = "geom",
                        primaryKey = "road_id",
                        srid = 3857
                    },
                    fields = new[]
                    {
                        new { name = "road_id", type = "int", nullable = false },
                        new { name = "name", type = "string", nullable = true },
                        new { name = "geom", type = "geometry", nullable = false }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }
}
