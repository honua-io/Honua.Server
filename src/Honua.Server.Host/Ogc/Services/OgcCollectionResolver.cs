// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for resolving and validating OGC collection identifiers.
/// Extracted from OgcSharedHandlers to enable dependency injection and testability.
/// </summary>
internal sealed class OgcCollectionResolver : IOgcCollectionResolver
{
    private const string CollectionIdSeparator = "::";

    /// <inheritdoc />
    public async Task<Result<FeatureContext>> ResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        if (!TryParseCollectionId(collectionId, out var serviceId, out var layerId))
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        // Security: Validate inputs to prevent path traversal and injection attacks
        if (ContainsDangerousCharacters(serviceId) || ContainsDangerousCharacters(layerId))
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        FeatureContext context;
        try
        {
            context = await resolver.ResolveAsync(serviceId, layerId, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException ex)
        {
            var message = ex.Message.IsNullOrWhiteSpace()
                ? $"Collection '{collectionId}' was not found."
                : ex.Message;
            return Result<FeatureContext>.Failure(Error.NotFound(message));
        }
        catch (InvalidOperationException)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }
        catch (Exception)
        {
            // Catch all other exceptions and return NotFound to avoid exposing internal errors
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        if (context == null)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' was not found."));
        }

        if (!context.Service.Enabled || !context.Service.Ogc.CollectionsEnabled)
        {
            return Result<FeatureContext>.Failure(Error.NotFound($"Collection '{collectionId}' is not available."));
        }

        return Result<FeatureContext>.Success(context);
    }

    /// <inheritdoc />
    public async Task<(FeatureContext? Context, IResult? Error)> TryResolveCollectionAsync(
        string collectionId,
        IFeatureContextResolver resolver,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveCollectionAsync(collectionId, resolver, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return (null, MapCollectionResolutionError(resolution.Error!, collectionId));
        }

        return (resolution.Value, null);
    }

    /// <inheritdoc />
    public IResult MapCollectionResolutionError(Error error, string collectionId)
    {
        return error.Code switch
        {
            "not_found" => CreateNotFoundProblem(error.Message ?? $"Collection '{collectionId}' was not found."),
            "invalid" => Results.Problem(error.Message ?? "Collection resolution failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Collection resolution failed"),
            _ => Results.Problem(error.Message ?? "Collection resolution failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Collection resolution failed")
        };
    }

    /// <inheritdoc />
    public string BuildCollectionId(ServiceDefinition service, LayerDefinition layer)
        => $"{service.Id}{CollectionIdSeparator}{layer.Id}";

    private static bool TryParseCollectionId(string collectionId, out string serviceId, out string layerId)
    {
        serviceId = string.Empty;
        layerId = string.Empty;

        if (collectionId.IsNullOrWhiteSpace())
        {
            return false;
        }

        var parts = collectionId.Split(CollectionIdSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        serviceId = parts[0];
        layerId = parts[1];
        return true;
    }

    private static bool ContainsDangerousCharacters(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return false;
        }

        // Check for path traversal attempts
        if (value.Contains("..") || value.Contains("/") || value.Contains("\\"))
        {
            return true;
        }

        // Check for SQL injection attempts
        if (value.Contains("'") || value.Contains("--") || value.Contains(";"))
        {
            return true;
        }

        // Check for XML/HTML injection
        if (value.Contains("<") || value.Contains(">"))
        {
            return true;
        }

        return false;
    }

    private static IResult CreateNotFoundProblem(string detail)
    {
        return Results.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
    }
}
