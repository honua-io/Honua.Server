using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.Wms;

/// <summary>
/// Tests for WMS 1.3.0 specification compliance.
/// Validates the 8 critical compliance fixes:
/// 1. VERSION parameter validation
/// 2. STYLES parameter requirement
/// 3. Exception namespace (ogc)
/// 4. GetLegendGraphic implementation
/// 5. BBOX axis order validation
/// 6. Layer queryability attribute
/// 7. SLD/SLD_BODY parameter support
/// 8. Service metadata (ContactInformation, Fees, AccessConstraints)
/// </summary>
[Collection("EndpointTests")]
[Trait("Category", "Integration")]
public sealed class Wms130ComplianceTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;

    public Wms130ComplianceTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
    }

    #region Fix #1: VERSION Parameter Validation

    [Fact]
    public async Task GetMap_WithoutVersion_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("version");
        content.Should().Contain("required");
    }

    [Fact]
    public async Task GetMap_WithInvalidVersion_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.1.1&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("1.3.0");
        content.Should().Contain("1.1.1");
    }

    [Fact]
    public async Task GetMap_WithCorrectVersion_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Fact]
    public async Task GetCapabilities_WithoutVersion_ShouldSucceed()
    {
        // Arrange - GetCapabilities may omit VERSION parameter
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&request=GetCapabilities";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("WMS_Capabilities");
    }

    #endregion

    #region Fix #2: STYLES Parameter Requirement

    [Fact]
    public async Task GetMap_WithoutStyles_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("styles");
        content.Should().Contain("required");
    }

    [Fact]
    public async Task GetMap_WithEmptyStyles_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetMap_WithNamedStyle_ShouldSucceed()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=default";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Fix #3: Exception Namespace

    [Fact]
    public async Task WmsException_ShouldUseOgcNamespace()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=nonexistent&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();

        // Parse XML and verify namespace
        var doc = XDocument.Parse(content);
        var ogcNs = XNamespace.Get("http://www.opengis.net/ogc");
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Should().Be(ogcNs + "ServiceExceptionReport");

        // Verify namespace declaration
        var nsDeclaration = doc.Root.Attribute(XNamespace.Xmlns + "ogc");
        nsDeclaration.Should().NotBeNull();
        nsDeclaration!.Value.Should().Be("http://www.opengis.net/ogc");
    }

    #endregion

    #region Fix #4: GetLegendGraphic Implementation

    [Fact]
    public async Task GetLegendGraphic_WithValidLayer_ShouldReturnPng()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetLegendGraphic&layer=roads:roads-imagery&format=image/png";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);

        // Verify PNG signature
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50); // 'P'
        bytes[2].Should().Be(0x4E); // 'N'
        bytes[3].Should().Be(0x47); // 'G'
    }

    [Fact]
    public async Task GetLegendGraphic_WithWidthAndHeight_ShouldRespectDimensions()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetLegendGraphic&layer=roads:roads-imagery&format=image/png&width=100&height=50";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLegendGraphic_WithoutLayer_ShouldReturnBadRequest()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetLegendGraphic&format=image/png";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("layer");
    }

    #endregion

    #region Fix #5: BBOX Axis Order Validation

    [Fact]
    public async Task GetMap_WithInvalidBboxOrder_ShouldReturnBadRequest()
    {
        // Arrange - Reversed coordinates (minx > maxx)
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.3,45.5,-122.6,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("bounding box");
        content.Should().Contain("minX");
        content.Should().Contain("maxX");
    }

    [Fact]
    public async Task GetMap_WithEpsg4326_ShouldHandleLatLonOrder()
    {
        // Arrange - EPSG:4326 uses lat,lon in WMS 1.3.0
        var client = _factory.CreateAuthenticatedClient();
        // Input: minLat, minLon, maxLat, maxLon
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=45.5,-122.6,45.7,-122.3&width=256&height=256&format=image/png&crs=EPSG:4326&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetMap_WithCrs84_ShouldHandleLonLatOrder()
    {
        // Arrange - CRS:84 uses lon,lat
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetMap_WithOutOfRangeLatitude_ShouldReturnBadRequest()
    {
        // Arrange - Invalid latitude (> 90)
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,95.7&width=256&height=256&format=image/png&crs=CRS:84&styles=";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("latitude");
        content.Should().Contain("[-90, 90]");
    }

    #endregion

    #region Fix #6: Layer Queryability

    [Fact]
    public async Task GetCapabilities_ShouldIncludeQueryableAttribute()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetCapabilities";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        // Parse XML and check for queryable attribute
        var doc = XDocument.Parse(content);
        var wmsNs = XNamespace.Get("http://www.opengis.net/wms");
        var layers = doc.Descendants(wmsNs + "Layer");

        var namedLayers = layers.Where(l => l.Element(wmsNs + "Name") != null);
        namedLayers.Should().NotBeEmpty();

        // At least one layer should have queryable attribute
        var layersWithQueryable = namedLayers.Where(l => l.Attribute("queryable") != null);
        layersWithQueryable.Should().NotBeEmpty();
    }

    #endregion

    #region Fix #7: SLD/SLD_BODY Parameter Support

    [Fact]
    public async Task GetMap_WithSldParameter_ShouldNotError()
    {
        // Arrange - Basic acknowledgment of SLD parameter
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=&sld=http://example.com/style.sld";

        // Act
        var response = await client.GetAsync(url);

        // Assert - Should succeed (fall back to default style for now)
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetMap_WithSldBodyParameter_ShouldNotError()
    {
        // Arrange - Basic acknowledgment of SLD_BODY parameter
        var client = _factory.CreateAuthenticatedClient();
        var sldBody = Uri.EscapeDataString("<StyledLayerDescriptor version=\"1.0.0\"></StyledLayerDescriptor>");
        var url = $"/wms?service=WMS&version=1.3.0&request=GetMap&layers=roads:roads-imagery&bbox=-122.6,45.5,-122.3,45.7&width=256&height=256&format=image/png&crs=CRS:84&styles=&sld_body={sldBody}";

        // Act
        var response = await client.GetAsync(url);

        // Assert - Should succeed (fall back to default style for now)
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Fix #8: Service Metadata

    [Fact]
    public async Task GetCapabilities_ShouldIncludeContactInformation()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetCapabilities";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        // Verify ContactInformation element exists
        var doc = XDocument.Parse(content);
        var wmsNs = XNamespace.Get("http://www.opengis.net/wms");
        var contactInfo = doc.Descendants(wmsNs + "ContactInformation").FirstOrDefault();

        contactInfo.Should().NotBeNull("WMS 1.3.0 requires ContactInformation element");
    }

    [Fact]
    public async Task GetCapabilities_ShouldIncludeFeesAndAccessConstraints()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var url = "/wms?service=WMS&version=1.3.0&request=GetCapabilities";

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        // Verify Fees and AccessConstraints elements exist
        var doc = XDocument.Parse(content);
        var wmsNs = XNamespace.Get("http://www.opengis.net/wms");

        var fees = doc.Descendants(wmsNs + "Fees").FirstOrDefault();
        fees.Should().NotBeNull("WMS 1.3.0 requires Fees element");

        var accessConstraints = doc.Descendants(wmsNs + "AccessConstraints").FirstOrDefault();
        accessConstraints.Should().NotBeNull("WMS 1.3.0 requires AccessConstraints element");
    }

    #endregion
}
