// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Metadata;

public sealed record FolderDefinition
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public int? Order { get; init; }
}
