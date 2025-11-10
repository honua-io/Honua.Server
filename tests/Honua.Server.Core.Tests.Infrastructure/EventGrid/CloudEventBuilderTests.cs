// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Models;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.EventGrid;

public class CloudEventBuilderTests
{
    [Fact]
    public void Build_WithRequiredFields_CreatesValidCloudEvent()
    {
        // Arrange
        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id-123")
            .WithSource("honua.io/features/parcels")
            .WithType(HonuaEventTypes.FeatureCreated);

        // Act
        var cloudEvent = builder.Build();

        // Assert
        Assert.Equal("test-id-123", cloudEvent.Id);
        Assert.Equal("honua.io/features/parcels", cloudEvent.Source);
        Assert.Equal(HonuaEventTypes.FeatureCreated, cloudEvent.Type);
        Assert.Equal("1.0", cloudEvent.SpecVersion);
        Assert.Equal("application/json", cloudEvent.DataContentType);
    }

    [Fact]
    public void Build_WithoutId_ThrowsException()
    {
        // Arrange
        var builder = new HonuaCloudEventBuilder()
            .WithSource("honua.io/features/parcels")
            .WithType(HonuaEventTypes.FeatureCreated);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Event ID is required", exception.Message);
    }

    [Fact]
    public void Build_WithoutSource_ThrowsException()
    {
        // Arrange
        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id")
            .WithType(HonuaEventTypes.FeatureCreated);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Event source is required", exception.Message);
    }

    [Fact]
    public void Build_WithoutType_ThrowsException()
    {
        // Arrange
        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id")
            .WithSource("honua.io/features/parcels");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("Event type is required", exception.Message);
    }

    [Fact]
    public void Build_WithAllFields_CreatesCompleteCloudEvent()
    {
        // Arrange
        var time = DateTimeOffset.UtcNow;
        var data = new { foo = "bar" };
        var bbox = new[] { -122.5, 37.7, -122.3, 37.9 };

        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id-123")
            .WithSource("honua.io/features/parcels")
            .WithType(HonuaEventTypes.FeatureCreated)
            .WithSubject("parcel-456")
            .WithData(data)
            .WithTenantId("tenant-abc")
            .WithBoundingBox(bbox)
            .WithCrs("EPSG:4326")
            .WithCollection("parcels")
            .WithSeverity("warning")
            .WithTime(time)
            .WithDataSchema("https://schema.honua.io/feature/v1");

        // Act
        var cloudEvent = builder.Build();

        // Assert
        Assert.Equal("test-id-123", cloudEvent.Id);
        Assert.Equal("honua.io/features/parcels", cloudEvent.Source);
        Assert.Equal(HonuaEventTypes.FeatureCreated, cloudEvent.Type);
        Assert.Equal("parcel-456", cloudEvent.Subject);
        Assert.Equal(data, cloudEvent.Data);
        Assert.Equal("tenant-abc", cloudEvent.TenantId);
        Assert.Equal(bbox, cloudEvent.BoundingBox);
        Assert.Equal("EPSG:4326", cloudEvent.Crs);
        Assert.Equal("parcels", cloudEvent.Collection);
        Assert.Equal("warning", cloudEvent.Severity);
        Assert.Equal(time, cloudEvent.Time);
        Assert.Equal("https://schema.honua.io/feature/v1", cloudEvent.DataSchema);
    }

    [Fact]
    public void Build_WithGeometryEnvelope_SetsBoundingBox()
    {
        // Arrange
        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var point = geometryFactory.CreatePoint(new Coordinate(-122.4, 37.8));
        var envelope = point.EnvelopeInternal;

        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id")
            .WithSource("honua.io/features/parcels")
            .WithType(HonuaEventTypes.FeatureCreated)
            .WithBoundingBox(envelope);

        // Act
        var cloudEvent = builder.Build();

        // Assert
        Assert.NotNull(cloudEvent.BoundingBox);
        Assert.Equal(4, cloudEvent.BoundingBox.Length);
        Assert.Equal(-122.4, cloudEvent.BoundingBox[0]); // minX
        Assert.Equal(37.8, cloudEvent.BoundingBox[1]);   // minY
        Assert.Equal(-122.4, cloudEvent.BoundingBox[2]); // maxX
        Assert.Equal(37.8, cloudEvent.BoundingBox[3]);   // maxY
    }

    [Fact]
    public void Build_WithNullEnvelope_DoesNotSetBoundingBox()
    {
        // Arrange
        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id")
            .WithSource("honua.io/features/parcels")
            .WithType(HonuaEventTypes.FeatureCreated)
            .WithBoundingBox((Envelope?)null);

        // Act
        var cloudEvent = builder.Build();

        // Assert
        Assert.Null(cloudEvent.BoundingBox);
    }

    [Fact]
    public void Build_DefaultTime_IsCloseToUtcNow()
    {
        // Arrange
        var beforeBuild = DateTimeOffset.UtcNow;
        var builder = new HonuaCloudEventBuilder()
            .WithId("test-id")
            .WithSource("honua.io/features/parcels")
            .WithType(HonuaEventTypes.FeatureCreated);

        // Act
        var cloudEvent = builder.Build();
        var afterBuild = DateTimeOffset.UtcNow;

        // Assert
        Assert.InRange(cloudEvent.Time, beforeBuild.AddSeconds(-1), afterBuild.AddSeconds(1));
    }
}
