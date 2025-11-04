// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Deployment pattern knowledge store backed by the pluggable vector search provider.
/// Works with the in-memory provider for local development and future vector backends.
/// </summary>
public sealed class VectorDeploymentPatternKnowledgeStore : IDeploymentPatternKnowledgeStore
{
    private readonly IVectorSearchProvider _vectorSearchProvider;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly VectorSearchOptions _options;
    private readonly ILogger<VectorDeploymentPatternKnowledgeStore> _logger;
    private readonly Lazy<Task<IVectorSearchIndex>> _indexFactory;

    private const string IndexNameDefault = "deployment-patterns";
    private const int DefaultTopK = 3;

    public VectorDeploymentPatternKnowledgeStore(
        IVectorSearchProvider vectorSearchProvider,
        IEmbeddingProvider embeddingProvider,
        IOptions<VectorSearchOptions> options,
        ILogger<VectorDeploymentPatternKnowledgeStore> logger)
    {
        _vectorSearchProvider = vectorSearchProvider ?? throw new ArgumentNullException(nameof(vectorSearchProvider));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _indexFactory = new Lazy<Task<IVectorSearchIndex>>(
            () => InitializeIndexAsync(CancellationToken.None),
            isThreadSafe: true);
    }

    public async Task IndexApprovedPatternAsync(DeploymentPattern pattern, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var index = await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        var embeddingText = DeploymentPatternTextGenerator.CreateEmbeddingText(pattern);

        var embedding = await GetEmbeddingAsync(embeddingText, cancellationToken).ConfigureAwait(false);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["patternName"] = pattern.Name,
            ["cloudProvider"] = NormalizeProviderString(pattern.CloudProvider),
            ["dataVolumeMin"] = pattern.DataVolumeMin.ToString(CultureInfo.InvariantCulture),
            ["dataVolumeMax"] = pattern.DataVolumeMax.ToString(CultureInfo.InvariantCulture),
            ["concurrentUsersMin"] = pattern.ConcurrentUsersMin.ToString(CultureInfo.InvariantCulture),
            ["concurrentUsersMax"] = pattern.ConcurrentUsersMax.ToString(CultureInfo.InvariantCulture),
            ["successRate"] = pattern.SuccessRate.ToString(CultureInfo.InvariantCulture),
            ["deploymentCount"] = pattern.DeploymentCount.ToString(CultureInfo.InvariantCulture),
            ["configurationJson"] = JsonSerializer.Serialize(pattern.Configuration),
            ["approvedBy"] = pattern.ApprovedBy,
            ["approvedDate"] = pattern.ApprovedDate.ToString("o", CultureInfo.InvariantCulture)
        };

        var document = new VectorSearchDocument(pattern.Id, embedding, embeddingText, metadata);
        await index.UpsertAsync(new[] { document }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Indexed deployment pattern {PatternId} ({PatternName}) via vector store.", pattern.Id, pattern.Name);
    }

    public async Task<List<PatternSearchResult>> SearchPatternsAsync(DeploymentRequirements requirements, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var index = await EnsureIndexAsync(cancellationToken).ConfigureAwait(false);
        var queryText = DeploymentPatternTextGenerator.CreateQueryText(requirements);
        var embedding = await GetEmbeddingAsync(queryText, cancellationToken).ConfigureAwait(false);

        var filter = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(requirements.CloudProvider))
        {
            filter["cloudProvider"] = NormalizeProviderString(requirements.CloudProvider);
        }

        var query = new VectorSearchQuery(embedding, TopK: DefaultTopK, MetadataFilter: filter);
        var results = await index.QueryAsync(query, cancellationToken).ConfigureAwait(false);

        return results
            .Select(r => MapResult(r))
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();
    }

    private async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var response = await _embeddingProvider.GetEmbeddingAsync(text, cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.ErrorMessage ?? "Embedding generation failed.");
        }

        return response.Embedding;
    }

    private async Task<IVectorSearchIndex> EnsureIndexAsync(CancellationToken cancellationToken)
    {
        // If already created, get the existing instance
        if (_indexFactory.IsValueCreated)
        {
            // The Lazy value was created with CancellationToken.None, but the instance is reusable
            // VSTHRD003: We're awaiting a task created elsewhere, but this is intentional
            // as we're using a cached factory pattern for index reuse
#pragma warning disable VSTHRD003
            return await _indexFactory.Value;
#pragma warning restore VSTHRD003
        }

        // If cancellation is required and the lazy hasn't been created yet, bypass it
        if (cancellationToken.CanBeCanceled)
        {
            return await InitializeIndexAsync(cancellationToken);
        }

        // Otherwise use the lazy factory
#pragma warning disable VSTHRD003
        return await _indexFactory.Value;
#pragma warning restore VSTHRD003
    }

    private async Task<IVectorSearchIndex> InitializeIndexAsync(CancellationToken cancellationToken)
    {
        var definition = new VectorIndexDefinition(GetIndexName(), _embeddingProvider.Dimensions);
        return await _vectorSearchProvider.GetOrCreateIndexAsync(definition, cancellationToken);
    }

    private string GetIndexName()
        => string.IsNullOrWhiteSpace(_options.IndexName) ? IndexNameDefault : _options.IndexName;

    private static PatternSearchResult? MapResult(VectorSearchResult result)
    {
        var metadata = result.Document.Metadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!metadata.TryGetValue("patternName", out var patternName))
        {
            return null;
        }

        if (!metadata.TryGetValue("configurationJson", out var configJson))
        {
            configJson = "{}";
        }
        metadata.TryGetValue("cloudProvider", out var cloudProvider);

        double successRate = TryParseDouble(metadata, "successRate");
        int deploymentCount = TryParseInt(metadata, "deploymentCount");

        return new PatternSearchResult
        {
            Id = result.Document.Id,
            PatternName = patternName,
            Content = result.Document.Text ?? string.Empty,
            ConfigurationJson = configJson ?? "{}",
            CloudProvider = cloudProvider ?? string.Empty,
            SuccessRate = successRate,
            DeploymentCount = deploymentCount,
            Score = result.Score
        };
    }

    private static double TryParseDouble(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static int TryParseInt(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;

    private static string NormalizeProviderString(string provider)
        => string.IsNullOrWhiteSpace(provider) ? string.Empty : provider.Trim().ToLowerInvariant();
}
