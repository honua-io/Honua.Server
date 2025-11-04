// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Rendering;

public interface IRasterRenderer
{
    Task<RasterRenderResult> RenderAsync(RasterRenderRequest request, CancellationToken cancellationToken = default);
}
