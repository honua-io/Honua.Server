using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.VectorSearch;

[Trait("Category", "Unit")]
public sealed class VectorDeploymentPatternKnowledgeStoreTests
{
    [Fact]
    public async Task SearchPatternsAsync_ShouldReturnMatchingPattern()
    {
        var provider = new InMemoryVectorSearchProvider();
        var embedding = new StubEmbeddingProvider();
        var options = Options.Create(new VectorSearchOptions
        {
            Provider = VectorSearchProviders.InMemory,
            IndexName = "test-patterns",
            Dimensions = embedding.Dimensions
        });

        var store = new VectorDeploymentPatternKnowledgeStore(
            provider,
            embedding,
            options,
            NullLogger<VectorDeploymentPatternKnowledgeStore>.Instance);

        var awsPattern = new DeploymentPattern
        {
            Id = "aws-pattern",
            Name = "AWS Reference",
            CloudProvider = "aws",
            DataVolumeMin = 10,
            DataVolumeMax = 100,
            ConcurrentUsersMin = 10,
            ConcurrentUsersMax = 100,
            SuccessRate = 0.95,
            DeploymentCount = 12,
            Configuration = new { service = "Honua" },
            ApprovedBy = "alice",
            ApprovedDate = DateTime.UtcNow
        };

        await store.IndexApprovedPatternAsync(awsPattern, CancellationToken.None);

        var requirements = new DeploymentRequirements
        {
            CloudProvider = "aws",
            DataVolumeGb = 50,
            ConcurrentUsers = 50,
            Region = "us-east-1"
        };

        var results = await store.SearchPatternsAsync(requirements, CancellationToken.None);

        Assert.Single(results);
        var result = results[0];
        Assert.Equal("aws-pattern", result.Id);
        Assert.Equal("AWS Reference", result.PatternName);
        Assert.Equal("aws", result.CloudProvider);
        Assert.Equal("{\"service\":\"Honua\"}", result.ConfigurationJson);
        Assert.True(result.Score > 0.5);
    }

    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public string ProviderName => "stub";

        public string DefaultModel => "stub-model";

        public int Dimensions => 3;

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<EmbeddingResponse> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var vector = ResolveVector(text);
            return Task.FromResult(new EmbeddingResponse
            {
                Embedding = vector,
                Model = DefaultModel,
                Success = true
            });
        }

        public Task<IReadOnlyList<EmbeddingResponse>> GetEmbeddingBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            var responses = texts.Select(text => new EmbeddingResponse
            {
                Embedding = ResolveVector(text),
                Model = DefaultModel,
                Success = true
            }).ToArray();

            return Task.FromResult<IReadOnlyList<EmbeddingResponse>>(responses);
        }

        private float[] ResolveVector(string text)
        {
            if (text.Contains("aws", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 1f, 0f, 0f };
            }

            if (text.Contains("azure", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { 0f, 1f, 0f };
            }

            return new[] { 0f, 0f, 1f };
        }
    }
}
