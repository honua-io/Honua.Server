// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Caching;

public readonly record struct RasterTileCacheEntry
{
    public RasterTileCacheEntry(ReadOnlyMemory<byte> content, string? contentType, DateTimeOffset createdUtc)
    {
        Content = content;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        CreatedUtc = createdUtc;
    }

    public ReadOnlyMemory<byte> Content { get; }

    public string ContentType { get; }

    public DateTimeOffset CreatedUtc { get; }
}

public readonly record struct RasterTileCacheHit
{
    public RasterTileCacheHit(ReadOnlyMemory<byte> content, string? contentType, DateTimeOffset createdUtc)
    {
        Content = content;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        CreatedUtc = createdUtc;
    }

    public ReadOnlyMemory<byte> Content { get; }

    public string ContentType { get; }

    public DateTimeOffset CreatedUtc { get; }
}
