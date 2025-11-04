using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Metadata.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Apis.Metadata.Providers;

/// <summary>
/// Unit tests for metadata provider system.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Category", "Metadata")]
public class MetadataProviderTests
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<RedisMetadataProvider> _redisLogger;
    private readonly ILogger<SqlServerMetadataProvider> _sqlLogger;
    private readonly ILogger<MetadataProviderMigration> _migrationLogger;

    public MetadataProviderTests(ITestOutputHelper output)
    {
        _output = output;
        _redisLogger = new TestLogger<RedisMetadataProvider>(output);
        _sqlLogger = new TestLogger<SqlServerMetadataProvider>(output);
        _migrationLogger = new TestLogger<MetadataProviderMigration>(output);
    }

    #region RedisMetadataProvider Tests

    [Fact]
    public void RedisMetadataProvider_Constructor_ShouldThrowOnNullRedis()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisMetadataProvider(null!, new RedisMetadataOptions(), _redisLogger));
    }

    [Fact]
    public void RedisMetadataProvider_Constructor_ShouldThrowOnNullOptions()
    {
        // Arrange
        var redis = new Mock<IConnectionMultiplexer>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new RedisMetadataProvider(redis.Object, null!, _redisLogger));
    }

    [Fact]
    public void RedisMetadataProvider_ShouldSupportChangeNotifications()
    {
        // Arrange
        var redis = CreateMockRedis(true, out var database, out var transaction, out var subscriber);
        var provider = new RedisMetadataProvider(redis.Object, new RedisMetadataOptions(), _redisLogger);

        // Act & Assert
        provider.SupportsChangeNotifications.Should().BeTrue();
        provider.SupportsVersioning.Should().BeTrue();
    }

    [Fact]
    public async Task RedisMetadataProvider_LoadAsync_ShouldThrowWhenNoActiveSnapshot()
    {
        // Arrange
        var redis = CreateMockRedis(hasActiveSnapshot: false);
        var provider = new RedisMetadataProvider(redis.Object, new RedisMetadataOptions(), _redisLogger);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.LoadAsync());
        ex.Message.Should().Contain("No active metadata snapshot found");
    }

    [Fact]
    public async Task RedisMetadataProvider_SaveAsync_ShouldSerializeAndPublish()
    {
        // Arrange
        var redis = CreateMockRedis(true, out _, out var transaction, out _);
        var provider = new RedisMetadataProvider(redis.Object, new RedisMetadataOptions(), _redisLogger);
        var snapshot = CreateTestSnapshot();

        // Act
        await provider.SaveAsync(snapshot);

        // Assert
        transaction.Verify(t => t.ExecuteAsync(It.IsAny<CommandFlags>()), Times.Once);

        _output.WriteLine("Redis SaveAsync verified - snapshot serialized and stored");
    }

    #endregion

    #region SqlServerMetadataProvider Tests

    [Fact]
    public void SqlServerMetadataProvider_Constructor_ShouldThrowOnNullConnectionString()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new SqlServerMetadataProvider(null!, new SqlServerMetadataOptions(), _sqlLogger));
    }

    [Fact]
    public void SqlServerMetadataProvider_ShouldSupportChangeNotifications()
    {
        // Arrange
        var provider = new SqlServerMetadataProvider(
            "Server=localhost;Database=Test;",
            new SqlServerMetadataOptions { EnablePolling = true },
            _sqlLogger);

        // Act & Assert
        provider.SupportsChangeNotifications.Should().BeTrue();
        provider.SupportsVersioning.Should().BeTrue();
    }

    [Fact]
    public void SqlServerMetadataProvider_WithPollingDisabled_ShouldNotSupportChangeNotifications()
    {
        // Arrange
        var provider = new SqlServerMetadataProvider(
            "Server=localhost;Database=Test;",
            new SqlServerMetadataOptions { EnablePolling = false },
            _sqlLogger);

        // Act & Assert
        provider.SupportsChangeNotifications.Should().BeFalse();
    }

    #endregion

    #region MetadataProviderConfiguration Tests

    [Fact]
    public void MetadataProviderConfiguration_ShouldDefaultToFileProvider()
    {
        // Arrange & Act
        var config = new MetadataProviderConfiguration();

        // Assert
        config.Provider.Should().Be(MetadataProviderType.File);
    }

    [Fact]
    public void MetadataProviderConfiguration_ShouldRegisterFileProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new MetadataProviderConfiguration
        {
            Provider = MetadataProviderType.File,
            FilePath = "./test-metadata.json"
        };

        // Act
        services.AddMetadataProvider(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var metadataProvider = provider.GetService<IMetadataProvider>();
        metadataProvider.Should().NotBeNull();
        metadataProvider.Should().BeOfType<JsonMetadataProvider>();
    }

    [Fact]
    public void MetadataProviderConfiguration_ShouldThrowWhenFilePathMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new MetadataProviderConfiguration
        {
            Provider = MetadataProviderType.File,
            FilePath = null
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            services.AddMetadataProvider(configuration));
    }

    [Fact]
    public void MetadataProviderConfiguration_ShouldLoadFromIConfiguration()
    {
        // Arrange
        var configData = new System.Collections.Generic.Dictionary<string, string>
        {
            ["MetadataProvider:Provider"] = "File",
            ["MetadataProvider:FilePath"] = "./metadata.json",
            ["MetadataProvider:WatchForFileChanges"] = "false"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData!)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddMetadataProvider(configuration);

        // Assert - should not throw
        var provider = services.BuildServiceProvider();
        provider.GetService<IMetadataProvider>().Should().NotBeNull();
    }

    #endregion

    #region MetadataProviderMigration Tests

    [Fact]
    public async Task MetadataProviderMigration_ShouldThrowOnNullSource()
    {
        // Arrange
        var migration = new MetadataProviderMigration(_migrationLogger);
        var destination = new Mock<IMutableMetadataProvider>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            migration.MigrateAsync(null!, destination.Object));
    }

    [Fact]
    public async Task MetadataProviderMigration_ShouldThrowOnNullDestination()
    {
        // Arrange
        var migration = new MetadataProviderMigration(_migrationLogger);
        var source = new Mock<IMetadataProvider>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            migration.MigrateAsync(source.Object, null!));
    }

    [Fact]
    public async Task MetadataProviderMigration_ShouldThrowOnNonMutableDestination()
    {
        // Arrange
        var migration = new MetadataProviderMigration(_migrationLogger);
        var source = new Mock<IMetadataProvider>();
        var destination = new Mock<IMetadataProvider>(); // Not IMutableMetadataProvider

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            migration.MigrateAsync(source.Object, destination.Object));
        ex.Message.Should().Contain("IMutableMetadataProvider");
    }

    [Fact]
    public async Task MetadataProviderMigration_ShouldMigrateSuccessfully()
    {
        // Arrange
        var migration = new MetadataProviderMigration(_migrationLogger);
        var snapshot = CreateTestSnapshot();

        var source = new Mock<IMetadataProvider>();
        source.Setup(s => s.LoadAsync(default)).ReturnsAsync(snapshot);

        var destination = new Mock<IMutableMetadataProvider>();
        destination.Setup(d => d.SaveAsync(It.IsAny<MetadataSnapshot>(), default))
            .Returns(Task.CompletedTask);

        // Act
        await migration.MigrateAsync(source.Object, destination.Object);

        // Assert
        source.Verify(s => s.LoadAsync(default), Times.Once);
        destination.Verify(d => d.SaveAsync(snapshot, default), Times.Once);
    }

    [Fact]
    public async Task MetadataProviderMigration_ExportToFile_ShouldCreateJsonFile()
    {
        // Arrange
        var migration = new MetadataProviderMigration(_migrationLogger);
        var snapshot = CreateTestSnapshot();
        var tempPath = Path.Combine(Path.GetTempPath(), $"test-export-{Guid.NewGuid()}.json");

        var source = new Mock<IMetadataProvider>();
        source.Setup(s => s.LoadAsync(default)).ReturnsAsync(snapshot);

        try
        {
            // Act
            await migration.ExportToFileAsync(source.Object, tempPath);

            // Assert
            File.Exists(tempPath).Should().BeTrue();
            var json = await File.ReadAllTextAsync(tempPath);
            json.Should().Contain("test-catalog");

            _output.WriteLine($"Exported metadata to: {tempPath}");
            _output.WriteLine($"File size: {new FileInfo(tempPath).Length} bytes");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    #endregion

    #region PostgresMetadataProvider Tests

    [Fact]
    public void PostgresMetadataProvider_Constructor_ShouldThrowOnNullConnectionString()
    {
        // Arrange
        var logger = new TestLogger<PostgresMetadataProvider>(_output);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new PostgresMetadataProvider(null!, new PostgresMetadataOptions(), logger));
    }

    [Fact]
    public void PostgresMetadataProvider_ShouldSupportChangeNotifications()
    {
        // Arrange
        var provider = new PostgresMetadataProvider(
            "Host=localhost;Database=Test;Username=test;Password=test",
            new PostgresMetadataOptions { EnableNotifications = true },
            new TestLogger<PostgresMetadataProvider>(_output));

        // Act & Assert
        provider.SupportsChangeNotifications.Should().BeTrue();
        provider.SupportsVersioning.Should().BeTrue();
    }

    [Fact]
    public void PostgresMetadataProvider_WithNotificationsDisabled_ShouldNotSupportChangeNotifications()
    {
        // Arrange
        var provider = new PostgresMetadataProvider(
            "Host=localhost;Database=Test;Username=test;Password=test",
            new PostgresMetadataOptions { EnableNotifications = false },
            new TestLogger<PostgresMetadataProvider>(_output));

        // Act & Assert
        provider.SupportsChangeNotifications.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private Mock<IConnectionMultiplexer> CreateMockRedis(bool hasActiveSnapshot, out Mock<IDatabase> database, out Mock<ITransaction> transaction, out Mock<ISubscriber> subscriber)
    {
        var redis = new Mock<IConnectionMultiplexer>();
        database = new Mock<IDatabase>();
        subscriber = new Mock<ISubscriber>();
        transaction = new Mock<ITransaction>();

        // Setup database
        redis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(database.Object);
        redis.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        // Setup active snapshot retrieval
        if (hasActiveSnapshot)
        {
            var snapshot = CreateTestSnapshot();
            var json = JsonSerializer.Serialize(snapshot);
            database.Setup(d => d.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(new RedisValue(json));
        }
        else
        {
            database.Setup(d => d.StringGetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);
        }

        // Setup transaction
        database.Setup(d => d.CreateTransaction(It.IsAny<object>())).Returns(transaction.Object);
        transaction.Setup(t => t.ExecuteAsync(It.IsAny<CommandFlags>())).ReturnsAsync(true);

        // Setup subscriber
        subscriber.Setup(s => s.SubscribeAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        return redis;
    }

    private Mock<IConnectionMultiplexer> CreateMockRedis(bool hasActiveSnapshot = true)
        => CreateMockRedis(hasActiveSnapshot, out _, out _, out _);

    private MetadataSnapshot CreateTestSnapshot()
    {
        var catalog = new CatalogDefinition
        {
            Id = "test-catalog",
            Title = "Test Catalog"
        };

        var folders = new[]
        {
            new FolderDefinition { Id = "test-folder", Title = "Test Folder" }
        };

        var dataSources = new[]
        {
            new DataSourceDefinition
            {
                Id = "test-datasource",
                Provider = "Postgres",
                ConnectionString = "Host=localhost;Database=test"
            }
        };

        var services = new[]
        {
            new ServiceDefinition
            {
                Id = "test-service",
                Title = "Test Service",
                FolderId = "test-folder",
                ServiceType = "feature",
                DataSourceId = "test-datasource"
            }
        };

        return new MetadataSnapshot(
            catalog,
            folders,
            dataSources,
            services,
            Array.Empty<LayerDefinition>(),
            Array.Empty<RasterDatasetDefinition>(),
            Array.Empty<StyleDefinition>(),
            ServerDefinition.Default);
    }

    #endregion
}

/// <summary>
/// Simple test logger that writes to xUnit output.
/// </summary>
internal class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        if (exception != null)
        {
            _output.WriteLine(exception.ToString());
        }
    }
}
