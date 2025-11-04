using Microsoft.Extensions.Logging.Abstractions;
﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Export;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class GeoPackageExporterTests
{
    [Fact]
    public async Task ExportAsync_WritesFeaturesToGeoPackage()
    {
        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = "roads",
            Title = "Roads",
            Description = "Sample",
            GeometryType = "Point",
            IdField = "road_id",
            DisplayField = "name",
            GeometryField = "geom",
            Crs = new[] { CrsHelper.DefaultCrsIdentifier },
            Fields = new[]
            {
                new FieldDefinition { Name = "road_id", DataType = "int64", StorageType = "INTEGER", Nullable = false, Editable = false },
                new FieldDefinition { Name = "name", DataType = "string", StorageType = "TEXT" }
            }
        };

        var query = new FeatureQuery(Crs: CrsHelper.DefaultCrsIdentifier);

        static async IAsyncEnumerable<FeatureRecord> CreateRecords()
        {
            yield return new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["road_id"] = 1,
                ["name"] = "First",
                ["geom"] = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[-122.4,45.6]}")
            });

            yield return new FeatureRecord(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["road_id"] = 2,
                ["name"] = "Second",
                ["geom"] = JsonNode.Parse("{\"type\":\"Point\",\"coordinates\":[-122.41,45.61]}")
            });
            await Task.CompletedTask;
        }

        var exporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);

        var result = await exporter.ExportAsync(layer, query, CrsHelper.DefaultCrsIdentifier, CreateRecords(), CancellationToken.None);

        result.Should().NotBeNull();
        result.FileName.Should().EndWith(".gpkg");
        result.FeatureCount.Should().Be(2);

        using var stream = result.Content as FileStream;
        stream.Should().NotBeNull();

        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = stream!.Name,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString()))
        {
            await connection.OpenAsync();

            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'roads_primary'";
            var tableExists = Convert.ToInt64(await tableCommand.ExecuteScalarAsync());
            tableExists.Should().Be(1);

            await using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM \"roads_primary\"";
            var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
            count.Should().Be(2);
        }
    }
}
