using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Testcontainers.Redis;
using Xunit;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Test fixture for E2E tests providing shared infrastructure.
/// Sets up Redis, mock LLM, telemetry collection, and service container.
/// </summary>
public class E2ETestFixture : IAsyncLifetime
{
    private RedisContainer? _redisContainer;
    private TracerProvider? _tracerProvider;
    private MeterProvider? _meterProvider;

    public ServiceProvider ServiceProvider { get; private set; } = null!;
    public MockChatCompletionService MockLLM { get; private set; } = null!;
    public List<Activity> CollectedTraces { get; } = new();
    public Dictionary<string, List<Measurement<long>>> CollectedMetrics { get; } = new();
    public string RedisConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        // Start Redis container for state persistence
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        await _redisContainer.StartAsync();
        RedisConnectionString = _redisContainer.GetConnectionString();

        // Build service container
        var services = new ServiceCollection();

        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.e2e.json")
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = RedisConnectionString
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add mock LLM service
        MockLLM = new MockChatCompletionService();
        services.AddSingleton<IChatCompletionService>(MockLLM);

        // Add Semantic Kernel
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton<IChatCompletionService>(MockLLM);
        var kernel = kernelBuilder.Build();
        services.AddSingleton(kernel);

        // Setup OpenTelemetry for testing
        SetupTelemetry(services);

        ServiceProvider = services.BuildServiceProvider();
    }

    private void SetupTelemetry(ServiceCollection services)
    {
        // Setup basic telemetry for testing
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Microsoft.SemanticKernel*")
            .AddSource("Honua.AI*")
            .AddSource("ProcessFramework")
            .Build();

        // Note: Metric collection setup for testing
        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter("Microsoft.SemanticKernel*")
            .AddMeter("Honua.ProcessFramework")
            .Build();
    }

    public async Task DisposeAsync()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();

        if (ServiceProvider != null)
        {
            await ServiceProvider.DisposeAsync();
        }

        if (_redisContainer != null)
        {
            await _redisContainer.StopAsync();
            await _redisContainer.DisposeAsync();
        }
    }

public void ResetTelemetry()
{
    CollectedTraces.Clear();
    CollectedMetrics.Clear();
    MockLLM.Reset();
}
}
