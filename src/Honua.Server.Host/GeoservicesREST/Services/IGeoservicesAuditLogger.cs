// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Security.Claims;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Interface for audit logging of Geoservices REST API data modification operations.
/// Provides comprehensive security tracking for all feature and attachment edits.
/// </summary>
public interface IGeoservicesAuditLogger
{
    /// <summary>
    /// Logs the addition of new features.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="featureCount">The number of features added.</param>
    /// <param name="user">The user principal performing the operation.</param>
    /// <param name="ipAddress">The IP address of the client.</param>
    void LogFeatureAdd(
        string serviceId,
        string layerId,
        int featureCount,
        ClaimsPrincipal user,
        string? ipAddress);

    /// <summary>
    /// Logs the update of existing features.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="featureIds">The IDs of features being updated.</param>
    /// <param name="user">The user principal performing the operation.</param>
    /// <param name="ipAddress">The IP address of the client.</param>
    void LogFeatureUpdate(
        string serviceId,
        string layerId,
        IEnumerable<object> featureIds,
        ClaimsPrincipal user,
        string? ipAddress);

    /// <summary>
    /// Logs the deletion of features.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="featureIds">The IDs of features being deleted.</param>
    /// <param name="user">The user principal performing the operation.</param>
    /// <param name="ipAddress">The IP address of the client.</param>
    void LogFeatureDelete(
        string serviceId,
        string layerId,
        IEnumerable<object> featureIds,
        ClaimsPrincipal user,
        string? ipAddress);

    /// <summary>
    /// Logs attachment operations (upload/delete).
    /// </summary>
    /// <param name="operation">The operation type (e.g., "Upload", "Delete").</param>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="featureId">The feature ID associated with the attachment.</param>
    /// <param name="fileName">The name of the attachment file.</param>
    /// <param name="user">The user principal performing the operation.</param>
    /// <param name="ipAddress">The IP address of the client.</param>
    void LogAttachmentOperation(
        string operation,
        string serviceId,
        string layerId,
        object featureId,
        string fileName,
        ClaimsPrincipal user,
        string? ipAddress);
}
