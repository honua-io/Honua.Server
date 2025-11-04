using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Xunit;

namespace Honua.Cli.AI.Tests.Services.AI.Providers;

[Trait("Category", "Unit")]
public sealed class AnthropicEmbeddingProviderTests
{
    [Fact]
    public async Task GetEmbeddingAsync_WithoutVoyageKey_ShouldProduceDeterministicMockEmbedding()
    {
        // Arrange
        Environment.SetEnvironmentVariable("VOYAGE_API_KEY", null);

        var options = new LlmProviderOptions
        {
            Anthropic = new AnthropicOptions
            {
                ApiKey = "test-key",
                DefaultModel = "claude-3-5-sonnet-20241022"
            }
        };

        var provider = new AnthropicEmbeddingProvider(options);

        // Act
        var first = await provider.GetEmbeddingAsync("deterministic text", CancellationToken.None);
        var second = await provider.GetEmbeddingAsync("deterministic text", CancellationToken.None);

        // Assert
        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.Embedding.Should().Equal(second.Embedding);
        first.ErrorMessage.Should().Contain("deterministic mock embedding");
    }
}
