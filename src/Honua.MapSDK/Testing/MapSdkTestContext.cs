using Bunit;
using Honua.MapSDK.Configuration;
using Honua.MapSDK.Core;
using Honua.MapSDK.Logging;
using Honua.MapSDK.Services;
using Honua.MapSDK.Services.DataLoading;
using Honua.MapSDK.Services.Performance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Testing;

/// <summary>
/// Test context for MapSDK components with pre-configured services.
/// </summary>
public class MapSdkTestContext : TestContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MapSdkTestContext"/> class.
    /// </summary>
    public MapSdkTestContext()
    {
        ConfigureDefaultServices();
    }

    /// <summary>
    /// Configures default services for testing.
    /// </summary>
    private void ConfigureDefaultServices()
    {
        // Add logging
        Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add MapSDK services
        Services.AddSingleton<ComponentBus>();
        Services.AddSingleton<MapConfigurationService>();

        // Add MapSDK options
        var options = new MapSdkOptions
        {
            EnablePerformanceMonitoring = false,
            LogLevel = LogLevel.Debug,
            Cache = new CacheOptions
            {
                Enabled = true,
                MaxSizeMB = 10,
                DefaultTtlSeconds = 60
            }
        };
        Services.AddSingleton(options);

        // Add data loading services
        Services.AddSingleton(sp => new DataCache(options.Cache));
        Services.AddHttpClient<DataLoader>();
        Services.AddScoped<DataLoader>();
        Services.AddScoped<StreamingLoader>();

        // Add logger
        Services.AddScoped<MapSdkLogger>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MapSdkLogger>>();
            return new MapSdkLogger(logger, options.EnablePerformanceMonitoring);
        });

        // Add performance monitor
        Services.AddScoped<PerformanceMonitor>(sp =>
        {
            var logger = sp.GetRequiredService<MapSdkLogger>();
            return new PerformanceMonitor(logger, options.EnablePerformanceMonitoring);
        });

        // Add keyboard shortcuts
        Services.AddScoped<KeyboardShortcuts>();

        // Add mock JS runtime
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Adds a mock ComponentBus with recorded messages.
    /// </summary>
    /// <returns>The mock ComponentBus.</returns>
    public MockComponentBus UseMockComponentBus()
    {
        var mockBus = new MockComponentBus();
        Services.AddSingleton<ComponentBus>(mockBus);
        return mockBus;
    }

    /// <summary>
    /// Configures test options.
    /// </summary>
    /// <param name="configure">Configuration action.</param>
    public void ConfigureOptions(Action<MapSdkOptions> configure)
    {
        var options = Services.BuildServiceProvider().GetRequiredService<MapSdkOptions>();
        configure(options);
    }
}

/// <summary>
/// Mock ComponentBus for testing that records all published messages.
/// </summary>
public class MockComponentBus : ComponentBus
{
    private readonly List<(string MessageType, object Message)> _publishedMessages = new();

    /// <summary>
    /// Gets the list of published messages.
    /// </summary>
    public IReadOnlyList<(string MessageType, object Message)> PublishedMessages => _publishedMessages;

    /// <summary>
    /// Publishes a message and records it.
    /// </summary>
    public new void Publish<TMessage>(TMessage message, string? source = null)
        where TMessage : class
    {
        _publishedMessages.Add((typeof(TMessage).Name, message!));
        base.Publish(message, source);
    }

    /// <summary>
    /// Clears all recorded messages.
    /// </summary>
    public new void Clear()
    {
        _publishedMessages.Clear();
        base.Clear();
    }

    /// <summary>
    /// Gets messages of a specific type.
    /// </summary>
    public List<T> GetMessagesOfType<T>()
    {
        return _publishedMessages
            .Where(m => m.Message is T)
            .Select(m => (T)m.Message)
            .ToList();
    }

    /// <summary>
    /// Asserts that a message of a specific type was published.
    /// </summary>
    public void AssertMessagePublished<T>()
    {
        if (!_publishedMessages.Any(m => m.Message is T))
        {
            throw new InvalidOperationException($"Expected message of type {typeof(T).Name} was not published");
        }
    }

    /// <summary>
    /// Asserts that a specific number of messages of a type were published.
    /// </summary>
    public void AssertMessageCount<T>(int expectedCount)
    {
        var count = _publishedMessages.Count(m => m.Message is T);
        if (count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} messages of type {typeof(T).Name}, but found {count}");
        }
    }
}
