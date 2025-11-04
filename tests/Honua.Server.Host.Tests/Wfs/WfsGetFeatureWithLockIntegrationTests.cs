using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Wfs;
using Honua.Server.Host.Tests.TestUtilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using NetTopologySuite.Geometries;
using Xunit;

namespace Honua.Server.Host.Tests.Wfs;

[Trait("Category", "Integration")]
[Trait("Feature", "WFS")]
public class WfsGetFeatureWithLockIntegrationTests
{
    [Fact]
    public async Task HandleGetFeatureWithLockAsync_StreamsGmlWithLockMetadata()
    {
        // Arrange
        const string serviceId = "svc";
        const string layerId = "parcels";

        var snapshot = new MetadataSnapshotBuilder()
            .WithFolder("root", "Root Folder")
            .WithDataSource("primary")
            .WithService(serviceId, "root", "primary", serviceType: "feature", configure: service =>
                service.WithOgc(ogc => ogc.WithWfsEnabled()))
            .WithLayer(layerId, serviceId, geometryType: "Point", idField: "OBJECTID", geometryField: "Shape", configure: layer =>
            {
                layer.WithField("OBJECTID", "integer", nullable: false);
                layer.WithField("Name", "string");
                layer.WithField("Shape", "geometry");
            })
            .Build();

        var service = snapshot.Services.Single(s => string.Equals(s.Id, serviceId, StringComparison.OrdinalIgnoreCase));
        var layer = snapshot.Layers.Single(l => string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));
        var dataSource = snapshot.DataSources.Single(ds => string.Equals(ds.Id, service.DataSourceId, StringComparison.OrdinalIgnoreCase));

        var metadataRegistryMock = new Mock<IMetadataRegistry>(MockBehavior.Strict);
        metadataRegistryMock.SetupGet(r => r.IsInitialized).Returns(true);
        metadataRegistryMock.Setup(r => r.EnsureInitializedAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        metadataRegistryMock.Setup(r => r.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken _) => new ValueTask<MetadataSnapshot>(snapshot));
        metadataRegistryMock.Setup(r => r.GetChangeToken()).Returns(NullChangeToken.Singleton);
        metadataRegistryMock.Setup(r => r.TryGetSnapshot(out snapshot)).Returns(true);
        metadataRegistryMock.SetupGet(r => r.Snapshot).Returns(snapshot);
        metadataRegistryMock.Setup(r => r.ReloadAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        metadataRegistryMock.Setup(r => r.UpdateAsync(It.IsAny<MetadataSnapshot>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var catalogSnapshot = BuildCatalogProjection(snapshot, service, layer);
        var catalogMock = new Mock<ICatalogProjectionService>(MockBehavior.Loose);
        catalogMock.Setup(c => c.GetSnapshot()).Returns(catalogSnapshot);
        catalogMock.Setup(c => c.Dispose());

        var providerMock = new Mock<IDataStoreProvider>(MockBehavior.Strict);
        var featureContext = new FeatureContext(snapshot, service, layer, dataSource, providerMock.Object);

        var resolverMock = new Mock<IFeatureContextResolver>(MockBehavior.Strict);
        resolverMock.Setup(r => r.ResolveAsync(serviceId, layerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureContext);

        var repositoryMock = new Mock<IFeatureRepository>(MockBehavior.Strict);

        var features = new[]
        {
            CreateFeatureRecord(layer.GeometryField, 1, "Alpha", 10, 20),
            CreateFeatureRecord(layer.GeometryField, 2, "Beta", 11.5, 21.25)
        };

        repositoryMock.Setup(r => r.CountAsync(service.Id, layer.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        repositoryMock.Setup(r => r.QueryAsync(service.Id, layer.Id, It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => EnumerateAsync(features));

        var lockManagerMock = new Mock<IWfsLockManager>(MockBehavior.Strict);
        lockManagerMock.Setup(m => m.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<IReadOnlyCollection<WfsLockTarget>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string owner, TimeSpan _, IReadOnlyCollection<WfsLockTarget> targets, CancellationToken _) =>
                new WfsLockAcquisitionResult(
                    true,
                    new WfsLockAcquisition("LOCK-123", owner, DateTimeOffset.UtcNow.AddMinutes(5), targets.ToList()),
                    null));
        lockManagerMock.Setup(m => m.ValidateAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<WfsLockTarget>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WfsLockValidationResult(true, null));
        lockManagerMock.Setup(m => m.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyCollection<WfsLockTarget>?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        lockManagerMock.Setup(m => m.ResetAsync()).Returns(Task.CompletedTask);

        var editOrchestratorMock = new Mock<IFeatureEditOrchestrator>(MockBehavior.Strict);
        var csvExporterMock = new Mock<ICsvExporter>(MockBehavior.Strict);
        var shapefileExporterMock = new Mock<IShapefileExporter>(MockBehavior.Strict);

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            Request =
            {
                Method = HttpMethods.Get,
                Scheme = "https",
                Host = new HostString("example.test"),
                Path = "/wfs",
                QueryString = new QueryString($"?service=WFS&request=GetFeatureWithLock&typeNames={serviceId}:{layerId}&srsName=EPSG:3857")
            },
            Response =
            {
                Body = new MemoryStream()
            },
            RequestServices = serviceProvider
        };

        // Act
        var result = await WfsHandlers.HandleAsync(
            httpContext,
            metadataRegistryMock.Object,
            catalogMock.Object,
            resolverMock.Object,
            repositoryMock.Object,
            lockManagerMock.Object,
            editOrchestratorMock.Object,
            csvExporterMock.Object,
            shapefileExporterMock.Object,
            CancellationToken.None).ConfigureAwait(false);

        await result.ExecuteAsync(httpContext).ConfigureAwait(false);

        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body);
        var payload = await reader.ReadToEndAsync().ConfigureAwait(false);

        // Assert
        payload.Should().Contain("lockId=\"LOCK-123\"");
        payload.Should().Contain("numberMatched=\"5\"");
        payload.Should().Contain("numberReturned=\"2\"");
        payload.Should().Contain("<gml:boundedBy>");
        payload.Should().Contain("<gml:lowerCorner>10 20</gml:lowerCorner>");
        payload.Should().Contain("<gml:upperCorner>11.5 21.25</gml:upperCorner>");

        metadataRegistryMock.VerifyAll();
        resolverMock.VerifyAll();
        repositoryMock.VerifyAll();
        lockManagerMock.Verify(m => m.TryAcquireAsync("anonymous", It.IsAny<TimeSpan>(), It.IsAny<IReadOnlyCollection<WfsLockTarget>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CatalogProjectionSnapshot BuildCatalogProjection(MetadataSnapshot snapshot, ServiceDefinition service, LayerDefinition layer)
    {
        var layerView = new CatalogLayerView
        {
            Layer = layer
        };

        var serviceView = new CatalogServiceView
        {
            Service = service,
            FolderTitle = "Root Folder",
            FolderOrder = 0,
            Layers = new[] { layerView }
        };

        var groupView = new CatalogGroupView
        {
            Id = "root",
            Title = "Root Folder",
            Services = new[] { serviceView }
        };

        return new CatalogProjectionSnapshot(
            new[] { groupView },
            new Dictionary<string, CatalogGroupView>(StringComparer.OrdinalIgnoreCase) { ["root"] = groupView },
            new Dictionary<string, CatalogServiceView>(StringComparer.OrdinalIgnoreCase) { [service.Id] = serviceView },
            new Dictionary<string, CatalogDiscoveryRecord>(StringComparer.OrdinalIgnoreCase));
    }

    private static FeatureRecord CreateFeatureRecord(string geometryField, int id, string name, double x, double y)
    {
        var point = new Point(x, y) { SRID = 3857 };
        return new FeatureRecord(new Dictionary<string, object?>
        {
            ["OBJECTID"] = id,
            ["Name"] = name,
            [geometryField] = point
        });
    }

    private static async IAsyncEnumerable<FeatureRecord> EnumerateAsync(IEnumerable<FeatureRecord> records)
    {
        foreach (var record in records)
        {
            yield return record;
            await Task.Yield();
        }
    }
}
