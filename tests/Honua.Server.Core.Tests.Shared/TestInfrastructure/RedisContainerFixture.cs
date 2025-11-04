using System;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Shared fixture for Redis container infrastructure.
/// Provides a reusable Redis instance across multiple test classes.
/// </summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    private RedisContainer? _redisContainer;

    public IConnectionMultiplexer? Redis { get; private set; }
    public string? ConnectionString { get; private set; }
    public bool IsDockerAvailable { get; private set; }
    public bool RedisAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        // Check if Docker is available
        IsDockerAvailable = await IsDockerRunningAsync();

        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is not available. Redis container tests will be skipped.");
            return;
        }

        // Initialize Redis
        try
        {
            _redisContainer = new RedisBuilder()
                .WithImage("redis:7-alpine")
                .Build();

            await _redisContainer.StartAsync();

            ConnectionString = _redisContainer.GetConnectionString();
            Redis = await ConnectionMultiplexer.ConnectAsync(ConnectionString);
            RedisAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Redis container initialization failed: {ex.Message}");
            RedisAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        Redis?.Dispose();

        if (_redisContainer != null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    private static async Task<bool> IsDockerRunningAsync()
    {
        try
        {
            var container = new ContainerBuilder()
                .WithImage("alpine:latest")
                .WithCommand("echo", "test")
                .WithWaitStrategy(Wait.ForUnixContainer())
                .Build();

            await container.StartAsync();
            await container.DisposeAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Collection definition for Redis container tests.
/// All tests using this collection will share the same Redis instance.
/// </summary>
[CollectionDefinition("RedisContainer")]
public class RedisContainerCollection : ICollectionFixture<RedisContainerFixture>
{
}
