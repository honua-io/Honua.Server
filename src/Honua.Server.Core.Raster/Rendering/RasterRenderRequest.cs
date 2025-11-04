// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Rendering;

public sealed record RasterRenderRequest(
    RasterDatasetDefinition Dataset,
    double[] BoundingBox,
    int Width,
    int Height,
    string SourceCrs,
    string TargetCrs,
    string Format,
    bool Transparent,
    string? StyleId,
    StyleDefinition? Style = null,
    IReadOnlyList<Geometry>? VectorGeometries = null,
    IReadOnlyList<RasterLayerRequest>? AdditionalLayers = null,
    string? Time = null);

public sealed record RasterLayerRequest(
    RasterDatasetDefinition Dataset,
    string? StyleId,
    StyleDefinition? Style = null,
    string? Time = null);
