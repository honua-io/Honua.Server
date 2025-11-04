using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Host.Carto;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.OgcProtocols.Carto;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class CartoSecurityTests
{
    [Fact]
    public void CartoSqlQueryParser_ShouldRejectInvalidDataset()
    {
        var parser = new CartoSqlQueryParser();

        var sql = "SELECT * FROM invalid_dataset WHERE 1 = 1";

        var result = parser.TryParse(sql, out _, out var error);

        result.Should().BeFalse();
        error.Should().NotBeNullOrEmpty();
        error.Should().Contain("FROM clause must reference a dataset using the pattern");
}

    [Fact]
    public void CartoSqlQueryParser_ShouldAcceptValidSql()
    {
        var parser = new CartoSqlQueryParser();
        var sql = "SELECT * FROM service1.layer1";

        var result = parser.TryParse(sql, out var query, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        query.Should().NotBeNull();
    }

    [Fact]
    public async Task CartoSqlQueryExecutor_ShouldExecuteCountQuery()
    {
        var parser = new CartoSqlQueryParser();

        var repoMock = new Mock<IFeatureRepository>();
        repoMock.Setup(x => x.CountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(100L);

        var service = new ServiceDefinition
        {
            Id = "service1",
            Title = "Test Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "test",
            Enabled = true
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "service1",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int64", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", Nullable = true }
            }
        };

        var layerView = new CatalogLayerView
        {
            Layer = layer
        };

        var resolver = CreateResolver(service, layerView);
        var executor = new CartoSqlQueryExecutor(resolver, parser, repoMock.Object);

        var result = await executor.ExecuteAsync("SELECT COUNT(*) FROM service1.layer1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(StatusCodes.Status200OK);
        result.Response.Should().NotBeNull();
        var response = result.Response!;
        response.TotalRows.Should().Be(1);
        response.Rows.Should().ContainSingle()
            .Which.TryGetValue("count", out var value)
            .Should().BeTrue();
        response.Rows[0]["count"].Should().Be(100L);
    }

    [Fact]
    public void CartoSqlQueryParser_ShouldValidateSqlLength()
    {
        var parser = new CartoSqlQueryParser();
        var validSql = "SELECT * FROM service1.layer1 WHERE name = 'test'";

        var result = parser.TryParse(validSql, out _, out var error);

        if (!result)
        {
            error.Should().NotContain("exceeds maximum allowed length");
        }
    }

    [Fact]
    public async Task CartoSqlQueryExecutor_ShouldEnforceLimits()
    {
        var parser = new CartoSqlQueryParser();

        var service = new ServiceDefinition
        {
            Id = "service1",
            Title = "Test Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "test",
            Enabled = true
        };

        var layer = new LayerDefinition
        {
            Id = "layer1",
            ServiceId = "service1",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "id", DataType = "int64", Nullable = false },
                new FieldDefinition { Name = "name", DataType = "string", Nullable = true }
            },
            Query = new LayerQueryDefinition
            {
                MaxRecordCount = 10
            }
        };

        var layerView = new CatalogLayerView
        {
            Layer = layer
        };

        var resolver = CreateResolver(service, layerView);

        var repoMock = new Mock<IFeatureRepository>();
        repoMock.Setup(x => x.QueryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyAsyncEnumerable());
        repoMock.Setup(x => x.CountAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FeatureQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        var executor = new CartoSqlQueryExecutor(resolver, parser, repoMock.Object);

        var result = await executor.ExecuteAsync("SELECT * FROM service1.layer1 LIMIT 1000", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private static CartoDatasetResolver CreateResolver(ServiceDefinition service, CatalogLayerView layerView)
    {
        var serviceView = new CatalogServiceView
        {
            Service = service,
            FolderTitle = "Root",
            Layers = new[] { layerView }
        };

        return new CartoDatasetResolver(new StubCatalogProjectionService(serviceView));
    }

    private static async IAsyncEnumerable<FeatureRecord> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private sealed class StubCatalogProjectionService : ICatalogProjectionService
    {
        private readonly CatalogServiceView _serviceView;

        public StubCatalogProjectionService(CatalogServiceView serviceView)
        {
            _serviceView = serviceView;
        }

        public CatalogProjectionSnapshot GetSnapshot()
        {
            var serviceIndex = new Dictionary<string, CatalogServiceView>(StringComparer.OrdinalIgnoreCase)
            {
                [_serviceView.Service.Id] = _serviceView
            };

            return new CatalogProjectionSnapshot(
                Array.Empty<CatalogGroupView>(),
                new Dictionary<string, CatalogGroupView>(StringComparer.OrdinalIgnoreCase),
                serviceIndex,
                new Dictionary<string, CatalogDiscoveryRecord>(StringComparer.OrdinalIgnoreCase));
        }

        public IReadOnlyList<CatalogGroupView> GetGroups() => Array.Empty<CatalogGroupView>();

        public CatalogGroupView? GetGroup(string groupId) => null;

        public CatalogServiceView? GetService(string serviceId)
            => string.Equals(serviceId, _serviceView.Service.Id, StringComparison.OrdinalIgnoreCase)
                ? _serviceView
                : null;

        public CatalogDiscoveryRecord? GetRecord(string recordId) => null;

        public IReadOnlyList<CatalogDiscoveryRecord> Search(string? query, string? groupId = null, int limit = 100, int offset = 0)
            => Array.Empty<CatalogDiscoveryRecord>();

        public Task WarmupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }
}
