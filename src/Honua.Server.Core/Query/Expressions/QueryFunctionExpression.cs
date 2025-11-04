// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Expressions;

public sealed class QueryFunctionExpression : QueryExpression
{
    public QueryFunctionExpression(string name, IReadOnlyList<QueryExpression> arguments)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }

    public string Name { get; }
    public IReadOnlyList<QueryExpression> Arguments { get; }
}
