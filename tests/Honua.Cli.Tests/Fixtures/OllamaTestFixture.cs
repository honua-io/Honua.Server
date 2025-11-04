using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Honua.Cli.Tests.Fixtures;

/// <summary>
/// Shared fixture for Ollama LLM container.
/// Provides a local LLM instance for testing instead of using real API keys.
/// Uses small models (phi3:mini ~2.3GB) to minimize resource usage.
/// </summary>
public sealed class OllamaTestFixture : IAsyncLifetime
{
    private IContainer? _container;
    private HttpClient? _httpClient;

    public string? Endpoint { get; private set; }
    public string ModelName { get; private set; } = "phi3:mini";
    public bool IsDockerAvailable { get; private set; }
    public bool OllamaAvailable { get; private set; }
    public bool ModelPulled { get; private set; }

    public async Task InitializeAsync()
    {
        // Check if Docker is available
        IsDockerAvailable = await IsDockerRunningAsync();

        if (!IsDockerAvailable)
        {
            Console.WriteLine("Docker is not available. Ollama container tests will be skipped.");
            return;
        }

        // Initialize Ollama container
        try
        {
            Console.WriteLine("Starting Ollama container...");

            _container = new ContainerBuilder()
                .WithImage("ollama/ollama:latest")
                .WithPortBinding(11434, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilPortIsAvailable(11434))
                .Build();

            await _container.StartAsync();

            var port = _container.GetMappedPublicPort(11434);
            Endpoint = $"http://localhost:{port}";

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Endpoint),
                Timeout = TimeSpan.FromMinutes(10) // Pull can take a while
            };

            // Verify Ollama is responsive
            var response = await _httpClient.GetAsync("/api/tags");
            response.EnsureSuccessStatusCode();

            OllamaAvailable = true;
            Console.WriteLine($"Ollama container started successfully at {Endpoint}");

            // Pull the model (this can take a few minutes on first run)
            await PullModelAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ollama container initialization failed: {ex.Message}");
            OllamaAvailable = false;
            ModelPulled = false;
        }
    }

    public async Task DisposeAsync()
    {
        _httpClient?.Dispose();

        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Pull the small model for testing. This method is idempotent.
    /// </summary>
    private async Task PullModelAsync()
    {
        try
        {
            Console.WriteLine($"Pulling model '{ModelName}' (this may take several minutes on first run)...");

            var pullRequest = new
            {
                name = ModelName,
                stream = false
            };

            var content = new StringContent(
                JsonSerializer.Serialize(pullRequest),
                Encoding.UTF8,
                "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var response = await _httpClient!.PostAsync("/api/pull", content, cts.Token);
            response.EnsureSuccessStatusCode();

            ModelPulled = true;
            Console.WriteLine($"Model '{ModelName}' pulled successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to pull model '{ModelName}': {ex.Message}");
            Console.WriteLine("Ollama tests will be skipped. To run these tests, ensure Docker has sufficient resources (4GB+ RAM recommended).");
            ModelPulled = false;
        }
    }

    /// <summary>
    /// Generate a completion using the Ollama model.
    /// </summary>
    public async Task<string> GenerateCompletionAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!OllamaAvailable || !ModelPulled)
        {
            throw new InvalidOperationException("Ollama is not available or model not pulled");
        }

        var generateRequest = new
        {
            model = ModelName,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = 100 // Limit tokens for faster tests
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(generateRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient!.PostAsync("/api/generate", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseBody);

        if (jsonDoc.RootElement.TryGetProperty("response", out var responseText))
        {
            return responseText.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Generate a chat completion using the Ollama model.
    /// </summary>
    public async Task<string> GenerateChatCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        if (!OllamaAvailable || !ModelPulled)
        {
            throw new InvalidOperationException("Ollama is not available or model not pulled");
        }

        var chatRequest = new
        {
            model = ModelName,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = 200 // Allow more tokens for chat
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(chatRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient!.PostAsync("/api/chat", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(responseBody);

        if (jsonDoc.RootElement.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var messageContent))
        {
            return messageContent.GetString() ?? string.Empty;
        }

        return string.Empty;
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
/// Collection definition for Ollama LLM tests.
/// All tests using this collection will share the same Ollama instance.
/// </summary>
[CollectionDefinition("OllamaContainer")]
public class OllamaContainerCollection : ICollectionFixture<OllamaTestFixture>
{
}
