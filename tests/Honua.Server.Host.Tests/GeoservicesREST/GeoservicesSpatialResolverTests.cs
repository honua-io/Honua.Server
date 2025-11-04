using Honua.Server.Core.Metadata;
using Honua.Server.Host.GeoservicesREST.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Host.Tests.GeoservicesREST;

/// <summary>
/// Unit tests for <see cref="GeoservicesSpatialResolver"/> public surface (ResolveTargetWkid).
/// Exercises query, header, service, and layer fallbacks.
/// </summary>
public sealed class GeoservicesSpatialResolverTests
{
    [Fact]
    public void ResolveTargetWkid_PrefersOutSrQueryParameter()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?outSR=EPSG:3857");

        var layer = GeoservicesTestFactory.CreateLayerDefinition();
        var service = GeoservicesTestFactory.CreateServiceDefinition();

        // Act
        var wkid = GeoservicesSpatialResolver.ResolveTargetWkid(context.Request, service, layer);

        // Assert
        Assert.Equal(3857, wkid);
    }

    [Fact]
    public void ResolveTargetWkid_FallsBackToAcceptCrsHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["Accept-Crs"] = new StringValues("EPSG:32632");

        var layer = GeoservicesTestFactory.CreateLayerDefinition();
        var service = GeoservicesTestFactory.CreateServiceDefinition();

        // Act
        var wkid = GeoservicesSpatialResolver.ResolveTargetWkid(context.Request, service, layer);

        // Assert
        Assert.Equal(32632, wkid);
    }

    [Fact]
    public void ResolveTargetWkid_UsesServiceDefaultCrs()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var service = GeoservicesTestFactory.CreateServiceDefinition(defaultCrs: "EPSG:2154");
        var layer = GeoservicesTestFactory.CreateLayerDefinition();

        // Act
        var wkid = GeoservicesSpatialResolver.ResolveTargetWkid(context.Request, service, layer);

        // Assert
        Assert.Equal(2154, wkid);
    }

    [Fact]
    public void ResolveTargetWkid_UsesLayerStorageSrid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = GeoservicesTestFactory.CreateLayerDefinition()
            with
            {
                Storage = new LayerStorageDefinition { Srid = 3857 }
            };
        var service = GeoservicesTestFactory.CreateServiceDefinition(defaultCrs: null);

        // Act
        var wkid = GeoservicesSpatialResolver.ResolveTargetWkid(context.Request, service, layer);

        // Assert
        Assert.Equal(3857, wkid);
    }

    [Fact]
    public void ResolveTargetWkid_DefaultsTo4326()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var layer = GeoservicesTestFactory.CreateLayerDefinition(crs: new[] { "EPSG:4326" });
        var service = GeoservicesTestFactory.CreateServiceDefinition(defaultCrs: null);

        // Act
        var wkid = GeoservicesSpatialResolver.ResolveTargetWkid(context.Request, service, layer);

        // Assert
        Assert.Equal(4326, wkid);
    }
}
