// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Stac.Cql2;

/// <summary>
/// Base class for all CQL2 expressions.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(Cql2LogicalExpression), typeDiscriminator: "and")]
[JsonDerivedType(typeof(Cql2LogicalExpression), typeDiscriminator: "or")]
[JsonDerivedType(typeof(Cql2NotExpression), typeDiscriminator: "not")]
[JsonDerivedType(typeof(Cql2ComparisonExpression), typeDiscriminator: "=")]
[JsonDerivedType(typeof(Cql2ComparisonExpression), typeDiscriminator: "<>")]
[JsonDerivedType(typeof(Cql2ComparisonExpression), typeDiscriminator: "<")]
[JsonDerivedType(typeof(Cql2ComparisonExpression), typeDiscriminator: "<=")]
[JsonDerivedType(typeof(Cql2ComparisonExpression), typeDiscriminator: ">")]
[JsonDerivedType(typeof(Cql2ComparisonExpression), typeDiscriminator: ">=")]
[JsonDerivedType(typeof(Cql2IsNullExpression), typeDiscriminator: "isNull")]
[JsonDerivedType(typeof(Cql2LikeExpression), typeDiscriminator: "like")]
[JsonDerivedType(typeof(Cql2BetweenExpression), typeDiscriminator: "between")]
[JsonDerivedType(typeof(Cql2InExpression), typeDiscriminator: "in")]
[JsonDerivedType(typeof(Cql2SpatialExpression), typeDiscriminator: "s_intersects")]
[JsonDerivedType(typeof(Cql2TemporalExpression), typeDiscriminator: "t_intersects")]
[JsonDerivedType(typeof(Cql2TemporalExpression), typeDiscriminator: "anyinteracts")]
public abstract record Cql2Expression
{
    /// <summary>
    /// The CQL2 operator for this expression.
    /// </summary>
    [JsonPropertyName("op")]
    public abstract string Operator { get; }
}

/// <summary>
/// Logical expression (AND, OR).
/// </summary>
public sealed record Cql2LogicalExpression : Cql2Expression
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Expression> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => Op;
}

/// <summary>
/// NOT expression.
/// </summary>
public sealed record Cql2NotExpression : Cql2Expression
{
    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Expression> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => "not";
}

/// <summary>
/// Comparison expression (equals, not equals, less than, less than or equal, greater than, greater than or equal).
/// </summary>
public sealed record Cql2ComparisonExpression : Cql2Expression
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => Op;
}

/// <summary>
/// IS NULL expression.
/// </summary>
public sealed record Cql2IsNullExpression : Cql2Expression
{
    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => "isNull";
}

/// <summary>
/// LIKE expression for pattern matching.
/// </summary>
public sealed record Cql2LikeExpression : Cql2Expression
{
    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => "like";
}

/// <summary>
/// BETWEEN expression.
/// </summary>
public sealed record Cql2BetweenExpression : Cql2Expression
{
    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => "between";
}

/// <summary>
/// IN expression.
/// </summary>
public sealed record Cql2InExpression : Cql2Expression
{
    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => "in";
}

/// <summary>
/// Spatial expression (S_INTERSECTS, etc.).
/// </summary>
public sealed record Cql2SpatialExpression : Cql2Expression
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => Op;
}

/// <summary>
/// Temporal expression (T_INTERSECTS, ANYINTERACTS).
/// </summary>
public sealed record Cql2TemporalExpression : Cql2Expression
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("args")]
    public required IReadOnlyList<Cql2Operand> Arguments { get; init; }

    [JsonIgnore]
    public override string Operator => Op;
}

/// <summary>
/// Base class for CQL2 operands (property references and literals).
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(Cql2PropertyRef), typeDiscriminator: "property")]
public abstract record Cql2Operand;

/// <summary>
/// Property reference operand.
/// </summary>
public sealed record Cql2PropertyRef : Cql2Operand
{
    [JsonPropertyName("property")]
    public required string Property { get; init; }
}

/// <summary>
/// Literal value operand.
/// </summary>
public sealed record Cql2Literal : Cql2Operand
{
    [JsonPropertyName("value")]
    public object? Value { get; init; }

    public Cql2Literal(object? value)
    {
        Value = value;
    }
}
