// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Expressions;

/// <summary>
/// Defines spatial predicates for WFS filter operations.
/// </summary>
public enum SpatialPredicate
{
    Intersects,
    Contains,
    Within,
    Overlaps,
    Crosses,
    Touches,
    Disjoint,
    Equals,
    DWithin,
    Beyond
}
