// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Standardized parameter names for database queries.
/// Using constants ensures consistency and prevents typos.
/// </summary>
public static class ParameterNames
{
    // Common parameters
    public const string Id = "@id";
    public const string Key = "@key";
    public const string Value = "@value";
    public const string Name = "@name";
    public const string Type = "@type";

    // User/Auth parameters
    public const string UserId = "@userId";
    public const string Username = "@username";
    public const string Email = "@email";
    public const string Subject = "@subject";
    public const string RoleId = "@roleId";
    public const string Password = "@password";
    public const string PasswordHash = "@hash";
    public const string PasswordSalt = "@salt";
    public const string HashAlgorithm = "@algorithm";
    public const string HashParameters = "@parameters";
    public const string PasswordChangedAt = "@passwordChangedAt";
    public const string PasswordExpiresAt = "@passwordExpiresAt";
    public const string FailedAttempts = "@failedAttempts";
    public const string FailedAt = "@failedAt";
    public const string LoginAt = "@loginAt";
    public const string IsActive = "@isActive";
    public const string IsLocked = "@isLocked";

    // Audit parameters
    public const string Action = "@action";
    public const string Details = "@details";
    public const string OldValue = "@oldValue";
    public const string NewValue = "@newValue";
    public const string ActorId = "@actorId";
    public const string IpAddress = "@ipAddress";
    public const string UserAgent = "@userAgent";
    public const string OccurredAt = "@occurredAt";
    public const string Cutoff = "@cutoff";

    // Geometry/Spatial parameters
    public const string Geometry = "@geometry";
    public const string GeometryWkt = "@geometryWkt";
    public const string GeometryGeoJson = "@geometryGeoJson";
    public const string Srid = "@srid";
    public const string Bbox = "@bbox";
    public const string BboxMinX = "@bboxMinX";
    public const string BboxMinY = "@bboxMinY";
    public const string BboxMaxX = "@bboxMaxX";
    public const string BboxMaxY = "@bboxMaxY";

    // Query parameters
    public const string Filter = "@filter";
    public const string Limit = "@limit";
    public const string Offset = "@offset";
    public const string OrderBy = "@orderBy";
    public const string SortOrder = "@sortOrder";

    // Metadata parameters
    public const string Schema = "@schema";
    public const string Table = "@table";
    public const string Column = "@column";
    public const string DataSource = "@dataSource";
    public const string Service = "@service";
    public const string Layer = "@layer";

    // Temporal parameters
    public const string DateTime = "@datetime";
    public const string StartTime = "@startTime";
    public const string EndTime = "@endTime";
    public const string CreatedAt = "@createdAt";
    public const string UpdatedAt = "@updatedAt";

    // Raster parameters
    public const string TileZ = "@tileZ";
    public const string TileX = "@tileX";
    public const string TileY = "@tileY";
    public const string Band = "@band";
    public const string Resolution = "@resolution";

    // Configuration parameters
    public const string Mode = "@mode";
    public const string Status = "@status";
    public const string Metadata = "@metadata";
    public const string Options = "@options";
    public const string Settings = "@settings";
}
