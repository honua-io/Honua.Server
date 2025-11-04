// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.Configuration;

/// <summary>
/// Configuration options for API pagination.
/// </summary>
public sealed class PaginationOptions
{
    /// <summary>
    /// Default number of items to return when limit is not specified.
    /// </summary>
    [Range(1, 10000)]
    public int DefaultLimit { get; set; } = 10;

    /// <summary>
    /// Maximum number of items that can be returned in a single request.
    /// </summary>
    [Range(1, 10000)]
    public int MaxLimit { get; set; } = 1000;

    /// <summary>
    /// Default number of items for STAC collections listing.
    /// </summary>
    [Range(1, 10000)]
    public int StacCollectionDefaultLimit { get; set; } = 10;

    /// <summary>
    /// Maximum number of items for STAC collections listing.
    /// </summary>
    [Range(1, 10000)]
    public int StacCollectionMaxLimit { get; set; } = 1000;

    /// <summary>
    /// Default number of items for STAC items listing.
    /// </summary>
    [Range(1, 10000)]
    public int StacItemDefaultLimit { get; set; } = 10;

    /// <summary>
    /// Maximum number of items for STAC items listing.
    /// </summary>
    [Range(1, 10000)]
    public int StacItemMaxLimit { get; set; } = 1000;

    /// <summary>
    /// Default number of items for STAC search results.
    /// </summary>
    [Range(1, 10000)]
    public int StacSearchDefaultLimit { get; set; } = 10;

    /// <summary>
    /// Maximum number of items for STAC search results.
    /// </summary>
    [Range(1, 10000)]
    public int StacSearchMaxLimit { get; set; } = 1000;
}
