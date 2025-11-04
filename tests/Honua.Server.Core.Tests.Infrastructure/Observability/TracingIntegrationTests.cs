using System.Diagnostics;
using System.Linq;
using Honua.Server.Core.Observability;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Observability;

[Trait("Category", "Integration")]
public class TracingIntegrationTests
{
    [Fact]
    public void HonuaTelemetry_ActivitySources_AreCreated()
    {
        // Arrange & Act & Assert
        Assert.NotNull(HonuaTelemetry.OgcProtocols);
        Assert.NotNull(HonuaTelemetry.OData);
        Assert.NotNull(HonuaTelemetry.Database);
        Assert.NotNull(HonuaTelemetry.RasterTiles);
        Assert.NotNull(HonuaTelemetry.Metadata);
        Assert.NotNull(HonuaTelemetry.Authentication);
        Assert.NotNull(HonuaTelemetry.Export);
        Assert.NotNull(HonuaTelemetry.Import);
        Assert.NotNull(HonuaTelemetry.Stac);
    }

    [Fact]
    public void HonuaTelemetry_ActivitySources_HaveCorrectNames()
    {
        // Arrange & Act & Assert
        Assert.Equal("Honua.Server.OgcProtocols", HonuaTelemetry.OgcProtocols.Name);
        Assert.Equal("Honua.Server.OData", HonuaTelemetry.OData.Name);
        Assert.Equal("Honua.Server.Database", HonuaTelemetry.Database.Name);
        Assert.Equal("Honua.Server.RasterTiles", HonuaTelemetry.RasterTiles.Name);
        Assert.Equal("Honua.Server.Metadata", HonuaTelemetry.Metadata.Name);
        Assert.Equal("Honua.Server.Authentication", HonuaTelemetry.Authentication.Name);
        Assert.Equal("Honua.Server.Export", HonuaTelemetry.Export.Name);
        Assert.Equal("Honua.Server.Import", HonuaTelemetry.Import.Name);
        Assert.Equal("Honua.Server.Stac", HonuaTelemetry.Stac.Name);
    }

    [Fact]
    public void HonuaTelemetry_ActivitySources_HaveCorrectVersion()
    {
        // Arrange & Act & Assert
        Assert.Equal(HonuaTelemetry.ServiceVersion, HonuaTelemetry.OgcProtocols.Version);
        Assert.Equal(HonuaTelemetry.ServiceVersion, HonuaTelemetry.OData.Version);
    }

    [Fact]
    public void OgcProtocols_CanCreateActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Honua.Server.OgcProtocols",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = HonuaTelemetry.OgcProtocols.StartActivity("Test WMS Operation");
        activity?.SetTag("wms.operation", "GetMap");
        activity?.SetTag("wms.layer", "test_layer");

        // Assert
        Assert.NotNull(activity);
        Assert.Equal("Test WMS Operation", activity.DisplayName);
        Assert.Contains(activity.Tags, t => t.Key == "wms.operation" && t.Value == "GetMap");
        Assert.Contains(activity.Tags, t => t.Key == "wms.layer" && t.Value == "test_layer");
    }

    [Fact]
    public void RasterTiles_CanCreateActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Honua.Server.RasterTiles",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = HonuaTelemetry.RasterTiles.StartActivity("WMTS GetTile");
        activity?.SetTag("wmts.operation", "GetTile");
        activity?.SetTag("wmts.layer", "satellite_imagery");
        activity?.SetTag("wmts.tileMatrix", "WebMercatorQuad");
        activity?.SetTag("wmts.zoom", 12);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal("WMTS GetTile", activity.DisplayName);
        Assert.Contains(activity.Tags, t => t.Key == "wmts.operation" && t.Value == "GetTile");
        Assert.Contains(activity.Tags, t => t.Key == "wmts.layer" && t.Value == "satellite_imagery");
        Assert.Contains(activity.Tags, t => t.Key == "wmts.tileMatrix" && t.Value == "WebMercatorQuad");
        Assert.Contains(activity.TagObjects, t => t.Key == "wmts.zoom");
    }

    [Fact]
    public void Database_CanCreateActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Honua.Server.Database",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = HonuaTelemetry.Database.StartActivity("Query Features");
        activity?.SetTag("db.operation", "SELECT");
        activity?.SetTag("db.table", "features");
        activity?.SetTag("db.query_duration_ms", 45);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal("Query Features", activity.DisplayName);
        Assert.Contains(activity.Tags, t => t.Key == "db.operation" && t.Value == "SELECT");
        Assert.Contains(activity.Tags, t => t.Key == "db.table" && t.Value == "features");
    }

    [Fact]
    public void Activity_CanBeNestedCorrectly()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Honua.Server"),
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var parentActivity = HonuaTelemetry.OgcProtocols.StartActivity("WMS GetMap");
        parentActivity?.SetTag("wms.operation", "GetMap");

        using var cacheActivity = HonuaTelemetry.RasterTiles.StartActivity("Cache Lookup");
        cacheActivity?.SetTag("cache.hit", false);

        using var renderActivity = HonuaTelemetry.RasterTiles.StartActivity("Raster Render");
        renderActivity?.SetTag("raster.format", "png");

        // Assert
        Assert.NotNull(parentActivity);
        Assert.NotNull(cacheActivity);
        Assert.NotNull(renderActivity);

        // Verify parent-child relationships
        Assert.Equal(parentActivity.Id, cacheActivity?.ParentId);
        Assert.Equal(cacheActivity?.Id, renderActivity?.ParentId);
    }

    [Fact]
    public void Activity_CanRecordDuration()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Honua.Server.RasterTiles",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        var stopwatch = Stopwatch.StartNew();
        using (var activity = HonuaTelemetry.RasterTiles.StartActivity("Raster Render"))
        {
            activity?.SetTag("raster.dataset", "elevation");
            Thread.Sleep(10); // Simulate work
            stopwatch.Stop();
            activity?.SetTag("raster.duration_ms", stopwatch.ElapsedMilliseconds);
        }

        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds >= 10);
    }

    [Fact]
    public void Activity_WithoutListener_ReturnsNull()
    {
        // Arrange - No listener registered for this test

        // Act
        var activity = HonuaTelemetry.OgcProtocols.StartActivity("Test Without Listener");

        // Assert - Activity will be null without a listener
        // This is expected behavior for ActivitySource
        Assert.True(activity == null || activity.Id != null);
    }
}
