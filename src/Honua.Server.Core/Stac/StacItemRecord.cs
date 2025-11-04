// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Stac;

public sealed record StacItemRecord
{
    public required string Id { get; init; }
    public required string CollectionId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public JsonObject? Properties { get; init; }
    public IReadOnlyDictionary<string, StacAsset> Assets { get; init; } = new Dictionary<string, StacAsset>();
    public IReadOnlyList<StacLink> Links { get; init; } = Array.Empty<StacLink>();
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
    public double[]? Bbox { get; init; }
    public string? Geometry { get; init; }
    public DateTimeOffset? Datetime { get; init; }
    public DateTimeOffset? StartDatetime { get; init; }
    public DateTimeOffset? EndDatetime { get; init; }
    public string? RasterDatasetId { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
