// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Query.Expressions;

public sealed class QueryUnaryExpression : QueryExpression
{
    public QueryUnaryExpression(QueryUnaryOperator op, QueryExpression operand)
    {
        Operator = op;
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public QueryUnaryOperator Operator { get; }
    public QueryExpression Operand { get; }
}
