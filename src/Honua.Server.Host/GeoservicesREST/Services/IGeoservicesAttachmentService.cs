// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer attachment operations.
/// </summary>
public interface IGeoservicesAttachmentService
{
    /// <summary>
    /// Queries attachments for specified feature object IDs.
    /// </summary>
    Task<IActionResult> QueryAttachmentsAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        IReadOnlyList<int> objectIds,
        IReadOnlyList<int> attachmentIdsFilter,
        string folderId,
        string serviceId,
        int layerIndex,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds an attachment to a feature.
    /// </summary>
    Task<IActionResult> AddAttachmentAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing attachment.
    /// </summary>
    Task<IActionResult> UpdateAttachmentAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes attachments from a feature.
    /// </summary>
    Task<IActionResult> DeleteAttachmentsAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        HttpRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads an attachment file.
    /// </summary>
    Task<IActionResult> DownloadAttachmentAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        string objectId,
        int attachmentId,
        CancellationToken cancellationToken);
}
