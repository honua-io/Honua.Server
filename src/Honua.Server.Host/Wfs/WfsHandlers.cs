// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Authorization;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Main entry point and router for WFS (Web Feature Service) operations.
/// Delegates to specialized handlers for each operation type.
/// </summary>
internal static class WfsHandlers
{
    /// <summary>
    /// Main handler that routes WFS requests to appropriate specialized handlers.
    /// </summary>
    public static async Task<IResult> HandleAsync(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        [FromServices] IWfsLockManager lockManager,
        [FromServices] IFeatureEditOrchestrator editOrchestrator,
        [FromServices] IResourceAuthorizationService authorizationService,
        [FromServices] ISecurityAuditLogger auditLogger,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] ICsvExporter csvExporter,
        [FromServices] IShapefileExporter shapefileExporter,
        [FromServices] IWfsSchemaCache schemaCache,
        [FromServices] IOptions<WfsOptions> wfsOptions,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        var logger = loggerFactory.CreateLogger("Honua.Server.Host.Wfs.WfsHandlers");
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(catalog);
        Guard.NotNull(contextResolver);
        Guard.NotNull(repository);
        Guard.NotNull(lockManager);
        Guard.NotNull(editOrchestrator);
        Guard.NotNull(csvExporter);
        Guard.NotNull(shapefileExporter);
        Guard.NotNull(schemaCache);

        var request = context.Request;
        var query = request.Query;

        var serviceValue = QueryParsingHelpers.GetQueryValue(query, "service");
        if (!serviceValue.EqualsIgnoreCase("WFS"))
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "service", "Parameter 'service' must be set to 'WFS'.");
        }

        var requestValue = QueryParsingHelpers.GetQueryValue(query, "request");
        if (requestValue.IsNullOrWhiteSpace())
        {
            return WfsHelpers.CreateException("MissingParameterValue", "request", "Parameter 'request' is required.");
        }

        var metadataSnapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            switch (requestValue.ToUpperInvariant())
            {
                case "GETCAPABILITIES":
                    return await WfsCapabilitiesHandlers.HandleGetCapabilitiesAsync(request, metadataSnapshot, cancellationToken);

                case "DESCRIBEFEATURETYPE":
                    return await WfsCapabilitiesHandlers.HandleDescribeFeatureTypeAsync(request, query, catalog, contextResolver, schemaCache, cancellationToken);

                case "LISTSTOREDQUERIES":
                    return await WfsCapabilitiesHandlers.HandleListStoredQueriesAsync(request, metadataRegistry, cancellationToken);

                case "DESCRIBESTOREDQUERIES":
                    return await WfsCapabilitiesHandlers.HandleDescribeStoredQueriesAsync(request, query, metadataRegistry, cancellationToken);

                case "GETFEATURE":
                    return await WfsGetFeatureHandlers.HandleGetFeatureAsync(request, query, catalog, contextResolver, repository, metadataRegistry, csvExporter, shapefileExporter, cancellationToken);

                case "GETPROPERTYVALUE":
                    return await WfsGetFeatureHandlers.HandleGetPropertyValueAsync(request, query, catalog, contextResolver, repository, cancellationToken);

                case "GETFEATUREWITHLOCK":
                    return await WfsLockHandlers.HandleGetFeatureWithLockAsync(context, request, query, catalog, contextResolver, repository, lockManager, metadataRegistry, cancellationToken);

                case "LOCKFEATURE":
                    return await WfsLockHandlers.HandleLockFeatureAsync(context, request, query, catalog, contextResolver, repository, lockManager, metadataRegistry, cancellationToken);

                case "TRANSACTION":
                    return await WfsTransactionHandlers.HandleTransactionAsync(context, request, query, catalog, contextResolver, repository, lockManager, editOrchestrator, authorizationService, auditLogger, loggerFactory, wfsOptions, cancellationToken);

                default:
                    return WfsHelpers.CreateException("OperationNotSupported", "request", $"Request '{requestValue}' is not supported.");
            }
        }
        catch (InvalidOperationException ex)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "request", ex.Message);
        }
        catch (FormatException ex)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "request", ex.Message);
        }
    }
}
