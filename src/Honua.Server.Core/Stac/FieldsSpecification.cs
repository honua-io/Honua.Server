// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Represents the fields specification for filtering STAC Item responses.
/// Implements the STAC API Fields Extension.
/// </summary>
/// <remarks>
/// The Fields Extension allows clients to request inclusion or exclusion of specific fields
/// from STAC Item responses, reducing payload size when full items aren't needed.
///
/// Reference: https://github.com/stac-api-extensions/fields
/// </remarks>
public sealed record FieldsSpecification
{
    /// <summary>
    /// Fields to include in the response. If specified, only these fields will be returned.
    /// Nested fields are supported using dot notation (e.g., "properties.datetime").
    /// </summary>
    [JsonPropertyName("include")]
    public IReadOnlySet<string>? Include { get; init; }

    /// <summary>
    /// Fields to exclude from the response. All fields except these will be returned.
    /// Nested fields are supported using dot notation (e.g., "properties.metadata").
    /// </summary>
    [JsonPropertyName("exclude")]
    public IReadOnlySet<string>? Exclude { get; init; }

    /// <summary>
    /// Gets the default field set that should be returned when no fields parameter is specified.
    /// According to spec: type, stac_version, id, geometry, bbox, links, assets, properties.datetime
    /// (or start_datetime/end_datetime if datetime is null).
    /// </summary>
    public static readonly FieldsSpecification Default = new()
    {
        Include = new HashSet<string>(new[]
        {
            "type",
            "stac_version",
            "id",
            "geometry",
            "bbox",
            "links",
            "assets",
            "properties.datetime",
            "properties.start_datetime",
            "properties.end_datetime"
        })
    };

    /// <summary>
    /// Returns true if this specification has no include or exclude filters.
    /// </summary>
    public bool IsEmpty => (Include is null || Include.Count == 0) && (Exclude is null || Exclude.Count == 0);

    /// <summary>
    /// Returns true if this specification is using include mode (only specified fields returned).
    /// </summary>
    public bool IsIncludeMode => Include is not null && Include.Count > 0;

    /// <summary>
    /// Returns true if this specification is using exclude mode (all fields except specified returned).
    /// </summary>
    public bool IsExcludeMode => !IsIncludeMode && Exclude is not null && Exclude.Count > 0;
}
