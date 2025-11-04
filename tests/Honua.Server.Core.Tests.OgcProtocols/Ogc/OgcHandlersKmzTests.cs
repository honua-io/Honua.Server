using Microsoft.Extensions.Logging.Abstractions;
﻿using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Export;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcHandlersKmzTests
{
    private static readonly IFlatGeobufExporter FlatGeobufExporter = new FlatGeobufExporter();
    private static readonly IGeoArrowExporter GeoArrowExporter = new GeoArrowExporter();

    [Fact]
    public async Task Items_WithKmzFormat_ShouldReturnArchive()
    {
        var registry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(registry);
        var repository = OgcTestUtilities.CreateRepository();
        var geoPackageExporter = new GeoPackageExporter(NullLogger<GeoPackageExporter>.Instance);
        var shapefileExporter = OgcTestUtilities.CreateShapefileExporterStub();
        var attachmentOrchestrator = OgcTestUtilities.CreateAttachmentOrchestratorStub();
        var csvExporter = OgcTestUtilities.CreateCsvExporter();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=kmz");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            resolver,
            repository,
            geoPackageExporter,
            shapefileExporter,
            FlatGeobufExporter,
            GeoArrowExporter,
            csvExporter,
            attachmentOrchestrator,
            registry,
            OgcTestUtilities.CreateApiMetrics(),
            OgcTestUtilities.CreateCacheHeaderService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.google-earth.kmz");

        var disposition = context.Response.Headers["Content-Disposition"].ToString();
        disposition.Should().Contain("attachment");
        disposition.Should().Contain("roads__roads-primary.kmz");

        context.Response.Body.Position = 0;
        using var archive = new ZipArchive(context.Response.Body, ZipArchiveMode.Read, leaveOpen: true);
        archive.Entries.Should().HaveCount(1);

        var entry = archive.GetEntry("roads__roads-primary.kml");
        entry.Should().NotBeNull();

        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        var kml = reader.ReadToEnd();
        kml.Should().Contain("<kml");
        kml.Should().Contain("<Placemark");
        kml.Should().Contain("roads-primary");
        kml.Should().Contain("collectionId");
    }

    [Fact]
    public async Task Item_WithKmzFormat_ShouldReturnArchive()
    {
        var registry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(registry);
        var repository = OgcTestUtilities.CreateRepository();
        var attachmentOrchestrator = OgcTestUtilities.CreateAttachmentOrchestratorStub();
        var csvExporter = OgcTestUtilities.CreateCsvExporter();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/items/1", "f=kmz");

        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "1",
            context.Request,
            resolver,
            repository,
            attachmentOrchestrator,
            registry,
            OgcTestUtilities.CreateCacheHeaderService(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.google-earth.kmz");

        var disposition = context.Response.Headers["Content-Disposition"].ToString();
        disposition.Should().Contain("attachment");
        disposition.Should().Contain("roads__roads-primary-1.kmz");

        context.Response.Body.Position = 0;
        using var archive = new ZipArchive(context.Response.Body, ZipArchiveMode.Read, leaveOpen: true);
        archive.Entries.Should().HaveCount(1);

        var entry = archive.GetEntry("roads__roads-primary-1.kml");
        entry.Should().NotBeNull();

        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        var kml = reader.ReadToEnd();
        kml.Should().Contain("<Placemark");
        kml.Should().Contain("First");
    }
}
