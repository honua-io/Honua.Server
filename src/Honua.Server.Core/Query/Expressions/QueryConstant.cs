// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Query.Expressions;

public sealed class QueryConstant : QueryExpression
{
    public QueryConstant(object? value)
    {
        Value = value;
    }

    public object? Value { get; }
}
