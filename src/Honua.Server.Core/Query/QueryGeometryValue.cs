// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query;

public sealed record QueryGeometryValue
{
    public QueryGeometryValue(string wellKnownText, int? srid)
    {
        WellKnownText = wellKnownText ?? throw new ArgumentNullException(nameof(wellKnownText));
        Srid = srid;
    }

    public string WellKnownText { get; }
    public int? Srid { get; }
}
