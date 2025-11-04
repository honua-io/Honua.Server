// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

#nullable enable

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Provides unified URL building for attachment resources across GeoServices REST and OGC APIs.
/// Handles token propagation and ensures consistent URL formatting.
/// </summary>
public static class AttachmentUrlBuilder
{
    /// <summary>
    /// Builds a GeoServices REST API attachment URL.
    /// Format: /rest/services/{serviceId}/FeatureServer/{layerIndex}/{objectId}/attachments/{attachmentId}
    /// Or with folder: /rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/{objectId}/attachments/{attachmentId}
    /// </summary>
    /// <param name="request">The HTTP request containing scheme, host, and path base information.</param>
    /// <param name="folderId">The folder identifier containing the service (optional - omitted from URL if null, empty, or "root").</param>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerIndex">The layer index within the feature server.</param>
    /// <param name="objectId">The feature object ID.</param>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <param name="includeRootFolder">When true, keeps the literal folder identifier (including "root") in the generated path.</param>
    /// <returns>A fully qualified URL to the attachment resource.</returns>
    public static string BuildGeoServicesUrl(
        HttpRequest request,
        string? folderId,
        string serviceId,
        int layerIndex,
        long objectId,
        long attachmentId,
        bool includeRootFolder = false)
    {
        Guard.NotNull(request);

        var basePath = request.PathBase.HasValue ? request.PathBase.Value!.TrimEnd('/') : string.Empty;

        // Omit folderId from URL if it's null, empty, whitespace, or the default "root" value
        var includeFolderId = !string.IsNullOrWhiteSpace(folderId) &&
                              (includeRootFolder || !string.Equals(folderId, "root", StringComparison.OrdinalIgnoreCase));

        var relativePath = includeFolderId
            ? $"/rest/services/{folderId}/{serviceId}/FeatureServer/{layerIndex}/{objectId.ToString(CultureInfo.InvariantCulture)}/attachments/{attachmentId.ToString(CultureInfo.InvariantCulture)}"
            : $"/rest/services/{serviceId}/FeatureServer/{layerIndex}/{objectId.ToString(CultureInfo.InvariantCulture)}/attachments/{attachmentId.ToString(CultureInfo.InvariantCulture)}";

        var host = request.Host.HasValue ? request.Host.Value : string.Empty;
        var url = host.IsNullOrEmpty()
            ? $"{basePath}{relativePath}"
            : $"{request.Scheme}://{host}{basePath}{relativePath}";

        return AppendTokenIfPresent(request, url);
    }

    /// <summary>
    /// Builds an OGC API attachment URL.
    /// Format: /ogc/collections/{collectionId}/items/{featureId}/attachments/{attachmentId}
    /// </summary>
    /// <param name="request">The HTTP request containing scheme, host, and path base information.</param>
    /// <param name="collectionId">The OGC collection identifier.</param>
    /// <param name="featureId">The feature identifier (typically a string representation of the object ID).</param>
    /// <param name="attachmentId">The attachment identifier.</param>
    /// <returns>A fully qualified URL to the attachment resource, or null if required parameters are missing.</returns>
    public static string? BuildOgcUrl(
        HttpRequest request,
        string collectionId,
        string featureId,
        string attachmentId)
    {
        Guard.NotNull(request);

        if (featureId.IsNullOrWhiteSpace() || attachmentId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var basePath = request.PathBase.HasValue ? request.PathBase.Value!.TrimEnd('/') : string.Empty;

        static string Encode(string value) => Uri.EscapeDataString(value);

        var relativePath =
            $"/ogc/collections/{Encode(collectionId)}/items/{Encode(featureId)}/attachments/{Encode(attachmentId)}";

        var host = request.Host.HasValue ? $"{request.Scheme}://{request.Host}" : string.Empty;
        var href = host.IsNullOrEmpty() ? $"{basePath}{relativePath}" : $"{host}{basePath}{relativePath}";

        return AppendTokenIfPresent(request, href);
    }

    /// <summary>
    /// Previously propagated authentication tokens to attachment URLs.
    /// NOW DISABLED: This method intentionally does NOT append tokens to prevent re-exposing
    /// short-lived security credentials that should remain confidential.
    ///
    /// Security rationale:
    /// - Attachment URLs may be cached, logged, or shared
    /// - Short-lived tokens (e.g., 15-minute S3 presigned URLs) should not be re-exposed in API responses
    /// - Clients should authenticate directly to attachment endpoints rather than relying on embedded tokens
    /// </summary>
    private static string AppendTokenIfPresent(HttpRequest request, string url)
    {
        // DO NOT propagate tokens to attachment URLs for security reasons
        // Clients must authenticate directly when accessing attachment resources
        return url;
    }
}
