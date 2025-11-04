// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Query.Expressions;

public sealed class QueryBinaryExpression : QueryExpression
{
    public QueryBinaryExpression(QueryExpression left, QueryBinaryOperator op, QueryExpression right)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        Operator = op;
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public QueryExpression Left { get; }
    public QueryBinaryOperator Operator { get; }
    public QueryExpression Right { get; }
}
