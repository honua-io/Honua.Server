// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;

namespace Honua.Server.Host.Records;

internal sealed record RecordsLandingResponse
{
    public string Title { get; init; } = "Honua Records";
    public IReadOnlyList<string> ConformsTo { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecordLink> Links { get; init; } = Array.Empty<RecordLink>();
}

internal sealed record RecordsCollectionsResponse
{
    public IReadOnlyList<RecordCollection> Collections { get; init; } = Array.Empty<RecordCollection>();
    public IReadOnlyList<RecordLink> Links { get; init; } = Array.Empty<RecordLink>();
}

internal sealed record RecordCollection
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ItemType { get; init; } = "record";
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public RecordExtent? Extent { get; init; }
    public IReadOnlyList<RecordLink> Links { get; init; } = Array.Empty<RecordLink>();
}

internal sealed record RecordItemsResponse
{
    public string CollectionId { get; init; } = string.Empty;
    public long NumberMatched { get; init; }
    public long NumberReturned { get; init; }
    public DateTimeOffset TimeStamp { get; init; }
    public IReadOnlyList<RecordResponse> Items { get; init; } = Array.Empty<RecordResponse>();
    public IReadOnlyList<RecordLink> Links { get; init; } = Array.Empty<RecordLink>();
}

internal sealed record RecordSearchResponse
{
    public long NumberMatched { get; init; }
    public long NumberReturned { get; init; }
    public DateTimeOffset TimeStamp { get; init; }
    public IReadOnlyList<RecordResponse> Items { get; init; } = Array.Empty<RecordResponse>();
    public IReadOnlyList<RecordLink> Links { get; init; } = Array.Empty<RecordLink>();
}

internal sealed record RecordResponse
{
    public string Type { get; init; } = "Record";
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Themes { get; init; } = Array.Empty<string>();
    public RecordExtent? Extent { get; init; }
    public IReadOnlyList<RecordContact> Contacts { get; init; } = Array.Empty<RecordContact>();
    public IReadOnlyList<RecordLink> Links { get; init; } = Array.Empty<RecordLink>();
    public string? Thumbnail { get; init; }
    public string? ServiceId { get; init; }
    public string? LayerId { get; init; }
    public string? GroupId { get; init; }
}

internal sealed record RecordExtent
{
    public RecordSpatialExtent? Spatial { get; init; }
    public RecordTemporalExtent? Temporal { get; init; }
}

internal sealed record RecordSpatialExtent
{
    public IReadOnlyList<double[]> Bbox { get; init; } = Array.Empty<double[]>();
    public string? Crs { get; init; }
}

internal sealed record RecordTemporalExtent
{
    public string? Start { get; init; }
    public string? End { get; init; }
}

internal sealed record RecordContact
{
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Organization { get; init; }
    public string? Phone { get; init; }
    public string? Url { get; init; }
    public string? Role { get; init; }
}

internal sealed record RecordLink
{
    public string Rel { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public string? Type { get; init; }
    public string? Title { get; init; }
}
