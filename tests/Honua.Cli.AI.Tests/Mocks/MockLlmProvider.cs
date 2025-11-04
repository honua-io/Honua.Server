using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;

namespace Honua.Cli.AI.Tests.Mocks;

public class MockLlmProvider : ILlmProvider
{
    private readonly Queue<string> _responses = new();
    private readonly List<LlmCallRecord> _callHistory = new();
    private readonly Dictionary<string, string> _patternResponses = new();
    private int _callCount = 0;
    private TimeSpan _simulatedDelay = TimeSpan.Zero;
    private Exception? _exceptionToThrow = null;
    private int _throwAfterNCalls = 0;

    public IReadOnlyList<LlmCallRecord> CallHistory => _callHistory.AsReadOnly();
    public int CallCount => _callCount;

    // ILlmProvider implementation
    public string ProviderName => "Mock";
    public string DefaultModel => "mock-model";

    public void SetResponse(string response)
    {
        _responses.Clear();
        _responses.Enqueue(response);
    }

    public void EnqueueResponse(string response)
    {
        _responses.Enqueue(response);
    }

    public void SetPatternResponse(string promptPattern, string response)
    {
        _patternResponses[promptPattern] = response;
    }

    public void SimulateDelay(TimeSpan delay)
    {
        _simulatedDelay = delay;
    }

    public void SimulateFailure(Exception exception, int afterNCalls = 0)
    {
        _exceptionToThrow = exception;
        _throwAfterNCalls = afterNCalls;
    }

    public void Reset()
    {
        _responses.Clear();
        _callHistory.Clear();
        _patternResponses.Clear();
        _callCount = 0;
        _simulatedDelay = TimeSpan.Zero;
        _exceptionToThrow = null;
        _throwAfterNCalls = 0;
    }

    public async Task<string> GenerateAsync(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        _callCount++;

        var record = new LlmCallRecord
        {
            CallNumber = _callCount,
            Prompt = prompt,
            SystemPrompt = systemPrompt,
            Timestamp = DateTime.UtcNow
        };

        _callHistory.Add(record);

        // Simulate delay if configured
        if (_simulatedDelay > TimeSpan.Zero)
        {
            await Task.Delay(_simulatedDelay, cancellationToken);
        }

        // Throw exception if configured
        if (_exceptionToThrow != null && _callCount > _throwAfterNCalls)
        {
            record.ThrewException = true;
            throw _exceptionToThrow;
        }

        // Check for pattern-based responses
        foreach (var pattern in _patternResponses.Keys)
        {
            if (prompt.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                record.Response = _patternResponses[pattern];
                return _patternResponses[pattern];
            }
        }

        // Return queued response or default
        string response;
        if (_responses.Count > 0)
        {
            response = _responses.Dequeue();
        }
        else
        {
            response = GenerateDefaultResponse(prompt);
        }

        record.Response = response;
        return response;
    }

    public async Task<string> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        var response = await GenerateAsync(prompt, systemPrompt, cancellationToken);

        // Try to ensure it's valid JSON
        if (!response.TrimStart().StartsWith("{") && !response.TrimStart().StartsWith("["))
        {
            // Wrap in a simple JSON object if not already JSON
            response = JsonSerializer.Serialize(new { result = response });
        }

        return response;
    }

    public async Task<T> GenerateTypedAsync<T>(string prompt, string? systemPrompt = null, CancellationToken cancellationToken = default)
    {
        var jsonResponse = await GenerateJsonAsync<T>(prompt, systemPrompt, cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<T>(jsonResponse) ?? throw new InvalidOperationException("Failed to deserialize response");
        }
        catch
        {
            // Return a default instance if deserialization fails
            return Activator.CreateInstance<T>();
        }
    }

    private string GenerateDefaultResponse(string prompt)
    {
        // Generate contextual responses based on common patterns
        if (prompt.Contains("analyze", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("intent", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                intent = "query",
                confidence = 0.85,
                entities = new[] { "test_entity" },
                suggestedAgents = new[] { "DataAgent", "QueryAgent" }
            });
        }

        if (prompt.Contains("validate", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("review", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                isValid = true,
                issues = Array.Empty<string>(),
                suggestions = new[] { "Looks good" }
            });
        }

        if (prompt.Contains("select agent", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                selectedAgent = "DefaultAgent",
                confidence = 0.75,
                reasoning = "Best match for the task"
            });
        }

        // Default response
        return "Mock response for: " + prompt.Substring(0, Math.Min(50, prompt.Length));
    }

    public void VerifyPromptContains(string expectedContent, int? callIndex = null)
    {
        var calls = callIndex.HasValue
            ? new List<LlmCallRecord> { _callHistory[callIndex.Value - 1] }
            : _callHistory;

        if (!calls.Any(c => c.Prompt.Contains(expectedContent, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"No prompt contained '{expectedContent}'");
        }
    }

    public void VerifyCallCount(int expected)
    {
        if (_callCount != expected)
        {
            throw new InvalidOperationException($"Expected {expected} calls but got {_callCount}");
        }
    }

    // ILlmProvider required methods
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var response = await GenerateAsync(request.UserPrompt, request.SystemPrompt, cancellationToken);

        return new LlmResponse
        {
            Content = response,
            Model = DefaultModel,
            Success = true
        };
    }

    public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await CompleteAsync(request, cancellationToken);
        yield return new LlmStreamChunk
        {
            Content = response.Content,
            IsFinal = true,
            TokenCount = response.TotalTokens
        };
    }

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "mock-model" });
    }
}

public class LlmCallRecord
{
    public int CallNumber { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? Response { get; set; }
    public DateTime Timestamp { get; set; }
    public bool ThrewException { get; set; }
}