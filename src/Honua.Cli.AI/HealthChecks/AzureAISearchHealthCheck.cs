// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.HealthChecks;

/// <summary>
/// Health check for Azure AI Search connectivity and index availability.
/// </summary>
public sealed class AzureAISearchHealthCheck : HealthCheckBase
{
    private readonly IConfiguration _configuration;

    public AzureAISearchHealthCheck(
        IConfiguration configuration,
        ILogger<AzureAISearchHealthCheck> logger)
        : base(logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    protected override async Task<HealthCheckResult> ExecuteHealthCheckAsync(
        Dictionary<string, object> data,
        CancellationToken cancellationToken)
    {
        var endpoint = _configuration["AzureAISearch:Endpoint"];
        var apiKey = _configuration["AzureAISearch:ApiKey"];
        var indexName = _configuration["AzureAISearch:IndexName"] ?? "deployment-knowledge";

        if (endpoint.IsNullOrWhiteSpace() || apiKey.IsNullOrWhiteSpace())
        {
            Logger.LogWarning("Azure AI Search not configured");
            return HealthCheckResult.Degraded(
                "Azure AI Search is not configured (endpoint or API key missing)");
        }

        // Create search client
        var credential = new AzureKeyCredential(apiKey);
        var indexClient = new Azure.Search.Documents.Indexes.SearchIndexClient(
            new Uri(endpoint),
            credential);

        // Try to get the search client for the index
        var searchClient = indexClient.GetSearchClient(indexName);

        // Perform a simple search to verify connectivity
        var searchOptions = new SearchOptions { Size = 1 };

        try
        {
            await searchClient.SearchAsync<Azure.Search.Documents.Models.SearchDocument>(
                "*",
                searchOptions,
                cancellationToken);

            data["endpoint"] = endpoint;
            data["index"] = indexName;

            Logger.LogDebug("Azure AI Search health check passed");
            return HealthCheckResult.Healthy("Azure AI Search is available", data);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            data["error"] = "Index not found";
            data["status"] = 404;

            Logger.LogWarning(ex, "Azure AI Search index not found");
            return HealthCheckResult.Degraded(
                "Azure AI Search index does not exist",
                ex,
                data);
        }
        catch (RequestFailedException ex) when (ex.Status == 401 || ex.Status == 403)
        {
            data["error"] = "Authentication failed";
            data["status"] = ex.Status;

            Logger.LogError(ex, "Azure AI Search authentication failed");
            return HealthCheckResult.Unhealthy(
                "Azure AI Search authentication failed",
                ex,
                data);
        }
    }
}
