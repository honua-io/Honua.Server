// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Implementation of audit logging for Geoservices REST API data modification operations.
/// Provides comprehensive security tracking for compliance and forensic analysis.
/// </summary>
public sealed class GeoservicesAuditLogger : IGeoservicesAuditLogger
{
    private readonly ILogger<GeoservicesAuditLogger> _logger;

    public GeoservicesAuditLogger(ILogger<GeoservicesAuditLogger> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    /// <inheritdoc />
    public void LogFeatureAdd(
        string serviceId,
        string layerId,
        int featureCount,
        ClaimsPrincipal user,
        string? ipAddress)
    {
        var userId = ExtractUserId(user);
        var userName = ExtractUserName(user);

        _logger.LogInformation(
            "AUDIT: Feature Add - Service: {ServiceId}, Layer: {LayerId}, FeatureCount: {FeatureCount}, " +
            "UserId: {UserId}, UserName: {UserName}, IP: {IpAddress}, Timestamp: {Timestamp}",
            serviceId,
            layerId,
            featureCount,
            userId,
            userName,
            ipAddress ?? "Unknown",
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public void LogFeatureUpdate(
        string serviceId,
        string layerId,
        IEnumerable<object> featureIds,
        ClaimsPrincipal user,
        string? ipAddress)
    {
        var userId = ExtractUserId(user);
        var userName = ExtractUserName(user);
        var featureIdList = featureIds.ToList();
        var featureCount = featureIdList.Count;
        var featureIdString = featureCount > 10
            ? $"{string.Join(", ", featureIdList.Take(10))}... ({featureCount} total)"
            : string.Join(", ", featureIdList);

        _logger.LogInformation(
            "AUDIT: Feature Update - Service: {ServiceId}, Layer: {LayerId}, FeatureIds: [{FeatureIds}], " +
            "FeatureCount: {FeatureCount}, UserId: {UserId}, UserName: {UserName}, IP: {IpAddress}, Timestamp: {Timestamp}",
            serviceId,
            layerId,
            featureIdString,
            featureCount,
            userId,
            userName,
            ipAddress ?? "Unknown",
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public void LogFeatureDelete(
        string serviceId,
        string layerId,
        IEnumerable<object> featureIds,
        ClaimsPrincipal user,
        string? ipAddress)
    {
        var userId = ExtractUserId(user);
        var userName = ExtractUserName(user);
        var featureIdList = featureIds.ToList();
        var featureCount = featureIdList.Count;
        var featureIdString = featureCount > 10
            ? $"{string.Join(", ", featureIdList.Take(10))}... ({featureCount} total)"
            : string.Join(", ", featureIdList);

        _logger.LogInformation(
            "AUDIT: Feature Delete - Service: {ServiceId}, Layer: {LayerId}, FeatureIds: [{FeatureIds}], " +
            "FeatureCount: {FeatureCount}, UserId: {UserId}, UserName: {UserName}, IP: {IpAddress}, Timestamp: {Timestamp}",
            serviceId,
            layerId,
            featureIdString,
            featureCount,
            userId,
            userName,
            ipAddress ?? "Unknown",
            DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public void LogAttachmentOperation(
        string operation,
        string serviceId,
        string layerId,
        object featureId,
        string fileName,
        ClaimsPrincipal user,
        string? ipAddress)
    {
        var userId = ExtractUserId(user);
        var userName = ExtractUserName(user);

        _logger.LogInformation(
            "AUDIT: Attachment {Operation} - Service: {ServiceId}, Layer: {LayerId}, FeatureId: {FeatureId}, " +
            "FileName: {FileName}, UserId: {UserId}, UserName: {UserName}, IP: {IpAddress}, Timestamp: {Timestamp}",
            operation,
            serviceId,
            layerId,
            featureId,
            fileName,
            userId,
            userName,
            ipAddress ?? "Unknown",
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Extracts the user ID from the ClaimsPrincipal.
    /// Delegates to UserIdentityHelper for consistent identity resolution.
    /// </summary>
    private static string ExtractUserId(ClaimsPrincipal user)
    {
        return UserIdentityHelper.GetUserIdentifier(user);
    }

    /// <summary>
    /// Extracts the user name from the ClaimsPrincipal.
    /// </summary>
    private static string ExtractUserName(ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "Anonymous";
        }

        var userName = user.FindFirst(ClaimTypes.Name)?.Value
                       ?? user.FindFirst("name")?.Value
                       ?? user.FindFirst(ClaimTypes.Email)?.Value
                       ?? user.Identity.Name
                       ?? "Unknown";

        return userName;
    }
}
