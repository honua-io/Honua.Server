// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Ogc;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Handlers for WFS lock-related operations.
/// </summary>
internal static class WfsLockHandlers
{
    /// <summary>
    /// Handles GetFeatureWithLock requests.
    /// </summary>
    public static async Task<IResult> HandleGetFeatureWithLockAsync(
        HttpContext context,
        HttpRequest request,
        IQueryCollection query,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        [FromServices] IWfsLockManager lockManager,
        [FromServices] IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        var executionResult = await WfsGetFeatureHandlers.ExecuteFeatureQueryAsync(request, query, catalog, contextResolver, repository, registry, cancellationToken).ConfigureAwait(false);
        if (executionResult.IsFailure)
        {
            return WfsHelpers.MapExecutionError(executionResult.Error!, query);
        }

        var result = executionResult.Value;
        var execution = result.Execution;

        if (!execution.OutputFormat.EqualsIgnoreCase(WfsConstants.GmlFormat))
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "outputFormat", "GetFeatureWithLock only supports GML output.");
        }

        if (execution.ResultType != FeatureResultType.Results)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "resultType", "GetFeatureWithLock requires resultType=results.");
        }

        var layer = execution.Context.Layer;
        if (layer.IdField.IsNullOrWhiteSpace())
        {
            return WfsHelpers.CreateException("NoApplicableCode", "typeNames", "Layer does not expose a stable identifier required for locking.");
        }

        IReadOnlyList<WfsLockTarget> targets;
        try
        {
            targets = BuildLockTargets(execution.Context.Service.Id, layer, result.Features);
        }
        catch (InvalidOperationException ex)
        {
            return WfsHelpers.CreateException("NoApplicableCode", "typeNames", ex.Message);
        }

        TimeSpan duration;
        try
        {
            duration = WfsHelpers.ParseLockDuration(query);
        }
        catch (InvalidOperationException ex)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "expiry", ex.Message);
        }

        var owner = WfsHelpers.ResolveLockOwner(context);
        var acquisition = await lockManager.TryAcquireAsync(owner, duration, targets, cancellationToken).ConfigureAwait(false);
        if (!acquisition.Success || acquisition.Lock is null)
        {
            return WfsHelpers.CreateException("OperationProcessingFailed", "LockFeature", acquisition.Error ?? "Unable to acquire lock.");
        }

        var responseCrs = CrsNormalizationHelper.NormalizeIdentifier(execution.ResponseCrsUrn);

        var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
        var writerLogger = loggerFactory.CreateLogger<GmlStreamingWriter>();
        var writer = new GmlStreamingWriter(writerLogger);

        var writerContext = new StreamingWriterContext
        {
            ServiceId = execution.Context.Service.Id,
            TargetWkid = execution.Srid,
            ReturnGeometry = true,
            TotalCount = result.NumberMatched,
            ExpectedFeatureCount = result.Features.Count,
            Options = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["lockId"] = acquisition.Lock.LockId
            }
        };

        context.Response.ContentType = WfsConstants.GmlFormat;
        context.Response.Headers["Content-Crs"] = responseCrs;

        var records = StreamFeatureRecords(result.Features, layer, cancellationToken);
        await writer.WriteCollectionAsync(context.Response.Body, records, layer, writerContext, cancellationToken).ConfigureAwait(false);
        return Results.Empty;
    }

    /// <summary>
    /// Handles LockFeature requests.
    /// </summary>
    public static async Task<IResult> HandleLockFeatureAsync(
        HttpContext context,
        HttpRequest request,
        IQueryCollection query,
        [FromServices] ICatalogProjectionService catalog,
        [FromServices] IFeatureContextResolver contextResolver,
        [FromServices] IFeatureRepository repository,
        [FromServices] IWfsLockManager lockManager,
        [FromServices] IMetadataRegistry registry,
        CancellationToken cancellationToken)
    {
        var lockActionValue = QueryParsingHelpers.GetQueryValue(query, "lockAction");
        var allowPartial = lockActionValue.EqualsIgnoreCase("SOME");
        if (lockActionValue.HasValue() &&
            !allowPartial &&
            !lockActionValue.EqualsIgnoreCase("ALL"))
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "lockAction", "Parameter 'lockAction' must be ALL or SOME.");
        }

        TimeSpan duration;
        try
        {
            duration = WfsHelpers.ParseLockDuration(query);
        }
        catch (InvalidOperationException ex)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "expiry", ex.Message);
        }

        var targetResult = await WfsGetFeatureHandlers.ExecuteFeatureQueryAsync(request, query, catalog, contextResolver, repository, registry, cancellationToken).ConfigureAwait(false);
        if (targetResult.IsFailure)
        {
            return WfsHelpers.MapExecutionError(targetResult.Error!, query);
        }

        var execution = targetResult.Value.Execution;
        var layer = execution.Context.Layer;
        if (layer.IdField.IsNullOrWhiteSpace())
        {
            return WfsHelpers.CreateException("NoApplicableCode", "typeNames", "Layer does not expose a stable identifier required for locking.");
        }

        if (execution.ResultType != FeatureResultType.Results)
        {
            return WfsHelpers.CreateException("InvalidParameterValue", "resultType", "LockFeature requires resultType=results.");
        }

        IReadOnlyList<WfsLockTarget> requestedTargets;
        try
        {
            requestedTargets = BuildLockTargets(execution.Context.Service.Id, layer, targetResult.Value.Features);
        }
        catch (InvalidOperationException ex)
        {
            return WfsHelpers.CreateException("NoApplicableCode", "typeNames", ex.Message);
        }

        var owner = WfsHelpers.ResolveLockOwner(context);
        WfsLockAcquisition? lockInfo = null;
        IReadOnlyList<WfsLockTarget> lockedTargets = Array.Empty<WfsLockTarget>();
        IReadOnlyList<WfsLockTarget> failedTargets = Array.Empty<WfsLockTarget>();

        if (requestedTargets.Count > 0)
        {
            if (allowPartial)
            {
                var available = new List<WfsLockTarget>();
                var conflicts = new List<WfsLockTarget>();

                foreach (var target in requestedTargets)
                {
                    var validation = await lockManager.ValidateAsync(null, new[] { target }, cancellationToken).ConfigureAwait(false);
                    if (validation.Success)
                    {
                        available.Add(target);
                    }
                    else
                    {
                        conflicts.Add(target);
                    }
                }

                if (available.Count > 0)
                {
                    var acquisition = await lockManager.TryAcquireAsync(owner, duration, available, cancellationToken).ConfigureAwait(false);
                    if (!acquisition.Success)
                    {
                        return WfsHelpers.CreateException("OperationProcessingFailed", "LockFeature", acquisition.Error ?? "Unable to acquire lock.");
                    }

                    lockInfo = acquisition.Lock;
                    lockedTargets = acquisition.Lock?.Targets ?? available.ToArray();
                }

                failedTargets = conflicts;
            }
            else
            {
                var acquisition = await lockManager.TryAcquireAsync(owner, duration, requestedTargets, cancellationToken).ConfigureAwait(false);
                if (!acquisition.Success || acquisition.Lock is null)
                {
                    return WfsHelpers.CreateException("OperationProcessingFailed", "LockFeature", acquisition.Error ?? "Unable to acquire lock.");
                }

                lockInfo = acquisition.Lock;
                lockedTargets = acquisition.Lock.Targets;
            }
        }

        return BuildLockFeatureResponse(lockInfo, lockedTargets, failedTargets);
    }

    #region Private Helper Methods

    private static IReadOnlyList<WfsLockTarget> BuildLockTargets(string serviceId, LayerDefinition layer, IReadOnlyList<WfsFeature> features)
    {
        if (features.Count == 0)
        {
            return Array.Empty<WfsLockTarget>();
        }

        var targets = new List<WfsLockTarget>(features.Count);
        foreach (var feature in features)
        {
            var identifier = WfsHelpers.ExtractFeatureIdentifier(layer, feature.Record);
            if (identifier.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException($"Feature does not have a stable identifier in field '{layer.IdField}'.");
            }

            targets.Add(new WfsLockTarget(serviceId, layer.Id, identifier));
        }

        return targets;
    }

    private static IResult BuildLockFeatureResponse(WfsLockAcquisition? lockInfo, IReadOnlyList<WfsLockTarget> lockedTargets, IReadOnlyList<WfsLockTarget> failedTargets)
    {
        var root = new XElement(WfsConstants.Wfs + "LockFeatureResponse",
            new XAttribute(XNamespace.Xmlns + "wfs", WfsConstants.Wfs));

        if (lockInfo is not null)
        {
            root.Add(new XElement(WfsConstants.Wfs + "LockId", lockInfo.LockId));
        }

        if (lockedTargets.Count > 0)
        {
            var lockedElement = new XElement(WfsConstants.Wfs + "FeaturesLocked");
            foreach (var target in lockedTargets)
            {
                lockedElement.Add(new XElement(WfsConstants.Wfs + "FeatureId", new XAttribute("fid", WfsHelpers.FormatLockTarget(target))));
            }

            root.Add(lockedElement);
        }

        if (failedTargets.Count > 0)
        {
            var failedElement = new XElement(WfsConstants.Wfs + "FeaturesNotLocked");
            foreach (var target in failedTargets)
            {
                failedElement.Add(new XElement(WfsConstants.Wfs + "FeatureId", new XAttribute("fid", WfsHelpers.FormatLockTarget(target))));
            }

            root.Add(failedElement);
        }

        return Results.Content(new XDocument(root).ToString(SaveOptions.DisableFormatting), "application/xml");
    }

#pragma warning disable CS1998 // Async method lacks 'await' operators
    private static async IAsyncEnumerable<FeatureRecord> StreamFeatureRecords(
        IReadOnlyList<WfsFeature> features,
        LayerDefinition layer,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var feature in features)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feature.Geometry is Geometry geometry)
            {
                if (!feature.Record.Attributes.TryGetValue(layer.GeometryField, out var existing) || existing is not Geometry)
                {
                    var dict = new Dictionary<string, object?>(feature.Record.Attributes, StringComparer.OrdinalIgnoreCase)
                    {
                        [layer.GeometryField] = geometry
                    };

                    yield return new FeatureRecord(dict);
                    continue;
                }
            }

            yield return feature.Record;
        }
    }
#pragma warning restore CS1998

    #endregion
}
