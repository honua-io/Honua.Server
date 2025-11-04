// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Honua.Server.Core.Query.Expressions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query.Filter;

public sealed record QueryFilter(QueryExpression? Expression);
