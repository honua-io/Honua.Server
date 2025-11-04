using Microsoft.Extensions.Logging.Abstractions;
﻿using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Export;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcHandlersGeoPackageTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcHandlersGeoPackageTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Items_WithGeoPackageFormat_ShouldReturnStream()
    {
        var geoPackageExporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geopackage");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            geoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/geopackage+sqlite3");
        context.Response.Body.Should().NotBeNull();

        context.Response.Body.Position = 0;
        var tempPath = Path.Combine(Path.GetTempPath(), $"honua-test-{Guid.NewGuid():N}.gpkg");
        await using (var file = File.Create(tempPath))
        {
            await context.Response.Body.CopyToAsync(file);
        }

        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = tempPath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync();

            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.CommandText = "SELECT COUNT(*) FROM \"roads_primary\"";
                var count = Convert.ToInt64(await countCommand.ExecuteScalarAsync());
                count.Should().Be(2);
            }

            await using (var geomCommand = connection.CreateCommand())
            {
                geomCommand.CommandText = "SELECT name FROM pragma_table_info('roads_primary') WHERE name = 'road_id'";
                var columnName = Convert.ToString(await geomCommand.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
                columnName.Should().Be("road_id");
            }
        }

        if (File.Exists(tempPath))
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    public async Task Items_WithGeoPackageHits_ShouldReturnValidationError()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=geopackage&resultType=hits");
        var geoPackageExporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);
        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            geoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            CancellationToken.None);

        await result.ExecuteAsync(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SingleItem_WithGeoPackage_ShouldReturnValidationError()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items/1", "f=geopackage");
        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "1",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.CacheHeaderService,
            CancellationToken.None);

        await result.ExecuteAsync(context);
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
