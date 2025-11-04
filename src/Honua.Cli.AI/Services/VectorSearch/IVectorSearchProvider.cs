// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.VectorSearch;

public interface IVectorSearchProvider
{
    Task<IVectorSearchIndex> GetOrCreateIndexAsync(VectorIndexDefinition definition, CancellationToken cancellationToken);

    Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken);

    Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken);
}

public interface IVectorSearchIndex
{
    Task UpsertAsync(IEnumerable<VectorSearchDocument> documents, CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorSearchResult>> QueryAsync(VectorSearchQuery query, CancellationToken cancellationToken);
}
