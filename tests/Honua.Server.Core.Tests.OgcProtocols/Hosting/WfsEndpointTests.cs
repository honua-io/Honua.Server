using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Tests.Shared;
using Honua.Server.Host;
using Honua.Server.Host.Wfs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Xunit;
using Xunit.Sdk;

namespace Honua.Server.Core.Tests.OgcProtocols.Hosting;

[Collection("EndpointTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "WFS")]
[Trait("Speed", "Slow")]
[Trait("Database", "Postgres")]
public class WfsEndpointTests : IClassFixture<WfsWebApplicationFactory>
{
    private static readonly XNamespace WfsNs = "http://www.opengis.net/wfs/2.0";

    private readonly WfsWebApplicationFactory _factory;

    public WfsEndpointTests(WfsWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetCapabilities_ShouldExposeFeatureTypes()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetCapabilities");

        await EnsureSuccessAsync(response);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var featureNames = document
            .Descendants(XName.Get("FeatureType", "http://www.opengis.net/wfs/2.0"))
            .Elements(XName.Get("Name", "http://www.opengis.net/wfs/2.0"))
            .Select(element => element.Value)
            .ToList();

        featureNames.Should().Contain("roads:roads-primary");

        var formats = document
            .Descendants(XName.Get("FeatureType", "http://www.opengis.net/wfs/2.0"))
            .Descendants(XName.Get("Format", "http://www.opengis.net/wfs/2.0"))
            .Select(element => element.Value)
            .ToList();

        formats.Should().Contain("application/geo+json");
        formats.Should().Contain("application/gml+xml; version=3.2");
    }

    [Fact]
    public async Task DescribeFeatureType_ShouldReturnSchema()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=DescribeFeatureType&typeNames=roads:roads-primary");

        await EnsureSuccessAsync(response);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());

        document.Root?.Name.LocalName.Should().Be("schema");
        document.Descendants(XName.Get("element", "http://www.w3.org/2001/XMLSchema"))
            .Any(element => element.Attribute("name")?.Value == "road_id")
            .Should().BeTrue();
    }

    [Fact]
    public async Task LockFeature_ShouldReturnLockIdForTargets()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary");

        await EnsureSuccessAsync(response);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/xml");

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var lockId = document.Root?.Element(WfsNs + "LockId")?.Value;
        lockId.Should().NotBeNullOrWhiteSpace();

        var locked = document.Root?
            .Element(WfsNs + "FeaturesLocked")?
            .Elements(WfsNs + "FeatureId")
            .Count() ?? 0;

        locked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task LockFeature_Some_ShouldReportConflicts()
    {
        await ResetRepositoryAsync();
        var client1 = _factory.CreateAuthenticatedClient();
        var initial = await client1.GetAsync("/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary");
        await EnsureSuccessAsync(initial);

        var client2 = _factory.CreateAuthenticatedClient();
        var response = await client2.GetAsync("/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary&lockAction=SOME");
        await EnsureSuccessAsync(response);

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?
            .Element(WfsNs + "FeaturesNotLocked")?
            .Elements(WfsNs + "FeatureId")
            .Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFeatureWithLock_ShouldReturnLockId()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetFeatureWithLock&typeNames=roads:roads-primary");

        await EnsureSuccessAsync(response);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/gml+xml");

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Attribute("lockId")?.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetFeature_Default_ShouldReturnGml()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary");

        await EnsureSuccessAsync(response);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/gml+xml");
        response.Content.Headers.ContentType?.Parameters
            .Should().ContainSingle(p => string.Equals(p.Name, "version", StringComparison.OrdinalIgnoreCase) && p.Value == "3.2");

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Name.Should().Be(XName.Get("FeatureCollection", "http://www.opengis.net/wfs/2.0"));
        document.Root?.Attribute("numberMatched")?.Value.Should().Be("3");
        document.Root?.Attribute("numberReturned")?.Value.Should().Be("3");
        document.Descendants(XName.Get("member", "http://www.opengis.net/wfs/2.0")).Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task GetFeature_ShouldApplyPaging()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&count=2&startIndex=1&outputFormat=application/geo+json");

        await EnsureSuccessAsync(response);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        document.RootElement.GetProperty("numberMatched").GetInt64().Should().Be(3);

        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").GetProperty("road_id").GetInt32())
            .ToArray();

        ids.Should().BeEquivalentTo(new[] { 2, 3 });
    }

    [Fact]
    public async Task GetFeature_Filter_ShouldReturnMatchingFeatures()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var filter = Uri.EscapeDataString("status = 'open'");
        var response = await client.GetAsync($"/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/geo+json&filter={filter}");
        var filterContent = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"Filter request failed: {filterContent}");
        using var document = JsonDocument.Parse(filterContent);

        document.RootElement.GetProperty("numberMatched").GetInt64().Should().Be(2);

        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").GetProperty("road_id").GetInt32())
            .ToArray();

        ids.Should().BeEquivalentTo(new[] { 1, 3 });
    }

    [Fact]
    public async Task GetFeature_Filter_ShouldSupportComplexCqlExpressions()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var cql = Uri.EscapeDataString("status = 'open' AND road_id > 1");
        var response = await client.GetAsync($"/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/geo+json&filter={cql}");
        await EnsureSuccessAsync(response);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").GetProperty("road_id").GetInt32())
            .ToArray();

        ids.Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public async Task GetFeature_Filter_ShouldSupportOrExpressions()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var cql = Uri.EscapeDataString("(status = 'closed' AND road_id = 2) OR road_id = 1");
        var response = await client.GetAsync($"/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/geo+json&filter={cql}");
        await EnsureSuccessAsync(response);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").GetProperty("road_id").GetInt32())
            .OrderBy(id => id)
            .ToArray();

        ids.Should().Equal(1, 2);
    }

    [Fact]
    public async Task GetFeature_Filter_ShouldSupportXmlFilters()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var xml = "<Filter xmlns=\"http://www.opengis.net/fes/2.0\"><And><PropertyIsEqualTo><ValueReference>status</ValueReference><Literal>open</Literal></PropertyIsEqualTo><PropertyIsGreaterThan><ValueReference>road_id</ValueReference><Literal>1</Literal></PropertyIsGreaterThan></And></Filter>";
        var encoded = Uri.EscapeDataString(xml);
        var response = await client.GetAsync($"/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/geo+json&filter={encoded}");
        await EnsureSuccessAsync(response);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());

        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").GetProperty("road_id").GetInt32())
            .ToArray();

        ids.Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public async Task GetFeature_Bbox_ShouldRestrictResults()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/geo+json&bbox=-122.58,45.52,-122.56,45.54");
        var bboxContent = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"BBox request failed: {bboxContent}");
        using var document = JsonDocument.Parse(bboxContent);

        document.RootElement.GetProperty("numberMatched").GetInt64().Should().Be(1);

        var ids = document.RootElement.GetProperty("features").EnumerateArray()
            .Select(feature => feature.GetProperty("properties").GetProperty("road_id").GetInt32())
            .ToArray();

        ids.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task GetFeature_ResultTypeHits_ShouldReturnCountsOnly()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&resultType=hits");

        await EnsureSuccessAsync(response);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/gml+xml");

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Attribute("numberMatched")?.Value.Should().Be("3");
        document.Root?.Attribute("numberReturned")?.Value.Should().Be("0");
        document.Descendants(XName.Get("member", "http://www.opengis.net/wfs/2.0")).Should().BeEmpty();
    }

    [Fact]
    public async Task GetFeature_InvalidOutputFormat_ShouldReturnException()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var response = await client.GetAsync("/wfs?service=WFS&request=GetFeature&typeNames=roads:roads-primary&outputFormat=application/xml");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Name.Should().Be(XName.Get("ExceptionReport", "http://www.opengis.net/ows/1.1"));
    }

    [Fact]
    public async Task Transaction_ShouldRejectWhenLockMissing()
    {
        await ResetRepositoryAsync();
        var lockingClient = _factory.CreateAuthenticatedClient();
        await EnsureSuccessAsync(await lockingClient.GetAsync("/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary"));

        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>status</wfs:Name>
      <wfs:Value>under_construction</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>1</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Name.Should().Be(XName.Get("ExceptionReport", "http://www.opengis.net/ows/1.1"));
        document
            .Descendants(XName.Get("ExceptionText", "http://www.opengis.net/ows/1.1"))
            .Select(element => element.Value)
            .Should()
            .Contain(text => text.Contains("locked", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transaction_WithLock_ShouldReleaseByDefault()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var lockResponse = await client.GetAsync("/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary");
        await EnsureSuccessAsync(lockResponse);

        var lockDocument = XDocument.Parse(await lockResponse.Content.ReadAsStringAsync());
        var lockId = lockDocument.Root?.Element(WfsNs + "LockId")?.Value;
        lockId.Should().NotBeNullOrWhiteSpace();

        const string firstPayload = """
<wfs:Transaction service="WFS" version="2.0.0" lockId="{0}" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>name</wfs:Name>
      <wfs:Value>SW Locked St</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>1</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var formattedFirst = string.Format(CultureInfo.InvariantCulture, firstPayload, lockId);
        var firstResponse = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(formattedFirst));
        await EnsureSuccessAsync(firstResponse);

        const string secondPayload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>name</wfs:Name>
      <wfs:Value>SW Final St</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>1</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var secondResponse = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(secondPayload));
        await EnsureSuccessAsync(secondResponse);

        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        var updated = repository.Features.First(feature => Convert.ToInt32(feature.Attributes["road_id"], CultureInfo.InvariantCulture) == 1);
        updated.Attributes["name"].Should().Be("SW Final St");
    }

    [Fact]
    public async Task Transaction_WithReleaseActionSome_ShouldRetainLock()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        var lockResponse = await client.GetAsync("/wfs?service=WFS&request=LockFeature&typeNames=roads:roads-primary");
        await EnsureSuccessAsync(lockResponse);

        var lockDocument = XDocument.Parse(await lockResponse.Content.ReadAsStringAsync());
        var lockId = lockDocument.Root?.Element(WfsNs + "LockId")?.Value;
        lockId.Should().NotBeNullOrWhiteSpace();

        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" lockId="{0}" releaseAction="SOME" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>status</wfs:Name>
      <wfs:Value>closed</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>1</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var formattedPayload = string.Format(CultureInfo.InvariantCulture, payload, lockId);
        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(formattedPayload));
        await EnsureSuccessAsync(response);

        var conflictingClient = _factory.CreateAuthenticatedClient();
        const string conflictingPayload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>status</wfs:Name>
      <wfs:Value>maintenance</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>1</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var conflictingResponse = await conflictingClient.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(conflictingPayload));
        conflictingResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await ResetRepositoryAsync();
    }

    [Fact]
    public async Task Transaction_Insert_ShouldReturnSummary()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads">
  <wfs:Insert>
    <tns:roads-primary>
      <road_id>100</road_id>
      <name>SW Salmon St</name>
      <status>planned</status>
      <tns:geom>
        <gml:LineString xmlns:gml="http://www.opengis.net/gml/3.2">
          <gml:posList>-122.560 45.520 -122.550 45.525</gml:posList>
        </gml:LineString>
      </tns:geom>
    </tns:roads-primary>
  </wfs:Insert>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        await EnsureSuccessAsync(response);

        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Name.Should().Be(WfsNs + "TransactionResponse");
        document.Root?.Element(WfsNs + "TransactionSummary")?
            .Element(WfsNs + "totalInserted")?.Value.Should().Be("1");

        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        repository.Features.Should().HaveCount(4);
        var inserted = repository.Features.Last();
        inserted.Attributes.Should().ContainKey("geom");
        var geometry = inserted.Attributes["geom"] as JsonObject;
        geometry.Should().NotBeNull();
        geometry!["type"]!.GetValue<string>().Should().Be("LineString");
    }

    [Fact]
    public async Task Transaction_Update_ShouldModifyFeature()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>name</wfs:Name>
      <wfs:Value>SW Updated St</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:ResourceId rid="roads:roads-primary.1" />
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        await EnsureSuccessAsync(response);

        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        var updated = repository.Features.First(feature => Convert.ToInt32(feature.Attributes["road_id"], CultureInfo.InvariantCulture) == 1);
        updated.Attributes["name"].Should().Be("SW Updated St");
    }

    [Fact]
    public async Task Transaction_Update_WithPropertyFilter_ShouldModifyFeature()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>status</wfs:Name>
      <wfs:Value>maintenance</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>3</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        await EnsureSuccessAsync(response);

        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        var updated = repository.Features.First(feature => Convert.ToInt32(feature.Attributes["road_id"], CultureInfo.InvariantCulture) == 3);
        updated.Attributes["status"].Should().Be("maintenance");
    }

    [Fact]
    public async Task Transaction_Delete_ShouldRemoveFeature()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Delete typeName="tns:roads-primary">
    <fes:Filter>
      <fes:ResourceId rid="roads:roads-primary.2" />
    </fes:Filter>
  </wfs:Delete>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        await EnsureSuccessAsync(response);

        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        repository.Features.Should().HaveCount(2);
        repository.Features.Should().NotContain(feature => Convert.ToInt32(feature.Attributes["road_id"], CultureInfo.InvariantCulture) == 2);
    }

    [Fact]
    public async Task Transaction_Delete_WithPropertyFilter_ShouldRemoveMatchingFeatures()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Delete typeName="tns:roads-primary">
    <fes:Filter>
      <fes:PropertyIsGreaterThan>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>1</fes:Literal>
      </fes:PropertyIsGreaterThan>
    </fes:Filter>
  </wfs:Delete>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        await EnsureSuccessAsync(response);

        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        repository.Features.Should().HaveCount(1);
        repository.Features.Should().OnlyContain(feature => Convert.ToInt32(feature.Attributes["road_id"], CultureInfo.InvariantCulture) == 1);
    }

    [Fact]
    public async Task Transaction_FilterWithoutMatches_ShouldReturnException()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads" xmlns:fes="http://www.opengis.net/fes/2.0">
  <wfs:Update typeName="tns:roads-primary">
    <wfs:Property>
      <wfs:Name>status</wfs:Name>
      <wfs:Value>closed</wfs:Value>
    </wfs:Property>
    <fes:Filter>
      <fes:PropertyIsEqualTo>
        <fes:ValueReference>road_id</fes:ValueReference>
        <fes:Literal>999</fes:Literal>
      </fes:PropertyIsEqualTo>
    </fes:Filter>
  </wfs:Update>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Name.Should().Be(XName.Get("ExceptionReport", "http://www.opengis.net/ows/1.1"));
        document.Descendants(XName.Get("ExceptionText", "http://www.opengis.net/ows/1.1"))
            .Select(element => element.Value)
            .Should().Contain(text => text.Contains("Filter did not match any features.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Transaction_MissingResourceId_ShouldReturnException()
    {
        await ResetRepositoryAsync();
        var client = _factory.CreateAuthenticatedClient();
        const string payload = """
<wfs:Transaction service="WFS" version="2.0.0" xmlns:wfs="http://www.opengis.net/wfs/2.0" xmlns:tns="https://honua.dev/wfs/roads">
  <wfs:Delete typeName="tns:roads-primary">
    <wfs:Filter />
  </wfs:Delete>
</wfs:Transaction>
""";

        var response = await client.PostAsync("/wfs?service=WFS&request=Transaction", CreateXmlContent(payload));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var document = XDocument.Parse(await response.Content.ReadAsStringAsync());
        document.Root?.Name.Should().Be(XName.Get("ExceptionReport", "http://www.opengis.net/ows/1.1"));
    }

    private static StringContent CreateXmlContent(string xml)
    {
        return new StringContent(xml, Encoding.UTF8, "application/xml");
    }

    private async Task ResetRepositoryAsync()
    {
        var repository = (InMemoryEditableFeatureRepository)_factory.Services.GetRequiredService<IFeatureRepository>();
        repository.Reset();

        var lockManager = _factory.Services.GetRequiredService<IWfsLockManager>();
        await lockManager.ResetAsync();
    }

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        throw new XunitException($"Unexpected status code {(int)response.StatusCode}: {content}");
    }
}

public sealed class WfsWebApplicationFactory : HonuaTestWebApplicationFactory
{
    protected override string GetMetadataJson()
    {
        var metadataPath = Path.Combine(AppContext.BaseDirectory, "Data", "metadata-ogc-sample.json");
        var metadata = File.ReadAllText(metadataPath);
        return EnableEditing(metadata);
    }

    protected override void ConfigureAppSettings(Dictionary<string, string?> settings)
    {
        base.ConfigureAppSettings(settings);
        settings["honua:wfs:enabled"] = "true";
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        base.ConfigureServices(services);

        // WFS requires InMemoryEditableFeatureRepository for transactional support
        services.AddSingleton<InMemoryEditableFeatureRepository>();
        services.AddSingleton<IFeatureRepository>(sp => sp.GetRequiredService<InMemoryEditableFeatureRepository>());
    }

    protected override void ConfigureNoOpServices(IServiceCollection services)
    {
        base.ConfigureNoOpServices(services);

        // Replace StubFeatureRepository (added by base) with InMemoryEditableFeatureRepository
        // This must be done here because ConfigureNoOpServices runs after ConfigureServices
        services.RemoveAll<IFeatureRepository>();
        services.AddSingleton<IFeatureRepository>(sp => sp.GetRequiredService<InMemoryEditableFeatureRepository>());
    }

    private static string EnableEditing(string metadataJson)
    {
        var root = JsonNode.Parse(metadataJson)?.AsObject();
        if (root is null)
        {
            return metadataJson;
        }

        if (root.TryGetPropertyValue("layers", out var layersNode) && layersNode is JsonArray layers)
        {
            const string globalIdField = "globalId";

            foreach (var element in layers)
            {
                if (element is not JsonObject layerNode)
                {
                    continue;
                }

                var fields = layerNode["fields"] as JsonArray ?? new JsonArray();
                if (!fields.OfType<JsonObject>().Any(field => string.Equals(field?["name"]?.GetValue<string>(), globalIdField, StringComparison.OrdinalIgnoreCase)))
                {
                    fields.Add(new JsonObject
                    {
                        ["name"] = globalIdField,
                        ["dataType"] = "string",
                        ["nullable"] = false
                    });
                    layerNode["fields"] = fields;
                }

                var editing = layerNode["editing"] as JsonObject ?? new JsonObject();

                var capabilities = editing["capabilities"] as JsonObject ?? new JsonObject();
                capabilities["allowAdd"] = true;
                capabilities["allowUpdate"] = true;
                capabilities["allowDelete"] = true;
                capabilities["requireAuthentication"] = true;
                capabilities["allowedRoles"] = new JsonArray();
                editing["capabilities"] = capabilities;

                var constraints = editing["constraints"] as JsonObject ?? new JsonObject();
                constraints["immutableFields"] = new JsonArray("road_id", globalIdField);
                constraints["requiredFields"] = new JsonArray("name");
                constraints["defaultValues"] = new JsonObject { ["status"] = "planned" };
                editing["constraints"] = constraints;

                layerNode["editing"] = editing;
            }
        }

        return root.ToJsonString(JsonSerializerOptionsRegistry.WebIndented);
    }
}
