using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using NetTopologySuite.Geometries;
using SkiaSharp;
using Xunit;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Server.Core.Tests.Raster.Raster;

[Trait("Category", "Unit")]
public class SkiaSharpRasterRendererTests
{
    [Fact]
    public async Task RenderAsync_ShouldDrawVectorLine()
    {
        var dataset = new RasterDatasetDefinition
        {
            Id = "vector-test",
            Title = "Vector Test",
            ServiceId = "svc",
            Source = new RasterSourceDefinition { Type = "cog", Uri = "file:///tmp/vector-test.tif" },
            Styles = new RasterStyleDefinition
            {
                DefaultStyleId = "natural-color",
                StyleIds = new[] { "natural-color" }
            }
        };

        var geometryFactory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
        var line = geometryFactory.CreateLineString(new[]
        {
            new Coordinate(-1, -1),
            new Coordinate(1, 1)
        });

        var styleDefinition = new StyleDefinition
        {
            Id = "natural-color",
            Renderer = "simple",
            Simple = new SimpleStyleDefinition
            {
                SymbolType = "polygon",
                FillColor = "#5AA06EFF",
                StrokeColor = "#FFFFFFFF",
                StrokeWidth = 1.5
            }
        };

        var requestWithVectors = new RasterRenderRequest(
            dataset,
            new[] { -1d, -1d, 1d, 1d },
            128,
            128,
            "EPSG:4326",
            "EPSG:4326",
            "png",
            Transparent: false,
            StyleId: "natural-color",
            Style: styleDefinition,
            VectorGeometries: new[] { line });

        var requestWithoutVectors = requestWithVectors with
        {
            VectorGeometries = Array.Empty<NtsGeometry>()
        };

        var providers = new List<Honua.Server.Core.Raster.Sources.IRasterSourceProvider>
        {
            new Honua.Server.Core.Raster.Sources.FileSystemRasterSourceProvider()
        };
        var registry = new Honua.Server.Core.Raster.Sources.RasterSourceProviderRegistry(providers);
        var metadataCache = new Honua.Server.Core.Raster.RasterMetadataCache();
        var renderer = new SkiaSharpRasterRenderer(registry, metadataCache);
        var withoutVector = await renderer.RenderAsync(requestWithoutVectors);
        withoutVector.Content.Seek(0, System.IO.SeekOrigin.Begin);
        using var withoutBitmap = SKBitmap.Decode(withoutVector.Content);

        var withVector = await renderer.RenderAsync(requestWithVectors);
        withVector.Content.Seek(0, System.IO.SeekOrigin.Begin);
        using var withBitmap = SKBitmap.Decode(withVector.Content);

        var withoutPixels = withoutBitmap.Pixels;
        var withPixels = withBitmap.Pixels;

        withPixels.Should().NotEqual(withoutPixels);
    }
}
