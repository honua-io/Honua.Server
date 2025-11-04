// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.VectorSearch;

public sealed record VectorIndexDefinition(string Name, int Dimensions);

public sealed record VectorSearchDocument(
    string Id,
    ReadOnlyMemory<float> Embedding,
    string? Text = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record VectorSearchQuery(
    ReadOnlyMemory<float> Embedding,
    int TopK = 5,
    IReadOnlyDictionary<string, string>? MetadataFilter = null);

public sealed record VectorSearchResult(
    VectorSearchDocument Document,
    double Score);
