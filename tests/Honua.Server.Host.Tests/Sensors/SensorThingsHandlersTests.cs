// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Data;
using Honua.Server.Enterprise.Sensors.Handlers;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Sensors;

[Trait("Category", "Unit")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "Handlers")]
public class SensorThingsHandlersTests
{
    [Fact]
    public async Task GetServiceRoot_ReturnsAllEightEntityTypes()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var config = new SensorThingsServiceDefinition
        {
            BasePath = "/sta/v1.1"
        };

        // Act
        var result = await SensorThingsHandlers.GetServiceRoot(httpContext, config);

        // Assert
        result.Should().BeOfType<JsonHttpResult<object>>();
        var jsonResult = (JsonHttpResult<object>)result;

        var json = JsonSerializer.Serialize(jsonResult.Value);
        json.Should().Contain("Things");
        json.Should().Contain("Locations");
        json.Should().Contain("HistoricalLocations");
        json.Should().Contain("Datastreams");
        json.Should().Contain("Sensors");
        json.Should().Contain("ObservedProperties");
        json.Should().Contain("Observations");
        json.Should().Contain("FeaturesOfInterest");
    }

    [Fact]
    public async Task GetThing_WithExistingId_ReturnsThing()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var thingId = Guid.NewGuid().ToString();
        var expectedThing = new Thing
        {
            Id = thingId,
            Name = "Test Thing",
            Description = "Test Description",
            SelfLink = $"/sta/v1.1/Things({thingId})",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.GetThingAsync(thingId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedThing);

        // Act
        var result = await SensorThingsHandlers.GetThing(
            httpContext,
            repositoryMock.Object,
            thingId,
            null);

        // Assert
        result.Should().BeOfType<JsonHttpResult<Thing>>();
        var jsonResult = (JsonHttpResult<Thing>)result;
        jsonResult.Value.Should().Be(expectedThing);
    }

    [Fact]
    public async Task GetThing_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var thingId = Guid.NewGuid().ToString();

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.GetThingAsync(thingId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Thing?)null);

        // Act
        var result = await SensorThingsHandlers.GetThing(
            httpContext,
            repositoryMock.Object,
            thingId,
            null);

        // Assert
        result.Should().BeOfType<JsonHttpResult<object>>();
        var jsonResult = (JsonHttpResult<object>)result;
        jsonResult.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateThing_WithValidData_ReturnsCreated()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var thing = new Thing
        {
            Name = "New Thing",
            Description = "New Description"
        };

        var createdThing = thing with
        {
            Id = Guid.NewGuid().ToString(),
            SelfLink = "/sta/v1.1/Things(test-id)",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.CreateThingAsync(It.IsAny<Thing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdThing);

        // Act
        var result = await SensorThingsHandlers.CreateThing(
            httpContext,
            repositoryMock.Object,
            thing);

        // Assert
        result.Should().BeOfType<Created<Thing>>();
        var createdResult = (Created<Thing>)result;
        createdResult.Location.Should().Be(createdThing.SelfLink);
        createdResult.Value.Should().Be(createdThing);
    }

    [Fact]
    public async Task CreateThing_WithoutName_ReturnsBadRequest()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var thing = new Thing
        {
            Name = "",  // Empty name
            Description = "Description"
        };

        var repositoryMock = new Mock<ISensorThingsRepository>();

        // Act
        var result = await SensorThingsHandlers.CreateThing(
            httpContext,
            repositoryMock.Object,
            thing);

        // Assert
        result.Should().BeOfType<JsonHttpResult<object>>();
        var badRequestResult = (JsonHttpResult<object>)result;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateObservation_WithDataArrayPayload_RoutesToDataArrayHandler()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var datastreamId = Guid.NewGuid().ToString();

        var dataArrayPayload = new
        {
            Datastream = new { Id = datastreamId },
            components = new[] { "phenomenonTime", "result" },
            dataArray = new object[]
            {
                new object[] { "2025-11-05T10:00:00Z", 23.5 },
                new object[] { "2025-11-05T10:01:00Z", 23.6 }
            }
        };

        var jsonElement = JsonSerializer.SerializeToElement(dataArrayPayload);

        var createdObservations = new List<Observation>
        {
            new() { Id = Guid.NewGuid().ToString(), Result = 23.5, SelfLink = "/sta/v1.1/Observations(1)" },
            new() { Id = Guid.NewGuid().ToString(), Result = 23.6, SelfLink = "/sta/v1.1/Observations(2)" }
        };

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.CreateObservationsDataArrayAsync(It.IsAny<DataArrayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdObservations);

        // Act
        var result = await SensorThingsHandlers.CreateObservation(
            httpContext,
            repositoryMock.Object,
            jsonElement);

        // Assert
        repositoryMock.Verify(
            r => r.CreateObservationsDataArrayAsync(It.IsAny<DataArrayRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateObservation_WithSingleObservation_CreatesSingleObservation()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var datastreamId = Guid.NewGuid().ToString();

        var observationPayload = new
        {
            DatastreamId = datastreamId,
            PhenomenonTime = DateTime.UtcNow,
            Result = 23.5
        };

        var jsonElement = JsonSerializer.SerializeToElement(observationPayload);

        var createdObservation = new Observation
        {
            Id = Guid.NewGuid().ToString(),
            DatastreamId = datastreamId,
            Result = 23.5,
            SelfLink = "/sta/v1.1/Observations(test-id)"
        };

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.CreateObservationAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdObservation);

        // Act
        var result = await SensorThingsHandlers.CreateObservation(
            httpContext,
            repositoryMock.Object,
            jsonElement);

        // Assert
        repositoryMock.Verify(
            r => r.CreateObservationAsync(It.IsAny<Observation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateObservation_WithoutDatastreamId_ReturnsBadRequest()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        var observationPayload = new
        {
            PhenomenonTime = DateTime.UtcNow,
            Result = 23.5
            // Missing DatastreamId
        };

        var jsonElement = JsonSerializer.SerializeToElement(observationPayload);

        var repositoryMock = new Mock<ISensorThingsRepository>();

        // Act
        var result = await SensorThingsHandlers.CreateObservation(
            httpContext,
            repositoryMock.Object,
            jsonElement);

        // Assert
        result.Should().BeOfType<JsonHttpResult<object>>();
        var badRequestResult = (JsonHttpResult<object>)result;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetThings_WithFilterParameter_PassesFilterToRepository()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.GetThingsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Thing> { Items = new List<Thing>() });

        // Act
        await SensorThingsHandlers.GetThings(
            httpContext,
            repositoryMock.Object,
            "name eq 'Station'",
            null,
            null,
            null,
            null,
            null,
            false);

        // Assert
        repositoryMock.Verify(
            r => r.GetThingsAsync(
                It.Is<QueryOptions>(o => o.Filter != null && o.Filter.Property == "name"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetThings_WithPaginationParameters_PassesPaginationToRepository()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.GetThingsAsync(It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Thing> { Items = new List<Thing>() });

        // Act
        await SensorThingsHandlers.GetThings(
            httpContext,
            repositoryMock.Object,
            null,
            null,
            null,
            null,
            top: 50,
            skip: 100,
            count: true);

        // Assert
        repositoryMock.Verify(
            r => r.GetThingsAsync(
                It.Is<QueryOptions>(o => o.Top == 50 && o.Skip == 100 && o.Count == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateThing_WithValidData_ReturnsOk()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var thingId = Guid.NewGuid().ToString();
        var thing = new Thing
        {
            Name = "Updated Thing",
            Description = "Updated Description"
        };

        var updatedThing = thing with
        {
            Id = thingId,
            SelfLink = $"/sta/v1.1/Things({thingId})",
            UpdatedAt = DateTime.UtcNow
        };

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.UpdateThingAsync(thingId, thing, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedThing);

        // Act
        var result = await SensorThingsHandlers.UpdateThing(
            httpContext,
            repositoryMock.Object,
            thingId,
            thing);

        // Assert
        result.Should().BeOfType<Ok<Thing>>();
        var okResult = (Ok<Thing>)result;
        okResult.Value.Should().Be(updatedThing);
    }

    [Fact]
    public async Task DeleteThing_WithExistingId_ReturnsNoContent()
    {
        // Arrange
        var thingId = Guid.NewGuid().ToString();

        var repositoryMock = new Mock<ISensorThingsRepository>();
        repositoryMock
            .Setup(r => r.DeleteThingAsync(thingId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await SensorThingsHandlers.DeleteThing(
            repositoryMock.Object,
            thingId);

        // Assert
        result.Should().BeOfType<NoContent>();
    }
}
