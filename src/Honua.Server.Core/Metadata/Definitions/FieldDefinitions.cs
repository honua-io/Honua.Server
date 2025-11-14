// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

public sealed record FieldDefinition
{
    public required string Name { get; init; }
    public string? Alias { get; init; }
    public string? DataType { get; init; }
    public string? StorageType { get; init; }
    public bool Nullable { get; init; } = true;
    public bool Editable { get; init; } = true;
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public FieldDomainDefinition? Domain { get; init; }
}

public sealed record FieldDomainDefinition
{
    public required string Type { get; init; }  // "codedValue" or "range"
    public string? Name { get; init; }
    public IReadOnlyList<CodedValueDefinition>? CodedValues { get; init; }
    public RangeDomainDefinition? Range { get; init; }
}

public sealed record CodedValueDefinition
{
    public required string Name { get; init; }
    public required object Code { get; init; }
}

public sealed record RangeDomainDefinition
{
    public required object MinValue { get; init; }
    public required object MaxValue { get; init; }
}
