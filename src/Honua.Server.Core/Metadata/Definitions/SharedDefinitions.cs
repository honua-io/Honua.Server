// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;

namespace Honua.Server.Core.Metadata;

public sealed record LinkDefinition
{
    public required string Href { get; init; }
    public string? Rel { get; init; }
    public string? Type { get; init; }
    public string? Title { get; init; }
}

public sealed record TemporalIntervalDefinition
{
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}
