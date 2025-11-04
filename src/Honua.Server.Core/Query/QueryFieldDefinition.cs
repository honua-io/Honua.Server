// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Query;

public sealed record QueryFieldDefinition
{
    public required string Name { get; init; }
    public required QueryDataType DataType { get; init; }
    public bool Nullable { get; init; }
    public bool IsKey { get; init; }
    public bool IsGeometry { get; init; }
}
