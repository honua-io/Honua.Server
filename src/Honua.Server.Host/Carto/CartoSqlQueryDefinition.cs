// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;

namespace Honua.Server.Host.Carto;

internal sealed record CartoSqlQueryDefinition
(
    string DatasetId,
    bool IsCount,
    bool IsDistinct,
    IReadOnlyList<CartoSqlProjection> Projections,
    IReadOnlyList<CartoSqlAggregateDefinition> Aggregates,
    IReadOnlyList<string> GroupBy,
    int? Limit,
    int? Offset,
    string? CountAlias,
    string? WhereClause,
    IReadOnlyList<CartoSqlSortDefinition> SortOrders
)
{
    public bool SelectsAll => Projections.Count == 0 && Aggregates.Count == 0 && !IsCount;

    public bool HasAggregates => Aggregates.Count > 0;

    public bool HasGrouping => GroupBy.Count > 0;
};

internal sealed record CartoSqlProjection(string Source, string OutputName, bool WasQuotedAlias);

internal sealed record CartoSqlAggregateDefinition(CartoSqlAggregateFunction Function, string OutputName, string? TargetField);

internal enum CartoSqlAggregateFunction
{
    Count,
    Sum,
    Avg,
    Min,
    Max
}

internal sealed record CartoSqlSortDefinition(string Field, bool Descending);
