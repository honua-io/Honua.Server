// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.LocationServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Honua.Server.Core.Tests.LocationServices.TestUtilities;

/// <summary>
/// Test fixture for location service tests providing common setup and utilities.
/// </summary>
public class LocationServiceTestFixture : IDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public IConfiguration Configuration { get; }
    public Mock<ILogger<T>> CreateMockLogger<T>() => new Mock<ILogger<T>>();

    public LocationServiceTestFixture()
    {
        // Build configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocationServices:GeocodingProvider"] = "nominatim",
            ["LocationServices:RoutingProvider"] = "osrm",
            ["LocationServices:BasemapTileProvider"] = "openstreetmap",
            ["LocationServices:Nominatim:BaseUrl"] = "https://nominatim.openstreetmap.org",
            ["LocationServices:Nominatim:UserAgent"] = "HonuaServer-Tests/1.0",
            ["LocationServices:Osrm:BaseUrl"] = "https://router.project-osrm.org",
            ["LocationServices:OsmTiles:UserAgent"] = "HonuaServer-Tests/1.0",
            ["LocationServices:AzureMaps:SubscriptionKey"] = "test-key-12345",
            ["LocationServices:AzureMaps:BaseUrl"] = "https://atlas.microsoft.com"
        });
        Configuration = configBuilder.Build();

        // Build service collection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddDebug());
        services.AddHttpClient();
        services.AddSingleton(Configuration);

        ServiceProvider = services.BuildServiceProvider();
    }

    public HttpClient CreateMockHttpClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler);
    }

    public Mock<HttpMessageHandler> CreateMockHttpMessageHandler()
    {
        var mock = new Mock<HttpMessageHandler>();
        return mock;
    }

    public void Dispose()
    {
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Collection definition for sharing test fixture across test classes.
/// </summary>
[CollectionDefinition("LocationService")]
public class LocationServiceTestCollection : ICollectionFixture<LocationServiceTestFixture>
{
}
