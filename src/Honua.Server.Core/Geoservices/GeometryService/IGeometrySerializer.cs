// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public interface IGeometrySerializer
{
    IReadOnlyList<Geometry> DeserializeGeometries(JsonNode? payload, string geometryType, int srid, CancellationToken cancellationToken = default);

    JsonObject SerializeGeometries(IReadOnlyList<Geometry> geometries, string geometryType, int srid, CancellationToken cancellationToken = default);

    JsonObject SerializeGeometry(Geometry geometry, string geometryType, int srid);
}
