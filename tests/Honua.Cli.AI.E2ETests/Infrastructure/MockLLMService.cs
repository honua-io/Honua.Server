using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Mock LLM service for E2E testing that returns predefined responses.
/// Simulates real LLM calls without external dependencies.
/// </summary>
public class MockChatCompletionService : IChatCompletionService
{
    private readonly Dictionary<string, string> _responseMap;
    private readonly Queue<string> _responseQueue;
    private int _callCount = 0;

    public string? LastResponse { get; private set; }

    public MockChatCompletionService()
    {
        _responseMap = new Dictionary<string, string>();
        _responseQueue = new Queue<string>();
        ConfigureDefaultResponses();
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        _callCount++;
        var response = GetNextResponse(chatHistory);
        var results = new List<ChatMessageContent>
        {
            new ChatMessageContent(AuthorRole.Assistant, response)
        };
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(results);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = GetNextResponse(chatHistory);
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, response);
        await Task.CompletedTask;
    }

    private string GetNextResponse(ChatHistory chatHistory)
    {
        // If we have queued responses, use them first
        if (_responseQueue.Count > 0)
        {
            var queued = _responseQueue.Dequeue();
            LastResponse = queued;
            return queued;
        }

        // Try to match user message to predefined responses
        var lastMessage = chatHistory[^1].Content ?? "";

        foreach (var kvp in _responseMap)
        {
            if (lastMessage.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                LastResponse = kvp.Value;
                return kvp.Value;
            }
        }

        LastResponse = "Mock response from LLM";
        return LastResponse;
    }

    public void AddResponse(string trigger, string response)
    {
        _responseMap[trigger] = response;
    }

    public void QueueResponse(string response)
    {
        _responseQueue.Enqueue(response);
    }

    public int GetCallCount() => _callCount;

    public void Reset()
    {
        _callCount = 0;
        _responseQueue.Clear();
        LastResponse = null;
    }

    private void ConfigureDefaultResponses()
    {
        // Deployment parameter extraction
        _responseMap["deploy"] = @"{
  ""cloudProvider"": ""AWS"",
  ""region"": ""us-west-2"",
  ""deploymentName"": ""test-deployment"",
  ""tier"": ""Development"",
  ""features"": [""GeoServer"", ""PostGIS"", ""VectorTiles""]
}";

        // Upgrade parameter extraction
        _responseMap["upgrade"] = @"{
  ""deploymentName"": ""production-deployment"",
  ""targetVersion"": ""2.0.0""
}";

        // Metadata parameter extraction
        _responseMap["metadata"] = @"{
  ""datasetPath"": ""/data/raster/landsat8.tif"",
  ""datasetName"": ""Landsat 8 Scene""
}";

        // GitOps parameter extraction
        _responseMap["gitops"] = @"{
  ""repoUrl"": ""https://github.com/org/honua-config.git"",
  ""branch"": ""main"",
  ""configPath"": ""deployments/production""
}";

        // Benchmark parameter extraction
        _responseMap["benchmark"] = @"{
  ""benchmarkName"": ""Load Test"",
  ""targetEndpoint"": ""https://api.honua.dev"",
  ""concurrency"": 100,
  ""duration"": 300
}";
    }
}
