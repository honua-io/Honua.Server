using System.Diagnostics;
using Honua.Server.AlertReceiver.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using Xunit;
using Dapper;

namespace Honua.Server.AlertReceiver.Tests.Services;

/// <summary>
/// Performance tests for SqlAlertDeduplicator to measure lock contention impact.
/// These tests verify that the race condition fixes don't significantly degrade performance.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqlAlertDeduplicatorPerformanceTests : IDisposable
{
    private readonly string _testConnectionString;
    private readonly string _testDatabase;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SqlAlertDeduplicator> _logger;
    private readonly IAlertMetricsService _metrics;
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<AlertDeduplicationCacheOptions> _cacheOptions;
    private bool _disposed;

    public SqlAlertDeduplicatorPerformanceTests()
    {
        // Use a unique test database for isolation
        _testDatabase = $"honua_perf_test_{Guid.NewGuid():N}";
        var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";

        var masterConnectionString = $"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database=postgres";
        _testConnectionString = $"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database={_testDatabase}";

        // Create test database
        using (var connection = new NpgsqlConnection(masterConnectionString))
        {
            connection.Open();
            connection.Execute($"CREATE DATABASE {_testDatabase}");
        }

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Alerts:Deduplication:CriticalWindowMinutes"] = "5",
                ["Alerts:Deduplication:HighWindowMinutes"] = "10",
                ["Alerts:Deduplication:WarningWindowMinutes"] = "15",
                ["Alerts:Deduplication:DefaultWindowMinutes"] = "30",
                ["Alerts:RateLimit:CriticalPerHour"] = "20",
                ["Alerts:RateLimit:HighPerHour"] = "10",
                ["Alerts:RateLimit:WarningPerHour"] = "5",
                ["Alerts:RateLimit:DefaultPerHour"] = "3"
            })
            .Build();

        _logger = new Mock<ILogger<SqlAlertDeduplicator>>().Object;
        _metrics = new Mock<IAlertMetricsService>().Object;
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _cacheOptions = Options.Create(new AlertDeduplicationCacheOptions());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Clean up test database
        try
        {
            var postgresHost = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
            var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
            var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
            var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
            var masterConnectionString = $"Host={postgresHost};Port={postgresPort};Username={postgresUser};Password={postgresPassword};Database=postgres";

            using var connection = new NpgsqlConnection(masterConnectionString);
            connection.Open();
            connection.Execute($@"
                SELECT pg_terminate_backend(pg_stat_activity.pid)
                FROM pg_stat_activity
                WHERE pg_stat_activity.datname = '{_testDatabase}'
                AND pid <> pg_backend_pid()");
            connection.Execute($"DROP DATABASE IF EXISTS {_testDatabase}");
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private IAlertReceiverDbConnectionFactory CreateConnectionFactory()
    {
        return new NpgsqlAlertReceiverDbConnectionFactory(_testConnectionString);
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task Performance_SingleFingerprint_HighContention()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var deduplicator = new SqlAlertDeduplicator(connectionFactory, _configuration, _logger, _metrics, _memoryCache, _cacheOptions);

        var fingerprint = "perf-test-single";
        var concurrentRequests = 100;
        var sw = Stopwatch.StartNew();

        // Act - 100 concurrent requests for same fingerprint
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                var allowed = deduplicator.ShouldSendAlert(fingerprint, "critical", out var reservationId);
                if (allowed)
                {
                    deduplicator.RecordAlert(fingerprint, "critical", reservationId);
                }
                await Task.CompletedTask;
            })
            .ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert - Should complete in reasonable time despite contention
        var avgLatencyMs = sw.ElapsedMilliseconds / (double)concurrentRequests;
        Assert.True(avgLatencyMs < 100, $"Average latency {avgLatencyMs:F2}ms exceeds 100ms threshold");
        Assert.True(sw.ElapsedMilliseconds < 10000, $"Total time {sw.ElapsedMilliseconds}ms exceeds 10s threshold");
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task Performance_MultipleFingerprints_LowContention()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var deduplicator = new SqlAlertDeduplicator(connectionFactory, _configuration, _logger, _metrics, _memoryCache, _cacheOptions);

        var concurrentRequests = 100;
        var sw = Stopwatch.StartNew();

        // Act - 100 concurrent requests with unique fingerprints (no contention)
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(async i =>
            {
                var fingerprint = $"perf-test-unique-{i}";
                var allowed = deduplicator.ShouldSendAlert(fingerprint, "critical", out var reservationId);
                if (allowed)
                {
                    deduplicator.RecordAlert(fingerprint, "critical", reservationId);
                }
                await Task.CompletedTask;
            })
            .ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert - Should be faster with no contention
        var avgLatencyMs = sw.ElapsedMilliseconds / (double)concurrentRequests;
        Assert.True(avgLatencyMs < 50, $"Average latency {avgLatencyMs:F2}ms exceeds 50ms threshold");
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Total time {sw.ElapsedMilliseconds}ms exceeds 5s threshold");
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task Performance_MixedWorkload_RealisticScenario()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var deduplicator = new SqlAlertDeduplicator(connectionFactory, _configuration, _logger, _metrics, _memoryCache, _cacheOptions);

        var totalRequests = 1000;
        var uniqueFingerprints = 20; // 50 requests per fingerprint on average
        var sw = Stopwatch.StartNew();

        // Act - Mixed workload: some contention, mostly unique
        var tasks = Enumerable.Range(0, totalRequests)
            .Select(async i =>
            {
                var fingerprintIndex = Random.Shared.Next(0, uniqueFingerprints);
                var fingerprint = $"perf-test-mixed-{fingerprintIndex}";

                var allowed = deduplicator.ShouldSendAlert(fingerprint, "critical", out var reservationId);
                if (allowed)
                {
                    await Task.Delay(Random.Shared.Next(1, 5)); // Simulate publish delay
                    deduplicator.RecordAlert(fingerprint, "critical", reservationId);
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var avgLatencyMs = sw.ElapsedMilliseconds / (double)totalRequests;
        Assert.True(avgLatencyMs < 100, $"Average latency {avgLatencyMs:F2}ms exceeds 100ms threshold");
        Assert.True(sw.ElapsedMilliseconds < 30000, $"Total time {sw.ElapsedMilliseconds}ms exceeds 30s threshold");
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task Performance_LockAcquisition_SubMillisecond()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var deduplicator = new SqlAlertDeduplicator(connectionFactory, _configuration, _logger, _metrics, _memoryCache, _cacheOptions);

        var iterations = 100;
        var latencies = new List<double>();

        // Act - Measure lock acquisition time for unique fingerprints (no contention)
        for (int i = 0; i < iterations; i++)
        {
            var fingerprint = $"perf-test-lock-{i}";
            var sw = Stopwatch.StartNew();

            deduplicator.ShouldSendAlert(fingerprint, "critical", out var reservationId);

            sw.Stop();
            latencies.Add(sw.Elapsed.TotalMilliseconds);

            deduplicator.RecordAlert(fingerprint, "critical", reservationId);
        }

        var avgLatency = latencies.Average();
        var p95Latency = latencies.OrderBy(x => x).ElementAt((int)(iterations * 0.95));
        var p99Latency = latencies.OrderBy(x => x).ElementAt((int)(iterations * 0.99));

        // Assert - Lock acquisition should be very fast
        Assert.True(avgLatency < 10, $"Average lock acquisition {avgLatency:F2}ms exceeds 10ms");
        Assert.True(p95Latency < 20, $"P95 lock acquisition {p95Latency:F2}ms exceeds 20ms");
        Assert.True(p99Latency < 50, $"P99 lock acquisition {p99Latency:F2}ms exceeds 50ms");

        await Task.CompletedTask;
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task Performance_MemoryUsage_ReservationCleanup()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var deduplicator = new SqlAlertDeduplicator(connectionFactory, _configuration, _logger, _metrics, _memoryCache, _cacheOptions);

        var iterations = 1000;

        // Act - Create many reservations
        for (int i = 0; i < iterations; i++)
        {
            var fingerprint = $"perf-test-memory-{i}";
            var allowed = deduplicator.ShouldSendAlert(fingerprint, "critical", out var reservationId);
            if (allowed)
            {
                deduplicator.RecordAlert(fingerprint, "critical", reservationId);
            }
        }

        // Force GC to see current memory usage
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(false);

        // Wait for reservation cleanup
        await Task.Delay(1000);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryAfter = GC.GetTotalMemory(false);

        // Assert - Memory should not grow unbounded
        var memoryGrowthMB = (memoryAfter - memoryBefore) / 1024.0 / 1024.0;
        Assert.True(Math.Abs(memoryGrowthMB) < 10, $"Memory growth {memoryGrowthMB:F2}MB indicates possible leak");
    }

    [Fact(Skip = "Performance test - run manually")]
    public async Task Performance_Throughput_AlertsPerSecond()
    {
        // Arrange
        var connectionFactory = CreateConnectionFactory();
        var deduplicator = new SqlAlertDeduplicator(connectionFactory, _configuration, _logger, _metrics, _memoryCache, _cacheOptions);

        var durationSeconds = 10;
        var completedCount = 0;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));

        // Act - Sustained throughput test
        var workers = Enumerable.Range(0, 10)
            .Select(async workerId =>
            {
                var localCount = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var fingerprint = $"perf-test-throughput-{workerId}-{localCount}";
                        var allowed = deduplicator.ShouldSendAlert(fingerprint, "critical", out var reservationId);
                        if (allowed)
                        {
                            deduplicator.RecordAlert(fingerprint, "critical", reservationId);
                        }
                        localCount++;
                        Interlocked.Increment(ref completedCount);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            })
            .ToArray();

        await Task.WhenAll(workers);

        // Assert
        var throughput = completedCount / (double)durationSeconds;
        Assert.True(throughput > 50, $"Throughput {throughput:F2} alerts/sec is below 50 alerts/sec minimum");
    }
}
