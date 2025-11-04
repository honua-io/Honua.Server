// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Azure;
using AzureSearchDocuments = global::Azure.Search.Documents;
using AzureSearchModels = global::Azure.Search.Documents.Models;
using AzureIndexModels = global::Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Azure AI Search-backed vector search provider.
/// </summary>
public sealed class AzureVectorSearchProvider : IVectorSearchProvider
{
    private readonly AzureSearchDocuments.Indexes.SearchIndexClient _indexClient;
    private readonly AzureVectorSearchOptions _azureOptions;
    private readonly ILogger<AzureVectorSearchProvider> _logger;
    private readonly ConcurrentDictionary<string, AzureVectorSearchIndex> _indexCache = new(StringComparer.OrdinalIgnoreCase);

    public AzureVectorSearchProvider(IOptions<VectorSearchOptions> options, ILogger<AzureVectorSearchProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _azureOptions = options.Value.Azure ?? throw new InvalidOperationException("VectorSearch:Azure configuration is required.");

        if (string.IsNullOrWhiteSpace(_azureOptions.Endpoint))
        {
            throw new InvalidOperationException("VectorSearch:Azure:Endpoint must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_azureOptions.ApiKey))
        {
            throw new InvalidOperationException("VectorSearch:Azure:ApiKey must be configured.");
        }

        _indexClient = new AzureSearchDocuments.Indexes.SearchIndexClient(new Uri(_azureOptions.Endpoint), new AzureKeyCredential(_azureOptions.ApiKey));
    }

    public async Task DeleteIndexAsync(string indexName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        try
        {
            await _indexClient.DeleteIndexAsync(indexName, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Azure Search index {IndexName} not found during delete.", indexName);
        }

        _indexCache.TryRemove(indexName, out _);
    }

    public async Task<IVectorSearchIndex> GetOrCreateIndexAsync(VectorIndexDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (_indexCache.TryGetValue(definition.Name, out var cached))
        {
            EnsureDimensions(definition, cached.Dimensions);
            return cached;
        }

        var exists = await IndexExistsAsync(definition.Name, cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            var index = BuildIndex(definition);
            await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        var searchClient = _indexClient.GetSearchClient(definition.Name);
        var vectorIndex = new AzureVectorSearchIndex(searchClient, definition.Dimensions, _logger, definition.Name);
        _indexCache[definition.Name] = vectorIndex;
        return vectorIndex;
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(indexName);

        try
        {
            await _indexClient.GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private AzureIndexModels.SearchIndex BuildIndex(VectorIndexDefinition definition)
    {
        var vectorField = new AzureIndexModels.SearchField("embedding", AzureIndexModels.SearchFieldDataType.Collection(AzureIndexModels.SearchFieldDataType.Single))
        {
            IsSearchable = true,
            VectorSearchDimensions = definition.Dimensions,
            VectorSearchProfileName = "honua-profile"
        };

        var fields = new List<AzureIndexModels.SearchField>
        {
            new AzureIndexModels.SimpleField("id", AzureIndexModels.SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new AzureIndexModels.SearchableField("content") { AnalyzerName = AzureIndexModels.LexicalAnalyzerName.StandardLucene },
            new AzureIndexModels.SimpleField("humanApproved", AzureIndexModels.SearchFieldDataType.Boolean) { IsFilterable = true },
            new AzureIndexModels.SimpleField("patternType", AzureIndexModels.SearchFieldDataType.String) { IsFilterable = true },
            new AzureIndexModels.SimpleField("cloudProvider", AzureIndexModels.SearchFieldDataType.String) { IsFilterable = true },
            new AzureIndexModels.SimpleField("dataVolumeMin", AzureIndexModels.SearchFieldDataType.Int32) { IsFilterable = true },
            new AzureIndexModels.SimpleField("dataVolumeMax", AzureIndexModels.SearchFieldDataType.Int32) { IsFilterable = true },
            new AzureIndexModels.SimpleField("concurrentUsersMin", AzureIndexModels.SearchFieldDataType.Int32) { IsFilterable = true },
            new AzureIndexModels.SimpleField("concurrentUsersMax", AzureIndexModels.SearchFieldDataType.Int32) { IsFilterable = true },
            new AzureIndexModels.SimpleField("successRate", AzureIndexModels.SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
            new AzureIndexModels.SimpleField("deploymentCount", AzureIndexModels.SearchFieldDataType.Int32) { IsFilterable = true, IsSortable = true },
            new AzureIndexModels.SearchableField("configurationJson"),
            vectorField
        };

        var hnsw = new AzureIndexModels.HnswAlgorithmConfiguration("honua-hnsw")
        {
            Parameters = new AzureIndexModels.HnswParameters()
        };

        var vectorSearch = new AzureIndexModels.VectorSearch();
        vectorSearch.Algorithms.Add(hnsw);
        vectorSearch.Profiles.Add(new AzureIndexModels.VectorSearchProfile("honua-profile", hnsw.Name));

        var index = new AzureIndexModels.SearchIndex(definition.Name)
        {
            Fields = fields,
            VectorSearch = vectorSearch
        };

        return index;
    }

    private static void EnsureDimensions(VectorIndexDefinition definition, int existingDimensions)
    {
        if (definition.Dimensions != existingDimensions)
        {
            throw new InvalidOperationException($"Index '{definition.Name}' exists with dimensions {existingDimensions}, but {definition.Dimensions} were requested.");
        }
    }

    private sealed class AzureVectorSearchIndex : IVectorSearchIndex
    {
        private readonly AzureSearchDocuments.SearchClient _client;
        private readonly ILogger _logger;
        private readonly string _indexName;

        public AzureVectorSearchIndex(AzureSearchDocuments.SearchClient client, int dimensions, ILogger logger, string indexName)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _indexName = indexName;
            Dimensions = dimensions;
        }

        public int Dimensions { get; }

        public async Task UpsertAsync(IEnumerable<VectorSearchDocument> documents, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(documents);

            var payload = documents.Select(ToSearchDocument).ToList();
            if (payload.Count == 0)
            {
                return;
            }

            var batch = AzureSearchDocuments.Models.IndexDocumentsBatch.MergeOrUpload(payload);
            try
            {
                await _client.IndexDocumentsAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Search indexing failed for index {Index} with {Count} documents.", _indexName, payload.Count);
                throw;
            }
        }

        public async Task<IReadOnlyList<VectorSearchResult>> QueryAsync(VectorSearchQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var searchOptions = new AzureSearchDocuments.SearchOptions
            {
                Size = query.TopK,
                Select = { "id", "content", "cloudProvider", "dataVolumeMin", "dataVolumeMax", "concurrentUsersMin", "concurrentUsersMax", "successRate", "deploymentCount", "configurationJson" }
            };

            if (query.MetadataFilter is not null && query.MetadataFilter.Count > 0)
            {
                searchOptions.Filter = BuildFilter(query.MetadataFilter);
            }

            var vectorQuery = new AzureSearchModels.VectorizedQuery(query.Embedding.ToArray())
            {
                Fields = { "embedding" },
                KNearestNeighborsCount = query.TopK
            };

            searchOptions.VectorSearch = new AzureSearchModels.VectorSearchOptions
            {
                Queries = { vectorQuery }
            };

            var results = new List<VectorSearchResult>();
            try
            {
                var response = await _client.SearchAsync<AzureSearchModels.SearchDocument>(string.Empty, searchOptions, cancellationToken).ConfigureAwait(false);
                await foreach (var result in response.Value.GetResultsAsync().ConfigureAwait(false))
                {
                    results.Add(ConvertResult(result));
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Azure Search query failed for index {Index}.", _indexName);
                throw;
            }

            return results;
        }

        private AzureSearchModels.SearchDocument ToSearchDocument(VectorSearchDocument document)
        {
            if (document.Embedding.Length != Dimensions)
            {
                throw new InvalidOperationException($"Vector dimension mismatch. Expected {Dimensions}, received {document.Embedding.Length}.");
            }

            var searchDoc = new AzureSearchModels.SearchDocument
            {
                ["id"] = document.Id,
                ["content"] = document.Text ?? string.Empty,
                ["embedding"] = document.Embedding.ToArray(),
                ["humanApproved"] = true,
                ["patternType"] = "architecture"
            };

            if (document.Metadata is not null)
            {
                foreach (var kvp in document.Metadata)
                {
                    searchDoc[kvp.Key] = CoerceValue(kvp.Value);
                }
            }

            return searchDoc;
        }

        private VectorSearchResult ConvertResult(AzureSearchModels.SearchResult<AzureSearchModels.SearchDocument> result)
        {
            var document = result.Document;
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            void AddMetadata(string key)
            {
                if (document.TryGetValue(key, out var value) && value is not null)
                {
                    metadata[key] = ConvertToString(value);
                }
            }

            AddMetadata("cloudProvider");
            AddMetadata("dataVolumeMin");
            AddMetadata("dataVolumeMax");
            AddMetadata("concurrentUsersMin");
            AddMetadata("concurrentUsersMax");
            AddMetadata("successRate");
            AddMetadata("deploymentCount");
            AddMetadata("configurationJson");

            var vectorDocument = new VectorSearchDocument(
                document["id"].ToString()!,
                ReadOnlyMemory<float>.Empty,
                document.TryGetValue("content", out var content) ? content?.ToString() : null,
                metadata);

            return new VectorSearchResult(vectorDocument, result.Score ?? 0);
        }

        private static object CoerceValue(string value)
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                return d;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                return i;
            }

            if (bool.TryParse(value, out var b))
            {
                return b;
            }

            return value;
        }

        private static string? BuildFilter(IReadOnlyDictionary<string, string> filter)
        {
            if (filter.Count == 0)
            {
                return null;
            }

            static string Escape(string value) => value.Replace("'", "''");

            var clauses = filter.Select(kvp => $"{kvp.Key} eq '{Escape(kvp.Value)}'");
            return string.Join(" and ", clauses);
        }
        private static string ConvertToString(object value)
        {
            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString() ?? string.Empty,
                    JsonValueKind.Number => element.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => element.GetRawText()
                };
            }

            return value?.ToString() ?? string.Empty;
        }
    }
}
