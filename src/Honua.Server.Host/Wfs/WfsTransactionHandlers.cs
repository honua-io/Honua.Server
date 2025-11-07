// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Authorization;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Results;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Honua.Server.Host.Wfs.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Handlers for WFS transaction operations.
/// </summary>
internal static class WfsTransactionHandlers
{
    /// <summary>
    /// Handles Transaction requests with streaming XML parsing and batch processing.
    /// Enforces both role-based and resource-level authorization for all operations.
    /// </summary>
    /// <remarks>
    /// Authorization is performed in two stages:
    /// 1. Role-based check: User must have administrator or datapublisher role
    /// 2. Resource-level check: User must have edit permission on each specific layer/service being modified
    /// All transaction attempts (success and failure) are logged for audit purposes.
    /// </remarks>
    public static async Task<IResult> HandleTransactionAsync(
        HttpContext context,
        HttpRequest request,
        IQueryCollection query,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        [FromServices] IWfsLockManager lockManager,
        [FromServices] IFeatureEditOrchestrator orchestrator,
        [FromServices] IResourceAuthorizationService authorizationService,
        [FromServices] ISecurityAuditLogger auditLogger,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] IOptions<WfsOptions> wfsOptions,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Honua.Server.Host.Wfs.WfsTransactionHandlers");
        var options = wfsOptions.Value;

        // Create timeout cancellation token for transaction operations
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TransactionTimeoutSeconds));

        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.OgcProtocols,
            "WFS Transaction",
            new[] { ("wfs.operation", (object?)"Transaction") },
            async activity =>
            {
                // Stage 1: Role-based authorization check
                var user = context.User;
                var username = user.Identity?.Name ?? "anonymous";
                var ipAddress = context.Connection.RemoteIpAddress?.ToString();

                if (user.Identity?.IsAuthenticated != true ||
                    (!user.IsInRole("administrator") && !user.IsInRole("datapublisher")))
                {
                    auditLogger.LogUnauthorizedAccess(
                        username,
                        "WFS Transaction",
                        ipAddress,
                        "User does not have DataPublisher or Administrator role");

                    logger.LogWarning(
                        "WFS Transaction rejected: User {Username} from {IPAddress} does not have required role",
                        username, ipAddress ?? "unknown");

                    return WfsHelpers.CreateException("OperationNotSupported", "Transaction", "Transaction operations require DataPublisher role.");
                }

                // Choose parsing strategy based on configuration
                if (options.EnableStreamingTransactionParser)
                {
                    return await HandleTransactionWithStreamingParserAsync(
                        context, request, catalog, contextResolver, repository,
                        lockManager, orchestrator, authorizationService, auditLogger, logger, options, timeoutCts.Token).ConfigureAwait(false);
                }
                else
                {
                    return await HandleTransactionWithDomParserAsync(
                        context, request, catalog, contextResolver, repository,
                        lockManager, orchestrator, authorizationService, auditLogger, logger, options, timeoutCts.Token).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles Transaction using DOM-based XML parsing (legacy approach).
    /// Loads entire XML document into memory - suitable for small transactions only.
    /// </summary>
    private static async Task<IResult> HandleTransactionWithDomParserAsync(
        HttpContext context,
        HttpRequest request,
        ICatalogProjectionService catalog,
        IFeatureContextResolver contextResolver,
        IFeatureRepository repository,
        IWfsLockManager lockManager,
        IFeatureEditOrchestrator orchestrator,
        IResourceAuthorizationService authorizationService,
        ISecurityAuditLogger auditLogger,
        ILogger logger,
        WfsOptions options,
        CancellationToken cancellationToken)
    {
        // Load XML document (buffers entire payload in memory)
        XDocument document;
        try
        {
            SecureXmlSettings.ValidateStreamSize(request.Body);
            document = await SecureXmlSettings.LoadSecureAsync(
                request.Body,
                LoadOptions.None,
                cancellationToken).ConfigureAwait(false);
        }
        catch (XmlException ex)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "Transaction", $"Transaction payload is not valid XML: {ex.Message}");
        }
        catch (Exception ex) when (ex.Message.Contains("maximum allowed size"))
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "Transaction", $"Transaction payload too large: {ex.Message}");
        }

        var root = document.Root;
        if (root is null || root.Name != WfsConstants.Wfs + "Transaction")
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "Transaction", "Transaction payload must contain a wfs:Transaction element.");
        }

        var lockId = root.Attribute("lockId")?.Value;
        var releaseAction = root.Attribute("releaseAction")?.Value;
        var handle = root.Attribute("handle")?.Value;

        if (releaseAction.HasValue() &&
            !releaseAction.EqualsIgnoreCase("ALL") &&
            !releaseAction.EqualsIgnoreCase("SOME"))
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "releaseAction", "releaseAction must be ALL or SOME.");
        }

        var commandInfos = new List<(FeatureEditCommand Command, string? FallbackId)>();

        // Parse Insert operations
        foreach (var insertElement in root.Elements(WfsConstants.Wfs + "Insert"))
        {
            foreach (var featureElement in insertElement.Elements())
            {
                var typeName = ResolveTypeName(featureElement);
                if (typeName.IsNullOrWhiteSpace())
                {
                    return WfsHelpers.CreateException("InvalidParameterValue", "typeNames", "Insert element must include a namespaced feature type.");
                }

                var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
                if (contextResult.IsFailure)
                {
                    return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
                }

                var featureContext = contextResult.Value;
                var attributes = ParseFeatureAttributes(featureElement, featureContext.Layer);
                var fallbackId = WfsHelpers.TryExtractIdValue(attributes, featureContext.Layer.IdField);
                commandInfos.Add((new AddFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, attributes), fallbackId));
            }
        }

        // Parse Update operations
        foreach (var updateElement in root.Elements(WfsConstants.Wfs + "Update"))
        {
            var typeNameAttribute = updateElement.Attribute("typeName")?.Value ?? updateElement.Attribute(XName.Get("typeName"))?.Value;
            var typeName = ResolveTypeName(updateElement, typeNameAttribute);
            if (typeName.IsNullOrWhiteSpace())
            {
                return WfsHelpers.CreateException("InvalidParameterValue", "typeName", "Update element requires a typeName attribute.");
            }

            var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
            if (contextResult.IsFailure)
            {
                return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
            }

            var featureContext = contextResult.Value;
            var attributes = ParseUpdateProperties(updateElement, featureContext.Layer);
            if (attributes.Count == 0)
            {
                return WfsHelpers.CreateException("InvalidParameterValue", "Property", "Update element must include at least one property.");
            }

            var targetResult = await ResolveTransactionTargetIdsAsync(updateElement, featureContext, repository, cancellationToken).ConfigureAwait(false);
            if (targetResult.IsFailure)
            {
                var error = targetResult.Error!;
                return WfsHelpers.CreateException("InvalidParameterValue", "Filter", error.Message);
            }

            foreach (var featureId in targetResult.Value)
            {
                commandInfos.Add((new UpdateFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, featureId, attributes), featureId));
            }
        }

        // Parse Delete operations
        foreach (var deleteElement in root.Elements(WfsConstants.Wfs + "Delete"))
        {
            var typeNameAttribute = deleteElement.Attribute("typeName")?.Value ?? deleteElement.Attribute(XName.Get("typeName"))?.Value;
            var typeName = ResolveTypeName(deleteElement, typeNameAttribute);
            if (typeName.IsNullOrWhiteSpace())
            {
                return WfsHelpers.CreateException("InvalidParameterValue", "typeName", "Delete element requires a typeName attribute.");
            }

            var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
            if (contextResult.IsFailure)
            {
                return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
            }

            var featureContext = contextResult.Value;
            var targetResult = await ResolveTransactionTargetIdsAsync(deleteElement, featureContext, repository, cancellationToken).ConfigureAwait(false);
            if (targetResult.IsFailure)
            {
                var error = targetResult.Error!;
                return WfsHelpers.CreateException("InvalidParameterValue", "Filter", error.Message);
            }

            foreach (var featureId in targetResult.Value)
            {
                commandInfos.Add((new DeleteFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, featureId), featureId));
            }
        }

        // Parse Replace operations (WFS 2.0)
        foreach (var replaceElement in root.Elements(WfsConstants.Wfs + "Replace"))
        {
            var typeNameAttribute = replaceElement.Attribute("typeName")?.Value ?? replaceElement.Attribute(XName.Get("typeName"))?.Value;

            // For Replace, we first need to find the feature element child
            var featureElement = replaceElement.Elements().FirstOrDefault(e => e.Name.NamespaceName != WfsConstants.Wfs.NamespaceName);
            if (featureElement is null)
            {
                return WfsHelpers.CreateException("InvalidParameterValue", "Replace", "Replace element must contain a feature.");
            }

            var typeName = typeNameAttribute.HasValue()
                ? ResolveTypeName(replaceElement, typeNameAttribute)
                : ResolveTypeName(featureElement);

            if (typeName.IsNullOrWhiteSpace())
            {
                return WfsHelpers.CreateException("InvalidParameterValue", "typeName", "Replace element requires a typeName attribute or namespaced feature.");
            }

            var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
            if (contextResult.IsFailure)
            {
                return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
            }

            var featureContext = contextResult.Value;

            // Replace requires a Filter to identify the feature(s) to replace
            var targetResult = await ResolveTransactionTargetIdsAsync(replaceElement, featureContext, repository, cancellationToken).ConfigureAwait(false);
            if (targetResult.IsFailure)
            {
                var error = targetResult.Error!;
                return WfsHelpers.CreateException("InvalidParameterValue", "Filter", error.Message);
            }

            // Parse the new feature attributes
            var attributes = ParseFeatureAttributes(featureElement, featureContext.Layer);

            foreach (var featureId in targetResult.Value)
            {
                commandInfos.Add((new ReplaceFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, featureId, attributes), featureId));
            }
        }

        if (commandInfos.Count == 0)
        {
            return WfsHelpers.CreateException("MissingParameterValue", "Transaction", "Transaction request did not contain any operations.");
        }

        // Check transaction size limit
        if (commandInfos.Count > options.MaxTransactionFeatures)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "Transaction",
                $"Transaction contains {commandInfos.Count} operations, exceeding the maximum of {options.MaxTransactionFeatures}. " +
                $"Configure honua:wfs:MaxTransactionFeatures to increase this limit.");
        }

        // Stage 2: Resource-level authorization check
        var authResult = await ValidateResourceAuthorizationAsync(
            context.User, commandInfos, authorizationService, auditLogger, logger, cancellationToken).ConfigureAwait(false);

        if (!authResult.IsAuthorized)
        {
            return authResult.ErrorResult!;
        }

        return await ExecuteTransactionAsync(
            context, lockId, releaseAction, handle, commandInfos,
            lockManager, orchestrator, auditLogger, logger, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles Transaction using streaming XML parsing (optimized for large transactions).
    /// Parses XML incrementally without loading entire document into memory.
    /// </summary>
    private static async Task<IResult> HandleTransactionWithStreamingParserAsync(
        HttpContext context,
        HttpRequest request,
        ICatalogProjectionService catalog,
        IFeatureContextResolver contextResolver,
        IFeatureRepository repository,
        IWfsLockManager lockManager,
        IFeatureEditOrchestrator orchestrator,
        IResourceAuthorizationService authorizationService,
        ISecurityAuditLogger auditLogger,
        ILogger logger,
        WfsOptions options,
        CancellationToken cancellationToken)
    {
        // Parse transaction using streaming XML reader
        var parseResult = await WfsStreamingTransactionParser.ParseTransactionStreamAsync(
            request.Body,
            options.MaxTransactionFeatures,
            cancellationToken).ConfigureAwait(false);

        if (parseResult.IsFailure)
        {
            var error = parseResult.Error!;
            return WfsHelpers.CreateException("InvalidParameterValue", "Transaction", error.Message);
        }

        var (metadata, operations) = parseResult.Value;
        var commandInfos = new List<(FeatureEditCommand Command, string? FallbackId)>();

        // Process operations in batches to limit memory usage
        var batchSize = options.TransactionBatchSize;
        var operationCount = 0;

        await foreach (var operation in operations.WithCancellation(cancellationToken))
        {
            operationCount++;

            // Process based on operation type
            switch (operation.Type)
            {
                case WfsStreamingTransactionParser.WfsTransactionOperationType.Insert:
                    foreach (var featureElement in WfsStreamingTransactionParser.ExtractInsertFeatures(operation.Element))
                    {
                        var typeName = ResolveTypeName(featureElement);
                        if (typeName.IsNullOrWhiteSpace())
                        {
                            return WfsHelpers.CreateException("InvalidParameterValue", "typeNames", "Insert element must include a namespaced feature type.");
                        }

                        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
                        if (contextResult.IsFailure)
                        {
                            return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
                        }

                        var featureContext = contextResult.Value;
                        var attributes = ParseFeatureAttributes(featureElement, featureContext.Layer);
                        var fallbackId = WfsHelpers.TryExtractIdValue(attributes, featureContext.Layer.IdField);
                        commandInfos.Add((new AddFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, attributes), fallbackId));
                    }
                    break;

                case WfsStreamingTransactionParser.WfsTransactionOperationType.Update:
                    {
                        var updateElement = operation.Element;
                        var typeName = operation.TypeName;

                        if (typeName.IsNullOrWhiteSpace())
                        {
                            typeName = ResolveTypeName(updateElement, updateElement.Attribute("typeName")?.Value);
                        }

                        if (typeName.IsNullOrWhiteSpace())
                        {
                            return WfsHelpers.CreateException("InvalidParameterValue", "typeName", "Update element requires a typeName attribute.");
                        }

                        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
                        if (contextResult.IsFailure)
                        {
                            return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
                        }

                        var featureContext = contextResult.Value;
                        var attributes = ParseUpdateProperties(updateElement, featureContext.Layer);
                        if (attributes.Count == 0)
                        {
                            return WfsHelpers.CreateException("InvalidParameterValue", "Property", "Update element must include at least one property.");
                        }

                        var targetResult = await ResolveTransactionTargetIdsAsync(updateElement, featureContext, repository, cancellationToken).ConfigureAwait(false);
                        if (targetResult.IsFailure)
                        {
                            var error = targetResult.Error!;
                            return WfsHelpers.CreateException("InvalidParameterValue", "Filter", error.Message);
                        }

                        foreach (var featureId in targetResult.Value)
                        {
                            commandInfos.Add((new UpdateFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, featureId, attributes), featureId));
                        }
                    }
                    break;

                case WfsStreamingTransactionParser.WfsTransactionOperationType.Delete:
                    {
                        var deleteElement = operation.Element;
                        var typeName = operation.TypeName;

                        if (typeName.IsNullOrWhiteSpace())
                        {
                            typeName = ResolveTypeName(deleteElement, deleteElement.Attribute("typeName")?.Value);
                        }

                        if (typeName.IsNullOrWhiteSpace())
                        {
                            return WfsHelpers.CreateException("InvalidParameterValue", "typeName", "Delete element requires a typeName attribute.");
                        }

                        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
                        if (contextResult.IsFailure)
                        {
                            return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
                        }

                        var featureContext = contextResult.Value;
                        var targetResult = await ResolveTransactionTargetIdsAsync(deleteElement, featureContext, repository, cancellationToken).ConfigureAwait(false);
                        if (targetResult.IsFailure)
                        {
                            var error = targetResult.Error!;
                            return WfsHelpers.CreateException("InvalidParameterValue", "Filter", error.Message);
                        }

                        foreach (var featureId in targetResult.Value)
                        {
                            commandInfos.Add((new DeleteFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, featureId), featureId));
                        }
                    }
                    break;

                case WfsStreamingTransactionParser.WfsTransactionOperationType.Replace:
                    {
                        var replaceElement = operation.Element;
                        var typeName = operation.TypeName;

                        // For Replace, we first need to find the feature element child
                        var featureElement = replaceElement.Elements().FirstOrDefault(e => e.Name.NamespaceName != WfsConstants.Wfs.NamespaceName);
                        if (featureElement is null)
                        {
                            return WfsHelpers.CreateException("InvalidParameterValue", "Replace", "Replace element must contain a feature.");
                        }

                        if (typeName.IsNullOrWhiteSpace())
                        {
                            typeName = ResolveTypeName(featureElement);
                        }

                        if (typeName.IsNullOrWhiteSpace())
                        {
                            return WfsHelpers.CreateException("InvalidParameterValue", "typeName", "Replace element requires a typeName attribute or namespaced feature.");
                        }

                        var contextResult = await WfsHelpers.ResolveLayerContextAsync(typeName!, catalog, contextResolver, cancellationToken).ConfigureAwait(false);
                        if (contextResult.IsFailure)
                        {
                            return WfsHelpers.MapResolutionError(contextResult.Error!, typeName!);
                        }

                        var featureContext = contextResult.Value;

                        // Replace requires a Filter to identify the feature(s) to replace
                        var targetResult = await ResolveTransactionTargetIdsAsync(replaceElement, featureContext, repository, cancellationToken).ConfigureAwait(false);
                        if (targetResult.IsFailure)
                        {
                            var error = targetResult.Error!;
                            return WfsHelpers.CreateException("InvalidParameterValue", "Filter", error.Message);
                        }

                        // Parse the new feature attributes
                        var attributes = ParseFeatureAttributes(featureElement, featureContext.Layer);

                        foreach (var featureId in targetResult.Value)
                        {
                            commandInfos.Add((new ReplaceFeatureCommand(featureContext.Service.Id, featureContext.Layer.Id, featureId, attributes), featureId));
                        }
                    }
                    break;
            }
        }

        if (commandInfos.Count == 0)
        {
            return WfsHelpers.CreateException("MissingParameterValue", "Transaction", "Transaction request did not contain any operations.");
        }

        // Stage 2: Resource-level authorization check
        var authResult = await ValidateResourceAuthorizationAsync(
            context.User, commandInfos, authorizationService, auditLogger, logger, cancellationToken).ConfigureAwait(false);

        if (!authResult.IsAuthorized)
        {
            return authResult.ErrorResult!;
        }

        return await ExecuteTransactionAsync(
            context, metadata.LockId, metadata.ReleaseAction, metadata.Handle, commandInfos,
            lockManager, orchestrator, auditLogger, logger, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the parsed transaction commands and returns the response.
    /// Shared by both DOM and streaming parsers.
    /// </summary>
    private static async Task<IResult> ExecuteTransactionAsync(
        HttpContext context,
        string? lockId,
        string? releaseAction,
        string? handle,
        List<(FeatureEditCommand Command, string? FallbackId)> commandInfos,
        IWfsLockManager lockManager,
        IFeatureEditOrchestrator orchestrator,
        ISecurityAuditLogger auditLogger,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Validate lock ownership if lockId is provided
        var lockTargets = CollectTransactionTargets(commandInfos);
        if (lockTargets.Count > 0)
        {
            var validation = await lockManager.ValidateAsync(lockId, lockTargets, cancellationToken).ConfigureAwait(false);
            if (!validation.Success)
            {
                return WfsHelpers.CreateException("OperationProcessingFailed", "Transaction", validation.ErrorMessage ?? "Locked features are not available for editing.");
            }
        }

        // Execute transaction batch with ACID semantics
        var batch = new FeatureEditBatch(
            commands: commandInfos.Select(ci => ci.Command).ToArray(),
            rollbackOnFailure: true,
            clientReference: handle,
            isAuthenticated: context.User?.Identity?.IsAuthenticated ?? false,
            userRoles: UserIdentityHelper.ExtractUserRoles(context.User));

        var editResult = await orchestrator.ExecuteAsync(batch, cancellationToken).ConfigureAwait(false);
        if (editResult.Results.Count != commandInfos.Count)
        {
            return WfsHelpers.CreateException("NoApplicableCode", "Transaction", "Unexpected response from edit pipeline.");
        }

        var entries = commandInfos
            .Select((info, index) => new TransactionEntry(info.Command, info.FallbackId, editResult.Results[index]))
            .ToArray();

        // Check for failures
        var failure = entries.FirstOrDefault(entry => !entry.Result.Success);
        if (failure is not null)
        {
            var message = failure.Result.Error?.Message ?? "Transaction failed.";

            // Log transaction failure
            var username = context.User?.Identity?.Name ?? "anonymous";
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            logger.LogWarning(
                "WFS Transaction failed for user {Username} from {IPAddress}: {ErrorMessage}. Operations: {OperationCount}",
                username, ipAddress ?? "unknown", message, commandInfos.Count);

            return WfsHelpers.CreateException("NoApplicableCode", "Transaction", message);
        }

        // Log successful transaction
        LogSuccessfulTransaction(context, commandInfos, auditLogger, logger);

        // Release locks if requested
        if (lockId.HasValue())
        {
            IReadOnlyList<WfsLockTarget>? releaseTargets;

            if (releaseAction.EqualsIgnoreCase("ALL"))
            {
                releaseTargets = null;
            }
            else if (releaseAction.EqualsIgnoreCase("SOME"))
            {
                releaseTargets = Array.Empty<WfsLockTarget>();
            }
            else
            {
                releaseTargets = CollectSuccessfulTransactionTargets(entries);
            }

            var currentUser = WfsHelpers.ResolveLockOwner(context);
            await lockManager.ReleaseAsync(currentUser, lockId!, releaseTargets, cancellationToken).ConfigureAwait(false);
        }

        return CreateTransactionResponse(entries);
    }

    #region Private Helper Methods

    private static string? ResolveTypeName(XElement featureElement)
    {
        var ns = featureElement.Name.NamespaceName;
        var serviceId = WfsHelpers.ExtractServiceIdFromNamespace(ns);
        var layerId = featureElement.Name.LocalName;
        return serviceId.IsNullOrWhiteSpace() ? layerId : $"{serviceId}:{layerId}";
    }

    private static string? ResolveTypeName(XElement element, string? typeNameAttribute)
    {
        if (typeNameAttribute.IsNullOrWhiteSpace())
        {
            return null;
        }

        var raw = typeNameAttribute.Trim();
        if (!raw.Contains(':', StringComparison.Ordinal))
        {
            return raw;
        }

        var parts = raw.Split(':');
        if (parts.Length != 2)
        {
            return raw;
        }

        var prefix = parts[0];
        var local = parts[1];
        var ns = element.GetNamespaceOfPrefix(prefix);
        if (ns is null)
        {
            return raw;
        }

        var serviceId = WfsHelpers.ExtractServiceIdFromNamespace(ns.NamespaceName);
        return serviceId.IsNullOrWhiteSpace() ? raw : $"{serviceId}:{local}";
    }

    private static Dictionary<string, object?> ParseFeatureAttributes(XElement featureElement, LayerDefinition layer)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in featureElement.Elements())
        {
            var name = property.Name.LocalName;
            var geometry = WfsHelpers.TryParseGeometryElement(property);
            if (geometry is not null)
            {
                attributes[name] = geometry;
                continue;
            }

            if (name.EqualsIgnoreCase(layer.GeometryField) && property.IsEmpty)
            {
                attributes[name] = null;
                continue;
            }

            attributes[name] = WfsHelpers.ParseLiteral(property.Value);
        }

        return attributes;
    }

    private static Dictionary<string, object?> ParseUpdateProperties(XElement updateElement, LayerDefinition layer)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in updateElement.Elements(WfsConstants.Wfs + "Property"))
        {
            var nameElement = property.Element(WfsConstants.Wfs + "Name");
            var valueElement = property.Element(WfsConstants.Wfs + "Value");
            if (nameElement is null)
            {
                continue;
            }

            var propertyName = nameElement.Value?.Trim();
            if (propertyName.IsNullOrWhiteSpace())
            {
                continue;
            }

            if (propertyName.EqualsIgnoreCase(layer.IdField))
            {
                continue;
            }

            if (valueElement is not null)
            {
                var geometry = WfsHelpers.TryParseGeometryElement(valueElement);
                if (geometry is not null)
                {
                    attributes[propertyName] = geometry;
                    continue;
                }

                attributes[propertyName] = WfsHelpers.ParseLiteral(valueElement.Value);
            }
            else
            {
                attributes[propertyName] = null;
            }
        }

        return attributes;
    }

    private static string? ResolveResourceId(XElement element)
    {
        var filter = element.Descendants(WfsConstants.Fes + "ResourceId").FirstOrDefault();
        var rid = filter?.Attribute("rid")?.Value;
        if (rid.IsNullOrWhiteSpace())
        {
            return null;
        }

        rid = rid.Trim();
        var dotIndex = rid.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex < rid.Length - 1)
        {
            return rid[(dotIndex + 1)..];
        }

        var colonIndex = rid.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex < rid.Length - 1)
        {
            return rid[(colonIndex + 1)..];
        }

        return rid;
    }

    private static async Task<Result<IReadOnlyList<string>>> ResolveTransactionTargetIdsAsync(
        XElement element,
        FeatureContext featureContext,
        IFeatureRepository repository,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(element);
        Guard.NotNull(featureContext);
        Guard.NotNull(repository);

        var resourceId = ResolveResourceId(element);
        if (resourceId.HasValue())
        {
            return Result<IReadOnlyList<string>>.Success(new[] { resourceId! });
        }

        var filterElement = element.Elements().FirstOrDefault(child => child.Name.LocalName.EqualsIgnoreCase("Filter"));
        if (filterElement is null)
        {
            return Result<IReadOnlyList<string>>.Failure(Error.Invalid("Transaction operation must include a filter when ResourceId is not provided."));
        }

        QueryFilter filter;
        try
        {
            var filterXml = filterElement.ToString(SaveOptions.DisableFormatting);
            filter = XmlFilterParser.Parse(filterXml, featureContext.Layer);
        }
        catch (Exception ex) when (ex is InvalidOperationException or XmlException)
        {
            return Result<IReadOnlyList<string>>.Failure(Error.Invalid(ex.Message));
        }

        var query = new FeatureQuery(
            Filter: filter,
            ResultType: FeatureResultType.Results);

        var identifiers = new List<string>();

        await foreach (var record in repository.QueryAsync(featureContext.Service.Id, featureContext.Layer.Id, query, cancellationToken).ConfigureAwait(false))
        {
            if (!record.Attributes.TryGetValue(featureContext.Layer.IdField, out var value) || value is null)
            {
                continue;
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text.HasValue())
            {
                identifiers.Add(text);
            }
        }

        if (identifiers.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Failure(Error.NotFound("Filter did not match any features."));
        }

        return Result<IReadOnlyList<string>>.Success(identifiers);
    }

    private static IReadOnlyList<WfsLockTarget> CollectTransactionTargets(IEnumerable<(FeatureEditCommand Command, string? FallbackId)> commandInfos)
    {
        var targets = new List<WfsLockTarget>();
        foreach (var entry in commandInfos)
        {
            switch (entry.Command)
            {
                case UpdateFeatureCommand update:
                    targets.Add(new WfsLockTarget(update.ServiceId, update.LayerId, update.FeatureId));
                    break;
                case ReplaceFeatureCommand replace:
                    targets.Add(new WfsLockTarget(replace.ServiceId, replace.LayerId, replace.FeatureId));
                    break;
                case DeleteFeatureCommand delete:
                    targets.Add(new WfsLockTarget(delete.ServiceId, delete.LayerId, delete.FeatureId));
                    break;
            }
        }

        return targets;
    }

    private static IReadOnlyList<WfsLockTarget> CollectSuccessfulTransactionTargets(IEnumerable<TransactionEntry> entries)
    {
        var targets = new List<WfsLockTarget>();
        foreach (var entry in entries)
        {
            if (!entry.Result.Success)
            {
                continue;
            }

            switch (entry.Command)
            {
                case UpdateFeatureCommand update:
                    targets.Add(new WfsLockTarget(update.ServiceId, update.LayerId, update.FeatureId));
                    break;
                case ReplaceFeatureCommand replace:
                    targets.Add(new WfsLockTarget(replace.ServiceId, replace.LayerId, replace.FeatureId));
                    break;
                case DeleteFeatureCommand delete:
                    targets.Add(new WfsLockTarget(delete.ServiceId, delete.LayerId, delete.FeatureId));
                    break;
            }
        }

        return targets;
    }

    private static IResult CreateTransactionResponse(IReadOnlyList<TransactionEntry> entries)
    {
        var inserted = 0;
        var updated = 0;
        var replaced = 0;
        var deleted = 0;
        var insertResults = new List<XElement>();

        foreach (var entry in entries)
        {
            switch (entry.Command.Operation)
            {
                case FeatureEditOperation.Add:
                    inserted++;
                    var addCommand = (AddFeatureCommand)entry.Command;
                    var newId = entry.Result.FeatureId ?? entry.FallbackId;
                    if (newId.HasValue())
                    {
                        var rid = $"{addCommand.ServiceId}:{addCommand.LayerId}.{newId}";
                        insertResults.Add(new XElement(WfsConstants.Wfs + "Feature",
                            new XElement(WfsConstants.Wfs + "ResourceId", new XAttribute("rid", rid))));
                    }

                    break;
                case FeatureEditOperation.Update:
                    updated++;
                    break;
                case FeatureEditOperation.Replace:
                    replaced++;
                    break;
                case FeatureEditOperation.Delete:
                    deleted++;
                    break;
            }
        }

        var response = new XElement(WfsConstants.Wfs + "TransactionResponse",
            new XAttribute(XNamespace.Xmlns + "wfs", WfsConstants.Wfs),
            new XAttribute(XNamespace.Xmlns + "ows", WfsConstants.Ows),
            new XAttribute("version", "2.0.0"),
            new XElement(WfsConstants.Wfs + "TransactionSummary",
                new XElement(WfsConstants.Wfs + "totalInserted", inserted),
                new XElement(WfsConstants.Wfs + "totalUpdated", updated),
                new XElement(WfsConstants.Wfs + "totalReplaced", replaced),
                new XElement(WfsConstants.Wfs + "totalDeleted", deleted)));

        if (insertResults.Count > 0)
        {
            response.Add(new XElement(WfsConstants.Wfs + "InsertResults", insertResults));
        }

        return Results.Content(new XDocument(response).ToString(SaveOptions.DisableFormatting), "application/xml");
    }

    /// <summary>
    /// Validates that the user has permission to perform the requested operations on all affected resources.
    /// </summary>
    /// <remarks>
    /// This method performs resource-level authorization checks for each unique service/layer combination
    /// in the transaction. It checks edit permissions on the layer resource type.
    /// </remarks>
    private static async Task<(bool IsAuthorized, IResult? ErrorResult)> ValidateResourceAuthorizationAsync(
        ClaimsPrincipal user,
        List<(FeatureEditCommand Command, string? FallbackId)> commandInfos,
        IResourceAuthorizationService authorizationService,
        ISecurityAuditLogger auditLogger,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var username = user.Identity?.Name ?? "anonymous";

        // Collect unique service/layer combinations that need authorization
        var resourcesToCheck = new HashSet<(string ServiceId, string LayerId)>();
        foreach (var (command, _) in commandInfos)
        {
            resourcesToCheck.Add((command.ServiceId, command.LayerId));
        }

        // Check authorization for each unique resource
        foreach (var (serviceId, layerId) in resourcesToCheck)
        {
            var authResult = await authorizationService.AuthorizeAsync(
                user,
                "layer",
                $"{serviceId}:{layerId}",
                "edit",
                cancellationToken).ConfigureAwait(false);

            if (!authResult.Succeeded)
            {
                var reason = authResult.FailureReason ?? "User does not have edit permission on this layer";

                // Log unauthorized access attempt
                auditLogger.LogUnauthorizedAccess(
                    username,
                    $"WFS Transaction on layer {serviceId}:{layerId}",
                    null,
                    reason);

                logger.LogWarning(
                    "WFS Transaction authorization failed: User {Username} does not have edit permission on layer {ServiceId}:{LayerId}. Reason: {Reason}",
                    username, serviceId, layerId, reason);

                // Return WFS-compliant error response
                var errorResult = WfsHelpers.CreateException(
                    "OperationNotSupported",
                    "Transaction",
                    $"You do not have permission to edit layer '{serviceId}:{layerId}'. {reason}");

                return (false, errorResult);
            }

            logger.LogDebug(
                "WFS Transaction authorization granted for user {Username} on layer {ServiceId}:{LayerId}",
                username, serviceId, layerId);
        }

        return (true, null);
    }

    /// <summary>
    /// Logs details of a successful transaction for audit purposes.
    /// </summary>
    private static void LogSuccessfulTransaction(
        HttpContext context,
        List<(FeatureEditCommand Command, string? FallbackId)> commandInfos,
        ISecurityAuditLogger auditLogger,
        ILogger logger)
    {
        var username = context.User?.Identity?.Name ?? "anonymous";
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        // Count operation types
        var insertCount = commandInfos.Count(c => c.Command is AddFeatureCommand);
        var updateCount = commandInfos.Count(c => c.Command is UpdateFeatureCommand);
        var replaceCount = commandInfos.Count(c => c.Command is ReplaceFeatureCommand);
        var deleteCount = commandInfos.Count(c => c.Command is DeleteFeatureCommand);

        // Get affected resources
        var affectedLayers = commandInfos
            .Select(c => $"{c.Command.ServiceId}:{c.Command.LayerId}")
            .Distinct()
            .ToList();

        // Log structured audit entry
        foreach (var layer in affectedLayers)
        {
            auditLogger.LogDataAccess(
                username,
                "WFS Transaction",
                "layer",
                layer,
                ipAddress);
        }

        // Log detailed transaction info
        logger.LogInformation(
            "WFS Transaction completed successfully - User={Username}, IP={IPAddress}, " +
            "Operations={{Insert={InsertCount}, Update={UpdateCount}, Replace={ReplaceCount}, Delete={DeleteCount}}}, " +
            "Layers=[{Layers}]",
            username,
            ipAddress ?? "unknown",
            insertCount,
            updateCount,
            replaceCount,
            deleteCount,
            string.Join(", ", affectedLayers));
    }

    #endregion
}
