// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Honua.Server.Core.Stac;

public sealed record StacCollectionRecord
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    /// <summary>
    /// License identifier for the collection. REQUIRED by STAC 1.0+ specification.
    /// Use SPDX license identifier (e.g., "CC-BY-4.0", "MIT") or "proprietary" or "various".
    /// </summary>
    public required string License { get; init; }
    public string? Version { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<StacLink> Links { get; init; } = Array.Empty<StacLink>();
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
    public StacExtent Extent { get; init; } = StacExtent.Empty;
    public JsonObject? Properties { get; init; }
    public string? ConformsTo { get; init; }
    public string? DataSourceId { get; init; }
    public string? ServiceId { get; init; }
    public string? LayerId { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
