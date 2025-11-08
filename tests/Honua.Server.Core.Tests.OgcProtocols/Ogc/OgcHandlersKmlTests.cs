using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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
public class OgcHandlersKmlTests : IClassFixture<OgcHandlerTestFixture>
{
    private readonly OgcHandlerTestFixture _fixture;

    public OgcHandlersKmlTests(OgcHandlerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Items_WithKmlFormat_ShouldReturnDocument()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items", "f=kml");

        var result = await OgcFeaturesHandlers.GetCollectionItems(
            "roads::roads-primary",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.GeoPackageExporter,
            _fixture.ShapefileExporter,
            _fixture.FlatGeobufExporter,
            _fixture.GeoArrowExporter,
            _fixture.CsvExporter,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.ApiMetrics,
            _fixture.CacheHeaderService,
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.google-earth.kml+xml");

        var disposition = context.Response.Headers["Content-Disposition"].ToString();
        disposition.Should().Contain("attachment");
        disposition.Should().Contain("roads__roads-primary.kml");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        payload.Should().Contain("<kml");
        payload.Should().Contain("<Placemark");
        payload.Should().Contain("roads-primary");
        payload.Should().Contain("<Style id=\"primary-roads-line\"");
        payload.Should().Contain("<styleUrl>#primary-roads-line</styleUrl>");

        // Validate KML XML structure
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(payload);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("kml", "http://www.opengis.net/kml/2.2");

        // Validate root KML element
        var kmlNode = xmlDoc.SelectSingleNode("//kml:kml", nsmgr);
        kmlNode.Should().NotBeNull("KML document must have root kml element");

        // Validate Document element
        var documentNode = xmlDoc.SelectSingleNode("//kml:Document", nsmgr);
        documentNode.Should().NotBeNull("KML must contain Document element");

        // Validate Document has a name
        var docNameNode = documentNode!.SelectSingleNode("kml:name", nsmgr);
        docNameNode.Should().NotBeNull("Document must have a name");
        docNameNode!.InnerText.Should().BeOneOf("roads-primary", "Primary Roads");

        // Validate Style element exists
        var styleNodes = xmlDoc.SelectNodes("//kml:Style", nsmgr);
        styleNodes.Should().NotBeNull();
        styleNodes!.Count.Should().BeGreaterThan(0, "Document must contain at least one Style element");

        var primaryRoadStyle = xmlDoc.SelectSingleNode("//kml:Style[@id='primary-roads-line']", nsmgr);
        primaryRoadStyle.Should().NotBeNull("Document must contain primary-roads-line Style");

        // Validate LineStyle within Style
        var lineStyle = primaryRoadStyle!.SelectSingleNode(".//kml:LineStyle", nsmgr);
        lineStyle.Should().NotBeNull("Style must contain LineStyle for line geometry");

        // Validate Placemarks
        var placemarks = xmlDoc.SelectNodes("//kml:Placemark", nsmgr);
        placemarks.Should().NotBeNull();
        placemarks!.Count.Should().BeGreaterThan(0, "Document must contain at least one Placemark");

        // Validate first placemark structure
        var firstPlacemark = placemarks[0];
        var placemarkName = firstPlacemark!.SelectSingleNode("kml:name", nsmgr);
        placemarkName.Should().NotBeNull("Placemark must have a name");
        placemarkName!.InnerText.Should().NotBeNullOrWhiteSpace();

        // Validate placemark has styleUrl
        var styleUrl = firstPlacemark.SelectSingleNode("kml:styleUrl", nsmgr);
        styleUrl.Should().NotBeNull("Placemark must reference a style");
        styleUrl!.InnerText.Should().Be("#primary-roads-line");

        // Validate geometry (LineString for roads)
        var lineString = firstPlacemark.SelectSingleNode(".//kml:LineString", nsmgr);
        lineString.Should().NotBeNull("Road placemark must contain LineString geometry");

        var coordinates = lineString!.SelectSingleNode("kml:coordinates", nsmgr);
        coordinates.Should().NotBeNull("LineString must have coordinates");
        coordinates!.InnerText.Should().NotBeNullOrWhiteSpace("Coordinates must not be empty");

        // Validate coordinate format (should be lon,lat,alt tuples separated by whitespace)
        var coordText = coordinates.InnerText.Trim();
        var coordParts = coordText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        coordParts.Should().HaveCountGreaterThan(1, "LineString must have at least 2 coordinate points");

        foreach (var coordPart in coordParts.Take(2))
        {
            var values = coordPart.Split(',');
        values.Should().HaveCountGreaterThanOrEqualTo(2, "Each coordinate must have at least lon,lat");
        }

        // Validate Extended Data if present
        var extendedData = firstPlacemark.SelectSingleNode("kml:ExtendedData", nsmgr);
        if (extendedData != null)
        {
            var dataNodes = extendedData.SelectNodes("kml:Data", nsmgr);
            dataNodes.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Item_WithKmlFormat_ShouldReturnDocument()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/items/1", "f=kml");

        var result = await OgcFeaturesHandlers.GetCollectionItem(
            "roads::roads-primary",
            "1",
            context.Request,
            _fixture.Resolver,
            _fixture.Repository,
            _fixture.AttachmentOrchestrator,
            _fixture.Registry,
            _fixture.CacheHeaderService,
            OgcTestUtilities.CreateOgcFeaturesAttachmentHandlerStub(),
            OgcTestUtilities.CreateOgcFeaturesEditingHandlerStub(),
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.google-earth.kml+xml");

        var disposition = context.Response.Headers["Content-Disposition"].ToString();
        disposition.Should().Contain("attachment");
        disposition.Should().Contain("roads__roads-primary-1.kml");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        payload.Should().Contain("<kml");
        payload.Should().Contain("<Placemark");
        payload.Should().Contain("First");
        payload.Should().Contain("<Style id=\"primary-roads-line\"");
        payload.Should().Contain("<styleUrl>#primary-roads-line</styleUrl>");

        // Validate KML XML structure
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(payload);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("kml", "http://www.opengis.net/kml/2.2");

        // Validate root KML element
        var kmlNode = xmlDoc.SelectSingleNode("//kml:kml", nsmgr);
        kmlNode.Should().NotBeNull("KML document must have root kml element");

        // Validate Document element
        var documentNode = xmlDoc.SelectSingleNode("//kml:Document", nsmgr);
        if (documentNode is not null)
        {
            var styleNodes = xmlDoc.SelectNodes("//kml:Document/kml:Style", nsmgr);
            styleNodes.Should().NotBeNull();
            styleNodes!.Count.Should().BeGreaterThan(0, "Document must contain at least one Style element");

            var primaryRoadStyle = xmlDoc.SelectSingleNode("//kml:Document/kml:Style[@id='primary-roads-line']", nsmgr);
            primaryRoadStyle.Should().NotBeNull("Document must contain primary-roads-line Style");

            var lineStyle = primaryRoadStyle!.SelectSingleNode(".//kml:LineStyle", nsmgr);
            lineStyle.Should().NotBeNull("Style must contain LineStyle for line geometry");
        }
        else
        {
            var inlineStyle = xmlDoc.SelectSingleNode("//kml:Placemark/kml:Style", nsmgr);
            inlineStyle.Should().NotBeNull("Placemark should include inline Style information");

            var lineStyle = inlineStyle!.SelectSingleNode(".//kml:LineStyle", nsmgr);
            lineStyle.Should().NotBeNull("Inline Style must contain LineStyle for line geometry");
        }

        // Validate Placemark (should be exactly one for single item)
        var placemarks = xmlDoc.SelectNodes("//kml:Placemark", nsmgr);
        placemarks.Should().NotBeNull();
        placemarks!.Count.Should().Be(1, "Single item response must contain exactly one Placemark");

        // Validate placemark structure
        var placemark = placemarks[0];
        var placemarkName = placemark!.SelectSingleNode("kml:name", nsmgr);
        placemarkName.Should().NotBeNull("Placemark must have a name");
        placemarkName!.InnerText.Should().BeOneOf("First", "1");

        // Validate placemark has styleUrl
        var styleUrl = placemark.SelectSingleNode("kml:styleUrl", nsmgr);
        styleUrl.Should().NotBeNull("Placemark must reference a style");
        styleUrl!.InnerText.Should().Be("#primary-roads-line");

        // Validate geometry (LineString for roads)
        var lineString = placemark.SelectSingleNode(".//kml:LineString", nsmgr);
        lineString.Should().NotBeNull("Road placemark must contain LineString geometry");

        var coordinates = lineString!.SelectSingleNode("kml:coordinates", nsmgr);
        coordinates.Should().NotBeNull("LineString must have coordinates");
        coordinates!.InnerText.Should().NotBeNullOrWhiteSpace("Coordinates must not be empty");

        // Validate coordinate format (should be lon,lat,alt tuples separated by whitespace)
        var coordText = coordinates.InnerText.Trim();
        var coordParts = coordText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        coordParts.Should().HaveCountGreaterThan(1, "LineString must have at least 2 coordinate points");

        foreach (var coordPart in coordParts.Take(2))
        {
            var values = coordPart.Split(',');
        values.Should().HaveCountGreaterThanOrEqualTo(2, "Each coordinate must have at least lon,lat");
        }
    }

    [Fact]
    public async Task Styles_Endpoint_ShouldReturnSld()
    {
        var context = _fixture.CreateHttpContext("/ogc/collections/roads::roads-primary/styles/primary-roads-line", string.Empty);

        var result = await OgcFeaturesHandlers.GetCollectionStyle(
            "roads::roads-primary",
            "primary-roads-line",
            context.Request,
            _fixture.Resolver,
            _fixture.Registry,
            CancellationToken.None);

        await result.ExecuteAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.ContentType.Should().Be("application/vnd.ogc.sld+xml");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync();
        payload.Should().Contain("StyledLayerDescriptor");
        payload.Should().Contain("primary-roads-line");

        // Validate SLD XML structure
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(payload);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("sld", "http://www.opengis.net/sld");
        nsmgr.AddNamespace("ogc", "http://www.opengis.net/ogc");
        nsmgr.AddNamespace("se", "http://www.opengis.net/se");

        // Validate root StyledLayerDescriptor element
        var sldNode = xmlDoc.SelectSingleNode("//sld:StyledLayerDescriptor", nsmgr);
        sldNode.Should().NotBeNull("SLD document must have root StyledLayerDescriptor element");

        // Validate NamedLayer element
        var namedLayer = xmlDoc.SelectSingleNode("//sld:NamedLayer", nsmgr);
        namedLayer.Should().NotBeNull("SLD must contain NamedLayer element");

        // Validate layer name
        var layerName = namedLayer!.SelectSingleNode("se:Name", nsmgr)
                        ?? namedLayer.SelectSingleNode("sld:Name", nsmgr);
        layerName.Should().NotBeNull("NamedLayer must have a Name element");
        layerName!.InnerText.Should().NotBeNullOrWhiteSpace();

        // Validate UserStyle element
        var userStyle = namedLayer.SelectSingleNode("sld:UserStyle", nsmgr);
        userStyle.Should().NotBeNull("NamedLayer must contain UserStyle");

        // Validate style name matches requested style
        var styleName = userStyle!.SelectSingleNode("se:Name", nsmgr)
                        ?? userStyle.SelectSingleNode("sld:Name", nsmgr);
        styleName.Should().NotBeNull("UserStyle must have a Name element");
        styleName!.InnerText.Should().Be("primary-roads-line");

        // Validate FeatureTypeStyle element
        var featureTypeStyle = userStyle.SelectSingleNode("se:FeatureTypeStyle", nsmgr)
                             ?? userStyle.SelectSingleNode("sld:FeatureTypeStyle", nsmgr);
        featureTypeStyle.Should().NotBeNull("UserStyle must contain FeatureTypeStyle");

        // Validate Rule element
        var rule = featureTypeStyle!.SelectSingleNode("se:Rule", nsmgr)
                   ?? featureTypeStyle.SelectSingleNode("sld:Rule", nsmgr);
        rule.Should().NotBeNull("FeatureTypeStyle must contain at least one Rule");

        // Validate symbolizer presence (LineSymbolizer or PolygonSymbolizer)
        var lineSymbolizer = rule!.SelectSingleNode("se:LineSymbolizer", nsmgr)
                            ?? rule.SelectSingleNode("sld:LineSymbolizer", nsmgr);
        var polygonSymbolizer = rule.SelectSingleNode("se:PolygonSymbolizer", nsmgr)
                               ?? rule.SelectSingleNode("sld:PolygonSymbolizer", nsmgr);

        (lineSymbolizer ?? polygonSymbolizer).Should().NotBeNull("Rule should contain at least one symbolizer definition");

        var strokeParent = lineSymbolizer ?? polygonSymbolizer;
        if (strokeParent is not null)
        {
            // Validate Stroke element within the symbolizer
            var stroke = strokeParent.SelectSingleNode(".//se:Stroke", nsmgr)
                        ?? strokeParent.SelectSingleNode(".//sld:Stroke", nsmgr);
            stroke.Should().NotBeNull("Symbolizer must include Stroke styling");
        }
    }
}
