// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.IO;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Rendering;

public sealed record RasterRenderResult(Stream Content, string ContentType, int Width, int Height);
