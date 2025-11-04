using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
[Trait("Feature", "Data")]
[Trait("Speed", "Fast")]
public class FeatureRepositoryTests
{
    [Fact]
    public async Task CountAsync_ShouldApplyLayerAutoFilter()
    {
        var provider = new CapturingProvider();
        var context = CreateContext(provider);
        var repository = new FeatureRepository(new StubResolver(context));

        await repository.CountAsync("svc", "layer", null);

        provider.LastQuery.Should().NotBeNull();
        provider.LastQuery!.Filter.Should().BeSameAs(context.Layer.Query.AutoFilter!.Expression);
        provider.LastQuery.EntityDefinition.Should().NotBeNull();
    }

    [Fact]
    public async Task CountAsync_ShouldCombineUserFilterWithAutoFilter()
    {
        var provider = new CapturingProvider();
        var context = CreateContext(provider);
        var repository = new FeatureRepository(new StubResolver(context));

        var userFilter = new QueryFilter(
            new QueryBinaryExpression(
                new QueryFieldReference("id"),
                QueryBinaryOperator.Equal,
                new QueryConstant(5)));

        await repository.CountAsync("svc", "layer", new FeatureQuery(Filter: userFilter));

        provider.LastQuery.Should().NotBeNull();
        provider.LastQuery!.Filter.Should().NotBeNull();
        provider.LastQuery.Filter!.Expression.Should().BeOfType<QueryBinaryExpression>();

        var combined = (QueryBinaryExpression)provider.LastQuery.Filter.Expression!;
        combined.Operator.Should().Be(QueryBinaryOperator.And);
        combined.Left.Should().BeSameAs(context.Layer.Query.AutoFilter!.Expression!.Expression);
        combined.Right.Should().BeSameAs(userFilter.Expression);
        provider.LastQuery.EntityDefinition.Should().NotBeNull();
    }

    private static FeatureContext CreateContext(IDataStoreProvider provider)
    {
        var layer = CreateLayer();
        var service = new ServiceDefinition
        {
            Id = "svc",
            Title = "Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "ds",
            Layers = new List<LayerDefinition> { layer }
        };

        var dataSource = new DataSourceDefinition
        {
            Id = "ds",
            Provider = provider.Provider,
            ConnectionString = "test"
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog" },
            new[] { new FolderDefinition { Id = "root", Title = "Root" } },
            new[] { dataSource },
            new[] { service },
            new[] { layer },
            styles: Array.Empty<StyleDefinition>());

        return new FeatureContext(snapshot, service, layer, dataSource, provider);
    }

    private static LayerDefinition CreateLayer()
    {
        var baseLayer = new LayerDefinition
        {
            Id = "layer",
            ServiceId = "svc",
            Title = "Layer",
            GeometryType = "point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int" },
                new() { Name = "name", DataType = "string" }
            },
            Query = new LayerQueryDefinition
            {
                AutoFilter = new LayerQueryFilterDefinition { Cql = "name = 'Main'" }
            }
        };

        var parsed = CqlFilterParser.Parse(baseLayer.Query.AutoFilter!.Cql!, baseLayer);
        var query = baseLayer.Query with
        {
            AutoFilter = new LayerQueryFilterDefinition
            {
                Cql = baseLayer.Query.AutoFilter!.Cql,
                Expression = parsed
            }
        };

        return baseLayer with
        {
            Query = query,
            Catalog = new CatalogEntryDefinition(),
            Links = Array.Empty<LinkDefinition>(),
            Keywords = Array.Empty<string>(),
            Crs = new List<string> { "EPSG:4326" }
        };
    }

    private sealed class StubResolver : IFeatureContextResolver
    {
        private readonly FeatureContext _context;

        public StubResolver(FeatureContext context)
        {
            _context = context;
        }

        public Task<FeatureContext> ResolveAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_context);
    }

    private sealed class CapturingProvider : IDataStoreProvider
    {
        public FeatureQuery? LastQuery { get; private set; }

        public string Provider => "test";

        public IDataStoreCapabilities Capabilities => TestDataStoreCapabilities.Instance;

        public IAsyncEnumerable<FeatureRecord> QueryAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery? query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return EmptyAsync(cancellationToken);
        }

        public Task<long> CountAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery? query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(0L);
        }

        public Task<FeatureRecord?> GetAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            FeatureQuery? query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult<FeatureRecord?>(null);
        }

        public Task<FeatureRecord> CreateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureRecord record,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task<FeatureRecord?> UpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            FeatureRecord record,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<FeatureRecord?>(record);

        public Task<bool> DeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> SoftDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            string? deletedBy,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> RestoreAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> HardDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            string? deletedBy,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<int> BulkInsertAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<FeatureRecord> records,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> BulkUpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> BulkDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<string> featureIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<byte[]?> GenerateMvtTileAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            int zoom,
            int x,
            int y,
            string? datetime = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IReadOnlyList<StatisticDefinition> statistics,
            IReadOnlyList<string>? groupByFields,
            FeatureQuery? filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());

        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IReadOnlyList<string> fieldNames,
            FeatureQuery? filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());

        public Task<BoundingBox?> QueryExtentAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery? filter,
            CancellationToken cancellationToken = default)
            => Task.FromResult<BoundingBox?>(null);

        public Task<IDataStoreTransaction?> BeginTransactionAsync(
            DataSourceDefinition dataSource,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IDataStoreTransaction?>(null);

        public Task TestConnectivityAsync(
            DataSourceDefinition dataSource,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private static async IAsyncEnumerable<FeatureRecord> EmptyAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
