// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Catalog;
using Honua.Server.Host.Attachments;
using Honua.Server.Host.Configuration;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer attachment operations.
/// Extracted from GeoservicesRESTFeatureServerController to reduce controller complexity.
/// </summary>
public sealed class GeoservicesAttachmentService : IGeoservicesAttachmentService
{
    private const string GlobalIdFieldName = "globalId";

    private readonly IFeatureAttachmentOrchestrator attachmentOrchestrator;
    private readonly IAttachmentStoreSelector attachmentStoreSelector;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<GeoservicesAttachmentService> logger;

    public GeoservicesAttachmentService(
        IFeatureAttachmentOrchestrator attachmentOrchestrator,
        IAttachmentStoreSelector attachmentStoreSelector,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GeoservicesAttachmentService> logger)
    {
        this.attachmentOrchestrator = Core.Utilities.Guard.NotNull(attachmentOrchestrator);
        this.attachmentStoreSelector = Core.Utilities.Guard.NotNull(attachmentStoreSelector);
        this.httpContextAccessor = Core.Utilities.Guard.NotNull(httpContextAccessor);
        this.logger = Core.Utilities.Guard.NotNull(logger);
    }

    public async Task<IActionResult> QueryAttachmentsAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        IReadOnlyList<int> objectIds,
        IReadOnlyList<int> attachmentIdsFilter,
        string folderId,
        string serviceId,
        int layerIndex,
        CancellationToken cancellationToken)
    {
        var attachmentGroups = new List<GeoservicesAttachmentGroup>();

        // Limit attachments per objectId to prevent memory exhaustion
        const int MaxAttachmentsPerObjectId = 1000;

        foreach (var objectId in objectIds)
        {
            var featureId = objectId.ToString(CultureInfo.InvariantCulture);
            var descriptors = await this.attachmentOrchestrator.ListAsync(serviceView.Service.Id, layerView.Layer.Id, featureId, cancellationToken).ConfigureAwait(false);
            if (attachmentIdsFilter.Count > 0)
            {
                descriptors = descriptors.Where(descriptor => attachmentIdsFilter.Contains(descriptor.AttachmentObjectId)).ToList();
            }

            // Apply pagination to limit attachments per objectId
            var totalAttachments = descriptors.Count;
            if (totalAttachments > MaxAttachmentsPerObjectId)
            {
                this.logger.LogWarning(
                    "Feature {FeatureId} in layer {LayerId} has {AttachmentCount} attachments, limiting to {MaxAttachments}.",
                    featureId,
                    layerView.Layer.Id,
                    totalAttachments,
                    MaxAttachmentsPerObjectId);
                descriptors = descriptors.Take(MaxAttachmentsPerObjectId).ToList();
            }

            var featureGlobalId = descriptors.FirstOrDefault(static descriptor => !string.IsNullOrWhiteSpace(descriptor.FeatureGlobalId))?.FeatureGlobalId;
            var infoList = descriptors.Select(descriptor => new GeoservicesAttachmentInfo
            {
                Id = descriptor.AttachmentObjectId,
                ContentType = descriptor.MimeType,
                Size = descriptor.SizeBytes,
                Name = descriptor.Name,
                GlobalId = descriptor.AttachmentId,
                ParentGlobalId = featureGlobalId,
                Url = AttachmentUrlBuilder.BuildGeoServicesUrl(GetHttpContext().Request, folderId, serviceId, layerIndex, objectId, descriptor.AttachmentObjectId)
            }).ToList();

            attachmentGroups.Add(new GeoservicesAttachmentGroup
            {
                ObjectId = objectId,
                GlobalId = featureGlobalId,
                AttachmentInfos = infoList
            });
        }

        var response = new GeoservicesQueryAttachmentsResponse
        {
            AttachmentGroups = attachmentGroups,
            HasAttachments = attachmentGroups.Any(group => group.AttachmentInfos.Count > 0)
        };

        return new OkObjectResult(response);
    }

    public async Task<IActionResult> AddAttachmentAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (uploadRequest, parseError) = await TryParseAttachmentUploadAsync(request, cancellationToken).ConfigureAwait(false);
        if (parseError is not null)
        {
            return parseError;
        }

        var featureId = uploadRequest!.ObjectId.ToString(CultureInfo.InvariantCulture);
        var addRequest = new AddFeatureAttachmentRequest(
            serviceView.Service.Id,
            layerView.Layer.Id,
            featureId,
            uploadRequest.Upload,
            uploadRequest.GlobalId,
            UserIdentityHelper.GetUserIdentifierOrNull(GetHttpContext().User));

        var result = await this.attachmentOrchestrator.AddAsync(addRequest, cancellationToken).ConfigureAwait(false);
        var response = new GeoservicesAddAttachmentResponse
        {
            Result = result.Success && result.Attachment is not null
                ? CreateMutationSuccess(uploadRequest.ObjectId, result.Attachment)
                : CreateMutationFailure(uploadRequest.ObjectId, result.Error)
        };

        return new OkObjectResult(response);
    }

    public async Task<IActionResult> UpdateAttachmentAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (updateRequest, parseError) = await TryParseAttachmentUpdateAsync(request, cancellationToken).ConfigureAwait(false);
        if (parseError is not null)
        {
            return parseError;
        }

        var featureId = updateRequest!.ObjectId.ToString(CultureInfo.InvariantCulture);
        var descriptor = await GetDescriptorAsync(serviceView.Service.Id, layerView.Layer.Id, featureId, updateRequest.AttachmentObjectId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return new NotFoundResult();
        }

        var updateAttachmentRequest = new UpdateFeatureAttachmentRequest(
            serviceView.Service.Id,
            layerView.Layer.Id,
            featureId,
            descriptor.AttachmentId,
            updateRequest.Upload,
            updateRequest.GlobalId,
            UserIdentityHelper.GetUserIdentifierOrNull(GetHttpContext().User));

        var result = await this.attachmentOrchestrator.UpdateAsync(updateAttachmentRequest, cancellationToken).ConfigureAwait(false);
        var response = new GeoservicesUpdateAttachmentResponse
        {
            Result = result.Success && result.Attachment is not null
                ? CreateMutationSuccess(updateRequest.ObjectId, result.Attachment)
                : CreateMutationFailure(updateRequest.ObjectId, result.Error)
        };

        return new OkObjectResult(response);
    }

    public async Task<IActionResult> DeleteAttachmentsAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (form, objectId, parseError) = await TryReadFormAndParseObjectIdAsync(request, cancellationToken).ConfigureAwait(false);
        if (parseError is not null)
        {
            return parseError;
        }

        var attachmentIdsRaw = form?["attachmentIds"].FirstOrDefault() ?? request.Query["attachmentIds"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(attachmentIdsRaw))
        {
            return new BadRequestObjectResult(new { error = "attachmentIds parameter is required." });
        }

        var attachmentIds = ParseIntList(attachmentIdsRaw);
        if (attachmentIds.Count == 0)
        {
            return new BadRequestObjectResult(new { error = "attachmentIds parameter did not contain any valid identifiers." });
        }

        // Limit attachmentIds to prevent fan-out DoS attacks on attachment storage
        const int MaxAttachmentIds = 100;
        if (attachmentIds.Count > MaxAttachmentIds)
        {
            return new BadRequestObjectResult(new { error = $"attachmentIds parameter exceeds maximum limit of {MaxAttachmentIds} identifiers." });
        }

        var featureId = objectId.ToString(CultureInfo.InvariantCulture);
        var descriptors = await this.attachmentOrchestrator.ListAsync(serviceView.Service.Id, layerView.Layer.Id, featureId, cancellationToken).ConfigureAwait(false);
        var descriptorLookup = descriptors.ToDictionary(descriptor => descriptor.AttachmentObjectId);

        var results = new List<GeoservicesAttachmentDeleteResult>();
        foreach (var attachmentObjectId in attachmentIds)
        {
            if (!descriptorLookup.TryGetValue(attachmentObjectId, out var descriptor))
            {
                results.Add(new GeoservicesAttachmentDeleteResult
                {
                    ObjectId = objectId,
                    AttachmentId = attachmentObjectId,
                    GlobalId = null,
                    Success = false,
                    Error = MapError(FeatureAttachmentError.AttachmentNotFound(attachmentObjectId.ToString(CultureInfo.InvariantCulture)))
                });
                continue;
            }

            var deleteRequest = new DeleteFeatureAttachmentRequest(
                serviceView.Service.Id,
                layerView.Layer.Id,
                featureId,
                descriptor.AttachmentId,
                UserIdentityHelper.GetUserIdentifierOrNull(GetHttpContext().User));

            var success = await this.attachmentOrchestrator.DeleteAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
            results.Add(new GeoservicesAttachmentDeleteResult
            {
                ObjectId = objectId,
                AttachmentId = attachmentObjectId,
                GlobalId = descriptor.AttachmentId,
                Success = success,
                Error = success ? null : MapError(FeatureAttachmentError.AttachmentNotFound(descriptor.AttachmentId))
            });
        }

        var response = new GeoservicesDeleteAttachmentsResponse
        {
            Results = results
        };

        return new OkObjectResult(response);
    }

    public async Task<IActionResult> DownloadAttachmentAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        string objectId,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var descriptor = await GetDescriptorAsync(serviceView.Service.Id, layerView.Layer.Id, objectId, attachmentId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return new NotFoundResult();
        }

        var downloadResult = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            layerView.Layer.Attachments.StorageProfileId,
            this.attachmentStoreSelector,
            this.logger,
            serviceView.Service.Id,
            layerView.Layer.Id,
            cancellationToken).ConfigureAwait(false);

        // Convert download result to IActionResult
        if (!downloadResult.IsSuccess)
        {
            return new NotFoundResult();
        }

        var stream = downloadResult.ReadResult!.Content;
        var contentType = descriptor.MimeType ?? "application/octet-stream";
        var fileName = descriptor.Name ?? $"attachment_{attachmentId}";

        return new FileStreamResult(stream, contentType)
        {
            FileDownloadName = fileName,
            EnableRangeProcessing = true
        };
    }

    private async Task<(AttachmentUploadRequest? Request, IActionResult? Error)> TryParseAttachmentUploadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (file, objectId, _, globalId, error) = await TryParseAttachmentFormAsync(
            request,
            requireAttachmentId: false,
            cancellationToken).ConfigureAwait(false);

        if (error is not null)
        {
            return (null, error);
        }

        var upload = new FeatureAttachmentUpload(
            file!.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            _ => Task.FromResult<Stream>(file.OpenReadStream()),
            file.Length);

        var uploadRequest = new AttachmentUploadRequest(objectId, upload, globalId);
        return (uploadRequest, null);
    }

    private async Task<(AttachmentUpdateRequest? Request, IActionResult? Error)> TryParseAttachmentUpdateAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var (file, objectId, attachmentId, globalId, error) = await TryParseAttachmentFormAsync(
            request,
            requireAttachmentId: true,
            cancellationToken).ConfigureAwait(false);

        if (error is not null)
        {
            return (null, error);
        }

        var upload = new FeatureAttachmentUpload(
            file!.FileName,
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            _ => Task.FromResult<Stream>(file.OpenReadStream()),
            file.Length);

        var updateRequest = new AttachmentUpdateRequest(objectId, attachmentId!.Value, upload, globalId);
        return (updateRequest, null);
    }

    private async Task<(IFormFile? File, int ObjectId, int? AttachmentId, string? GlobalId, IActionResult? Error)> TryParseAttachmentFormAsync(
        HttpRequest request,
        bool requireAttachmentId,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            var errorMessage = requireAttachmentId
                ? "Multipart form data is required for updating attachments."
                : "Multipart form-data content is required.";
            return (null, 0, null, null, new BadRequestObjectResult(new { error = errorMessage }));
        }

        var (form, objectId, error) = await TryReadFormAndParseObjectIdAsync(request, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return (null, 0, null, null, error);
        }

        int? attachmentId = null;
        if (requireAttachmentId)
        {
            var attachmentIdRaw = form!["attachmentId"].FirstOrDefault() ?? request.Query["attachmentId"].FirstOrDefault();
            if (!TryParseObjectId(attachmentIdRaw, out var attachmentObjectId))
            {
                return (null, 0, null, null, new BadRequestObjectResult(new { error = "A valid attachmentId must be supplied." }));
            }
            attachmentId = attachmentObjectId;
        }

        if (form!.Files.Count == 0)
        {
            var errorMessage = requireAttachmentId
                ? "An updated attachment file must be provided."
                : "An attachment file must be provided.";
            return (null, 0, null, null, new BadRequestObjectResult(new { error = errorMessage }));
        }

        var file = form.Files[0];
        if (!requireAttachmentId && file.Length == 0)
        {
            return (null, 0, null, null, new BadRequestObjectResult(new { error = "An attachment file must be provided." }));
        }

        var globalId = form[GlobalIdFieldName].FirstOrDefault();
        return (file, objectId, attachmentId, globalId, null);
    }

    private async Task<(IFormCollection? Form, int ObjectId, IActionResult? Error)> TryReadFormAndParseObjectIdAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var form = request.HasFormContentType ? await request.ReadFormAsync(cancellationToken).ConfigureAwait(false) : null;
        var objectIdRaw = form?["objectId"].FirstOrDefault() ?? request.Query["objectId"].FirstOrDefault();

        if (!TryParseObjectId(objectIdRaw, out var objectId))
        {
            return (null, 0, new BadRequestObjectResult(new { error = "A valid objectId must be supplied." }));
        }

        return (form, objectId, null);
    }

    private async Task<AttachmentDescriptor?> GetDescriptorAsync(
        string serviceId,
        string layerId,
        string featureId,
        int attachmentObjectId,
        CancellationToken cancellationToken)
    {
        var descriptors = await this.attachmentOrchestrator.ListAsync(serviceId, layerId, featureId, cancellationToken).ConfigureAwait(false);
        return descriptors.FirstOrDefault(item => item.AttachmentObjectId == attachmentObjectId);
    }

    private static GeoservicesAttachmentMutationResult CreateMutationSuccess(int objectId, AttachmentDescriptor descriptor)
    {
        return new GeoservicesAttachmentMutationResult
        {
            ObjectId = objectId,
            Id = descriptor.AttachmentObjectId,
            GlobalId = descriptor.AttachmentId,
            Success = true
        };
    }

    private static GeoservicesAttachmentMutationResult CreateMutationFailure(int objectId, FeatureAttachmentError? error)
    {
        return new GeoservicesAttachmentMutationResult
        {
            ObjectId = objectId,
            Success = false,
            Error = error is null ? null : MapError(error)
        };
    }

    private static GeoservicesAttachmentError MapError(FeatureAttachmentError error)
    {
        var code = error.Code switch
        {
            "feature_not_found" => 404,
            "attachment_not_found" => 404,
            "attachments_disabled" => 400,
            "max_size_exceeded" => 413,
            _ => 400
        };

        return new GeoservicesAttachmentError
        {
            Code = code,
            Description = error.Message,
            Details = error.Details
        };
    }

    private static IReadOnlyList<int> ParseIntList(string? raw)
    {
        return QueryParsingHelpers.ParseCsv(raw)
            .Select(s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? (int?)v : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }

    private static bool TryParseObjectId(string? raw, out int objectId)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out objectId);
    }

    private HttpContext GetHttpContext()
    {
        return this.httpContextAccessor.HttpContext ?? throw new InvalidOperationException("HttpContext is not available.");
    }

    private sealed record AttachmentUploadRequest(int ObjectId, FeatureAttachmentUpload Upload, string? GlobalId);
    private sealed record AttachmentUpdateRequest(int ObjectId, int AttachmentObjectId, FeatureAttachmentUpload Upload, string? GlobalId);
}
