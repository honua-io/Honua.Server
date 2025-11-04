using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.AI.Providers;

/// <summary>
/// Tests for OllamaLlmProvider to verify local LLM inference functionality.
/// Uses mock HTTP responses to simulate Ollama API behavior.
/// </summary>
[Trait("Category", "Unit")]
public class OllamaLlmProviderTests
{
    private readonly LlmProviderOptions _defaultOptions;

    public OllamaLlmProviderTests()
    {
        _defaultOptions = new LlmProviderOptions
        {
            Provider = "Ollama",
            TimeoutSeconds = 120,
            Ollama = new OllamaOptions
            {
                EndpointUrl = "http://localhost:11434",
                DefaultModel = "llama3.2"
            }
        };
    }

    [Fact]
    public void Constructor_ValidOptions_Succeeds()
    {
        // Act
        var provider = new OllamaLlmProvider(_defaultOptions, NullLogger<OllamaLlmProvider>.Instance);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal("Ollama", provider.ProviderName);
        Assert.Equal("llama3.2", provider.DefaultModel);
    }

    [Fact]
    public void Constructor_NullOllamaOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Ollama = null!
        };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new OllamaLlmProvider(options, NullLogger<OllamaLlmProvider>.Instance));
    }

    [Fact]
    public void Constructor_EmptyEndpointUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var options = new LlmProviderOptions
        {
            Ollama = new OllamaOptions
            {
                EndpointUrl = "",
                DefaultModel = "llama3.2"
            }
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            new OllamaLlmProvider(options, NullLogger<OllamaLlmProvider>.Instance));
    }

    [Fact]
    public async Task IsAvailableAsync_OllamaRunningWithModel_ReturnsTrue()
    {
        // Arrange
        var mockResponse = new
        {
            models = new[]
            {
                new
                {
                    name = "llama3.2:latest",
                    size = 2000000000L,
                    modified_at = "2024-01-15T10:30:00Z"
                }
            }
        };

        var httpClient = CreateMockHttpClient("/api/tags", HttpStatusCode.OK, mockResponse);
        var options = _defaultOptions;
        var provider = CreateProvider(options, httpClient);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        Assert.True(isAvailable);
    }

    [Fact]
    public async Task IsAvailableAsync_OllamaNotRunning_ReturnsFalse()
    {
        // Arrange
        var httpClient = CreateFailingHttpClient();
        var provider = CreateProvider(_defaultOptions, httpClient);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task IsAvailableAsync_NoModelsAvailable_ReturnsFalse()
    {
        // Arrange
        var mockResponse = new
        {
            models = Array.Empty<object>()
        };

        var httpClient = CreateMockHttpClient("/api/tags", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task IsAvailableAsync_ConfiguredModelNotAvailable_ReturnsFalse()
    {
        // Arrange
        var mockResponse = new
        {
            models = new[]
            {
                new
                {
                    name = "mistral:latest",
                    size = 4000000000L,
                    modified_at = "2024-01-15T10:30:00Z"
                }
            }
        };

        var httpClient = CreateMockHttpClient("/api/tags", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        // Act
        var isAvailable = await provider.IsAvailableAsync();

        // Assert
        Assert.False(isAvailable);
    }

    [Fact]
    public async Task CompleteAsync_ValidRequest_ReturnsSuccessfulResponse()
    {
        // Arrange
        var mockResponse = new
        {
            model = "llama3.2",
            response = "Hello! I'm Llama 3.2. How can I help you today?",
            done = true,
            prompt_eval_count = 15,
            eval_count = 12
        };

        var httpClient = CreateMockHttpClient("/api/generate", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            UserPrompt = "Hello, who are you?",
            MaxTokens = 100,
            Temperature = 0.7
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("Hello! I'm Llama 3.2. How can I help you today?", response.Content);
        Assert.Equal("llama3.2", response.Model);
        Assert.Equal(27, response.TotalTokens); // 15 + 12
    }

    [Fact]
    public async Task CompleteAsync_WithSystemPrompt_IncludesSystemPromptInRequest()
    {
        // Arrange
        var mockResponse = new
        {
            model = "llama3.2",
            response = "As a helpful AI assistant, I'm here to answer your questions!",
            done = true,
            prompt_eval_count = 20,
            eval_count = 15
        };

        var httpClient = CreateMockHttpClient("/api/generate", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            SystemPrompt = "You are a helpful AI assistant.",
            UserPrompt = "What is your purpose?",
            MaxTokens = 100,
            Temperature = 0.5
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Contains("helpful AI assistant", response.Content);
        Assert.Equal(35, response.TotalTokens);
    }

    [Fact]
    public async Task CompleteAsync_WithConversationHistory_BuildsPromptCorrectly()
    {
        // Arrange
        var mockResponse = new
        {
            model = "llama3.2",
            response = "The capital of France is Paris.",
            done = true,
            prompt_eval_count = 25,
            eval_count = 10
        };

        var httpClient = CreateMockHttpClient("/api/generate", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            SystemPrompt = "You are a geography expert.",
            ConversationHistory = new List<LlmMessage>
            {
                new() { Role = "user", Content = "What is the capital of Germany?" },
                new() { Role = "assistant", Content = "The capital of Germany is Berlin." }
            },
            UserPrompt = "What about France?",
            MaxTokens = 50
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Contains("Paris", response.Content);
    }

    [Fact]
    public async Task CompleteAsync_OllamaReturnsError_ReturnsFailedResponse()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("/api/generate", HttpStatusCode.InternalServerError, "Internal server error");
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            UserPrompt = "Test prompt",
            MaxTokens = 100
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("InternalServerError", response.ErrorMessage);
    }

    [Fact]
    public async Task CompleteAsync_OllamaNotReachable_ReturnsFailedResponse()
    {
        // Arrange
        var httpClient = CreateFailingHttpClient();
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            UserPrompt = "Test prompt",
            MaxTokens = 100
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("Cannot connect to Ollama", response.ErrorMessage);
    }

    [Fact]
    public async Task CompleteAsync_Timeout_ReturnsFailedResponse()
    {
        // Arrange
        var httpClient = CreateTimeoutHttpClient();
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            UserPrompt = "Test prompt",
            MaxTokens = 100
        };

        // Act
        var response = await provider.CompleteAsync(request);

        // Assert
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.ErrorMessage);
        Assert.Contains("timed out", response.ErrorMessage);
    }

    [Fact]
    public async Task StreamAsync_ValidRequest_StreamsResponseChunks()
    {
        // Arrange
        var streamLines = new[]
        {
            JsonSerializer.Serialize(new { model = "llama3.2", response = "Hello", done = false }),
            JsonSerializer.Serialize(new { model = "llama3.2", response = " world", done = false }),
            JsonSerializer.Serialize(new { model = "llama3.2", response = "!", done = true, prompt_eval_count = 10, eval_count = 8 })
        };

        var httpClient = CreateStreamingHttpClient("/api/generate", streamLines);
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            UserPrompt = "Say hello",
            MaxTokens = 50
        };

        // Act
        var chunks = new List<LlmStreamChunk>();
        await foreach (var chunk in provider.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Equal(4, chunks.Count); // 3 content chunks + 1 final chunk
        Assert.Equal("Hello", chunks[0].Content);
        Assert.Equal(" world", chunks[1].Content);
        Assert.Equal("!", chunks[2].Content);
        Assert.True(chunks[3].IsFinal);
        Assert.Equal(18, chunks[3].TokenCount);
    }

    [Fact]
    public async Task StreamAsync_OllamaReturnsError_YieldsErrorChunk()
    {
        // Arrange
        var httpClient = CreateMockHttpClient("/api/generate", HttpStatusCode.InternalServerError, "Server error");
        var provider = CreateProvider(_defaultOptions, httpClient);

        var request = new LlmRequest
        {
            UserPrompt = "Test prompt",
            MaxTokens = 50
        };

        // Act
        var chunks = new List<LlmStreamChunk>();
        await foreach (var chunk in provider.StreamAsync(request))
        {
            chunks.Add(chunk);
        }

        // Assert
        Assert.Single(chunks);
        Assert.True(chunks[0].IsFinal);
        Assert.Contains("Error", chunks[0].Content);
    }

    [Fact]
    public async Task ListModelsAsync_OllamaRunning_ReturnsModelList()
    {
        // Arrange
        var mockResponse = new
        {
            models = new[]
            {
                new { name = "llama3.2:latest", size = 2000000000L },
                new { name = "mistral:latest", size = 4000000000L },
                new { name = "codellama:latest", size = 3000000000L }
            }
        };

        var httpClient = CreateMockHttpClient("/api/tags", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        // Act
        var models = await provider.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Equal(3, models.Count);
        Assert.Contains("llama3.2:latest", models);
        Assert.Contains("mistral:latest", models);
        Assert.Contains("codellama:latest", models);
    }

    [Fact]
    public async Task ListModelsAsync_OllamaNotReachable_ReturnsDefaultModel()
    {
        // Arrange
        var httpClient = CreateFailingHttpClient();
        var provider = CreateProvider(_defaultOptions, httpClient);

        // Act
        var models = await provider.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Single(models);
        Assert.Equal("llama3.2", models[0]);
    }

    [Fact]
    public async Task ListModelsAsync_NoModels_ReturnsEmptyList()
    {
        // Arrange
        var mockResponse = new
        {
            models = Array.Empty<object>()
        };

        var httpClient = CreateMockHttpClient("/api/tags", HttpStatusCode.OK, mockResponse);
        var provider = CreateProvider(_defaultOptions, httpClient);

        // Act
        var models = await provider.ListModelsAsync();

        // Assert
        Assert.NotNull(models);
        Assert.Empty(models);
    }

    #region Helper Methods

    private OllamaLlmProvider CreateProvider(LlmProviderOptions options, HttpClient httpClient)
    {
        // Use reflection to set the private _httpClient field since we can't inject it via constructor
        var provider = new OllamaLlmProvider(options, NullLogger<OllamaLlmProvider>.Instance);
        var httpClientField = typeof(OllamaLlmProvider).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        httpClientField?.SetValue(provider, httpClient);
        return provider;
    }

    private HttpClient CreateMockHttpClient(string requestPath, HttpStatusCode statusCode, object responseContent)
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(statusCode);

            if (responseContent is string stringContent)
            {
                response.Content = new StringContent(stringContent);
            }
            else
            {
                var json = JsonSerializer.Serialize(responseContent);
                response.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        });

        return new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    private HttpClient CreateStreamingHttpClient(string requestPath, string[] streamLines)
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var streamContent = string.Join("\n", streamLines);
            response.Content = new StringContent(streamContent);
            return Task.FromResult(response);
        });

        return new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    private HttpClient CreateFailingHttpClient()
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            throw new HttpRequestException("Cannot connect to Ollama");
        });

        return new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    private HttpClient CreateTimeoutHttpClient()
    {
        var mockHandler = new MockHttpMessageHandler((request, cancellationToken) =>
        {
            throw new TaskCanceledException("Request timed out");
        });

        return new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(1)
        };
    }

    #endregion

    #region Mock HTTP Handler

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendFunc;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendFunc)
        {
            _sendFunc = sendFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _sendFunc(request, cancellationToken);
        }
    }

    #endregion
}
