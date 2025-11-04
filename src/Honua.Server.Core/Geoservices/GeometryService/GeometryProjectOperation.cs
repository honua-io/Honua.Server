// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public sealed record GeometryProjectOperation(
    string GeometryType,
    int InputSpatialReference,
    int OutputSpatialReference,
    IReadOnlyList<Geometry> Geometries);
