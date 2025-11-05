// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FluentAssertions;
using Honua.Server.Enterprise.Sensors.Data.Postgres;
using Honua.Server.Enterprise.Sensors.Models;
using Honua.Server.Enterprise.Sensors.Query;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Enterprise.Tests.Sensors.Data;

[Collection("SensorThings")]
[Trait("Category", "Integration")]
[Trait("Feature", "SensorThings")]
[Trait("Component", "Repository")]
public class PostgresSensorThingsRepositoryTests
{
    private readonly SensorThingsTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PostgresSensorThingsRepositoryTests(SensorThingsTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [SkippableFact]
    public async Task CreateThingAsync_WithValidData_ReturnsThingWithDynamicSelfLink()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition
            {
                BasePath = "/test/v1.1"  // Test custom base path
            };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var thing = new Thing
            {
                Name = "Test Weather Station",
                Description = "A test weather station for unit testing",
                Properties = new Dictionary<string, object>
                {
                    ["location"] = "Rooftop",
                    ["owner"] = "Test Department"
                }
            };

            // Act
            var created = await repository.CreateThingAsync(thing);

            // Assert
            created.Should().NotBeNull();
            created.Id.Should().NotBeNullOrEmpty();
            created.Name.Should().Be(thing.Name);
            created.Description.Should().Be(thing.Description);
            created.Properties.Should().NotBeNull();
            created.Properties!["location"].ToString().Should().Be("Rooftop");
            created.SelfLink.Should().StartWith("/test/v1.1/Things(");
            created.SelfLink.Should().Contain(created.Id);
            created.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            created.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            _output.WriteLine($"Created Thing: {created.Id} with self-link: {created.SelfLink}");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetThingAsync_WithExistingId_ReturnsThing()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var created = await repository.CreateThingAsync(new Thing
            {
                Name = "Test Thing",
                Description = "Test Description"
            });

            // Act
            var retrieved = await repository.GetThingAsync(created.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(created.Id);
            retrieved.Name.Should().Be(created.Name);
            retrieved.Description.Should().Be(created.Description);
            retrieved.SelfLink.Should().Be(created.SelfLink);
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetThingAsync_WithNonExistentId_ReturnsNull()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            // Act
            var retrieved = await repository.GetThingAsync(Guid.NewGuid().ToString());

            // Assert
            retrieved.Should().BeNull();
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetThingsAsync_WithFiltering_ReturnsFilteredResults()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            // Create test data
            await repository.CreateThingAsync(new Thing { Name = "Station A", Description = "First station" });
            await repository.CreateThingAsync(new Thing { Name = "Station B", Description = "Second station" });
            await repository.CreateThingAsync(new Thing { Name = "Device C", Description = "Third device" });

            // Act
            var options = new QueryOptions
            {
                Filter = new FilterExpression
                {
                    Property = "name",
                    Operator = ComparisonOperator.Equals,
                    Value = "Station A"
                }
            };
            var result = await repository.GetThingsAsync(options);

            // Assert
            result.Items.Should().HaveCount(1);
            result.Items[0].Name.Should().Be("Station A");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetThingsAsync_WithPagination_ReturnsPagedResults()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            // Create 10 test things
            for (int i = 0; i < 10; i++)
            {
                await repository.CreateThingAsync(new Thing
                {
                    Name = $"Thing {i:D2}",
                    Description = $"Description {i}"
                });
            }

            // Act
            var options = new QueryOptions { Top = 3, Skip = 2 };
            var result = await repository.GetThingsAsync(options);

            // Assert
            result.Items.Should().HaveCount(3);
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task UpdateThingAsync_WithNewData_UpdatesSuccessfully()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var created = await repository.CreateThingAsync(new Thing
            {
                Name = "Original Name",
                Description = "Original Description"
            });

            // Act
            var updated = await repository.UpdateThingAsync(created.Id, new Thing
            {
                Name = "Updated Name",
                Description = "Updated Description"
            });

            // Assert
            updated.Name.Should().Be("Updated Name");
            updated.Description.Should().Be("Updated Description");
            updated.UpdatedAt.Should().BeAfter(created.UpdatedAt);
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task DeleteThingAsync_WithExistingId_DeletesSuccessfully()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var created = await repository.CreateThingAsync(new Thing
            {
                Name = "To Delete",
                Description = "This will be deleted"
            });

            // Act
            await repository.DeleteThingAsync(created.Id);

            // Assert
            var retrieved = await repository.GetThingAsync(created.Id);
            retrieved.Should().BeNull();
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task CreateLocationAsync_WithGeometry_StoresGeometryCorrectly()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var point = geometryFactory.CreatePoint(new Coordinate(-122.4194, 37.7749)); // San Francisco

            var location = new Location
            {
                Name = "SF Location",
                Description = "A location in San Francisco",
                EncodingType = "application/geo+json",
                Location = point
            };

            // Act
            var created = await repository.CreateLocationAsync(location);

            // Assert
            created.Should().NotBeNull();
            created.Location.Should().NotBeNull();
            created.Location.SRID.Should().Be(4326);
            created.Location.Coordinate.X.Should().BeApproximately(-122.4194, 0.0001);
            created.Location.Coordinate.Y.Should().BeApproximately(37.7749, 0.0001);
            created.SelfLink.Should().StartWith("/sta/v1.1/Locations(");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetOrCreateFeatureOfInterestAsync_WithExistingGeometry_ReturnsExisting()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var point = geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));

            // Create first FOI
            var first = await repository.GetOrCreateFeatureOfInterestAsync(
                "Test FOI",
                "A test feature",
                point);

            // Act - try to create another with same geometry
            var second = await repository.GetOrCreateFeatureOfInterestAsync(
                "Another FOI",
                "Different name but same geometry",
                point);

            // Assert
            second.Id.Should().Be(first.Id, "Should return existing FOI with same geometry");
            second.Name.Should().Be(first.Name, "Should preserve original name");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetOrCreateFeatureOfInterestAsync_WithNewGeometry_CreatesNew()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
            var point1 = geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
            var point2 = geometryFactory.CreatePoint(new Coordinate(-122.5, 37.9)); // Different location

            // Create first FOI
            var first = await repository.GetOrCreateFeatureOfInterestAsync(
                "FOI 1",
                "First feature",
                point1);

            // Act - create another with different geometry
            var second = await repository.GetOrCreateFeatureOfInterestAsync(
                "FOI 2",
                "Second feature",
                point2);

            // Assert
            second.Id.Should().NotBe(first.Id, "Should create new FOI with different geometry");
            second.Name.Should().Be("FOI 2");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task CreateDatastreamAsync_WithUnitOfMeasurement_SerializesCorrectly()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            // Create prerequisites
            var thing = await repository.CreateThingAsync(new Thing { Name = "Test Thing", Description = "Test" });
            var sensor = await repository.CreateSensorAsync(new Sensor
            {
                Name = "Test Sensor",
                Description = "Test",
                EncodingType = "application/pdf",
                Metadata = "test metadata"
            });
            var observedProperty = await repository.CreateObservedPropertyAsync(new ObservedProperty
            {
                Name = "Temperature",
                Description = "Air temperature",
                Definition = "http://www.qudt.org/qudt/owl/1.0.0/quantity/Instances.html#AirTemperature"
            });

            var datastream = new Datastream
            {
                Name = "Temperature Stream",
                Description = "Stream of temperature readings",
                ObservationType = "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
                UnitOfMeasurement = new UnitOfMeasurement
                {
                    Name = "Degree Celsius",
                    Symbol = "°C",
                    Definition = "http://www.qudt.org/qudt/owl/1.0.0/unit/Instances.html#DegreeCelsius"
                },
                ThingId = thing.Id,
                SensorId = sensor.Id,
                ObservedPropertyId = observedProperty.Id
            };

            // Act
            var created = await repository.CreateDatastreamAsync(datastream);

            // Assert
            created.UnitOfMeasurement.Should().NotBeNull();
            created.UnitOfMeasurement.Name.Should().Be("Degree Celsius");
            created.UnitOfMeasurement.Symbol.Should().Be("°C");
            created.UnitOfMeasurement.Definition.Should().Contain("DegreeCelsius");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task GetThingDatastreamsAsync_ReturnsRelatedDatastreams()
    {
        Skip.IfNot(_fixture.IsAvailable, "PostgreSQL container not available");

        // Arrange
        var (connection, transaction) = await _fixture.CreateTransactionScopeAsync();
        try
        {
            var config = new SensorThingsServiceDefinition { BasePath = "/sta/v1.1" };
            var repository = new PostgresSensorThingsRepository(connection, config, NullLogger<PostgresSensorThingsRepository>.Instance);

            // Create test data
            var thing = await repository.CreateThingAsync(new Thing { Name = "Weather Station", Description = "Test" });
            var sensor = await repository.CreateSensorAsync(new Sensor
            {
                Name = "Temp Sensor",
                Description = "Test",
                EncodingType = "application/pdf",
                Metadata = "test"
            });
            var observedProperty = await repository.CreateObservedPropertyAsync(new ObservedProperty
            {
                Name = "Temperature",
                Description = "Air temp",
                Definition = "http://example.com/temp"
            });

            var datastream1 = await repository.CreateDatastreamAsync(new Datastream
            {
                Name = "Stream 1",
                Description = "First stream",
                ObservationType = "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
                UnitOfMeasurement = new UnitOfMeasurement { Name = "Celsius", Symbol = "°C", Definition = "http://example.com" },
                ThingId = thing.Id,
                SensorId = sensor.Id,
                ObservedPropertyId = observedProperty.Id
            });

            var datastream2 = await repository.CreateDatastreamAsync(new Datastream
            {
                Name = "Stream 2",
                Description = "Second stream",
                ObservationType = "http://www.opengis.net/def/observationType/OGC-OM/2.0/OM_Measurement",
                UnitOfMeasurement = new UnitOfMeasurement { Name = "Celsius", Symbol = "°C", Definition = "http://example.com" },
                ThingId = thing.Id,
                SensorId = sensor.Id,
                ObservedPropertyId = observedProperty.Id
            });

            // Act
            var result = await repository.GetThingDatastreamsAsync(thing.Id, new QueryOptions());

            // Assert
            result.Items.Should().HaveCount(2);
            result.Items.Should().Contain(d => d.Name == "Stream 1");
            result.Items.Should().Contain(d => d.Name == "Stream 2");
        }
        finally
        {
            await transaction.RollbackAsync();
            await connection.DisposeAsync();
        }
    }
}
