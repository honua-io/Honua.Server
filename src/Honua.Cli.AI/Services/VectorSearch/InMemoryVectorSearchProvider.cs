// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Simple in-memory vector search provider used for local development and tests.
/// Not intended for production workloads.
/// </summary>
public sealed class InMemoryVectorSearchProvider : IVectorSearchProvider
{
    private readonly ConcurrentDictionary<string, InMemoryVectorSearchIndex> _indexes = new(StringComparer.OrdinalIgnoreCase);

    public Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken)
    {
        _indexes.TryRemove(indexName, out _);
        return Task.CompletedTask;
    }

    public Task<IVectorSearchIndex> GetOrCreateIndexAsync(VectorIndexDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var index = _indexes.GetOrAdd(definition.Name, _ => new InMemoryVectorSearchIndex(definition));
        if (index.Definition.Dimensions != definition.Dimensions)
        {
            throw new InvalidOperationException($"Vector index '{definition.Name}' already exists with dimensionality {index.Definition.Dimensions} (requested {definition.Dimensions}).");
        }

        return Task.FromResult<IVectorSearchIndex>(index);
    }

    public Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        return Task.FromResult(_indexes.ContainsKey(indexName));
    }

    private sealed class InMemoryVectorSearchIndex : IVectorSearchIndex
    {
        private readonly ConcurrentDictionary<string, StoredVectorDocument> _documents = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _sync = new();

        public InMemoryVectorSearchIndex(VectorIndexDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public VectorIndexDefinition Definition { get; }

        public Task UpsertAsync(IEnumerable<VectorSearchDocument> documents, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(documents);

            foreach (var doc in documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateDocument(doc);

                var stored = new StoredVectorDocument(
                    doc.Id,
                    doc.Embedding.ToArray(),
                    doc.Text,
                    doc.Metadata is null ? null : new Dictionary<string, string>(doc.Metadata, StringComparer.OrdinalIgnoreCase));

                _documents[doc.Id] = stored;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<VectorSearchResult>> QueryAsync(VectorSearchQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (query.TopK <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(query.TopK), "TopK must be positive.");
            }

            var queryVector = query.Embedding.ToArray();
            EnsureDimensions(queryVector.Length);

            var filter = query.MetadataFilter;
            var scored = new List<VectorSearchResult>();

            foreach (var entry in _documents.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (filter is not null && !MatchesFilter(entry, filter))
                {
                    continue;
                }

                var score = CosineSimilarity(queryVector, entry.Vector);
                var doc = entry.ToDocument();
                scored.Add(new VectorSearchResult(doc, score));
            }

            var results = scored
                .OrderByDescending(r => r.Score)
                .Take(query.TopK)
                .ToList();

            return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
        }

        private void ValidateDocument(VectorSearchDocument document)
        {
            if (string.IsNullOrWhiteSpace(document.Id))
            {
                throw new ArgumentException("Document Id is required.", nameof(document));
            }

            EnsureDimensions(document.Embedding.Length);
        }

        private void EnsureDimensions(int length)
        {
            if (length != Definition.Dimensions)
            {
                throw new InvalidOperationException($"Vector dimension mismatch. Expected {Definition.Dimensions}, received {length}.");
            }
        }

        private static bool MatchesFilter(StoredVectorDocument doc, IReadOnlyDictionary<string, string> filter)
        {
            if (doc.Metadata is null)
            {
                return false;
            }

            foreach (var pair in filter)
            {
                if (!doc.Metadata.TryGetValue(pair.Key, out var value))
                {
                    return false;
                }

                if (!string.Equals(value, pair.Value, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            var dot = 0.0;
            var magA = 0.0;
            var magB = 0.0;

            for (var i = 0; i < vectorA.Length; i++)
            {
                dot += vectorA[i] * vectorB[i];
                magA += vectorA[i] * vectorA[i];
                magB += vectorB[i] * vectorB[i];
            }

            if (magA == 0 || magB == 0)
            {
                return 0.0;
            }

            return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
        }

        private sealed record StoredVectorDocument(
            string Id,
            float[] Vector,
            string? Text,
            Dictionary<string, string>? Metadata)
        {
            public VectorSearchDocument ToDocument()
            {
                IReadOnlyDictionary<string, string>? metadataView = Metadata;
                return new VectorSearchDocument(Id, Vector, Text, metadataView);
            }
        }
    }
}
