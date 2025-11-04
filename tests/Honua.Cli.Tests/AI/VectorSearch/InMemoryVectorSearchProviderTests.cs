using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.VectorSearch;
using Xunit;

namespace Honua.Cli.Tests.AI.VectorSearch;

[Trait("Category", "Unit")]
public class InMemoryVectorSearchProviderTests
{
    [Fact]
    public async Task Query_ShouldReturnMostSimilarDocuments()
    {
        var provider = new InMemoryVectorSearchProvider();
        var index = await provider.GetOrCreateIndexAsync(new VectorIndexDefinition("test", 3), CancellationToken.None);

        await index.UpsertAsync(new[]
        {
            new VectorSearchDocument("a", new float[] { 1, 0, 0 }, "Alpha"),
            new VectorSearchDocument("b", new float[] { 0, 1, 0 }, "Beta"),
            new VectorSearchDocument("c", new float[] { 0, 0, 1 }, "Gamma")
        }, CancellationToken.None);

        var results = await index.QueryAsync(new VectorSearchQuery(new float[] { 0.9f, 0.1f, 0 }, TopK: 1), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("a", results[0].Document.Id);
        Assert.True(results[0].Score > 0.5);
    }

    [Fact]
    public async Task Upsert_ShouldOverwriteExistingDocument()
    {
        var provider = new InMemoryVectorSearchProvider();
        var index = await provider.GetOrCreateIndexAsync(new VectorIndexDefinition("test-overwrite", 2), CancellationToken.None);

        await index.UpsertAsync(new[]
        {
            new VectorSearchDocument("doc", new float[] { 1, 0 }, "Original")
        }, CancellationToken.None);

        await index.UpsertAsync(new[]
        {
            new VectorSearchDocument("doc", new float[] { 0, 1 }, "Updated")
        }, CancellationToken.None);

        var results = await index.QueryAsync(new VectorSearchQuery(new float[] { 0, 1 }, TopK: 1), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal("doc", results[0].Document.Id);
        Assert.Equal("Updated", results[0].Document.Text);
    }
}
