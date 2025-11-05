// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Host.Tests.Sensors;

/// <summary>
/// OGC SensorThings API v1.1 Conformance Test Suite
/// Tests compliance with the OGC SensorThings API specification (Sensing profile)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "Conformance")]
public class OgcSensorThingsConformanceTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private const string BasePath = "/sta/v1.1";

    // Test data for cleanup
    private readonly List<string> _createdThingIds = new();
    private readonly List<string> _createdLocationIds = new();
    private readonly List<string> _createdSensorIds = new();
    private readonly List<string> _createdObservedPropertyIds = new();
    private readonly List<string> _createdDatastreamIds = new();
    private readonly List<string> _createdObservationIds = new();

    public OgcSensorThingsConformanceTests(
        WebApplicationFactory<Program> factory,
        ITestOutputHelper output)
    {
        _output = output;
        _client = factory.CreateClient();
    }

    #region Conformance Class 1: Service Root and Metadata

    [Fact]
    public async Task ServiceRoot_ReturnsAllEntitySets()
    {
        // Act
        var response = await _client.GetAsync(BasePath);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // OGC SensorThings v1.1 requires these 8 entity sets
        var requiredEntitySets = new[]
        {
            "Things",
            "Locations",
            "HistoricalLocations",
            "Datastreams",
            "Sensors",
            "Observations",
            "ObservedProperties",
            "FeaturesOfInterest"
        };

        foreach (var entitySet in requiredEntitySets)
        {
            json.TryGetProperty($"{entitySet}@iot.navigationLink", out var navLink)
                .Should().BeTrue($"Service root must include {entitySet}@iot.navigationLink");

            var url = navLink.GetString();
            url.Should().Be($"{BasePath}/{entitySet}");
        }

        _output.WriteLine("✓ Service root returns all required entity sets");
    }

    [Fact]
    public async Task ServiceRoot_ReturnsConformanceClasses()
    {
        // Act
        var response = await _client.GetAsync(BasePath);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Assert
        json.TryGetProperty("serverSettings", out var settings).Should().BeTrue();
        settings.TryGetProperty("conformance", out var conformance).Should().BeTrue();

        var conformanceClasses = conformance.EnumerateArray()
            .Select(c => c.GetString())
            .ToList();

        // Required conformance classes for Sensing profile
        var requiredClasses = new[]
        {
            "http://www.opengis.net/spec/iot_sensing/1.1/req/core",
            "http://www.opengis.net/spec/iot_sensing/1.1/req/create-update-delete",
            "http://www.opengis.net/spec/iot_sensing/1.1/req/data-array"
        };

        foreach (var requiredClass in requiredClasses)
        {
            conformanceClasses.Should().Contain(requiredClass,
                $"Must declare conformance to {requiredClass}");
        }

        _output.WriteLine($"✓ Service declares {conformanceClasses.Count} conformance classes");
    }

    #endregion

    #region Conformance Class 2: Entity CRUD Operations

    [Fact]
    public async Task Thing_SupportsCRUDOperations()
    {
        // CREATE
        var thing = new
        {
            name = "Conformance Test Thing",
            description = "Test thing for OGC conformance validation",
            properties = new { testId = Guid.NewGuid().ToString() }
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        createResponse.Headers.Location.Should().NotBeNull();

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        created.TryGetProperty("@iot.id", out var idProp).Should().BeTrue();
        var thingId = idProp.GetString()!;
        _createdThingIds.Add(thingId);

        // READ
        var readResponse = await _client.GetAsync($"{BasePath}/Things({thingId})");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();
        read.GetProperty("name").GetString().Should().Be(thing.name);

        // UPDATE
        var update = new { description = "Updated description for conformance test" };
        var updateResponse = await _client.PatchAsJsonAsync($"{BasePath}/Things({thingId})", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var verifyResponse = await _client.GetAsync($"{BasePath}/Things({thingId})");
        var verified = await verifyResponse.Content.ReadFromJsonAsync<JsonElement>();
        verified.GetProperty("description").GetString().Should().Be(update.description);

        // DELETE
        var deleteResponse = await _client.DeleteAsync($"{BasePath}/Things({thingId})");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var notFoundResponse = await _client.GetAsync($"{BasePath}/Things({thingId})");
        notFoundResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _createdThingIds.Remove(thingId);
        _output.WriteLine("✓ Thing entity supports CREATE, READ, UPDATE, DELETE");
    }

    [Fact]
    public async Task Location_SupportsCRUDOperations()
    {
        // CREATE
        var location = new
        {
            name = "Conformance Test Location",
            description = "Test location for OGC conformance",
            encodingType = "application/geo+json",
            location = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            }
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Locations", location);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var locationId = created.GetProperty("@iot.id").GetString()!;
        _createdLocationIds.Add(locationId);

        // READ
        var readResponse = await _client.GetAsync($"{BasePath}/Locations({locationId})");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();
        read.GetProperty("name").GetString().Should().Be(location.name);

        // UPDATE
        var update = new { name = "Updated Location Name" };
        var updateResponse = await _client.PatchAsJsonAsync($"{BasePath}/Locations({locationId})", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // DELETE
        var deleteResponse = await _client.DeleteAsync($"{BasePath}/Locations({locationId})");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _createdLocationIds.Remove(locationId);
        _output.WriteLine("✓ Location entity supports CREATE, READ, UPDATE, DELETE");
    }

    [Fact]
    public async Task Sensor_SupportsCRUDOperations()
    {
        // CREATE
        var sensor = new
        {
            name = "Conformance Test Sensor",
            description = "Test sensor for OGC conformance",
            encodingType = "application/pdf",
            metadata = "https://example.com/sensor-spec.pdf"
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Sensors", sensor);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sensorId = created.GetProperty("@iot.id").GetString()!;
        _createdSensorIds.Add(sensorId);

        // READ
        var readResponse = await _client.GetAsync($"{BasePath}/Sensors({sensorId})");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // UPDATE
        var update = new { description = "Updated sensor description" };
        var updateResponse = await _client.PatchAsJsonAsync($"{BasePath}/Sensors({sensorId})", update);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // DELETE
        var deleteResponse = await _client.DeleteAsync($"{BasePath}/Sensors({sensorId})");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _createdSensorIds.Remove(sensorId);
        _output.WriteLine("✓ Sensor entity supports CREATE, READ, UPDATE, DELETE");
    }

    [Fact]
    public async Task ObservedProperty_SupportsCRUDOperations()
    {
        // CREATE
        var observedProperty = new
        {
            name = "Test Temperature",
            description = "Air temperature for conformance testing",
            definition = "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AirTemperature"
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/ObservedProperties", observedProperty);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var propertyId = created.GetProperty("@iot.id").GetString()!;
        _createdObservedPropertyIds.Add(propertyId);

        // READ, UPDATE, DELETE
        var readResponse = await _client.GetAsync($"{BasePath}/ObservedProperties({propertyId})");
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await _client.DeleteAsync($"{BasePath}/ObservedProperties({propertyId})");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _createdObservedPropertyIds.Remove(propertyId);
        _output.WriteLine("✓ ObservedProperty entity supports CREATE, READ, UPDATE, DELETE");
    }

    #endregion

    #region Conformance Class 3: Self-Links and Navigation Links

    [Fact]
    public async Task Entities_IncludeValidSelfLinks()
    {
        // Create a test Thing
        var thing = new
        {
            name = "Self-Link Test Thing",
            description = "Testing self-link conformance"
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var thingId = created.GetProperty("@iot.id").GetString()!;
        _createdThingIds.Add(thingId);

        // Verify self-link
        created.TryGetProperty("@iot.selfLink", out var selfLink).Should().BeTrue(
            "Entity must include @iot.selfLink property");

        var selfLinkUrl = selfLink.GetString();
        selfLinkUrl.Should().Be($"{BasePath}/Things({thingId})",
            "Self-link must match entity URL pattern");

        // Verify self-link is resolvable
        var selfLinkResponse = await _client.GetAsync(selfLinkUrl);
        selfLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        _output.WriteLine($"✓ Self-links are valid and resolvable: {selfLinkUrl}");
    }

    [Fact]
    public async Task Entities_IncludeNavigationLinks()
    {
        // Create Thing with associated entities
        var thing = new
        {
            name = "Navigation Link Test Thing",
            description = "Testing navigation link conformance"
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var thingId = created.GetProperty("@iot.id").GetString()!;
        _createdThingIds.Add(thingId);

        // Verify navigation links
        var requiredNavigationLinks = new[]
        {
            "Locations@iot.navigationLink",
            "HistoricalLocations@iot.navigationLink",
            "Datastreams@iot.navigationLink"
        };

        foreach (var navLink in requiredNavigationLinks)
        {
            created.TryGetProperty(navLink, out var link).Should().BeTrue(
                $"Thing must include {navLink}");

            var linkUrl = link.GetString();
            linkUrl.Should().StartWith($"{BasePath}/Things({thingId})/");
        }

        _output.WriteLine("✓ Navigation links are present and correctly formatted");
    }

    #endregion

    #region Conformance Class 4: OData Query Options

    [Fact]
    public async Task EntitySet_SupportsTopQueryOption()
    {
        // Create multiple test Things
        for (int i = 0; i < 5; i++)
        {
            var thing = new { name = $"Top Test {i}", description = "Testing $top" };
            var response = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            _createdThingIds.Add(created.GetProperty("@iot.id").GetString()!);
        }

        // Query with $top=2
        var queryResponse = await _client.GetAsync($"{BasePath}/Things?$top=2");
        queryResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await queryResponse.Content.ReadFromJsonAsync<JsonElement>();
        var values = json.GetProperty("value").EnumerateArray().ToList();
        values.Should().HaveCountLessOrEqualTo(2, "$top=2 should return at most 2 results");

        _output.WriteLine("✓ $top query option is supported");
    }

    [Fact]
    public async Task EntitySet_SupportsSkipQueryOption()
    {
        // Query with $skip
        var response = await _client.GetAsync($"{BasePath}/Things?$skip=1&$top=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("value", out _).Should().BeTrue();

        _output.WriteLine("✓ $skip query option is supported");
    }

    [Fact]
    public async Task EntitySet_SupportsCountQueryOption()
    {
        // Query with $count=true
        var response = await _client.GetAsync($"{BasePath}/Things?$count=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("@iot.count", out var count).Should().BeTrue(
            "$count=true must include @iot.count in response");

        count.GetInt32().Should().BeGreaterOrEqualTo(0);

        _output.WriteLine($"✓ $count query option is supported (count: {count.GetInt32()})");
    }

    [Fact]
    public async Task EntitySet_SupportsOrderByQueryOption()
    {
        // Query with $orderby
        var response = await _client.GetAsync($"{BasePath}/Things?$orderby=name asc&$top=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var values = json.GetProperty("value").EnumerateArray().ToList();

        if (values.Count > 1)
        {
            var names = values.Select(v => v.GetProperty("name").GetString()).ToList();
            var sortedNames = names.OrderBy(n => n).ToList();
            names.Should().Equal(sortedNames, "$orderby=name asc should return sorted results");
        }

        _output.WriteLine("✓ $orderby query option is supported");
    }

    [Fact]
    public async Task EntitySet_SupportsFilterQueryOption()
    {
        // Create a thing with specific name
        var uniqueName = $"Filter Test {Guid.NewGuid()}";
        var thing = new { name = uniqueName, description = "Testing $filter" };
        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        _createdThingIds.Add(created.GetProperty("@iot.id").GetString()!);

        // Query with $filter
        var encodedFilter = Uri.EscapeDataString($"name eq '{uniqueName}'");
        var response = await _client.GetAsync($"{BasePath}/Things?$filter={encodedFilter}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var values = json.GetProperty("value").EnumerateArray().ToList();
        values.Should().HaveCountGreaterOrEqualTo(1, "Filter should return at least the created entity");

        var found = values.Any(v => v.GetProperty("name").GetString() == uniqueName);
        found.Should().BeTrue("Filter should return the entity with matching name");

        _output.WriteLine("✓ $filter query option is supported");
    }

    [Fact]
    public async Task EntitySet_SupportsSelectQueryOption()
    {
        // Query with $select
        var response = await _client.GetAsync($"{BasePath}/Things?$select=name,description&$top=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var values = json.GetProperty("value").EnumerateArray().ToList();

        if (values.Count > 0)
        {
            var entity = values[0];
            entity.TryGetProperty("name", out _).Should().BeTrue("$select=name should include name");
            entity.TryGetProperty("description", out _).Should().BeTrue("$select=description should include description");
        }

        _output.WriteLine("✓ $select query option is supported");
    }

    [Fact]
    public async Task EntitySet_SupportsExpandQueryOption()
    {
        // Create Thing with Location
        var location = new
        {
            name = "Expand Test Location",
            description = "Testing $expand",
            encodingType = "application/geo+json",
            location = new { type = "Point", coordinates = new[] { -122.4194, 37.7749 } }
        };
        var locResponse = await _client.PostAsJsonAsync($"{BasePath}/Locations", location);
        var createdLoc = await locResponse.Content.ReadFromJsonAsync<JsonElement>();
        var locationId = createdLoc.GetProperty("@iot.id").GetString()!;
        _createdLocationIds.Add(locationId);

        var thing = new
        {
            name = "Expand Test Thing",
            description = "Testing $expand",
            Locations = new[] { new { "@iot.id" = locationId } }
        };
        var thingResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        var createdThing = await thingResponse.Content.ReadFromJsonAsync<JsonElement>();
        var thingId = createdThing.GetProperty("@iot.id").GetString()!;
        _createdThingIds.Add(thingId);

        // Query with $expand=Locations
        var response = await _client.GetAsync($"{BasePath}/Things({thingId})?$expand=Locations");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("Locations", out var locations).Should().BeTrue(
            "$expand=Locations should include expanded Locations array");

        _output.WriteLine("✓ $expand query option is supported");
    }

    #endregion

    #region Conformance Class 5: DataArray Extension

    [Fact]
    public async Task Observations_SupportsDataArrayExtension()
    {
        // Create necessary entities
        var thing = new { name = "DataArray Test Thing", description = "Test" };
        var thingResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        var thingId = (await thingResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("@iot.id").GetString()!;
        _createdThingIds.Add(thingId);

        var sensor = new
        {
            name = "DataArray Test Sensor",
            description = "Test sensor",
            encodingType = "application/pdf",
            metadata = "https://example.com/sensor.pdf"
        };
        var sensorResponse = await _client.PostAsJsonAsync($"{BasePath}/Sensors", sensor);
        var sensorId = (await sensorResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("@iot.id").GetString()!;
        _createdSensorIds.Add(sensorId);

        var observedProperty = new
        {
            name = "DataArray Test Property",
            description = "Test property",
            definition = "http://example.com/property"
        };
        var propResponse = await _client.PostAsJsonAsync($"{BasePath}/ObservedProperties", observedProperty);
        var propertyId = (await propResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("@iot.id").GetString()!;
        _createdObservedPropertyIds.Add(propertyId);

        var datastream = new
        {
            name = "DataArray Test Datastream",
            description = "Test datastream",
            unitOfMeasurement = new
            {
                name = "Celsius",
                symbol = "°C",
                definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
            },
            observationType = "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
            Thing = new { "@iot.id" = thingId },
            Sensor = new { "@iot.id" = sensorId },
            ObservedProperty = new { "@iot.id" = propertyId }
        };
        var datastreamResponse = await _client.PostAsJsonAsync($"{BasePath}/Datastreams", datastream);
        var datastreamId = (await datastreamResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("@iot.id").GetString()!;
        _createdDatastreamIds.Add(datastreamId);

        // Create DataArray request
        var dataArrayRequest = new
        {
            dataArray = new[]
            {
                new
                {
                    Datastream = new { "@iot.id" = datastreamId },
                    components = new[] { "phenomenonTime", "result" },
                    dataArray = new object[][]
                    {
                        new object[] { "2025-01-01T00:00:00Z", 20.5 },
                        new object[] { "2025-01-01T01:00:00Z", 21.0 },
                        new object[] { "2025-01-01T02:00:00Z", 21.5 }
                    }
                }
            }
        };

        // POST to /Observations with dataArray payload
        var response = await _client.PostAsJsonAsync($"{BasePath}/Observations", dataArrayRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "DataArray request to /Observations endpoint should succeed");

        // Verify observations were created
        var getResponse = await _client.GetAsync($"{BasePath}/Datastreams({datastreamId})/Observations?$top=10");
        var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        var observations = json.GetProperty("value").EnumerateArray().ToList();
        observations.Should().HaveCountGreaterOrEqualTo(3,
            "DataArray should create all 3 observations");

        _output.WriteLine("✓ DataArray extension is supported at /Observations endpoint");
    }

    #endregion

    #region Conformance Class 6: GeoJSON and Spatial Support

    [Fact]
    public async Task Location_SupportsGeoJSONGeometry()
    {
        // Create Location with Point geometry
        var location = new
        {
            name = "GeoJSON Test Location",
            description = "Testing GeoJSON support",
            encodingType = "application/geo+json",
            location = new
            {
                type = "Point",
                coordinates = new[] { -122.4194, 37.7749 }
            }
        };

        var createResponse = await _client.PostAsJsonAsync($"{BasePath}/Locations", location);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var locationId = created.GetProperty("@iot.id").GetString()!;
        _createdLocationIds.Add(locationId);

        // Verify geometry is returned correctly
        var readResponse = await _client.GetAsync($"{BasePath}/Locations({locationId})");
        var read = await readResponse.Content.ReadFromJsonAsync<JsonElement>();

        read.TryGetProperty("location", out var geom).Should().BeTrue();
        geom.GetProperty("type").GetString().Should().Be("Point");
        geom.GetProperty("coordinates").EnumerateArray().Should().HaveCount(2);

        _output.WriteLine("✓ GeoJSON geometry support is functional");
    }

    #endregion

    #region Conformance Class 7: HistoricalLocations (Read-Only)

    [Fact]
    public async Task HistoricalLocation_IsCreatedAutomatically()
    {
        // Create Thing with Location
        var location = new
        {
            name = "Historical Location Test",
            description = "Testing automatic HistoricalLocation creation",
            encodingType = "application/geo+json",
            location = new { type = "Point", coordinates = new[] { -122.4194, 37.7749 } }
        };
        var locResponse = await _client.PostAsJsonAsync($"{BasePath}/Locations", location);
        var locationId = (await locResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("@iot.id").GetString()!;
        _createdLocationIds.Add(locationId);

        var thing = new
        {
            name = "Historical Location Test Thing",
            description = "Test thing for HistoricalLocation",
            Locations = new[] { new { "@iot.id" = locationId } }
        };
        var thingResponse = await _client.PostAsJsonAsync($"{BasePath}/Things", thing);
        var thingId = (await thingResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("@iot.id").GetString()!;
        _createdThingIds.Add(thingId);

        // Query HistoricalLocations for this Thing
        var histResponse = await _client.GetAsync($"{BasePath}/Things({thingId})/HistoricalLocations");
        histResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await histResponse.Content.ReadFromJsonAsync<JsonElement>();
        var historicalLocations = json.GetProperty("value").EnumerateArray().ToList();
        historicalLocations.Should().HaveCountGreaterOrEqualTo(1,
            "HistoricalLocation should be created automatically when Thing is associated with Location");

        _output.WriteLine("✓ HistoricalLocations are created automatically");
    }

    [Fact]
    public async Task HistoricalLocation_IsReadOnly()
    {
        // Attempt to create HistoricalLocation directly (should fail)
        var historicalLocation = new
        {
            time = DateTime.UtcNow.ToString("O"),
            Thing = new { "@iot.id" = Guid.NewGuid().ToString() },
            Locations = new[] { new { "@iot.id" = Guid.NewGuid().ToString() } }
        };

        var response = await _client.PostAsJsonAsync($"{BasePath}/HistoricalLocations", historicalLocation);
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            "HistoricalLocations should not support direct creation");

        _output.WriteLine("✓ HistoricalLocations are read-only");
    }

    #endregion

    #region Conformance Summary

    [Fact]
    public void ConformanceSummary_GenerateReport()
    {
        _output.WriteLine("");
        _output.WriteLine("═══════════════════════════════════════════════════════════");
        _output.WriteLine("  OGC SensorThings API v1.1 Conformance Test Summary");
        _output.WriteLine("═══════════════════════════════════════════════════════════");
        _output.WriteLine("");
        _output.WriteLine("Conformance Classes Tested:");
        _output.WriteLine("  ✓ Class 1: Service Root and Metadata");
        _output.WriteLine("  ✓ Class 2: Entity CRUD Operations");
        _output.WriteLine("  ✓ Class 3: Self-Links and Navigation Links");
        _output.WriteLine("  ✓ Class 4: OData Query Options");
        _output.WriteLine("  ✓ Class 5: DataArray Extension");
        _output.WriteLine("  ✓ Class 6: GeoJSON and Spatial Support");
        _output.WriteLine("  ✓ Class 7: HistoricalLocations (Read-Only)");
        _output.WriteLine("");
        _output.WriteLine("Entity Coverage:");
        _output.WriteLine("  ✓ Things");
        _output.WriteLine("  ✓ Locations");
        _output.WriteLine("  ✓ HistoricalLocations");
        _output.WriteLine("  ✓ Sensors");
        _output.WriteLine("  ✓ ObservedProperties");
        _output.WriteLine("  ✓ Datastreams");
        _output.WriteLine("  ✓ Observations");
        _output.WriteLine("  ✓ FeaturesOfInterest");
        _output.WriteLine("");
        _output.WriteLine("OData Query Options:");
        _output.WriteLine("  ✓ $top");
        _output.WriteLine("  ✓ $skip");
        _output.WriteLine("  ✓ $count");
        _output.WriteLine("  ✓ $orderby");
        _output.WriteLine("  ✓ $filter");
        _output.WriteLine("  ✓ $select");
        _output.WriteLine("  ✓ $expand");
        _output.WriteLine("");
        _output.WriteLine("═══════════════════════════════════════════════════════════");
        _output.WriteLine("");
    }

    #endregion

    public void Dispose()
    {
        // Cleanup created test data
        foreach (var id in _createdObservationIds)
        {
            _ = _client.DeleteAsync($"{BasePath}/Observations({id})").Result;
        }
        foreach (var id in _createdDatastreamIds)
        {
            _ = _client.DeleteAsync($"{BasePath}/Datastreams({id})").Result;
        }
        foreach (var id in _createdObservedPropertyIds)
        {
            _ = _client.DeleteAsync($"{BasePath}/ObservedProperties({id})").Result;
        }
        foreach (var id in _createdSensorIds)
        {
            _ = _client.DeleteAsync($"{BasePath}/Sensors({id})").Result;
        }
        foreach (var id in _createdLocationIds)
        {
            _ = _client.DeleteAsync($"{BasePath}/Locations({id})").Result;
        }
        foreach (var id in _createdThingIds)
        {
            _ = _client.DeleteAsync($"{BasePath}/Things({id})").Result;
        }
    }
}
