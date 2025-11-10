// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure;
using Azure.Core;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.IoT.Azure.Services;

/// <summary>
/// Wrapper for Azure Digital Twins client that implements IAzureDigitalTwinsClient.
/// </summary>
public sealed class AzureDigitalTwinsClientWrapper : IAzureDigitalTwinsClient, IDisposable
{
    private readonly DigitalTwinsClient _client;
    private readonly ILogger<AzureDigitalTwinsClientWrapper> _logger;
    private readonly AzureDigitalTwinsOptions _options;

    public AzureDigitalTwinsClientWrapper(
        ILogger<AzureDigitalTwinsClientWrapper> logger,
        IOptions<AzureDigitalTwinsOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.InstanceUrl))
        {
            throw new InvalidOperationException(
                "Azure Digital Twins instance URL is not configured. " +
                "Please set AzureDigitalTwins:InstanceUrl in configuration.");
        }

        var credential = CreateCredential();
        var clientOptions = new DigitalTwinsClientOptions
        {
            Retry =
            {
                MaxRetries = _options.MaxRetryAttempts,
                Mode = RetryMode.Exponential
            }
        };

        _client = new DigitalTwinsClient(
            new Uri(_options.InstanceUrl),
            credential,
            clientOptions);

        _logger.LogInformation(
            "Azure Digital Twins client initialized for instance: {InstanceUrl}",
            _options.InstanceUrl);
    }

    public async Task<Response<BasicDigitalTwin>> CreateOrReplaceDigitalTwinAsync(
        string twinId,
        BasicDigitalTwin twin,
        ETag? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating or replacing twin: {TwinId}", twinId);

        try
        {
            var response = await _client.CreateOrReplaceDigitalTwinAsync(
                twinId,
                twin,
                ifNoneMatch,
                cancellationToken);

            _logger.LogInformation("Successfully created/replaced twin: {TwinId}", twinId);
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            _logger.LogWarning(ex, "Rate limit exceeded when creating/replacing twin: {TwinId}", twinId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/replacing twin: {TwinId}", twinId);
            throw;
        }
    }

    public async Task<Response<BasicDigitalTwin>> GetDigitalTwinAsync(
        string twinId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting twin: {TwinId}", twinId);

        try
        {
            return await _client.GetDigitalTwinAsync<BasicDigitalTwin>(twinId, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("Twin not found: {TwinId}", twinId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting twin: {TwinId}", twinId);
            throw;
        }
    }

    public async Task<Response<BasicDigitalTwin>> UpdateDigitalTwinAsync(
        string twinId,
        string jsonPatch,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating twin: {TwinId}", twinId);

        try
        {
            var response = await _client.UpdateDigitalTwinAsync(
                twinId,
                jsonPatch,
                ifMatch,
                cancellationToken);

            _logger.LogInformation("Successfully updated twin: {TwinId}", twinId);
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            _logger.LogWarning("Precondition failed (ETag mismatch) when updating twin: {TwinId}", twinId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating twin: {TwinId}", twinId);
            throw;
        }
    }

    public async Task<Response> DeleteDigitalTwinAsync(
        string twinId,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting twin: {TwinId}", twinId);

        try
        {
            var response = await _client.DeleteDigitalTwinAsync(twinId, ifMatch, cancellationToken);
            _logger.LogInformation("Successfully deleted twin: {TwinId}", twinId);
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Twin not found when attempting delete: {TwinId}", twinId);
            return Response.FromValue<object?>(null, ex.GetRawResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting twin: {TwinId}", twinId);
            throw;
        }
    }

    public AsyncPageable<BasicDigitalTwin> QueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing query: {Query}", query);
        return _client.QueryAsync<BasicDigitalTwin>(query, cancellationToken);
    }

    public async Task<Response<BasicRelationship>> CreateOrReplaceRelationshipAsync(
        string twinId,
        string relationshipId,
        BasicRelationship relationship,
        ETag? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Creating or replacing relationship: {RelationshipId} on twin: {TwinId}",
            relationshipId,
            twinId);

        try
        {
            var response = await _client.CreateOrReplaceRelationshipAsync(
                twinId,
                relationshipId,
                relationship,
                ifNoneMatch,
                cancellationToken);

            _logger.LogInformation(
                "Successfully created/replaced relationship: {RelationshipId} on twin: {TwinId}",
                relationshipId,
                twinId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating/replacing relationship: {RelationshipId} on twin: {TwinId}",
                relationshipId,
                twinId);
            throw;
        }
    }

    public async Task<Response<BasicRelationship>> GetRelationshipAsync(
        string twinId,
        string relationshipId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting relationship: {RelationshipId} on twin: {TwinId}", relationshipId, twinId);
        return await _client.GetRelationshipAsync<BasicRelationship>(
            twinId,
            relationshipId,
            cancellationToken);
    }

    public async Task<Response> DeleteRelationshipAsync(
        string twinId,
        string relationshipId,
        ETag? ifMatch = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting relationship: {RelationshipId} on twin: {TwinId}", relationshipId, twinId);

        try
        {
            var response = await _client.DeleteRelationshipAsync(
                twinId,
                relationshipId,
                ifMatch,
                cancellationToken);

            _logger.LogInformation(
                "Successfully deleted relationship: {RelationshipId} on twin: {TwinId}",
                relationshipId,
                twinId);

            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(
                "Relationship not found when attempting delete: {RelationshipId} on twin: {TwinId}",
                relationshipId,
                twinId);
            return Response.FromValue<object?>(null, ex.GetRawResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error deleting relationship: {RelationshipId} on twin: {TwinId}",
                relationshipId,
                twinId);
            throw;
        }
    }

    public AsyncPageable<BasicRelationship> GetRelationshipsAsync(
        string twinId,
        string? relationshipName = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting relationships for twin: {TwinId}", twinId);
        return _client.GetRelationshipsAsync<BasicRelationship>(
            twinId,
            relationshipName,
            cancellationToken);
    }

    public async Task<Response<DigitalTwinsModelData[]>> CreateModelsAsync(
        IEnumerable<string> models,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating models");

        try
        {
            var response = await _client.CreateModelsAsync(models, cancellationToken);
            _logger.LogInformation("Successfully created {Count} model(s)", response.Value.Length);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating models");
            throw;
        }
    }

    public async Task<Response<DigitalTwinsModelData>> GetModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting model: {ModelId}", modelId);
        return await _client.GetModelAsync(modelId, cancellationToken);
    }

    public AsyncPageable<DigitalTwinsModelData> GetModelsAsync(
        GetModelsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting models");
        return _client.GetModelsAsync(options, cancellationToken);
    }

    public async Task<Response> DeleteModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting model: {ModelId}", modelId);

        try
        {
            var response = await _client.DeleteModelAsync(modelId, cancellationToken);
            _logger.LogInformation("Successfully deleted model: {ModelId}", modelId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting model: {ModelId}", modelId);
            throw;
        }
    }

    public async Task<Response> DecommissionModelAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Decommissioning model: {ModelId}", modelId);

        try
        {
            var response = await _client.DecommissionModelAsync(modelId, cancellationToken);
            _logger.LogInformation("Successfully decommissioned model: {ModelId}", modelId);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decommissioning model: {ModelId}", modelId);
            throw;
        }
    }

    private TokenCredential CreateCredential()
    {
        if (_options.UseManagedIdentity)
        {
            _logger.LogInformation("Using Managed Identity for authentication");
            return new DefaultAzureCredential();
        }

        if (!string.IsNullOrWhiteSpace(_options.ClientId)
            && !string.IsNullOrWhiteSpace(_options.ClientSecret)
            && !string.IsNullOrWhiteSpace(_options.TenantId))
        {
            _logger.LogInformation(
                "Using Client Secret Credential for authentication (ClientId: {ClientId})",
                _options.ClientId);

            return new ClientSecretCredential(
                _options.TenantId,
                _options.ClientId,
                _options.ClientSecret);
        }

        _logger.LogInformation("Using Default Azure Credential for authentication");
        return new DefaultAzureCredential();
    }

    public void Dispose()
    {
        // DigitalTwinsClient doesn't implement IDisposable, but we keep this for future-proofing
    }
}
