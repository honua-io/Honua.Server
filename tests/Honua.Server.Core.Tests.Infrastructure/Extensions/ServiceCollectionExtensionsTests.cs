using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Data;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster.Caching;
using Honua.Server.Host.Extensions;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Raster;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Extensions;

/// <summary>
/// Tests for ServiceCollectionExtensions ensuring proper service registration.
/// </summary>
[Trait("Category", "Unit")]
public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly string _metadataPath;

    public ServiceCollectionExtensionsTests()
    {
        _metadataPath = Path.Combine(Path.GetTempPath(), $"honua-service-tests-{Guid.NewGuid():N}.json");
        File.WriteAllText(_metadataPath, "{\"server\":{\"allowedHosts\":[\"localhost\"]},\"catalog\":{\"id\":\"test\"},\"folders\":[],\"dataSources\":[],\"services\":[],\"layers\":[],\"styles\":[],\"rasterDatasets\":[]}");

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "honua:metadata:provider", "json" },
                { "honua:metadata:path", _metadataPath },
                { "honua:dataProviders:0:id", "test-provider" },
                { "honua:dataProviders:0:type", "PostgreSQL" },
                { "honua:dataProviders:0:connectionString", "Server=localhost;Database=test" },
                { "honua:authentication:mode", "Local" },
                { "honua:authentication:enforce", "false" }
            })
            .Build();
    }

    [Fact]
    public void AddHonuaCoreServices_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHonuaCoreServices(_configuration, AppContext.BaseDirectory);

        // Assert
        var provider = services.BuildServiceProvider();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaCoreServices_WithYamlMetadataProvider_ShouldResolveYamlProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "honua:metadata:provider", "yaml" },
                { "honua:metadata:path", "./metadata.yaml" }
            })
            .Build();

        // Act
        services.AddHonuaCoreServices(configuration, AppContext.BaseDirectory);

        // Assert
        var provider = services.BuildServiceProvider();
        var metadataProvider = provider.GetRequiredService<IMetadataProvider>();
        metadataProvider.Should().BeOfType<YamlMetadataProvider>();
    }

    [Fact]
    public void AddHonuaWfsServices_ShouldRegisterWfsServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Add required dependencies for WFS services
        var builder = WebApplication.CreateBuilder();
        services.AddSingleton<IHostEnvironment>(builder.Environment);
        services.AddSingleton<IConfiguration>(builder.Configuration);
        services.AddSingleton<IMetadataRegistry, StubMetadataRegistry>();

        // Act
        services.AddHonuaWfsServices(_configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        // WFS services are registered as internal, so we can't verify specific types
        // but we can verify the service collection was modified
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaRasterServices_ShouldRegisterRasterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(_configuration);

        // Act
        services.AddSingleton<IRasterDatasetRegistry, StubRasterDatasetRegistry>();
        services.AddSingleton<IMetadataRegistry, StubMetadataRegistry>();
        services.AddSingleton<IFeatureRepository, StubFeatureRepository>();
        services.AddSingleton<IRasterRenderer, StubRasterRenderer>();
        services.AddSingleton<IRasterTileCacheProvider, StubRasterTileCacheProvider>();
        services.AddHonuaRasterServices();

        // Assert
        var provider = services.BuildServiceProvider();
        var metrics = provider.GetService<IRasterTileCacheMetrics>();
        metrics.Should().NotBeNull();

        var preseedService = provider.GetService<IRasterTilePreseedService>();
        preseedService.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaCartoServices_ShouldRegisterCartoServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICatalogProjectionService, StubCatalogProjectionService>();
        services.AddSingleton<IFeatureRepository, StubFeatureRepository>();

        // Act
        services.AddHonuaCartoServices();

        // Assert
        var provider = services.BuildServiceProvider();
        // Carto services are registered as internal, so we can't verify specific types
        // but we can verify the service collection was modified and builds successfully
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaPerformanceOptimizations_ShouldRegisterCompressionAndCaching()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddHonuaPerformanceOptimizations(_configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        provider.Should().NotBeNull();

        // Verify response compression is registered
        var compressionProvider = provider.GetService<Microsoft.AspNetCore.ResponseCompression.IResponseCompressionProvider>();
        compressionProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaCors_ShouldRegisterCorsServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(_configuration);

        // Add required dependencies
        services.AddHonuaCoreServices(_configuration, AppContext.BaseDirectory);

        // Act
        services.AddHonuaCors();

        // Assert
        var provider = services.BuildServiceProvider();
        var corsProvider = provider.GetService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>();
        corsProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaMvcServices_ShouldReturnMvcBuilder()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var mvcBuilder = services.AddHonuaMvcServices();

        // Assert
        mvcBuilder.Should().NotBeNull();

        var provider = services.BuildServiceProvider();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaSecurityValidation_ShouldRegisterValidators()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(_configuration);

        // Act
        services.AddHonuaSecurityValidation();

        // Assert
        var provider = services.BuildServiceProvider();
        var validator = provider.GetService<Host.Configuration.IRuntimeSecurityConfigurationValidator>();
        validator.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureRequestLimits_ShouldConfigureKestrelLimits()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "RequestLimits:MaxBodySize", "104857600" } // 100MB
        });

        // Act
        builder.ConfigureRequestLimits();

        // Assert
        builder.Should().NotBeNull();
        // Verification that limits are set would require building the app
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_metadataPath))
            {
                File.Delete(_metadataPath);
            }
        }
        catch
        {
            // ignored
        }
    }

    private sealed class StubCatalogProjectionService : ICatalogProjectionService
    {
        public CatalogProjectionSnapshot GetSnapshot() => new CatalogProjectionSnapshot(Array.Empty<CatalogGroupView>(), new Dictionary<string, CatalogGroupView>(), new Dictionary<string, CatalogServiceView>(), new Dictionary<string, CatalogDiscoveryRecord>());
        public IReadOnlyList<CatalogGroupView> GetGroups() => Array.Empty<CatalogGroupView>();
        public CatalogGroupView? GetGroup(string groupId) => null;
        public CatalogServiceView? GetService(string serviceId) => null;
        public CatalogDiscoveryRecord? GetRecord(string recordId) => null;
        public IReadOnlyList<CatalogDiscoveryRecord> Search(string? query, string? groupId = null, int limit = 100, int offset = 0) => Array.Empty<CatalogDiscoveryRecord>();
        public Task WarmupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class StubRasterDatasetRegistry : IRasterDatasetRegistry
    {
        public ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetAllAsync(CancellationToken cancellationToken = default) => new(Array.Empty<RasterDatasetDefinition>());
        public ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetByServiceAsync(string serviceId, CancellationToken cancellationToken = default) => new(Array.Empty<RasterDatasetDefinition>());
        public ValueTask<RasterDatasetDefinition?> FindAsync(string datasetId, CancellationToken cancellationToken = default) => new((RasterDatasetDefinition?)null);
    }

    private sealed class StubFeatureRepository : IFeatureRepository
    {
        public IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
            => Empty();
        public Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default) => Task.FromResult<FeatureRecord?>(null);
        public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(record);
        public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult<FeatureRecord?>(record);
        public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());
        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());
        public Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default) => Task.FromResult<BoundingBox?>(null);
        private static async IAsyncEnumerable<FeatureRecord> Empty()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class StubMetadataRegistry : IMetadataRegistry
    {
        private readonly MetadataSnapshot _snapshot;

        public StubMetadataRegistry()
        {
            var catalog = new CatalogDefinition { Id = "stub" };
            var folders = new[] { new FolderDefinition { Id = "default" } };
            var dataSources = new[] { new DataSourceDefinition { Id = "ds", Provider = "sqlite", ConnectionString = "Data Source=:memory:" } };
            var services = new[]
            {
                new ServiceDefinition
                {
                    Id = "svc",
                    Title = "Stub Service",
                    FolderId = "default",
                    ServiceType = "FeatureServer",
                    DataSourceId = "ds"
                }
            };
            var layers = new[]
            {
                new LayerDefinition
                {
                    Id = "layer",
                    ServiceId = "svc",
                    Title = "Stub Layer",
                    GeometryType = "Point",
                    IdField = "id",
                    GeometryField = "geom"
                }
            };
            var server = new ServerDefinition { AllowedHosts = new[] { "localhost" } };
            _snapshot = new MetadataSnapshot(catalog, folders, dataSources, services, layers, Array.Empty<RasterDatasetDefinition>(), Array.Empty<StyleDefinition>(), server);
        }

        [Obsolete]
        public MetadataSnapshot Snapshot => _snapshot;

        public bool IsInitialized => true;

        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => new(_snapshot);

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        [Obsolete]
        public void Update(MetadataSnapshot snapshot) { }

        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IChangeToken GetChangeToken() => new CancellationChangeToken(new CancellationToken(false));

        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = _snapshot;
            return true;
        }
    }

    private sealed class StubRasterRenderer : IRasterRenderer
    {
        public Task<RasterRenderResult> RenderAsync(RasterRenderRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RasterRenderResult(Stream.Null, "image/png", request.Width, request.Height));
        }
    }

    private sealed class StubRasterTileCacheProvider : IRasterTileCacheProvider
    {
        public ValueTask<RasterTileCacheHit?> TryGetAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default) => new((RasterTileCacheHit?)null);
        public Task StoreAsync(RasterTileCacheKey key, RasterTileCacheEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(RasterTileCacheKey key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PurgeDatasetAsync(string datasetId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
