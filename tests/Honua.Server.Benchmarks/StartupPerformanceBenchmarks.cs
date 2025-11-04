// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.DependencyInjection;
using Honua.Server.Core.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Benchmarks for startup performance optimizations.
/// Measures the impact of connection pool warmup and lazy service loading.
/// </summary>
/// <remarks>
/// Run with: dotnet run -c Release --project tests/Honua.Server.Benchmarks
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[MarkdownExporter]
public class StartupPerformanceBenchmarks
{
    private IConfiguration _configuration = null!;

    [GlobalSetup]
    public void Setup()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionPoolWarmup:Enabled"] = "true",
                ["ConnectionPoolWarmup:StartupDelayMs"] = "0",
                ["ConnectionPoolWarmup:MaxConcurrentWarmups"] = "3"
            })
            .Build();
    }

    [Benchmark(Baseline = true)]
    public async Task<long> ServiceRegistration_WithoutLazy()
    {
        // Benchmark standard service registration
        var services = new ServiceCollection();
        services.AddLogging();

        // Register services eagerly (standard way)
        services.AddSingleton<ITestHeavyService, TestHeavyService>();
        services.AddSingleton<IAnotherHeavyService, AnotherHeavyService>();

        var sw = Stopwatch.StartNew();
        var provider = services.BuildServiceProvider();

        // Services are created during container build
        _ = provider.GetRequiredService<ITestHeavyService>();
        _ = provider.GetRequiredService<IAnotherHeavyService>();

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public async Task<long> ServiceRegistration_WithLazy()
    {
        // Benchmark lazy service registration
        var services = new ServiceCollection();
        services.AddLogging();

        // Register services lazily
        services.AddLazySingleton<ITestHeavyService, TestHeavyService>();
        services.AddLazySingleton<IAnotherHeavyService, AnotherHeavyService>();

        var sw = Stopwatch.StartNew();
        var provider = services.BuildServiceProvider();

        // Services are NOT created until accessed
        _ = provider.GetRequiredService<ITestHeavyService>();
        _ = provider.GetRequiredService<IAnotherHeavyService>();

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public async Task<long> LazyWrapper_AccessTime()
    {
        // Benchmark Lazy<T> wrapper overhead
        var services = new ServiceCollection();
        services.AddSingleton<ITestHeavyService, TestHeavyService>();
        services.AddLazyWrapper<ITestHeavyService>();

        var provider = services.BuildServiceProvider();

        var sw = Stopwatch.StartNew();
        var lazy = provider.GetRequiredService<Lazy<ITestHeavyService>>();
        var service = lazy.Value;
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public async Task<long> LazyService_AccessTime()
    {
        // Benchmark LazyService<T> wrapper overhead
        var services = new ServiceCollection();
        services.AddSingleton<ITestHeavyService, TestHeavyService>();
        services.AddSingleton(typeof(LazyService<>), typeof(LazyService<>));

        var provider = services.BuildServiceProvider();

        var sw = Stopwatch.StartNew();
        var lazyService = provider.GetRequiredService<LazyService<ITestHeavyService>>();
        var service = lazyService.Value;
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public void StartupProfiler_CheckpointOverhead()
    {
        // Benchmark startup profiler overhead
        var profiler = new StartupProfiler();

        // Simulate typical startup checkpoint count
        for (int i = 0; i < 20; i++)
        {
            profiler.Checkpoint($"Checkpoint {i}");
        }
    }

    [Benchmark]
    public void StartupProfiler_LogResults()
    {
        // Benchmark profiler result logging
        var profiler = new StartupProfiler();

        for (int i = 0; i < 10; i++)
        {
            profiler.Checkpoint($"Step {i}");
        }

        var logger = new LoggerFactory().CreateLogger("Test");
        profiler.LogResults(logger);
    }

    [Benchmark]
    public async Task<long> ConnectionPoolWarmup_Overhead()
    {
        // Benchmark warmup service overhead
        // Note: This uses mocks since we don't have real DB connections in benchmarks

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_configuration);
        services.AddSingleton<IHostEnvironment>(sp => new MockHostEnvironment());

        // Note: Would need to mock IMetadataRegistry and IDataStoreProviderFactory
        // for a real benchmark. This is a simplified version.

        var sw = Stopwatch.StartNew();
        var provider = services.BuildServiceProvider();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    // Test services that simulate heavy initialization
    public interface ITestHeavyService
    {
        string GetData();
    }

    public class TestHeavyService : ITestHeavyService
    {
        public TestHeavyService()
        {
            // Simulate expensive initialization
            System.Threading.Thread.Sleep(10);
        }

        public string GetData() => "data";
    }

    public interface IAnotherHeavyService
    {
        void DoWork();
    }

    public class AnotherHeavyService : IAnotherHeavyService
    {
        public AnotherHeavyService()
        {
            // Simulate expensive initialization
            System.Threading.Thread.Sleep(10);
        }

        public void DoWork() { }
    }

    private class MockHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "TestApp";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

/// <summary>
/// Benchmarks for cold start scenarios specific to serverless deployments.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ColdStartBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<long> ColdStart_Baseline()
    {
        // Simulate baseline cold start without optimizations
        var sw = Stopwatch.StartNew();

        var services = new ServiceCollection();
        services.AddLogging();

        // Simulate typical service registrations
        for (int i = 0; i < 10; i++)
        {
            services.AddSingleton<ITestHeavyService, TestHeavyService>();
        }

        var provider = services.BuildServiceProvider();

        // Trigger service creation
        foreach (var _ in Enumerable.Range(0, 10))
        {
            _ = provider.GetRequiredService<ITestHeavyService>();
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public async Task<long> ColdStart_WithOptimizations()
    {
        // Simulate cold start with lazy loading
        var sw = Stopwatch.StartNew();

        var services = new ServiceCollection();
        services.AddLogging();

        // Use lazy registration
        for (int i = 0; i < 10; i++)
        {
            services.AddLazySingleton<ITestHeavyService, TestHeavyService>();
        }

        var provider = services.BuildServiceProvider();

        // Services are created on demand
        foreach (var _ in Enumerable.Range(0, 10))
        {
            _ = provider.GetRequiredService<ITestHeavyService>();
        }

        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public async Task<long> FirstRequest_Latency_NoWarmup()
    {
        // Simulate first request latency without warmup
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITestHeavyService, TestHeavyService>();

        var provider = services.BuildServiceProvider();

        // Measure time to first service access (simulating first request)
        var sw = Stopwatch.StartNew();
        _ = provider.GetRequiredService<ITestHeavyService>();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    [Benchmark]
    public async Task<long> FirstRequest_Latency_WithWarmup()
    {
        // Simulate first request with pre-warmed services
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITestHeavyService, TestHeavyService>();

        var provider = services.BuildServiceProvider();

        // Pre-warm the service
        _ = provider.GetRequiredService<ITestHeavyService>();

        // Measure subsequent access (simulating warmed request)
        var sw = Stopwatch.StartNew();
        _ = provider.GetRequiredService<ITestHeavyService>();
        sw.Stop();

        return sw.ElapsedMilliseconds;
    }

    public interface ITestHeavyService { }
    public class TestHeavyService : ITestHeavyService
    {
        public TestHeavyService()
        {
            System.Threading.Thread.Sleep(5);
        }
    }
}

using Microsoft.Extensions.FileProviders;

/// <summary>
/// Benchmarks comparing memory usage with and without lazy loading.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class MemoryBenchmarks
{
    [Benchmark(Baseline = true)]
    public long MemoryUsage_EagerLoading()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Register 100 services eagerly
        for (int i = 0; i < 100; i++)
        {
            services.AddSingleton<ITestService>(sp => new TestService());
        }

        var provider = services.BuildServiceProvider();

        // Force creation of all services
        var initialMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < 100; i++)
        {
            _ = provider.GetServices<ITestService>();
        }

        var finalMemory = GC.GetTotalMemory(false);
        return finalMemory - initialMemory;
    }

    [Benchmark]
    public long MemoryUsage_LazyLoading()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Register 100 services lazily
        for (int i = 0; i < 100; i++)
        {
            services.AddLazySingleton<ITestService, TestService>();
        }

        var provider = services.BuildServiceProvider();

        var initialMemory = GC.GetTotalMemory(true);

        // Only access 10% of services (simulating partial usage)
        for (int i = 0; i < 10; i++)
        {
            _ = provider.GetRequiredService<ITestService>();
        }

        var finalMemory = GC.GetTotalMemory(false);
        return finalMemory - initialMemory;
    }

    public interface ITestService { }
    public class TestService : ITestService
    {
        private readonly byte[] _data = new byte[1024]; // 1KB per service
    }
}
