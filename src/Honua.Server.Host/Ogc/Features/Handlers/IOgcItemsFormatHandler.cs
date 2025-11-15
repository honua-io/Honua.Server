// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers;

/// <summary>
/// Defines the contract for handling OGC API Features items output in a specific format.
/// Implementations are responsible for validating requests, determining buffering requirements,
/// and producing HTTP responses in their designated format (e.g., GeoJSON, GeoPackage, KML).
/// </summary>
public interface IOgcItemsFormatHandler
{
    /// <summary>
    /// Gets the output format this handler supports.
    /// </summary>
    OgcSharedHandlers.OgcResponseFormat Format { get; }

    /// <summary>
    /// Validates whether this handler can process the given request parameters.
    /// </summary>
    /// <param name="query">The feature query containing pagination, filters, and other parameters.</param>
    /// <param name="requestedCrs">The requested coordinate reference system, or null for default.</param>
    /// <param name="context">Additional context information for validation.</param>
    /// <returns>A validation result indicating success or failure with error details.</returns>
    ValidationResult Validate(
        FeatureQuery query,
        string? requestedCrs,
        FormatContext context);

    /// <summary>
    /// Determines whether this handler requires all features to be buffered in memory
    /// before generating the response, or can stream features as they are retrieved.
    /// </summary>
    /// <param name="context">The format context containing metadata and configuration.</param>
    /// <returns>True if features must be buffered; false if streaming is supported.</returns>
    bool RequiresBuffering(FormatContext context);

    /// <summary>
    /// Handles the feature request and produces an HTTP response in this format.
    /// </summary>
    /// <param name="request">The format request containing all necessary data and dependencies.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>An HTTP result to be returned to the client.</returns>
    Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Encapsulates context information needed for format validation and processing decisions.
/// </summary>
/// <param name="Service">The service definition containing configuration and metadata.</param>
/// <param name="Layer">The layer definition containing schema and capabilities.</param>
/// <param name="CollectionId">The OGC API collection identifier.</param>
/// <param name="ExposeAttachments">Whether attachment links should be included in the response.</param>
/// <param name="SupportedCrs">List of coordinate reference systems supported by the layer.</param>
public sealed record FormatContext(
    ServiceDefinition Service,
    LayerDefinition Layer,
    string CollectionId,
    bool ExposeAttachments,
    IReadOnlyList<string> SupportedCrs);

/// <summary>
/// Encapsulates all data and dependencies needed to execute a format handler request.
/// </summary>
/// <param name="HttpRequest">The HTTP request object for building links and reading headers.</param>
/// <param name="Service">The service definition.</param>
/// <param name="Layer">The layer definition.</param>
/// <param name="CollectionId">The OGC API collection identifier.</param>
/// <param name="Query">The feature query with filters, pagination, and options.</param>
/// <param name="ContentCrs">The coordinate reference system for response content.</param>
/// <param name="ContentType">The MIME type for the response.</param>
/// <param name="NumberMatched">Total number of features matching the query, if known.</param>
/// <param name="Features">Async enumerable stream of feature records to be formatted.</param>
/// <param name="BufferedFeatures">Pre-loaded list of features, if buffering was required.</param>
/// <param name="Dependencies">Service dependencies (repositories, exporters, metrics, etc.).</param>
public sealed record FormatRequest(
    HttpRequest HttpRequest,
    ServiceDefinition Service,
    LayerDefinition Layer,
    string CollectionId,
    FeatureQuery Query,
    string ContentCrs,
    string ContentType,
    long? NumberMatched,
    IAsyncEnumerable<FeatureRecord>? Features,
    IReadOnlyList<object>? BufferedFeatures,
    FormatRequestDependencies Dependencies);

/// <summary>
/// Encapsulates service dependencies needed by format handlers.
/// This reduces the number of constructor parameters and makes it easier to add new dependencies.
/// </summary>
/// <param name="Repository">Feature repository for querying data.</param>
/// <param name="MetadataRegistry">Metadata registry for accessing styles and configurations.</param>
/// <param name="ApiMetrics">Metrics tracking service.</param>
/// <param name="CacheHeaderService">Cache header generation service.</param>
public sealed record FormatRequestDependencies(
    Core.Data.IFeatureRepository Repository,
    IMetadataRegistry MetadataRegistry,
    Core.Observability.IApiMetrics ApiMetrics,
    OgcCacheHeaderService CacheHeaderService);

/// <summary>
/// Represents the result of a format validation operation.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Gets a successful validation result.
    /// </summary>
    public static readonly ValidationResult Success = new();

    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    public bool IsValid => this.ErrorMessage == null && this.ProblemResult == null;

    /// <summary>
    /// Gets the error message if validation failed, or null if successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the parameter name that failed validation, or null if not parameter-specific.
    /// </summary>
    public string? ParameterName { get; init; }

    /// <summary>
    /// Gets a pre-built problem result for complex error responses, or null to use ErrorMessage.
    /// </summary>
    public IResult? ProblemResult { get; init; }

    /// <summary>
    /// Creates a validation failure with an error message and optional parameter name.
    /// </summary>
    /// <param name="errorMessage">The error message describing why validation failed.</param>
    /// <param name="parameterName">The parameter that failed validation, or null for general errors.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ValidationResult Failure(string errorMessage, string? parameterName = null)
    {
        return new ValidationResult
        {
            ErrorMessage = errorMessage,
            ParameterName = parameterName
        };
    }

    /// <summary>
    /// Creates a validation failure with a pre-built problem result.
    /// </summary>
    /// <param name="problemResult">The problem result to return to the client.</param>
    /// <returns>A validation result indicating failure.</returns>
    public static ValidationResult Failure(IResult problemResult)
    {
        return new ValidationResult
        {
            ProblemResult = problemResult
        };
    }
}
