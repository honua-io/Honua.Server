// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MetadataState = Honua.Cli.AI.Services.Processes.State.MetadataState;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Honua.Cli.AI.Services.Processes.Steps.Metadata;

/// <summary>
/// Publishes STAC Item to catalog and makes it searchable via OGC API.
/// </summary>
public class PublishStacStep : KernelProcessStep<MetadataState>
{
    private readonly ILogger<PublishStacStep> _logger;
    private readonly IHttpClientFactory? _httpClientFactory;
    private MetadataState _state = new();

    public PublishStacStep(ILogger<PublishStacStep> logger, IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<MetadataState> state)
    {
        _state = state.State ?? new MetadataState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("PublishStac")]
    public async Task PublishStacAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Publishing STAC Item for {DatasetName}", _state.DatasetName);

        _state.Status = "PublishingStac";

        try
        {
            // Publish to STAC catalog
            await PublishToCatalog();

            // Index for search
            await IndexForSearch();

            _state.Status = "Completed";
            _state.PublishedUrl = $"https://api.honua.io/collections/datasets/items/{_state.MetadataId}";

            _logger.LogInformation("STAC Item published at {Url}", _state.PublishedUrl);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "StacPublished",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish STAC Item for {DatasetName}", _state.DatasetName);
            _state.Status = "PublishFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "PublishFailed",
                Data = new { _state.DatasetName, Error = ex.Message }
            });
        }
    }

    private async Task PublishToCatalog()
    {
        _logger.LogInformation("Publishing STAC Item to catalog");

        // Validate that STAC Item was generated
        if (string.IsNullOrWhiteSpace(_state.StacItemJson))
        {
            var errorMsg = "Cannot publish STAC Item: STAC JSON is null or empty. STAC generation may have failed.";
            _logger.LogError(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        // Determine catalog endpoint from environment or configuration
        var catalogBaseUrl = Environment.GetEnvironmentVariable("STAC_CATALOG_URL")
            ?? Environment.GetEnvironmentVariable("HONUA_API_URL")
            ?? "http://localhost:5000";

        var collectionId = Environment.GetEnvironmentVariable("STAC_COLLECTION_ID") ?? "datasets";
        var publishUrl = $"{catalogBaseUrl}/collections/{collectionId}/items";

        _logger.LogInformation("Publishing to {Url}", publishUrl);

        try
        {
            HttpClient? httpClient = null;
            var shouldDispose = false;

            if (_httpClientFactory != null)
            {
                httpClient = _httpClientFactory.CreateClient();
            }
            else
            {
                httpClient = new HttpClient();
                shouldDispose = true;
            }

            try
            {
                // Add authentication headers if available
                AddAuthenticationHeaders(httpClient);

                // Prepare the STAC Item for publishing
                var content = new StringContent(_state.StacItemJson, Encoding.UTF8, "application/json");

                // POST to STAC catalog endpoint
                var response = await httpClient.PostAsync(publishUrl, content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogInformation("Successfully published STAC Item. Response: {Response}", responseBody);

                    // Try to extract the published URL from response
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("links", out var links))
                        {
                            foreach (var link in links.EnumerateArray())
                            {
                                if (link.TryGetProperty("rel", out var rel) && rel.GetString() == "self")
                                {
                                    if (link.TryGetProperty("href", out var href))
                                    {
                                        _state.PublishedUrl = href.GetString();
                                        break;
                                    }
                                }
                            }
                        }

                        // Fallback: construct URL from catalog endpoint
                        if (string.IsNullOrWhiteSpace(_state.PublishedUrl))
                        {
                            _state.PublishedUrl = $"{catalogBaseUrl}/collections/{collectionId}/items/{_state.MetadataId}";
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Could not parse STAC catalog response to extract published URL");
                        _state.PublishedUrl = $"{catalogBaseUrl}/collections/{collectionId}/items/{_state.MetadataId}";
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // Handle authentication errors specially
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError(
                            "STAC publishing failed with 401 Unauthorized. Authentication credentials may be missing or invalid.");
                        throw new HttpRequestException(
                            "STAC publishing failed due to authentication error. " +
                            "Ensure API key or bearer token is configured in metadata state or environment variables.");
                    }

                    _logger.LogError(
                        "Failed to publish STAC Item. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, errorBody);

                    throw new HttpRequestException(
                        $"Failed to publish STAC Item. Status: {response.StatusCode}, Response: {errorBody}");
                }
            }
            finally
            {
                if (shouldDispose)
                {
                    httpClient?.Dispose();
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed when publishing STAC Item to {Url}. This may be expected if the catalog is not running.", publishUrl);
            // Set a fallback URL even if publishing failed
            _state.PublishedUrl = $"{catalogBaseUrl}/collections/{collectionId}/items/{_state.MetadataId}";
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error publishing STAC Item to catalog");
            throw;
        }
    }

    private async Task IndexForSearch()
    {
        _logger.LogInformation("Indexing STAC Item for OGC API - Features search");

        // Determine search index endpoint
        var catalogBaseUrl = Environment.GetEnvironmentVariable("STAC_CATALOG_URL")
            ?? Environment.GetEnvironmentVariable("HONUA_API_URL")
            ?? "http://localhost:5000";

        var indexUrl = $"{catalogBaseUrl}/admin/search/index";

        _logger.LogInformation("Indexing at {Url}", indexUrl);

        try
        {
            HttpClient? httpClient = null;
            var shouldDispose = false;

            if (_httpClientFactory != null)
            {
                httpClient = _httpClientFactory.CreateClient();
            }
            else
            {
                httpClient = new HttpClient();
                shouldDispose = true;
            }

            try
            {
                // Add authentication headers if available
                AddAuthenticationHeaders(httpClient);

                // Prepare indexing request
                var indexRequest = new
                {
                    itemId = _state.MetadataId,
                    collectionId = Environment.GetEnvironmentVariable("STAC_COLLECTION_ID") ?? "datasets",
                    reindex = false
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(indexRequest),
                    Encoding.UTF8,
                    "application/json");

                // POST to search index endpoint
                var response = await httpClient.PostAsync(indexUrl, content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully indexed STAC Item for search");
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogWarning(
                        "Failed to index STAC Item. Status: {StatusCode}, Response: {Response}. Item is published but may not be immediately searchable.",
                        response.StatusCode, errorBody);

                    // Don't throw - indexing is optional, item is already published
                }
            }
            finally
            {
                if (shouldDispose)
                {
                    httpClient?.Dispose();
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed when indexing STAC Item. Item is published but may not be immediately searchable.");
            // Don't throw - indexing is optional
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error indexing STAC Item. Item is published but may not be immediately searchable.");
            // Don't throw - indexing is optional
        }
    }

    private void AddAuthenticationHeaders(HttpClient httpClient)
    {
        // Try to get authentication credentials from state first
        var apiKey = _state.ApiKey;
        var bearerToken = _state.BearerToken;

        // Fallback to environment variables if not in state
        if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(bearerToken))
        {
            apiKey = Environment.GetEnvironmentVariable("STAC_API_KEY")
                ?? Environment.GetEnvironmentVariable("HONUA_API_KEY");

            bearerToken = Environment.GetEnvironmentVariable("STAC_BEARER_TOKEN")
                ?? Environment.GetEnvironmentVariable("HONUA_BEARER_TOKEN");
        }

        // Add authentication header if credentials are available
        if (!string.IsNullOrEmpty(bearerToken))
        {
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {bearerToken}");
            _logger.LogInformation("Added Bearer token authentication to STAC HTTP client");
        }
        else if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
            _logger.LogInformation("Added API key authentication to STAC HTTP client");
        }
        else
        {
            _logger.LogWarning(
                "No authentication credentials found in metadata state or environment variables. " +
                "STAC publishing may fail on protected APIs. " +
                "Set ApiKey or BearerToken in metadata state, or STAC_API_KEY/STAC_BEARER_TOKEN/HONUA_API_KEY/HONUA_BEARER_TOKEN environment variables.");
        }
    }
}
