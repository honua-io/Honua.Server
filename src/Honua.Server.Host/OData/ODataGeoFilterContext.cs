// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Honua.Server.Core.Query;

namespace Honua.Server.Host.OData;

internal static class ODataGeoFilterContext
{
    public const string OriginalFilterKey = "__honua_odata_original_filter";
    public const string GeoIntersectsInfoKey = "__honua_odata_geo_intersects";
    public const string GeoIntersectsPushdownKey = "__honua_odata_geo_intersects_pushdown";
    public const string GeoIntersectsPushdownAppliedKey = "__honua_odata_geo_intersects_pushdown_applied";
}

public sealed record GeoIntersectsFilterInfo(string Field, QueryGeometryValue Geometry, int? StorageSrid)
{
    public int? TargetSrid { get; set; }
}
