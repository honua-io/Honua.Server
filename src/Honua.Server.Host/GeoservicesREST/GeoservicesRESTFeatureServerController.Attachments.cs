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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST;

public sealed partial class GeoservicesRESTFeatureServerController
{
    /// <summary>
    /// Resolves service and layer, validates attachments are enabled.
    /// Returns null if resolution succeeds; returns an IActionResult error if it fails.
    /// </summary>
    private IActionResult? ValidateAttachmentContext(
        string? folderId,
        string serviceId,
        int layerIndex,
        out CatalogServiceView? serviceView,
        out CatalogLayerView? layerView)
    {
        var resolution = GeoservicesRESTServiceResolutionHelper.ResolveServiceAndLayer(this, this.catalog, folderId, serviceId, layerIndex);
        if (resolution.Error is not null)
        {
            serviceView = null;
            layerView = null;
            return resolution.Error;
        }

        serviceView = resolution.ServiceView;
        layerView = resolution.LayerView;

        if (!layerView!.Layer.Attachments.Enabled)
        {
            return this.BadRequest(new { error = "Attachments are not enabled for this layer." });
        }

        return null;
    }

    /// <summary>
    /// Result of parsing attachment upload form data.
    /// </summary>
    private sealed record AttachmentUploadRequest(
        int ObjectId,
        FeatureAttachmentUpload Upload,
        string? GlobalId);

    /// <summary>
    /// Result of parsing attachment update form data (includes attachment ID).
    /// </summary>
    private sealed record AttachmentUpdateRequest(
        int ObjectId,
        int AttachmentObjectId,
        FeatureAttachmentUpload Upload,
        string? GlobalId);

    /// <summary>
    /// Parses form data and extracts objectId. Returns form, objectId, or error.
    /// Used by all attachment operations that need to read form data.
    /// </summary>
    private async Task<(IFormCollection? Form, int ObjectId, IActionResult? Error)> TryReadFormAndParseObjectIdAsync(
        CancellationToken cancellationToken)
    {
        var form = Request.HasFormContentType ? await Request.ReadFormAsync(cancellationToken).ConfigureAwait(false) : null;
        var objectIdRaw = form?["objectId"].FirstOrDefault() ?? this.Request.Query["objectId"].FirstOrDefault();

        if (!TryParseObjectId(objectIdRaw, out var objectId))
        {
            return (null, 0, BadRequest(new { error = "A valid objectId must be supplied." }));
        }

        return (form, objectId, null);
    }

    /// <summary>
    /// Parses multipart form data for attachment operations (add or update).
    /// Returns the parsed values or an error IActionResult if validation fails.
    /// </summary>
    private async Task<(IFormFile? File, int ObjectId, int? AttachmentId, string? GlobalId, IActionResult? Error)> TryParseAttachmentFormAsync(
        bool requireAttachmentId,
        CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            var errorMessage = requireAttachmentId
                ? "Multipart form data is required for updating attachments."
                : "Multipart form-data content is required.";
            return (null, 0, null, null, BadRequest(new { error = errorMessage }));
        }

        var (form, objectId, error) = await TryReadFormAndParseObjectIdAsync(cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return (null, 0, null, null, error);
        }

        int? attachmentId = null;
        if (requireAttachmentId)
        {
            var attachmentIdRaw = form!["attachmentId"].FirstOrDefault() ?? this.Request.Query["attachmentId"].FirstOrDefault();
            if (!TryParseObjectId(attachmentIdRaw, out var attachmentObjectId))
            {
                return (null, 0, null, null, BadRequest(new { error = "A valid attachmentId must be supplied." }));
            }
            attachmentId = attachmentObjectId;
        }

        if (form!.Files.Count == 0)
        {
            var errorMessage = requireAttachmentId
                ? "An updated attachment file must be provided."
                : "An attachment file must be provided.";
            return (null, 0, null, null, BadRequest(new { error = errorMessage }));
        }

        var file = form.Files[0];
        if (!requireAttachmentId && file.Length == 0)
        {
            return (null, 0, null, null, BadRequest(new { error = "An attachment file must be provided." }));
        }

        var globalId = form[GlobalIdFieldName].FirstOrDefault();
        return (file, objectId, attachmentId, globalId, null);
    }

    /// <summary>
    /// Parses multipart form data for attachment add operations.
    /// Returns the parsed request or an error IActionResult if validation fails.
    /// </summary>
    private async Task<(AttachmentUploadRequest? Request, IActionResult? Error)> TryParseAttachmentUploadAsync(
        CancellationToken cancellationToken)
    {
        var (file, objectId, _, globalId, error) = await TryParseAttachmentFormAsync(
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

        var request = new AttachmentUploadRequest(objectId, upload, globalId);
        return (request, null);
    }

    /// <summary>
    /// Parses multipart form data for attachment update operations.
    /// Returns the parsed request or an error IActionResult if validation fails.
    /// </summary>
    private async Task<(AttachmentUpdateRequest? Request, IActionResult? Error)> TryParseAttachmentUpdateAsync(
        CancellationToken cancellationToken)
    {
        var (file, objectId, attachmentId, globalId, error) = await TryParseAttachmentFormAsync(
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

        var request = new AttachmentUpdateRequest(objectId, attachmentId!.Value, upload, globalId);
        return (request, null);
    }

    /// <summary>
    /// Retrieves a single attachment descriptor by object ID.
    /// Encapsulates the ListAsync + FirstOrDefault pattern used across update, delete, and download operations.
    /// </summary>
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

    [HttpGet("{layerIndex:int}/queryAttachments")]
    public async Task<IActionResult> QueryAttachmentsAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var error = ValidateAttachmentContext(folderId, serviceId, layerIndex, out var serviceView, out var layerView);
        if (error is not null)
        {
            return error;
        }

        var objectIdsRaw = this.Request.Query.TryGetValue("objectIds", out var objectIdsValues) ? objectIdsValues.ToString() : null;
        if (string.IsNullOrWhiteSpace(objectIdsRaw))
        {
            return this.BadRequest(new { error = "objectIds parameter is required." });
        }

        var objectIds = ParseIntList(objectIdsRaw);
        if (objectIds.Count == 0)
        {
            return this.BadRequest(new { error = "objectIds parameter did not contain any valid identifiers." });
        }

        // Limit objectIds to prevent fan-out DoS attacks on attachment storage
        const int MaxObjectIds = 100;
        if (objectIds.Count > MaxObjectIds)
        {
            return this.BadRequest(new { error = $"objectIds parameter exceeds maximum limit of {MaxObjectIds} identifiers." });
        }

        var attachmentGroups = new List<GeoservicesAttachmentGroup>();
        var attachmentIdsFilter = this.Request.Query.TryGetValue("attachmentIds", out var attachmentIdsValues)
            ? ParseIntList(attachmentIdsValues.ToString())
            : Array.Empty<int>();

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
                Url = BuildAttachmentUrl(folderId, serviceId, layerIndex, objectId, descriptor.AttachmentObjectId)
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

        return this.Ok(response);
    }

    [HttpPost("{layerIndex:int}/queryAttachments")]
    public Task<IActionResult> QueryAttachmentsPostAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        return QueryAttachmentsAsync(folderId, serviceId, layerIndex, cancellationToken);
    }

    /// <summary>
    /// Adds an attachment to a feature. Request size limited to 100 MB to prevent DoS attacks.
    /// </summary>
    [HttpPost("{layerIndex:int}/addAttachment")]
    [Authorize(Policy = "RequireDataPublisher")]
    [RequestSizeLimit((int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes)]
    public async Task<IActionResult> AddAttachmentAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var error = ValidateAttachmentContext(folderId, serviceId, layerIndex, out var serviceView, out var layerView);
        if (error is not null)
        {
            return error;
        }

        var (uploadRequest, parseError) = await TryParseAttachmentUploadAsync(cancellationToken).ConfigureAwait(false);
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
            UserIdentityHelper.GetUserIdentifierOrNull(User));

        var result = await this.attachmentOrchestrator.AddAsync(addRequest, cancellationToken).ConfigureAwait(false);
        var response = new GeoservicesAddAttachmentResponse
        {
            Result = result.Success && result.Attachment is not null
                ? CreateMutationSuccess(uploadRequest.ObjectId, result.Attachment)
                : CreateMutationFailure(uploadRequest.ObjectId, result.Error)
        };

        return this.Ok(response);
    }

    [HttpPost("{layerIndex:int}/updateAttachment")]
    [Authorize(Policy = "RequireDataPublisher")]
    [RequestSizeLimit((int)ApiLimitsAndConstants.DefaultMaxRequestBodyBytes)]
    public async Task<IActionResult> UpdateAttachmentAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var error = ValidateAttachmentContext(folderId, serviceId, layerIndex, out var serviceView, out var layerView);
        if (error is not null)
        {
            return error;
        }

        var (updateRequest, parseError) = await TryParseAttachmentUpdateAsync(cancellationToken).ConfigureAwait(false);
        if (parseError is not null)
        {
            return parseError;
        }

        var featureId = updateRequest!.ObjectId.ToString(CultureInfo.InvariantCulture);
        var descriptor = await GetDescriptorAsync(serviceView.Service.Id, layerView.Layer.Id, featureId, updateRequest.AttachmentObjectId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return this.NotFound();
        }

        var request = new UpdateFeatureAttachmentRequest(
            serviceView.Service.Id,
            layerView.Layer.Id,
            featureId,
            descriptor.AttachmentId,
            updateRequest.Upload,
            updateRequest.GlobalId,
            UserIdentityHelper.GetUserIdentifierOrNull(User));

        var result = await this.attachmentOrchestrator.UpdateAsync(request, cancellationToken).ConfigureAwait(false);
        var response = new GeoservicesUpdateAttachmentResponse
        {
            Result = result.Success && result.Attachment is not null
                ? CreateMutationSuccess(updateRequest.ObjectId, result.Attachment)
                : CreateMutationFailure(updateRequest.ObjectId, result.Error)
        };

        return this.Ok(response);
    }

    [HttpPost("{layerIndex:int}/deleteAttachments")]
    [Authorize(Policy = "RequireDataPublisher")]
    public async Task<IActionResult> DeleteAttachmentsAsync(string? folderId, string serviceId, int layerIndex, CancellationToken cancellationToken)
    {
        var error = ValidateAttachmentContext(folderId, serviceId, layerIndex, out var serviceView, out var layerView);
        if (error is not null)
        {
            return error;
        }

        var (form, objectId, parseError) = await TryReadFormAndParseObjectIdAsync(cancellationToken).ConfigureAwait(false);
        if (parseError is not null)
        {
            return parseError;
        }

        var attachmentIdsRaw = form?["attachmentIds"].FirstOrDefault() ?? this.Request.Query["attachmentIds"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(attachmentIdsRaw))
        {
            return this.BadRequest(new { error = "attachmentIds parameter is required." });
        }

        var attachmentIds = ParseIntList(attachmentIdsRaw);
        if (attachmentIds.Count == 0)
        {
            return this.BadRequest(new { error = "attachmentIds parameter did not contain any valid identifiers." });
        }

        // Limit attachmentIds to prevent fan-out DoS attacks on attachment storage
        const int MaxAttachmentIds = 100;
        if (attachmentIds.Count > MaxAttachmentIds)
        {
            return this.BadRequest(new { error = $"attachmentIds parameter exceeds maximum limit of {MaxAttachmentIds} identifiers." });
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
                UserIdentityHelper.GetUserIdentifierOrNull(User));

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

        return this.Ok(response);
    }

    [HttpGet("{layerIndex:int}/{objectId}/attachments/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachmentAsync(string? folderId, string serviceId, int layerIndex, string objectId, int attachmentId, CancellationToken cancellationToken)
    {
        var error = ValidateAttachmentContext(folderId, serviceId, layerIndex, out var serviceView, out var layerView);
        if (error is not null)
        {
            // Download returns NotFound instead of BadRequest for disabled attachments
            return error is BadRequestObjectResult ? NotFound() : error;
        }

        var descriptor = await GetDescriptorAsync(serviceView.Service.Id, layerView.Layer.Id, objectId, attachmentId, cancellationToken).ConfigureAwait(false);
        if (descriptor is null)
        {
            return this.NotFound();
        }

        var downloadResult = await AttachmentDownloadHelper.TryDownloadAsync(
            descriptor,
            layerView.Layer.Attachments.StorageProfileId,
            this.attachmentStoreSelector,
            this.logger,
            serviceView.Service.Id,
            layerView.Layer.Id,
            cancellationToken).ConfigureAwait(false);

        return await AttachmentDownloadHelper.ToActionResultAsync(downloadResult, this, cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Parses a comma-separated list of integers.
    /// Non-integer values are silently skipped.
    /// </summary>
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

    private string BuildAttachmentUrl(string? folderId, string serviceId, int layerIndex, int objectId, int attachmentId)
    {
        return AttachmentUrlBuilder.BuildGeoServicesUrl(Request, folderId, serviceId, layerIndex, objectId, attachmentId);
    }
}
