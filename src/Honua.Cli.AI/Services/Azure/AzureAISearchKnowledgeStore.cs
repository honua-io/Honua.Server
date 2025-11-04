// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.AI.Providers;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Azure;

/// <summary>
/// Azure AI Search-based knowledge store for deployment patterns.
/// Provides hybrid search (vector + keyword + filters) over human-approved patterns.
/// </summary>
public sealed class AzureAISearchKnowledgeStore : IDeploymentPatternKnowledgeStore
{
    private readonly IConfiguration _configuration;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<AzureAISearchKnowledgeStore> _logger;
    private readonly SearchClient _searchClient;
    private readonly string _indexName;

    public AzureAISearchKnowledgeStore(
        IConfiguration configuration,
        IEmbeddingProvider embeddingProvider,
        ILogger<AzureAISearchKnowledgeStore> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var endpoint = configuration["AzureAISearch:Endpoint"]
            ?? throw new InvalidOperationException("AzureAISearch:Endpoint not configured");

        var apiKey = configuration["AzureAISearch:ApiKey"]
            ?? throw new InvalidOperationException("AzureAISearch:ApiKey not configured");

        _indexName = configuration["AzureAISearch:IndexName"] ?? "deployment-knowledge";

        var credential = new AzureKeyCredential(apiKey);
        var indexClient = new SearchIndexClient(new Uri(endpoint), credential);
        _searchClient = indexClient.GetSearchClient(_indexName);

        _logger.LogInformation("Initialized Azure AI Search client - Endpoint: {Endpoint}, Index: {Index}",
            endpoint, _indexName);
    }

    /// <summary>
    /// Indexes a human-approved deployment pattern for semantic search.
    /// </summary>
    public async Task IndexApprovedPatternAsync(DeploymentPattern pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for semantic search
            var embeddingText = GenerateEmbeddingText(pattern);
            var embeddingResponse = await _embeddingProvider.GetEmbeddingAsync(embeddingText, cancellationToken);

            if (!embeddingResponse.Success)
            {
                throw new InvalidOperationException($"Failed to generate embedding: {embeddingResponse.ErrorMessage}");
            }

            var document = new SearchDocument
            {
                ["id"] = pattern.Id,
                ["content"] = embeddingText,
                ["contentVector"] = embeddingResponse.Embedding,
                ["patternType"] = "architecture",
                ["patternName"] = pattern.Name,
                ["cloudProvider"] = pattern.CloudProvider,
                ["dataVolumeMin"] = pattern.DataVolumeMin,
                ["dataVolumeMax"] = pattern.DataVolumeMax,
                ["concurrentUsersMin"] = pattern.ConcurrentUsersMin,
                ["concurrentUsersMax"] = pattern.ConcurrentUsersMax,
                ["successRate"] = pattern.SuccessRate,
                ["deploymentCount"] = pattern.DeploymentCount,
                ["configuration"] = JsonSerializer.Serialize(pattern.Configuration),
                ["humanApproved"] = true,
                ["approvedBy"] = pattern.ApprovedBy,
                ["approvedDate"] = pattern.ApprovedDate
            };

            await _searchClient.UploadDocumentsAsync(new[] { document }, cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Indexed pattern {PatternId} - {PatternName} (approved by {ApprovedBy})",
                pattern.Id, pattern.Name, pattern.ApprovedBy);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to index pattern {PatternId}", pattern.Id);
            throw;
        }
    }

    /// <summary>
    /// Searches for deployment patterns using hybrid search.
    /// </summary>
    public async Task<List<PatternSearchResult>> SearchPatternsAsync(
        DeploymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate query embedding
            var queryText = GenerateQueryText(requirements);
            var embeddingResponse = await _embeddingProvider.GetEmbeddingAsync(queryText, cancellationToken);

            if (!embeddingResponse.Success)
            {
                throw new InvalidOperationException($"Failed to generate query embedding: {embeddingResponse.ErrorMessage}");
            }

            var searchOptions = new SearchOptions
            {
                // Vector search
                VectorSearch = new()
                {
                    Queries =
                    {
                        new VectorizedQuery(embeddingResponse.Embedding.ToArray())
                        {
                            KNearestNeighborsCount = 5,
                            Fields = { "contentVector" }
                        }
                    }
                },

                // Metadata filters (only human-approved patterns in scope)
                Filter = BuildFilter(requirements),

                // Ranking: prefer high success rate, many deployments
                OrderBy = { "successRate desc", "deploymentCount desc" },

                // Return specific fields
                Select =
                {
                    "id", "patternName", "content", "configuration",
                    "successRate", "deploymentCount", "cloudProvider"
                },

                Size = 3, // Top 3 matches
                IncludeTotalCount = true
            };

            var response = await _searchClient.SearchAsync<SearchDocument>(
                queryText,
                searchOptions,
                cancellationToken);

            var matches = new List<PatternSearchResult>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                matches.Add(new PatternSearchResult
                {
                    Id = result.Document["id"].ToString()!,
                    PatternName = result.Document["patternName"].ToString()!,
                    Content = result.Document["content"].ToString()!,
                    ConfigurationJson = result.Document["configuration"].ToString()!,
                    SuccessRate = Convert.ToDouble(result.Document["successRate"]),
                    DeploymentCount = Convert.ToInt32(result.Document["deploymentCount"]),
                    CloudProvider = result.Document["cloudProvider"].ToString()!,
                    Score = result.Score ?? 0
                });
            }

            _logger.LogInformation(
                "Pattern search completed - Query: '{Query}', Matches: {Count}",
                queryText, matches.Count);

            return matches;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Pattern search failed - Status: {Status}", ex.Status);
            throw;
        }
    }

    private static string GenerateEmbeddingText(DeploymentPattern pattern)
        => DeploymentPatternTextGenerator.CreateEmbeddingText(pattern);

    private static string GenerateQueryText(DeploymentRequirements requirements)
        => DeploymentPatternTextGenerator.CreateQueryText(requirements);

    private static string BuildFilter(DeploymentRequirements requirements)
    {
        var filters = new List<string>
        {
            "humanApproved eq true",
            "patternType eq 'architecture'",
            $"cloudProvider eq '{requirements.CloudProvider}'",
            $"dataVolumeMin le {requirements.DataVolumeGb}",
            $"dataVolumeMax ge {requirements.DataVolumeGb}",
            $"concurrentUsersMin le {requirements.ConcurrentUsers}",
            $"concurrentUsersMax ge {requirements.ConcurrentUsers}"
        };

        return string.Join(" and ", filters);
    }
}

// Supporting types moved to Honua.Cli.AI.Services.VectorSearch namespace.
