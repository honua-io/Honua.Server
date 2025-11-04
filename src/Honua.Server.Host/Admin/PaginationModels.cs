// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Server.Host.Admin;

/// <summary>
/// Represents a paginated response containing a subset of items with pagination metadata.
/// </summary>
/// <typeparam name="T">The type of items in the response.</typeparam>
/// <param name="Items">The items for the current page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="NextPageToken">The token to retrieve the next page, or null if this is the last page.</param>
public record PaginatedResponse<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    string? NextPageToken
);
