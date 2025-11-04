using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Honua.Cli.AI.Tests.TestInfrastructure;

/// <summary>
/// Helper class for creating AI service providers in tests.
/// Supports both local services (LocalAI, PostgreSQL) and mocked services.
/// </summary>
public static class AITestHelpers
{
    private const string LocalAIDefaultEndpoint = "http://localai:8080";
    private const string PostgresDefaultConnection = "Host=postgres;Database=honua;Username=postgres;Password=Honua123!";

    /// <summary>
    /// Checks if LocalAI is available by attempting a health check.
    /// </summary>
    public static async Task<bool> IsLocalAIAvailableAsync()
    {
        try
        {
            var endpoint = GetLocalAIEndpoint();
            using var httpClient = new HttpClient
            {
                BaseAddress = new Uri(endpoint),
                Timeout = TimeSpan.FromSeconds(5)
            };

            // Try to access the models endpoint
            var response = await httpClient.GetAsync("/v1/models", CancellationToken.None);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if PostgreSQL with pgvector is available.
    /// </summary>
    public static async Task<bool> IsPostgresAvailableAsync()
    {
        try
        {
            var connectionString = GetPostgresConnectionString();
            await using var connection = new Npgsql.NpgsqlConnection(connectionString);
            await connection.OpenAsync(CancellationToken.None);

            // Check if pgvector extension is available
            await using var cmd = new Npgsql.NpgsqlCommand("SELECT * FROM pg_extension WHERE extname = 'vector';", connection);
            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates an LLM provider - LocalAI if available and enabled, otherwise a mock.
    /// </summary>
    public static ILlmProvider CreateLlmProvider()
    {
        var useLocalAI = Environment.GetEnvironmentVariable("USE_LOCAL_AI_FOR_TESTS");
        if (string.Equals(useLocalAI, "true", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var options = new LlmProviderOptions
                {
                    Provider = "LocalAI",
                    LocalAI = new LocalAIOptions
                    {
                        EndpointUrl = GetLocalAIEndpoint(),
                        DefaultModel = "llama-3.1-8b-instruct"
                    }
                };

                return new LocalAILlmProvider(options);
            }
            catch
            {
                // Fall back to mock if LocalAI creation fails
            }
        }

        // Return configured mock provider for guard system tests
        var mock = new MockLlmProvider();
        ConfigureMockLlmForGuardTests(mock);
        return mock;
    }

    /// <summary>
    /// Configures the MockLlmProvider to detect common attack patterns for guard system tests.
    /// </summary>
    private static void ConfigureMockLlmForGuardTests(MockLlmProvider mock)
    {
        // Setup response generator that detects malicious patterns
        mock.SetupResponseGenerator("analyze", (prompt) =>
        {
            var lowerPrompt = prompt.ToLowerInvariant();

            // Detect if this is an input guard request or output guard request
            bool isOutputGuard = lowerPrompt.Contains("agent's output") || lowerPrompt.Contains("hallucination");

            if (isOutputGuard)
            {
                // Output guard - check for dangerous operations
                bool hasDangerousOps =
                    lowerPrompt.Contains("rm -rf") ||
                    lowerPrompt.Contains("drop table") ||
                    lowerPrompt.Contains("drop database") ||
                    lowerPrompt.Contains("delete from") ||
                    lowerPrompt.Contains("truncate table") ||
                    lowerPrompt.Contains("chmod 777") ||
                    lowerPrompt.Contains("sudo rm") ||
                    lowerPrompt.Contains("eval(") ||
                    lowerPrompt.Contains("exec(") ||
                    lowerPrompt.Contains("__import__");

                if (hasDangerousOps)
                {
                    var issues = new System.Collections.Generic.List<string>();
                    if (lowerPrompt.Contains("rm -rf"))
                        issues.Add("Dangerous operation: rm -rf");
                    if (lowerPrompt.Contains("sudo rm"))
                        issues.Add("Dangerous operation: sudo rm");
                    if (lowerPrompt.Contains("drop table") || lowerPrompt.Contains("drop database"))
                        issues.Add("Dangerous operation: DROP statement");
                    if (lowerPrompt.Contains("delete from"))
                        issues.Add("Dangerous operation: DELETE statement");
                    if (lowerPrompt.Contains("chmod 777"))
                        issues.Add("Dangerous operation: chmod 777");
                    if (lowerPrompt.Contains("eval(") || lowerPrompt.Contains("exec(") || lowerPrompt.Contains("__import__"))
                        issues.Add("Dangerous operation: code execution");

                    var issuesJson = string.Join(", ", issues.Select(i => $"\"{i}\""));
                    return $"{{\"isSafe\": false, \"confidence\": 0.95, \"hallucinationRisk\": 0.1, \"issues\": [{issuesJson}], \"explanation\": \"Output contains dangerous operations\"}}";
                }

                // Safe output
                return "{\"isSafe\": true, \"confidence\": 0.9, \"hallucinationRisk\": 0.2, \"issues\": [], \"explanation\": \"Output appears safe\"}";
            }
            else
            {
                // Input guard - check for attack patterns
                bool isMalicious =
                    lowerPrompt.Contains("ignore previous") ||
                    lowerPrompt.Contains("disregard") ||
                    lowerPrompt.Contains("forget") ||
                    lowerPrompt.Contains("you are now") ||
                    lowerPrompt.Contains("drop table") ||
                    lowerPrompt.Contains("rm -rf") ||
                    lowerPrompt.Contains("curl") && lowerPrompt.Contains("bash") ||
                    lowerPrompt.Contains("| bash") ||
                    lowerPrompt.Contains("| sh") ||
                    lowerPrompt.Contains("<script") ||
                    lowerPrompt.Contains("javascript:") ||
                    lowerPrompt.Contains("<img") && lowerPrompt.Contains("onerror") ||
                    lowerPrompt.Contains("<svg") && lowerPrompt.Contains("onload") ||
                    lowerPrompt.Contains("admin'--") ||
                    lowerPrompt.Contains("union select") ||
                    lowerPrompt.Contains("system prompt") ||
                    lowerPrompt.Contains("show me your") ||
                    lowerPrompt.Contains("what are your instructions") ||
                    lowerPrompt.Contains("repeat the text above") ||
                    lowerPrompt.Contains("output the content from") ||
                    lowerPrompt.Contains("eval(") ||
                    lowerPrompt.Contains("exec(") ||
                    lowerPrompt.Contains("__import__");

                if (isMalicious)
                {
                    // Determine which threats were detected
                    var threats = new System.Collections.Generic.List<string>();
                    if (lowerPrompt.Contains("ignore previous") || lowerPrompt.Contains("disregard") || lowerPrompt.Contains("forget"))
                        threats.Add("prompt injection attempt");
                    if (lowerPrompt.Contains("you are now"))
                        threats.Add("role manipulation attempt");
                    if (lowerPrompt.Contains("drop table") || lowerPrompt.Contains("union select") || lowerPrompt.Contains("admin'--"))
                        threats.Add("SQL injection");
                    if (lowerPrompt.Contains("rm -rf"))
                        threats.Add("dangerous shell command");
                    if (lowerPrompt.Contains("| bash") || lowerPrompt.Contains("| sh") || (lowerPrompt.Contains("curl") && lowerPrompt.Contains("bash")))
                        threats.Add("shell command injection");
                    if (lowerPrompt.Contains("<script") || lowerPrompt.Contains("javascript:") || lowerPrompt.Contains("onerror") || lowerPrompt.Contains("onload"))
                        threats.Add("XSS injection");
                    if (lowerPrompt.Contains("system prompt") || lowerPrompt.Contains("show me your") || lowerPrompt.Contains("what are your instructions"))
                        threats.Add("system prompt extraction attempt");
                    if (lowerPrompt.Contains("eval(") || lowerPrompt.Contains("exec(") || lowerPrompt.Contains("__import__"))
                        threats.Add("code execution injection");

                    var threatsJson = string.Join(", ", threats.Select(t => $"\"{t}\""));
                    return $"{{\"isSafe\": false, \"confidence\": 0.95, \"threats\": [{threatsJson}], \"explanation\": \"Detected malicious content\"}}";
                }

                // Safe input
                return "{\"isSafe\": true, \"confidence\": 0.9, \"threats\": [], \"explanation\": \"Input appears safe\"}";
            }
        });
    }

    /// <summary>
    /// Creates an embedding provider - LocalAI if available and enabled, otherwise a mock.
    /// </summary>
    public static IEmbeddingProvider CreateEmbeddingProvider()
    {
        var useLocalAI = Environment.GetEnvironmentVariable("USE_LOCAL_AI_FOR_TESTS");
        if (string.Equals(useLocalAI, "true", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var options = new LlmProviderOptions
                {
                    Provider = "LocalAI",
                    LocalAI = new LocalAIOptions
                    {
                        EndpointUrl = GetLocalAIEndpoint(),
                        DefaultEmbeddingModel = "all-minilm-l6-v2"
                    }
                };

                return new LocalAIEmbeddingProvider(options, dimensions: 1536);
            }
            catch
            {
                // Fall back to mock if LocalAI creation fails
            }
        }

        // Return mock provider (dimensions are fixed at 1536)
        return new MockEmbeddingProvider();
    }

    /// <summary>
    /// Creates a vector search provider - PostgreSQL if available and enabled, otherwise in-memory.
    /// </summary>
    public static IDeploymentPatternKnowledgeStore CreateVectorSearch()
    {
        var usePostgres = Environment.GetEnvironmentVariable("USE_POSTGRES_FOR_TESTS");
        if (string.Equals(usePostgres, "true", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new[]
                    {
                        new System.Collections.Generic.KeyValuePair<string, string?>(
                            "ConnectionStrings:PostgreSQL",
                            GetPostgresConnectionString()
                        ),
                        new System.Collections.Generic.KeyValuePair<string, string?>(
                            "PostgresVectorSearch:TableName",
                            $"test_patterns_{Guid.NewGuid():N}"
                        )
                    })
                    .Build();

                var embeddingProvider = CreateEmbeddingProvider();
                var provider = new PostgresVectorSearchProvider(
                    config,
                    embeddingProvider,
                    NullLogger<PostgresVectorSearchProvider>.Instance);

                // Ensure schema is created
                provider.EnsureSchemaAsync().Wait();

                return provider;
            }
            catch
            {
                // Fall back to in-memory if PostgreSQL creation fails
            }
        }

        // Return in-memory vector search
        var inMemoryProvider = new InMemoryVectorSearchProvider();
        var inMemoryEmbedding = CreateEmbeddingProvider();

        var options = Microsoft.Extensions.Options.Options.Create(new VectorSearchOptions
        {
            Provider = "InMemory",
            IndexName = $"test-patterns-{Guid.NewGuid():N}"
        });

        return new VectorDeploymentPatternKnowledgeStore(
            inMemoryProvider,
            inMemoryEmbedding,
            options,
            NullLogger<VectorDeploymentPatternKnowledgeStore>.Instance);
    }

    /// <summary>
    /// Gets the LocalAI endpoint from environment or uses default.
    /// </summary>
    public static string GetLocalAIEndpoint()
    {
        return Environment.GetEnvironmentVariable("LOCALAI_ENDPOINT") ?? LocalAIDefaultEndpoint;
    }

    /// <summary>
    /// Gets the PostgreSQL connection string from environment or uses default.
    /// </summary>
    public static string GetPostgresConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSQL") ?? PostgresDefaultConnection;
    }

    /// <summary>
    /// Creates a vector search provider with a custom embedding provider.
    /// Useful for testing embedding behavior.
    /// </summary>
    public static IDeploymentPatternKnowledgeStore CreateVectorSearchWithEmbedding(IEmbeddingProvider embeddingProvider)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new VectorSearchOptions
        {
            Provider = "InMemory",
            IndexName = $"test-patterns-{Guid.NewGuid():N}"
        });

        var inMemoryProvider = new InMemoryVectorSearchProvider();

        return new VectorDeploymentPatternKnowledgeStore(
            inMemoryProvider,
            embeddingProvider,
            options,
            NullLogger<VectorDeploymentPatternKnowledgeStore>.Instance);
    }
}

/// <summary>
/// Mock embedding provider that always fails with a specific error message.
/// </summary>
public sealed class FailingEmbeddingProvider : IEmbeddingProvider
{
    private readonly string _errorMessage;

    public FailingEmbeddingProvider(string errorMessage)
    {
        _errorMessage = errorMessage;
    }

    public string ProviderName => "FailingProvider";
    public string DefaultModel => "test-model";
    public int Dimensions => 1536;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EmbeddingResponse
        {
            Embedding = Array.Empty<float>(),
            Model = DefaultModel,
            Success = false,
            ErrorMessage = _errorMessage
        });
    }

    public Task<System.Collections.Generic.IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(
        System.Collections.Generic.IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var responses = new List<EmbeddingResponse>();
        foreach (var _ in texts)
        {
            responses.Add(new EmbeddingResponse
            {
                Embedding = Array.Empty<float>(),
                Model = DefaultModel,
                Success = false,
                ErrorMessage = _errorMessage
            });
        }
        return Task.FromResult<System.Collections.Generic.IReadOnlyList<EmbeddingResponse>>(responses);
    }
}

/// <summary>
/// Mock embedding provider that captures the text sent for embedding.
/// Useful for testing what text is being generated for embeddings.
/// </summary>
public sealed class CapturingEmbeddingProvider : IEmbeddingProvider
{
    private readonly Random _random = new(42);

    public string? LastCapturedText { get; private set; }

    public string ProviderName => "CapturingProvider";
    public string DefaultModel => "test-model";
    public int Dimensions => 1536;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        LastCapturedText = text;

        // Generate a test embedding
        var embedding = new float[Dimensions];
        for (int i = 0; i < Dimensions; i++)
        {
            embedding[i] = (float)(_random.NextDouble() * 2 - 1);
        }

        return Task.FromResult(new EmbeddingResponse
        {
            Embedding = embedding,
            Model = DefaultModel,
            Success = true,
            TotalTokens = 100
        });
    }

    public Task<System.Collections.Generic.IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(
        System.Collections.Generic.IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        var responses = new List<EmbeddingResponse>();
        foreach (var text in texts)
        {
            LastCapturedText = text; // Will capture the last one
            var embedding = new float[Dimensions];
            for (int i = 0; i < Dimensions; i++)
            {
                embedding[i] = (float)(_random.NextDouble() * 2 - 1);
            }

            responses.Add(new EmbeddingResponse
            {
                Embedding = embedding,
                Model = DefaultModel,
                Success = true,
                TotalTokens = 100
            });
        }
        return Task.FromResult<System.Collections.Generic.IReadOnlyList<EmbeddingResponse>>(responses);
    }
}
