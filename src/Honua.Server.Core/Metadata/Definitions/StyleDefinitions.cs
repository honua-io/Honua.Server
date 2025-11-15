// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

public sealed record StyleDefinition
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public string Renderer { get; init; } = "simple";
    public string Format { get; init; } = "legacy";
    public string GeometryType { get; init; } = "polygon";
    public IReadOnlyList<StyleRuleDefinition> Rules { get; init; } = Array.Empty<StyleRuleDefinition>();
    public SimpleStyleDefinition? Simple { get; init; }
    public UniqueValueStyleDefinition? UniqueValue { get; init; }
}

public sealed record StyleRuleDefinition
{
    public required string Id { get; init; }
    public bool IsDefault { get; init; }
    public string? Label { get; init; }
    public RuleFilterDefinition? Filter { get; init; }
    public double? MinScale { get; init; }
    public double? MaxScale { get; init; }
    public SimpleStyleDefinition Symbolizer { get; init; } = new();
}

public sealed record RuleFilterDefinition(string Field, string Value);

public sealed record SimpleStyleDefinition
{
    public string? Label { get; init; }
    public string? Description { get; init; }
    public string SymbolType { get; init; } = "shape";
    public string? FillColor { get; init; }
    public string? StrokeColor { get; init; }
    public double? StrokeWidth { get; init; }
    public string? StrokeStyle { get; init; }
    public string? IconHref { get; init; }
    public double? Size { get; init; }
    public double? Opacity { get; init; }
}

public sealed record UniqueValueStyleDefinition
{
    public required string Field { get; init; }
    public SimpleStyleDefinition? DefaultSymbol { get; init; }
    public IReadOnlyList<UniqueValueStyleClassDefinition> Classes { get; init; } = Array.Empty<UniqueValueStyleClassDefinition>();
}

public sealed record UniqueValueStyleClassDefinition
{
    public required string Value { get; init; }
    public SimpleStyleDefinition Symbol { get; init; } = new();
}
