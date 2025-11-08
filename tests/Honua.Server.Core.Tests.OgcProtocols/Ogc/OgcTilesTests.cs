using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Ogc;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Feature", "OGC")]
[Trait("Speed", "Fast")]
public class OgcTilesTests
{
    [Fact]
    public async Task Tiles_ShouldListRasterDatasets()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/tiles", string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileSets(
            "roads::roads-primary",
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);
        Console.WriteLine(payload);

        var root = document.RootElement;
        var tilesets = root.GetProperty("tilesets").EnumerateArray();
        tilesets.Should().NotBeEmpty();
        var first = tilesets.First();
        first.GetProperty("id").GetString().Should().Be("roads-imagery");
        var matrixLinks = first.GetProperty("tileMatrixSetLinks").EnumerateArray().ToArray();
        matrixLinks.Should().Contain(link => link.GetProperty("tileMatrixSet").GetString() == "WorldCRS84Quad");
        matrixLinks.Should().Contain(link => link.GetProperty("tileMatrixSet").GetString() == "WorldWebMercatorQuad");
    }

    [Fact]
    public async Task Tileset_ShouldDescribeDataset()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/tiles/roads-imagery", string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileSet(
            "roads::roads-primary",
            "roads-imagery",
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("id").GetString().Should().Be("roads-imagery");
        root.GetProperty("defaultStyle").GetString().Should().Be("natural-color");
        root.GetProperty("minZoom").GetInt32().Should().Be(0);
        root.GetProperty("maxZoom").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task TileJson_ShouldDescribeTileset()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/tiles/roads-imagery/tilejson", string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileJson(
            "roads::roads-primary",
            "roads-imagery",
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("tilejson").GetString().Should().Be("3.0.0");
        root.GetProperty("name").GetString().Should().Be("Roads Imagery");
        root.GetProperty("scheme").GetString().Should().Be("xyz");
        root.GetProperty("minzoom").GetInt32().Should().Be(0);
        root.GetProperty("maxzoom").GetInt32().Should().Be(2);

        var bounds = root.GetProperty("bounds").EnumerateArray().Select(element => element.GetDouble()).ToArray();
        bounds.Should().HaveCount(4);

        var center = root.GetProperty("center").EnumerateArray().Select(element => element.GetDouble()).ToArray();
        center.Should().HaveCount(3);

        var tilesArray = root.GetProperty("tiles").EnumerateArray().Select(element => element.GetString()).ToArray();
        tilesArray.Should().ContainSingle(tile => tile != null);
        tilesArray[0].Should().Contain("WorldWebMercatorQuad/{z}/{y}/{x}");

        var links = root.GetProperty("links").EnumerateArray().ToArray();
        links.Should().Contain(link => link.GetProperty("rel").GetString() == "self");
        links.Should().Contain(link => link.GetProperty("rel").GetString() == "collection");
    }

    [Fact]
    public async Task TileJson_ShouldReflectRequestedParameters()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var queryString = string.Join('&', new[]
        {
            "tileMatrixSet=WorldCRS84Quad",
            "format=jpeg",
            "tileSize=512",
            "styleId=infrared",
            "transparent=false"
        });
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/tilejson",
            queryString);

        var result = await OgcTilesHandlers.GetCollectionTileJson(
            "roads::roads-primary",
            "roads-imagery",
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        var tileHref = root.GetProperty("tiles").EnumerateArray().First().GetString();
        tileHref.Should().NotBeNull();

        var uri = new Uri(tileHref!);
        uri.Query.Should().BeEmpty();

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        segments = segments.Select(Uri.UnescapeDataString).ToArray();
        segments.Should().ContainInOrder(new[]
        {
            "ogc",
            "collections",
            "roads::roads-primary",
            "tiles",
            "roads-imagery",
            OgcTileMatrixHelper.WorldWebMercatorQuadId,
            "{z}",
            "{y}",
            "{x}"
        });

        root.GetProperty("format").GetString().Should().Be("png");
        root.GetProperty("scheme").GetString().Should().Be("xyz");
        root.GetProperty("dataType").GetString().Should().Be("map");
    }
    [Fact]
    public async Task TileMatrixSet_ShouldReturnMatrices()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad",
            string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileMatrixSet(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("id").GetString().Should().Be(OgcTileMatrixHelper.WorldCrs84QuadId);
        var matrices = root.GetProperty("tileMatrices").EnumerateArray().ToArray();
        matrices.Should().HaveCount(3);
        matrices[0].GetProperty("id").GetString().Should().Be("0");
        matrices[^1].GetProperty("id").GetString().Should().Be("2");
    }

    [Fact]
    public async Task TileMatrixSet_WebMercator_ShouldReturnMatrices()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad",
            string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileMatrixSet(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldWebMercatorQuadId,
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.GetProperty("id").GetString().Should().Be(OgcTileMatrixHelper.WorldWebMercatorQuadId);
        var matrices = root.GetProperty("tileMatrices").EnumerateArray().ToArray();
        matrices.Should().HaveCount(3);
        matrices[0].GetProperty("id").GetString().Should().Be("0");
    }

    [Fact]
    public async Task Tile_ShouldReturnImage()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            "tileSize=128&format=png");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("image/png");
        context.Response.Body.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Tile_WebMercator_ShouldReturnImage()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldWebMercatorQuad/0/0/0",
            "tileSize=128&format=png");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldWebMercatorQuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("image/png");
        context.Response.Body.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Tile_WithStyleId_ShouldChangePayload()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        async Task<byte[]> RenderAsync(string query)
        {
            var context = OgcTestUtilities.CreateHttpContext(
                "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/1/0/0",
                query);

            var result = await OgcTilesHandlers.GetCollectionTile(
                "roads::roads-primary",
                "roads-imagery",
                OgcTileMatrixHelper.WorldCrs84QuadId,
                "1",
                0,
                0,
                context.Request,
                resolver,
                rasterRegistry,
                rasterRenderer,
                metadataRegistry,
                repository,
                pmTilesExporter,
                tileCacheProvider,
                tileCacheMetrics,
                cacheHeaderService,
                tilesHandler,
                CancellationToken.None);

            await result.ExecuteAsync(context);

            context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
            context.Response.Body.Position = 0;
            return (context.Response.Body as MemoryStream)?.ToArray() ?? Array.Empty<byte>();
        }

        var defaultBytes = await RenderAsync("tileSize=128");
        var infraredBytes = await RenderAsync("tileSize=128&styleId=infrared");

        infraredBytes.Should().NotEqual(defaultBytes);
    }

    [Fact]
    public async Task Tile_InvalidStyle_ShouldReturnBadRequest()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            "styleId=unknown");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Tile_WithPmTilesFormat_ShouldReturnArchive()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-vectortiles/WorldCRS84Quad/0/0/0",
            "format=pmtiles");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-vectortiles",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.pmtiles");

        context.Response.Body.Position = 0;
        var headerBytes = new byte[7];
        var bytesRead = await context.Response.Body.ReadAsync(headerBytes, CancellationToken.None);
        bytesRead.Should().Be(7);
        Encoding.ASCII.GetString(headerBytes).Should().Be("PMTiles");
    }

    #region Temporal Validation Tests

    [Fact]
    public async Task Tile_WithValidDatetime_Succeeds()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            "datetime=2024-01-01");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("2024-01-01T00:00:00Z")]
    [InlineData("2024-06-15T12:30:45Z")]
    [InlineData("2024-12-31T23:59:59Z")]
    public async Task Tile_WithValidISO8601Datetime_Succeeds(string datetime)
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            $"datetime={Uri.EscapeDataString(datetime)}");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("2024-01-01..2024-12-31")]
    [InlineData("2024-01-01..")]
    [InlineData("..2024-12-31")]
    public async Task Tile_WithValidTemporalInterval_Succeeds(string interval)
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            $"datetime={Uri.EscapeDataString(interval)}");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("2024-13-01")]
    [InlineData("not-a-datetime")]
    public async Task Tile_WithInvalidDatetime_ReturnsBadRequest(string datetime)
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            $"datetime={datetime}");

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Tile_WithInvalidTemporalInterval_ReturnsBadRequest()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            "datetime=2024-12-31..2024-01-01"); // start > end

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task Tile_WithDurationNotation_Succeeds()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/roads-imagery/WorldCRS84Quad/0/0/0",
            "datetime=2024-01-01..P1M"); // 1 month duration

        var result = await OgcTilesHandlers.GetCollectionTile(
            "roads::roads-primary",
            "roads-imagery",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    #endregion

    #region OGC API - Tiles Compliance Tests

    [Fact]
    public async Task Tileset_ShouldIncludeBoundingBox()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/tiles/roads-imagery", string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileSet(
            "roads::roads-primary",
            "roads-imagery",
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.TryGetProperty("boundingBox", out var bbox).Should().BeTrue("tileset metadata should include boundingBox");
        bbox.TryGetProperty("lowerLeft", out var lowerLeft).Should().BeTrue();
        bbox.TryGetProperty("upperRight", out var upperRight).Should().BeTrue();
        bbox.TryGetProperty("crs", out var crs).Should().BeTrue();

        lowerLeft.GetArrayLength().Should().Be(2);
        upperRight.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Tileset_ShouldIncludeTileMatrixSetLimits()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();
        var context = OgcTestUtilities.CreateHttpContext("/ogc/collections/roads::roads-primary/tiles/roads-imagery", string.Empty);

        var result = await OgcTilesHandlers.GetCollectionTileSet(
            "roads::roads-primary",
            "roads-imagery",
            context.Request,
            resolver,
            rasterRegistry,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);
        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var payload = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(payload);

        var root = document.RootElement;
        root.TryGetProperty("tileMatrixSetLinks", out var matrixLinks).Should().BeTrue();

        var links = matrixLinks.EnumerateArray().ToArray();
        links.Should().NotBeEmpty();

        foreach (var link in links)
        {
            link.TryGetProperty("tileMatrixSetLimits", out var limits).Should().BeTrue("each tile matrix set link should include tileMatrixSetLimits");

            var limitArray = limits.EnumerateArray().ToArray();
            limitArray.Should().NotBeEmpty();

            foreach (var limit in limitArray)
            {
                limit.TryGetProperty("tileMatrix", out _).Should().BeTrue();
                limit.TryGetProperty("minTileRow", out _).Should().BeTrue();
                limit.TryGetProperty("maxTileRow", out _).Should().BeTrue();
                limit.TryGetProperty("minTileCol", out _).Should().BeTrue();
                limit.TryGetProperty("maxTileCol", out _).Should().BeTrue();
            }
        }
    }

    [Fact]
    public async Task StandardTileEndpoint_WithoutTilesetId_ShouldWork()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();

        // Use standard OGC pattern without tilesetId
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/WorldCRS84Quad/0/0/0",
            "tileSize=128&format=png");

        var result = await OgcTilesHandlers.GetCollectionTileStandard(
            "roads::roads-primary",
            OgcTileMatrixHelper.WorldCrs84QuadId,
            "0",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("image/png");
        context.Response.Body.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StandardTileEndpoint_WebMercator_ShouldWork()
    {
        var metadataRegistry = OgcTestUtilities.CreateRegistry();
        var resolver = OgcTestUtilities.CreateResolver(metadataRegistry);
        var rasterRegistry = new RasterDatasetRegistry(metadataRegistry);
        var cacheHeaderService = OgcTestUtilities.CreateCacheHeaderService();
        var rasterRenderer = OgcTestUtilities.CreateRasterRenderer();
        var repository = OgcTestUtilities.CreateRepository();
        var pmTilesExporter = OgcTestUtilities.CreatePmTilesExporter();
        var tileCacheProvider = OgcTestUtilities.CreateRasterTileCacheProvider();
        var tileCacheMetrics = OgcTestUtilities.CreateRasterTileCacheMetrics();
        var tilesHandler = OgcTestUtilities.CreateOgcTilesHandlerStub();

        // Use standard OGC pattern without tilesetId
        var context = OgcTestUtilities.CreateHttpContext(
            "/ogc/collections/roads::roads-primary/tiles/WorldWebMercatorQuad/1/0/0",
            "tileSize=256&format=png");

        var result = await OgcTilesHandlers.GetCollectionTileStandard(
            "roads::roads-primary",
            OgcTileMatrixHelper.WorldWebMercatorQuadId,
            "1",
            0,
            0,
            context.Request,
            resolver,
            rasterRegistry,
            rasterRenderer,
            metadataRegistry,
            repository,
            pmTilesExporter,
            tileCacheProvider,
            tileCacheMetrics,
            cacheHeaderService,
            tilesHandler,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("image/png");
        context.Response.Body.Length.Should().BeGreaterThan(0);
    }

    #endregion
}
